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

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;

/// <summary>
/// Rule set of operation duration analysis.
/// </summary>
class OperationDurationAnalysisRuleSet : BaseProfile<IULogViewerApplication>
{
    /// <summary>
    /// A rule of operation duration analysis.
    /// </summary>
    public class Rule : IEquatable<Rule>
    {
        /// <summary>
        /// Initialize new <see cref="Rule"/> instance.
        /// </summary>
        /// <param name="operationName">Name of operation.</param>
        /// <param name="beginningPattern">Pattern to match text of beginning of operation log.</param>
        /// <param name="beginningConditions">Conditions for beginning of operation log after text matched.</param>
        /// <param name="endingPattern">Pattern to match text of ending of operation log.</param>
        /// <param name="endingConditions">Conditions for ending of operation log after text matched.</param>
        /// <param name="endingOrder">Order of ending operation.</param>
        public Rule(string operationName, Regex beginningPattern, IEnumerable<ContextualBaseAnalysisCondition> beginningConditions, Regex endingPattern, IEnumerable<ContextualBaseAnalysisCondition> endingConditions, OperationEndingOrder endingOrder)
        {
            this.BeginningConditions = beginningConditions.ToArray().AsReadOnly();
            this.BeginningPattern = beginningPattern;
            this.EndingConditions = endingConditions.ToArray().AsReadOnly();
            this.EndingOrder = endingOrder;
            this.EndingPattern = endingPattern;
            this.OperationName = operationName;
        }

        /// <summary>
        /// Get list of conditions for beginning of operation log after text matched.
        /// </summary>
        public IList<ContextualBaseAnalysisCondition> BeginningConditions { get; }

        /// <summary>
        /// Get pattern to match text of beginning of operation log.
        /// </summary>
        public Regex BeginningPattern { get; }

        /// <summary>
        /// Get list of conditions for ending of operation log after text matched.
        /// </summary>
        public IList<ContextualBaseAnalysisCondition> EndingConditions { get; }

        /// <summary>
        /// Get order of ending operation.
        /// </summary>
        public OperationEndingOrder EndingOrder { get; }

        /// <summary>
        /// Get pattern to match text of ending of operation log.
        /// </summary>
        public Regex EndingPattern { get; }

        /// <inheritdoc/>
        public bool Equals(Rule? rule) =>
            rule != null
            && rule.OperationName == this.OperationName
            && rule.BeginningPattern.ToString() == this.BeginningPattern.ToString()
            && rule.BeginningPattern.Options == this.BeginningPattern.Options
            && rule.BeginningConditions.SequenceEqual(this.BeginningConditions)
            && rule.EndingPattern.ToString() == this.EndingPattern.ToString()
            && rule.EndingPattern.Options == this.EndingPattern.Options
            && rule.EndingConditions.SequenceEqual(this.EndingConditions);

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is Rule rule && this.Equals(rule);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            this.OperationName.GetHashCode();

        /// <summary>
        /// Get name of operation.
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Equality operator.
        /// </summary>
        public static bool operator ==(Rule? lhs, Rule? rhs) =>
            lhs?.Equals(rhs) ?? object.ReferenceEquals(rhs, null);
        
        /// <summary>
        /// Inequality operator.
        /// </summary>
        public static bool operator !=(Rule? lhs, Rule? rhs) =>
            object.ReferenceEquals(lhs, null) ? !object.ReferenceEquals(rhs, null) : !lhs.Equals(rhs);
    }


    // Fields.
    LogProfileIcon icon = LogProfileIcon.Analysis;
    IList<Rule> rules = new Rule[0];


    /// <summary>
    /// Initialize new <see cref="OperationDurationAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="name">Name of rule set.</param>
    public OperationDurationAnalysisRuleSet(IULogViewerApplication app, string name) : base(app, OperationDurationAnalysisRuleSetManager.Default.GenerateProfileId(), false)
    {
        this.Name = name;
    }


    // Constructor.
    OperationDurationAnalysisRuleSet(IULogViewerApplication app, string id, bool isBuiltIn) : base(app, id, isBuiltIn)
    { }


    // Change ID.
    internal void ChangeId() =>
        this.Id = OperationDurationAnalysisRuleSetManager.Default.GenerateProfileId();


    /// <inheritdoc/>
    public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
        profile is OperationDurationAnalysisRuleSet ruleSet
        && this.Id == ruleSet.Id
        && this.Name == ruleSet.Name
        && this.icon == ruleSet.icon
        && this.rules.SequenceEqual(ruleSet.rules);
    

    /// <summary>
    /// Get or set icon of rule set.
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


    // Check whether data has been upgraded when loading or not.
    internal bool IsDataUpgraded { get; private set; }
    

    /// <summary>
    /// Load <see cref="OperationDurationAnalysisRuleSet"/> from file asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of loading.</returns>
    public static async Task<OperationDurationAnalysisRuleSet> LoadAsync(IULogViewerApplication app, string fileName)
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
        
        // get ID
        var id = element.TryGetProperty(nameof(Id), out var jsonProperty) && jsonProperty.ValueKind == JsonValueKind.String
            ? jsonProperty.GetString().AsNonNull()
            : KeyLogAnalysisRuleSetManager.Default.GenerateProfileId();
        
        // load
        var ruleSet = new OperationDurationAnalysisRuleSet(app, id, false);
        ruleSet.Load(element);
        return ruleSet;
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
    /// Get or set rules to analyze operation durations.
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
            this.rules = value.ToArray().AsReadOnly();
            this.OnPropertyChanged(nameof(Rules));
        }
    }
}


/// <summary>
/// Order of handling ending of operation.
/// </summary>
enum OperationEndingOrder
{
    /// <summary>
    /// First-in First-out.
    /// </summary>
    FirstInFirstOut,
    /// <summary>
    /// First-in Last-out.
    /// </summary>
    FirstInLastOut,
}