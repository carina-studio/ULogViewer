using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="JsonLogsSavingOptions"/>.
/// </summary>
class JsonLogsSavingOptionsDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Fields.
	readonly ObservableList<KeyValuePair<string, string>> logPropertyMap = new();
	readonly ListBox logPropertyMapListBox;


	/// <summary>
	/// Initialize new <see cref="JsonLogsSavingOptionsDialog"/> instance.
	/// </summary>
	public JsonLogsSavingOptionsDialog()
	{
		this.EditLogPropertyMapEntryCommand = new Command<ListBoxItem>(this.EditLogPropertyMapEntry);
		this.RemoveLogPropertyMapEntryCommand = new Command<ListBoxItem>(this.RemoveLogPropertyMapEntry);
		AvaloniaXamlLoader.Load(this);
		this.logPropertyMap.CollectionChanged += (_, _) => this.InvalidateInput();
		this.logPropertyMapListBox = this.Get<ListBox>(nameof(logPropertyMapListBox));
		this.AddHandler(KeyUpEvent, (sender, e) =>
		{
			if (this.logPropertyMapListBox.IsSelectedItemFocused && e.Key == Key.Enter)
				_ = this.EditLogPropertyMapEntry((KeyValuePair<string, string>)this.logPropertyMapListBox.SelectedItem!);
		}, RoutingStrategies.Tunnel);
	}


	/// <summary>
	/// Add log property map entry.
	/// </summary>
	public void AddLogPropertyMapEntry() => 
		_ = this.EditLogPropertyMapEntry(new KeyValuePair<string, string>("", ""));


	// Edit log property map entry.
	void EditLogPropertyMapEntry(ListBoxItem item) => 
		_ = this.EditLogPropertyMapEntry((KeyValuePair<string, string>)item.DataContext.AsNonNull());
	async Task EditLogPropertyMapEntry(KeyValuePair<string, string> entry)
	{
		// ReSharper disable once UsageOfDefaultStructEquality
		var index = this.logPropertyMap.IndexOf(entry);
		var newEntry = (KeyValuePair<string, string>?)entry;
		while (true)
		{
			newEntry = await new StringLogPropertyMapEntryEditorDialog
			{
				Entry = newEntry.Value
			}.ShowDialog<KeyValuePair<string, string>?>(this);
			// ReSharper disable once UsageOfDefaultStructEquality
			if (newEntry is null || newEntry.Value.Equals(entry))
			{
				if (index >= 0)
					this.SelectListBoxItem(this.logPropertyMapListBox, index);
				return;
			}
			var newKey = newEntry.Value.Key;
			var newValue = newEntry.Value.Value;
			if (newKey != entry.Key && this.logPropertyMap.FirstOrDefault(it => it.Key == newKey).Key == newKey)
			{
				await new AppSuite.Controls.MessageDialog
				{
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = LogPropertyNameConverter.Default.Convert(newKey);
						it.BindToResource(FormattedString.FormatProperty, this, "String/JsonLogsSavingOptionsDialog.DuplicatedLogPropertyMapEntry.Key");
					}),
					Title = this.Title,
				}.ShowDialog(this);
				continue;
			}
			if (newValue != entry.Value && this.logPropertyMap.FirstOrDefault(it => it.Value == newValue).Value == newValue)
			{
				await new AppSuite.Controls.MessageDialog
				{
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = newValue;
						it.BindToResource(FormattedString.FormatProperty, this, "String/JsonLogsSavingOptionsDialog.DuplicatedLogPropertyMapEntry.Value");
					}),
					Title = this.Title,
				}.ShowDialog(this);
				continue;
			}
			if (index >= 0)
			{
				this.logPropertyMap.RemoveAt(index);
				this.logPropertyMap.Insert(index, newEntry.Value);
				this.SelectListBoxItem(this.logPropertyMapListBox, index);
			}
			else
			{
				this.logPropertyMap.Add(newEntry.Value);
				this.SelectListBoxItem(this.logPropertyMapListBox, this.logPropertyMap.Count - 1);
			}
			break;
		}
	}


	/// <summary>
	/// Command to edit log property map entry.
	/// </summary>
	public ICommand EditLogPropertyMapEntryCommand { get; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		// get log writer
		var options = this.LogsSavingOptions;
		if (options is null)
			return Task.FromResult((object?)null);

		// setup log writer
		options.LogPropertyMap = new Dictionary<string, string>(this.logPropertyMap);

		// complete
		return Task.FromResult((object?)options);
	}


	/// <summary>
	/// Log properties to be written.
	/// </summary>
	public IList<KeyValuePair<string, string>> LogPropertyMap => this.logPropertyMap;


	/// <summary>
	/// Get or set <see cref="JsonLogsSavingOptions"/> to be edited.
	/// </summary>
	public JsonLogsSavingOptions? LogsSavingOptions { get; init; }


	/// <inheritdoc/>
	protected override void OnEnterKeyClickedOnInputControl(Control control)
	{
		base.OnEnterKeyClickedOnInputControl(control);
		if (!this.logPropertyMapListBox.IsSelectedItemFocused)
			_ = this.EditLogPropertyMapEntry((KeyValuePair<string, string>)this.logPropertyMapListBox.SelectedItem!);
	}


	// Called when double-tapped on list box.
	void OnListBoxDoubleTapped(object? sender, TappedEventArgs e)
	{
		if (sender is not ListBox listBox)
			return;
		var selectedItem = listBox.SelectedItem;
		if (selectedItem is null 
			|| !listBox.TryFindListBoxItem(selectedItem, out var listBoxItem)
			|| listBoxItem is null
			|| !listBoxItem.IsPointerOver)
		{
			return;
		}
		if (listBox == this.logPropertyMapListBox)
			this.EditLogPropertyMapEntry(listBoxItem);
	}


	// Called when list box lost focus.
	void OnListBoxLostFocus(object? sender, RoutedEventArgs e)
	{
		if (sender is not ListBox listBox)
			return;
		Dispatcher.UIThread.Post(() =>
		{
			if (!listBox.IsSelectedItemFocused)
				listBox.SelectedItem = null;
		});
	}


	// Called when selection in list box changed.
	void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (sender is not ListBox listBox)
			return;
		if (listBox.SelectedIndex >= 0)
			listBox.ScrollIntoView(listBox.SelectedIndex);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		if (this.LogsSavingOptions is null)
			this.SynchronizationContext.Post(this.Close);
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		this.LogsSavingOptions?.Let(it =>
		{
			var propertyNameSet = new HashSet<string>();
			var displayNameSet = new HashSet<string>();
			foreach (var (propertyName, displayName) in it.LogPropertyMap)
			{
				if (!propertyNameSet.Add(propertyName))
					continue;
				if (string.IsNullOrEmpty(displayName))
				{
					if (displayNameSet.Add(propertyName))
						this.logPropertyMap.Add(new(propertyName, propertyName));
				}
				else
				{
					if (displayNameSet.Add(displayName))
						this.logPropertyMap.Add(new(propertyName, displayName));
				}
			}
		});
	}


	// Validate input.
	protected override bool OnValidateInput()
	{
		return base.OnValidateInput()
		       && this.logPropertyMap.IsNotEmpty()
		       && this.logPropertyMap.Let(it =>
		       {
			       var displayNameSet = new HashSet<string>();
			       foreach (var (propertyName, displayName) in it)
			       {
				       if (string.IsNullOrEmpty(propertyName) 
				           || string.IsNullOrEmpty(displayName) 
				           || !displayNameSet.Add(displayName))
				       {
					       return false;
				       }
			       }
			       return true;
		       });
	}


	// Remove log property map entry.
	void RemoveLogPropertyMapEntry(ListBoxItem item)
	{
		// ReSharper disable once UsageOfDefaultStructEquality
		var index = this.logPropertyMap.IndexOf((KeyValuePair<string, string>)item.DataContext.AsNonNull());
		if (index >= 0)
		{
			this.logPropertyMap.RemoveAt(index);
			this.SelectListBoxItem(this.logPropertyMapListBox, -1);
		}
	}


	/// <summary>
	/// Command to remove log property map entry.
	/// </summary>
	public ICommand RemoveLogPropertyMapEntryCommand { get; }


	// Select given item in list box.
	void SelectListBoxItem(ListBox listBox, int index)
	{
		listBox.SelectedIndex = index;
		if (index >= 0)
		{
			listBox.ScrollIntoView(index);
			listBox.FocusSelectedItem();
		}
		/*
		this.SynchronizationContext.Post(() =>
		{
			listBox.SelectedItems?.Clear();
			if (index < 0 || index >= listBox.ItemCount)
				return;
			listBox.Focus();
			listBox.SelectedIndex = index;
			listBox.ScrollIntoView(index);
		});
		*/
	}
}
