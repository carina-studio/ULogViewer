using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
/// Dialog to edit visible <see cref="LogChartProperty"/>.
/// </summary>
class LogChartPropertyEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
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


	// Fields.
	readonly ToggleSwitch customDisplayNameSwitch;
	readonly TextBox customDisplayNameTextBox;
	readonly ComboBox displayNameComboBox;
	bool isDisplayNameSameAsName;
	readonly ComboBox nameComboBox;


	/// <summary>
	/// Initialize new <see cref="LogChartPropertyEditorDialog"/> instance.
	/// </summary>
	public LogChartPropertyEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.customDisplayNameSwitch = this.Get<ToggleSwitch>(nameof(customDisplayNameSwitch)).Also(it =>
		{
			it.GetObservable(ToggleSwitch.IsCheckedProperty).Subscribe(this.InvalidateInput);
		});
		this.customDisplayNameTextBox = this.Get<TextBox>(nameof(customDisplayNameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(this.InvalidateInput);
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
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var displayName = this.customDisplayNameSwitch.IsChecked.GetValueOrDefault()
				? this.customDisplayNameTextBox.Text?.Trim() ?? ""
				: (string)this.displayNameComboBox.SelectedItem.AsNonNull();
		return Task.FromResult((object?)new LogChartProperty((string)this.nameComboBox.SelectedItem.AsNonNull(), displayName));
	}


	/// <summary>
	/// Get or set <see cref="LogChartProperty"/> to be edited.
	/// </summary>
	public LogChartProperty? LogChartProperty { get; set; }


	// Called when opened.
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var property = this.LogChartProperty;
		if (property == null)
		{
			this.displayNameComboBox.SelectedItem = nameof(Log.Message);
			this.isDisplayNameSameAsName = true;
			this.nameComboBox.SelectedItem = nameof(Log.Message);
		}
		else
		{
			this.nameComboBox.SelectedItem = property.Name;
			if (DisplayableLogProperty.DisplayNames.Contains(property.DisplayName))
				this.displayNameComboBox.SelectedItem = property.DisplayName;
			else
			{
				this.isDisplayNameSameAsName = true;
				this.customDisplayNameSwitch.IsChecked = true;
				this.customDisplayNameTextBox.Text = property.DisplayName.Trim();
			}
		}
		this.SynchronizationContext.Post(this.nameComboBox.Focus);
	}
}
