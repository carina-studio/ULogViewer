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
class OperationCountingAnalysisRuleSet : DisplayableLogAnalysisRuleSet<OperationCountingAnalysisRuleSet.Rule>
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
                ? ListExtensions.AsReadOnly(list)
                : ListExtensions.AsReadOnly(conditions.ToArray());
            this.Interval = interval;
            this.Level = level;
            this.OperationName = operationName;
            this.Pattern = pattern;
            this.ResultType = resultType;
        }

        /// <summary>
        /// Initialize new <see cref="Rule"/> instance.
        /// </summary>
        /// <param name="template">Template rule.</param>
        /// <param name="operationName">Name of operation.</param>
        public Rule(Rule template, string operationName)
        {
            this.Conditions = template.Conditions;
            this.Interval = template.Interval;
            this.Level = template.Level;
            this.OperationName = operationName;
            this.Pattern = template.Pattern;
            this.ResultType = template.ResultType;
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


    // Constructor.
    OperationCountingAnalysisRuleSet(IULogViewerApplication app, string id) : base(app, id)
    { }


    /// <summary>
    /// Initialize new <see cref="OperationCountingAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public OperationCountingAnalysisRuleSet(IULogViewerApplication app) : base(app, OperationCountingAnalysisRuleSetManager.Default.GenerateProfileId())
    { }


    /// <summary>
    /// Initialize new <see cref="OperationCountingAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="template">Template rule set.</param>
    /// <param name="name">Name.</param>
    public OperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet template, string name) : base(template, name, OperationCountingAnalysisRuleSetManager.Default.GenerateProfileId())
    { }


    /// <inheritdoc/>
    internal override void ChangeId() =>
        this.Id = OperationCountingAnalysisRuleSetManager.Default.GenerateProfileId();
    

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
            : OperationCountingAnalysisRuleSetManager.Default.GenerateProfileId();
        
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
}