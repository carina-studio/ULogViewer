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
		this.displayNameComboBox = this.Get<ComboBox>(nameof(displayNameComboBox));
		this.foregroundColorComboBox = this.Get<ComboBox>(nameof(foregroundColorComboBox));
		this.nameComboBox = this.Get<ComboBox>(nameof(nameComboBox));
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


	// Called when selection of name combo box changed.
	void OnNameComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		this.displayNameComboBox.SelectedItem = this.nameComboBox.SelectedItem;
	}


	// Called when opened.
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var property = this.LogProperty;
		if (property == null)
		{
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
