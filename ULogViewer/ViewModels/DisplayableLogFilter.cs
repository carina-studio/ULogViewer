using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Filter of <see cref="DisplayableLog"/>.
/// </summary>
partial class DisplayableLogFilter : BaseDisplayableLogProcessor<DisplayableLogFilter.FilteringToken, byte>, IDisplayableLogFilter
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
        public IList<DisplayableLogStringPropertyGetter> LogTextPropertyGetters = Array.Empty<DisplayableLogStringPropertyGetter>();
        public int? ProcessId;
        public int? ThreadId;
        public Regex[] TextRegexList = Array.Empty<Regex>();
    }


    // Static fields.
    [ThreadStatic]
    static char[]? logTextBufferToMatch;


    // Fields.
    FilterCombinationMode combinationMode = FilterCombinationMode.Auto;
    readonly SortedObservableList<DisplayableLog> filteredLogs;
    IList<DisplayableLogProperty> filteringLogProperties = Array.Empty<DisplayableLogProperty>();
    bool includeMarkedLogs = true;
    Logs.LogLevel level = Logs.LogLevel.Undefined;
    int? processId;
    IList<Regex> textRegexList = Array.Empty<Regex>();
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
        var textPropertyGetters = new List<DisplayableLogStringPropertyGetter>();
        foreach (var logProperty in this.filteringLogProperties)
        {
            if (DisplayableLog.HasStringProperty(logProperty.Name))
            {
                textPropertyGetters.Add(DisplayableLog.CreateLogStringPropertyGetter(logProperty.Name));
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
                    if (Utility.IsAllMatchingRegex(regex))
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
        filteringToken.CombinationMode = this.combinationMode.Let(it =>
        {
            if (it != FilterCombinationMode.Auto)
                return it;
            if (textRegexList.IsEmpty())
                return FilterCombinationMode.Intersection;
            if (this.processId.HasValue || this.threadId.HasValue)
                return FilterCombinationMode.Union;
            return FilterCombinationMode.Intersection;
        });
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


    // Increase size of text buffer.
    static void IncreaseTextBuffer(ref char[] buffer)
    {
        var newBuffer = buffer.Length <= 1024
            ? new char[buffer.Length << 1]
            : new char[buffer.Length + 1024];
        Array.Copy(buffer, 0, newBuffer, 0, buffer.Length);
        buffer = newBuffer;
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
    protected override int MaxConcurrencyLevel => BaseDisplayableLogProcessors.GetMaxConcurrencyLevel(this.ProcessingPriority);


    /// <inheritdoc/>
    public override long MemorySize => base.MemorySize + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.filteredLogs.Count);


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
    protected override void OnProcessingCancelled(FilteringToken token, bool willStartProcessing)
    {
        this.filteredLogs.Clear();
        if (!willStartProcessing && this.MemoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
            this.filteredLogs.TrimExcess();
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
            ref var textBufferToMatch = ref logTextBufferToMatch;
            var textBufferLength = 0;
            textBufferToMatch ??= new char[512];
            var textBufferCapacity = textBufferToMatch.Length;
            var textSpanToMatch = textBufferToMatch.AsSpan();
            for (var j = 0; j < textPropertyCount; ++j)
            {
                if (j > 0)
                {
                    if (textBufferLength + 2 > textBufferCapacity)
                    {
                        IncreaseTextBuffer(ref textBufferToMatch);
                        textBufferCapacity = textBufferToMatch.Length;
                        textSpanToMatch = textBufferToMatch.AsSpan();
                    }
                    textBufferToMatch[textBufferLength++] = '$'; // special separator between text properties
                    textBufferToMatch[textBufferLength++] = '$';
                }
                while (true)
                {
                    var valueLength = token.LogTextPropertyGetters[j](log, textSpanToMatch, textBufferLength);
                    if (valueLength >= 0)
                    {
                        textBufferLength += valueLength;
                        break;
                    }
                    IncreaseTextBuffer(ref textBufferToMatch);
                    textBufferCapacity = textBufferToMatch.Length;
                    textSpanToMatch = textBufferToMatch.AsSpan();
                }
            }
            for (var j = textRegexCount - 1; j >= 0; --j)
            {
                var e = textRegexList[j].EnumerateMatches(textSpanToMatch[0..textBufferLength]);
                if (e.MoveNext())
                {
                    isTextRegexMatched = true;
                    break;
                }
            }
        }
        if (isTextRegexMatched && isTextFilteringNeeded && token.CombinationMode == FilterCombinationMode.Union)
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
            if (!isTextFilteringNeeded || isTextRegexMatched || token.CombinationMode == FilterCombinationMode.Union)
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