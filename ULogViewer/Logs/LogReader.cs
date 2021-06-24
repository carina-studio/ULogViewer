using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Log reader.
	/// </summary>
	class LogReader : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Constants.
		const int FlushPendingLogsInterval = 100;
		const int LogsReadingChunkSize = 1024;


		// Static fields.
		static int nextId = 1;


		// Fields.
		readonly ScheduledAction flushPendingLogsAction;
		bool isContinuousReading;
		readonly Dictionary<string, LogLevel> logLevelMap = new Dictionary<string, LogLevel>();
		IList<LogPattern> logPatterns = new LogPattern[0];
		readonly ObservableCollection<Log> logs = new ObservableCollection<Log>();
		CancellationTokenSource? logsReadingCancellationTokenSource;
		readonly List<Log> pendingLogs = new List<Log>();
		readonly IDictionary<string, LogLevel> readOnlyLogLevelMap;
		LogReaderState state = LogReaderState.Preparing;
		string? timestampFormat;


		/// <summary>
		/// Initialize new <see cref="LogReader"/> instance.
		/// </summary>
		/// <param name="dataSource"><see cref="ILogDataSource"/> to read log data from.</param>
		/// <param name="logPatterns">Log patterns to parse log data.</param>
		public LogReader(ILogDataSource dataSource, IEnumerable<LogPattern> logPatterns)
		{
			// check thread
			dataSource.VerifyAccess();

			// setup properties
			this.Application = (IApplication)dataSource.Application;
			this.DataSource = dataSource;
			this.Id = nextId++;
			this.Logger = dataSource.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.Logs = new ReadOnlyObservableCollection<Log>(this.logs);
			this.readOnlyLogLevelMap = new ReadOnlyDictionary<string, LogLevel>(this.logLevelMap);

			// create scheduled actions
			this.flushPendingLogsAction = new ScheduledAction(() =>
			{
				if (this.pendingLogs.IsEmpty())
					return;
				if (!this.CanAddLogs)
					return;
				var logs = this.logs;
				foreach (var log in this.pendingLogs)
					logs.Add(log);
				this.pendingLogs.Clear();
			});

			// attach to data source
			dataSource.PropertyChanged += this.OnDataSourcePropertyChanged;

			this.Logger.LogDebug($"Create with data source: {dataSource}");
		}


		/// <summary>
		/// Get <see cref="IApplication"/> instance.
		/// </summary>
		public IApplication Application { get; }


		// Whether read logs can be added or not.
		bool CanAddLogs
		{
			get => this.state switch
			{
				LogReaderState.Starting => true,
				LogReaderState.ReadingLogs => true,
				LogReaderState.Stopping => true,
				_ => false,
			};
		}


		// Change state.
		bool ChangeState(LogReaderState state)
		{
			var prevState = this.state;
			if (prevState == state)
				return true;
			this.state = state;
			this.Logger.LogDebug($"Change state from {prevState} to {state}");
			this.OnPropertyChanged(nameof(State));
			return (this.state == state);
		}


		/// <summary>
		/// Get <see cref="ILogDataSource"/> to read log data from.
		/// </summary>
		public ILogDataSource DataSource { get; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// check state.
			if (disposing)
				this.VerifyAccess();
			else
				return; // ignore releasing managed resources

			// change state
			this.ChangeState(LogReaderState.Disposed);

			// detach from data source
			this.DataSource.PropertyChanged -= this.OnDataSourcePropertyChanged;

			// cancel reading logs
			this.logsReadingCancellationTokenSource?.Let(it =>
			{
				this.Logger.LogWarning("Cancel reading logs because of disposing");
				it.Cancel();
				it.Dispose();
				this.logsReadingCancellationTokenSource = null;
			});
			this.flushPendingLogsAction.Cancel();

			// clear logs
			this.pendingLogs.Clear();
			this.logs.Clear();
		}


		/// <summary>
		/// Get unique ID of this <see cref="LogReader"/> instance.
		/// </summary>
		public int Id { get; }


		/// <summary>
		/// Get or set whether restart reading is needed when reaching end of log data.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public bool IsContinuousReading
		{
			get => this.isContinuousReading;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(IsContinuousReading)} when state is {this.state}.");
				if (this.isContinuousReading == value)
					return;
				this.isContinuousReading = value;
				this.OnPropertyChanged(nameof(IsContinuousReading));
			}
		}


		/// <summary>
		/// Get or set log patterns to parse log data.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public IList<LogPattern> LogPatterns 
		{
			get => this.logPatterns;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogPatterns)} when state is {this.state}.");
				if (this.logPatterns.SequenceEqual(value))
					return;
				this.logPatterns = value.ToList().AsReadOnly();
				this.OnPropertyChanged(nameof(LogPatterns));
			}
		}


		/// <summary>
		/// Get list of read <see cref="Log"/>s.
		/// </summary>
		/// <remarks>The list implements <see cref="INotifyCollectionChanged"/> interface.</remarks>
		public IList<Log> Logs { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Get or set <see cref="IDictionary{TKey, TValue}"/> to map from string to <see cref="LogLevel"/>.
		/// </summary>
		public IDictionary<string, LogLevel> LogLevelMap
		{
			get => this.readOnlyLogLevelMap;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogLevelMap)} when state is {this.state}.");
				if (this.logLevelMap.SequenceEqual(value))
					return;
				this.logLevelMap.Clear();
				foreach (var pair in value)
					this.logLevelMap.Add(pair.Key, pair.Value);
				this.OnPropertyChanged(nameof(LogLevelMap));
			}
		}


		// Called when property of data source has been changed.
		void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => this.OnDataSourcePropertyChanged(e);


		/// <summary>
		/// Called when property of <see cref="DataSource"/> has been changed.
		/// </summary>
		/// <param name="e">Event data.</param>
		protected virtual void OnDataSourcePropertyChanged(PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataSource.State))
				this.OnDataSourceStateChanged(this.DataSource.State);
		}


		/// <summary>
		/// Called when state of <see cref="DataSource"/> has been changed.
		/// </summary>
		/// <param name="state">New state.</param>
		protected virtual void OnDataSourceStateChanged(LogDataSourceState state)
		{
			if (state == LogDataSourceState.ReadyToOpenReader && this.state == LogReaderState.Starting)
			{
				this.Logger.LogWarning("Data source is ready to open reader, start reading logs");
				this.StartReadingLogs();
			}
		}


		// Called when logs read.
		void OnLogRead(Log log)
		{
			if (!this.CanAddLogs)
				return;
			if (this.isContinuousReading)
			{
				this.pendingLogs.Add(log);
				this.flushPendingLogsAction.Schedule(FlushPendingLogsInterval);
			}
			else
				this.logs.Add(log);
		}


		// Called when logs read.
		void OnLogsRead(IEnumerable<Log> readLogs)
		{
			if (!this.CanAddLogs)
				return;
			if (this.isContinuousReading)
			{
				this.pendingLogs.AddRange(readLogs);
				this.flushPendingLogsAction.Schedule(FlushPendingLogsInterval);
			}
			else
			{
				var logs = this.logs;
				foreach (var log in readLogs)
					logs.Add(log);
			}
		}


		// Called when all logs read.
		void OnLogsReadingCompleted(Exception? ex)
		{
			// check state
			if (this.state != LogReaderState.ReadingLogs)
				return;

			this.Logger.LogWarning("Complete reading logs");

			// release cancellation token
			this.logsReadingCancellationTokenSource = this.logsReadingCancellationTokenSource.DisposeAndReturnNull();

			// change state
			if (!this.isContinuousReading)
			{
				if (ex != null)
				{
					if (this.ChangeState(LogReaderState.Stopping))
					{
						this.flushPendingLogsAction.ExecuteIfScheduled();
						this.ChangeState(LogReaderState.Stopped);
					}
				}
				else
					this.ChangeState(LogReaderState.UnclassifiedError);
				return;
			}

			// restart reading
			this.Logger.LogWarning("Restart reading logs");
			this.StartReadingLogs();
		}


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		// Read single line of log.
		void ReadLog(LogBuilder logBuilder, Match match)
		{
			foreach (Group group in match.Groups)
			{
				var name = group.Name;
				switch(name)
				{
					case nameof(Log.Level):
						if (this.logLevelMap.TryGetValue(group.Value, out var level))
							logBuilder.Set(name, level.ToString());
						break;
					case nameof(Log.Message):
						logBuilder.AppendToNextLine(name, group.Value);
						break;
					case nameof(Log.Timestamp):
						this.timestampFormat.Let(format =>
						{
							if (format == null)
								logBuilder.Set(name, group.Value);
							else if (DateTime.TryParseExact(group.Value, format, null, System.Globalization.DateTimeStyles.None, out var timestamp))
								logBuilder.Set(name, timestamp.ToBinary().ToString());
						});
						break;
					default:
						logBuilder.Set(name, group.Value);
						break;
				}
			}
		}


		// Read logs.
		void ReadLogs(TextReader reader, CancellationToken cancellationToken)
		{
			this.Logger.LogDebug("Start reading logs in background");
			var readLogs = new List<Log>();
			var logPatterns = this.logPatterns;
			var logBuilder = new LogBuilder();
			var syncContext = this.SynchronizationContext;
			var isContinuousReading = this.isContinuousReading;
			var exception = (Exception?)null;
			try
			{
				var prevLogPattern = (LogPattern?)null;
				var logPatternIndex = 0;
				var lastLogPatternIndex = (logPatterns.Count - 1);
				var logLine = reader.ReadLine();
				while (logLine != null && !cancellationToken.IsCancellationRequested)
				{
					var logPattern = logPatterns[logPatternIndex];
					try
					{
						var match = logPattern.Regex.Match(logLine);
						if (match.Success)
						{
							// read log
							this.ReadLog(logBuilder, match);

							// creat log and move to next pattern
							if (!logPattern.IsRepeatable)
							{
								if (logPatternIndex == lastLogPatternIndex)
								{
									if (logBuilder.IsNotEmpty())
										readLogs.Add(logBuilder.BuildAndReset());
									logPatternIndex = 0;
								}
								else
									++logPatternIndex;
							}

							// read next line
							logLine = reader.ReadLine();
						}
						else if (logPattern.IsSkippable)
						{
							// move to next pattern
							if (logPatternIndex < lastLogPatternIndex)
							{
								++logPatternIndex;
								continue;
							}

							// build log if this is the last pattern
							if (logBuilder.IsNotEmpty())
								readLogs.Add(logBuilder.BuildAndReset());

							// move to first pattern
							logPatternIndex = 0;

							// need to move to next line because there is only one pattern
							if (lastLogPatternIndex == 0)
								logLine = reader.ReadLine();
						}
						else if (logPattern.IsRepeatable)
						{
							// drop this log if this pattern never be matched
							if (prevLogPattern != logPattern)
							{
								// drop log
								logBuilder.Reset();

								// move to first pattern
								logPatternIndex = 0;

								// need to move to next line because there is only one pattern
								if (lastLogPatternIndex == 0)
									logLine = reader.ReadLine();
								continue;
							}

							// move to next pattern
							if (logPatternIndex != lastLogPatternIndex)
							{
								++logPatternIndex;
								continue;
							}

							// build log if this is the last pattern
							if (logBuilder.IsNotEmpty())
								readLogs.Add(logBuilder.BuildAndReset());

							// move to first pattern
							logPatternIndex = 0;

							// need to move to next line because there is only one pattern
							if (lastLogPatternIndex == 0)
								logLine = reader.ReadLine();
						}
						else
						{
							// drop log
							logBuilder.Reset();

							// move to first pattern
							logPatternIndex = 0;

							// need to move to next line because there is only one pattern
							if (lastLogPatternIndex == 0)
								logLine = reader.ReadLine();
						}
					}
					finally
					{
						if (isContinuousReading)
						{
							if (readLogs.IsNotEmpty())
							{
								var log = readLogs[0];
								readLogs.Clear();
								syncContext.Post(() => this.OnLogRead(log));
							}
						}
						else if (readLogs.Count >= LogsReadingChunkSize)
						{
							var logArray = readLogs.ToArray();
							readLogs.Clear();
							syncContext.Post(() => this.OnLogsRead(logArray));
						}
						prevLogPattern = logPattern;
					}
				}
				if (logLine != null && cancellationToken.IsCancellationRequested)
					this.Logger.LogWarning("Logs reading has been cancelled");
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Error occurred while reading logs.");
				exception = ex;
			}
			finally
			{
				this.Logger.LogDebug("Complete reading logs in background");

				// send last chunk of logs
				if (readLogs.IsNotEmpty())
					syncContext.Post(() => this.OnLogsRead(readLogs));

				// close reader
				Global.RunWithoutError(reader.Close);

				// complete reading
				syncContext.Post(() => this.OnLogsReadingCompleted(exception));
			}
		}


		/// <summary>
		/// Start reading logs.
		/// </summary>
		public void Start()
		{
			// check state
			this.VerifyAccess();
			if (this.state != LogReaderState.Preparing)
				throw new InvalidOperationException($"Cannot start log reading when state is {this.state}.");
			if (this.logLevelMap.IsEmpty())
				throw new InvalidOperationException("No log level mapping specified.");
			if (this.logPatterns.IsEmpty())
				throw new InvalidOperationException("No log pattern specified.");

			// start
			this.StartReadingLogs();
		}


		// Start reading logs.
		async void StartReadingLogs()
		{
			// change state
			if (!this.ChangeState(LogReaderState.Starting))
				return;

			// check source state
			if (this.DataSource.State != LogDataSourceState.ReadyToOpenReader)
			{
				this.Logger.LogWarning("Wait for data source ready to open reader");
				return;
			}

			// open reader
			var reader = (TextReader?)null;
			try
			{
				this.Logger.LogDebug("Start opening reader");
				reader = await this.DataSource.OpenReaderAsync();
				this.Logger.LogDebug("Reader opened");
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to open reader");
			}

			// check state
			if (this.state != LogReaderState.Starting)
			{
				this.Logger.LogWarning($"State has been changed to {this.state} when opening reader");
				Global.RunWithoutErrorAsync(() => reader?.Close());
				return;
			}
			if (reader == null)
			{
				this.ChangeState(LogReaderState.DataSourceError);
				return;
			}

			// change state
			if (!this.ChangeState(LogReaderState.ReadingLogs))
			{
				Global.RunWithoutErrorAsync(() => reader?.Close());
				return;
			}

			// read logs
			this.logsReadingCancellationTokenSource = new CancellationTokenSource();
			var cancellationToken = this.logsReadingCancellationTokenSource.Token;
			ThreadPool.QueueUserWorkItem(_ => this.ReadLogs(reader, cancellationToken));
		}


		/// <summary>
		/// Get current state of <see cref="LogReader"/>.
		/// </summary>
		public LogReaderState State { get => this.state; }


		/// <summary>
		/// Get or set format to parse timestamp of log.
		/// </summary>
		public string? TimestampFormat
		{
			get => this.timestampFormat;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(TimestampFormat)} when state is {this.state}.");
				if (this.timestampFormat == value)
					return;
				this.timestampFormat = value;
				this.OnPropertyChanged(nameof(TimestampFormat));
			}
		}


		// Implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";
	}


	/// <summary>
	/// State of <see cref="LogReader"/>.
	/// </summary>
	enum LogReaderState
	{
		/// <summary>
		/// Preparing.
		/// </summary>
		Preparing,
		/// <summary>
		/// Starting to read logs.
		/// </summary>
		Starting,
		/// <summary>
		/// Reading logs.
		/// </summary>
		ReadingLogs,
		/// <summary>
		/// Stopping from reading logs.
		/// </summary>
		Stopping,
		/// <summary>
		/// Stopped.
		/// </summary>
		Stopped,
		/// <summary>
		/// Error caused by <see cref="LogReader.DataSource"/>.
		/// </summary>
		DataSourceError,
		/// <summary>
		/// Unclassified error.
		/// </summary>
		UnclassifiedError,
		/// <summary>
		/// Disposed.
		/// </summary>
		Disposed,
	}
}
