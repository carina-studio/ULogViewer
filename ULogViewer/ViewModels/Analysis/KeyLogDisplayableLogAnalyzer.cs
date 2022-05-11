using CarinaStudio.Collections;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Analyzer to find key logs.
/// </summary>
class KeyLogDisplayableLogAnalyzer : BaseDisplayableLogAnalyzer<KeyLogDisplayableLogAnalyzer.AnalyzingToken>
{
    /// <summary>
    /// Token of analyzing.
    /// </summary>
    public class AnalyzingToken
    {
        public readonly IDictionary<string, Func<DisplayableLog, object?>> LogPropertyGetters = new Dictionary<string, Func<DisplayableLog, object?>>();
        public readonly IList<Func<DisplayableLog, string?>> LogTextPropertyGetters = new List<Func<DisplayableLog, string?>>();
        public readonly ISet<KeyLogAnalysisRuleSet.Rule> Rules = new HashSet<KeyLogAnalysisRuleSet.Rule>();
    }


    // Result.
    class Result : DisplayableLogAnalysisResult
    {
        // Fields.
        readonly string message;

        // Constructor.
        public Result(KeyLogDisplayableLogAnalyzer analyzer, DisplayableLogAnalysisResultType type, DisplayableLog log, string message) : base(analyzer, type, log) =>
            this.message = message;

        // Update message.
        protected override string? OnUpdateMessage() =>
            this.message;
    }


    // Static fields.
    [ThreadStatic]
    static StringBuilder? LogTextBuffer;
    [ThreadStatic]
    static StringBuilder? ResultMessageBuffer;
    static readonly Regex StringInterpolationRegex = new("(^|[^\\\\])\\{(?<Name>[^\\}\\,\\:]*)(\\,(?<Alignment>[\\+\\-]?\\d+))?(\\:(?<Format>[^\\}]*))?\\}");
    [ThreadStatic]
    static StringBuilder? StringFormatBuffer;


    // Fields.
    readonly List<KeyLogAnalysisRuleSet> attachedRuleSets = new();
    readonly ObservableList<DisplayableLogProperty> logProperties = new(); 
    readonly ObservableList<KeyLogAnalysisRuleSet> ruleSets = new();


    /// <summary>
    /// Initialize new <see cref="KeyLogDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source logs.</param>
    /// <param name="comparison">Comparison for source logs.</param>
    public KeyLogDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison) : base(app, sourceLogs, comparison)
    { 
        this.logProperties.CollectionChanged += this.OnLogPropertiesChanged;
        this.ruleSets.CollectionChanged += this.OnRuleSetsChanged;
    }


    /// <inheritdoc/>
    protected override AnalyzingToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        // check state
        isProcessingNeeded = false;
        var token = new AnalyzingToken();
        if (this.logProperties.IsEmpty() || this.ruleSets.IsEmpty())
            return token;

        // collect rules
        foreach (var ruleSet in this.ruleSets)
            token.Rules.AddAll(ruleSet.Rules);
        if (token.Rules.IsEmpty())
            return token;
        
        // collect log properties
        foreach (var logProperty in this.logProperties)
        {
            if (DisplayableLog.HasStringProperty(logProperty.Name))
                token.LogTextPropertyGetters.Add(DisplayableLog.CreateLogPropertyGetter<string?>(logProperty.Name));
        }
        if (token.LogTextPropertyGetters.IsEmpty())
            return token;

        // complete
        isProcessingNeeded = true;
        return token;
    }


    /// <summary>
    /// Get list of log properties to be included in analysis.
    /// </summary>
    public IList<DisplayableLogProperty> LogProperties { get => this.logProperties; }


    // Called whe list of log properties changed.
    void OnLogPropertiesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        this.InvalidateProcessing();


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalyzingToken token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        // get text to match
        var textBuffer = LogTextBuffer ?? new StringBuilder().Also(it =>
            LogTextBuffer = it);
        foreach (var propertyGetter in token.LogTextPropertyGetters)
        {
            if (textBuffer.Length > 0)
                textBuffer.Append("$$");
            textBuffer.Append(propertyGetter(log));
        }
        if (textBuffer.Length == 0)
        {
            result = this.EmptyResults;
            return false;
        }

        // match rules
        var text = textBuffer.ToString().Also(_ =>
            textBuffer.Clear());
        var partialResults = (List<DisplayableLogAnalysisResult>?)null;
        foreach (var rule in token.Rules)
        {
            var textMatch = rule.Pattern.Match(text);
            if (textMatch.Success)
            {
                // generate message
                var messageFormat = rule.Message;
                var messageBuffer = ResultMessageBuffer ?? new StringBuilder().Also(it =>
                    ResultMessageBuffer = it);
                var formatMatch = StringInterpolationRegex.Match(messageFormat);
                var startIndex = 0;
                while (formatMatch.Success)
                {
                    // append raw text
                    var endIndex = formatMatch.Index;
                    if (messageFormat[endIndex] != '{')
                        ++endIndex;
                    if (endIndex > startIndex)
                        messageBuffer.Append(messageFormat.Substring(startIndex, endIndex - startIndex));
                    
                    // prepare format parameters
                    var varName = formatMatch.Groups["Name"].Value;
                    var alignment = formatMatch.Groups["Alignment"].Let(it =>
                        it.Success ? it.Value : null);
                    var format = formatMatch.Groups["Format"].Let(it =>
                        it.Success ? it.Value : null);
                    var formatBuffer = StringFormatBuffer ?? new StringBuilder().Also(it =>
                        StringFormatBuffer = it);
                    formatBuffer.Append("{0");
                    if (alignment != null)
                    {
                        formatBuffer.Append(',');
                        formatBuffer.Append(alignment);
                    }
                    if (format != null)
                    {
                        formatBuffer.Append(':');
                        formatBuffer.Append(format);
                    }
                    formatBuffer.Append('}');

                    // get value
                    var varValue = (object?)null;
                    try
                    {
                        varValue = DisplayableLog.HasProperty(varName)
                            ? token.LogPropertyGetters.Lock(() =>
                            {
                                return token.LogPropertyGetters.TryGetValue(varName, out var getter) 
                                    ? getter(log) 
                                    : DisplayableLog.CreateLogPropertyGetter<object?>(varName).Also(it =>
                                        token.LogPropertyGetters[varName] = it)(log);
                            })
                            : textMatch.Groups.ContainsKey(varName)
                                ? textMatch.Groups[varName].Let(it => it.Success ? it.Value : null)
                                : null;
                    }
                    catch
                    { }

                    // write value
                    try
                    {
                        messageBuffer.AppendFormat(formatBuffer.ToString(), varValue);
                    }
                    catch 
                    { }
                    formatBuffer.Clear();
                    startIndex = formatMatch.Index + formatMatch.Length;
                    
                    // find next interpolation
                    formatMatch = formatMatch.NextMatch();
                }
                if (startIndex > 0 && startIndex < messageFormat.Length)
                    messageBuffer.Append(messageFormat.Substring(startIndex));
                var message = messageBuffer.Length > 0
                    ? messageBuffer.ToString().Also(_ => messageBuffer.Clear())
                    : messageFormat;

                // generate result
                partialResults ??= new();
                partialResults.Add(new Result(this, rule.ResultType, log, message));
            }
        }

        // complete
        if (partialResults == null)
        {
            result = this.EmptyResults;
            return false;
        }
        result = partialResults;
        return true;
    }


    // Called when rule set changed.
    void OnRuleSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(KeyLogAnalysisRuleSet.Rules))
            this.InvalidateProcessing();
    }


    // Called when list of rule sets changed.
    void OnRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                break;
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                e.NewItems!.Cast<KeyLogAnalysisRuleSet>().Let(it =>
                {
                    foreach (var ruleSet in it)
                        ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                    this.attachedRuleSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var ruleSet in this.attachedRuleSets)
                    ruleSet.PropertyChanged -= this.OnRuleSetPropertyChanged;
                this.attachedRuleSets.Clear();
                foreach (var ruleSet in this.ruleSets)
                    ruleSet.PropertyChanged += this.OnRuleSetPropertyChanged;
                this.attachedRuleSets.AddRange(this.ruleSets);
                break;
            default:
                throw new NotSupportedException("Unsupported change of rule sets.");
        }
        this.InvalidateProcessing();
    }


    /// <summary>
    /// Get list of rule sets for analysis.
    /// </summary>
    public IList<KeyLogAnalysisRuleSet> RuleSets { get => this.ruleSets; }
}