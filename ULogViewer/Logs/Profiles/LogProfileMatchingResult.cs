using CarinaStudio.ULogViewer.Logs.DataSources;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Log profile which is eligible to be matched against log files, along with a snapshot of its data source options.
/// </summary>
/// <param name="Profile">Log profile.</param>
/// <param name="DataSourceOptions">Snapshot of data source options of the log profile.</param>
/// <remarks>The snapshot is taken while selecting candidates on the application thread, so that matching never reads the options of the log profile on a background thread.</remarks>
readonly record struct LogProfileMatchingCandidate(LogProfile Profile, LogDataSourceOptions DataSourceOptions);


/// <summary>
/// Result of matching a log profile against one or more log files.
/// </summary>
/// <param name="Profile">Log profile which matched the log files.</param>
/// <param name="FileNames">Names of log files which the log profile matched.</param>
/// <param name="Score">Best score among the matched log files.</param>
readonly record struct LogProfileMatchingResult(LogProfile Profile, IList<string> FileNames, LogProfileMatchingScore Score);


/// <summary>
/// Score of matching a log profile against a single log file.
/// </summary>
/// <param name="LogCount">Number of logs read from the log file.</param>
/// <param name="FirstLogLineNumber">Line number of the first line of the first log which was read.</param>
/// <param name="LastLogLineNumber">Line number of the first line of the last log which was read.</param>
/// <param name="RawLineCount">Number of raw log lines consumed from the log file.</param>
/// <param name="ReachedEndOfDataSource">Whether reading stopped because the whole log file has been read or not.</param>
readonly record struct LogProfileMatchingScore(int LogCount, int FirstLogLineNumber, int LastLogLineNumber, int RawLineCount, bool ReachedEndOfDataSource)
{
    /// <summary>
    /// Get average number of raw log lines which each log spans.
    /// </summary>
    /// <remarks>The value is the number of raw log lines between the first and the last log divided by the number of logs, so a tighter parse gives a smaller value. It is <see cref="double.MaxValue"/> when no log was read.</remarks>
    public double RawLineCountPerLog
    {
        get
        {
            if (this.LogCount <= 0)
                return double.MaxValue;
            return (double)(this.LastLogLineNumber - this.FirstLogLineNumber + 1) / this.LogCount;
        }
    }
}
