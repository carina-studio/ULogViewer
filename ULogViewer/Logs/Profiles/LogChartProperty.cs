using System;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Property of log for chart.
/// </summary>
public class LogChartProperty : IEquatable<LogChartProperty>
{
    /// <summary>
    /// Initialize new <see cref="LogProperty"/> instance.
    /// </summary>
    /// <param name="name">Name of property of log.</param>
    /// <param name="displayName">Name which is suitable to display on chart.</param>
    public LogChartProperty(string name, string? displayName)
    {
        this.DisplayName = displayName ?? name;
        this.Name = name;
    }
    
    
    /// <summary>
    /// Name which is suitable to display on chart.
    /// </summary>
    public string DisplayName { get; }


    /// <inheritdoc/>
    public bool Equals(LogChartProperty? property) =>
        property is not null
        && this.Name == property.Name
        && this.DisplayName == property.DisplayName;


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is LogChartProperty property && this.Equals(property);


    /// <inheritdoc/>
    public override int GetHashCode() => this.Name.GetHashCode();


    /// <summary>
    /// Name of property of log.
    /// </summary>
    public string Name { get; }


    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(LogChartProperty? x, LogChartProperty? y) => x?.Equals(y) ?? y is null;


    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(LogChartProperty? x, LogChartProperty? y) => !(x?.Equals(y) ?? y is null);


    // Get readable string.
    public override string ToString() => this.Name;
}