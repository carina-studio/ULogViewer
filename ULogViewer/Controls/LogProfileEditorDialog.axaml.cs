using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

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
		public static readonly Uri LogsReadingAndParsingPageUri = new("https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs");


		// Static fields.
		static readonly Dictionary<LogProfile, LogProfileEditorDialog> NonBlockingDialogs = new();
		static readonly StyledProperty<bool> HasCooperativeLogAnalysisScriptSetProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("HasCooperativeLogAnalysisScriptSet");
		static readonly StyledProperty<bool> HasEmbeddedScriptLogDataSourceProviderProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("HasEmbeddedScriptLogDataSourceProvider");
		static readonly StyledProperty<bool> HasDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("HasDataSourceOptions");
		static readonly SettingKey<bool> HasLearnAboutLogsReadingAndParsingHintShown = new($"{nameof(LogProfileEditorDialog)}.{nameof(HasLearnAboutLogsReadingAndParsingHintShown)}");
		static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("IsProVersionActivated");
		static readonly StyledProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
		static readonly StyledProperty<bool> IsWorkingDirectorySupportedProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("IsWorkingDirectorySupported");
		

		readonly ToggleSwitch adminNeededSwitch;
		readonly Panel allowMultipleFilesPanel;
		readonly ToggleSwitch allowMultipleFilesSwitch;
		readonly ScrollViewer baseScrollViewer;
		readonly ComboBox colorIndicatorComboBox;
		readonly ToggleSwitch continuousReadingSwitch;
		LogAnalysisScriptSet? cooperativeLogAnalysisScriptSet;
		LogDataSourceOptions dataSourceOptions;
		readonly ComboBox dataSourceProviderComboBox;
		readonly ObservableList<ILogDataSourceProvider> dataSourceProviders = new();
		readonly TextBox descriptionTextBox;
		EmbeddedScriptLogDataSourceProvider? embeddedScriptLogDataSourceProvider;
		readonly LogProfileIconColorComboBox iconColorComboBox;
		readonly LogProfileIconComboBox iconComboBox;
		readonly ToggleSwitch isTemplateSwitch;
		readonly SortedObservableList<KeyValuePair<string, Logs.LogLevel>> logLevelMapEntriesForReading = new((x, y) => x.Key.CompareTo(y.Key));
		readonly SortedObservableList<KeyValuePair<Logs.LogLevel, string>> logLevelMapEntriesForWriting = new((x, y) => x.Key.CompareTo(y.Key));
		readonly AppSuite.Controls.ListBox logLevelMapForReadingListBox;
		readonly AppSuite.Controls.ListBox logLevelMapForWritingListBox;
		readonly AppSuite.Controls.ListBox logPatternListBox;
		readonly ObservableList<LogPattern> logPatterns = new();
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
		readonly ObservableList<string> timeSpanFormatsForReading = new();
		readonly AppSuite.Controls.ListBox timeSpanFormatsForReadingListBox;
		readonly ComboBox timestampCategoryGranularityComboBox;
		readonly ComboBox timestampEncodingForReadingComboBox;
		readonly TextBox timestampFormatForDisplayingTextBox;
		readonly TextBox timestampFormatForWritingTextBox;
		readonly ObservableList<string> timestampFormatsForReading = new();
		readonly AppSuite.Controls.ListBox timestampFormatsForReadingListBox;
		readonly AppSuite.Controls.ListBox visibleLogPropertyListBox;
		readonly ObservableList<LogProperty> visibleLogProperties = new();
		readonly ToggleSwitch workingDirNeededSwitch;


		/// <summary>
		/// Initialize new <see cref="LogProfileEditorDialog"/> instance.
		/// </summary>
		public LogProfileEditorDialog()
		{
			// setup properties
			this.DataSourceProviders = ListExtensions.AsReadOnly(this.dataSourceProviders);
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

			// create commands
			this.EditLogLevelMapEntryForReadingCommand = new Command<KeyValuePair<string, Logs.LogLevel>>(this.EditLogLevelMapEntryForReading);
			this.EditLogLevelMapEntryForWritingCommand = new Command<KeyValuePair<Logs.LogLevel, string>>(this.EditLogLevelMapEntryForWriting);
			this.EditLogPatternCommand = new Command<ListBoxItem>(this.EditLogPattern);
			this.EditLogWritingFormatCommand = new Command<ListBoxItem>(this.EditLogWritingFormat);
			this.EditTimeSpanFormatForReadingCommand = new Command<ListBoxItem>(this.EditTimeSpanFormatForReading);
			this.EditTimestampFormatForReadingCommand = new Command<ListBoxItem>(this.EditTimestampFormatForReading);
			this.EditVisibleLogPropertyCommand = new Command<ListBoxItem>(this.EditVisibleLogProperty);
			this.MoveLogPatternDownCommand = new Command<ListBoxItem>(this.MoveLogPatternDown);
			this.MoveLogPatternUpCommand = new Command<ListBoxItem>(this.MoveLogPatternUp);
			this.MoveLogWritingFormatDownCommand = new Command<ListBoxItem>(this.MoveLogWritingFormatDown);
			this.MoveLogWritingFormatUpCommand = new Command<ListBoxItem>(this.MoveLogWritingFormatUp);
			this.MoveVisibleLogPropertyDownCommand = new Command<ListBoxItem>(this.MoveVisibleLogPropertyDown);
			this.MoveVisibleLogPropertyUpCommand = new Command<ListBoxItem>(this.MoveVisibleLogPropertyUp);
			this.RemoveLogLevelMapEntryCommand = new Command<object>(this.RemoveLogLevelMapEntry);
			this.RemoveLogPatternCommand = new Command<ListBoxItem>(this.RemoveLogPattern);
			this.RemoveLogWritingFormatCommand = new Command<ListBoxItem>(this.RemoveLogWritingFormat);
			this.RemoveTimeSpanFormatForReadingCommand = new Command<ListBoxItem>(this.RemoveTimeSpanFormatForReading);
			this.RemoveTimestampFormatForReadingCommand = new Command<ListBoxItem>(this.RemoveTimestampFormatForReading);
			this.RemoveVisibleLogPropertyCommand = new Command<ListBoxItem>(this.RemoveVisibleLogProperty);

			// create syntax highlighting definition sets
			this.DateTimeFormatSyntaxHighlightingDefinitionSet = DateTimeFormatSyntaxHighlighting.CreateDefinitionSet(this.Application);
			this.LogWritingFormatSyntaxHighlightingDefinitionSet = StringInterpolationFormatSyntaxHighlighting.CreateDefinitionSet(this.Application);
			this.RegexSyntaxHighlightingDefinitionSet = RegexSyntaxHighlighting.CreateDefinitionSet(this.Application);
			this.TimeSpanFormatSyntaxHighlightingDefinitionSet = TimeSpanFormatSyntaxHighlighting.CreateDefinitionSet(this.Application);

			// initialize.
			AvaloniaXamlLoader.Load(this);

			// setup controls
			this.adminNeededSwitch = this.Get<ToggleSwitch>("adminNeededSwitch");
			this.allowMultipleFilesPanel = this.Get<Panel>(nameof(allowMultipleFilesPanel));
			this.allowMultipleFilesSwitch = this.allowMultipleFilesPanel.FindControl<ToggleSwitch>(nameof(allowMultipleFilesSwitch)).AsNonNull();
			this.baseScrollViewer = this.Get<ScrollViewer>("baseScrollViewer");
			this.colorIndicatorComboBox = this.Get<ComboBox>("colorIndicatorComboBox");
			this.continuousReadingSwitch = this.Get<ToggleSwitch>("continuousReadingSwitch");
			this.dataSourceProviderComboBox = this.Get<ComboBox>("dataSourceProviderComboBox").Also(it =>
			{
				it.GetObservable(ComboBox.SelectedItemProperty).Subscribe(item =>
					this.OnSelectedDataSourceChanged());
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

			// attach to log data source providers
			LogDataSourceProviders.All.Let(allProviders =>
			{
				this.dataSourceProviders.AddAll(allProviders);
				(allProviders as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += this.OnAllLogDataSourceProvidersChanged);
			});
		}


		/// <summary>
		/// Add log level map entry.
		/// </summary>
		public async void AddLogLevelMapEntryForReading()
		{
			var entry = (KeyValuePair<string, Logs.LogLevel>?)null;
			while (true)
			{
				entry = await new LogLevelMapEntryForReadingEditorDialog()
				{
					Entry = entry
				}.ShowDialog<KeyValuePair<string, Logs.LogLevel>?>(this);
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


		/// <summary>
		/// Add log level map entry.
		/// </summary>
		/// <returns></returns>
		public async void AddLogLevelMapEntryForWriting()
		{
			var entry = (KeyValuePair<Logs.LogLevel, string>?)null;
			while (true)
			{
				entry = await new LogLevelMapEntryForWritingEditorDialog()
				{
					Entry = entry
				}.ShowDialog<KeyValuePair<Logs.LogLevel, string>?>(this);
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


		/// <summary>
		/// Add log pattern.
		/// </summary>
		public async void AddLogPattern()
		{
			var logPattern = await new LogPatternEditorDialog().ShowDialog<LogPattern>(this);
			if (logPattern != null)
			{
				this.logPatterns.Add(logPattern);
				this.SelectListBoxItem(this.logPatternListBox, this.logPatterns.Count - 1);
			}
		}


		/// <summary>
		/// Add log writing format.
		/// </summary>
		public async void AddLogWritingFormat()
		{
			var format = await new LogWritingFormatEditorDialog().ShowDialog<string?>(this);
			if (format != null)
			{
				this.logWritingFormats.Add(format);
				this.SelectListBoxItem(this.logWritingFormatListBox, this.logWritingFormats.Count - 1);
			}
		}


		/// <summary>
		/// Add time span format for reading log.
		/// </summary>
		public async void AddTimeSpanFormatForReading()
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


		/// <summary>
		/// Add timestamp format for reading log.
		/// </summary>
		public async void AddTimestampFormatForReading()
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


		/// <summary>
		/// Add visible log property.
		/// </summary>
		public async void AddVisibleLogProperty()
		{
			var logProperty = await new VisibleLogPropertyEditorDialog().ShowDialog<LogProperty>(this);
			if (logProperty != null)
			{
				this.visibleLogProperties.Add(logProperty);
				this.SelectListBoxItem(this.visibleLogPropertyListBox, this.visibleLogProperties.Count - 1);
			}
		}


		/// <summary>
		/// Copy the given log pattern.
		/// </summary>
		/// <param name="parameter"><see cref="LogPattern"/> to be copied.</param>
		public async void CopyLogPattern(object? parameter)
		{
			if (parameter is not LogPattern logPattern)
				return;
			var newLogPattern = await new LogPatternEditorDialog()
			{
				LogPattern = logPattern,
			}.ShowDialog<LogPattern>(this);
			if (newLogPattern != null)
			{
				this.logPatterns.Add(newLogPattern);
				this.SelectListBoxItem(this.logPatternListBox, this.logPatterns.Count - 1);
			}
		}


		/// <summary>
		/// Create cooperative log analysis script.
		/// </summary>
		public async void CreateCooperativeLogAnalysisScript()
		{
			var scriptSet = await new LogAnalysisScriptSetEditorDialog()
			{
				IsEmbeddedScriptSet = true,
			}.ShowDialog<LogAnalysisScriptSet?>(this);
			if (scriptSet == null || this.IsClosed)
				return;
			this.cooperativeLogAnalysisScriptSet = scriptSet;
			this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, true);
		}


		/// <summary>
		/// Create log data source script.
		/// </summary>
		public async void CreateEmbeddedScriptLogDataSourceProvider()
		{
			var provider = await new ScriptLogDataSourceProviderEditorDialog()
			{
				IsEmbeddedProvider = true,
			}.ShowDialog<ScriptLogDataSourceProvider?>(this);
			if (provider == null || this.IsClosed)
				return;
			this.embeddedScriptLogDataSourceProvider = new(provider);
			this.dataSourceProviders.Add(provider);
			this.SetValue(HasEmbeddedScriptLogDataSourceProviderProperty, true);
		}


		/// <summary>
		/// Get list of available log data source providers.
		/// </summary>
		public IList<ILogDataSourceProvider> DataSourceProviders { get; }


		/// <summary>
		/// Definition set of date time format syntax highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet DateTimeFormatSyntaxHighlightingDefinitionSet { get; }


		/// <summary>
		/// Edit cooperative log analysis script.
		/// </summary>
		public async void EditCooperativeLogAnalysisScript()
		{
			var scriptSet = await new LogAnalysisScriptSetEditorDialog()
			{
				IsEmbeddedScriptSet = true,
				ScriptSetToEdit = this.cooperativeLogAnalysisScriptSet?.Let(it => new LogAnalysisScriptSet(it, "")),
			}.ShowDialog<LogAnalysisScriptSet?>(this);
			if (scriptSet == null || this.IsClosed)
				return;
			this.cooperativeLogAnalysisScriptSet = scriptSet;
		}


		/// <summary>
		/// Edit embedded log data source script.
		/// </summary>
		public async void EditEmbeddedScriptLogDataSourceProvider()
		{
			var provider = await new ScriptLogDataSourceProviderEditorDialog()
			{
				IsEmbeddedProvider = true,
				Provider = this.embeddedScriptLogDataSourceProvider,
			}.ShowDialog<ScriptLogDataSourceProvider?>(this);
			if (provider == null || this.IsClosed)
				return;
			if (this.dataSourceProviderComboBox.SelectedItem is EmbeddedScriptLogDataSourceProvider)
				this.OnSelectedDataSourceChanged();
		}


		// Edit log level map entry.
		async void EditLogLevelMapEntryForReading(KeyValuePair<string, Logs.LogLevel> entry)
		{
			var newEntry = (KeyValuePair<string, Logs.LogLevel>?)entry;
			while (true)
			{
				newEntry = await new LogLevelMapEntryForReadingEditorDialog()
				{
					Entry = newEntry
				}.ShowDialog<KeyValuePair<string, Logs.LogLevel>?>(this);
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


		/// <summary>
		/// Command to edit log level map entry.
		/// </summary>
		public ICommand EditLogLevelMapEntryForReadingCommand { get; }


		// Edit log level map entry.
		async void EditLogLevelMapEntryForWriting(KeyValuePair<Logs.LogLevel, string> entry)
		{
			var newEntry = (KeyValuePair<Logs.LogLevel, string>?)entry;
			while (true)
			{
				newEntry = await new LogLevelMapEntryForWritingEditorDialog()
				{
					Entry = newEntry
				}.ShowDialog<KeyValuePair<Logs.LogLevel, string>?>(this);
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


		/// <summary>
		/// Command to edit log level map entry.
		/// </summary>
		public ICommand EditLogLevelMapEntryForWritingCommand { get; }


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


		/// <summary>
		/// Command to edit log pattern.
		/// </summary>
		public ICommand EditLogPatternCommand { get; }


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


		/// <summary>
		/// Command to edit log writing format.
		/// </summary>
		public ICommand EditLogWritingFormatCommand { get; }


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


		/// <summary>
		/// Command to edit time span format for reading logs.
		/// </summary>
		public ICommand EditTimeSpanFormatForReadingCommand { get; }


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


		/// <summary>
		/// Command to edit timestamp format for reading logs.
		/// </summary>
		public ICommand EditTimestampFormatForReadingCommand { get; }


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


		/// <summary>
		/// Command to edit visible log property.
		/// </summary>
		public ICommand EditVisibleLogPropertyCommand { get; }


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
			logProfile.CooperativeLogAnalysisScriptSet = this.cooperativeLogAnalysisScriptSet;
			logProfile.DataSourceOptions = this.dataSourceOptions;
			logProfile.DataSourceProvider = (ILogDataSourceProvider)this.dataSourceProviderComboBox.SelectedItem.AsNonNull();
			logProfile.Description = this.descriptionTextBox.Text;
			logProfile.EmbeddedScriptLogDataSourceProvider = this.embeddedScriptLogDataSourceProvider;
			logProfile.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
			logProfile.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
			logProfile.IsAdministratorNeeded = this.adminNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsContinuousReading = this.continuousReadingSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsTemplate = isTemplate;
			logProfile.IsWorkingDirectoryNeeded = this.workingDirNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.LogLevelMapForReading = new Dictionary<string, Logs.LogLevel>(this.logLevelMapEntriesForReading);
			logProfile.LogLevelMapForWriting = new Dictionary<Logs.LogLevel, string>(this.logLevelMapEntriesForWriting);
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


		/// <summary>
		/// Import cooperative log analysis script from file.
		/// </summary>
		public async void ImportCooperativeLogAnalysisScriptFromFile()
		{
			// select file
			var fileName = (await this.StorageProvider.OpenFilePickerAsync(new()
			{
				FileTypeFilter = new[]
				{
					new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
					{
						Patterns = new[] { "*.json" }
					}
				}
			})).Let(it =>
			{
				if (it.Count == 1 && it[0].TryGetUri(out var uri))
					return uri.LocalPath;
				return null;
			});
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// try loading script set
			var scriptSet = await Global.RunOrDefaultAsync(async () => await LogAnalysisScriptSet.LoadAsync(this.Application, fileName));
			if (scriptSet == null)
			{
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.FailedToImportLogAnalysisRuleSet"));
					}),
				}.ShowDialog(this);
				return;
			}

			// edit script set and replace
			scriptSet = await new LogAnalysisScriptSetEditorDialog()
			{
				IsEmbeddedScriptSet = true,
				ScriptSetToEdit = scriptSet,
			}.ShowDialog<LogAnalysisScriptSet?>(this);
			if (scriptSet != null)
			{
				this.cooperativeLogAnalysisScriptSet = scriptSet;
				this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, true);
			}
		}


		/// <summary>
		/// Import embedded log data source script from file.
		/// </summary>
		public async void ImportEmbeddedScriptLogDataSourceProviderFromFile()
		{
			// select file
			var fileName = (await this.StorageProvider.OpenFilePickerAsync(new()
			{
				FileTypeFilter = new[]
				{
					new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
					{
						Patterns = new[] { "*.json" }
					}
				}
			})).Let(it =>
			{
				if (it.Count == 1 && it[0].TryGetUri(out var uri))
					return uri.LocalPath;
				return null;
			});
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// try loading provider
			var provider = await Global.RunOrDefaultAsync(async () => await ScriptLogDataSourceProvider.LoadAsync(this.Application, fileName));
			if (provider == null)
			{
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("ScriptLogDataSourceProvidersDialog.FailedToImportProvider"));
					}),
				}.ShowDialog(this);
				return;
			}

			// edit provider and replace
			provider = await new ScriptLogDataSourceProviderEditorDialog()
			{
				IsEmbeddedProvider = true,
				Provider = provider,
			}.ShowDialog<ScriptLogDataSourceProvider?>(this);
			if (provider != null)
			{
				provider = new EmbeddedScriptLogDataSourceProvider(provider);
				if (this.dataSourceProviderComboBox.SelectedItem is EmbeddedScriptLogDataSourceProvider)
				{
					var index = this.dataSourceProviderComboBox.SelectedIndex;
					this.dataSourceProviders[index] = provider;
					this.dataSourceProviderComboBox.SelectedIndex = index;
					this.InvalidateInput();
				}
				else
				{
					var index = this.dataSourceProviders.Count - 1;
					if (index >= 0 && this.dataSourceProviders[index] is EmbeddedScriptLogDataSourceProvider)
						this.dataSourceProviders[index] = provider;
					else
						this.dataSourceProviders.Add(provider);
					this.embeddedScriptLogDataSourceProvider = new(provider);
					this.SetValue(HasEmbeddedScriptLogDataSourceProviderProperty, true);
				}
			}
		}


		/// <summary>
		/// Import existing cooperative log analysis script.
		/// </summary>
		public async void ImportExistingCooperativeLogAnalysisScript()
		{
			// select script set
			var scriptSet = await new LogAnalysisScriptSetSelectionDialog().ShowDialog<LogAnalysisScriptSet?>(this);
			if (scriptSet == null)
				return;

			// edit script set and replace
			scriptSet = await new LogAnalysisScriptSetEditorDialog()
			{
				IsEmbeddedScriptSet = true,
				ScriptSetToEdit = new(scriptSet, ""),
			}.ShowDialog<LogAnalysisScriptSet?>(this);
			if (scriptSet != null)
			{
				this.cooperativeLogAnalysisScriptSet = scriptSet;
				this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, true);
			}
		}


		/// <summary>
		/// Import existing embedded log data source script.
		/// </summary>
		public async void ImportExistingEmbeddedScriptLogDataSourceProvider()
		{
			// select provider
			var provider = await new ScriptLogDataSourceProviderSelectionDialog().ShowDialog<ScriptLogDataSourceProvider>(this);
			if (provider == null)
				return;
			
			// edit provider and replace
			provider = await new ScriptLogDataSourceProviderEditorDialog()
			{
				IsEmbeddedProvider = true,
				Provider = new EmbeddedScriptLogDataSourceProvider(provider),
			}.ShowDialog<ScriptLogDataSourceProvider?>(this);
			if (provider != null)
			{
				if (provider is not EmbeddedScriptLogDataSourceProvider)
					provider = new EmbeddedScriptLogDataSourceProvider(provider);
				if (this.dataSourceProviderComboBox.SelectedItem is EmbeddedScriptLogDataSourceProvider)
				{
					var index = this.dataSourceProviderComboBox.SelectedIndex;
					this.dataSourceProviders[index] = provider;
					this.dataSourceProviderComboBox.SelectedIndex = index;
					this.InvalidateInput();
				}
				else
				{
					var index = this.dataSourceProviders.Count - 1;
					if (index >= 0 && this.dataSourceProviders[index] is EmbeddedScriptLogDataSourceProvider)
						this.dataSourceProviders[index] = provider;
					else
						this.dataSourceProviders.Add(provider);
					this.embeddedScriptLogDataSourceProvider = new(provider);
					this.SetValue(HasEmbeddedScriptLogDataSourceProviderProperty, true);
				}
			}
		}


		// Check whether log data source options is valid or not.
		bool IsValidDataSourceOptions { get => this.GetValue<bool>(IsValidDataSourceOptionsProperty); }


		/// <summary>
		/// Entries of log level map.
		/// </summary>
		public IList<KeyValuePair<string, Logs.LogLevel>> LogLevelMapEntriesForReading { get; }


		/// <summary>
		/// Entries of log level map.
		/// </summary>
		public IList<KeyValuePair<Logs.LogLevel, string>> LogLevelMapEntriesForWriting { get; }


		/// <summary>
		/// Log patterns.
		/// </summary>
		public IList<LogPattern> LogPatterns { get; }


		/// <summary>
		/// Get or set <see cref="LogProfile"/> to be edited.
		/// </summary>
		public LogProfile? LogProfile { get; set; }


		// Log writing formats.
		public IList<string> LogWritingFormats { get; }


		/// <summary>
		/// Definition set of log writing format syntax highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet LogWritingFormatSyntaxHighlightingDefinitionSet { get; }


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


		/// <summary>
		/// Command to move log pattern down.
		/// </summary>
		public ICommand MoveLogPatternDownCommand { get; }


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


		/// <summary>
		/// Command to move log pattern up.
		/// </summary>
		public ICommand MoveLogPatternUpCommand { get; }


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


		/// <summary>
		/// Command to move log writing format down.
		/// </summary>
		public ICommand MoveLogWritingFormatDownCommand { get; }


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


		/// <summary>
		/// Command to move log writing format up.
		/// </summary>
		public ICommand MoveLogWritingFormatUpCommand { get; }


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


		/// <summary>
		/// Command to move visible log property down.
		/// </summary>
		public ICommand MoveVisibleLogPropertyDownCommand { get; }


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


		/// <summary>
		/// Command to move visible log property up.
		/// </summary>
		public ICommand MoveVisibleLogPropertyUpCommand { get; }


		// Called when list of all log data source providers changed.
		void OnAllLogDataSourceProvidersChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					e.NewItems!.Cast<ILogDataSourceProvider>().Let(it => this.dataSourceProviders.InsertRange(e.NewStartingIndex, it));
					break;
				case NotifyCollectionChangedAction.Remove:
					e.OldItems!.Cast<ILogDataSourceProvider>().Let(it => 
					{
						if (it.Contains(this.dataSourceProviderComboBox.SelectedItem))
							this.SelectDefaultDataSource();
						this.dataSourceProviders.RemoveRange(e.OldStartingIndex, it.Count);
					});
					break;
				default:
#if DEBUG
					throw new NotSupportedException("Unsupported change of log data source providers: " + e.Action);
#else
					this.Logger.LogError("Unsupported change of log data source providers: {action}", e.Action);
					break;
#endif
			}
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

			// detach from log data source providers
			(LogDataSourceProviders.All as INotifyCollectionChanged)?.Let(it => it.CollectionChanged -= this.OnAllLogDataSourceProvidersChanged);

			// call base
			base.OnClosed(e);
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
				this.EditLogLevelMapEntryForReading((KeyValuePair<string, Logs.LogLevel>)e.Item);
			else if (listBox == this.logLevelMapForWritingListBox)
				this.EditLogLevelMapEntryForWriting((KeyValuePair<Logs.LogLevel, string>)e.Item);
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
			listBox.SelectedItems?.Clear();
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
				this.SelectDefaultDataSource();
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
				this.cooperativeLogAnalysisScriptSet = profile.CooperativeLogAnalysisScriptSet;
				this.dataSourceOptions = profile.DataSourceOptions;
				this.descriptionTextBox.Text = profile.Description;
				this.embeddedScriptLogDataSourceProvider = profile.EmbeddedScriptLogDataSourceProvider;
				if (this.embeddedScriptLogDataSourceProvider != null)
					this.dataSourceProviders.Add(this.embeddedScriptLogDataSourceProvider);
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
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
			
			// update state
			this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, this.cooperativeLogAnalysisScriptSet != null);
			this.SetValue(HasEmbeddedScriptLogDataSourceProviderProperty, this.embeddedScriptLogDataSourceProvider != null);

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
					else
						this.nameTextBox.Focus();
				}
			}

			// setup initial focus
			this.SynchronizationContext.Post(this.nameTextBox.Focus);
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


		// Called when selected log data source provider changed.
		void OnSelectedDataSourceChanged()
		{
			if (this.dataSourceProviderComboBox?.SelectedItem is not ILogDataSourceProvider provider)
				return;
			this.SetValue<bool>(HasDataSourceOptionsProperty, provider.SupportedSourceOptions.IsNotEmpty());
			this.SetValue<bool>(IsWorkingDirectorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory))
				&& !provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory)));
			this.InvalidateInput();
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
						case nameof(LogDataSourceOptions.ConnectionString):
						case nameof(LogDataSourceOptions.Password):
						case nameof(LogDataSourceOptions.QueryString):
						case nameof(LogDataSourceOptions.UserName):
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


		/// <summary>
		/// Open online documentation.
		/// </summary>
#pragma warning disable CA1822
		public void OpenDocumentation() =>
			Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs");
#pragma warning restore CA1822


		/// <summary>
		/// Definition set of regex syntax highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet RegexSyntaxHighlightingDefinitionSet { get; }


		/// <summary>
		/// Remove cooperative log analysis script.
		/// </summary>
		public async void RemoveCooperativeLogAnalysisScript()
		{
			if (this.cooperativeLogAnalysisScriptSet == null)
				return;
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				DefaultResult = MessageDialogResult.No,
				Icon = MessageDialogIcon.Question,
				Message = this.Application.GetObservableString("LogProfileEditorDialog.CooperativeLogAnalysisScriptSet.ConfirmDeletion"),
			}.ShowDialog(this);
			if (result == MessageDialogResult.Yes)
			{
				this.cooperativeLogAnalysisScriptSet = null;
				this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, false);
			}
		}


		/// <summary>
		/// Remove embedded log data source script.
		/// </summary>
		public async void RemoveEmbeddedScriptLogDataSourceProvider()
		{
			if (this.embeddedScriptLogDataSourceProvider == null)
				return;
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				DefaultResult = MessageDialogResult.No,
				Icon = MessageDialogIcon.Question,
				Message = this.Application.GetObservableString("LogProfileEditorDialog.EmbeddedScriptLogDataSourceProvider.ConfirmDeletion"),
			}.ShowDialog(this);
			if (result == MessageDialogResult.Yes)
			{
				if (this.dataSourceProviderComboBox.SelectedItem == this.embeddedScriptLogDataSourceProvider)
					this.SelectDefaultDataSource();
				this.dataSourceProviders.Remove(this.embeddedScriptLogDataSourceProvider);
				this.embeddedScriptLogDataSourceProvider = null;
				this.SetValue(HasEmbeddedScriptLogDataSourceProviderProperty, false);
			}
		}


		// Remove log level map entry.
		void RemoveLogLevelMapEntry(object entry)
		{
			if (entry is KeyValuePair<string, Logs.LogLevel> readingEntry)
			{
				this.logLevelMapEntriesForReading.Remove(readingEntry);
				this.SelectListBoxItem(this.logLevelMapForReadingListBox, -1);
			}
			else if (entry is KeyValuePair<Logs.LogLevel, string> writingEntry)
			{
				this.logLevelMapEntriesForWriting.Remove(writingEntry);
				this.SelectListBoxItem(this.logLevelMapForWritingListBox, -1);
			}
		}


		/// <summary>
		/// Command to remove log level map entry.
		/// </summary>
		public ICommand RemoveLogLevelMapEntryCommand { get; }


		// Remove log pattern.
		void RemoveLogPattern(ListBoxItem item)
		{
			var index = this.logPatterns.IndexOf((LogPattern)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.logPatterns.RemoveAt(index);
			this.SelectListBoxItem(this.logPatternListBox, -1);
		}


		/// <summary>
		/// Command to remove log pattern.
		/// </summary>
		public ICommand RemoveLogPatternCommand { get; }


		// Remove log writing format.
		void RemoveLogWritingFormat(ListBoxItem item)
		{
			var index = this.logWritingFormats.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.logWritingFormats.RemoveAt(index);
			this.SelectListBoxItem(this.logWritingFormatListBox, -1);
		}


		/// <summary>
		/// Command to remove log writing format.
		/// </summary>
		public ICommand RemoveLogWritingFormatCommand { get; }


		// Remove time span format for reading.
		void RemoveTimeSpanFormatForReading(ListBoxItem item)
		{
			var index = this.TimeSpanFormatsForReading.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.timeSpanFormatsForReading.RemoveAt(index);
			this.SelectListBoxItem(this.timeSpanFormatsForReadingListBox, -1);
		}


		/// <summary>
		/// Command to remove log time span format for reading.
		/// </summary>
		public ICommand RemoveTimeSpanFormatForReadingCommand { get; }


		// Remove timestamp format for reading.
		void RemoveTimestampFormatForReading(ListBoxItem item)
		{
			var index = this.timestampFormatsForReading.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.timestampFormatsForReading.RemoveAt(index);
			this.SelectListBoxItem(this.timestampFormatsForReadingListBox, -1);
		}


		/// <summary>
		/// Command to remove timestamp format for reading.
		/// </summary>
		public ICommand RemoveTimestampFormatForReadingCommand { get; }


		// Remove visible log property.
		void RemoveVisibleLogProperty(ListBoxItem item)
		{
			var index = this.visibleLogProperties.IndexOf((LogProperty)item.DataContext.AsNonNull());
			if (index < 0)
				return;
			this.visibleLogProperties.RemoveAt(index);
			this.SelectListBoxItem(this.visibleLogPropertyListBox, -1);
		}


		/// <summary>
		/// Command to remove visible log property.
		/// </summary>
		public ICommand RemoveVisibleLogPropertyCommand { get; }


		// Select default log data source provider.
		void SelectDefaultDataSource() =>
			this.dataSourceProviderComboBox.SelectedItem = this.dataSourceProviders.FirstOrDefault(it => it is FileLogDataSourceProvider);


		// Select given item in list box.
		void SelectListBoxItem(Avalonia.Controls.ListBox listBox, int index)
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


		/// <summary>
		/// Set log data source options.
		/// </summary>
		public async void SetDataSourceOptions()
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
			if (parent != null)
				dialog.Show(parent);
			else
				dialog.Show();
		} 


		/// <summary>
		/// Show dialog to manage script log data source providers.
		/// </summary>
		public void ShowScriptLogDataSourceProvidersDialog() =>
			_ = new ScriptLogDataSourceProvidersDialog().ShowDialog(this);


		/// <summary>
		/// List of time span format to read logs.
		/// </summary>
		public IList<string> TimeSpanFormatsForReading { get; }


		/// <summary>
		/// Definition set of time span format syntax highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet TimeSpanFormatSyntaxHighlightingDefinitionSet { get; }


		/// <summary>
		/// List of timestamp format to read logs.
		/// </summary>
		public IList<string> TimestampFormatsForReading { get; }


		/// <summary>
		/// List of visible log properties.
		/// </summary>
		public IList<LogProperty> VisibleLogProperties { get; }
	}
}
