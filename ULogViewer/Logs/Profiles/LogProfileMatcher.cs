using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Matcher which selects log profiles that are able to read given log files.
/// </summary>
static class LogProfileMatcher
{
    // Constants.
    const string FileDataSourceProviderName = "File";
    const string WindowsEventLogFileDataSourceProviderName = "WindowsEventLogFile";


    /// <summary>
    /// Compare two results of matching log profiles for ranking, the better match is ordered first.
    /// </summary>
    /// <param name="lhs">Result to compare.</param>
    /// <param name="rhs">Result to compare with.</param>
    /// <returns>Negative value if <paramref name="lhs"/> is a better match, positive value if <paramref name="rhs"/> is a better match, or 0 if they are equally good.</returns>
    public static int CompareResults(LogProfileMatchingResult lhs, LogProfileMatchingResult rhs)
    {
        // a log profile which matched more log files is a better match
        var result = rhs.FileNames.Count - lhs.FileNames.Count;
        if (result != 0)
            return result;

        // a log profile which started matching earlier in the log file is a better match
        result = lhs.Score.FirstLogLineNumber - rhs.Score.FirstLogLineNumber;
        if (result != 0)
            return result;

        // a log profile which parsed the log file more tightly is a better match
        result = lhs.Score.RawLineCountPerLog.CompareTo(rhs.Score.RawLineCountPerLog);
        if (result != 0)
            return result;

        // a log profile which defines more log patterns is more specific
        result = rhs.Profile.LogPatterns.Count - lhs.Profile.LogPatterns.Count;
        if (result != 0)
            return result;

        // a log profile authored by the user is a stronger signal of intent than a built-in one
        if (lhs.Profile.IsBuiltIn != rhs.Profile.IsBuiltIn)
            return lhs.Profile.IsBuiltIn ? 1 : -1;

        // compare by name to keep the order deterministic
        return string.CompareOrdinal(lhs.Profile.Name, rhs.Profile.Name);
    }


    // Check whether the format of log file is readable by given data source or not.
    static bool IsFormatMatched(LogFileFormat format, ILogDataSourceProvider provider, LogDataSourceOptions options)
    {
        // a Windows event log file is binary, it has nothing to offer to a data source which reads text
        var isWindowsEventLogFileProvider = provider.Name == WindowsEventLogFileDataSourceProviderName;
        if (format == LogFileFormat.WindowsEventLog)
            return isWindowsEventLogFileProvider;
        if (isWindowsEventLogFileProvider)
            return false;

        // the remaining formats are distinguished by how the data source formats the text it reads
        return format switch
        {
            LogFileFormat.Json => options.FormatJsonData,
            LogFileFormat.PlainText => !options.FormatJsonData,
            _ => false,
        };
    }


    /// <summary>
    /// Check whether the score of matching a log profile against a log file is good enough to be treated as a match or not.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="score">Score of matching.</param>
    /// <returns>True if the score is treated as a match.</returns>
    public static bool IsMatched(IULogViewerApplication app, LogProfileMatchingScore score)
    {
        // no log was read at all
        if (score.LogCount <= 0)
            return false;

        // the full quota of logs must be read, unless the whole log file has been read first
        var configuration = app.Configuration;
        if (score.LogCount < configuration.GetValueOrDefault(ConfigurationKeys.MaxLogCountToMatchLogProfile) && !score.ReachedEndOfDataSource)
            return false;

        // the format must start near the head of log file instead of after a page of noise
        if (score.FirstLogLineNumber > configuration.GetValueOrDefault(ConfigurationKeys.MaxSkippedLineCountToMatchLogProfile))
            return false;

        // the logs must be dense enough, a pattern which matches once every few hundred lines is noise
        var maxRawLineCount = score.LogCount * configuration.GetValueOrDefault(ConfigurationKeys.MaxRawLineCountPerLogToMatchLogProfile);
        if (score.LastLogLineNumber - score.FirstLogLineNumber + 1 > maxRawLineCount)
            return false;

        // complete
        return true;
    }


    // Check whether logs can be read by given data source provider without running a script or spawning a process.
    static bool IsProviderAllowed(ILogDataSourceProvider provider) =>
        provider.Name == FileDataSourceProviderName || provider.Name == WindowsEventLogFileDataSourceProviderName;


    /// <summary>
    /// Select log profiles which are eligible to be matched against log files with given format.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="format">Format of log files.</param>
    /// <param name="fileCount">Number of log files to be matched.</param>
    /// <returns>List of candidates of log profile.</returns>
    /// <remarks>The method must be called on the application thread, it reads log profiles which are only allowed to be modified on that thread.</remarks>
    public static IList<LogProfileMatchingCandidate> SelectCandidates(IULogViewerApplication app, LogFileFormat format, int fileCount)
    {
        // check state
        app.VerifyAccess();

        // get state
        var isProVersionActivated = app.ProductManager.IsProductActivated(Products.Professional);
        var fileNameOptionName = nameof(LogDataSourceOptions.FileName);

        // select candidates
        var candidates = new List<LogProfileMatchingCandidate>();
        foreach (var profile in LogProfileManager.Default.Profiles)
        {
            // a template carries no usable data source configuration
            if (profile.IsTemplate)
                continue;

            // a log profile without log patterns is read through the raw fall-back pattern, it matches every text file
            if (profile.LogPatterns.IsEmpty())
                continue;

            // a Pro-only log profile is unusable without activating the Pro version
            if (profile.IsProVersionOnly && !isProVersionActivated)
                continue;

            // a log profile which reads a single file cannot take a drop of multiple files
            if (fileCount > 1 && !profile.AllowMultipleFiles)
                continue;

            // never run a script or spawn a process as a side effect of matching
            var provider = profile.DataSourceProvider;
            if (!IsProviderAllowed(provider))
                continue;

            // the log profile must be driven by the dropped file instead of pinning its own one
            if (!provider.IsSourceOptionRequired(fileNameOptionName))
                continue;
            var options = profile.DataSourceOptions;
            if (options.IsOptionSet(fileNameOptionName))
                continue;

            // the log profile must be able to read the detected format
            if (!IsFormatMatched(format, provider, options))
                continue;
            candidates.Add(new LogProfileMatchingCandidate(profile, options));
        }

        // complete
        return candidates;
    }
}
