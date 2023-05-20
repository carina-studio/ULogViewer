using CarinaStudio.ULogViewer.Logs.Profiles;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Source of log chart series for displaying.
/// </summary>
class DisplayableLogChartSeriesSource : BaseDisposable, INotifyPropertyChanged
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
    /// Secondary name of property to display on chart.
    /// </summary>
    public string? SecondaryPropertyDisplayName { get; }


    /// <summary>
    /// Scaling on value got from log property.
    /// </summary>
    public double ValueScaling { get; }
}