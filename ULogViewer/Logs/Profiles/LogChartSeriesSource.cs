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
    /// <param name="secondaryDisplayName">Secondary name to display on chart.</param>
    /// <param name="quantifier">Quantifier to display on chart.</param>
    /// <param name="defaultValue">Default value if value cannot be got from log property.</param>
    /// <param name="scaling">Scaling on value got from log property.</param>
    public LogChartSeriesSource(string propertyName, string? displayName, string? secondaryDisplayName, string? quantifier, double? defaultValue, double scaling)
    {
        if (!double.IsFinite(scaling))
            throw new ArgumentOutOfRangeException(nameof(scaling));
        this.DefaultValue = defaultValue?.Let(it => double.IsFinite(it) ? (double?)it : null);
        this.HasValueScaling = Math.Abs(1 - scaling) >= (double.Epsilon * 2);
        this.PropertyDisplayName = displayName ?? propertyName;
        this.PropertyName = propertyName;
        this.Quantifier = quantifier;
        this.SecondaryPropertyDisplayName = secondaryDisplayName;
        this.ValueScaling = scaling;
    }
    
    
    /// <summary>
    /// Default value if value cannot be got from log property.
    /// </summary>
    public double? DefaultValue { get; }


    /// <inheritdoc/>
    public bool Equals(LogChartSeriesSource? source) =>
        source is not null
        && this.DefaultValue.Equals(source.DefaultValue)
        && this.PropertyName == source.PropertyName
        && this.PropertyDisplayName == source.PropertyDisplayName
        && this.Quantifier == source.Quantifier
        && this.SecondaryPropertyDisplayName == source.SecondaryPropertyDisplayName
        && Math.Abs(this.ValueScaling - source.ValueScaling) <= Double.Epsilon * 2;


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is LogChartSeriesSource source && this.Equals(source);


    /// <inheritdoc/>
    public override int GetHashCode() => this.PropertyName.GetHashCode();
    
    
    /// <summary>
    /// Check whether <see cref="ValueScaling"/> is not 1.0 or not.
    /// </summary>
    public bool HasValueScaling { get; }


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
    
    
    /// <summary>
    /// Quantifier to display on chart.
    /// </summary>
    public string? Quantifier { get; }
    
    
    /// <summary>
    /// Secondary name of property to display on chart.
    /// </summary>
    public string? SecondaryPropertyDisplayName { get; }


    // Get readable string.
    public override string ToString() => this.PropertyName;
    
    
    /// <summary>
    /// Scaling on value got from log property.
    /// </summary>
    public double ValueScaling { get; }
}