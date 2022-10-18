using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Filter of <see cref="DisplayableLog"/>.
/// </summary>
class DisplayableLogFilter : BaseDisplayableLogProcessor<DisplayableLogFilter.FilteringToken, byte>, IDisplayableLogFilter
{
    // Token of filtering.
    public class FilteringToken
    {
        public FilterCombinationMode CombinationMode;
        public bool HasLogProcessId;
        public bool HasLogTextPropertyGetter;
        public bool HasLogThreadId;
        public bool HasTextRegex;
        public bool IncludeMarkedLogs;
        public volatile bool IsTextRegexListReady;
        public Logs.LogLevel Level;
        public IList<Func<DisplayableLog, string?>> LogTextPropertyGetters = new Func<DisplayableLog, string?>[0];
        public int? ProcessId;
        public int? ThreadId;
        public Regex[] TextRegexList = new Regex[0];
    }


    // Static fields.
	static readonly Regex allMatchingPatternRegex = new Regex(@"^\.[\*\+]{0,1}$");
    [ThreadStatic]
    static StringBuilder? logTextToMatchBuilder;


    // Fields.
    FilterCombinationMode combinationMode = FilterCombinationMode.Intersection;
    readonly SortedObservableList<DisplayableLog> filteredLogs;
    IList<DisplayableLogProperty> filteringLogProperties = new DisplayableLogProperty[0];
    bool includeMarkedLogs = true;
    Logs.LogLevel level = Logs.LogLevel.Undefined;
    int? processId;
    IList<Regex> textRegexList = new Regex[0];
    int? threadId;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogFilter"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparer"><see cref="IComparer{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public DisplayableLogFilter(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, IComparer<DisplayableLog> comparer) : this(app, sourceLogs, comparer.Compare)
    { }


    /// <summary>
    /// Initialize new <see cref="DisplayableLogFilter"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    public DisplayableLogFilter(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison, DisplayableLogProcessingPriority.Realtime)
    {
        this.filteredLogs = new SortedObservableList<DisplayableLog>(comparison);
        this.FilteredLogs = new Collections.SafeReadOnlyList<DisplayableLog>(this.filteredLogs);
    }


    /// <summary>
    /// Get or set mode to combine condition of <see cref="TextRegexList"/> and other conditions excluding <see cref="IncludeMarkedLogs"/>.
    /// </summary>
    public FilterCombinationMode CombinationMode
    {
        get => this.combinationMode;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.combinationMode == value)
                return;
            this.combinationMode = value;
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(CombinationMode));
        }
    }


    /// <inheritdoc/>
    protected override FilteringToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        // check log properties
        isProcessingNeeded = false;
        var filteringToken = new FilteringToken();
        var textPropertyGetters = new List<Func<DisplayableLog, string?>>();
        foreach (var logProperty in this.filteringLogProperties)
        {
            if (DisplayableLog.HasStringProperty(logProperty.Name))
            {
                textPropertyGetters.Add(DisplayableLog.CreateLogPropertyGetter<string?>(logProperty.Name));
                isProcessingNeeded = true;
            }
            else if (logProperty.Name == nameof(DisplayableLog.ProcessId))
                filteringToken.HasLogProcessId = true;
            else if (logProperty.Name == nameof(DisplayableLog.ThreadId))
                filteringToken.HasLogThreadId = true;
        }
        if (isProcessingNeeded)
        {
            if (this.textRegexList.IsNotEmpty())
            {
                foreach (var regex in this.textRegexList)
                {
                    if (this.IsAllMatchingRegex(regex))
                    {
                        isProcessingNeeded = false;
                        break;
                    }
                }
            }
            else
                isProcessingNeeded = false;
        }
        if (!isProcessingNeeded)
            isProcessingNeeded = (this.level != Logs.LogLevel.Undefined);
        if (!isProcessingNeeded)
            isProcessingNeeded = (this.processId != null);
        if (!isProcessingNeeded)
            isProcessingNeeded = (this.threadId != null);
        
        // no need to filter
        if (!isProcessingNeeded)
            return filteringToken;
        
        // setup token
        filteringToken.CombinationMode = this.combinationMode;
        filteringToken.HasLogTextPropertyGetter = textPropertyGetters.IsNotEmpty();
        filteringToken.HasTextRegex = this.textRegexList.IsNotEmpty();
        filteringToken.IncludeMarkedLogs = this.includeMarkedLogs;
        filteringToken.Level = this.level;
        filteringToken.LogTextPropertyGetters = textPropertyGetters;
        filteringToken.ProcessId = this.processId;
        filteringToken.TextRegexList = this.textRegexList.ToArray();
        filteringToken.ThreadId = this.threadId;
        return filteringToken;
    }


    /// <summary>
    /// Get list of filtered logs.
    /// </summary>
    /// <remarks>The list implements <see cref="INotifyCollectionChanged"/> interface.</remarks>
    public IList<DisplayableLog> FilteredLogs { get; }


    /// <summary>
    /// Get or set list of <see cref="DisplayableLogProperty"/> to be considered into filtering.
    /// </summary>
    public IList<DisplayableLogProperty> FilteringLogProperties
    {
        get => this.filteringLogProperties;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.filteringLogProperties.SequenceEqual(value))
                return;
            this.filteringLogProperties = new List<DisplayableLogProperty>(value).AsReadOnly();
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(FilteringLogProperties));
        }
    }


    /// <summary>
    /// Get or set whether marked logs should be filtered out or not.
    /// </summary>
    public bool IncludeMarkedLogs
    {
        get => this.includeMarkedLogs;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.includeMarkedLogs == value)
                return;
            this.includeMarkedLogs = value;
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(IncludeMarkedLogs));
        }
    }


    // Check whether given regex can match all strings or not.
    bool IsAllMatchingRegex(Regex regex)
    {
        var pattern = regex.ToString();
        var patternLength = pattern.Length;
        var patternStart = 0;
        var subPatternBuffer = new StringBuilder();
        var bracketCount = 0;
        while (patternStart < patternLength)
        {
            var c = pattern[patternStart++];
            switch (c)
            {
                case '|':
                    if (bracketCount == 0)
                    {
                        if (subPatternBuffer.Length == 0 || allMatchingPatternRegex.IsMatch(subPatternBuffer.ToString()))
                            return true;
                        subPatternBuffer.Clear();
                        break;
                    }
                    goto default;
                case '\\':
                    subPatternBuffer.Append(c);
                    if (patternStart >= patternLength)
                        break;
                    c = pattern[patternStart++];
                    goto default;
                case '(':
                    ++bracketCount;
                    goto default;
                case ')':
                    --bracketCount;
                    goto default;
                default:
                    subPatternBuffer.Append(c);
                    break;
            }
        }
        if (bracketCount == 0)
            return (subPatternBuffer.Length == 0 || allMatchingPatternRegex.IsMatch(subPatternBuffer.ToString()));
        return false;
    }


    /// <summary>
    /// Get or set level of log to be filtered.
    /// </summary>
    public Logs.LogLevel Level
    {
        get => this.level;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.level == value)
                return;
            this.level = value;
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(Level));
        }
    }


    /// <inheritdoc/>
    protected override int MaxConcurrencyLevel => GetMaxConcurrencyLevel(this.ProcessingPriority);


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize + TypeExtensions.EstimateCollectionInstanceSize(IntPtr.Size, this.filteredLogs.Count);


    /// <inheritdoc/>
    protected override void OnChunkProcessed(FilteringToken token, List<DisplayableLog> logs, List<byte> results) =>
        this.filteredLogs.AddAll(logs, true);


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log)
    {
        if (this.includeMarkedLogs && log.IsMarked)
        {
            if (!this.filteredLogs.Contains(log))
                this.filteredLogs.Add(log);
            return true;
        }
        this.filteredLogs.Remove(log);
        return false;
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(FilteringToken token)
    {
        this.filteredLogs.Clear();
    }


    /// <inheritdoc/>
    protected override bool OnProcessLog(FilteringToken token, DisplayableLog log, out byte result)
    {
        // check marking state
        result = 0; // not used
        if (token.IncludeMarkedLogs && log.MarkedColor != MarkColor.None)
            return true;

        // check text regex
        var isTextFilteringNeeded = (token.HasTextRegex && token.HasLogTextPropertyGetter);
        var isTextRegexMatched = false;
        if (isTextFilteringNeeded)
        {
            var textRegexList = token.TextRegexList;
            var textRegexCount = textRegexList.Length;
            if (!token.IsTextRegexListReady)
            {
                lock (token)
                {
                    if (!token.IsTextRegexListReady)
                    {
                        if (this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.UseCompiledRegex))
                        {
                            for (var i = textRegexCount - 1; i >= 0 ; --i)
                            {
                                var regex = textRegexList[i];
                                if ((regex.Options & RegexOptions.Compiled) == 0)
                                    textRegexList[i] = new(regex.ToString(), regex.Options | RegexOptions.Compiled);
                            }
                        }
                        token.IsTextRegexListReady = true;
                    }
                }
            }
            var textPropertyCount = token.LogTextPropertyGetters.Count;
            var textToMatchBuilder = logTextToMatchBuilder ?? new StringBuilder().Also(it => logTextToMatchBuilder = it);
            for (var j = 0; j < textPropertyCount; ++j)
            {
                if (j > 0)
                    textToMatchBuilder.Append("$$"); // special separator between text properties
                textToMatchBuilder.Append(token.LogTextPropertyGetters[j](log));
            }
            for (var j = textRegexCount - 1; j >= 0; --j)
            {
                if (textRegexList[j].IsMatch(textToMatchBuilder.ToString()))
                {
                    isTextRegexMatched = true;
                    break;
                }
            }
            textToMatchBuilder.Remove(0, textToMatchBuilder.Length);
        }
        if (isTextRegexMatched && isTextFilteringNeeded && combinationMode == FilterCombinationMode.Union)
            return true;

        // check level
        var areOtherConditionsMatched = true;
        if (level != Logs.LogLevel.Undefined && log.Level != level)
            areOtherConditionsMatched = false;
        if (areOtherConditionsMatched && token.ProcessId.HasValue && token.HasLogProcessId)
            areOtherConditionsMatched = (token.ProcessId == log.ProcessId);
        if (areOtherConditionsMatched && token.ThreadId.HasValue && token.HasLogThreadId)
            areOtherConditionsMatched = (token.ThreadId == log.ThreadId);

        // filter
        if (areOtherConditionsMatched)
        {
            if (!isTextFilteringNeeded || isTextRegexMatched || combinationMode == FilterCombinationMode.Union)
                return true;
        }
        return false;
    }


    /// <inheritdoc/>
    protected override void OnSourceLogsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnSourceLogsChanged(e);
        if (e.Action == NotifyCollectionChangedAction.Remove)
            this.filteredLogs.RemoveAll(e.OldItems.AsNonNull().Cast<DisplayableLog>(), true);
    }


    /// <summary>
    /// Get or set process ID of log to filter.
    /// </summary>
    public int? ProcessId
    {
        get => this.processId;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.processId == value)
                return;
            this.processId = value;
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(ProcessId));
        }
    }


    /// <summary>
    /// Get or set list of <see cref="Regex"/> to filter text properties.
    /// </summary>
    public IList<Regex> TextRegexList
    {
        get => this.textRegexList;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.textRegexList.SequenceEqual(value))
                return;
            this.textRegexList = new List<Regex>(value).AsReadOnly();
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(TextRegexList));
        }
    }


    /// <summary>
    /// Get or set thread ID of log to filter.
    /// </summary>
    public int? ThreadId
    {
        get => this.threadId;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.threadId == value)
                return;
            this.threadId = value;
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(ThreadId));
        }
    }
}