using System;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Define source of series of log for chart.
/// </summary>
public class LogChartSeriesSource : IEquatable<LogChartSeriesSource>
{
    /// <summary>
    /// Initialize new <see cref="LogChartSeriesSource"/> instance.
    /// </summary>
    /// <param name="propertyName">Name of property of log.</param>
    /// <param name="displayName">Name which is suitable to display on chart.</param>
    public LogChartSeriesSource(string propertyName, string? displayName)
    {
        this.PropertyDisplayName = displayName ?? propertyName;
        this.PropertyName = propertyName;
    }


    /// <inheritdoc/>
    public bool Equals(LogChartSeriesSource? property) =>
        property is not null
        && this.PropertyName == property.PropertyName
        && this.PropertyDisplayName == property.PropertyDisplayName;


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is LogChartSeriesSource property && this.Equals(property);


    /// <inheritdoc/>
    public override int GetHashCode() => this.PropertyName.GetHashCode();


    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(LogChartSeriesSource? x, LogChartSeriesSource? y) => x?.Equals(y) ?? y is null;


    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(LogChartSeriesSource? x, LogChartSeriesSource? y) => !(x?.Equals(y) ?? y is null);
    
    
    /// <summary>
    /// Name of property which is suitable to display on chart.
    /// </summary>
    public string PropertyDisplayName { get; }
    
    
    /// <summary>
    /// Name of property of log.
    /// </summary>
    public string PropertyName { get; }


    // Get readable string.
    public override string ToString() => this.PropertyName;
}