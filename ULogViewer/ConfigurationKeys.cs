using CarinaStudio.Configuration;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Configuration keys.
/// </summary>
abstract class ConfigurationKeys
{
    /// <summary>
    /// Interval of updating continuous logs reading in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> ContinuousLogsReadingUpdateInterval = new(nameof(ContinuousLogsReadingUpdateInterval), 100);
    /// <summary>
    /// Delay before restarting contextual-based log analysis in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DelayToRestartContextualBasedLogAnalysis = new(nameof(DelayToRestartContextualBasedLogAnalysis), 1000);
    /// <summary>
    /// Interval between each displayable log chunk processing in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkProcessingPaddingIntervalBackground = new(nameof(DisplayableLogChunkProcessingPaddingIntervalBackground), 300);
    /// <summary>
    /// Interval between each displayable log chunk processing in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkProcessingPaddingIntervalDefault = new(nameof(DisplayableLogChunkProcessingPaddingIntervalDefault), 100);
    /// <summary>
    /// Interval between each displayable log chunk processing in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkProcessingPaddingIntervalRealtime = new(nameof(DisplayableLogChunkProcessingPaddingIntervalRealtime), 50);
    /// <summary>
    /// Size of chunk of displayable log processing.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkProcessingSizeBackground = new(nameof(DisplayableLogChunkProcessingSizeBackground), 1024);
     /// <summary>
    /// Size of chunk of displayable log processing.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkProcessingSizeDefault = new(nameof(DisplayableLogChunkProcessingSizeDefault), 4096);
     /// <summary>
    /// Size of chunk of displayable log processing.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkProcessingSizeRealtime = new(nameof(DisplayableLogChunkProcessingSizeRealtime), 16384);
    /// <summary>
    /// Delay before start processing displayable logs in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogProcessingDelayBackground = new(nameof(DisplayableLogProcessingDelayBackground), 1000);
    /// <summary>
    /// Delay before start processing displayable logs in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogProcessingDelayDefault = new(nameof(DisplayableLogProcessingDelayDefault), 500);
    /// <summary>
    /// Delay before applying log analysis parameters in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> LogAnalysisParamsUpdateDelay = new(nameof(LogAnalysisParamsUpdateDelay), 500);
    /// <summary>
    /// Initial ratio of height of LogAnalysisScriptSetEditorDialog.
    /// </summary>
    public static readonly SettingKey<double> LogAnalysisScriptSetEditorDialogInitHeightRatio = new(nameof(LogAnalysisScriptSetEditorDialogInitHeightRatio), 0.9);
    /// <summary>
    /// Initial ratio of width of LogAnalysisScriptSetEditorDialog.
    /// </summary>
    public static readonly SettingKey<double> LogAnalysisScriptSetEditorDialogInitWidthRatio = new(nameof(LogAnalysisScriptSetEditorDialogInitWidthRatio), 0.6);
    /// <summary>
    /// Delay time in milliseconds to show view of log properties.
    /// </summary>
    public static readonly SettingKey<int> LogPropertyViewsShowingDelay = new(nameof(LogPropertyViewsShowingDelay), 33);
    /// <summary>
    /// Size of string cache for log reading in MB.
    /// </summary>
    public static readonly SettingKey<int> LogReadingStringCacheSizeInMB = new(nameof(LogReadingStringCacheSizeInMB), 8);
    /// <summary>
    /// Maximum number of log text filter to be kept in history queue.
    /// </summary>
    public static readonly SettingKey<int> LogTextFilterHistoryCount = new(nameof(LogTextFilterHistoryCount), 16);
    /// <summary>
    /// Maximum number of series in log chart.
    /// </summary>
    public static readonly SettingKey<int> MaxSeriesCountInLogChart = new(nameof(MaxValueCountInLogChart), 10);
    /// <summary>
    /// Maximum number of value in log chart.
    /// </summary>
    public static readonly SettingKey<int> MaxValueCountInLogChart = new(nameof(MaxValueCountInLogChart), 10000);
    /// <summary>
    /// Size of chunk of non-continuous logs reading.
    /// </summary>
    public static readonly SettingKey<int> NonContinuousLogsReadingUpdateChunkSize = new(nameof(NonContinuousLogsReadingUpdateChunkSize), 32768);
    /// <summary>
    /// Interval of updating non-continuous logs reading in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> NonContinuousLogsReadingUpdateInterval = new(nameof(NonContinuousLogsReadingUpdateInterval), 2000);
    /// <summary>
    /// Interval between non-continuous logs reading reporting in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> NonContinuousLogsReadingPaddingInterval = new(nameof(NonContinuousLogsReadingPaddingInterval), 50);
    /// <summary>
    /// Size of chunk of progressive log clearing.
    /// </summary>
    public static readonly SettingKey<int> ProgressiveLogClearingChunkSize = new(nameof(ProgressiveLogClearingChunkSize), 8192);
    /// <summary>
    /// Interval of progressive log clearing in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> ProgressiveLogClearingInterval = new(nameof(ProgressiveLogClearingInterval), 60);
    /// <summary>
    /// Interval of restarting continuous log reading when there is no log read yet.
    /// </summary>
    public static readonly SettingKey<long> RestartContinuousLogReadingWhenNoLogReadDelay = new(nameof(RestartContinuousLogReadingWhenNoLogReadDelay), 2000);
    /// <summary>
    /// Try reading raw log lines concurrently.
    /// </summary>
    public static readonly SettingKey<bool> ReadRawLogLinesConcurrently = new(nameof(ReadRawLogLinesConcurrently), false);
    /// <summary>
    /// Timeout before calculating size of one or more files.
    /// </summary>
    public static readonly SettingKey<int> TimeoutToCancelFileSizeCalculation = new(nameof(TimeoutToCancelFileSizeCalculation), 5 * 1000);
    /// <summary>
    /// Whether regular expression should be compiled before using or not.
    /// </summary>
    public static readonly SettingKey<bool> UseCompiledRegex = new(nameof(UseCompiledRegex), true);
    /// <summary>
    /// Smooth scrolling of logs.
    /// </summary>
    public static readonly SettingKey<bool> UseSmoothLogScrolling = new(nameof(UseSmoothLogScrolling), true);


    // Constructor.
    ConfigurationKeys() 
    { }
}