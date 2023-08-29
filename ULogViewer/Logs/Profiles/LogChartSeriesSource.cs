using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

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
    /// Save instance in JSON format.
    /// </summary>
    /// <param name="writer">JSON writer.</param>
    public void Save(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        this.DefaultValue?.Let(it => writer.WriteNumber(nameof(DefaultValue), it));
        if (this.PropertyDisplayName != this.PropertyName)
            writer.WriteString(nameof(PropertyDisplayName), this.PropertyDisplayName);
        writer.WriteString(nameof(PropertyName), this.PropertyName);
        this.Quantifier?.Let(it => writer.WriteString(nameof(Quantifier), it));
        this.SecondaryPropertyDisplayName?.Let(it => writer.WriteString(nameof(SecondaryPropertyDisplayName), it));
        if (Math.Abs(this.ValueScaling - 1) > double.Epsilon * 2)
            writer.WriteNumber(nameof(ValueScaling), this.ValueScaling);
        writer.WriteEndObject();
    }
    
    
    /// <summary>
    /// Secondary name of property to display on chart.
    /// </summary>
    public string? SecondaryPropertyDisplayName { get; }


    // Get readable string.
    public override string ToString() => this.PropertyName;
    
    
    /// <summary>
    /// Try loading <see cref="LogChartSeriesSource"/> instance from JSON format data.
    /// </summary>
    /// <param name="element">JSON element to load data from.</param>
    /// <param name="source">Loaded <see cref="LogChartSeriesSource"/> instance.</param>
    /// <returns>True if <see cref="LogChartSeriesSource"/> instance loaded successfully.</returns>
    public static bool TryLoad(JsonElement element, [NotNullWhen(true)] out LogChartSeriesSource? source)
    {
        source = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        string propertyName;
        if (element.TryGetProperty(nameof(PropertyName), out var jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.String)
        {
            propertyName = jsonProperty.GetString() ?? "";
        }
        else
            return false;
        var propertyDisplayName = default(string);
        if (element.TryGetProperty(nameof(PropertyDisplayName), out jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.String)
        {
            propertyDisplayName = jsonProperty.GetString();
        }
        var defaultValue = default(double?);
        if (element.TryGetProperty(nameof(DefaultValue), out jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.Number
            && jsonProperty.TryGetDouble(out var doubleValue))
        {
            defaultValue = doubleValue;
        }
        var quantifier = default(string);
        if (element.TryGetProperty(nameof(Quantifier), out jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.String)
        {
            quantifier = jsonProperty.GetString();
        }
        var secondaryPropertyDisplayName = default(string);
        if (element.TryGetProperty(nameof(SecondaryPropertyDisplayName), out jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.String)
        {
            secondaryPropertyDisplayName = jsonProperty.GetString();
        }
        var valueScaling = 1.0;
        if (element.TryGetProperty(nameof(ValueScaling), out jsonProperty)
            && jsonProperty.ValueKind == JsonValueKind.Number
            && jsonProperty.TryGetDouble(out doubleValue))
        {
            valueScaling = doubleValue;
        }
        source = new(propertyName, propertyDisplayName, secondaryPropertyDisplayName, quantifier, defaultValue, valueScaling);
        return true;
    }
    
    
    /// <summary>
    /// Scaling on value got from log property.
    /// </summary>
    public double ValueScaling { get; }
}