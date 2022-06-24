using CarinaStudio.ULogViewer.Scripting;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Script for log analysis.
/// </summary>
class LogAnalysisScript : Script<LogAnalysisScriptContext>
{
    /// <summary>
    /// Initialize new <see cref="LogAnalysisScript"/> instance.
    /// </summary>
    /// <param name="language">Language.</param>
    /// <param name="source">Source.</param>
    public LogAnalysisScript(ScriptLanguage language, string source) : base(language, source)
    { }


    /// <summary>
    /// Load script from JSON format data.
    /// </summary>
    /// <param name="json">JSON data.</param>
    /// <returns>Loaded script.</returns>
    public static LogAnalysisScript Load(JsonElement json) =>
        LogAnalysisScript.Load<LogAnalysisScript>(json);
}


/// <summary>
/// Context of log analysis script.
/// </summary>
class LogAnalysisScriptContext : ScriptContext
{
    /// <summary>
    /// Initialiize new <see cref="LogAnalysisScriptContext"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public LogAnalysisScriptContext(IULogViewerApplication app) : base(app)
    { }
}