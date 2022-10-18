using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Rule set of operation counting analysis,
/// </summary>
class OperationCountingAnalysisRuleSet : BaseProfile<IULogViewerApplication>, ILogProfileIconSource
{
    /// <summary>
    /// Analysis rule.
    /// </summary>
    public class Rule : IEquatable<Rule>
    {
        /// <summary>
        /// Initialize new <see cref="Rule"/> instance.
        /// </summary>
        /// <param name="operationName">Name of operation.</param>
        /// <param name="interval">Interval of counting operation.</param>
        /// <param name="pattern">Pattern to match log of operation.</param>
        /// <param name="level">Level to match log of operation.</param>
        /// <param name="conditions">Conditions to match log of operation.</param>
        /// <param name="resultType">Type of analysis result to be generated when operation found.</param>
        public Rule(string operationName, TimeSpan interval, Regex pattern, Logs.LogLevel level, IEnumerable<DisplayableLogAnalysisCondition> conditions, DisplayableLogAnalysisResultType resultType)
        {
            if (interval.Ticks <= 0)
                throw new ArgumentOutOfRangeException(nameof(interval));
            this.Conditions = conditions is IList<DisplayableLogAnalysisCondition> list
                ? list.AsReadOnly()
                : conditions.ToArray().AsReadOnly();
            this.Interval = interval;
            this.Level = level;
            this.OperationName = operationName;
            this.Pattern = pattern;
            this.ResultType = resultType;
        }

        /// <summary>
        /// Conditions to match log.
        /// </summary>
        public IList<DisplayableLogAnalysisCondition> Conditions { get; }

        /// <inheritdoc/>
        public bool Equals(Rule? rule) =>
            rule != null
            && rule.Conditions.SequenceEqual(this.Conditions)
            && rule.Interval == this.Interval
            && rule.Level == this.Level
            && rule.OperationName == this.OperationName
            && rule.Pattern.ToString() == this.Pattern.ToString()
            && rule.Pattern.Options == this.Pattern.Options
            && rule.ResultType == this.ResultType;

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is Rule rule && this.Equals(rule);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            this.Pattern.ToString().GetHashCode();
        
        /// <summary>
        /// Interval of counting operation.
        /// </summary>
        public TimeSpan Interval { get; }
        
        /// <summary>
        /// Get level to match log of operation.
        /// </summary>
        public Logs.LogLevel Level { get; }

        /// <summary>
        /// Get name of operation
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Get pattern to match log of operation.
        /// </summary>
        public Regex Pattern { get; }

        /// <summary>
        /// Get type of analysis result to be generated when operation found.
        /// </summary>
        public DisplayableLogAnalysisResultType ResultType { get; }
    }


    // Fields.
    LogProfileIcon icon = LogProfileIcon.Analysis;
    LogProfileIconColor iconColor = LogProfileIconColor.Default;
    IList<Rule> rules = new Rule[0];


    // Constructor.
    OperationCountingAnalysisRuleSet(IULogViewerApplication app, string id) : base(app, id, false)
    { }


    /// <summary>
    /// Initialize new <see cref="OperationCountingAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public OperationCountingAnalysisRuleSet(IULogViewerApplication app) : this(app, OperationCountingAnalysisRuleSetManager.Default.GenerateProfileId())
    { }


    /// <summary>
    /// Initialize new <see cref="OperationCountingAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="template">Template rule set.</param>
    /// <param name="name">Name.</param>
    public OperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet template, string name) : this(template.Application)
    {
        this.icon = template.icon;
        this.iconColor = template.iconColor;
        this.Name = name;
        this.rules = template.rules;
    }


    // Change ID.
    internal void ChangeId() =>
        this.Id = KeyLogAnalysisRuleSetManager.Default.GenerateProfileId();


    /// <inheritdoc/>
    public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
        profile is OperationCountingAnalysisRuleSet ruleSet
        && this.Id == ruleSet.Id
        && this.icon == ruleSet.icon
        && this.iconColor == ruleSet.iconColor
        && this.Name == ruleSet.Name
        && this.rules.SequenceEqual(ruleSet.rules);
    

    /// <summary>
    /// Get or set icon of rule sets.
    /// </summary>
    public LogProfileIcon Icon
    {
        get => this.icon;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.icon == value)
                return;
            this.icon = value;
            this.OnPropertyChanged(nameof(Icon));
        }
    }


    /// <summary>
    /// Get or set color of icon of rule sets.
    /// </summary>
    public LogProfileIconColor IconColor
    {
        get => this.iconColor;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.iconColor == value)
                return;
            this.iconColor = value;
            this.OnPropertyChanged(nameof(IconColor));
        }
    }


    // Check whether data has been upgraded when loading or not.
    internal bool IsDataUpgraded { get; private set; }
    

    /// <summary>
    /// Load profile from file.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="checkType">True to check whether type written in file is correct or not.</param>
    /// <returns>Task of loading profile.</returns>
    public static async Task<OperationCountingAnalysisRuleSet> LoadAsync(IULogViewerApplication app, string fileName, bool checkType)
    {
        // load JSON data
        using var jsonDocument = await ProfileExtensions.IOTaskFactory.StartNew(() =>
        {
            using var reader = new StreamReader(fileName, System.Text.Encoding.UTF8);
            return JsonDocument.Parse(reader.ReadToEnd());
        });
        var element = jsonDocument.RootElement;
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Root element must be an object.");
        if (checkType)
        {
            if (!element.TryGetProperty("Type", out var jsonValue)
                || jsonValue.ValueKind != JsonValueKind.String
                || jsonValue.GetString() != nameof(OperationCountingAnalysisRuleSet))
            {
                throw new ArgumentException($"Invalid type: {jsonValue}.");
            }
        }
        
        // get ID
        var id = element.TryGetProperty(nameof(Id), out var jsonProperty) && jsonProperty.ValueKind == JsonValueKind.String
            ? jsonProperty.GetString().AsNonNull()
            : KeyLogAnalysisRuleSetManager.Default.GenerateProfileId();
        
        // load
        var profile = new OperationCountingAnalysisRuleSet(app, id);
        profile.Load(element);
        return profile;
    }


    /// <inheritdoc/>
    protected override void OnLoad(JsonElement element)
    { 
    }


    /// <inheritdoc/>
    protected override void OnSave(Utf8JsonWriter writer, bool includeId)
    {
    }


    /// <summary>
    /// Get or set list of rule for this set.
    /// </summary>
    public IList<Rule> Rules
    {
        get => this.rules;
        set
        {
            this.VerifyAccess();
            this.VerifyBuiltIn();
            if (this.rules.SequenceEqual(value))
                return;
            this.rules = value.AsReadOnly();
            this.OnPropertyChanged(nameof(Rules));
        }
    }
}