using System;
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
    /// Get named variable as byte size.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <param name="unit">Unit of value of variable.</param>
    /// <returns>Value of variable or Null if value cannot be got as byte size.</returns>
    public long? GetVariableAsByteSize(string name, IO.FileSizeUnit unit)
    {
        if (double.TryParse(this.GetVariable(name), out var value))
        {
            try
            {
                return (long)(unit switch
                {
                    IO.FileSizeUnit.Bits => (value / 8),
                    IO.FileSizeUnit.Kilobytes => (value * 1024),
                    IO.FileSizeUnit.Megabytes => (value * 1048576),
                    IO.FileSizeUnit.Gigabytes => (value * 1073741824),
                    IO.FileSizeUnit.Terabytes => (value * 1099511627776),
                    IO.FileSizeUnit.Petabytes => (value * 1125899906842624),
                    _ => value,
                } + 0.5);
            }
            catch (OverflowException)
            { }
        }
        return null;
    }


    /// <summary>
    /// Get named variable as <see cref="double"/>.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Value of variable or <see cref="double.NaN"/> if value cannot be got as <see cref="double"/>.</returns>
    public double GetVariableAsDouble(string name)
    {
        if (double.TryParse(this.GetVariable(name), out var value))
            return value;
        return double.NaN;
    }


    /// <summary>
    /// Get named variable as <see cref="int"/>.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Value of variable or Null if value cannot be got as <see cref="int"/>.</returns>
    public int? GetVariableAsInt32(string name)
    {
        if (double.TryParse(this.GetVariable(name), out var value) && value <= int.MaxValue && value >= int.MinValue)
            return (int)(value + 0.5);
        return null;
    }


    /// <summary>
    /// Get named variable as <see cref="long"/>.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <returns>Value of variable or Null if value cannot be got as <see cref="long"/>.</returns>
    public long? GetVariableAsInt64(string name)
    {
        if (double.TryParse(this.GetVariable(name), out var value) && value <= long.MaxValue && value >= long.MinValue)
            return (long)(value + 0.5);
        return null;
    }


    /// <summary>
    /// Get named variable as <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="name">Name.</param>
    /// <param name="unit">Unit to parse value of variable.</param>
    /// <returns>Value of variable or Null if value cannot be got as <see cref="TimeSpan"/>.</returns>
    public TimeSpan? GetVariableAsTimeSpan(string name, TimeSpanUnit unit)
    {
        if (double.TryParse(this.GetVariable(name), out var value))
        {
            return unit switch
            {
                TimeSpanUnit.Milliseconds => TimeSpan.FromMilliseconds(value),
                TimeSpanUnit.Microseconds => TimeSpan.FromMicroseconds(value),
                TimeSpanUnit.Nanoseconds => TimeSpan.FromTicks((long)(value / 100 + 0.5)),
                TimeSpanUnit.Minutes => TimeSpan.FromMinutes(value),
                TimeSpanUnit.Hours => TimeSpan.FromHours(value),
                TimeSpanUnit.Days => TimeSpan.FromDays(value),
                _ => TimeSpan.FromSeconds(value),
            };
        }
        return null;
    }


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