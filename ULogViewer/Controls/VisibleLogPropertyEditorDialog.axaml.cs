using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit visible <see cref="LogProperty"/>.
/// </summary>
partial class VisibleLogPropertyEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly ToggleSwitch customDisplayNameSwitch;
	readonly TextBox customDisplayNameTextBox;
	readonly ComboBox displayNameComboBox;
	readonly ComboBox foregroundColorComboBox;
	bool isDisplayNameSameAsName;
	readonly ComboBox nameComboBox;
	readonly ToggleSwitch specifyWidthSwitch;
	readonly IntegerTextBox widthTextBox;


	/// <summary>
	/// Initialize new <see cref="VisibleLogPropertyEditorDialog"/> instance.
	/// </summary>
	public VisibleLogPropertyEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.customDisplayNameSwitch = this.Get<ToggleSwitch>(nameof(customDisplayNameSwitch));
		this.customDisplayNameTextBox = this.Get<TextBox>(nameof(customDisplayNameTextBox));
		this.displayNameComboBox = this.Get<ComboBox>(nameof(displayNameComboBox)).Also(it =>
		{
			it.GetObservable(ComboBox.SelectedItemProperty).Subscribe(item =>
			{
				this.isDisplayNameSameAsName = (item as string == this.nameComboBox?.SelectedItem as string);
			});
		});
		this.foregroundColorComboBox = this.Get<ComboBox>(nameof(foregroundColorComboBox));
		this.nameComboBox = this.Get<ComboBox>(nameof(nameComboBox)).Also(it =>
		{
			it.GetObservable(ComboBox.SelectedItemProperty).Subscribe(item =>
			{
				if (item is string name && this.isDisplayNameSameAsName)
					this.displayNameComboBox.SelectedItem = name;
			});
		});
		this.specifyWidthSwitch = this.Get<ToggleSwitch>(nameof(specifyWidthSwitch));
		this.widthTextBox = this.Get<IntegerTextBox>(nameof(widthTextBox)).Also(it =>
		{
			it.Minimum = (int)(this.FindResourceOrDefault<double>("Double/SessionView.LogHeader.MinWidth", 10) + 0.5);
		});
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var displayName = this.customDisplayNameSwitch.IsChecked.GetValueOrDefault()
				? this.customDisplayNameTextBox.Text?.Trim() ?? ""
				: (string)this.displayNameComboBox.SelectedItem.AsNonNull();
		var width = this.specifyWidthSwitch.IsChecked.GetValueOrDefault() ? (int?)this.widthTextBox.Value : null;
		return Task.FromResult((object?)new LogProperty((string)this.nameComboBox.SelectedItem.AsNonNull(), displayName, (LogPropertyForegroundColor)this.foregroundColorComboBox.SelectedItem!, width));
	}


	/// <summary>
	/// Get or set <see cref="LogProperty"/> to be edited.
	/// </summary>
	public LogProperty? LogProperty { get; set; }


	// Called when property of editor control changed.
	void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		var property = e.Property;
		if (property == ToggleSwitch.IsCheckedProperty
			|| property == TextBox.TextProperty)
		{
			this.InvalidateInput();
		}
	}


	// Called when opened.
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var property = this.LogProperty;
		if (property == null)
		{
			this.displayNameComboBox.SelectedItem = nameof(Log.Message);
			this.isDisplayNameSameAsName = true;
			this.nameComboBox.SelectedItem = nameof(Log.Message);
			this.specifyWidthSwitch.IsChecked = true;
			this.foregroundColorComboBox.SelectedItem = LogPropertyForegroundColor.Level;
			this.widthTextBox.Value = 100;
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
			this.foregroundColorComboBox.SelectedItem = property.ForegroundColor;
			property.Width.Let(it =>
			{
				this.specifyWidthSwitch.IsChecked = it.HasValue;
				this.widthTextBox.Value = it ?? 100;
			});
		}
		this.SynchronizationContext.Post(this.nameComboBox.Focus);
	}
}
