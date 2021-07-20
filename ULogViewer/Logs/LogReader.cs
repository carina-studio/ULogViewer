using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using CarinaStudio.ULogViewer.Logs.DataSources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Log reader.
	/// </summary>
	class LogReader : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Constants.
		const int LogsReadingChunkSize = 65536;


		// Static fields.
		static readonly TaskFactory defaultReadingTaskFactory = new TaskFactory(TaskScheduler.Default);
		static readonly CultureInfo defaultTimestampCultureInfo = CultureInfo.GetCultureInfo("en-US");
		static readonly TaskFactory fileReadingTaskFactory = new TaskFactory(new FixedThreadsTaskScheduler(2));
		static int nextId = 1;


		// Fields.
		int continuousReadingUpdateInterval = 100;
		int dropLogCount = -1;
		readonly ScheduledAction flushPendingLogsAction;
		bool isContinuousReading;
		readonly Dictionary<string, LogLevel> logLevelMap = new Dictionary<string, LogLevel>();
		IList<LogPattern> logPatterns = new LogPattern[0];
		readonly ObservableList<Log> logs = new ObservableList<Log>();
		CancellationTokenSource? logsReadingCancellationTokenSource;
		object logsReadingToken = new object();
		int maxLogCount = -1;
		readonly List<Log> pendingLogs = new List<Log>();
		readonly IDictionary<string, LogLevel> readOnlyLogLevelMap;
		LogReaderState state = LogReaderState.Preparing;
		CultureInfo timestampCultureInfo = defaultTimestampCultureInfo;
		string? timestampFormat;


		/// <summary>
		/// Initialize new <see cref="LogReader"/> instance.
		/// </summary>
		/// <param name="dataSource"><see cref="ILogDataSource"/> to read log data from.</param>
		public LogReader(ILogDataSource dataSource)
		{
			// check thread
			dataSource.VerifyAccess();

			// setup properties
			this.Application = (IApplication)dataSource.Application;
			this.DataSource = dataSource;
			this.Id = nextId++;
			this.Logger = dataSource.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.Logs = new ReadOnlyObservableList<Log>(this.logs);
			this.readOnlyLogLevelMap = new ReadOnlyDictionary<string, LogLevel>(this.logLevelMap);

			// create scheduled actions
			this.flushPendingLogsAction = new ScheduledAction(() =>
			{
				if (this.pendingLogs.IsEmpty())
					return;
				if (!this.CanAddLogs)
					return;
				this.DropLogs(this.pendingLogs.Count);
				this.logs.AddRange(this.pendingLogs);
				this.pendingLogs.Clear();
				this.DropLogs(0);
			});

			// attach to data source
			dataSource.PropertyChanged += this.OnDataSourcePropertyChanged;

			// attach to log list
			this.logs.CollectionChanged += (_, e) => this.LogsChanged?.Invoke(this, e);

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
		/// Clear all read logs.
		/// </summary>
		public void ClearLogs()
		{
			this.VerifyAccess();
			this.flushPendingLogsAction.Cancel();
			this.pendingLogs.Clear();
			this.logs.Clear();
		}


		/// <summary>
		/// Update interval of <see cref="Logs"/> in milliseconds when <see cref="IsContinuousReading"/> is true.
		/// </summary>
		public int ContinuousReadingUpdateInterval
		{
			get => this.continuousReadingUpdateInterval;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (value < 0)
					throw new ArgumentOutOfRangeException();
				if (this.continuousReadingUpdateInterval == value)
					return;
				this.continuousReadingUpdateInterval = value;
				this.OnPropertyChanged(nameof(ContinuousReadingUpdateInterval));
			}
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
		/// Get or set number of logs to drop when number of logs reaches <see cref="MaxLogCount"/>.
		/// </summary>
		/// <remarks>Negative value means dropping 10% of read logs.</remarks>
		public int DropLogCount
		{
			get => this.dropLogCount;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (value == 0)
					throw new ArgumentOutOfRangeException();
				if (this.dropLogCount == value)
					return;
				this.dropLogCount = value;
				this.OnPropertyChanged(nameof(DropLogCount));
			}
		}


		// Drop exceeded logs.
		void DropLogs(int addingLogCount)
		{
			// prepare dropping information
			if (this.maxLogCount < 0)
				return;
			var logs = this.logs;
			var logCount = logs.Count + addingLogCount;
			var droppingLogCount = (logCount - this.maxLogCount);
			if (droppingLogCount <= 0)
				return;
			if (this.dropLogCount < 0)
				droppingLogCount += (this.maxLogCount / 10);
			else
				droppingLogCount += this.dropLogCount;

			// drop logs
			if (droppingLogCount < logs.Count)
			{
				for (var i = droppingLogCount - 1; i >= 0; --i)
					logs.RemoveAt(i);
			}
			else
				logs.Clear();
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
		/// Raised when elements in <see cref="Logs"/> has been changed.
		/// </summary>
		public event NotifyCollectionChangedEventHandler? LogsChanged;


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


		/// <summary>
		/// Get or set maximum number of logs to read. Logs read earlier will be dropped if number of logs reaches <see cref="MaxLogCount"/>.
		/// </summary>
		/// <remarks>Negative number means unlimited number of logs.</remarks>
		public int MaxLogCount
		{
			get => this.maxLogCount;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (value == 0)
					throw new ArgumentOutOfRangeException();
				if (this.maxLogCount == value)
					return;
				this.maxLogCount = value;
				this.DropLogs(0);
				this.OnPropertyChanged(nameof(MaxLogCount));
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
			switch(state)
			{
				case LogDataSourceState.ReadyToOpenReader:
					switch (this.state)
					{
						case LogReaderState.Starting:
						case LogReaderState.StartingWhenPaused:
							this.Logger.LogWarning("Data source is ready to open reader, start reading logs");
							this.StartReadingLogs();
							break;
					}
					break;
				case LogDataSourceState.OpeningReader:
				case LogDataSourceState.ReaderOpened:
				case LogDataSourceState.ClosingReader:
					break;
				default:
					switch (this.state)
					{
						case LogReaderState.Starting:
						case LogReaderState.StartingWhenPaused:
						case LogReaderState.ReadingLogs:
						case LogReaderState.Paused:
							if (this.DataSource.IsErrorState())
							{
								this.Logger.LogWarning($"Data source state changed to {state}, cancel reading logs");
								this.OnLogsReadingCompleted(null);
							}
							break;
					}
					break;
			}
		}


		// Called when logs read.
		void OnLogRead(object readingToken, Log log)
		{
			if (!this.CanAddLogs || this.logsReadingToken != readingToken)
				return;
			if (this.isContinuousReading)
			{
				this.pendingLogs.Add(log);
				this.flushPendingLogsAction.Schedule(this.continuousReadingUpdateInterval);
			}
			else
			{
				this.DropLogs(1);
				this.logs.Add(log);
			}
		}


		// Called when logs read.
		void OnLogsRead(object readingToken, ICollection<Log> readLogs)
		{
			if (!this.CanAddLogs || this.logsReadingToken != readingToken)
				return;
			if (this.isContinuousReading)
			{
				this.pendingLogs.AddRange(readLogs);
				this.flushPendingLogsAction.Schedule(this.continuousReadingUpdateInterval);
			}
			else
			{
				this.DropLogs(readLogs.Count);
				this.logs.AddRange(readLogs);
				this.DropLogs(0);
			}
		}


		// Called when all logs read.
		void OnLogsReadingCompleted(Exception? ex)
		{
			// check state
			switch(this.state)
			{
				case LogReaderState.Starting:
				case LogReaderState.StartingWhenPaused:
				case LogReaderState.ReadingLogs:
				case LogReaderState.Paused:
					break;
				default:
					return;
			}

			this.Logger.LogWarning("Complete reading logs");

			// release cancellation token
			this.logsReadingCancellationTokenSource?.Let(it =>
			{
				it.Cancel();
				it.Dispose();
				this.logsReadingCancellationTokenSource = null;
			});

			// change state
			if (!this.isContinuousReading)
			{
				this.flushPendingLogsAction.ExecuteIfScheduled();
				if (this.DataSource.IsErrorState())
				{
					this.Logger.LogError($"Data source state is {this.DataSource.State} when completing reading logs");
					this.ChangeState(LogReaderState.DataSourceError);
				}
				else if (ex == null)
					this.ChangeState(LogReaderState.Stopped);
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


		/// <summary>
		/// Pause reading logs.
		/// </summary>
		/// <returns>True if logs reading has been paused successfully.</returns>
		public bool Pause()
		{
			this.VerifyAccess();
			switch (this.state)
			{
				case LogReaderState.StartingWhenPaused:
				case LogReaderState.Paused:
					return true;
				case LogReaderState.Starting:
					if (!this.isContinuousReading)
					{
						this.Logger.LogWarning($"Cannot pause logs reading when {nameof(IsContinuousReading)} is false");
						return false;
					}
					return this.ChangeState(LogReaderState.StartingWhenPaused);
				case LogReaderState.ReadingLogs:
					if (!this.isContinuousReading)
					{
						this.Logger.LogWarning($"Cannot pause logs reading when {nameof(IsContinuousReading)} is false");
						return false;
					}
					return this.ChangeState(LogReaderState.Paused);
				default:
					this.Logger.LogWarning($"Cannot pause logs reading when state is {this.state}");
					return false;
			}
		}


		// Read single line of log.
		void ReadLog(LogBuilder logBuilder, Match match, StringPool stringPool)
		{
			foreach (Group group in match.Groups)
			{
				var name = group.Name;
				switch (name)
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
							else if (DateTime.TryParseExact(group.Value, format, this.timestampCultureInfo, DateTimeStyles.None, out var timestamp))
								logBuilder.Set(name, timestamp.ToBinary().ToString());
						});
						break;
					case nameof(Log.ProcessId):
					case nameof(Log.ProcessName):
					case nameof(Log.SourceName):
					case nameof(Log.ThreadId):
					case nameof(Log.ThreadName):
					case nameof(Log.UserId):
					case nameof(Log.UserName):
						logBuilder.Set(name, stringPool[group.Value]);
						break;
					default:
						logBuilder.Set(name, group.Value);
						break;
				}
			}
		}


		// Read logs.
		void ReadLogs(object readingToken, TextReader reader, CancellationToken cancellationToken)
		{
			this.Logger.LogDebug("Start reading logs in background");
			var readLogs = new List<Log>();
			var logPatterns = this.logPatterns;
			var logBuilder = new LogBuilder();
			var syncContext = this.SynchronizationContext;
			var isContinuousReading = this.isContinuousReading;
			var isReadingFromFile = this.DataSource.UnderlyingSource == UnderlyingLogDataSource.File;
			var dataSourceOptions = this.DataSource.CreationOptions;
			var stringPool = new StringPool();
			var exception = (Exception?)null;
			try
			{
				var prevLogPattern = (LogPattern?)null;
				var logPatternIndex = 0;
				var lastLogPatternIndex = (logPatterns.Count - 1);
				var lineNumber = 1;
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
							this.ReadLog(logBuilder, match, stringPool);

							// set file name and line number
							if (logPatternIndex == 0 && isReadingFromFile)
							{
								logBuilder.Set(nameof(Log.LineNumber), lineNumber.ToString());
								dataSourceOptions.FileName?.Let(it => logBuilder.Set(nameof(Log.FileName), it));
							}

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
							++lineNumber;
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

							// need to move to next line if there is only one pattern or this is the first pattern
							if (logPatternIndex == 0 || lastLogPatternIndex == 0)
							{
								++lineNumber;
								logLine = reader.ReadLine();
							}

							// move to first pattern
							logPatternIndex = 0;
						}
						else if (logPattern.IsRepeatable)
						{
							// drop this log if this pattern never be matched
							if (prevLogPattern != logPattern)
							{
								// drop log
								logBuilder.Reset();

								// need to move to next line if there is only one pattern or this is the first pattern
								if (logPatternIndex == 0 || lastLogPatternIndex == 0)
								{
									++lineNumber;
									logLine = reader.ReadLine();
								}

								// move to first pattern
								logPatternIndex = 0;
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

							// need to move to next line if there is only one pattern or this is the first pattern
							if (logPatternIndex == 0 || lastLogPatternIndex == 0)
							{
								++lineNumber;
								logLine = reader.ReadLine();
							}

							// move to first pattern
							logPatternIndex = 0;
						}
						else
						{
							// drop log
							logBuilder.Reset();

							// need to move to next line if there is only one pattern or this is the first pattern
							if (logPatternIndex == 0 || lastLogPatternIndex == 0)
							{
								++lineNumber;
								logLine = reader.ReadLine();
							}

							// move to first pattern
							logPatternIndex = 0;
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
								syncContext.Post(() => this.OnLogRead(readingToken, log));
							}
						}
						else if (readLogs.Count >= LogsReadingChunkSize)
						{
							var logArray = readLogs.ToArray();
							readLogs.Clear();
							syncContext.PostDelayed(() => this.OnLogsRead(readingToken, logArray), 100);
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
					syncContext.PostDelayed(() => this.OnLogsRead(readingToken, readLogs), 100);

				// close reader
				Global.RunWithoutError(reader.Close);

				// complete reading
				if (readLogs.IsNotEmpty())
					syncContext.PostDelayed(() => this.OnLogsReadingCompleted(exception), 100);
				else
					syncContext.Post(() => this.OnLogsReadingCompleted(exception));
			}
		}


		/// <summary>
		/// Resume reading logs.
		/// </summary>
		/// <returns>True if logs reading has been resumed successfully.</returns>
		public bool Resume()
		{
			this.VerifyAccess();
			switch (this.state)
			{
				case LogReaderState.Starting:
				case LogReaderState.ReadingLogs:
					return true;
				case LogReaderState.StartingWhenPaused:
					return this.ChangeState(LogReaderState.Starting);
				case LogReaderState.Paused:
					return this.ChangeState(LogReaderState.ReadingLogs);
				default:
					this.Logger.LogWarning($"Cannot resume logs reading when state is {this.state}");
					return false;
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
			if (this.logPatterns.IsEmpty())
				throw new InvalidOperationException("No log pattern specified.");

			// start
			this.StartReadingLogs();
		}


		// Start reading logs.
		async void StartReadingLogs()
		{
			// change state
			switch (this.state)
			{
				case LogReaderState.Preparing:
				case LogReaderState.ReadingLogs:
					if (!this.ChangeState(LogReaderState.Starting))
						return;
					break;
				case LogReaderState.Paused:
					if (!this.ChangeState(LogReaderState.StartingWhenPaused))
						return;
					break;
				case LogReaderState.Starting:
				case LogReaderState.StartingWhenPaused:
					break;
				default:
					this.Logger.LogError($"Cannot start readong logs when state is {this.state}");
					return;
			}


			// check source state
			if (this.DataSource.State != LogDataSourceState.ReadyToOpenReader)
			{
				if (this.DataSource.IsErrorState())
				{
					this.Logger.LogError($"Data source state is {this.DataSource.State} when starting reading logs");
					this.ChangeState(LogReaderState.DataSourceError);
				}
				else
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

			// change state
			switch(this.state)
			{
				case LogReaderState.Starting:
					if (reader == null)
					{
						this.ChangeState(LogReaderState.DataSourceError);
						return;
					}
					if (!this.ChangeState(LogReaderState.ReadingLogs))
					{
						Global.RunWithoutErrorAsync(() => reader?.Close());
						return;
					}
					break;
				case LogReaderState.StartingWhenPaused:
					if (reader == null)
					{
						this.ChangeState(LogReaderState.DataSourceError);
						return;
					}
					if (!this.ChangeState(LogReaderState.Paused))
					{
						Global.RunWithoutErrorAsync(() => reader?.Close());
						return;
					}
					break;
				default:
					this.Logger.LogWarning($"State has been changed to {this.state} when opening reader");
					Global.RunWithoutErrorAsync(() => reader?.Close());
					return;
			}

			// read logs
			this.logsReadingToken = new object();
			this.logsReadingCancellationTokenSource = new CancellationTokenSource();
			var readingToken = this.logsReadingToken;
			var cancellationToken = this.logsReadingCancellationTokenSource.Token;
			var taskFactory = this.DataSource.UnderlyingSource switch
			{
				UnderlyingLogDataSource.File => fileReadingTaskFactory,
				_ => defaultReadingTaskFactory,
			};
			_ = taskFactory.StartNew(() => this.ReadLogs(readingToken, reader, cancellationToken));
		}


		/// <summary>
		/// Get current state of <see cref="LogReader"/>.
		/// </summary>
		public LogReaderState State { get => this.state; }


		/// <summary>
		/// Get or set <see cref="CultureInfo"/> to parse timestamp of log.
		/// </summary>
		public CultureInfo TimestampCultureInfo
		{
			get => this.timestampCultureInfo;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(TimestampCultureInfo)} when state is {this.state}.");
				if (this.timestampCultureInfo.Equals(value))
					return;
				this.timestampCultureInfo = value;
				this.OnPropertyChanged(nameof(TimestampCultureInfo));
			}
		}


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
		/// Starting to read logs but pausing logs reading has been requested.
		/// </summary>
		StartingWhenPaused,
		/// <summary>
		/// Reading logs.
		/// </summary>
		ReadingLogs,
		/// <summary>
		/// Pause reading logs.
		/// </summary>
		Paused,
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
