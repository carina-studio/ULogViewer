using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
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
	readonly ComboBox nameComboBox;
	readonly TextBox quantifierTextBox;
	readonly TextBox secondaryDisplayNameTextBox;
	readonly RealNumberTextBox valueScalingTextBox;


	/// <summary>
	/// Initialize new <see cref="LogChartSeriesSourceEditorDialog"/> instance.
	/// </summary>
	public LogChartSeriesSourceEditorDialog()
	{
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
		var source = this.Source;
		if (source == null)
		{
			this.displayNameComboBox.SelectedItem = nameof(Log.Message);
			this.isDisplayNameSameAsName = true;
			this.nameComboBox.SelectedItem = nameof(Log.Message);
		}
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
}
