using System;
using System.Linq;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.Logs
{
    /// <summary>
    /// Precondition of reading log.
    /// </summary>
    struct LogReadingPrecondition : IEquatable<LogReadingPrecondition>
    {
        /// <inheritdoc/>
        public bool Equals(LogReadingPrecondition precondition) =>
            this.TimeSpanRange == precondition.TimeSpanRange
            && this.TimestampRange == precondition.TimestampRange;
        

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is LogReadingPrecondition precondition && this.Equals(precondition);

        
        /// <inheritdoc/>
        override public int GetHashCode() =>
            (this.TimeSpanRange.GetHashCode() << 16) | (this.TimestampRange.GetHashCode() & 0xffff);
        

        /// <summary>
        /// Check whether this is an empty precondition or not.
        /// </summary>
        /// <value></value>
        public bool IsEmpty 
        { 
            get => this.TimeSpanRange.IsUniversal && this.TimestampRange.IsUniversal;
        }


        /// <summary>
        /// Load precondition from JSON data.
        /// </summary>
        /// <param name="jsonElement">JSON elements.</param>
        /// <returns>Loaded precondition.</returns>
        public static LogReadingPrecondition Load(JsonElement jsonElement)
        {
            // check root element
            var precondition = new LogReadingPrecondition();
            if (jsonElement.ValueKind != JsonValueKind.Object)
                return precondition;

            // time span range
            if (jsonElement.TryGetProperty(nameof(TimeSpanRange), out var jsonProperty)
                && jsonProperty.ValueKind == JsonValueKind.Array
                && jsonProperty.GetArrayLength() == 2)
            {
                var array = jsonProperty.EnumerateArray().ToArray();
                var start = array[0].TryGetInt64(out var ticks)
                    ? new TimeSpan(ticks) 
                    : (TimeSpan?)null;
                var end = array[1].TryGetInt64(out ticks)
                    ? new TimeSpan(ticks) 
                    : (TimeSpan?)null;
                precondition.TimeSpanRange = (start, end);
            }

            // timestamp range
            if (jsonElement.TryGetProperty(nameof(TimestampRange), out jsonProperty)
                && jsonProperty.ValueKind == JsonValueKind.Array
                && jsonProperty.GetArrayLength() == 2)
            {
                var array = jsonProperty.EnumerateArray().ToArray();
                var start = array[0].ValueKind == JsonValueKind.Number && array[0].TryGetInt64(out var binary)
                    ? DateTime.FromBinary(binary)
                    : (DateTime?)null;
                var end = array[1].ValueKind == JsonValueKind.Number && array[1].TryGetInt64(out binary)
                    ? DateTime.FromBinary(binary)
                    : (DateTime?)null;
                precondition.TimestampRange = (start, end);
            }

            // complete
            return precondition;
        }


        /// <summary>
        /// Check whether given log is matched to this precondition or not.
        /// </summary>
        /// <param name="log">Log.</param>
        /// <returns>True if log is matched to this precondition.</returns>
        public bool Matches(Log log)
        {
            // match time span
            var hasTimeSpanProperty = false;
            var timeSpanRange = this.TimeSpanRange;
            if (!timeSpanRange.IsUniversal)
            {
                if (log.BeginningTimeSpan?.Let(it =>
                {
                    hasTimeSpanProperty = true;
                    return timeSpanRange.Contains(it);
                }) == true)
                {
                    return true;
                }
                if (log.EndingTimeSpan?.Let(it =>
                {
                    hasTimeSpanProperty = true;
                    return timeSpanRange.Contains(it);
                }) == true)
                {
                    return true;
                }
                if (log.TimeSpan?.Let(it =>
                {
                    hasTimeSpanProperty = true;
                    return timeSpanRange.Contains(it);
                }) == true)
                {
                    return true;
                }
            }

            // match timestamp
            var hasTimestampProperty = false;
            var timestampRange = this.TimestampRange;
            if (!timestampRange.IsUniversal)
            {
                if (log.BeginningTimestamp?.Let(it =>
                {
                    hasTimestampProperty = true;
                    return timestampRange.Contains(it);
                }) == true)
                {
                    return true;
                }
                if (log.EndingTimestamp?.Let(it =>
                {
                    hasTimestampProperty = true;
                    return timestampRange.Contains(it);
                }) == true)
                {
                    return true;
                }
                if (log.Timestamp?.Let(it =>
                {
                    hasTimestampProperty = true;
                    return timestampRange.Contains(it);
                }) == true)
                {
                    return true;
                }
            }

            // not matched if related property exists
            return (!hasTimeSpanProperty || timeSpanRange.IsUniversal)
                && (!hasTimestampProperty || timestampRange.IsUniversal);
        }


        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(LogReadingPrecondition x, LogReadingPrecondition y) =>
            x.Equals(y);
        

        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(LogReadingPrecondition x, LogReadingPrecondition y) =>
            !x.Equals(y);


        /// <summary>
        /// Range of time span of logs.
        /// </summary>
        public Range<TimeSpan> TimeSpanRange { get; set; }


        /// <summary>
        /// Range of timestamp of logs.
        /// </summary>
        public Range<DateTime> TimestampRange { get; set; }


        /// <summary>
        /// Save precondition in JSON format data.
        /// </summary>
        /// <param name="writer">JSON writer.</param>
        public void Save(Utf8JsonWriter writer)
        {
            // start
            writer.WriteStartObject();

            // time span range
            this.TimeSpanRange.Let(it =>
            {
                if (!it.IsUniversal)
                {
                    writer.WritePropertyName(nameof(TimeSpanRange));
                    writer.WriteStartArray();
                    if (it.Start.HasValue)
                        writer.WriteNumberValue(it.Start.Value.Ticks);
                    else
                        writer.WriteNullValue();
                    if (it.End.HasValue)
                        writer.WriteNumberValue(it.End.Value.Ticks);
                    else
                        writer.WriteNullValue();
                    writer.WriteEndArray();
                }
            });

            // timestamp range
            this.TimestampRange.Let(it =>
            {
                if (!it.IsUniversal)
                {
                    writer.WritePropertyName(nameof(TimestampRange));
                    writer.WriteStartArray();
                    if (it.Start.HasValue)
                        writer.WriteNumberValue(it.Start.Value.ToBinary());
                    else
                        writer.WriteNullValue();
                    if (it.End.HasValue)
                        writer.WriteNumberValue(it.End.Value.ToBinary());
                    else
                        writer.WriteNullValue();
                    writer.WriteEndArray();
                }
            });

            // complete
            writer.WriteEndObject();
        }
    }
}