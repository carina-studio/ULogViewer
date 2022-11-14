using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// <see cref="ILogWriter"/> to write <see cref="Log"/>s in JSON format.
	/// </summary>
	class JsonLogWriter : BaseLogWriter
	{
		// Fields.
		readonly Dictionary<LogLevel, string> logLevelMap = new();
		readonly Dictionary<string, string> logPropertyMap = new();
		readonly IDictionary<LogLevel, string> readOnlyLogLevelMap;
		readonly IDictionary<string, string> readOnlyLogPropertyMap;
		CultureInfo timeSpanCultureInfo = CultureInfo.GetCultureInfo("en-US");
		CultureInfo timestampCultureInfo = CultureInfo.GetCultureInfo("en-US");
		string? timeSpanFormat;
		string? timestampFormat;


		/// <summary>
		/// Initialize new <see cref="JsonLogWriter"/> instance.
		/// </summary>
		/// <param name="dataOutput">Data output.</param>
		public JsonLogWriter(ILogDataOutput dataOutput) : base(dataOutput)
		{
			this.readOnlyLogLevelMap = DictionaryExtensions.AsReadOnly(this.logLevelMap);
			this.readOnlyLogPropertyMap = DictionaryExtensions.AsReadOnly(this.logPropertyMap);
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
		/// Get or set map of name of property of <see cref="Log"/> to be written and its name in JSON object.
		/// </summary>
		public IDictionary<string, string> LogPropertyMap
		{
			get => this.readOnlyLogPropertyMap;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				this.VerifyPreparing();
				this.logPropertyMap.Clear();
				this.logPropertyMap.AddAll(value);
				this.OnPropertyChanged(nameof(LogPropertyMap));
			}
		}


		/// <summary>
		/// Get or set <see cref="CultureInfo"/> for <see cref="TimeSpanFormat"/> to format timestamp of log.
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


		// Write logs.
		protected override async Task WriteLogsAsync(TextWriter writer, CancellationToken cancellationToken)
		{
			// prepare property getters
			var propertyGetters = new SortedList<string, Func<Log, object?>>();
			foreach (var pair in this.logPropertyMap)
			{
				if (Log.HasProperty(pair.Key))
					propertyGetters.Add(pair.Value, Log.CreatePropertyGetter<object?>(pair.Key));
			}
			if (propertyGetters.IsEmpty())
				throw new ArgumentException("No log property to write.");

			// write logs
			await Task.Run(() =>
			{
				var tempFilePath = "";
				var tempFileStream = (Stream?)null;
				var logLevelMap = this.logLevelMap;
				var timeSpanCultureInfo = this.timeSpanCultureInfo;
				var timestampCultureInfo = this.timestampCultureInfo;
				var timeSpanFormat = this.timeSpanFormat;
				var timestampFormat = this.timestampFormat;
				var hasTimeSpanFormat = !string.IsNullOrWhiteSpace(timeSpanFormat);
				var hasTimestampFormat = !string.IsNullOrWhiteSpace(timestampFormat);
				try
				{
					// create temp file
					tempFilePath = Path.GetTempFileName();

					// create JSON writer
					tempFileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite);
					using var jsonWriter = new Utf8JsonWriter(tempFileStream, new JsonWriterOptions() { Indented = true });

					// write logs
					jsonWriter.WriteStartArray();
					foreach (var log in this.Logs)
					{
						jsonWriter.WriteStartObject();
						foreach (var propertyName in propertyGetters.Keys)
						{
							var value = propertyGetters[propertyName](log);
							jsonWriter.WritePropertyName(propertyName);
							if (value == null)
								jsonWriter.WriteNullValue();
							else if (value is DateTime dateTimeValue)
							{
								if (hasTimestampFormat)
									jsonWriter.WriteStringValue(dateTimeValue.ToString(timestampFormat, timestampCultureInfo));
								else
									jsonWriter.WriteStringValue(dateTimeValue.ToString(timestampCultureInfo));
							}
							else if (value is TimeSpan timeSpanValue)
							{
								if (hasTimeSpanFormat)
									jsonWriter.WriteStringValue(timeSpanValue.ToString(timeSpanFormat, timeSpanCultureInfo));
								else
									jsonWriter.WriteStringValue(timeSpanValue.ToString());
							}
							else if (value is int intValue)
								jsonWriter.WriteNumberValue(intValue);
							else if (value is LogLevel level)
							{
								if (logLevelMap.TryGetValue(level, out var str))
									jsonWriter.WriteStringValue(str);
								else
									jsonWriter.WriteStringValue(level.ToString());
							}
							else
								jsonWriter.WriteStringValue(value.ToString());
						}
						jsonWriter.WriteEndObject();
					}
					jsonWriter.WriteEndArray();

					// cancellation check
					if (cancellationToken.IsCancellationRequested)
						throw new TaskCanceledException();

					// copy data to output writer
					jsonWriter.Dispose();
					tempFileStream.Position = 0;
					using (var reader = new StreamReader(tempFileStream, Encoding.UTF8))
					{
						var line = reader.ReadLine();
						while (line != null && !cancellationToken.IsCancellationRequested)
						{
							writer.WriteLine(line);
							line = reader.ReadLine();
						}
					}
				}
				finally
				{
					Global.RunWithoutError(() => tempFileStream?.Close());
					if (!string.IsNullOrEmpty(tempFilePath))
						File.Delete(tempFilePath);
				}
			}, CancellationToken.None);
		}
	}
}
