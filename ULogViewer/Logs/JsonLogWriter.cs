using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using CarinaStudio.ULogViewer.Logs.Profiles;
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
		readonly Dictionary<LogLevel, string> logLevelMap = new Dictionary<LogLevel, string>();
		IList<LogProperty> logProperties = new LogProperty[0];
		readonly IDictionary<LogLevel, string> readOnlyLogLevelMap;
		CultureInfo timestampCultureInfo = CultureInfo.GetCultureInfo("en-US");
		string? timestampFormat;
		bool useLogPropertyDisplayNames = true;


		/// <summary>
		/// Initialize new <see cref="JsonLogWriter"/> instance.
		/// </summary>
		/// <param name="dataOutput">Data output.</param>
		public JsonLogWriter(ILogDataOutput dataOutput) : base(dataOutput)
		{
			this.readOnlyLogLevelMap = this.logLevelMap.AsReadOnly();
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
		/// Get or set properties of <see cref="Log"/> to be written.
		/// </summary>
		public IList<LogProperty> LogProperties
		{
			get => this.logProperties;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				this.VerifyPreparing();
				if (this.logProperties.SequenceEqual(value))
					return;
				this.logProperties = new List<LogProperty>(value).AsReadOnly();
				this.OnPropertyChanged(nameof(LogProperties));
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


		/// <summary>
		/// Get or set whether use <see cref="LogProperty.DisplayName"/> as name of JSON property or not.
		/// </summary>
		public bool UseLogPropertyDisplayNames
		{
			get => this.useLogPropertyDisplayNames;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				this.VerifyPreparing();
				if (this.useLogPropertyDisplayNames == value)
					return;
				this.useLogPropertyDisplayNames = value;
				this.OnPropertyChanged(nameof(UseLogPropertyDisplayNames));
			}
		}


		// Write logs.
		protected override async Task WriteLogsAsync(TextWriter writer, CancellationToken cancellationToken)
		{
			// prepare property getters
			var propertyGetters = new SortedList<string, Func<Log, object?>>();
			foreach (var logProperty in this.logProperties)
			{
				if (Log.HasProperty(logProperty.Name))
					propertyGetters.Add(this.useLogPropertyDisplayNames ? logProperty.DisplayName : logProperty.Name, Log.CreatePropertyGetter<object?>(logProperty.Name));
			}
			if (propertyGetters.IsEmpty())
				throw new ArgumentException("No log property to write.");

			// write logs
			await Task.Run(() =>
			{
				var tempFilePath = "";
				var logLevelMap = this.logLevelMap;
				var timestampCultureInfo = this.timestampCultureInfo;
				var timestampFormat = this.timestampFormat;
				var hasTimestampFormat = !string.IsNullOrWhiteSpace(timestampFormat);
				try
				{
					// create temp file
					tempFilePath = Path.GetTempFileName();

					// create JSON writer
					using var tempStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.ReadWrite);
					using var jsonWriter = new Utf8JsonWriter(tempStream, new JsonWriterOptions() { Indented = true });

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
					jsonWriter.Flush();
					tempStream.Position = 0;
					using (var reader = new StreamReader(tempStream, Encoding.UTF8))
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
					if (!string.IsNullOrEmpty(tempFilePath))
						File.Delete(tempFilePath);
				}
			});
		}
	}
}
