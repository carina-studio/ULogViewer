using System;

namespace CarinaStudio.ULogViewer.Logs
{
    /// <summary>
    /// Source encoding of <see cref="TimeSpan"/> properties of <see cref="Log"/>.
    /// </summary>
    enum LogTimeSpanEncoding
    {
        /// <summary>
        /// Custom format.
        /// </summary>
        Custom,
        /// <summary>
        /// Represent in days.
        /// </summary>
        TotalDays,
        /// <summary>
        /// Represent in hours.
        /// </summary>
        TotalHours,
        /// <summary>
        /// Represent in minutes.
        /// </summary>
        TotalMinutes,
        /// <summary>
        /// Represent in seconds.
        /// </summary>
        TotalSeconds,
        /// <summary>
        /// Represent in milliseconds.
        /// </summary>
        TotalMilliseconds,
        /// <summary>
        /// Represent in microseconds.
        /// </summary>
        TotalMicroseconds,
    }
}
