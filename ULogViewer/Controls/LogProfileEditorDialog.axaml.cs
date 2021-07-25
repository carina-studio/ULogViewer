using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileEditorDialog : BaseDialog
	{
		/// <summary>
		/// <see cref="IValueConverter"/> to convert <see cref="LogLevel"/> to readable name.
		/// </summary>
		public static readonly IValueConverter LogLevelNameConverter = new EnumConverter<LogLevel>(App.Current);
		/// <summary>
		/// <see cref="IValueConverter"/> to convert <see cref="LogProfileIcon"/> to display name.
		/// </summary>
		public static readonly IValueConverter LogProfileIconNameConverter = new EnumConverter<LogProfileIcon>(App.Current);


		// Static fields.
		static readonly AvaloniaProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
		static readonly AvaloniaProperty<UnderlyingLogDataSource> UnderlyingDataSourceProperty = AvaloniaProperty.Register<LogProfileEditorDialog, UnderlyingLogDataSource>(nameof(UnderlyingDataSource), UnderlyingLogDataSource.Undefined);


		// Fields.
		readonly ComboBox colorIndicatorComboBox;
		readonly ToggleSwitch continuousReadingSwitch;
		LogDataSourceOptions dataSourceOptions;
		readonly ComboBox dataSourceProviderComboBox;
		readonly ComboBox iconComboBox;
		readonly SortedObservableList<KeyValuePair<string, LogLevel>> logLevelMapEntriesForReading = new SortedObservableList<KeyValuePair<string, LogLevel>>((x, y) => x.Key.CompareTo(y.Key));
		readonly ObservableList<LogPattern> logPatterns = new ObservableList<LogPattern>();
		readonly TextBox nameTextBox;
		readonly ComboBox sortDirectionComboBox;
		readonly ComboBox sortKeyComboBox;
		readonly TextBox timestampFormatForReadingTextBox;
		readonly TextBox timestampFormatForDisplayingTextBox;
		readonly ObservableList<LogProperty> visibleLogProperties = new ObservableList<LogProperty>();
		readonly ToggleSwitch workingDirNeededSwitch;


		/// <summary>
		/// Initialize new <see cref="LogProfileEditorDialog"/> instance.
		/// </summary>
		public LogProfileEditorDialog()
		{
			this.LogLevelMapEntriesForReading = this.logLevelMapEntriesForReading.AsReadOnly();
			this.LogPatterns = this.logPatterns.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}).AsReadOnly();
			this.VisibleLogProperties = this.visibleLogProperties.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}).AsReadOnly();
			InitializeComponent();
			this.colorIndicatorComboBox = this.FindControl<ComboBox>("colorIndicatorComboBox").AsNonNull();
			this.continuousReadingSwitch = this.FindControl<ToggleSwitch>("continuousReadingSwitch").AsNonNull();
			this.dataSourceProviderComboBox = this.FindControl<ComboBox>("dataSourceProviderComboBox").AsNonNull();
			this.iconComboBox = this.FindControl<ComboBox>("iconComboBox").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.sortDirectionComboBox = this.FindControl<ComboBox>("sortDirectionComboBox").AsNonNull();
			this.sortKeyComboBox = this.FindControl<ComboBox>("sortKeyComboBox").AsNonNull();
			this.timestampFormatForReadingTextBox = this.FindControl<TextBox>("timestampFormatForReadingTextBox").AsNonNull();
			this.timestampFormatForDisplayingTextBox = this.FindControl<TextBox>("timestampFormatForDisplayingTextBox").AsNonNull();
			this.workingDirNeededSwitch = this.FindControl<ToggleSwitch>("workingDirNeededSwitch").AsNonNull();
		}


		// Add log level map entry.
		async void AddLogLevelMapEntry()
		{
			var entry = (KeyValuePair<string, LogLevel>?)null;
			while (true)
			{
				entry = await new LogLevelMapEntryForReadingEditorDialog()
				{
					Entry = entry
				}.ShowDialog<KeyValuePair<string, LogLevel>?>(this);
				if (entry == null)
					return;
				if (this.logLevelMapEntriesForReading.Contains(entry.Value))
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", entry.Value.Key),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMap"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForReading.Add(entry.Value);
				break;
			}
		}


		// Add log pattern.
		async void AddLogPattern()
		{
			var logPattern = await new LogPatternEditorDialog().ShowDialog<LogPattern>(this);
			if (logPattern != null)
				this.logPatterns.Add(logPattern);
		}


		// Add visible log property.
		async void AddVisibleLogProperty()
		{
			var logProperty = await new VisibleLogPropertyEditorDialog().ShowDialog<LogProperty>(this);
			if (logProperty != null)
				this.visibleLogProperties.Add(logProperty);
		}


		// Edit log level map entry.
		async void EditLogLevelMapEntry(KeyValuePair<string, LogLevel> entry)
		{
			var newEntry = (KeyValuePair<string, LogLevel>?)entry;
			while (true)
			{
				newEntry = await new LogLevelMapEntryForReadingEditorDialog()
				{
					Entry = newEntry
				}.ShowDialog<KeyValuePair<string, LogLevel>?>(this);
				if (newEntry == null || newEntry.Value.Equals(entry))
					return;
				if (this.logLevelMapEntriesForReading.Contains(newEntry.Value))
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", newEntry.Value.Key),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMap"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForReading.Remove(entry);
				this.logLevelMapEntriesForReading.Add(newEntry.Value);
				break;
			}
		}


		// Edit log pattern.
		async void EditLogPattern(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			if (item.DataContext is not LogPattern logPattern)
				return;
			var newLlogPattern = await new LogPatternEditorDialog()
			{
				LogPattern = logPattern
			}.ShowDialog<LogPattern>(this);
			if (newLlogPattern != null && newLlogPattern != logPattern)
				this.logPatterns[index] = newLlogPattern;
		}


		// Edit visible log property.
		async void EditVisibleLogProperty(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			if (item.DataContext is not LogProperty logProperty)
				return;
			var newLogProperty = await new VisibleLogPropertyEditorDialog()
			{
				LogProperty = logProperty
			}.ShowDialog<LogProperty>(this);
			if (newLogProperty != null && newLogProperty != logProperty)
				this.visibleLogProperties[index] = newLogProperty;
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Check whether log data source options is valid or not.
		bool IsValidDataSourceOptions { get => this.GetValue<bool>(IsValidDataSourceOptionsProperty); }


		// Entries of log level map.
		IList<KeyValuePair<string, LogLevel>> LogLevelMapEntriesForReading { get; }


		// Log patterns.
		IList<LogPattern> LogPatterns { get; }


		/// <summary>
		/// Get or set <see cref="LogProfile"/> to be edited.
		/// </summary>
		public LogProfile? LogProfile { get; set; }


		// All log profile icons.
		IList<LogProfileIcon> LogProfileIcons { get; } = (LogProfileIcon[])Enum.GetValues(typeof(LogProfileIcon));


		// Move log pattern down.
		void MoveLogPatternDown(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			if (index < this.logPatterns.Count - 1)
				this.logPatterns.Move(index, index + 1);
		}


		// Move log pattern up.
		void MoveLogPatternUp(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index <= 0)
				return;
			this.logPatterns.Move(index, index - 1);
		}


		// Move visible log property down.
		void MoveVisibleLogPropertyDown(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			if (index < this.visibleLogProperties.Count - 1)
				this.visibleLogProperties.Move(index, index + 1);
		}


		// Move visible log property up.
		void MoveVisibleLogPropertyUp(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index <= 0)
				return;
			this.visibleLogProperties.Move(index, index - 1);
		}


		// Called when selection of data source provider changed.
		void OnDataSourceProviderComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			this.SetValue<UnderlyingLogDataSource>(UnderlyingDataSourceProperty, dataSourceProvider?.UnderlyingSource ?? UnderlyingLogDataSource.Undefined);
			this.InvalidateInput();
		}


		// Called when property of editor control has been changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			var property = e.Property;
			if (property == TextBox.TextProperty)
			{
				this.InvalidateInput();
			}
		}


		// Generate result.
		protected override object? OnGenerateResult()
		{
			var logProfile = this.LogProfile ?? new LogProfile(this.Application);
			logProfile.ColorIndicator = (LogColorIndicator)this.colorIndicatorComboBox.SelectedItem.AsNonNull();
			logProfile.DataSourceOptions = this.dataSourceOptions;
			logProfile.DataSourceProvider = (ILogDataSourceProvider)this.dataSourceProviderComboBox.SelectedItem.AsNonNull();
			logProfile.Icon = (LogProfileIcon)this.iconComboBox.SelectedItem.AsNonNull();
			logProfile.IsContinuousReading = this.continuousReadingSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsWorkingDirectoryNeeded = this.workingDirNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.LogLevelMapForReading = new Dictionary<string, LogLevel>(this.logLevelMapEntriesForReading);
			logProfile.LogPatterns = this.logPatterns;
			logProfile.Name = this.nameTextBox.Text.AsNonNull();
			logProfile.SortDirection = (SortDirection)this.sortDirectionComboBox.SelectedItem.AsNonNull();
			logProfile.SortKey = (LogSortKey)this.sortKeyComboBox.SelectedItem.AsNonNull();
			logProfile.TimestampFormatForDisplaying = this.timestampFormatForDisplayingTextBox.Text;
			logProfile.TimestampFormatForReading = this.timestampFormatForReadingTextBox.Text;
			logProfile.VisibleLogProperties = this.visibleLogProperties;
			return logProfile;
		}


		// Called when pointer released on description text.
		void OnLinkDescriptionPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton != MouseButton.Left)
				return;
			if (sender is Control control && control.Tag is string uri)
				this.OpenLink(uri);
		}


		// Called when selection in list box changed.
		void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			if (e.AddedItems.Count > 0)
				this.SynchronizationContext.Post(() => listBox.SelectedItem = null);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Create");
				this.colorIndicatorComboBox.SelectedItem = LogColorIndicator.None;
				this.dataSourceProviderComboBox.SelectedItem = LogDataSourceProviders.All.FirstOrDefault(it => it is FileLogDataSourceProvider);
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
				this.sortDirectionComboBox.SelectedItem = SortDirection.Ascending;
				this.sortKeyComboBox.SelectedItem = LogSortKey.Timestamp;
			}
			else if (!profile.IsBuiltIn)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Edit");
				this.colorIndicatorComboBox.SelectedItem = profile.ColorIndicator;
				this.dataSourceOptions = profile.DataSourceOptions;
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
				this.iconComboBox.SelectedItem = profile.Icon;
				this.continuousReadingSwitch.IsChecked = profile.IsContinuousReading;
				this.logLevelMapEntriesForReading.AddAll(profile.LogLevelMapForReading);
				this.logPatterns.AddRange(profile.LogPatterns);
				this.nameTextBox.Text = profile.Name;
				this.sortDirectionComboBox.SelectedItem = profile.SortDirection;
				this.sortKeyComboBox.SelectedItem = profile.SortKey;
				this.timestampFormatForDisplayingTextBox.Text = profile.TimestampFormatForDisplaying;
				this.timestampFormatForReadingTextBox.Text = profile.TimestampFormatForReading;
				this.visibleLogProperties.AddRange(profile.VisibleLogProperties);
				this.workingDirNeededSwitch.IsChecked = profile.IsWorkingDirectoryNeeded;
			}
			else
				this.SynchronizationContext.Post(this.Close);
			this.nameTextBox.Focus();
			base.OnOpened(e);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			// call base
			if (!base.OnValidateInput())
				return false;

			// check data source and options
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			if (dataSourceProvider == null)
				return false;
			if (dataSourceProvider.UnderlyingSource == UnderlyingLogDataSource.StandardOutput && string.IsNullOrWhiteSpace(this.dataSourceOptions.Command))
			{
				this.SetValue<bool>(IsValidDataSourceOptionsProperty, false);
				return false;
			}
			this.SetValue<bool>(IsValidDataSourceOptionsProperty, true);

			// check name
			if (string.IsNullOrEmpty(this.nameTextBox.Text))
				return false;

			// check log patterns
			if (this.logPatterns.IsEmpty())
				return false;

			// check visible log properties
			if (this.visibleLogProperties.IsEmpty())
				return false;

			// ok
			return true;
		}


		// Remove log level map entry.
		void RemoveLogLevelMapEntry(KeyValuePair<string, LogLevel> entry) => this.logLevelMapEntriesForReading.Remove(entry);


		// Remove log pattern.
		void RemoveLogPattern(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			this.logPatterns.RemoveAt(index);
		}


		// Remove visible log property.
		void RemoveVisibleLogProperty(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			this.visibleLogProperties.RemoveAt(index);
		}


		// Set log data source options.
		async void SetDataSourceOptions()
		{
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			if (dataSourceProvider == null)
				return;
			var options = await new LogDataSourceOptionsDialog()
			{
				Options = this.dataSourceOptions,
				UnderlyingLogDataSource = dataSourceProvider.UnderlyingSource,
			}.ShowDialog<LogDataSourceOptions?>(this);
			if (options != null)
			{
				this.dataSourceOptions = options.Value;
				this.InvalidateInput();
			}
		}


		// Get underlying type of log data source.
		UnderlyingLogDataSource UnderlyingDataSource { get => this.GetValue<UnderlyingLogDataSource>(UnderlyingDataSourceProperty); }


		// List of visible log properties.
		IList<LogProperty> VisibleLogProperties { get; }
	}
}
