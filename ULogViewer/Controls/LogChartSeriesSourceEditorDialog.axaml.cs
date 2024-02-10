using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit visible <see cref="LogChartSeriesSource"/>.
/// </summary>
class LogChartSeriesSourceEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	/// <summary>
	/// List of valid names of log properties for chart.
	/// </summary>
	public static readonly IList<string> LogChartPropertyNames = new List<string>(Log.PropertyNames).Also(it =>
	{
		for (var i = it.Count - 1; i >= 0; --i)
		{
			var name = it[i];
			if (!Log.HasStringProperty(name) && !Log.HasInt32Property(name) && !Log.HasInt64Property(name))
				it.RemoveAt(i);
		}
	}).AsReadOnly();
	
	
	// Static fields.
	static readonly StyledProperty<bool> IsDirectNumberValueSeriesProperty = AvaloniaProperty.Register<LogChartSeriesSourceEditorDialog, bool>("IsDirectNumberValueSeries", false);


	// Fields.
	LogChartType chartType = LogChartType.None;
	readonly ToggleSwitch customDisplayNameSwitch;
	readonly TextBox customDisplayNameTextBox;
	readonly RealNumberTextBox defaultValueTextBox;
	readonly ComboBox displayNameComboBox;
	bool isDisplayNameSameAsName;
	readonly SortedObservableList<string> logPropertyNames = new(string.CompareOrdinal);
	readonly ComboBox nameComboBox;
	readonly TextBox quantifierTextBox;
	readonly TextBox secondaryDisplayNameTextBox;
	readonly ToggleSwitch showDefinedLogPropertiesOnlySwitch;
	readonly RealNumberTextBox valueScalingTextBox;


	/// <summary>
	/// Initialize new <see cref="LogChartSeriesSourceEditorDialog"/> instance.
	/// </summary>
	public LogChartSeriesSourceEditorDialog()
	{
		this.LogPropertyNames = ListExtensions.AsReadOnly(this.logPropertyNames);
		AvaloniaXamlLoader.Load(this);
		this.customDisplayNameSwitch = this.Get<ToggleSwitch>(nameof(customDisplayNameSwitch)).Also(it =>
		{
			it.GetObservable(ToggleSwitch.IsCheckedProperty).Subscribe(this.InvalidateInput);
		});
		this.customDisplayNameTextBox = this.Get<TextBox>(nameof(customDisplayNameTextBox)).Also(it =>
		{
			it.LostFocus += (_, _) =>
			{
				var text = it.Text;
				if (string.IsNullOrWhiteSpace(text))
					it.Text = null;
			};
			it.GetObservable(TextBox.TextProperty).Subscribe(this.InvalidateInput);
		});
		this.defaultValueTextBox = this.Get<RealNumberTextBox>(nameof(defaultValueTextBox)).Also(it =>
		{
			it.GetObservable(RealNumberTextBox.IsTextValidProperty).Subscribe(this.InvalidateInput);
		});
		this.displayNameComboBox = this.Get<ComboBox>(nameof(displayNameComboBox)).Also(it =>
		{
			it.GetObservable(ComboBox.SelectedItemProperty).Subscribe(item =>
			{
				this.isDisplayNameSameAsName = (item as string == this.nameComboBox?.SelectedItem as string);
			});
		});
		this.nameComboBox = this.Get<ComboBox>(nameof(nameComboBox)).Also(it =>
		{
			it.GetObservable(ComboBox.SelectedItemProperty).Subscribe(item =>
			{
				if (item is string name && this.isDisplayNameSameAsName)
					this.displayNameComboBox.SelectedItem = name;
			});
		});
		this.quantifierTextBox = this.Get<TextBox>(nameof(quantifierTextBox));
		this.secondaryDisplayNameTextBox = this.Get<TextBox>(nameof(secondaryDisplayNameTextBox));
		this.showDefinedLogPropertiesOnlySwitch = this.Get<ToggleSwitch>(nameof(showDefinedLogPropertiesOnlySwitch)).Also(it =>
		{
			it.IsCheckedChanged += (_, _) =>
			{
				if (this.IsOpened)
					this.UpdateLogPropertyNames();
			};
		});
		this.valueScalingTextBox = this.Get<RealNumberTextBox>(nameof(valueScalingTextBox)).Also(it =>
		{
			it.GetObservable(RealNumberTextBox.IsTextValidProperty).Subscribe(this.InvalidateInput);
		});
	}


	/// <summary>
	/// Get or set type of log chart.
	/// </summary>
	public LogChartType ChartType
	{
		get => this.chartType;
		set
		{
			this.chartType = value;
			this.SetValue(IsDirectNumberValueSeriesProperty, value.IsDirectNumberValueSeriesType());
		}
	}
	
	
	/// <summary>
	/// Get or set name of log properties defined by user.
	/// </summary>
	public ISet<string>? DefinedLogPropertyNames { get; init; }


	/// <inheritdoc/>
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var displayName = this.customDisplayNameSwitch.IsChecked.GetValueOrDefault()
				? this.customDisplayNameTextBox.Text?.Trim() ?? ""
				: (string)this.displayNameComboBox.SelectedItem.AsNonNull();
		return Task.FromResult((object?)new LogChartSeriesSource((string)this.nameComboBox.SelectedItem.AsNonNull(), 
			displayName, 
			this.secondaryDisplayNameTextBox.Text?.Trim(),
			this.quantifierTextBox.Text?.Trim(),
			this.defaultValueTextBox.Value,
			this.valueScalingTextBox.Value.GetValueOrDefault()));
	}
	
	
	/// <summary>
	/// Get all valid name of log properties.
	/// </summary>
	public IList<string> LogPropertyNames { get; }


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() => this.nameComboBox.Focus());
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		this.UpdateLogPropertyNames();
		if (this.Source is not { } source)
			this.isDisplayNameSameAsName = true;
		else
		{
			this.defaultValueTextBox.Value = source.DefaultValue;
			this.nameComboBox.SelectedItem = source.PropertyName;
			if (DisplayableLogProperty.DisplayNames.Contains(source.PropertyDisplayName))
				this.displayNameComboBox.SelectedItem = source.PropertyDisplayName;
			else
			{
				this.isDisplayNameSameAsName = true;
				this.customDisplayNameSwitch.IsChecked = true;
				this.customDisplayNameTextBox.Text = source.PropertyDisplayName.Trim();
			}
			this.quantifierTextBox.Text = source.Quantifier?.Trim();
			this.secondaryDisplayNameTextBox.Text = source.SecondaryPropertyDisplayName?.Trim();
			this.valueScalingTextBox.Value = source.ValueScaling;
		}
	}


	/// <inheritdoc/>
	protected override bool OnValidateInput() =>
		base.OnValidateInput()
		&& (this.customDisplayNameSwitch.IsChecked == false || !string.IsNullOrWhiteSpace(this.customDisplayNameTextBox.Text))
		&& this.defaultValueTextBox.IsTextValid
		&& this.valueScalingTextBox.IsTextValid;


	/// <summary>
	/// Get or set <see cref="Source"/> to be edited.
	/// </summary>
	public LogChartSeriesSource? Source { get; set; }
	
	
	// Update valid name of log properties.
	void UpdateLogPropertyNames()
	{
		var prevSelectedPropertyName = this.nameComboBox.SelectedItem as string;
		string? newSelectedPropertyName;
		var propertyNames = new HashSet<string>();
		if (this.showDefinedLogPropertiesOnlySwitch.IsChecked != true)
		{
			this.logPropertyNames.Clear();
			this.logPropertyNames.AddAll(Log.PropertyNames);
			propertyNames.AddAll(Log.PropertyNames);
		}
		else
		{
			propertyNames.Add(nameof(Log.ReadTime));
			if (this.Source is { } source && Log.HasProperty(source.PropertyName))
				propertyNames.Add(source.PropertyName);
			if (this.DefinedLogPropertyNames is not null)
				propertyNames.AddAll(this.DefinedLogPropertyNames.Where(Log.HasProperty));
			this.logPropertyNames.Clear();
			this.logPropertyNames.AddAll(propertyNames);
		}
		if (string.IsNullOrEmpty(prevSelectedPropertyName) || !propertyNames.Contains(prevSelectedPropertyName))
		{
			newSelectedPropertyName = propertyNames.Contains(nameof(Log.Message)) 
				? nameof(Log.Message) 
				: this.logPropertyNames[0];
		}
		else
			newSelectedPropertyName = prevSelectedPropertyName;
		this.nameComboBox.SelectedItem = newSelectedPropertyName;
	}
}
