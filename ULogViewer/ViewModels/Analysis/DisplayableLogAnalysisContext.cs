using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Context of log analysis.
/// </summary>
class DisplayableLogAnalysisContext
{
    // Fields.
    readonly IDictionary<string, string> variables = new Dictionary<string, string>();


    /// <summary>
    /// Clear all variables.
    /// </summary>
    public void ClearVariables()
    {
        lock (this.variables)
            this.variables.Clear();
    }


    /// <summary>
    /// Get named variable.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Value of variable.</returns>
    public string? GetVariable(string name) => this.variables.Lock(it =>
    {
        if (it.TryGetValue(name, out var value))
            return value;
        return null;
    });


    /// <summary>
    /// Set named variable.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <param name="value">New value of variable. Null to clear variable.</param>
    /// <returns>Previous value of variable.</returns>
    public string? SetVariable(string name, string? value) => this.variables.Lock(it =>
    {
        var prevValue = (string?)null;
        it.TryGetValue(name, out prevValue);
        if (value == null)
            it.Remove(name);
        else
            it[name] = value;
        return prevValue;
    });
}