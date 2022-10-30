using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.IO;
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
class OperationDurationAnalysisRuleSet : DisplayableLogAnalysisRuleSet<OperationDurationAnalysisRuleSet.Rule>
{
    /// <summary>
    /// A rule of operation duration analysis.
    /// </summary>
    public class Rule : IEquatable<Rule>
    {
        /// <summary>
        /// Initialize new <see cref="Rule"/> instance.
        /// </summary>
        public Rule(string operationName, 
            DisplayableLogAnalysisResultType resultType,
            Regex beginningPattern, 
            IEnumerable<ContextualBasedAnalysisAction> beginningPreActions,
            IEnumerable<DisplayableLogAnalysisCondition> beginningConditions, 
            IEnumerable<ContextualBasedAnalysisAction> beginningPostActions,
            Regex endingPattern, 
            IEnumerable<ContextualBasedAnalysisAction> endingPreActions,
            IEnumerable<DisplayableLogAnalysisCondition> endingConditions, 
            IEnumerable<ContextualBasedAnalysisAction> endingPostActions,
            OperationEndingMode endingMode,
            IEnumerable<string> endingVars,
            TimeSpan? minDuration,
            TimeSpan? maxDuration,
            string? customMessage,
            string? byteSizeVarName,
            FileSizeUnit byteSizeUnit,
            string? quantityVarName)
        {
            minDuration?.Let(min =>
            {
                maxDuration?.Let(max =>
                {
                    if (min.Ticks < 0)
                    {
                        min = default;
                        minDuration = null;
                    }
                    if (max.Ticks < 0)
                    {
                        max = default;
                        maxDuration = null;
                    }
                    if (max < min)
                        maxDuration = minDuration;
                });
            });
            this.BeginningConditions = ListExtensions.AsReadOnly(beginningConditions.ToArray());
            this.BeginningPattern = beginningPattern;
            this.BeginningPostActions = ListExtensions.AsReadOnly(beginningPostActions.ToArray());
            this.BeginningPreActions = ListExtensions.AsReadOnly(beginningPreActions.ToArray());
            this.ByteSizeUnit = byteSizeUnit;
            this.ByteSizeVariableName = string.IsNullOrWhiteSpace(byteSizeVarName) ? null : byteSizeVarName;
            this.CustomMessage = customMessage;
            this.EndingConditions = ListExtensions.AsReadOnly(endingConditions.ToArray());
            this.EndingMode = endingMode;
            this.EndingPattern = endingPattern;
            this.EndingPostActions = ListExtensions.AsReadOnly(endingPostActions.ToArray());
            this.EndingPreActions = ListExtensions.AsReadOnly(endingPreActions.ToArray());
            this.EndingVariables = ListExtensions.AsReadOnly(endingVars.ToArray());
            this.MaxDuration = maxDuration;
            this.MinDuration = minDuration;
            this.OperationName = operationName;
            this.QuantityVariableName = string.IsNullOrWhiteSpace(quantityVarName) ? null : quantityVarName;
            this.ResultType = resultType;
        }

        /// <summary>
        /// Initialize new <see cref="Rule"/> instance.
        /// </summary>
        /// <param name="template">Template rule.</param>
        /// <param name="operationName">Operation name.</param>
        public Rule(Rule template, string operationName)
        {
            this.BeginningConditions = template.BeginningConditions;
            this.BeginningPattern = template.BeginningPattern;
            this.BeginningPostActions = template.BeginningPostActions;
            this.BeginningPreActions = template.BeginningPreActions;
            this.ByteSizeUnit = template.ByteSizeUnit;
            this.ByteSizeVariableName = template.ByteSizeVariableName;
            this.CustomMessage = template.CustomMessage;
            this.EndingConditions = template.EndingConditions;
            this.EndingMode = template.EndingMode;
            this.EndingPattern = template.EndingPattern;
            this.EndingPostActions = template.EndingPostActions;
            this.EndingPreActions = template.EndingPreActions;
            this.EndingVariables = template.EndingVariables;
            this.MaxDuration = template.MaxDuration;
            this.MinDuration = template.MinDuration;
            this.OperationName = operationName;
            this.QuantityVariableName = template.QuantityVariableName;
            this.ResultType = template.ResultType;
        }

        /// <summary>
        /// Get list of conditions for beginning of operation log after text matched.
        /// </summary>
        public IList<DisplayableLogAnalysisCondition> BeginningConditions { get; }

        /// <summary>
        /// Get pattern to match text of beginning of operation log.
        /// </summary>
        public Regex BeginningPattern { get; }

        /// <summary>
        /// Get list of actions to perform after all beginning conditions matched.
        /// </summary>
        public IList<ContextualBasedAnalysisAction> BeginningPostActions { get; }

        /// <summary>
        /// Get list of actions to perform before all matching beginning conditions.
        /// </summary>
        public IList<ContextualBasedAnalysisAction> BeginningPreActions { get; }

        /// <summary>
        /// Unit to parse byte size of result.
        /// </summary>
        public FileSizeUnit ByteSizeUnit { get; }

        /// <summary>
        /// Name of variable to be treat as byte size of result.
        /// </summary>
        public string? ByteSizeVariableName { get; }

        /// <summary>
        /// Get custom formatted message.
        /// </summary>
        public string? CustomMessage { get; }

        /// <summary>
        /// Get list of conditions for ending of operation log after text matched.
        /// </summary>
        public IList<DisplayableLogAnalysisCondition> EndingConditions { get; }

        /// <summary>
        /// Get mode of ending operation.
        /// </summary>
        public OperationEndingMode EndingMode { get; }

        /// <summary>
        /// Get pattern to match text of ending of operation log.
        /// </summary>
        public Regex EndingPattern { get; }

        /// <summary>
        /// Get list of actions to perform after all ending conditions matched.
        /// </summary>
        public IList<ContextualBasedAnalysisAction> EndingPostActions { get; }

        /// <summary>
        /// Get list of actions to perform before all matching ending conditions.
        /// </summary>
        public IList<ContextualBasedAnalysisAction> EndingPreActions { get; }

        /// <summary>
        /// Get list of variables to compare when <see cref="EndingMode"/> is <see cref="OperationEndingMode.CompareVariables"/>.
        /// </summary>
        public IList<string> EndingVariables { get; }

        /// <inheritdoc/>
        public bool Equals(Rule? rule) =>
            rule != null
            && rule.OperationName == this.OperationName
            && rule.BeginningPattern.ToString() == this.BeginningPattern.ToString()
            && rule.BeginningPattern.Options == this.BeginningPattern.Options
            && rule.BeginningConditions.SequenceEqual(this.BeginningConditions)
            && rule.BeginningPreActions.SequenceEqual(this.BeginningPreActions)
            && rule.BeginningPostActions.SequenceEqual(this.BeginningPostActions)
            && rule.ByteSizeUnit == this.ByteSizeUnit
            && rule.ByteSizeVariableName == this.ByteSizeVariableName
            && rule.CustomMessage == this.CustomMessage
            && rule.EndingPattern.ToString() == this.EndingPattern.ToString()
            && rule.EndingPattern.Options == this.EndingPattern.Options
            && rule.EndingConditions.SequenceEqual(this.EndingConditions)
            && rule.EndingPreActions.SequenceEqual(this.EndingPreActions)
            && rule.EndingPostActions.SequenceEqual(this.EndingPostActions)
            && rule.EndingVariables.SequenceEqual(this.EndingVariables)
            && rule.MaxDuration == this.MaxDuration
            && rule.MinDuration == this.MinDuration
            && rule.QuantityVariableName == this.QuantityVariableName
            && rule.ResultType == this.ResultType;

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is Rule rule && this.Equals(rule);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            this.OperationName.GetHashCode();
        
        /// <summary>
        /// Get the upper bound of duration to generate result.
        /// </summary>
        public TimeSpan? MaxDuration { get; }

        /// <summary>
        /// Get the lower bound of duration to generate result.
        /// </summary>
        public TimeSpan? MinDuration { get; }

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
        
        /// <summary>
        /// Name of variable to be treat as quantity of result.
        /// </summary>
        public string? QuantityVariableName { get; }
        
        /// <summary>
        /// Get result type.
        /// </summary>
        public DisplayableLogAnalysisResultType ResultType { get; }
    }


    /// <summary>
    /// Initialize new <see cref="OperationDurationAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="name">Name of rule set.</param>
    public OperationDurationAnalysisRuleSet(IULogViewerApplication app, string name) : base(app, OperationDurationAnalysisRuleSetManager.Default.GenerateProfileId())
    {
        this.Name = name;
    }


    /// <summary>
    /// Initialize new <see cref="OperationDurationAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="template">Template rule set.</param>
    /// <param name="name">Name.</param>
    public OperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet template, string name) : base(template, name, OperationDurationAnalysisRuleSetManager.Default.GenerateProfileId())
    { }


    // Constructor.
    OperationDurationAnalysisRuleSet(IULogViewerApplication app, string id, bool isBuiltIn) : base(app, id, isBuiltIn)
    { }


    // Change ID.
    internal override void ChangeId() =>
        this.Id = OperationDurationAnalysisRuleSetManager.Default.GenerateProfileId();
    

    /// <summary>
    /// Load <see cref="OperationDurationAnalysisRuleSet"/> from file asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <param name="checkType">True to check whether type written in file is correct or not.</param>
    /// <returns>Task of loading.</returns>
    public static async Task<OperationDurationAnalysisRuleSet> LoadAsync(IULogViewerApplication app, string fileName, bool checkType)
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
                || jsonValue.GetString() != nameof(OperationDurationAnalysisRuleSet))
            {
                throw new ArgumentException($"Invalid type: {jsonValue}.");
            }
        }
        
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
}


/// <summary>
/// Mode of handling ending of operation.
/// </summary>
enum OperationEndingMode
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