using CarinaStudio.Collections;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Writer to write list of <see cref="Log"/> to specific <see cref="ILogDataOutput"/>.
	/// </summary>
	class LogWriter : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Static fields.
		static readonly Regex logPropertyNameRegex = new Regex("\\{(?<PropertyName>[\\w\\d]+)(\\,(?<Alignment>[\\+\\-]?[\\d]+))?(\\:(?<Format>[^\\}]+))?\\}");
		static readonly string newLineString = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\r\n" : "\n";
		static int nextId = 1;


		// Fields.
		string logFormat = "";
		IList<Log> logs = new Log[0];
		readonly Dictionary<LogLevel, string> logLevelMap = new Dictionary<LogLevel, string>();
		readonly IDictionary<LogLevel, string> readOnlyLogLevelMap;
		LogWriterState state = LogWriterState.Preparing;
		CultureInfo timestampCultureInfo = CultureInfo.GetCultureInfo("en-US");
		string? timestampFormat;
		bool writeFileNames = true;
		CancellationTokenSource writingLogsCancellationTokenSource = new CancellationTokenSource();



		/// <summary>
		/// Initialize new <see cref="LogWriter"/> instance.
		/// </summary>
		/// <param name="output"><see cref="ILogDataOutput"/> to output raw log data.</param>
		public LogWriter(ILogDataOutput output)
		{
			// check state
			output.VerifyAccess();
			if (output.State == LogDataOutputState.Disposed)
				throw new ArgumentException("Output has been disposed.");

			// setup properties.
			this.Application = (IApplication)output.Application;
			this.DataOutput = output;
			this.Id = nextId++;
			this.Logger = output.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.readOnlyLogLevelMap = new ReadOnlyDictionary<LogLevel, string>(this.logLevelMap);

			// attach to output
			output.PropertyChanged += this.OnDataOutputPropertyChanged;
		}


		/// <summary>
		/// Get <see cref="IApplication"/> instance.
		/// </summary>
		public IApplication Application { get; }


		// Change state.
		bool ChangeState(LogWriterState state)
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
		/// Get <see cref="ILogDataOutput"/> to output raw log data.
		/// </summary>
		public ILogDataOutput DataOutput { get; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// check thread
			if (!disposing)
				return;
			this.VerifyAccess();

			// cancel writing logs
			this.writingLogsCancellationTokenSource.Cancel();

			// detach from data source
			this.DataOutput.PropertyChanged -= this.OnDataOutputPropertyChanged;
		}


		/// <summary>
		/// Get unique ID of this <see cref="LogReader"/> instance.
		/// </summary>
		public int Id { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Get or set string format to output raw log data.
		/// </summary>
		public string LogFormat
		{
			get => this.logFormat;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				if (this.logFormat == value)
					return;
				this.logFormat = value;
				this.OnPropertyChanged(nameof(LogFormat));
			}
		}


		/// <summary>
		/// Get or set map from <see cref="LogLevel"/> to <see cref="string"/>.
		/// </summary>
		public IDictionary<LogLevel, string> LogLevelMap
		{
			get => this.readOnlyLogLevelMap;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				if (this.logLevelMap.SequenceEqual(value))
					return;
				this.logLevelMap.Clear();
				this.logLevelMap.AddAll(value);
				this.OnPropertyChanged(nameof(LogLevelMap));
			}
		}


		/// <summary>
		/// Get or set list of <see cref="Log"/> to be output.
		/// </summary>
		public IList<Log> Logs
		{
			get => this.logs;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				this.logs = value.IsNotEmpty() ? new List<Log>(value).AsReadOnly() : new Log[0];
				this.OnPropertyChanged(nameof(Logs));
			}
		}


		// Property of data output changed.
		void OnDataOutputPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataOutput.State) && this.state == LogWriterState.Starting)
				this.StartWritingLogs();
		}


		// Called when logs writing completed.
		void OnLogsWritingCompleted(Exception? ex)
		{
			// check state
			if (this.state != LogWriterState.WritingLogs)
				return;

			// change state
			if (ex == null)
				this.ChangeState(LogWriterState.Stopped);
			else
				this.ChangeState(LogWriterState.UnclassifiedError);
		}


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		/// <summary>
		/// Start writing logs.
		/// </summary>
		public void Start()
		{
			// check state
			this.VerifyAccess();
			switch (this.state)
			{
				case LogWriterState.Preparing:
					break;
				case LogWriterState.Starting:
				case LogWriterState.WritingLogs:
					return;
				default:
					throw new InvalidOperationException($"Cannot start writing log in state {this.state}.");
			}

			// change state
			if (!this.ChangeState(LogWriterState.Starting))
				throw new InternalStateCorruptedException();

			// start writing logs
			this.StartWritingLogs();
		}


		// Start writing logs.
		async void StartWritingLogs()
		{
			// check state
			if (this.state != LogWriterState.Starting)
				return;
			switch (this.DataOutput.State)
			{
				case LogDataOutputState.ReadyToOpenWriter:
					break;
				case LogDataOutputState.TargetNotFound:
				case LogDataOutputState.UnclassifiedError:
					this.Logger.LogError($"Unable to start writing logs because of data output state is {this.DataOutput.State}");
					this.ChangeState(LogWriterState.DataOutputError);
					return;
				default:
					this.Logger.LogWarning("Wait for data output ready");
					return;
			}

			// start opening writer
			var writerOpeningTask = this.DataOutput.OpenWriterAsync();

			// prepare output format and log property getters
			var logFormatStart = 0;
			var formatBuilder = new StringBuilder();
			var logPropertyGetters = new List<Func<Log, object?>>();
			var argIndex = 0;
			var match = logPropertyNameRegex.Match(this.logFormat);
			while (match.Success)
			{
				formatBuilder.Append(this.logFormat.Substring(logFormatStart, match.Index - logFormatStart));
				logFormatStart = match.Index + match.Length;
				var propertyName = match.Groups["PropertyName"].Value;
				if (propertyName == "NewLine")
					formatBuilder.Append(newLineString);
				else if (Log.HasProperty(propertyName))
				{
					var logPropertyGetter = propertyName switch
					{
						nameof(Log.Level) => log =>
						{
							if (this.logLevelMap.TryGetValue(log.Level, out var str))
								return str;
							return log.Level.ToString();
						},
						nameof(Log.Timestamp) => log =>
						{
							var timestamp = log.Timestamp;
							if (timestamp == null)
								return "";
							if (this.timestampFormat != null)
								return timestamp.Value.ToString(this.timestampFormat, this.timestampCultureInfo);
							return timestamp.Value.ToString(this.timestampCultureInfo);
						},
						_ => Log.CreatePropertyGetter<object?>(propertyName),
					};
					logPropertyGetters.Add(logPropertyGetter);
					formatBuilder.Append($"{{{argIndex++}");
					match.Groups["Alignment"].Let(it =>
					{
						if (it.Success)
							formatBuilder.Append($",{it.Value}");
					});
					match.Groups["Format"].Let(it =>
					{
						if (it.Success)
							formatBuilder.Append($":{it.Value}");
					});
					formatBuilder.Append('}');
				}
				match = match.NextMatch();
			}
			formatBuilder.Append(this.logFormat.Substring(logFormatStart));

			// wait for writer opening
			var writer = (TextWriter?)null;
			try
			{
				writer = await writerOpeningTask;
			}
			catch (Exception ex)
			{
				if (this.state == LogWriterState.Starting)
				{
					this.Logger.LogError(ex, "Unable to open writer");
					this.ChangeState(LogWriterState.DataOutputError);
				}
				return;
			}

			// start writing
			var format = formatBuilder.ToString();
			_ = Task.Run(() => this.WriteLogs(writer, format, logPropertyGetters, this.writingLogsCancellationTokenSource.Token));
			this.ChangeState(LogWriterState.WritingLogs);
		}


		/// <summary>
		/// Get current state.
		/// </summary>
		public LogWriterState State { get => this.state; }


		/// <summary>
		/// Get or set <see cref="CultureInfo"/> for <see cref="TimestampFormat"/> to format timestamp of log.
		/// </summary>
		public CultureInfo TimestampCultureInfo
		{
			get => this.timestampCultureInfo;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				if (this.timestampCultureInfo.Equals(value))
					return;
				this.timestampCultureInfo = value;
				this.OnPropertyChanged(nameof(TimestampCultureInfo));
			}
		}


		/// <summary>
		/// Get or set format of <see cref="Log.Timestamp"/> to output to raw log data.
		/// </summary>
		public string? TimestampFormat
		{
			get => this.timestampFormat;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				if (this.timestampFormat == value)
					return;
				this.timestampFormat = value;
				this.OnPropertyChanged(nameof(TimestampFormat));
			}
		}


		// Verify whether current state if Preparing or not.
		void VerifyPreparing()
		{
			if (this.state != LogWriterState.Preparing)
				throw new InvalidOperationException($"Cannot perform operation when state is {this.state}.");
		}


		/// <summary>
		/// Get or set whether writing file name on top of related logs or not.
		/// </summary>
		public bool WriteFileNames
		{
			get => this.writeFileNames;
			set
			{
				this.VerifyAccess();
				this.VerifyPreparing();
				if (this.writeFileNames == value)
					return;
				this.writeFileNames = value;
				this.OnPropertyChanged(nameof(WriteFileNames));
			}
		}


		// Write logs.
		void WriteLogs(TextWriter writer, string format, IList<Func<Log, object?>> logPropertyGetters, CancellationToken cancellationToken)
		{
			var exception = (Exception?)null;
			var logs = this.logs;
			var logCount = logs.Count;
			var logPropertyCount = logPropertyGetters.Count;
			var writeFileNames = this.writeFileNames;
			var currentFileName = "";
			try
			{
				var formatArgs = new object?[logPropertyCount];
				for (var i = 0; i < logCount && !cancellationToken.IsCancellationRequested; ++i)
				{
					var log = logs[i];
					for (var j = logPropertyCount - 1; j >= 0; --j)
						formatArgs[j] = logPropertyGetters[j](log);
					if (i > 0)
						writer.WriteLine();
					if (writeFileNames)
					{
						log.FileName?.Let(fileName =>
						{
							if (!PathEqualityComparer.Default.Equals(currentFileName, fileName))
							{
								writer.WriteLine($"[{Path.GetFileName(fileName)}]");
								currentFileName = fileName;
							}
						});
					}
					writer.Write(string.Format(format, formatArgs));
				}
			}
			catch (Exception ex)
			{
				if (!cancellationToken.IsCancellationRequested)
					this.Logger.LogError(ex, "Error occurred while writing logs");
				exception = ex;
			}
			finally
			{
				Global.RunWithoutError(() => writer.Dispose());
				this.SynchronizationContext.Post(() => this.OnLogsWritingCompleted(exception));
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";
	}


	/// <summary>
	/// State of <see cref="LogWriter"/>.
	/// </summary>
	enum LogWriterState
	{
		/// <summary>
		/// Preparing.
		/// </summary>
		Preparing,
		/// <summary>
		/// Starting to write logs.
		/// </summary>
		Starting,
		/// <summary>
		/// Writing logs.
		/// </summary>
		WritingLogs,
		/// <summary>
		/// Stopped.
		/// </summary>
		Stopped,
		/// <summary>
		/// Error caused by <see cref="LogWriter.DataOutput"/>.
		/// </summary>
		DataOutputError,
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
