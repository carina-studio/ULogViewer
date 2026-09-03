//#define SIMULATE_SLOW_MATCHING

using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using CarinaStudio.Logging;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Matcher which selects log profiles that are able to read given log files.
/// </summary>
static class LogProfileMatcher
{
    // Constants.
    const string FileDataSourceProviderName = "File";
    const string WindowsEventLogFileDataSourceProviderName = "WindowsEventLogFile";
#if SIMULATE_SLOW_MATCHING
    const int SimulatedMatchingDelayPerFile = 3000;
#endif


    // Static fields.
    static ILogger? logger;


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
        var maxLogCount = configuration.GetValueOrDefault(ConfigurationKeys.MaxLogCountToMatchLogProfile);
        if (score.LogCount < maxLogCount && !score.ReachedEndOfDataSource)
            return false;

        // the format must start near the head of log file instead of after a page of noise
        if (score.FirstLogLineNumber > configuration.GetValueOrDefault(ConfigurationKeys.MaxSkippedLineCountToMatchLogProfile))
            return false;

        // the logs must be dense enough, a pattern which matches once every few hundred lines is noise
        var maxRawLineCount = score.LogCount * configuration.GetValueOrDefault(ConfigurationKeys.MaxRawLineCountPerLogToMatchLogProfile);
        if (score.LastLogLineNumber - score.FirstLogLineNumber + 1 > maxRawLineCount)
            return false;

        // the logs must also cover the log file which they were read from, a single log which swallowed the whole file is not a parse
        if (score.LogCount < maxLogCount && score.RawLineCount > maxRawLineCount)
            return false;

        // complete
        return true;
    }


    // Check whether logs can be read by given data source provider without running a script or spawning a process.
    static bool IsProviderAllowed(ILogDataSourceProvider provider) =>
        provider.Name == FileDataSourceProviderName || provider.Name == WindowsEventLogFileDataSourceProviderName;


    /// <summary>
    /// Match log profiles which are able to read given log files.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileNames">Names of log files to be read.</param>
    /// <param name="options">Options of matching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of matching log profiles, the result is ranked with the best match first.</returns>
    /// <remarks>The method must be called on the application thread. Reading logs is performed by <see cref="LogReader"/> on its own background threads, but every log reader is created, started and disposed on the application thread.</remarks>
    public static async Task<IList<LogProfileMatchingResult>> MatchAsync(IULogViewerApplication app, IEnumerable<string> fileNames, LogProfileMatchingOptions options, CancellationToken cancellationToken)
    {
        // check state
        app.VerifyAccess();
        logger ??= app.LoggerFactory.CreateLogger(nameof(LogProfileMatcher));
        var allFileNames = fileNames.ToArray();
        if (allFileNames.IsEmpty())
            return [];

        // a log profile without log patterns reads every text file, report it as matched without reading anything
        if (options.ProfileToMatch is { } profileToMatch && profileToMatch.LogPatterns.IsEmpty())
        {
            logger.LogTrace("Match: {profile} [ok], no log pattern to check", profileToMatch.Name);
            return [ new LogProfileMatchingResult(profileToMatch, allFileNames, new(0, 0, 0, 0, true)) ];
        }

        // examine only the leading log files, the rest ride along on the winning log profile
        var configuration = app.Configuration;
        var maxFileCount = Math.Max(1, configuration.GetValueOrDefault(ConfigurationKeys.MaxFileCountToMatchLogProfile));
        var fileNamesToExamine = allFileNames.Length <= maxFileCount ? allFileNames : allFileNames[..maxFileCount];
        if (allFileNames.Length > fileNamesToExamine.Length)
            logger.LogDebug("Match: examining {count} of {total} log files", fileNamesToExamine.Length, allFileNames.Length);

        // bound the whole operation, a caller which cancels is reported but a timeout only stops further testing
        using var timeoutCancellationTokenSource = new CancellationTokenSource(configuration.GetValueOrDefault(ConfigurationKeys.TimeoutToMatchAllLogProfiles));
        using var matchingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);
        var matchingToken = matchingCancellationTokenSource.Token;
        var maxConcurrency = Math.Max(1, configuration.GetValueOrDefault(ConfigurationKeys.MaxConcurrentLogProfileMatching));
        using var concurrencyLimiter = new SemaphoreSlim(maxConcurrency);

        // match each log file against the candidates of its own format
        var fileNamesByProfile = new Dictionary<LogProfile, List<string>>();
        var scoreByProfile = new Dictionary<LogProfile, LogProfileMatchingScore>();
        try
        {
            foreach (var fileName in fileNamesToExamine)
            {
#if SIMULATE_SLOW_MATCHING
                // slow matching down so that the progress dialog and cancelling it can be exercised by hand
                await Task.Delay(SimulatedMatchingDelayPerFile, matchingToken);
#endif

                // classify the log file, the format narrows the candidates before any log file is read
                var detection = await LogFileFormatDetector.DetectAsync(app, fileName, matchingToken);

                // a group which yields no match cascades to the next one, a Windows event log file is exclusive
                foreach (var format in SelectFormatCascade(detection.Format))
                {
                    // select candidates of the group
                    var candidates = SelectCandidates(app, format, allFileNames.Length);
                    if (options.ProfileToMatch is { } profile)
                        candidates = candidates.Where(it => it.Profile == profile).ToArray();
                    if (candidates.IsEmpty())
                        continue;

                    // read each log file with each candidate, limiting how many log readers run at once
                    var scores = await Task.WhenAll(candidates.Select(async candidate =>
                    {
                        await concurrencyLimiter.WaitAsync(matchingToken);
                        try
                        {
                            return await MatchFileAsync(app, candidate, fileName, detection.Encoding, matchingToken);
                        }
                        finally
                        {
                            concurrencyLimiter.Release();
                        }
                    }));

                    // collect the matches of the group
                    var hasMatch = false;
                    for (var i = 0; i < candidates.Count; ++i)
                    {
                        var score = scores[i];
                        if (!IsMatched(app, score))
                            continue;
                        hasMatch = true;
                        var matchedProfile = candidates[i].Profile;
                        if (fileNamesByProfile.TryGetValue(matchedProfile, out var matchedFileNames))
                        {
                            matchedFileNames.Add(fileName);
                            if (CompareScores(score, scoreByProfile[matchedProfile]) < 0)
                                scoreByProfile[matchedProfile] = score;
                        }
                        else
                        {
                            fileNamesByProfile[matchedProfile] = [ fileName ];
                            scoreByProfile[matchedProfile] = score;
                        }
                    }
                    if (hasMatch)
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // a caller which cancels expects the exception, a timeout keeps whatever has been matched so far
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogWarning("Match: timeout, keeping {count} matched log profiles", fileNamesByProfile.Count);
        }

        // rank the matched log profiles
        var results = fileNamesByProfile.Select(it => new LogProfileMatchingResult(it.Key, it.Value, scoreByProfile[it.Key])).ToList();
        results.Sort(CompareResults);
        logger.LogDebug("Match [ok], profiles: {count}, files: {files}", results.Count, fileNamesToExamine.Length);
        return results;
    }


    // Compare two scores of matching, the better score is ordered first.
    static int CompareScores(LogProfileMatchingScore lhs, LogProfileMatchingScore rhs)
    {
        var result = lhs.FirstLogLineNumber - rhs.FirstLogLineNumber;
        if (result != 0)
            return result;
        return lhs.RawLineCountPerLog.CompareTo(rhs.RawLineCountPerLog);
    }


    // Read one log file with one candidate of log profile and score the result.
    static async Task<LogProfileMatchingScore> MatchFileAsync(IULogViewerApplication app, LogProfileMatchingCandidate candidate, string fileName, Encoding encoding, CancellationToken cancellationToken)
    {
        // drive the dropped log file through the data source of the candidate
        var profile = candidate.Profile;
        var sourceOptions = candidate.DataSourceOptions;
        sourceOptions.FileName = fileName;
        sourceOptions.Encoding = encoding;
        var source = profile.DataSourceProvider.CreateSource(sourceOptions);
        var reader = (LogReader?)null;
        try
        {
            // create log reader, mirroring how a session configures one but bounded and never tailing
            var configuration = app.Configuration;
            var maxRawLogLineCount = Math.Max(1, configuration.GetValueOrDefault(ConfigurationKeys.MaxRawLineCountToMatchLogProfile));
            reader = new LogReader(null, source).Setup(it =>
            {
                it.DefaultLogLevel = profile.DefaultLogLevel;
                it.IsContinuousReading = false;
                it.LogLevelMap = profile.LogLevelMapForReading;
                it.LogPatternMatchingMode = profile.LogPatternMatchingMode;
                it.LogPatterns = profile.LogPatterns;
                it.LogStringEncoding = profile.LogStringEncodingForReading;
                it.MaxLogCount = Math.Max(1, configuration.GetValueOrDefault(ConfigurationKeys.MaxLogCountToMatchLogProfile));
                it.MaxRawLogLineCount = maxRawLogLineCount;
                it.Precondition = new LogReadingPrecondition();
                it.RawLogLevelPropertyName = profile.RawLogLevelPropertyName;
                it.ReadingWindow = LogReadingWindow.StartOfDataSource;
                it.TimeSpanCultureInfo = profile.TimeSpanCultureInfoForReading;
                it.TimeSpanEncoding = profile.TimeSpanEncodingForReading;
                it.TimeSpanFormats = profile.TimeSpanFormatsForReading;
                it.TimestampCultureInfo = profile.TimestampCultureInfoForReading;
                it.TimestampEncoding = profile.TimestampEncodingForReading;
                it.TimestampFormats = profile.TimestampFormatsForReading;
            });

            // read logs until the log reader settles or the pair takes too long
            reader.Start();
            if (!IsTerminalState(reader.State))
            {
                using var timeoutCancellationTokenSource = new CancellationTokenSource(configuration.GetValueOrDefault(ConfigurationKeys.TimeoutToMatchLogProfile));
                using var pairCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);
                try
                {
                    await reader.WaitForPropertyChangeAsync(nameof(LogReader.State), it => IsTerminalState(it.State), pairCancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    logger?.LogTrace("Match: {profile} [timeout]", profile.Name);
                }
            }

            // score the result
            var score = ScoreReader(reader, maxRawLogLineCount);
            logger?.LogTrace("Match: {profile} [ok], logs: {logCount}, lines: {lineCount}", profile.Name, score.LogCount, score.RawLineCount);
            return score;
        }
        finally
        {
            // dispose on the application thread on every path, a leaked log reader keeps reading the log file
            reader?.Dispose();
            source.Dispose();
        }
    }


    // Score the logs which a log reader produced.
    static LogProfileMatchingScore ScoreReader(LogReader reader, int maxRawLogLineCount)
    {
        // reading is exhausted only when it stopped before consuming the whole raw line budget
        var rawLineCount = reader.RawLogLineCount;
        var reachedEndOfDataSource = reader.State == LogReaderState.Stopped && rawLineCount < maxRawLogLineCount;

        // a log reader which produced nothing carries no line numbers to score
        var logs = reader.Logs;
        if (logs.IsEmpty())
            return new LogProfileMatchingScore(0, 0, 0, rawLineCount, reachedEndOfDataSource);

        // line numbers point at the first line of each log
        var firstLogLineNumber = logs[0].LineNumber ?? 1;
        var lastLogLineNumber = logs[^1].LineNumber ?? firstLogLineNumber;
        return new LogProfileMatchingScore(logs.Count, firstLogLineNumber, lastLogLineNumber, rawLineCount, reachedEndOfDataSource);
    }


    // Get the sequence of format groups to try for a log file with given format, stopping at the first group which matches.
    static LogFileFormat[] SelectFormatCascade(LogFileFormat format) => format switch
    {
        LogFileFormat.Binary => [],
        LogFileFormat.WindowsEventLog => [ LogFileFormat.WindowsEventLog ],
        LogFileFormat.Json => [ LogFileFormat.Json, LogFileFormat.PlainText ],
        _ => [ LogFileFormat.PlainText ],
    };


    // Check whether a log reader has settled and will produce no more logs or not.
    static bool IsTerminalState(LogReaderState state) =>
        state is LogReaderState.Stopped or LogReaderState.DataSourceError or LogReaderState.UnclassifiedError or LogReaderState.Disposed;


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
