using CarinaStudio.Collections;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Json;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// <see cref="ILogWriter"/> to write <see cref="Log"/>s as raw log data.
	/// </summary>
	class RawLogWriter : BaseLogWriter
	{
		// Static fields.
		static readonly Regex logPropertyNameRegex = new Regex("\\{(?<PropertyName>[\\w\\d]+)(\\,(?<Alignment>[\\+\\-]?[\\d]+))?(\\:(?<Format>[^\\}]+))?\\}");
		static readonly string newLineString = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "\r\n" : "\n";


		// Fields.
		IList<string> logFormats = new string[0];
		readonly Dictionary<Log, int> lineNumbers = new Dictionary<Log, int>();
		readonly HashSet<Log> logsToGetLineNumber = new HashSet<Log>();
		readonly Dictionary<LogLevel, string> logLevelMap = new Dictionary<LogLevel, string>();
		LogStringEncoding logStringEncoding = LogStringEncoding.Plane;
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
			this.LineNumbers = this.lineNumbers.AsReadOnly();
			this.readOnlyLogLevelMap = this.logLevelMap.AsReadOnly();
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
				this.logFormats = value.ToArray().AsReadOnly();
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
				if (this.logLevelMap.SequenceEqual(value))
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
		void WriteLogs(TextWriter writer, List<(string, List<string>)> formats, Dictionary<string, Func<Log, object?>> logPropertyGetters, CancellationToken cancellationToken)
		{
			var logs = this.Logs;
			var logCount = logs.Count;
			var logsToGetLineNumber = this.logsToGetLineNumber;
			var logStringEncoding = this.logStringEncoding;
			var logStringBuilder = new StringBuilder();
			using var logStringWriter = new StringWriter(logStringBuilder);
			var writeFileNames = this.writeFileNames;
			var currentFileName = "";
			var writtenLineCount = 0;
			var lineNumbers = new Dictionary<Log, int>();
			var formatArgs = new object?[logPropertyGetters.Count];
			try
			{
				for (var i = 0; i < logCount && !cancellationToken.IsCancellationRequested; ++i)
				{
					// select format
					var log = logs[i];
					var format = (string?)null;
					var propertyNameList = (List<string>?)null;
					foreach (var (candFormat, candPropNameList) in formats)
					{
						var areAllPropertiesValid = true;
						foreach (var propName in candPropNameList)
						{
							if (!logPropertyGetters.TryGetValue(propName, out var getter)
								|| getter(log) == null)
							{
								areAllPropertiesValid = false;
								break;;
							}
						}
						if (areAllPropertiesValid)
						{
							format = candFormat;
							propertyNameList = candPropNameList;
							break;
						}
					}
					if (format == null)
					{
						format = formats[0].Item1;
						propertyNameList = formats[0].Item2;
					}
					
					// get property values
					if (formatArgs.Length < propertyNameList!.Count)
						formatArgs = new object?[propertyNameList.Count];
					for (var j = propertyNameList.Count - 1; j >= 0; --j)
					{
						if (!logPropertyGetters.TryGetValue(propertyNameList[j], out var getter))
							continue;
						formatArgs[j] = getter(log).Let(it =>
						{
							if (it is not string str || str.Length == 0)
								return it;
							return logStringEncoding switch
							{
								LogStringEncoding.Json => JsonUtility.EncodeToJsonString(str),
								LogStringEncoding.Xml => WebUtility.HtmlEncode(str),
								_ => it,
							};
						});
					}

					// move to next line
					if (i > 0)
						writer.WriteLine();

					// write to memory
					if (writeFileNames)
					{
						log.FileName?.Let(fileName =>
						{
							if (!PathEqualityComparer.Default.Equals(currentFileName, fileName))
							{
								logStringWriter.WriteLine($"[{Path.GetFileName(fileName)}]");
								currentFileName = fileName;
							}
						});
					}
					logStringWriter.Write(string.Format(format, formatArgs));

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
		protected override async Task WriteLogsAsync(TextWriter writer, CancellationToken cancellationToken)
		{
			// prepare output format and log property getters
			var logFormatStart = 0;
			var formatBuilder = new StringBuilder();
			var logPropertyGetters = new Dictionary<string, Func<Log, object?>>();
			var formats = new List<(string, List<string>)>();
			var argIndex = 0;
			var cultureInfo = this.Application.CultureInfo;
			foreach (var logFormat in this.logFormats)
			{
				var propNameList = new List<string>();
				var match = logPropertyNameRegex.Match(logFormat);
				while (match.Success)
				{
					formatBuilder.Append(logFormat.Substring(logFormatStart, match.Index - logFormatStart));
					logFormatStart = match.Index + match.Length;
					var propertyName = match.Groups["PropertyName"].Value;
					if (propertyName == "NewLine")
						formatBuilder.Append(newLineString);
					else if (Log.HasProperty(propertyName))
					{
						if (!logPropertyGetters.ContainsKey(propertyName))
						{
							var logPropertyGetter = propertyName switch
							{
								nameof(Log.BeginningTimeSpan)
								or nameof(Log.EndingTimeSpan)
								or nameof(Log.TimeSpan) => Log.CreatePropertyGetter<TimeSpan?>(propertyName).Let(getter =>
								{
									return (Func<Log, string?>)(log =>
									{
										var timeSpan = getter(log);
										if (timeSpan == null)
											return "";
										if (this.timeSpanFormat != null)
										{
											try
											{
												return timeSpan.Value.ToString(this.timeSpanFormat, this.timeSpanCultureInfo);
											}
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
										if (timestamp == null)
											return "";
										if (this.timestampFormat != null)
										{
											try
											{
												return timestamp.Value.ToString(this.timestampFormat, this.timestampCultureInfo);
											}
											catch
											{ }
										}
										return timestamp.Value.ToString(this.timestampCultureInfo);
									});
								}),
								nameof(Log.Level) => log =>
								{
									if (this.logLevelMap.TryGetValue(log.Level, out var str))
										return str;
									return Converters.EnumConverters.LogLevel.Convert(log.Level, typeof(string), null, cultureInfo);
								},
								_ => Log.CreatePropertyGetter<object?>(propertyName),
							};
							logPropertyGetters[propertyName] = logPropertyGetter;
						}
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
						propNameList.Add(propertyName);
					}
					match = match.NextMatch();
				}
				formatBuilder.Append(logFormat.Substring(logFormatStart));
				formats.Add(new(formatBuilder.ToString(), propNameList));
				formatBuilder.Clear();
			}

			// start writing
			var format = formatBuilder.ToString();
			await Task.Run(() => this.WriteLogs(writer, formats, logPropertyGetters, cancellationToken));
		}
	}
}
