using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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
using System.Runtime.InteropServices;

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
		readonly ToggleSwitch adminNeededSwitch;
		readonly ComboBox colorIndicatorComboBox;
		readonly ToggleSwitch continuousReadingSwitch;
		LogDataSourceOptions dataSourceOptions;
		readonly ComboBox dataSourceProviderComboBox;
		readonly ComboBox iconComboBox;
		readonly ToggleButton insertLogWritingFormatSyntaxButton;
		readonly ContextMenu insertLogWritingFormatSyntaxMenu;
		readonly SortedObservableList<KeyValuePair<string, LogLevel>> logLevelMapEntriesForReading = new SortedObservableList<KeyValuePair<string, LogLevel>>((x, y) => x.Key.CompareTo(y.Key));
		readonly SortedObservableList<KeyValuePair<LogLevel, string>> logLevelMapEntriesForWriting = new SortedObservableList<KeyValuePair<LogLevel, string>>((x, y) => x.Key.CompareTo(y.Key));
		readonly ObservableList<LogPattern> logPatterns = new ObservableList<LogPattern>();
		readonly TextBox logWritingFormatTextBox;
		readonly TextBox nameTextBox;
		readonly ComboBox sortDirectionComboBox;
		readonly ComboBox sortKeyComboBox;
		readonly TextBox timestampFormatForDisplayingTextBox;
		readonly TextBox timestampFormatForReadingTextBox;
		readonly TextBox timestampFormatForWritingTextBox;
		readonly ObservableList<LogProperty> visibleLogProperties = new ObservableList<LogProperty>();
		readonly ToggleSwitch workingDirNeededSwitch;


		/// <summary>
		/// Initialize new <see cref="LogProfileEditorDialog"/> instance.
		/// </summary>
		public LogProfileEditorDialog()
		{
			// setup properties
			this.LogLevelMapEntriesForReading = this.logLevelMapEntriesForReading.AsReadOnly();
			this.LogLevelMapEntriesForWriting = this.logLevelMapEntriesForWriting.AsReadOnly();
			this.LogPatterns = this.logPatterns.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}).AsReadOnly();
			this.VisibleLogProperties = this.visibleLogProperties.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}).AsReadOnly();

			// initialize.
			InitializeComponent();

			// setup controls
			this.adminNeededSwitch = this.FindControl<ToggleSwitch>("adminNeededSwitch").AsNonNull();
			this.colorIndicatorComboBox = this.FindControl<ComboBox>("colorIndicatorComboBox").AsNonNull();
			this.continuousReadingSwitch = this.FindControl<ToggleSwitch>("continuousReadingSwitch").AsNonNull();
			this.dataSourceProviderComboBox = this.FindControl<ComboBox>("dataSourceProviderComboBox").AsNonNull();
			this.iconComboBox = this.FindControl<ComboBox>("iconComboBox").AsNonNull();
			this.insertLogWritingFormatSyntaxButton = this.FindControl<ToggleButton>("insertLogWritingFormatSyntaxButton").AsNonNull();
			this.insertLogWritingFormatSyntaxMenu = ((ContextMenu)this.Resources["insertLogWritingFormatSyntaxMenu"].AsNonNull()).Also(it =>
			{
				var itemTemplate = it.DataTemplates[0];
				var items = new List<object>();
				foreach (var propertyName in Log.PropertyNames)
				{
					items.Add(new MenuItem().Also(item =>
					{
						item.Bind(MenuItem.CommandProperty, new Binding() { Path = nameof(InsertLogWritingFormatSyntax), Source = this });
						item.CommandParameter = propertyName;
						item.DataContext = propertyName;
						item.Header = itemTemplate.Build(propertyName);
					}));
				}
				items.Add(new Separator());
				items.Add(new MenuItem().Also(item =>
				{
					item.Bind(MenuItem.CommandProperty, new Binding() { Path = nameof(InsertLogWritingFormatSyntax), Source = this });
					item.CommandParameter = "NewLine";
					item.Bind(MenuItem.HeaderProperty, item.GetResourceObservable("String.Common.NewLine"));
				}));
				it.Items = items;
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.insertLogWritingFormatSyntaxButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.insertLogWritingFormatSyntaxButton.IsChecked = true);
			});
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				this.FindControl<Control>("isAdminNeededPanel").AsNonNull().IsVisible = false;
			this.logWritingFormatTextBox = this.FindControl<TextBox>("logWritingFormatTextBox").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.sortDirectionComboBox = this.FindControl<ComboBox>("sortDirectionComboBox").AsNonNull();
			this.sortKeyComboBox = this.FindControl<ComboBox>("sortKeyComboBox").AsNonNull();
			this.timestampFormatForDisplayingTextBox = this.FindControl<TextBox>("timestampFormatForDisplayingTextBox").AsNonNull();
			this.timestampFormatForReadingTextBox = this.FindControl<TextBox>("timestampFormatForReadingTextBox").AsNonNull();
			this.timestampFormatForWritingTextBox = this.FindControl<TextBox>("timestampFormatForWritingTextBox").AsNonNull();
			this.workingDirNeededSwitch = this.FindControl<ToggleSwitch>("workingDirNeededSwitch").AsNonNull();
		}


		// Add log level map entry.
		async void AddLogLevelMapEntryForReading()
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
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForReading"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForReading.Add(entry.Value);
				break;
			}
		}


		// Add log level map entry.
		async void AddLogLevelMapEntryForWriting()
		{
			var entry = (KeyValuePair<LogLevel, string>?)null;
			while (true)
			{
				entry = await new LogLevelMapEntryForWritingEditorDialog()
				{
					Entry = entry
				}.ShowDialog<KeyValuePair<LogLevel, string>?>(this);
				if (entry == null)
					return;
				if (this.logLevelMapEntriesForWriting.Contains(entry.Value))
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", LogLevelNameConverter.Convert(entry.Value.Key, typeof(string), null, this.Application.CultureInfo)),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForReading"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForWriting.Add(entry.Value);
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
		async void EditLogLevelMapEntryForReading(KeyValuePair<string, LogLevel> entry)
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
				if (!entry.Equals(this.logLevelMapEntriesForReading.FirstOrDefault(it => it.Key == newEntry.Value.Key)))
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", newEntry.Value.Key),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForReading"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForReading.Remove(entry);
				this.logLevelMapEntriesForReading.Add(newEntry.Value);
				break;
			}
		}


		// Edit log level map entry.
		async void EditLogLevelMapEntryForWriting(KeyValuePair<LogLevel, string> entry)
		{
			var newEntry = (KeyValuePair<LogLevel, string>?)entry;
			while (true)
			{
				newEntry = await new LogLevelMapEntryForWritingEditorDialog()
				{
					Entry = newEntry
				}.ShowDialog<KeyValuePair<LogLevel, string>?>(this);
				if (newEntry == null || newEntry.Value.Equals(entry))
					return;
				if (!entry.Equals(this.logLevelMapEntriesForWriting.FirstOrDefault(it => it.Key == newEntry.Value.Key)))
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", LogLevelNameConverter.Convert(newEntry.Value.Key, typeof(string), null, this.Application.CultureInfo)),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForWriting"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForWriting.Remove(entry);
				this.logLevelMapEntriesForWriting.Add(newEntry.Value);
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


		// Insert log writing syntax for given log property.
		void InsertLogWritingFormatSyntax(string propertyName)
		{
			this.logWritingFormatTextBox.SelectedText = $"{{{propertyName}}}";
			this.logWritingFormatTextBox.Focus();
		}


		// Check whether log data source options is valid or not.
		bool IsValidDataSourceOptions { get => this.GetValue<bool>(IsValidDataSourceOptionsProperty); }


		// Entries of log level map.
		IList<KeyValuePair<string, LogLevel>> LogLevelMapEntriesForReading { get; }


		// Entries of log level map.
		IList<KeyValuePair<LogLevel, string>> LogLevelMapEntriesForWriting { get; }


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
			logProfile.IsAdministratorNeeded = this.adminNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsContinuousReading = this.continuousReadingSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsWorkingDirectoryNeeded = this.workingDirNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.LogLevelMapForReading = new Dictionary<string, LogLevel>(this.logLevelMapEntriesForReading);
			logProfile.LogLevelMapForWriting = new Dictionary<LogLevel, string>(this.logLevelMapEntriesForWriting);
			logProfile.LogPatterns = this.logPatterns;
			logProfile.LogWritingFormat = this.logWritingFormatTextBox.Text?.Let(it =>
			{
				if (string.IsNullOrWhiteSpace(it))
					return null;
				return it;
			});
			logProfile.Name = this.nameTextBox.Text.AsNonNull();
			logProfile.SortDirection = (SortDirection)this.sortDirectionComboBox.SelectedItem.AsNonNull();
			logProfile.SortKey = (LogSortKey)this.sortKeyComboBox.SelectedItem.AsNonNull();
			logProfile.TimestampFormatForDisplaying = this.timestampFormatForDisplayingTextBox.Text;
			logProfile.TimestampFormatForReading = this.timestampFormatForReadingTextBox.Text;
			logProfile.TimestampFormatForWriting = this.timestampFormatForWritingTextBox.Text;
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
				this.adminNeededSwitch.IsChecked = profile.IsAdministratorNeeded;
				this.colorIndicatorComboBox.SelectedItem = profile.ColorIndicator;
				this.dataSourceOptions = profile.DataSourceOptions;
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
				this.iconComboBox.SelectedItem = profile.Icon;
				this.continuousReadingSwitch.IsChecked = profile.IsContinuousReading;
				this.logLevelMapEntriesForReading.AddAll(profile.LogLevelMapForReading);
				this.logLevelMapEntriesForWriting.AddAll(profile.LogLevelMapForWriting);
				this.logPatterns.AddRange(profile.LogPatterns);
				this.logWritingFormatTextBox.Text = profile.LogWritingFormat;
				this.nameTextBox.Text = profile.Name;
				this.sortDirectionComboBox.SelectedItem = profile.SortDirection;
				this.sortKeyComboBox.SelectedItem = profile.SortKey;
				this.timestampFormatForDisplayingTextBox.Text = profile.TimestampFormatForDisplaying;
				this.timestampFormatForReadingTextBox.Text = profile.TimestampFormatForReading;
				this.timestampFormatForWritingTextBox.Text = profile.TimestampFormatForWriting;
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
			switch(dataSourceProvider.UnderlyingSource)
			{
				case UnderlyingLogDataSource.StandardOutput:
					if (string.IsNullOrWhiteSpace(this.dataSourceOptions.Command))
					{
						this.SetValue<bool>(IsValidDataSourceOptionsProperty, false);
						return false;
					}
					break;
				case UnderlyingLogDataSource.WebRequest:
					if (this.dataSourceOptions.Uri == null)
					{
						this.SetValue<bool>(IsValidDataSourceOptionsProperty, false);
						return false;
					}
					break;
				case UnderlyingLogDataSource.WindowsEventLogs:
					if (string.IsNullOrWhiteSpace(this.dataSourceOptions.Category))
					{
						this.SetValue<bool>(IsValidDataSourceOptionsProperty, false);
						return false;
					}
					break;
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
		void RemoveLogLevelMapEntry(object entry)
		{
			if (entry is KeyValuePair<string, LogLevel> readingEntry)
				this.logLevelMapEntriesForReading.Remove(readingEntry);
			else if (entry is KeyValuePair<LogLevel, string> writingEntry)
				this.logLevelMapEntriesForWriting.Remove(writingEntry);
		}


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


		// Show menu to insert log writing format syntax.
		void ShowInsertLogWritingFormatSyntaxMenu()
		{
			if (this.insertLogWritingFormatSyntaxMenu.PlacementTarget == null)
				this.insertLogWritingFormatSyntaxMenu.PlacementTarget = this.insertLogWritingFormatSyntaxButton;
			this.insertLogWritingFormatSyntaxMenu.Open(this);
		}


		// Get underlying type of log data source.
		UnderlyingLogDataSource UnderlyingDataSource { get => this.GetValue<UnderlyingLogDataSource>(UnderlyingDataSourceProperty); }


		// List of visible log properties.
		IList<LogProperty> VisibleLogProperties { get; }
	}
}
