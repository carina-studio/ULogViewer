using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="JsonLogsSavingOptions"/>.
	/// </summary>
	partial class JsonLogsSavingOptionsDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
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
			this.logPropertyMap.CollectionChanged += (_, e) => this.InvalidateInput();
			this.logPropertyMapListBox = this.FindControl<ListBox>(nameof(logPropertyMapListBox)).AsNonNull();
		}


		/// <summary>
		/// Add log property map entry.
		/// </summary>
		public void AddLogPropertyMapEntry() => 
			this.EditLogPropertyMapEntry(new KeyValuePair<string, string>("", ""));


		// Edit log property map entry.
		void EditLogPropertyMapEntry(ListBoxItem item) => 
			this.EditLogPropertyMapEntry((KeyValuePair<string, string>)item.DataContext.AsNonNull());
		async void EditLogPropertyMapEntry(KeyValuePair<string, string> entry)
		{
			var index = this.logPropertyMap.IndexOf(entry);
			var newEntry = (KeyValuePair<string, string>?)entry;
			while (true)
			{
				newEntry = await new StringLogPropertyMapEntryEditorDialog()
				{
					Entry = newEntry.Value
				}.ShowDialog<KeyValuePair<string, string>?>(this);
				if (newEntry == null || newEntry.Value.Equals(entry))
				{
					if (index >= 0)
						this.SelectListBoxItem(this.logPropertyMapListBox, index);
					return;
				}
				var newKey = newEntry.Value.Key;
				if (newKey != entry.Key && this.logPropertyMap.FirstOrDefault(it => it.Key == newKey).Key == newKey)
				{
					await new AppSuite.Controls.MessageDialog()
					{
						Icon = AppSuite.Controls.MessageDialogIcon.Warning,
						Message = new FormattedString().Also(it =>
						{
							it.Arg1 = LogPropertyNameConverter.Default.Convert(newKey);
							it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileEditorDialog.DuplicateLogLevelMapEntry"));
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
			if (options == null)
				return Task.FromResult((object?)null);

			// setup log writer
			options.LogPropertyMap = new Dictionary<string, string>(this.logPropertyMap);

			// complete
			return Task.FromResult((object?)options);
		}


		/// <summary>
		/// Log properties to be written.
		/// </summary>
		public IList<KeyValuePair<string, string>> LogPropertyMap { get => this.logPropertyMap; }


		/// <summary>
		/// Get or set <see cref="JsonLogsSavingOptions"/> to be edited.
		/// </summary>
		public JsonLogsSavingOptions? LogsSavingOptions { get; set; }


		// Called when double-tapped on list box.
		void OnListBoxDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			var selectedItem = listBox.SelectedItem;
			if (selectedItem == null 
				|| !listBox.TryFindListBoxItem(selectedItem, out var listBoxItem)
				|| listBoxItem == null
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
			listBox.SelectedItems?.Clear();
		}


		// Called when selection in list box changed.
		void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			if (listBox.SelectedIndex >= 0)
				listBox.ScrollIntoView(listBox.SelectedIndex);
		}


		// Dialog opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var options = this.LogsSavingOptions;
			if (options != null)
			{
				this.logPropertyMap.AddRange(options.LogPropertyMap);
			}
			else
				this.SynchronizationContext.Post(this.Close);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && this.logPropertyMap.IsNotEmpty();
		}


		// Remove log property map entry.
		void RemoveLogPropertyMapEntry(ListBoxItem item)
		{
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
			this.SynchronizationContext.Post(() =>
			{
				listBox.SelectedItems?.Clear();
				if (index < 0 || index >= listBox.GetItemCount())
					return;
				listBox.Focus();
				listBox.SelectedIndex = index;
				listBox.ScrollIntoView(index);
			});
		}
	}
}
