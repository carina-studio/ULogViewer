using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using System;
using System.Collections.Generic;
using System.Linq;
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
		public static readonly Uri LogsReadingAndParsingPageUri = new Uri("https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs");


		// Static fields.
		static readonly SettingKey<bool> HasLearnAboutLogsReadingAndParsingHintShown = new SettingKey<bool>($"{nameof(LogProfileEditorDialog)}.{nameof(HasLearnAboutLogsReadingAndParsingHintShown)}");
		static readonly AvaloniaProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
		

		// Fields.
		readonly ToggleSwitch adminNeededSwitch;
		readonly Panel allowMultipleFilesPanel;
		readonly ToggleSwitch allowMultipleFilesSwitch;
		readonly ScrollViewer baseScrollViewer;
		readonly ComboBox colorIndicatorComboBox;
		readonly ToggleSwitch continuousReadingSwitch;
		LogDataSourceOptions dataSourceOptions;
		readonly ComboBox dataSourceProviderComboBox;
		readonly TextBox descriptionTextBox;
		readonly ComboBox iconComboBox;
		readonly SortedObservableList<KeyValuePair<string, LogLevel>> logLevelMapEntriesForReading = new SortedObservableList<KeyValuePair<string, LogLevel>>((x, y) => x.Key.CompareTo(y.Key));
		readonly SortedObservableList<KeyValuePair<LogLevel, string>> logLevelMapEntriesForWriting = new SortedObservableList<KeyValuePair<LogLevel, string>>((x, y) => x.Key.CompareTo(y.Key));
		readonly AppSuite.Controls.ListBox logLevelMapForReadingListBox;
		readonly AppSuite.Controls.ListBox logLevelMapForWritingListBox;
		readonly AppSuite.Controls.ListBox logPatternListBox;
		readonly ObservableList<LogPattern> logPatterns = new ObservableList<LogPattern>();
		readonly ComboBox logStringEncodingForReadingComboBox;
		readonly ComboBox logStringEncodingForWritingComboBox;
		readonly TextBox logWritingFormatTextBox;
		readonly TextBox nameTextBox;
		readonly IntegerTextBox restartReadingDelayTextBox;
		readonly ComboBox sortDirectionComboBox;
		readonly ComboBox sortKeyComboBox;
		readonly ComboBox timeSpanEncodingForReadingComboBox;
		readonly TextBox timeSpanFormatForDisplayingTextBox;
		readonly TextBox timeSpanFormatForWritingTextBox;
		readonly ObservableList<string> timeSpanFormatsForReading = new ObservableList<string>();
		readonly AppSuite.Controls.ListBox timeSpanFormatsForReadingListBox;
		readonly ComboBox timestampCategoryGranularityComboBox;
		readonly ComboBox timestampEncodingForReadingComboBox;
		readonly TextBox timestampFormatForDisplayingTextBox;
		readonly TextBox timestampFormatForWritingTextBox;
		readonly ObservableList<string> timestampFormatsForReading = new ObservableList<string>();
		readonly AppSuite.Controls.ListBox timestampFormatsForReadingListBox;
		readonly AppSuite.Controls.ListBox visibleLogPropertyListBox;
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
			this.TimeSpanFormatsForReading = this.timeSpanFormatsForReading.AsReadOnly();
			this.TimestampFormatsForReading = this.timestampFormatsForReading.AsReadOnly();
			this.VisibleLogProperties = this.visibleLogProperties.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}).AsReadOnly();

			// initialize.
			InitializeComponent();

			// setup controls
			this.adminNeededSwitch = this.FindControl<ToggleSwitch>("adminNeededSwitch").AsNonNull();
			this.allowMultipleFilesPanel = this.FindControl<Panel>(nameof(allowMultipleFilesPanel));
			this.allowMultipleFilesSwitch = this.allowMultipleFilesPanel.FindControl<ToggleSwitch>(nameof(allowMultipleFilesSwitch));
			this.baseScrollViewer = this.FindControl<ScrollViewer>("baseScrollViewer").AsNonNull();
			this.colorIndicatorComboBox = this.FindControl<ComboBox>("colorIndicatorComboBox").AsNonNull();
			this.continuousReadingSwitch = this.FindControl<ToggleSwitch>("continuousReadingSwitch").AsNonNull();
			this.dataSourceProviderComboBox = this.FindControl<ComboBox>("dataSourceProviderComboBox").AsNonNull();
			this.descriptionTextBox = this.FindControl<TextBox>(nameof(descriptionTextBox));
			this.iconComboBox = this.FindControl<ComboBox>("iconComboBox").AsNonNull();
			if (Platform.IsNotWindows)
				this.FindControl<Control>("isAdminNeededPanel").AsNonNull().IsVisible = false;
			this.logLevelMapForReadingListBox = this.FindControl<AppSuite.Controls.ListBox>("logLevelMapForReadingListBox").AsNonNull();
			this.logLevelMapForWritingListBox = this.FindControl<AppSuite.Controls.ListBox>("logLevelMapForWritingListBox").AsNonNull();
			this.logPatternListBox = this.FindControl<AppSuite.Controls.ListBox>("logPatternListBox").AsNonNull();
			this.logStringEncodingForReadingComboBox = this.FindControl<ComboBox>("logStringEncodingForReadingComboBox").AsNonNull();
			this.logStringEncodingForWritingComboBox = this.FindControl<ComboBox>("logStringEncodingForWritingComboBox").AsNonNull();
			this.logWritingFormatTextBox = this.FindControl<StringInterpolationFormatTextBox>("logWritingFormatTextBox").AsNonNull().Also(it =>
			{
				foreach (var propertyName in Log.PropertyNames)
				{
					it.PredefinedVariables.Add(new StringInterpolationVariable().Also(variable =>
					{
						variable.Bind(StringInterpolationVariable.DisplayNameProperty, new Binding() 
						{
							Converter = Converters.LogPropertyNameConverter.Default,
							Path = nameof(StringInterpolationVariable.Name),
							Source = variable,
						});
						variable.Name = propertyName;
					}));
				}
				it.PredefinedVariables.Add(new StringInterpolationVariable().Also(variable =>
				{
					variable.Bind(StringInterpolationVariable.DisplayNameProperty,this.GetResourceObservable("String/Common.NewLine"));
					variable.Name = "NewLine";
				}));
			});
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.restartReadingDelayTextBox = this.FindControl<IntegerTextBox>(nameof(restartReadingDelayTextBox)).AsNonNull();
			this.sortDirectionComboBox = this.FindControl<ComboBox>("sortDirectionComboBox").AsNonNull();
			this.sortKeyComboBox = this.FindControl<ComboBox>("sortKeyComboBox").AsNonNull();
			this.timeSpanEncodingForReadingComboBox = this.FindControl<ComboBox>(nameof(timeSpanEncodingForReadingComboBox));
			this.timeSpanFormatForDisplayingTextBox = this.FindControl<TextBox>(nameof(timeSpanFormatForDisplayingTextBox));
			this.timeSpanFormatForWritingTextBox = this.FindControl<TextBox>(nameof(timeSpanFormatForWritingTextBox));
			this.timeSpanFormatsForReadingListBox = this.FindControl<AppSuite.Controls.ListBox>(nameof(timeSpanFormatsForReadingListBox));
			this.timestampCategoryGranularityComboBox = this.FindControl<ComboBox>(nameof(timestampCategoryGranularityComboBox));
			this.timestampEncodingForReadingComboBox = this.FindControl<ComboBox>(nameof(timestampEncodingForReadingComboBox)).AsNonNull();
			this.timestampFormatForDisplayingTextBox = this.FindControl<TextBox>("timestampFormatForDisplayingTextBox").AsNonNull();
			this.timestampFormatForWritingTextBox = this.FindControl<TextBox>("timestampFormatForWritingTextBox").AsNonNull();
			this.timestampFormatsForReadingListBox = this.FindControl<AppSuite.Controls.ListBox>(nameof(timestampFormatsForReadingListBox)).AsNonNull();
			this.visibleLogPropertyListBox = this.FindControl<AppSuite.Controls.ListBox>("visibleLogPropertyListBox").AsNonNull();
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
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
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


		// Add time span format for reading log.
		async void AddTimeSpanFormatForReading()
		{
			var format = await new TimeSpanFormatInputDialog()
			{
				Title = this.Application.GetString("LogProfileEditorDialog.TimeSpanFormatsForReading")
			}.ShowDialog<string>(this);
			if (!string.IsNullOrWhiteSpace(format))
			{
				this.timeSpanFormatsForReading.Add(format);
				this.SelectListBoxItem(this.timeSpanFormatsForReadingListBox, this.timeSpanFormatsForReading.Count - 1);
			}
		}


		// Add timestamp format for reading log.
		async void AddTimestampFormatForReading()
        {
			var format = await new DateTimeFormatInputDialog()
			{
				Title = this.Application.GetString("LogProfileEditorDialog.TimestampFormatsForReading")
			}.ShowDialog<string>(this);
			if (!string.IsNullOrWhiteSpace(format))
			{
				this.timestampFormatsForReading.Add(format);
				this.SelectListBoxItem(this.timestampFormatsForReadingListBox, this.timestampFormatsForReading.Count - 1);
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
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
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
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
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


		// Edit time span format for reading logs.
		async void EditTimeSpanFormatForReading(ListBoxItem item)
		{
			var format = (string)item.DataContext.AsNonNull();
			var index = this.timeSpanFormatsForReading.IndexOf(format);
			if (index < 0)
				return;
			var newFormat = await new TimeSpanFormatInputDialog()
			{
				InitialFormat = format,
				Title = this.Application.GetString("LogProfileEditorDialog.TimeSpanFormatsForReading")
			}.ShowDialog<string>(this);
			if (!string.IsNullOrWhiteSpace(newFormat) && newFormat != format)
			{
				this.timeSpanFormatsForReading[index] = newFormat;
				this.SelectListBoxItem(this.timeSpanFormatsForReadingListBox, index);
			}
		}


		// Edit timestamp format for reading logs.
		async void EditTimestampFormatForReading(ListBoxItem item)
		{
			var format = (string)item.DataContext.AsNonNull();
			var index = this.timestampFormatsForReading.IndexOf(format);
			if (index < 0)
				return;
			var newFormat = await new DateTimeFormatInputDialog()
			{
				InitialFormat = format,
				Title = this.Application.GetString("LogProfileEditorDialog.TimestampFormatsForReading")
			}.ShowDialog<string>(this);
			if (!string.IsNullOrWhiteSpace(newFormat) && newFormat != format)
			{
				this.timestampFormatsForReading[index] = newFormat;
				this.SelectListBoxItem(this.timestampFormatsForReadingListBox, index);
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
					var result = await new MessageDialog()
					{
						Buttons = MessageDialogButtons.YesNo,
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetString("LogProfileEditorDialog.VisibleLogPropertiesWithoutLogPatterns"),
					}.ShowDialog(this);
					if (result == MessageDialogResult.Yes)
					{
						this.baseScrollViewer.ScrollIntoView(this.logPatternListBox);
						this.logPatternListBox.Focus();
						return null;
					}
				}
			}
			else if (this.visibleLogProperties.IsEmpty())
			{
				var result = await new MessageDialog()
				{
					Buttons = MessageDialogButtons.YesNo,
					Icon = MessageDialogIcon.Warning,
					Message = this.Application.GetString("LogProfileEditorDialog.LogPatternsWithoutVisibleLogProperties"),
				}.ShowDialog(this);
				if (result == MessageDialogResult.Yes)
				{
					this.baseScrollViewer.ScrollIntoView(this.visibleLogPropertyListBox);
					this.visibleLogPropertyListBox.Focus();
					return null;
				}
			}

			// update log profile
			var logProfile = this.LogProfile ?? new LogProfile(this.Application);
			logProfile.AllowMultipleFiles = this.allowMultipleFilesSwitch.IsChecked.GetValueOrDefault();
			logProfile.ColorIndicator = (LogColorIndicator)this.colorIndicatorComboBox.SelectedItem.AsNonNull();
			logProfile.DataSourceOptions = this.dataSourceOptions;
			logProfile.DataSourceProvider = (ILogDataSourceProvider)this.dataSourceProviderComboBox.SelectedItem.AsNonNull();
			logProfile.Description = this.descriptionTextBox.Text;
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
			logProfile.RestartReadingDelay = this.restartReadingDelayTextBox.Value.GetValueOrDefault();
			logProfile.SortDirection = (SortDirection)this.sortDirectionComboBox.SelectedItem.AsNonNull();
			logProfile.SortKey = (LogSortKey)this.sortKeyComboBox.SelectedItem.AsNonNull();
			logProfile.TimeSpanEncodingForReading = (LogTimeSpanEncoding)this.timeSpanEncodingForReadingComboBox.SelectedItem.AsNonNull();
			logProfile.TimeSpanFormatForDisplaying = this.timeSpanFormatForDisplayingTextBox.Text;
			logProfile.TimeSpanFormatForWriting = this.timeSpanFormatForWritingTextBox.Text;
			logProfile.TimeSpanFormatsForReading = this.timeSpanFormatsForReading;
			logProfile.TimestampCategoryGranularity = (TimestampDisplayableLogCategoryGranularity)this.timestampCategoryGranularityComboBox.SelectedItem.AsNonNull();
			logProfile.TimestampEncodingForReading = (LogTimestampEncoding)this.timestampEncodingForReadingComboBox.SelectedItem.AsNonNull();
			logProfile.TimestampFormatForDisplaying = this.timestampFormatForDisplayingTextBox.Text;
			logProfile.TimestampFormatForWriting = this.timestampFormatForWritingTextBox.Text;
			logProfile.TimestampFormatsForReading = this.timestampFormatsForReading;
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
		void OnListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e)
		{
			if (sender is not Avalonia.Controls.ListBox listBox)
				return;
			if (!listBox.TryFindListBoxItem(e.Item, out var listBoxItem) || listBoxItem == null)
				return;
			if (listBox == this.logLevelMapForReadingListBox)
				this.EditLogLevelMapEntryForReading((KeyValuePair<string, LogLevel>)e.Item);
			else if (listBox == this.logLevelMapForWritingListBox)
				this.EditLogLevelMapEntryForWriting((KeyValuePair<LogLevel, string>)e.Item);
			else if (listBox == this.logPatternListBox)
				this.EditLogPattern(listBoxItem);
			else if (listBox == this.timeSpanFormatsForReadingListBox)
				this.EditTimeSpanFormatForReading(listBoxItem);
			else if (listBox == this.timestampFormatsForReadingListBox)
				this.EditTimestampFormatForReading(listBoxItem);
			else if (listBox == this.visibleLogPropertyListBox)
				this.EditVisibleLogProperty(listBoxItem);
		}


		// Called when list box lost focus.
		void OnListBoxLostFocus(object? sender, RoutedEventArgs e)
		{
			if (sender is not Avalonia.Controls.ListBox listBox)
				return;
			listBox.SelectedItems.Clear();
		}


		// Called when selection in list box changed.
		void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (sender is not Avalonia.Controls.ListBox listBox)
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
				this.allowMultipleFilesSwitch.IsChecked = true;
				this.colorIndicatorComboBox.SelectedItem = LogColorIndicator.None;
				this.dataSourceProviderComboBox.SelectedItem = LogDataSourceProviders.All.FirstOrDefault(it => it is FileLogDataSourceProvider);
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
				this.logStringEncodingForReadingComboBox.SelectedItem = LogStringEncoding.Plane;
				this.logStringEncodingForWritingComboBox.SelectedItem = LogStringEncoding.Plane;
				this.sortDirectionComboBox.SelectedItem = SortDirection.Ascending;
				this.sortKeyComboBox.SelectedItem = LogSortKey.Timestamp;
				this.timeSpanEncodingForReadingComboBox.SelectedItem = LogTimeSpanEncoding.Custom;
				this.timestampCategoryGranularityComboBox.SelectedItem = TimestampDisplayableLogCategoryGranularity.Day;
				this.timestampEncodingForReadingComboBox.SelectedItem = LogTimestampEncoding.Custom;
			}
			else if (!profile.IsBuiltIn)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Edit");
				this.adminNeededSwitch.IsChecked = profile.IsAdministratorNeeded;
				this.allowMultipleFilesSwitch.IsChecked = profile.AllowMultipleFiles;
				this.colorIndicatorComboBox.SelectedItem = profile.ColorIndicator;
				this.dataSourceOptions = profile.DataSourceOptions;
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
				this.descriptionTextBox.Text = profile.Description;
				this.iconComboBox.SelectedItem = profile.Icon;
				this.continuousReadingSwitch.IsChecked = profile.IsContinuousReading;
				this.logLevelMapEntriesForReading.AddAll(profile.LogLevelMapForReading);
				this.logLevelMapEntriesForWriting.AddAll(profile.LogLevelMapForWriting);
				this.logPatterns.AddRange(profile.LogPatterns);
				this.logStringEncodingForReadingComboBox.SelectedItem = profile.LogStringEncodingForReading;
				this.logStringEncodingForWritingComboBox.SelectedItem = profile.LogStringEncodingForWriting;
				this.logWritingFormatTextBox.Text = profile.LogWritingFormat;
				this.nameTextBox.Text = profile.Name;
				this.restartReadingDelayTextBox.Value = profile.RestartReadingDelay;
				this.sortDirectionComboBox.SelectedItem = profile.SortDirection;
				this.sortKeyComboBox.SelectedItem = profile.SortKey;
				this.timeSpanEncodingForReadingComboBox.SelectedItem = profile.TimeSpanEncodingForReading;
				this.timeSpanFormatForDisplayingTextBox.Text = profile.TimeSpanFormatForDisplaying;
				this.timeSpanFormatForWritingTextBox.Text = profile.TimeSpanFormatForWriting;
				this.timeSpanFormatsForReading.AddRange(profile.TimeSpanFormatsForReading);
				this.timestampCategoryGranularityComboBox.SelectedItem = profile.TimestampCategoryGranularity;
				this.timestampEncodingForReadingComboBox.SelectedItem = profile.TimestampEncodingForReading;
				this.timestampFormatForDisplayingTextBox.Text = profile.TimestampFormatForDisplaying;
				this.timestampFormatForWritingTextBox.Text = profile.TimestampFormatForWriting;
				this.timestampFormatsForReading.AddRange(profile.TimestampFormatsForReading);
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
				var result = await new MessageDialog()
				{
					Buttons = MessageDialogButtons.YesNo,
					Icon = MessageDialogIcon.Question,
					Message = this.Application.GetString("LogProfileEditorDialog.LearnAboutLogsReadingAndParsingFirst"),
					Title = this.Title
				}.ShowDialog(this);
				if (this.IsOpened)
				{
					this.PersistentState.SetValue<bool>(HasLearnAboutLogsReadingAndParsingHintShown, true);
					if (result == MessageDialogResult.Yes)
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
			this.allowMultipleFilesPanel.IsVisible = dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName));
			foreach (var optionName in dataSourceProvider.RequiredSourceOptions)
			{
				switch (optionName)
				{
					case nameof(LogDataSourceOptions.Category):
					case nameof(LogDataSourceOptions.Command):
					case nameof(LogDataSourceOptions.QueryString):
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


		// Remove time span format for reading.
		void RemoveTimeSpanFormatForReading(ListBoxItem item)
		{
			var index = this.TimeSpanFormatsForReading.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.timeSpanFormatsForReading.RemoveAt(index);
			this.SelectListBoxItem(this.timeSpanFormatsForReadingListBox, -1);
		}


		// Remove timestamp format for reading.
		void RemoveTimestampFormatForReading(ListBoxItem item)
		{
			var index = this.timestampFormatsForReading.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.timestampFormatsForReading.RemoveAt(index);
			this.SelectListBoxItem(this.timestampFormatsForReadingListBox, -1);
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
		void SelectListBoxItem(Avalonia.Controls.ListBox listBox, int index)
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


		// List of time span format to read logs.
		IList<string> TimeSpanFormatsForReading { get; }


		// List of timestamp format to read logs.
		IList<string> TimestampFormatsForReading { get; }


		// List of visible log properties.
		IList<LogProperty> VisibleLogProperties { get; }
	}
}
