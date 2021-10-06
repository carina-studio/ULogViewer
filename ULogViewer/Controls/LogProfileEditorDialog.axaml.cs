using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		/// <summary>
		/// <see cref="IValueConverter"/> to convert <see cref="LogLevel"/> to readable name.
		/// </summary>
		public static readonly IValueConverter LogLevelNameConverter = Converters.EnumConverters.LogLevel;
		/// <summary>
		/// <see cref="IValueConverter"/> to convert <see cref="LogProfileIcon"/> to display name.
		/// </summary>
		public static readonly IValueConverter LogProfileIconNameConverter = new AppSuite.Converters.EnumConverter(App.Current, typeof(LogProfileIcon));
		/// <summary>
		/// URI of 'How ULogViewer read and parse logs' page.
		/// </summary>
		public static readonly Uri LogsReadingAndParsingPageUri = new Uri("https://carina-studio.github.io/ULogViewer/logs_reading_flow.html");


		// Static fields.
		static readonly SettingKey<bool> HasLearnAboutLogsReadingAndParsingHintShown = new SettingKey<bool>($"{nameof(LogProfileEditorDialog)}.{nameof(HasLearnAboutLogsReadingAndParsingHintShown)}");
		static readonly AvaloniaProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
		

		// Fields.
		readonly ToggleSwitch adminNeededSwitch;
		readonly ScrollViewer baseScrollViewer;
		readonly ComboBox colorIndicatorComboBox;
		readonly ToggleSwitch continuousReadingSwitch;
		LogDataSourceOptions dataSourceOptions;
		readonly ComboBox dataSourceProviderComboBox;
		readonly ComboBox iconComboBox;
		readonly ToggleButton insertLogWritingFormatSyntaxButton;
		readonly ContextMenu insertLogWritingFormatSyntaxMenu;
		readonly SortedObservableList<KeyValuePair<string, LogLevel>> logLevelMapEntriesForReading = new SortedObservableList<KeyValuePair<string, LogLevel>>((x, y) => x.Key.CompareTo(y.Key));
		readonly SortedObservableList<KeyValuePair<LogLevel, string>> logLevelMapEntriesForWriting = new SortedObservableList<KeyValuePair<LogLevel, string>>((x, y) => x.Key.CompareTo(y.Key));
		readonly ListBox logLevelMapForReadingListBox;
		readonly ListBox logLevelMapForWritingListBox;
		readonly ListBox logPatternListBox;
		readonly ObservableList<LogPattern> logPatterns = new ObservableList<LogPattern>();
		readonly ComboBox logStringEncodingForReadingComboBox;
		readonly ComboBox logStringEncodingForWritingComboBox;
		readonly TextBox logWritingFormatTextBox;
		readonly TextBox nameTextBox;
		readonly ComboBox sortDirectionComboBox;
		readonly ComboBox sortKeyComboBox;
		readonly ComboBox timestampEncodingForReadingComboBox;
		readonly TextBox timestampFormatForDisplayingTextBox;
		readonly TextBox timestampFormatForReadingTextBox;
		readonly TextBox timestampFormatForWritingTextBox;
		readonly ListBox visibleLogPropertyListBox;
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
			this.baseScrollViewer = this.FindControl<ScrollViewer>("baseScrollViewer").AsNonNull();
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
			this.logLevelMapForReadingListBox = this.FindControl<ListBox>("logLevelMapForReadingListBox").AsNonNull();
			this.logLevelMapForWritingListBox = this.FindControl<ListBox>("logLevelMapForWritingListBox").AsNonNull();
			this.logPatternListBox = this.FindControl<ListBox>("logPatternListBox").AsNonNull();
			this.logStringEncodingForReadingComboBox = this.FindControl<ComboBox>("logStringEncodingForReadingComboBox").AsNonNull();
			this.logStringEncodingForWritingComboBox = this.FindControl<ComboBox>("logStringEncodingForWritingComboBox").AsNonNull();
			this.logWritingFormatTextBox = this.FindControl<TextBox>("logWritingFormatTextBox").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.sortDirectionComboBox = this.FindControl<ComboBox>("sortDirectionComboBox").AsNonNull();
			this.sortKeyComboBox = this.FindControl<ComboBox>("sortKeyComboBox").AsNonNull();
			this.timestampEncodingForReadingComboBox = this.FindControl<ComboBox>(nameof(timestampEncodingForReadingComboBox)).AsNonNull();
			this.timestampFormatForDisplayingTextBox = this.FindControl<TextBox>("timestampFormatForDisplayingTextBox").AsNonNull();
			this.timestampFormatForReadingTextBox = this.FindControl<TextBox>("timestampFormatForReadingTextBox").AsNonNull();
			this.timestampFormatForWritingTextBox = this.FindControl<TextBox>("timestampFormatForWritingTextBox").AsNonNull();
			this.visibleLogPropertyListBox = this.FindControl<ListBox>("visibleLogPropertyListBox").AsNonNull();
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
					await new AppSuite.Controls.MessageDialog()
					{
						Icon = AppSuite.Controls.MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", entry.Value.Key),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForReading"),
					}.ShowDialog(this);
					continue;
				}
				var index = this.logLevelMapEntriesForReading.Add(entry.Value);
				this.SelectListBoxItem(this.logLevelMapForReadingListBox, index);
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
					await new AppSuite.Controls.MessageDialog()
					{
						Icon = AppSuite.Controls.MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", LogLevelNameConverter.Convert(entry.Value.Key, typeof(string), null, this.Application.CultureInfo)),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForReading"),
					}.ShowDialog(this);
					continue;
				}
				var index = this.logLevelMapEntriesForWriting.Add(entry.Value);
				this.SelectListBoxItem(this.logLevelMapForWritingListBox, index);
				break;
			}
		}


		// Add log pattern.
		async void AddLogPattern()
		{
			var logPattern = await new LogPatternEditorDialog().ShowDialog<LogPattern>(this);
			if (logPattern != null)
			{
				this.logPatterns.Add(logPattern);
				this.SelectListBoxItem(this.logPatternListBox, this.logPatterns.Count - 1);
			}
		}


		// Add visible log property.
		async void AddVisibleLogProperty()
		{
			var logProperty = await new VisibleLogPropertyEditorDialog().ShowDialog<LogProperty>(this);
			if (logProperty != null)
			{
				this.visibleLogProperties.Add(logProperty);
				this.SelectListBoxItem(this.visibleLogPropertyListBox, this.visibleLogProperties.Count - 1);
			}
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
				var checkingEntry = this.logLevelMapEntriesForReading.FirstOrDefault(it => it.Key == newEntry.Value.Key);
				if (checkingEntry.Key != null && !entry.Equals(checkingEntry))
				{
					await new AppSuite.Controls.MessageDialog()
					{
						Icon = AppSuite.Controls.MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", newEntry.Value.Key),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForReading"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForReading.Remove(entry);
				var index = this.logLevelMapEntriesForReading.Add(newEntry.Value);
				this.SelectListBoxItem(this.logLevelMapForReadingListBox, index);
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
				var checkingEntry = this.logLevelMapEntriesForWriting.FirstOrDefault(it => it.Key == newEntry.Value.Key);
				if (checkingEntry.Value != null && !entry.Equals(checkingEntry))
				{
					await new AppSuite.Controls.MessageDialog()
					{
						Icon = AppSuite.Controls.MessageDialogIcon.Warning,
						Message = this.Application.GetFormattedString("LogProfileEditorDialog.DuplicateLogLevelMapEntry", LogLevelNameConverter.Convert(newEntry.Value.Key, typeof(string), null, this.Application.CultureInfo)),
						Title = this.Application.GetString("LogProfileEditorDialog.LogLevelMapForWriting"),
					}.ShowDialog(this);
					continue;
				}
				this.logLevelMapEntriesForWriting.Remove(entry);
				var index = this.logLevelMapEntriesForWriting.Add(newEntry.Value);
				this.SelectListBoxItem(this.logLevelMapForWritingListBox, index);
				break;
			}
		}


		// Edit log pattern.
		async void EditLogPattern(ListBoxItem item)
		{
			var index = this.logPatterns.IndexOf((LogPattern)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (item.DataContext is not LogPattern logPattern)
				return;
			var newLlogPattern = await new LogPatternEditorDialog()
			{
				LogPattern = logPattern
			}.ShowDialog<LogPattern>(this);
			if (newLlogPattern != null && newLlogPattern != logPattern)
			{
				this.logPatterns[index] = newLlogPattern;
				this.SelectListBoxItem(this.logPatternListBox, index);
			}
		}


		// Edit visible log property.
		async void EditVisibleLogProperty(ListBoxItem item)
		{
			var index = this.visibleLogProperties.IndexOf((LogProperty)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (item.DataContext is not LogProperty logProperty)
				return;
			var newLogProperty = await new VisibleLogPropertyEditorDialog()
			{
				LogProperty = logProperty
			}.ShowDialog<LogProperty>(this);
			if (newLogProperty != null && newLogProperty != logProperty)
			{
				this.visibleLogProperties[index] = newLogProperty;
				this.SelectListBoxItem(this.visibleLogPropertyListBox, index);
			}
		}


		// Generate result.
		protected override async Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			// check log patterns and visible log properties
			if (this.logPatterns.IsEmpty())
			{
				if (this.visibleLogProperties.IsNotEmpty())
				{
					var result = await new AppSuite.Controls.MessageDialog()
					{
						Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
						Icon = AppSuite.Controls.MessageDialogIcon.Warning,
						Message = this.Application.GetString("LogProfileEditorDialog.VisibleLogPropertiesWithoutLogPatterns"),
					}.ShowDialog(this);
					if (result == AppSuite.Controls.MessageDialogResult.Yes)
					{
						this.baseScrollViewer.ScrollIntoView(this.logPatternListBox);
						this.logPatternListBox.Focus();
						return null;
					}
				}
			}
			else if (this.visibleLogProperties.IsEmpty())
			{
				var result = await new AppSuite.Controls.MessageDialog()
				{
					Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = this.Application.GetString("LogProfileEditorDialog.LogPatternsWithoutVisibleLogProperties"),
				}.ShowDialog(this);
				if (result == AppSuite.Controls.MessageDialogResult.Yes)
				{
					this.baseScrollViewer.ScrollIntoView(this.visibleLogPropertyListBox);
					this.visibleLogPropertyListBox.Focus();
					return null;
				}
			}

			// update log profile
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
			logProfile.LogStringEncodingForReading = (LogStringEncoding)this.logStringEncodingForReadingComboBox.SelectedItem.AsNonNull();
			logProfile.LogStringEncodingForWriting = (LogStringEncoding)this.logStringEncodingForWritingComboBox.SelectedItem.AsNonNull();
			logProfile.LogWritingFormat = this.logWritingFormatTextBox.Text?.Let(it =>
			{
				if (string.IsNullOrWhiteSpace(it))
					return null;
				return it;
			});
			logProfile.Name = this.nameTextBox.Text.AsNonNull();
			logProfile.SortDirection = (SortDirection)this.sortDirectionComboBox.SelectedItem.AsNonNull();
			logProfile.SortKey = (LogSortKey)this.sortKeyComboBox.SelectedItem.AsNonNull();
			logProfile.TimestampEncodingForReading = (LogTimestampEncoding)this.timestampEncodingForReadingComboBox.SelectedItem.AsNonNull();
			logProfile.TimestampFormatForDisplaying = this.timestampFormatForDisplayingTextBox.Text;
			logProfile.TimestampFormatForReading = this.timestampFormatForReadingTextBox.Text;
			logProfile.TimestampFormatForWriting = this.timestampFormatForWritingTextBox.Text;
			logProfile.VisibleLogProperties = this.visibleLogProperties;
			return logProfile;
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
			var index = this.logPatterns.IndexOf((LogPattern)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (index < this.logPatterns.Count - 1)
			{
				this.logPatterns.Move(index, index + 1);
				++index;
			}
			this.SelectListBoxItem(this.logPatternListBox, index);
		}


		// Move log pattern up.
		void MoveLogPatternUp(ListBoxItem item)
		{
			var index = this.logPatterns.IndexOf((LogPattern)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (index > 0)
			{
				this.logPatterns.Move(index, index - 1);
				--index;
			}
			this.SelectListBoxItem(this.logPatternListBox, index);
		}


		// Move visible log property down.
		void MoveVisibleLogPropertyDown(ListBoxItem item)
		{
			var index = this.visibleLogProperties.IndexOf((LogProperty)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (index < this.visibleLogProperties.Count - 1)
			{
				this.visibleLogProperties.Move(index, index + 1);
				++index;
			}
			this.SelectListBoxItem(this.visibleLogPropertyListBox, index);
		}


		// Move visible log property up.
		void MoveVisibleLogPropertyUp(ListBoxItem item)
		{
			var index = this.visibleLogProperties.IndexOf((LogProperty)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (index > 0)
			{
				this.visibleLogProperties.Move(index, index - 1);
				--index;
			}
			this.SelectListBoxItem(this.visibleLogPropertyListBox, index);
		}


		// Called when selection of data source provider changed.
		void OnDataSourceProviderComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
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


		// Called when double-tapped on list box.
		void OnListBoxDoubleTapped(object? sender, RoutedEventArgs e)
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
			if (listBox == this.logLevelMapForReadingListBox)
				this.EditLogLevelMapEntryForReading((KeyValuePair<string, LogLevel>)selectedItem.AsNonNull());
			else if (listBox == this.logLevelMapForWritingListBox)
				this.EditLogLevelMapEntryForWriting((KeyValuePair<LogLevel, string>)selectedItem.AsNonNull());
			else if (listBox == this.logPatternListBox)
				this.EditLogPattern(listBoxItem);
			else if (listBox == this.visibleLogPropertyListBox)
				this.EditVisibleLogProperty(listBoxItem);
		}


		// Called when list box lost focus.
		void OnListBoxLostFocus(object? sender, RoutedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			listBox.SelectedItems.Clear();
		}


		// Called when selection in list box changed.
		void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			if (listBox.SelectedIndex >= 0)
				listBox.ScrollIntoView(listBox.SelectedIndex);
		}


		// Called when opened.
		protected override async void OnOpened(EventArgs e)
		{
			// setup initial state and focus
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Create");
				this.colorIndicatorComboBox.SelectedItem = LogColorIndicator.None;
				this.dataSourceProviderComboBox.SelectedItem = LogDataSourceProviders.All.FirstOrDefault(it => it is FileLogDataSourceProvider);
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
				this.logStringEncodingForReadingComboBox.SelectedItem = LogStringEncoding.Plane;
				this.logStringEncodingForWritingComboBox.SelectedItem = LogStringEncoding.Plane;
				this.sortDirectionComboBox.SelectedItem = SortDirection.Ascending;
				this.sortKeyComboBox.SelectedItem = LogSortKey.Timestamp;
				this.timestampEncodingForReadingComboBox.SelectedItem = LogTimestampEncoding.Custom;
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
				this.logStringEncodingForReadingComboBox.SelectedItem = profile.LogStringEncodingForReading;
				this.logStringEncodingForWritingComboBox.SelectedItem = profile.LogStringEncodingForWriting;
				this.logWritingFormatTextBox.Text = profile.LogWritingFormat;
				this.nameTextBox.Text = profile.Name;
				this.sortDirectionComboBox.SelectedItem = profile.SortDirection;
				this.sortKeyComboBox.SelectedItem = profile.SortKey;
				this.timestampEncodingForReadingComboBox.SelectedItem = profile.TimestampEncodingForReading;
				this.timestampFormatForDisplayingTextBox.Text = profile.TimestampFormatForDisplaying;
				this.timestampFormatForReadingTextBox.Text = profile.TimestampFormatForReading;
				this.timestampFormatForWritingTextBox.Text = profile.TimestampFormatForWriting;
				this.visibleLogProperties.AddRange(profile.VisibleLogProperties);
				this.workingDirNeededSwitch.IsChecked = profile.IsWorkingDirectoryNeeded;
			}
			else
				this.SynchronizationContext.Post(this.Close);
			this.nameTextBox.Focus();

			// call base
			base.OnOpened(e);

			// show hint of 'learn about logs reading and parsing'
			if (!this.PersistentState.GetValueOrDefault(HasLearnAboutLogsReadingAndParsingHintShown))
			{
				var result = await new AppSuite.Controls.MessageDialog()
				{
					Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
					Icon = AppSuite.Controls.MessageDialogIcon.Question,
					Message = this.Application.GetString("LogProfileEditorDialog.LearnAboutLogsReadingAndParsingFirst"),
					Title = this.Title
				}.ShowDialog(this);
				if (this.IsOpened)
				{
					this.PersistentState.SetValue<bool>(HasLearnAboutLogsReadingAndParsingHintShown, true);
					if (result == AppSuite.Controls.MessageDialogResult.Yes)
						Platform.OpenLink(LogsReadingAndParsingPageUri);
				}
			}
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
			foreach (var optionName in dataSourceProvider.RequiredSourceOptions)
			{
				switch (optionName)
				{
					case nameof(LogDataSourceOptions.Category):
					case nameof(LogDataSourceOptions.Command):
					case nameof(LogDataSourceOptions.QueryString):
					case nameof(LogDataSourceOptions.Uri):
						if (!this.dataSourceOptions.IsOptionSet(optionName))
						{
							this.SetValue<bool>(IsValidDataSourceOptionsProperty, false);
							return false;
						}
						break;
				}
			}
			this.SetValue<bool>(IsValidDataSourceOptionsProperty, true);

			// check name
			if (string.IsNullOrEmpty(this.nameTextBox.Text))
				return false;

			// ok
			return true;
		}


		// Remove log level map entry.
		void RemoveLogLevelMapEntry(object entry)
		{
			if (entry is KeyValuePair<string, LogLevel> readingEntry)
			{
				this.logLevelMapEntriesForReading.Remove(readingEntry);
				this.SelectListBoxItem(this.logLevelMapForReadingListBox, -1);
			}
			else if (entry is KeyValuePair<LogLevel, string> writingEntry)
			{
				this.logLevelMapEntriesForWriting.Remove(writingEntry);
				this.SelectListBoxItem(this.logLevelMapForWritingListBox, -1);
			}
		}


		// Remove log pattern.
		void RemoveLogPattern(ListBoxItem item)
		{
			var index = this.logPatterns.IndexOf((LogPattern)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.logPatterns.RemoveAt(index);
			this.SelectListBoxItem(this.logPatternListBox, -1);
		}


		// Remove visible log property.
		void RemoveVisibleLogProperty(ListBoxItem item)
		{
			var index = this.visibleLogProperties.IndexOf((LogProperty)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.visibleLogProperties.RemoveAt(index);
			this.SelectListBoxItem(this.visibleLogPropertyListBox, -1);
		}


		// Select given item in list box.
		void SelectListBoxItem(ListBox listBox, int index)
		{
			this.SynchronizationContext.Post(() =>
			{
				listBox.SelectedItems.Clear();
				if (index < 0 || index >= listBox.GetItemCount())
					return;
				listBox.Focus();
				listBox.SelectedIndex = index;
				listBox.ScrollIntoView(index);
			});
		}


		// Set log data source options.
		async void SetDataSourceOptions()
		{
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			if (dataSourceProvider == null)
				return;
			var options = await new LogDataSourceOptionsDialog()
			{
				DataSourceProvider = dataSourceProvider,
				Options = this.dataSourceOptions,
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


		// List of visible log properties.
		IList<LogProperty> VisibleLogProperties { get; }
	}
}
