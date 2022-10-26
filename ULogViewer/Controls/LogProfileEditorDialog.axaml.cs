using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Product;
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
		static readonly Dictionary<LogProfile, LogProfileEditorDialog> NonBlockingDialogs = new();
		static readonly AvaloniaProperty<bool> HasDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("HasDataSourceOptions");
		static readonly SettingKey<bool> HasLearnAboutLogsReadingAndParsingHintShown = new SettingKey<bool>($"{nameof(LogProfileEditorDialog)}.{nameof(HasLearnAboutLogsReadingAndParsingHintShown)}");
		static readonly AvaloniaProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("IsProVersionActivated");
		static readonly AvaloniaProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
		static readonly AvaloniaProperty<bool> IsWorkingDirectorySupportedProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("IsWorkingDirectorySupported");
		

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
		readonly LogProfileIconColorComboBox iconColorComboBox;
		readonly LogProfileIconComboBox iconComboBox;
		readonly ToggleSwitch isTemplateSwitch;
		readonly SortedObservableList<KeyValuePair<string, LogLevel>> logLevelMapEntriesForReading = new SortedObservableList<KeyValuePair<string, LogLevel>>((x, y) => x.Key.CompareTo(y.Key));
		readonly SortedObservableList<KeyValuePair<LogLevel, string>> logLevelMapEntriesForWriting = new SortedObservableList<KeyValuePair<LogLevel, string>>((x, y) => x.Key.CompareTo(y.Key));
		readonly AppSuite.Controls.ListBox logLevelMapForReadingListBox;
		readonly AppSuite.Controls.ListBox logLevelMapForWritingListBox;
		readonly AppSuite.Controls.ListBox logPatternListBox;
		readonly ObservableList<LogPattern> logPatterns = new ObservableList<LogPattern>();
		readonly ComboBox logStringEncodingForReadingComboBox;
		readonly ComboBox logStringEncodingForWritingComboBox;
		readonly Avalonia.Controls.ListBox logWritingFormatListBox;
		readonly ObservableList<string> logWritingFormats = new();
		readonly TextBox nameTextBox;
		readonly ComboBox rawLogLevelPropertyComboBox;
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
			this.LogLevelMapEntriesForReading = ListExtensions.AsReadOnly(this.logLevelMapEntriesForReading);
			this.LogLevelMapEntriesForWriting = ListExtensions.AsReadOnly(this.logLevelMapEntriesForWriting);
			this.LogPatterns = ListExtensions.AsReadOnly(this.logPatterns.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}));
			this.LogWritingFormats = ListExtensions.AsReadOnly(this.logWritingFormats);
			this.TimeSpanFormatsForReading = ListExtensions.AsReadOnly(this.timeSpanFormatsForReading);
			this.TimestampFormatsForReading = ListExtensions.AsReadOnly(this.timestampFormatsForReading);
			this.VisibleLogProperties = ListExtensions.AsReadOnly(this.visibleLogProperties.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}));

			// initialize.
			AvaloniaXamlLoader.Load(this);

			// setup controls
			this.adminNeededSwitch = this.Get<ToggleSwitch>("adminNeededSwitch");
			this.allowMultipleFilesPanel = this.Get<Panel>(nameof(allowMultipleFilesPanel));
			this.allowMultipleFilesSwitch = this.allowMultipleFilesPanel.FindControl<ToggleSwitch>(nameof(allowMultipleFilesSwitch));
			this.baseScrollViewer = this.Get<ScrollViewer>("baseScrollViewer");
			this.colorIndicatorComboBox = this.Get<ComboBox>("colorIndicatorComboBox");
			this.continuousReadingSwitch = this.Get<ToggleSwitch>("continuousReadingSwitch");
			this.dataSourceProviderComboBox = this.Get<ComboBox>("dataSourceProviderComboBox").Also(it =>
			{
				it.GetObservable(ComboBox.SelectedItemProperty).Subscribe(item =>
				{
					if (item is not ILogDataSourceProvider provider)
						return;
					this.SetValue<bool>(HasDataSourceOptionsProperty, provider.SupportedSourceOptions.IsNotEmpty());
					this.SetValue<bool>(IsWorkingDirectorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory))
						&& !provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory)));
				});
			});
			this.descriptionTextBox = this.Get<TextBox>(nameof(descriptionTextBox));
			this.iconColorComboBox = this.Get<LogProfileIconColorComboBox>(nameof(iconColorComboBox));
			this.iconComboBox = this.Get<LogProfileIconComboBox>("iconComboBox");
			if (Platform.IsNotWindows)
				this.Get<Control>("isAdminNeededPanel").IsVisible = false;
			this.isTemplateSwitch = this.Get<ToggleSwitch>(nameof(isTemplateSwitch)).Also(it =>
			{
				it.GetObservable(ToggleSwitch.IsCheckedProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.logLevelMapForReadingListBox = this.Get<AppSuite.Controls.ListBox>("logLevelMapForReadingListBox");
			this.logLevelMapForWritingListBox = this.Get<AppSuite.Controls.ListBox>("logLevelMapForWritingListBox");
			this.logPatternListBox = this.Get<AppSuite.Controls.ListBox>("logPatternListBox");
			this.logStringEncodingForReadingComboBox = this.Get<ComboBox>("logStringEncodingForReadingComboBox");
			this.logStringEncodingForWritingComboBox = this.Get<ComboBox>("logStringEncodingForWritingComboBox");
			this.logWritingFormatListBox = this.Get<Avalonia.Controls.ListBox>(nameof(logWritingFormatListBox));
			this.nameTextBox = this.Get<TextBox>("nameTextBox");
			this.rawLogLevelPropertyComboBox = this.Get<ComboBox>(nameof(rawLogLevelPropertyComboBox));
			this.restartReadingDelayTextBox = this.Get<IntegerTextBox>(nameof(restartReadingDelayTextBox));
			this.sortDirectionComboBox = this.Get<ComboBox>("sortDirectionComboBox");
			this.sortKeyComboBox = this.Get<ComboBox>("sortKeyComboBox");
			this.timeSpanEncodingForReadingComboBox = this.Get<ComboBox>(nameof(timeSpanEncodingForReadingComboBox));
			this.timeSpanFormatForDisplayingTextBox = this.Get<TextBox>(nameof(timeSpanFormatForDisplayingTextBox));
			this.timeSpanFormatForWritingTextBox = this.Get<TextBox>(nameof(timeSpanFormatForWritingTextBox));
			this.timeSpanFormatsForReadingListBox = this.Get<AppSuite.Controls.ListBox>(nameof(timeSpanFormatsForReadingListBox));
			this.timestampCategoryGranularityComboBox = this.Get<ComboBox>(nameof(timestampCategoryGranularityComboBox));
			this.timestampEncodingForReadingComboBox = this.Get<ComboBox>(nameof(timestampEncodingForReadingComboBox));
			this.timestampFormatForDisplayingTextBox = this.Get<TextBox>("timestampFormatForDisplayingTextBox");
			this.timestampFormatForWritingTextBox = this.Get<TextBox>("timestampFormatForWritingTextBox");
			this.timestampFormatsForReadingListBox = this.Get<AppSuite.Controls.ListBox>(nameof(timestampFormatsForReadingListBox));
			this.visibleLogPropertyListBox = this.Get<AppSuite.Controls.ListBox>("visibleLogPropertyListBox");
			this.workingDirNeededSwitch = this.Get<ToggleSwitch>("workingDirNeededSwitch");
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
						Message = new FormattedString().Also(it =>
						{
							it.Arg1 = entry.Value.Key;
							it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileEditorDialog.DuplicateLogLevelMapEntry"));
						}),
						Title = this.Application.GetResourceObservable("String/LogProfileEditorDialog.LogLevelMapForReading"),
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
						Message = new FormattedString().Also(it =>
						{
							it.Arg1 = LogLevelNameConverter.Convert(entry.Value.Key, typeof(string), null, this.Application.CultureInfo);
							it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileEditorDialog.DuplicateLogLevelMapEntry"));
						}),
						Title = this.GetResourceObservable("String/LogProfileEditorDialog.LogLevelMapForReading"),
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


		// Add log writing format.
		async void AddLogWritingFormat()
		{
			var format = await new LogWritingFormatEditorDialog().ShowDialog<string?>(this);
			if (format != null)
			{
				this.logWritingFormats.Add(format);
				this.SelectListBoxItem(this.logWritingFormatListBox, this.logWritingFormats.Count - 1);
			}
		}


		// Add time span format for reading log.
		async void AddTimeSpanFormatForReading()
		{
			var format = await new TimeSpanFormatInputDialog().Also(it =>
			{
				it.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.TimeSpanFormatsForReading"));
			}).ShowDialog<string>(this);
			if (!string.IsNullOrWhiteSpace(format))
			{
				this.timeSpanFormatsForReading.Add(format);
				this.SelectListBoxItem(this.timeSpanFormatsForReadingListBox, this.timeSpanFormatsForReading.Count - 1);
			}
		}


		// Add timestamp format for reading log.
		async void AddTimestampFormatForReading()
        {
			var format = await new DateTimeFormatInputDialog().Also(it =>
			{
				it.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.TimestampFormatsForReading"));
			}).ShowDialog<string>(this);
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
						Message = new FormattedString().Also(it =>
						{
							it.Arg1 = newEntry.Value.Key;
							it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileEditorDialog.DuplicateLogLevelMapEntry"));
						}),
						Title = this.GetResourceObservable("String/LogProfileEditorDialog.LogLevelMapForReading"),
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
						Message = new FormattedString().Also(it =>
						{
							it.Arg1 = LogLevelNameConverter.Convert(newEntry.Value.Key, typeof(string), null, this.Application.CultureInfo);
							it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileEditorDialog.DuplicateLogLevelMapEntry"));
						}),
						Title = this.GetResourceObservable("String/LogProfileEditorDialog.LogLevelMapForWriting"),
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
			if (item.DataContext is not LogPattern logPattern)
				return;
			var index = this.logPatterns.IndexOf(logPattern);
			if (index < 0)
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


		// Edit log writing format.
		async void EditLogWritingFormat(ListBoxItem item)
		{
			if (item.DataContext is not string format)
				return;
			var index = this.logWritingFormats.IndexOf(format);
			if (index < 0)
				return;
			var newFormat = await new LogWritingFormatEditorDialog()
			{
				Format = format
			}.ShowDialog<string?>(this);
			if (newFormat != null && newFormat != format)
			{
				this.logWritingFormats[index] = newFormat;
				this.SelectListBoxItem(this.logWritingFormatListBox, index);
			}
		}


		// Edit time span format for reading logs.
		async void EditTimeSpanFormatForReading(ListBoxItem item)
		{
			var format = (string)item.DataContext.AsNonNull();
			var index = this.timeSpanFormatsForReading.IndexOf(format);
			if (index < 0)
				return;
			var newFormat = await new TimeSpanFormatInputDialog().Also(it =>
			{
				it.InitialFormat = format;
				it.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.TimeSpanFormatsForReading"));
			}).ShowDialog<string>(this);
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
			var newFormat = await new DateTimeFormatInputDialog().Also(it =>
			{
				it.InitialFormat = format;
				it.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.TimestampFormatsForReading"));
			}).ShowDialog<string>(this);
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
			var isTemplate = this.isTemplateSwitch.IsChecked.GetValueOrDefault();
			if (this.logPatterns.IsEmpty() && !isTemplate)
			{
				if (this.visibleLogProperties.IsNotEmpty())
				{
					var result = await new MessageDialog()
					{
						Buttons = MessageDialogButtons.YesNo,
						Icon = MessageDialogIcon.Warning,
						Message = this.GetResourceObservable("String/LogProfileEditorDialog.VisibleLogPropertiesWithoutLogPatterns"),
					}.ShowDialog(this);
					if (result == MessageDialogResult.Yes)
					{
						this.baseScrollViewer.ScrollIntoView(this.logPatternListBox);
						this.logPatternListBox.Focus();
						return null;
					}
				}
			}
			else if (this.visibleLogProperties.IsEmpty() && !isTemplate)
			{
				var result = await new MessageDialog()
				{
					Buttons = MessageDialogButtons.YesNo,
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/LogProfileEditorDialog.LogPatternsWithoutVisibleLogProperties"),
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
			logProfile.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
			logProfile.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
			logProfile.IsAdministratorNeeded = this.adminNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsContinuousReading = this.continuousReadingSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsTemplate = isTemplate;
			logProfile.IsWorkingDirectoryNeeded = this.workingDirNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.LogLevelMapForReading = new Dictionary<string, LogLevel>(this.logLevelMapEntriesForReading);
			logProfile.LogLevelMapForWriting = new Dictionary<LogLevel, string>(this.logLevelMapEntriesForWriting);
			logProfile.LogPatterns = this.logPatterns;
			logProfile.LogStringEncodingForReading = (LogStringEncoding)this.logStringEncodingForReadingComboBox.SelectedItem.AsNonNull();
			logProfile.LogStringEncodingForWriting = (LogStringEncoding)this.logStringEncodingForWritingComboBox.SelectedItem.AsNonNull();
			logProfile.LogWritingFormats = this.logWritingFormats;
			logProfile.Name = this.nameTextBox.Text.AsNonNull();
			logProfile.RawLogLevelPropertyName = (string)this.rawLogLevelPropertyComboBox.SelectedItem.AsNonNull();
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


		// Log writing formats.
		public IList<string> LogWritingFormats { get; }


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


		// Move log writing format down.
		void MoveLogWritingFormatDown(ListBoxItem item)
		{
			var index = this.logWritingFormats.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (index < this.logWritingFormats.Count - 1)
			{
				this.logWritingFormats.Move(index, index + 1);
				++index;
			}
			this.SelectListBoxItem(this.logWritingFormatListBox, index);
		}


		// Move log writing format up.
		void MoveLogWritingFormatUp(ListBoxItem item)
		{
			var index = this.logWritingFormats.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			if (index > 0)
			{
				this.logWritingFormats.Move(index, index - 1);
				--index;
			}
			this.SelectListBoxItem(this.logWritingFormatListBox, index);
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


		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			if (this.LogProfile != null 
				&& NonBlockingDialogs.TryGetValue(this.LogProfile, out var dialog)
				&& dialog == this)
			{
				NonBlockingDialogs.Remove(this.LogProfile);
			}

			// detach fron product manager
			this.Application.ProductManager.ProductStateChanged -= this.OnProductStateChanged;

			// call base
			base.OnClosed(e);
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
			else if (listBox == this.logWritingFormatListBox)
				this.EditLogWritingFormat(listBoxItem);
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
			// attach to product manager
			this.Application.ProductManager.Let(it =>
			{
				this.SetValue<bool>(IsProVersionActivatedProperty, it.IsProductActivated(Products.Professional));
				it.ProductStateChanged += this.OnProductStateChanged;
			});

			// setup initial state and focus
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.Title.Create"));
				this.allowMultipleFilesSwitch.IsChecked = true;
				this.colorIndicatorComboBox.SelectedItem = LogColorIndicator.None;
				this.dataSourceProviderComboBox.SelectedItem = LogDataSourceProviders.All.FirstOrDefault(it => it is FileLogDataSourceProvider);
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
				this.logStringEncodingForReadingComboBox.SelectedItem = LogStringEncoding.Plane;
				this.logStringEncodingForWritingComboBox.SelectedItem = LogStringEncoding.Plane;
				this.rawLogLevelPropertyComboBox.SelectedItem = nameof(Log.Level);
				this.sortDirectionComboBox.SelectedItem = SortDirection.Ascending;
				this.sortKeyComboBox.SelectedItem = LogSortKey.Timestamp;
				this.timeSpanEncodingForReadingComboBox.SelectedItem = LogTimeSpanEncoding.Custom;
				this.timestampCategoryGranularityComboBox.SelectedItem = TimestampDisplayableLogCategoryGranularity.Day;
				this.timestampEncodingForReadingComboBox.SelectedItem = LogTimestampEncoding.Custom;
			}
			else if (!profile.IsBuiltIn)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.Title.Edit"));
				this.adminNeededSwitch.IsChecked = profile.IsAdministratorNeeded;
				this.allowMultipleFilesSwitch.IsChecked = profile.AllowMultipleFiles;
				this.colorIndicatorComboBox.SelectedItem = profile.ColorIndicator;
				this.dataSourceOptions = profile.DataSourceOptions;
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
				this.descriptionTextBox.Text = profile.Description;
				this.iconColorComboBox.SelectedItem = profile.IconColor;
				this.iconComboBox.SelectedItem = profile.Icon;
				this.isTemplateSwitch.IsChecked = profile.IsTemplate;
				this.continuousReadingSwitch.IsChecked = profile.IsContinuousReading;
				this.logLevelMapEntriesForReading.AddAll(profile.LogLevelMapForReading);
				this.logLevelMapEntriesForWriting.AddAll(profile.LogLevelMapForWriting);
				this.logPatterns.AddRange(profile.LogPatterns);
				this.logStringEncodingForReadingComboBox.SelectedItem = profile.LogStringEncodingForReading;
				this.logStringEncodingForWritingComboBox.SelectedItem = profile.LogStringEncodingForWriting;
				this.logWritingFormats.AddAll(profile.LogWritingFormats);
				this.nameTextBox.Text = profile.Name;
				this.rawLogLevelPropertyComboBox.SelectedItem = profile.RawLogLevelPropertyName;
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
					Message = this.GetResourceObservable("String/LogProfileEditorDialog.LearnAboutLogsReadingAndParsingFirst"),
					Title = this.GetObservable(TitleProperty),
				}.ShowDialog(this);
				if (this.IsOpened)
				{
					this.PersistentState.SetValue<bool>(HasLearnAboutLogsReadingAndParsingHintShown, true);
					if (result == MessageDialogResult.Yes)
						Platform.OpenLink(LogsReadingAndParsingPageUri);
				}
			}
		}


		// Called when stte of product changed.
		void OnProductStateChanged(IProductManager? productManager, string productId)
		{
			if (productManager != null && productId == Products.Professional)
			{
				this.SetValue<bool>(IsProVersionActivatedProperty, productManager.IsProductActivated(productId));
				this.InvalidateInput();
			}
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			// call base
			if (!base.OnValidateInput())
				return false;
			
			// check data source and options
			var isTemplate = this.isTemplateSwitch.IsChecked.GetValueOrDefault();
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			if (dataSourceProvider == null)
				return false;
			this.allowMultipleFilesPanel.IsVisible = dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName));
			if (!isTemplate)
			{
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
			}
			this.SetValue<bool>(IsValidDataSourceOptionsProperty, true);

			// check name
			if (string.IsNullOrEmpty(this.nameTextBox.Text))
				return false;
			
			// data pro-version only data source
			if (dataSourceProvider.IsProVersionOnly && !this.GetValue<bool>(IsProVersionActivatedProperty))
				return false;

			// ok
			return true;
		}


		// Open online documentation.
		void OpenDocumentation() =>
			Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs");


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


		// Remove log writing format.
		void RemoveLogWritingFormat(ListBoxItem item)
		{
			var index = this.logWritingFormats.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.logWritingFormats.RemoveAt(index);
			this.SelectListBoxItem(this.logWritingFormatListBox, -1);
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
				IsTemplate = this.isTemplateSwitch.IsChecked.GetValueOrDefault(),
				Options = this.dataSourceOptions,
			}.ShowDialog<LogDataSourceOptions?>(this);
			if (options != null)
			{
				this.dataSourceOptions = options.Value;
				this.InvalidateInput();
			}
		}


		/// <summary>
		/// Show dialog in non-blocking mode.
		/// </summary>
		/// <param name="parent">Parent window.</param>
		/// <param name="logProfile">Log profile to be edited.</param>
		public static void Show(Avalonia.Controls.Window? parent, LogProfile? logProfile)
		{
			if (logProfile != null && NonBlockingDialogs.TryGetValue(logProfile, out var dialog))
			{
				dialog.ActivateAndBringToFront();
				return;
			}
			dialog = new LogProfileEditorDialog()
			{
				LogProfile = logProfile,
			};
			if (logProfile != null)
				NonBlockingDialogs[logProfile] = dialog;
			dialog.Show(parent);
		} 


		// Show dialog to manage script log data source providers.
		void ShowScriptLogDataSourceProvidersDialog() =>
			_ = new ScriptLogDataSourceProvidersDialog().ShowDialog(this);


		// List of time span format to read logs.
		IList<string> TimeSpanFormatsForReading { get; }


		// List of timestamp format to read logs.
		IList<string> TimestampFormatsForReading { get; }


		// List of visible log properties.
		IList<LogProperty> VisibleLogProperties { get; }
	}
}
