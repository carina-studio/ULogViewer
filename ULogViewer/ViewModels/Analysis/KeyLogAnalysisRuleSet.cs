using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Set of rule for key log analysis.
/// </summary>
class KeyLogAnalysisRuleSet : BaseProfile<IULogViewerApplication>
{
    /// <summary>
    /// Analysis rule.
    /// </summary>
    public class Rule
    {
        /// <summary>
        /// Initialize new <see cref="Rule"/> instance.
        /// </summary>
        /// <param name="pattern">Pattern to match log.</param>
        /// <param name="resultType">Type of analysis result to be generated when pattern matched.</param>
        /// <param name="message">Formatted message to be generated when pattern matched.</param>
        public Rule(Regex pattern, DisplayableLogAnalysisResultType resultType, string message)
        {
            this.Message = message;
            this.Pattern = pattern;
            this.ResultType = resultType;
        }

        /// <summary>
        /// Get formatted message to be generated when pattern matched.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Get pattern to match log.
        /// </summary>
        public Regex Pattern { get; }

        /// <summary>
        /// Get type of analysis result to be generated when pattern matched.
        /// </summary>
        public DisplayableLogAnalysisResultType ResultType { get; }
    }


    // Fields.
    IList<Rule> rules = new Rule[0];


    // Constructor.
    KeyLogAnalysisRuleSet(IULogViewerApplication app, string id) : base(app, id, false)
    { }


    /// <summary>
    /// Initialize new <see cref="KeyLogAnalysisRuleSet"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public KeyLogAnalysisRuleSet(IULogViewerApplication app) : this(app, KeyLogAnalysisRuleSetManager.Default.GenerateProfileId())
    { }


    // Change ID.
    internal void ChangeId() =>
        this.Id = KeyLogAnalysisRuleSetManager.Default.GenerateProfileId();


    /// <inheritdoc/>
    public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
        profile is KeyLogAnalysisRuleSet analysisProfile
        && analysisProfile.Id == this.Id;
    

    /// <summary>
    /// Load profile from file.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of loading profile.</returns>
    public static async Task<KeyLogAnalysisRuleSet> LoadAsync(IULogViewerApplication app, string fileName)
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
        var profile = new KeyLogAnalysisRuleSet(app, id);
        profile.Load(element);
        return profile;
    }


    /// <inheritdoc/>
    protected override void OnLoad(JsonElement element)
    { 
        foreach (var jsonProperty in element.EnumerateObject())
        {
            switch (jsonProperty.Name)
            {
                case nameof(Id):
                    break;
                case nameof(Name):
                    this.Name = jsonProperty.Value.GetString();
                    break;
                case nameof(Rules):
                    this.rules = new List<Rule>().Also(patterns =>
                    {
                        foreach (var jsonValue in jsonProperty.Value.EnumerateArray())
                        {
                            var ignoreCase = jsonValue.TryGetProperty("IgnoreCase", out var ignoreCaseProperty) && ignoreCaseProperty.ValueKind == JsonValueKind.True;
                            var regex = new Regex(jsonValue.GetProperty(nameof(Rule.Pattern)).GetString()!, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
                            var formattedMessage = jsonValue.GetProperty(nameof(Rule.Message)).GetString().AsNonNull();
                            var resultType = jsonValue.TryGetProperty(nameof(Rule.ResultType), out var resultTypeValue) 
                                && resultTypeValue.ValueKind == JsonValueKind.String
                                && Enum.TryParse<DisplayableLogAnalysisResultType>(resultTypeValue.GetString(), out var type)
                                    ? type
                                    : DisplayableLogAnalysisResultType.Information;
                            patterns.Add(new(regex, resultType, formattedMessage));
                        }
                    }).AsReadOnly();
                    break;
            }
        }
    }


    /// <inheritdoc/>
    protected override void OnSave(Utf8JsonWriter writer, bool includeId)
    {
        writer.WriteStartObject();
        if (includeId)
            writer.WriteString(nameof(Id), this.Id);
        this.Name?.Let(it =>
            writer.WriteString(nameof(Name), it));
        if (this.rules.IsNotEmpty())
        {
            writer.WritePropertyName(nameof(Rules));
            writer.WriteStartArray();
            foreach (var pattern in this.rules)
            {
                writer.WriteStartObject();
                pattern.Pattern.Let(it =>
                {
                    writer.WriteString(nameof(Rule.Pattern), it.ToString());
                    if ((it.Options & RegexOptions.IgnoreCase) != 0)
                        writer.WriteBoolean("IgnoreCase", true);
                });
                writer.WriteString(nameof(Rule.Message), pattern.Message);
                writer.WriteString(nameof(Rule.ResultType), pattern.ResultType.ToString());
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
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
            this.rules = value.AsReadOnly();
            this.OnPropertyChanged(nameof(Rules));
        }
    }
}