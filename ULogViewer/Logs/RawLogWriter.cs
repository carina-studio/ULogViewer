using CarinaStudio.Collections;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Json;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using CarinaStudio.ULogViewer.Text;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs;

/// <summary>
/// <see cref="ILogWriter"/> to write <see cref="Log"/>s as raw log data.
/// </summary>
class RawLogWriter : BaseLogWriter
{
	// Fields.
	IList<string> logFormats = Array.Empty<string>();
	readonly Dictionary<Log, int> lineNumbers = new();
	readonly HashSet<Log> logsToGetLineNumber = new();
	readonly Dictionary<LogLevel, string> logLevelMap = new();
	LogStringEncoding logStringEncoding = LogStringEncoding.Plain;
	readonly IDictionary<LogLevel, string> readOnlyLogLevelMap;
	readonly ISet<Log> readOnlyLogsToGetLineNumbers;
	CultureInfo timeSpanCultureInfo = CultureInfo.GetCultureInfo("en-US");
	string? timeSpanFormat;
	CultureInfo timestampCultureInfo = CultureInfo.GetCultureInfo("en-US");
	string? timestampFormat;
	bool writeFileNames = true;


	/// <summary>
	/// Initialize new <see cref="RawLogWriter"/> instance.
	/// </summary>
	/// <param name="output"><see cref="ILogDataOutput"/> to output raw log data.</param>
	public RawLogWriter(ILogDataOutput output) : base(output)
	{
		// setup properties.
		this.LineNumbers = DictionaryExtensions.AsReadOnly(this.lineNumbers);
		this.readOnlyLogLevelMap = DictionaryExtensions.AsReadOnly(this.logLevelMap);
		this.readOnlyLogsToGetLineNumbers = this.logsToGetLineNumber.AsReadOnly();
	}


	/// <summary>
	/// Get line numbers of <see cref="Log"/> in <see cref="LogsToGetLineNumber"/>.
	/// </summary>
	public IDictionary<Log, int> LineNumbers { get; }


	/// <summary>
	/// Get or set list of string format to output raw log data.
	/// </summary>
	public IList<string> LogFormats
	{
		get => this.logFormats;
		set
		{
			this.VerifyAccess();
			this.VerifyPreparing();
			if (this.logFormats.SequenceEqual(value))
				return;
			this.logFormats = ListExtensions.AsReadOnly(value.ToArray());
			this.OnPropertyChanged(nameof(LogFormats));
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
			if (this.logLevelMap.Equals(value))
				return;
			this.logLevelMap.Clear();
			this.logLevelMap.AddAll(value);
			this.OnPropertyChanged(nameof(LogLevelMap));
		}
	}


	/// <summary>
	/// Get or set the set of <see cref="Log"/> to get line number after writing completed.
	/// </summary>
	public ISet<Log> LogsToGetLineNumber
	{
		get => this.readOnlyLogsToGetLineNumbers;
		set
		{
			this.VerifyAccess();
			this.VerifyPreparing();
			if (this.logsToGetLineNumber.SetEquals(value))
				return;
			this.logsToGetLineNumber.Clear();
			this.logsToGetLineNumber.AddAll(value);
			this.OnPropertyChanged(nameof(LogsToGetLineNumber));
		}
	}


	/// <summary>
	/// Get or set string encoding of log.
	/// </summary>
	public LogStringEncoding LogStringEncoding
	{
		get => this.logStringEncoding;
		set
		{
			this.VerifyAccess();
			this.VerifyPreparing();
			if (this.logStringEncoding == value)
				return;
			this.logStringEncoding = value;
			this.OnPropertyChanged(nameof(LogStringEncoding));
		}
	}


	/// <summary>
	/// Get or set <see cref="CultureInfo"/> for <see cref="TimeSpanFormat"/> to format time span of log.
	/// </summary>
	public CultureInfo TimeSpanCultureInfo
	{
		get => this.timeSpanCultureInfo;
		set
		{
			this.VerifyAccess();
			this.VerifyPreparing();
			if (this.timeSpanCultureInfo.Equals(value))
				return;
			this.timeSpanCultureInfo = value;
			this.OnPropertyChanged(nameof(TimeSpanCultureInfo));
		}
	}


	/// <summary>
	/// Get or set format of <see cref="Log.BeginningTimeSpan"/>, <see cref="Log.EndingTimeSpan"/> and <see cref="Log.TimeSpan"/> to output to raw log data.
	/// </summary>
	public string? TimeSpanFormat
	{
		get => this.timeSpanFormat;
		set
		{
			this.VerifyAccess();
			this.VerifyPreparing();
			if (this.timeSpanFormat == value)
				return;
			this.timeSpanFormat = value;
			this.OnPropertyChanged(nameof(TimeSpanFormat));
		}
	}


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
	/// Get or set format of <see cref="Log.BeginningTimestamp"/>, <see cref="Log.EndingTimestamp"/> and <see cref="Log.Timestamp"/> to output to raw log data.
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
	[CalledOnBackgroundThread]
	void WriteLogs(IList<Log> logs, TextWriter writer, List<StringFormatter> formatters, Dictionary<string, Func<Log, object?>> logPropertyGetters, CancellationToken cancellationToken)
	{
		var logCount = logs.Count;
		var logsToGetLineNumber = this.logsToGetLineNumber;
		var logStringBuilder = new StringBuilder();
		using var logStringWriter = new StringWriter(logStringBuilder);
		var writeFileNames = this.writeFileNames;
		var currentFileName = "";
		var writtenLineCount = 0;
		var lineNumbers = new Dictionary<Log, int>();
		try
		{
			for (var i = 0; i < logCount; ++i)
			{
				// cancellation check
				cancellationToken.ThrowIfCancellationRequested();
				
				// select format
				var log = logs[i];
				var formatter = (StringFormatter?)null;
				foreach (var candFormatter in formatters)
				{
					var areAllPropertiesValid = true;
					foreach (var propName in candFormatter.ParameterNames)
					{
						if (!logPropertyGetters.TryGetValue(propName, out var getter)
							|| getter(log) is null)
						{
							areAllPropertiesValid = false;
							break;
						}
					}
					if (areAllPropertiesValid)
					{
						formatter = candFormatter;
						break;
					}
				}
				formatter ??= formatters[0];

				// move to next line
				if (i > 0)
					writer.WriteLine();

				// write to memory
				if (writeFileNames)
				{
					log.FileName?.ToString().Let(fileName =>
					{
						if (!PathEqualityComparer.Default.Equals(currentFileName, fileName))
						{
							logStringWriter.WriteLine($"[{Path.GetFileName(fileName)}]");
							currentFileName = fileName;
						}
					});
				}
				logStringWriter.Write(formatter.ToString(log));

				// check line count
				if (logsToGetLineNumber.IsNotEmpty())
				{
					var lineCount = 1;
					for (var cIndex = logStringBuilder.Length - 1; cIndex >= 0; --cIndex)
					{
						if (logStringBuilder[cIndex] == '\n')
							++lineCount;
					}
					if (logsToGetLineNumber.Contains(log))
						lineNumbers[log] = (writtenLineCount + 1);
					writtenLineCount += lineCount;
				}

				// write to output
				writer.Write(logStringBuilder.ToString());
				logStringBuilder.Remove(0, logStringBuilder.Length);
			}
		}
		finally
		{
			this.SynchronizationContext.Post(() => this.lineNumbers.AddAll(lineNumbers));
		}
	}


	// Write logs.
	protected override async Task WriteLogsAsync(IList<Log> logs, TextWriter writer, CancellationToken cancellationToken)
	{
		// copy state to local
		var logLevelMap = this.logLevelMap;
		var logStringEncoding = this.logStringEncoding;
		var timeSpanFormat = this.timeSpanFormat;
		var timeSpanCultureInfo = this.timeSpanCultureInfo;
		var timestampFormat = this.timestampFormat;
		var timestampCultureInfo = this.timestampCultureInfo;
		
		// prepare output format and log property getters
		var logPropertyGetters = new Dictionary<string, Func<Log, object?>>();
		var formatters = new List<StringFormatter>();
		foreach (var logFormat in this.logFormats)
		{
			var formatter = new StringFormatter(logFormat, (obj, propertyName) =>
			{
				if (obj is not Log log)
					return null;
				if (propertyName == "NewLine")
					return "\n";
				if (Log.HasProperty(propertyName))
				{
					if (!logPropertyGetters.TryGetValue(propertyName, out var getter))
					{
						getter = propertyName switch
						{
							nameof(Log.BeginningTimeSpan)
							or nameof(Log.EndingTimeSpan)
							or nameof(Log.TimeSpan) => Log.CreatePropertyGetter<TimeSpan?>(propertyName).Let(getter =>
							{
								return (Func<Log, string?>)(log =>
								{
									var timeSpan = getter(log);
									if (timeSpan is null)
										return "";
									if (timeSpanFormat is not null)
									{
										try
										{
											return timeSpan.Value.ToString(timeSpanFormat, timeSpanCultureInfo);
										}
										// ReSharper disable once EmptyGeneralCatchClause
										catch
										{ }
									}
									return timeSpan.Value.ToString();
								});
							}),
							nameof(Log.BeginningTimestamp)
							or nameof(Log.EndingTimestamp)
							or nameof(Log.Timestamp) => Log.CreatePropertyGetter<DateTime?>(propertyName).Let(getter =>
							{
								return (Func<Log, string?>)(log =>
								{
									var timestamp = getter(log);
									if (timestamp is null)
										return "";
									if (timestampFormat is not null)
									{
										try
										{
											return timestamp.Value.ToString(timestampFormat, timestampCultureInfo);
										}
										// ReSharper disable once EmptyGeneralCatchClause
										catch
										{ }
									}
									return timestamp.Value.ToString(this.timestampCultureInfo);
								});
							}),
							nameof(Log.Level) => log =>
							{
								if (logLevelMap.TryGetValue(log.Level, out var str))
									return str;
								return log.Level.ToString().ToUpperInvariant();
							},
							_ => Log.CreatePropertyGetter<object?>(propertyName).Let(getter =>
							{
								return (Func<Log, object?>)(log =>
								{
									var value = getter(log);
									string s;
									if (value is IStringSource stringSource)
									{
										if (stringSource.Length <= 0)
											return value;
										s = stringSource.ToString() ?? "";
									}
									else if (value is string str)
									{
										if (str.Length <= 0)
											return value;
										s = str;
									}
									else
										return value;
									return logStringEncoding switch
									{
										LogStringEncoding.Json => JsonUtility.EncodeToJsonString(s),
										LogStringEncoding.Xml => WebUtility.HtmlEncode(s),
										_ => s,
									};
								});
							}),
						};
						logPropertyGetters[propertyName] = getter;
					}
					return getter(log);
				}
				return null;
			});
			formatters.Add(formatter);
		}

		// start writing
		await Task.Run(() => this.WriteLogs(logs, writer, formatters, logPropertyGetters, cancellationToken), CancellationToken.None);
	}
}
