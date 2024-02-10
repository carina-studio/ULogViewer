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
/// Dialog to edit visible <see cref="LogProperty"/>.
/// </summary>
class VisibleLogPropertyEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly ToggleSwitch customDisplayNameSwitch;
	readonly TextBox customDisplayNameTextBox;
	readonly ComboBox displayNameComboBox;
	readonly ComboBox foregroundColorComboBox;
	bool isDisplayNameSameAsName;
	readonly SortedObservableList<string> logPropertyNames = new(string.CompareOrdinal);
	readonly ComboBox nameComboBox;
	readonly TextBox quantifierTextBox;
	readonly TextBox secondaryDisplayNameTextBox;
	readonly ToggleSwitch showDefinedLogPropertiesOnlySwitch;
	readonly ToggleSwitch specifyWidthSwitch;
	readonly IntegerTextBox widthTextBox;


	/// <summary>
	/// Initialize new <see cref="VisibleLogPropertyEditorDialog"/> instance.
	/// </summary>
	public VisibleLogPropertyEditorDialog()
	{
		this.LogPropertyNames = ListExtensions.AsReadOnly(this.logPropertyNames);
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
		this.specifyWidthSwitch = this.Get<ToggleSwitch>(nameof(specifyWidthSwitch));
		this.widthTextBox = this.Get<IntegerTextBox>(nameof(widthTextBox)).Also(it =>
		{
			it.Minimum = (int)(this.FindResourceOrDefault<double>("Double/SessionView.LogHeader.MinWidth", 10) + 0.5);
		});
	}
	
	
	/// <summary>
	/// Get or set name of log properties defined by user.
	/// </summary>
	public ISet<string>? DefinedLogPropertyNames { get; init; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var displayName = this.customDisplayNameSwitch.IsChecked.GetValueOrDefault()
				? this.customDisplayNameTextBox.Text?.Trim() ?? ""
				: (string)this.displayNameComboBox.SelectedItem.AsNonNull();
		var width = this.specifyWidthSwitch.IsChecked.GetValueOrDefault() ? (int?)this.widthTextBox.Value : null;
		return Task.FromResult((object?)new LogProperty(
			(string)this.nameComboBox.SelectedItem.AsNonNull(), 
			displayName, 
			this.secondaryDisplayNameTextBox.Text?.Trim(), 
			this.quantifierTextBox.Text?.Trim(), 
			(LogPropertyForegroundColor)this.foregroundColorComboBox.SelectedItem!, width));
	}


	/// <summary>
	/// Get or set <see cref="LogProperty"/> to be edited.
	/// </summary>
	public LogProperty? LogProperty { get; init; }
	
	
	/// <summary>
	/// Get all valid name of log properties.
	/// </summary>
	public IList<string> LogPropertyNames { get; }


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
		if (this.LogProperty is not { } property)
		{
			this.isDisplayNameSameAsName = true;
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
			this.quantifierTextBox.Text = property.Quantifier?.Trim();
			this.secondaryDisplayNameTextBox.Text = property.SecondaryDisplayName?.Trim();
			property.Width.Let(it =>
			{
				this.specifyWidthSwitch.IsChecked = it.HasValue;
				this.widthTextBox.Value = it ?? 100;
			});
		}
	}
	
	
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
			propertyNames.Add(nameof(Log.FileName));
			propertyNames.Add(nameof(Log.LineNumber));
			propertyNames.Add(nameof(Log.ReadTime));
			if (this.LogProperty is { } logProperty && Log.HasProperty(logProperty.Name))
				propertyNames.Add(logProperty.Name);
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
