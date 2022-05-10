using CarinaStudio.AppSuite.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Rule of key log analysis.
/// </summary>
class KeyLogAnalysisRule : BaseProfile<IULogViewerApplication>
{
    // Constructor.
    KeyLogAnalysisRule(IULogViewerApplication app, string id) : base(app, id, false)
    { }


    /// <summary>
    /// Initialize new <see cref="KeyLogAnalysisRule"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public KeyLogAnalysisRule(IULogViewerApplication app) : this(app, KeyLogAnalysisRuleManager.Default.GenerateProfileId())
    { }


    // Change ID.
    internal void ChangeId() =>
        this.Id = KeyLogAnalysisRuleManager.Default.GenerateProfileId();


    /// <inheritdoc/>
    public override bool Equals(IProfile<IULogViewerApplication>? profile) =>
        profile is KeyLogAnalysisRule analysisProfile
        && analysisProfile.Id == this.Id;
    

    /// <summary>
    /// Load profile from file.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">File name.</param>
    /// <returns>Task of loading profile.</returns>
    public static async Task<KeyLogAnalysisRule> LoadAsync(IULogViewerApplication app, string fileName)
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
            : KeyLogAnalysisRuleManager.Default.GenerateProfileId();
        
        // load
        var profile = new KeyLogAnalysisRule(app, id);
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
        writer.WriteEndObject();
    }
}