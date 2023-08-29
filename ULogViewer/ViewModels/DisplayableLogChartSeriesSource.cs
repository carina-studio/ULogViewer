using System;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Source of log chart series for displaying.
/// </summary>
class DisplayableLogChartSeriesSource : BaseDisposable, IEquatable<DisplayableLogChartSeriesSource>, INotifyPropertyChanged
{
    // Fields.
    readonly DisplayableLogProperty logProperty;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogChartSeriesSource"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="source">Source of series.</param>
    public DisplayableLogChartSeriesSource(IULogViewerApplication app, LogChartSeriesSource source)
    {
        this.DefaultValue = source.DefaultValue;
        this.logProperty = new(app, source.PropertyName, source.PropertyDisplayName, null);
        this.logProperty.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(DisplayableLogProperty.DisplayName):
                    this.PropertyChanged?.Invoke(this, new(nameof(PropertyDisplayName)));
                    break;
            }
        };
        this.Quantifier = source.Quantifier;
        this.SecondaryPropertyDisplayName = source.SecondaryPropertyDisplayName;
        this.ValueScaling = source.ValueScaling;
    }
    
    
    /// <summary>
    /// Default value if value cannot be got from log property.
    /// </summary>
    public double? DefaultValue { get; }
    
    
    /// <inheritdoc/>
    protected override void Dispose(bool disposing) =>
        this.logProperty.Dispose();


    /// <inheritdoc/>
    public bool Equals(DisplayableLogChartSeriesSource? other) =>
        other is not null
        && other.DefaultValue.HasValue == this.DefaultValue.HasValue
        && Math.Abs(other.DefaultValue.GetValueOrDefault() - this.DefaultValue.GetValueOrDefault()) < double.Epsilon * 2
        && other.PropertyDisplayName == this.PropertyDisplayName
        && other.Quantifier == this.Quantifier
        && other.SecondaryPropertyDisplayName == this.SecondaryPropertyDisplayName
        && Math.Abs(other.ValueScaling - this.ValueScaling) < double.Epsilon * 2;


    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is DisplayableLogChartSeriesSource source
        && this.Equals(source);


    /// <inheritdoc/>
    public override int GetHashCode() =>
        this.PropertyName.GetHashCode();


    /// <summary>
    /// Raised when property changed.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    
    
    /// <summary>
    /// Get displayed name of log property for series.
    /// </summary>
    public string PropertyDisplayName => this.logProperty.DisplayName;


    /// <summary>
    /// Get name of log property for series.
    /// </summary>
    public string PropertyName => this.logProperty.Name;


    /// <summary>
    /// Quantifier to display on chart.
    /// </summary>
    public string? Quantifier { get; }


    /// <summary>
    /// Save instance in JSON format.
    /// </summary>
    /// <param name="writer">JSON writer.</param>
    public void Save(Utf8JsonWriter writer) =>
        this.ToLogChartSeriesSource().Save(writer);


    /// <summary>
    /// Secondary name of property to display on chart.
    /// </summary>
    public string? SecondaryPropertyDisplayName { get; }
    
    
    /// <summary>
    /// Convert to <see cref="LogChartSeriesSource"/> instance.
    /// </summary>
    /// <returns><see cref="LogChartSeriesSource"/>.</returns>
    public LogChartSeriesSource ToLogChartSeriesSource() => 
        new(this.PropertyName, this.PropertyDisplayName, this.SecondaryPropertyDisplayName, this.Quantifier, this.DefaultValue, this.ValueScaling);


    /// <summary>
    /// Try loading <see cref="DisplayableLogChartSeriesSource"/> instance from JSON format data.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="element">JSON element to load data from.</param>
    /// <param name="source">Loaded <see cref="DisplayableLogChartSeriesSource"/> instance.</param>
    /// <returns>True if <see cref="DisplayableLogChartSeriesSource"/> instance loaded successfully.</returns>
    public static bool TryLoad(IULogViewerApplication app, JsonElement element, [NotNullWhen(true)] out DisplayableLogChartSeriesSource? source)
    {
        if (LogChartSeriesSource.TryLoad(element, out var rawSource))
        {
            source = new(app, rawSource);
            return true;
        }
        source = default;
        return false;
    }


    /// <summary>
    /// Scaling on value got from log property.
    /// </summary>
    public double ValueScaling { get; }
}