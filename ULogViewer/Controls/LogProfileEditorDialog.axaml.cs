using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.AppSuite.Converters;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
#if !DEBUG
using CarinaStudio.Logging;
#endif
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using ListBox = Avalonia.Controls.ListBox;

// ReSharper disable RedundantNameQualifier
namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="LogProfile"/>.
/// </summary>
class LogProfileEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert <see cref="LogChartType"/> to readable name.
	/// </summary>
	public static readonly IValueConverter LogChartTypeNameConverter = new EnumConverter(CarinaStudio.Application.CurrentOrNull, typeof(LogChartType));
	/// <summary>
	/// <see cref="IValueConverter"/> to convert <see cref="Logs.LogLevel"/> to readable name.
	/// </summary>
	public static readonly IValueConverter LogLevelNameConverter = EnumConverters.LogLevel;
	

	// Static fields.
	static readonly SettingKey<bool> DoNotShowDialogForLogPatternsWithoutLogLevelMapKey = new("LogProfileEditorDialog.DoNotShowDialogForLogPatternsWithoutLogLevelMap", false);
	static readonly StyledProperty<bool> HasCooperativeLogAnalysisScriptSetProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("HasCooperativeLogAnalysisScriptSet");
	static readonly StyledProperty<bool> HasEmbeddedScriptLogDataSourceProviderProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("HasEmbeddedScriptLogDataSourceProvider");
	static readonly StyledProperty<bool> HasDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(HasDataSourceOptions));
	static readonly SettingKey<bool> HasLearnAboutLogsReadingAndParsingHintShown = new($"{nameof(LogProfileEditorDialog)}.{nameof(HasLearnAboutLogsReadingAndParsingHintShown)}");
	static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsProVersionActivated));
	static readonly StyledProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
	static readonly StyledProperty<bool> IsWorkingDirectorySupportedProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>("IsWorkingDirectorySupported");
	static readonly Dictionary<LogProfile, LogProfileEditorDialog> NonBlockingDialogs = new();
	

	// Fields.
	readonly ToggleSwitch adminNeededSwitch;
	readonly Panel allowMultipleFilesPanel;
	readonly ToggleSwitch allowMultipleFilesSwitch;
	readonly ScrollViewer baseScrollViewer;
	readonly MutableObservableBoolean canAddLogChartSeriesSource = new(true);
	readonly ComboBox colorIndicatorComboBox;
	readonly Panel commonPanel;
	readonly ToggleButton commonPanelButton;
	readonly ToggleSwitch continuousReadingSwitch;
	LogAnalysisScriptSet? cooperativeLogAnalysisScriptSet;
	LogDataSourceOptions dataSourceOptions;
	readonly ComboBox dataSourceProviderComboBox;
	readonly ObservableList<ILogDataSourceProvider> dataSourceProviders = new();
	readonly ComboBox defaultLogLevelComboBox;
	readonly TextBox descriptionTextBox;
	EmbeddedScriptLogDataSourceProvider? embeddedScriptLogDataSourceProvider;
	readonly LogProfileIconColorComboBox iconColorComboBox;
	readonly LogProfileIconComboBox iconComboBox;
	readonly ToggleSwitch isTemplateSwitch;
	readonly Panel logDisplayingPanel;
		readonly ToggleButton logDisplayingPanelButton;
		readonly SortedObservableList<KeyValuePair<string, Logs.LogLevel>> logLevelMapEntriesForReading = new((x, y) => string.Compare(x.Key, y.Key, StringComparison.InvariantCulture));
		readonly SortedObservableList<KeyValuePair<Logs.LogLevel, string>> logLevelMapEntriesForWriting = new((x, y) => x.Key.CompareTo(y.Key));
		readonly AppSuite.Controls.ListBox logLevelMapForReadingListBox;
		readonly AppSuite.Controls.ListBox logLevelMapForWritingListBox;
		readonly AppSuite.Controls.ListBox logPatternListBox;
		readonly ComboBox logPatternMatchingModeComboBox;
		readonly ObservableList<LogPattern> logPatterns = new();
		readonly HashSet<string> logPropertyNamesInLogPatterns = new();
		readonly Panel logReadingPanel;
		readonly ToggleButton logReadingPanelButton;
		readonly ComboBox logStringEncodingForReadingComboBox;
		readonly ComboBox logStringEncodingForWritingComboBox;
		readonly Avalonia.Controls.ListBox logWritingFormatListBox;
		readonly ObservableList<string> logWritingFormats = new();
		readonly Panel logWritingPanel;
		readonly ToggleButton logWritingPanelButton;
		readonly TextBox nameTextBox;
		readonly ComboBox rawLogLevelPropertyComboBox;
		readonly IntegerTextBox restartReadingDelayTextBox;
	readonly ComboBox sortDirectionComboBox;
	readonly ComboBox sortKeyComboBox;
	readonly ComboBox timeSpanCultureInfoForReadingComboBox;
	readonly ComboBox timeSpanCultureInfoForWritingComboBox;
	readonly ComboBox timeSpanEncodingForReadingComboBox;
	readonly TextBox timeSpanFormatForDisplayingTextBox;
	readonly TextBox timeSpanFormatForWritingTextBox;
	readonly ObservableList<string> timeSpanFormatsForReading = new();
	readonly AppSuite.Controls.ListBox timeSpanFormatsForReadingListBox;
	readonly ComboBox timestampCategoryGranularityComboBox;
	readonly ComboBox timestampCultureInfoForReadingComboBox;
	readonly ComboBox timestampCultureInfoForWritingComboBox;
	readonly ComboBox timestampEncodingForReadingComboBox;
	readonly TextBox timestampFormatForDisplayingTextBox;
	readonly TextBox timestampFormatForWritingTextBox;
	readonly ObservableList<string> timestampFormatsForReading = new();
	readonly AppSuite.Controls.ListBox timestampFormatsForReadingListBox;
	readonly AppSuite.Controls.ListBox visibleLogPropertyListBox;
	readonly ObservableList<LogProperty> visibleLogProperties = new();
	readonly ComboBox workingDirPriorityComboBox;


		/// <summary>
		/// Initialize new <see cref="LogProfileEditorDialog"/> instance.
		/// </summary>
		public LogProfileEditorDialog()
		{
			// setup properties
			this.DataSourceProviders = ListExtensions.AsReadOnly(this.dataSourceProviders);
			this.HasNavigationBar = true;
			this.LogLevelMapEntriesForReading = ListExtensions.AsReadOnly(this.logLevelMapEntriesForReading);
			this.LogLevelMapEntriesForWriting = ListExtensions.AsReadOnly(this.logLevelMapEntriesForWriting);
			this.LogPatterns = ListExtensions.AsReadOnly(this.logPatterns.Also(it =>
			{
				it.CollectionChanged += (_, _) => this.InvalidateInput();
			}));
			this.LogWritingFormats = ListExtensions.AsReadOnly(this.logWritingFormats);
			this.TimeSpanFormatsForReading = ListExtensions.AsReadOnly(this.timeSpanFormatsForReading);
			this.TimestampFormatsForReading = ListExtensions.AsReadOnly(this.timestampFormatsForReading);
			this.VisibleLogProperties = ListExtensions.AsReadOnly(this.visibleLogProperties.Also(it =>
			{
				it.CollectionChanged += (_, _) => this.InvalidateInput();
			}));

		// create commands
		this.AddLogChartSeriesSourceCommand = new Command(this.AddLogChartSeriesSource, this.canAddLogChartSeriesSource);
		this.EditLogChartSeriesSourceCommand = new Command<ListBoxItem>(this.EditLogChartSeriesSource);
		this.EditLogLevelMapEntryForReadingCommand = new Command<KeyValuePair<string, Logs.LogLevel>>(this.EditLogLevelMapEntryForReading);
		this.EditLogLevelMapEntryForWritingCommand = new Command<KeyValuePair<Logs.LogLevel, string>>(this.EditLogLevelMapEntryForWriting);
		this.EditLogPatternCommand = new Command<ListBoxItem>(this.EditLogPattern);
		this.EditLogWritingFormatCommand = new Command<ListBoxItem>(this.EditLogWritingFormat);
		this.EditTimeSpanFormatForReadingCommand = new Command<ListBoxItem>(this.EditTimeSpanFormatForReading);
		this.EditTimestampFormatForReadingCommand = new Command<ListBoxItem>(this.EditTimestampFormatForReading);
		this.EditVisibleLogPropertyCommand = new Command<ListBoxItem>(this.EditVisibleLogProperty);
		this.RemoveLogChartSeriesSourceCommand = new Command<ListBoxItem>(this.RemoveLogChartSeriesSource);
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
		
		// prepare function to setup item dragging
		void SetupItemDragging<T>(CarinaStudio.AppSuite.Controls.ListBox listBox, ObservableList<T> items)
		{
			ListBoxItemDragging.SetItemDraggingEnabled(listBox, true);
			listBox.AddHandler(ListBoxItemDragging.ItemDragStartedEvent, (_, e) => this.OnListBoxItemDragStarted(listBox, e));
			listBox.AddHandler(ListBoxItemDragging.ItemDroppedEvent, (_, e) => this.OnListBoxItemDropped(listBox, items, e));
		}

		// setup controls
		this.adminNeededSwitch = this.Get<ToggleSwitch>("adminNeededSwitch");
		this.allowMultipleFilesPanel = this.Get<Panel>(nameof(allowMultipleFilesPanel));
		this.allowMultipleFilesSwitch = this.allowMultipleFilesPanel.FindControl<ToggleSwitch>(nameof(allowMultipleFilesSwitch)).AsNonNull();
		this.baseScrollViewer = this.Get<ScrollViewer>(nameof(baseScrollViewer)).Also(it =>
		{
			it.GetObservable(ScrollViewer.OffsetProperty).Subscribe(this.InvalidateNavigationBar);
			it.GetObservable(ScrollViewer.ViewportProperty).Subscribe(this.InvalidateNavigationBar);
		});
		this.colorIndicatorComboBox = this.Get<ComboBox>("colorIndicatorComboBox");
		this.commonPanel = this.Get<Panel>(nameof(commonPanel));
		this.commonPanelButton = this.Get<ToggleButton>(nameof(commonPanelButton)).Also(it =>
		{
			it.Click += (_, _) => this.ScrollToPanel(it);
		});
		this.continuousReadingSwitch = this.Get<ToggleSwitch>("continuousReadingSwitch");
			this.dataSourceProviderComboBox = this.Get<ComboBox>("dataSourceProviderComboBox").Also(it =>
			{
				it.GetObservable(SelectingItemsControl.SelectedItemProperty).Subscribe(this.OnSelectedDataSourceChanged);
			});
			this.defaultLogLevelComboBox = this.Get<ComboBox>(nameof(defaultLogLevelComboBox));
			this.descriptionTextBox = this.Get<TextBox>(nameof(descriptionTextBox));
			this.iconColorComboBox = this.Get<LogProfileIconColorComboBox>(nameof(iconColorComboBox));
			this.iconComboBox = this.Get<LogProfileIconComboBox>("iconComboBox");
			if (Platform.IsNotWindows)
				this.Get<Control>("isAdminNeededPanel").IsVisible = false;
			this.isTemplateSwitch = this.Get<ToggleSwitch>(nameof(isTemplateSwitch)).Also(it =>
			{
				it.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.logDisplayingPanel = this.Get<Panel>(nameof(logDisplayingPanel));
			this.logDisplayingPanelButton = this.Get<ToggleButton>(nameof(logDisplayingPanelButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToPanel(it);
			});
			this.logLevelMapForReadingListBox = this.Get<AppSuite.Controls.ListBox>("logLevelMapForReadingListBox");
			this.logLevelMapForWritingListBox = this.Get<AppSuite.Controls.ListBox>("logLevelMapForWritingListBox");
			this.logPatternListBox = this.Get<AppSuite.Controls.ListBox>(nameof(logPatternListBox)).Also(it =>
			{
				SetupItemDragging(it, this.logPatterns);
			});
			this.logPatternMatchingModeComboBox = this.Get<ComboBox>(nameof(logPatternMatchingModeComboBox));
			this.logReadingPanel = this.Get<Panel>(nameof(logReadingPanel));
			this.logReadingPanelButton = this.Get<ToggleButton>(nameof(logReadingPanelButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToPanel(it);
			});
			this.logStringEncodingForReadingComboBox = this.Get<ComboBox>("logStringEncodingForReadingComboBox");
			this.logStringEncodingForWritingComboBox = this.Get<ComboBox>("logStringEncodingForWritingComboBox");
			this.logWritingFormatListBox = this.Get<CarinaStudio.AppSuite.Controls.ListBox>(nameof(logWritingFormatListBox)).Also(it =>
			{
				SetupItemDragging(it, this.logWritingFormats);
			});
			this.logWritingPanel = this.Get<Panel>(nameof(logWritingPanel));
			this.logWritingPanelButton = this.Get<ToggleButton>(nameof(logWritingPanelButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToPanel(it);
		});
		this.nameTextBox = this.Get<TextBox>("nameTextBox");
		this.rawLogLevelPropertyComboBox = this.Get<ComboBox>(nameof(rawLogLevelPropertyComboBox));
		this.restartReadingDelayTextBox = this.Get<IntegerTextBox>(nameof(restartReadingDelayTextBox));
		this.sortDirectionComboBox = this.Get<ComboBox>("sortDirectionComboBox");
		this.sortKeyComboBox = this.Get<ComboBox>("sortKeyComboBox");
		this.timeSpanCultureInfoForReadingComboBox = this.Get<ComboBox>(nameof(timeSpanCultureInfoForReadingComboBox)).Also(it =>
		{
			it.SelectionChanged += (_, e) =>
			{
				if (this.IsOpened && e.RemovedItems.Count > 0 && e.RemovedItems[0]?.Equals(this.timeSpanCultureInfoForWritingComboBox?.SelectedItem) == true)
					this.timeSpanCultureInfoForWritingComboBox!.SelectedItem = it.SelectedItem;
			};
		});
		this.timeSpanCultureInfoForWritingComboBox = this.Get<ComboBox>(nameof(timeSpanCultureInfoForWritingComboBox));
		this.timeSpanEncodingForReadingComboBox = this.Get<ComboBox>(nameof(timeSpanEncodingForReadingComboBox));
		this.timeSpanFormatForDisplayingTextBox = this.Get<TextBox>(nameof(timeSpanFormatForDisplayingTextBox));
		this.timeSpanFormatForWritingTextBox = this.Get<TextBox>(nameof(timeSpanFormatForWritingTextBox));
		this.timeSpanFormatsForReadingListBox = this.Get<AppSuite.Controls.ListBox>(nameof(timeSpanFormatsForReadingListBox));
		this.timestampCategoryGranularityComboBox = this.Get<ComboBox>(nameof(timestampCategoryGranularityComboBox));
		this.timestampCultureInfoForReadingComboBox = this.Get<ComboBox>(nameof(timestampCultureInfoForReadingComboBox)).Also(it =>
		{
			it.SelectionChanged += (_, e) =>
			{
				if (this.IsOpened && e.RemovedItems.Count > 0 && e.RemovedItems[0]?.Equals(this.timestampCultureInfoForWritingComboBox?.SelectedItem) == true)
					this.timestampCultureInfoForWritingComboBox!.SelectedItem = it.SelectedItem;
			};
		});
		this.timestampCultureInfoForWritingComboBox = this.Get<ComboBox>(nameof(timestampCultureInfoForWritingComboBox));
		this.timestampEncodingForReadingComboBox = this.Get<ComboBox>(nameof(timestampEncodingForReadingComboBox));
		this.timestampFormatForDisplayingTextBox = this.Get<TextBox>("timestampFormatForDisplayingTextBox");
		this.timestampFormatForWritingTextBox = this.Get<TextBox>("timestampFormatForWritingTextBox");
		this.timestampFormatsForReadingListBox = this.Get<AppSuite.Controls.ListBox>(nameof(timestampFormatsForReadingListBox));
		this.visibleLogPropertyListBox = this.Get<AppSuite.Controls.ListBox>(nameof(visibleLogPropertyListBox)).Also(it =>
		{
			SetupItemDragging(it, this.visibleLogProperties);
		});
		this.workingDirPriorityComboBox = this.Get<ComboBox>(nameof(workingDirPriorityComboBox));

		// attach to log data source providers
		LogDataSourceProviders.All.Let(allProviders =>
		{
			this.dataSourceProviders.AddAll(allProviders);
			(allProviders as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += this.OnAllLogDataSourceProvidersChanged);
		});
	}
	
	
	// Add source of log chart series.
	Task AddLogChartSeriesSource()
		{
			return Task.CompletedTask;
	}
	
	
	/// <summary>
	/// Command to add source of log chart series.
	/// </summary>
	public ICommand AddLogChartSeriesSourceCommand { get; }


	/// <summary>
	/// Add log level map entry.
	/// </summary>
	public async Task AddLogLevelMapEntryForReading()
	{
		var entry = (KeyValuePair<string, Logs.LogLevel>?)null;
		while (true)
		{
			entry = await new LogLevelMapEntryForReadingEditorDialog
			{
				Entry = entry
			}.ShowDialog<KeyValuePair<string, Logs.LogLevel>?>(this);
			if (entry is null)
				return;
			if (this.logLevelMapEntriesForReading.Contains(entry.Value))
			{
				await new MessageDialog
				{
					Icon = MessageDialogIcon.Warning,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = entry.Value.Key;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileEditorDialog.DuplicateLogLevelMapEntry"));
					}),
					Title = this.GetResourceObservable("String/LogProfileEditorDialog.LogLevelMapForReading"),
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
	public async Task AddLogLevelMapEntryForWriting()
	{
		var entry = (KeyValuePair<Logs.LogLevel, string>?)null;
		while (true)
		{
			entry = await new LogLevelMapEntryForWritingEditorDialog
			{
				Entry = entry
			}.ShowDialog<KeyValuePair<Logs.LogLevel, string>?>(this);
			if (entry is null)
				return;
			if (this.logLevelMapEntriesForWriting.Contains(entry.Value))
			{
				await new MessageDialog
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
	public async Task AddLogPattern()
	{
		var logPattern = await new LogPatternEditorDialog().ShowDialog<LogPattern?>(this);
		if (logPattern is not null)
		{
			this.logPatterns.Add(logPattern);
			this.logPropertyNamesInLogPatterns.AddAll(logPattern.DefinedLogPropertyNames);
			this.SelectListBoxItem(this.logPatternListBox, this.logPatterns.Count - 1);
		}
	}


	/// <summary>
	/// Add log writing format.
	/// </summary>
	public async Task AddLogWritingFormat()
	{
		var format = await new LogWritingFormatEditorDialog().ShowDialog<string?>(this);
		if (format is not null)
		{
			this.logWritingFormats.Add(format);
			this.SelectListBoxItem(this.logWritingFormatListBox, this.logWritingFormats.Count - 1);
		}
	}


	/// <summary>
	/// Add time span format for reading log.
	/// </summary>
	public async Task AddTimeSpanFormatForReading()
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
	public async Task AddTimestampFormatForReading()
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
	public async Task AddVisibleLogProperty()
	{
		var logProperty = await new VisibleLogPropertyEditorDialog
		{
			DefinedLogPropertyNames = this.logPropertyNamesInLogPatterns,
		}.ShowDialog<LogProperty?>(this);
		if (logProperty is not null)
		{
			this.visibleLogProperties.Add(logProperty);
			this.SelectListBoxItem(this.visibleLogPropertyListBox, this.visibleLogProperties.Count - 1);
		}
	}


	/// <summary>
	/// Copy the given log pattern.
	/// </summary>
	/// <param name="parameter"><see cref="LogPattern"/> to be copied.</param>
	public async Task CopyLogPattern(object? parameter)
	{
		if (parameter is not LogPattern logPattern)
			return;
		var newLogPattern = await new LogPatternEditorDialog
		{
			LogPattern = logPattern,
		}.ShowDialog<LogPattern?>(this);
		if (newLogPattern is not null)
		{
			this.logPatterns.Add(newLogPattern);
			this.logPropertyNamesInLogPatterns.AddAll(newLogPattern.DefinedLogPropertyNames);
			this.SelectListBoxItem(this.logPatternListBox, this.logPatterns.Count - 1);
		}
	}


	/// <summary>
	/// Get list of available log data source providers.
	/// </summary>
	public IList<ILogDataSourceProvider> DataSourceProviders { get; }


	/// <summary>
	/// Definition set of date time format syntax highlighting.
	/// </summary>
	public SyntaxHighlightingDefinitionSet DateTimeFormatSyntaxHighlightingDefinitionSet { get; }
	
	
	// Edit source of log chart series.
	void EditLogChartSeriesSource(ListBoxItem item)
	{ }


	/// <summary>
	/// Command to edit source of log chart series.
	/// </summary>
	public ICommand EditLogChartSeriesSourceCommand { get; }


	// Edit log level map entry.
	async Task EditLogLevelMapEntryForReading(KeyValuePair<string, Logs.LogLevel> entry)
	{
		var newEntry = (KeyValuePair<string, Logs.LogLevel>?)entry;
		while (true)
		{
			newEntry = await new LogLevelMapEntryForReadingEditorDialog
			{
				Entry = newEntry
			}.ShowDialog<KeyValuePair<string, Logs.LogLevel>?>(this);
			if (newEntry is null || newEntry.Value.Equals(entry))
				return;
			var checkingEntry = this.logLevelMapEntriesForReading.FirstOrDefault(it => it.Key == newEntry.Value.Key);
			if (checkingEntry.Key is not null && !entry.Equals(checkingEntry))
			{
				await new MessageDialog
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
	async Task EditLogLevelMapEntryForWriting(KeyValuePair<Logs.LogLevel, string> entry)
	{
		var newEntry = (KeyValuePair<Logs.LogLevel, string>?)entry;
		while (true)
		{
			newEntry = await new LogLevelMapEntryForWritingEditorDialog
			{
				Entry = newEntry
			}.ShowDialog<KeyValuePair<Logs.LogLevel, string>?>(this);
			if (newEntry is null || newEntry.Value.Equals(entry))
				return;
			var checkingEntry = this.logLevelMapEntriesForWriting.FirstOrDefault(it => it.Key == newEntry.Value.Key);
			if (checkingEntry.Value is not null && !entry.Equals(checkingEntry))
			{
				await new MessageDialog
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
	async Task EditLogPattern(ListBoxItem item)
	{
		if (item.DataContext is not LogPattern logPattern)
			return;
		var index = this.logPatterns.IndexOf(logPattern);
		if (index < 0)
			return;
		var newLogPattern = await new LogPatternEditorDialog
		{
			LogPattern = logPattern
		}.ShowDialog<LogPattern?>(this);
		if (newLogPattern is not null && newLogPattern != logPattern)
		{
			this.logPatterns[index] = newLogPattern;
			this.UpdateLogPropertyNamesInLogPatterns();
			this.SelectListBoxItem(this.logPatternListBox, index);
		}
	}


	/// <summary>
	/// Command to edit log pattern.
	/// </summary>
	public ICommand EditLogPatternCommand { get; }


	// Edit log writing format.
	async Task EditLogWritingFormat(ListBoxItem item)
	{
		if (item.DataContext is not string format)
			return;
		var index = this.logWritingFormats.IndexOf(format);
		if (index < 0)
			return;
		var newFormat = await new LogWritingFormatEditorDialog
		{
			Format = format
		}.ShowDialog<string?>(this);
		if (newFormat is not null && newFormat != format)
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
	async Task EditTimeSpanFormatForReading(ListBoxItem item)
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
	async Task EditTimestampFormatForReading(ListBoxItem item)
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
	async Task EditVisibleLogProperty(ListBoxItem item)
	{
		if (item.DataContext is not LogProperty logProperty)
			return;
		var index = this.visibleLogProperties.IndexOf(logProperty);
		if (index < 0)
			return;
		var newLogProperty = await new VisibleLogPropertyEditorDialog
		{
			DefinedLogPropertyNames = this.logPropertyNamesInLogPatterns,
			LogProperty = logProperty
		}.ShowDialog<LogProperty?>(this);
		if (newLogProperty is not null && newLogProperty != logProperty)
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
		// check name
		if (string.IsNullOrEmpty(this.nameTextBox.Text))
		{
			this.Get<Control>("nameItem").Let(it =>
			{
				this.baseScrollViewer.ScrollIntoView(it, true);
				this.AnimateItem(it);
			});
			this.nameTextBox.Focus();
			return null;
		}
		
		// check data source and options
		if (!this.GetValue(IsValidDataSourceOptionsProperty))
		{
			this.Get<Control>("dataSourceProviderContainer").Let(it =>
			{
				this.baseScrollViewer.ScrollIntoView(it, true);
				this.AnimateItem(it);
			});
			this.dataSourceProviderComboBox.Focus();
			return null;
		}
		
		// data pro-version only data source
			if (this.dataSourceProviderComboBox.SelectionBoxItem is not ILogDataSourceProvider dataSourceProvider)
				return null;
			if (dataSourceProvider.IsProVersionOnly && !this.GetValue(IsProVersionActivatedProperty))
			{
				this.Get<Control>("dataSourceProviderItem").Let(it =>
				{
					this.baseScrollViewer.ScrollIntoView(it, true);
					this.AnimateItem(it);
				});
				this.dataSourceProviderComboBox.Focus();
				return null;
			}
			
			// check log level map for reading
			if (this.rawLogLevelPropertyComboBox.SelectedItem is string rawLogLevelProperty 
			    && this.logPropertyNamesInLogPatterns.Contains(rawLogLevelProperty) 
			    && this.logLevelMapEntriesForReading.IsEmpty()
			    && !this.Application.Configuration.GetValueOrDefault(DoNotShowDialogForLogPatternsWithoutLogLevelMapKey))
			{
				var dialog = new MessageDialog
				{
					Buttons = MessageDialogButtons.OK,
					DoNotAskOrShowAgain = false,
					Icon = MessageDialogIcon.Information,
					Message = this.GetResourceObservable("String/LogProfileEditorDialog.LogPatternsWithoutLogLevelMap"),
				};
				await dialog.ShowDialog(this);
				if (dialog.DoNotAskOrShowAgain == true)
					this.Application.Configuration.SetValue(DoNotShowDialogForLogPatternsWithoutLogLevelMapKey, true);
				await Task.Delay(300, CancellationToken.None); // [Workaround] Prevent crashing when closing two windows immediately on macOS.
			}
			
			// check log patterns and visible log properties
			var isTemplate = this.isTemplateSwitch.IsChecked.GetValueOrDefault();
			if (this.logPatterns.IsEmpty() && !isTemplate)
			{
				if (this.visibleLogProperties.IsNotEmpty())
				{
					var result = await new MessageDialog
					{
						Buttons = MessageDialogButtons.YesNo,
						DefaultResult = MessageDialogResult.Yes,
						Icon = MessageDialogIcon.Warning,
						Message = this.GetResourceObservable("String/LogProfileEditorDialog.VisibleLogPropertiesWithoutLogPatterns"),
					}.ShowDialog(this);
					if (result == MessageDialogResult.Yes)
					{
						this.SynchronizationContext.PostDelayed(() =>
						{
							this.Get<Control>("logPatternsContainer").Let(it =>
							{
								this.baseScrollViewer.ScrollIntoView(it, true);
								this.AnimateItem(it);
							});
							this.Get<Button>("addLogPatternButton").Focus();
						}, 100);
						return null;
					}
					await Task.Delay(300, CancellationToken.None); // [Workaround] Prevent crashing when closing two windows immediately on macOS.
				}
			}
			else if (this.visibleLogProperties.IsEmpty() && !isTemplate)
			{
				var result = await new MessageDialog
				{
					Buttons = MessageDialogButtons.YesNo,
					DefaultResult = MessageDialogResult.Yes,
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/LogProfileEditorDialog.LogPatternsWithoutVisibleLogProperties"),
				}.ShowDialog(this);
				if (result == MessageDialogResult.Yes)
				{
					this.SynchronizationContext.PostDelayed(() =>
					{
						this.Get<Control>("visibleLogPropertiesContainer").Let(it =>
						{
							this.baseScrollViewer.ScrollIntoView(it, true);
							this.AnimateItem(it);
						});
						this.Get<Button>("addVisibleLogPropertyButton").Focus();
					}, 100);
					return null;
				}
				await Task.Delay(300, CancellationToken.None); // [Workaround] Prevent crashing when closing two windows immediately on macOS.
			}

			// update log profile
			var logProfile = this.LogProfile ?? new LogProfile(this.Application);
			logProfile.AllowMultipleFiles = this.allowMultipleFilesSwitch.IsChecked.GetValueOrDefault();
			logProfile.ColorIndicator = (LogColorIndicator)this.colorIndicatorComboBox.SelectedItem.AsNonNull();
			logProfile.CooperativeLogAnalysisScriptSet = this.cooperativeLogAnalysisScriptSet;
			logProfile.DataSourceOptions = this.dataSourceOptions;
			logProfile.DataSourceProvider = (ILogDataSourceProvider)this.dataSourceProviderComboBox.SelectedItem.AsNonNull();
			logProfile.DefaultLogLevel = (Logs.LogLevel)this.defaultLogLevelComboBox.SelectedItem.AsNonNull();
			logProfile.Description = this.descriptionTextBox.Text;
			logProfile.EmbeddedScriptLogDataSourceProvider = this.embeddedScriptLogDataSourceProvider;
			logProfile.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
			logProfile.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
			logProfile.IsAdministratorNeeded = this.adminNeededSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsContinuousReading = this.continuousReadingSwitch.IsChecked.GetValueOrDefault();
			logProfile.IsTemplate = isTemplate;
			logProfile.LogLevelMapForReading = new Dictionary<string, Logs.LogLevel>(this.logLevelMapEntriesForReading);
			logProfile.LogLevelMapForWriting = new Dictionary<Logs.LogLevel, string>(this.logLevelMapEntriesForWriting);
			logProfile.LogPatternMatchingMode = (LogPatternMatchingMode)this.logPatternMatchingModeComboBox.SelectedItem.AsNonNull();
			logProfile.LogPatterns = this.logPatterns;
			logProfile.LogStringEncodingForReading = (LogStringEncoding)this.logStringEncodingForReadingComboBox.SelectedItem.AsNonNull();
			logProfile.LogStringEncodingForWriting = (LogStringEncoding)this.logStringEncodingForWritingComboBox.SelectedItem.AsNonNull();
			logProfile.LogWritingFormats = this.logWritingFormats;
		logProfile.Name = this.nameTextBox.Text.AsNonNull();
		logProfile.RawLogLevelPropertyName = (string)this.rawLogLevelPropertyComboBox.SelectedItem.AsNonNull();
		logProfile.RestartReadingDelay = this.restartReadingDelayTextBox.Value.GetValueOrDefault();
		logProfile.SortDirection = (SortDirection)this.sortDirectionComboBox.SelectedItem.AsNonNull();
		logProfile.SortKey = (LogSortKey)this.sortKeyComboBox.SelectedItem.AsNonNull();
		logProfile.TimeSpanCultureInfoForReading = (CultureInfo)this.timeSpanCultureInfoForReadingComboBox.SelectedItem.AsNonNull();
		logProfile.TimeSpanCultureInfoForWriting = (CultureInfo)this.timeSpanCultureInfoForWritingComboBox.SelectedItem.AsNonNull();
		logProfile.TimeSpanEncodingForReading = (LogTimeSpanEncoding)this.timeSpanEncodingForReadingComboBox.SelectedItem.AsNonNull();
		logProfile.TimeSpanFormatForDisplaying = this.timeSpanFormatForDisplayingTextBox.Text;
		logProfile.TimeSpanFormatForWriting = this.timeSpanFormatForWritingTextBox.Text;
		logProfile.TimeSpanFormatsForReading = this.timeSpanFormatsForReading;
		logProfile.TimestampCategoryGranularity = (TimestampDisplayableLogCategoryGranularity)this.timestampCategoryGranularityComboBox.SelectedItem.AsNonNull();
		logProfile.TimestampCultureInfoForReading = (CultureInfo)this.timestampCultureInfoForReadingComboBox.SelectedItem.AsNonNull();
		logProfile.TimestampCultureInfoForWriting = (CultureInfo)this.timestampCultureInfoForWritingComboBox.SelectedItem.AsNonNull();
		logProfile.TimestampEncodingForReading = (LogTimestampEncoding)this.timestampEncodingForReadingComboBox.SelectedItem.AsNonNull();
		logProfile.TimestampFormatForDisplaying = this.timestampFormatForDisplayingTextBox.Text;
		logProfile.TimestampFormatForWriting = this.timestampFormatForWritingTextBox.Text;
		logProfile.TimestampFormatsForReading = this.timestampFormatsForReading;
		logProfile.VisibleLogProperties = this.visibleLogProperties;
		logProfile.WorkingDirectoryRequirement = (LogProfilePropertyRequirement)this.workingDirPriorityComboBox.SelectedItem.AsNonNull();
		return logProfile;
	}


	/// <summary>
	/// Check whether options of log data source is supported or not.
	/// </summary>
	public bool HasDataSourceOptions => this.GetValue(HasDataSourceOptionsProperty);


	// Whether Pro-version is activated or not.
	bool IsProVersionActivated => this.GetValue(IsProVersionActivatedProperty);


	// Check whether log data source options is valid or not.
	bool IsValidDataSourceOptions => this.GetValue(IsValidDataSourceOptionsProperty);


	/// <summary>
	/// Get all available types of log chart.
	/// </summary>
	public IList<LogChartType> LogChartTypes { get; } = Enum.GetValues<LogChartType>();


	/// <summary>
	/// Entries of log level map.
	/// </summary>
	public IList<KeyValuePair<string, Logs.LogLevel>> LogLevelMapEntriesForReading { get; }


	/// <summary>
	/// Entries of log level map.
	/// </summary>
	public IList<KeyValuePair<Logs.LogLevel, string>> LogLevelMapEntriesForWriting { get; }
	
	
	/// <summary>
	/// Get all log levels.
	/// </summary>
	public Logs.LogLevel[] LogLevels { get; } = Enum.GetValues<Logs.LogLevel>();


	/// <summary>
	/// Log patterns.
	/// </summary>
	public IList<LogPattern> LogPatterns { get; }


	/// <summary>
	/// Get or set <see cref="LogProfile"/> to be edited.
	/// </summary>
	public LogProfile? LogProfile { get; init; }


	// Log writing formats.
	public IList<string> LogWritingFormats { get; }


	/// <summary>
	/// Definition set of log writing format syntax highlighting.
	/// </summary>
	public SyntaxHighlightingDefinitionSet LogWritingFormatSyntaxHighlightingDefinitionSet { get; }


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
		if (this.LogProfile is not null 
			&& NonBlockingDialogs.TryGetValue(this.LogProfile, out var dialog)
			&& dialog == this)
		{
			NonBlockingDialogs.Remove(this.LogProfile);
		}

		// detach from product manager
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
	
	
	/// <inheritdoc/>
	protected override void OnFirstMeasurementCompleted(Size measuredSize)
	{
		// call base
		base.OnFirstMeasurementCompleted(measuredSize);
		
		// [Workaround] force layout again to prevent insufficient space in baseScrollViewer
		var margin = this.baseScrollViewer.Margin;
		this.baseScrollViewer.Margin = new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom + 1);
		this.baseScrollViewer.RequestLayoutCallback(() =>
		{
			this.baseScrollViewer.Margin = margin;
		});
	}


	// Called when double-tapped on list box.
	void OnListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e)
	{
		if (sender is not Avalonia.Controls.ListBox listBox)
			return;
		if (!listBox.TryFindListBoxItem(e.Item, out var listBoxItem) || listBoxItem is null)
			return;
		if (listBox == this.logLevelMapForReadingListBox)
			_ = this.EditLogLevelMapEntryForReading((KeyValuePair<string, Logs.LogLevel>)e.Item);
		else if (listBox == this.logLevelMapForWritingListBox)
			_ = this.EditLogLevelMapEntryForWriting((KeyValuePair<Logs.LogLevel, string>)e.Item);
		else if (listBox == this.logPatternListBox)
			_ = this.EditLogPattern(listBoxItem);
		else if (listBox == this.logWritingFormatListBox)
			_ = this.EditLogWritingFormat(listBoxItem);
		else if (listBox == this.timeSpanFormatsForReadingListBox)
			_ = this.EditTimeSpanFormatForReading(listBoxItem);
		else if (listBox == this.timestampFormatsForReadingListBox)
			_ = this.EditTimestampFormatForReading(listBoxItem);
		else if (listBox == this.visibleLogPropertyListBox)
			_ = this.EditVisibleLogProperty(listBoxItem);
	}
	
	
	// Called when user start dragging item in list box.
	void OnListBoxItemDragStarted(ListBox listBox, ListBoxItemDragEventArgs e)
	{
		if (listBox.ItemCount <= 1)
			e.Handled = true;
	}
	
	
	// Called when user dropped item on list box.
	void OnListBoxItemDropped<T>(ListBox listBox, ObservableList<T> items, ListBoxItemDragEventArgs e)
	{
		if (e.ItemIndex != e.StartItemIndex)
		{
			items.Move(e.StartItemIndex, e.ItemIndex);
			listBox.SelectedIndex = e.ItemIndex;
		}
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
	protected override void OnOpened(EventArgs e)
	{
		// attach to product manager
		this.Application.ProductManager.Let(it =>
		{
			this.SetValue(IsProVersionActivatedProperty, it.IsProductActivated(Products.Professional));
			it.ProductStateChanged += this.OnProductStateChanged;
		});
		
		// call base
		base.OnOpened(e);

		// continue opening dialog
		if (this.LogProfile?.IsBuiltIn != true)
			_ = this.OnOpenedAsync();
		else
			this.SynchronizationContext.Post(this.Close);
	}
	
	
	// Handle dialog opened asynchronously.
	async Task OnOpenedAsync()
	{
		// show hint of 'learn about logs reading and parsing'
		if (!this.PersistentState.GetValueOrDefault(HasLearnAboutLogsReadingAndParsingHintShown))
		{
			var result = await new MessageDialog
			{
				Buttons = MessageDialogButtons.YesNo,
				Icon = MessageDialogIcon.Question,
				Message = this.GetResourceObservable("String/LogProfileEditorDialog.LearnAboutLogsReadingAndParsingFirst"),
				Title = this.GetObservable(TitleProperty),
			}.ShowDialog(this);
			if (this.IsOpened)
			{
				this.PersistentState.SetValue(HasLearnAboutLogsReadingAndParsingHintShown, true);
				if (result == MessageDialogResult.Yes)
					Platform.OpenLink(Uris.LogsReadingAndParsingDocument);
				else
					this.nameTextBox.Focus();
			}
		}

		// setup initial focus
		this.SynchronizationContext.Post(() => this.nameTextBox.Focus());
		
		// check Pro-version only parameters
		if ((this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider)?.IsProVersionOnly == true
		    && !this.Application.ProductManager.IsProductActivated(Products.Professional))
		{
			await new MessageDialog
			{
				Icon = MessageDialogIcon.Error,
				Message = new FormattedString().Also(it =>
				{
					it.Arg1 = this.LogProfile?.Name;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("LogProfileEditorDialog.CannotEditLogProfileWithProVersionOnlyParams"));
				}),
			}.ShowDialog(this);
			await Task.Delay(300); // [Workaround] Prevent crashing when closing two windows immediately on macOS
			if (!this.IsClosed)
				this.Close();
		}
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		// call base
		base.OnOpening(e);
		
		// check Pro-version
		this.SetValue(IsProVersionActivatedProperty, this.Application.ProductManager.IsProductActivated(Products.Professional));

			// setup initial state
			var profile = this.LogProfile;
			if (profile is null)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.Title.Create"));
				this.allowMultipleFilesSwitch.IsChecked = true;
				this.colorIndicatorComboBox.SelectedItem = LogColorIndicator.None;
				this.SelectDefaultDataSource();
				this.defaultLogLevelComboBox.SelectedItem = Logs.LogLevel.Undefined;
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
				this.logPatternMatchingModeComboBox.SelectedItem = LogPatternMatchingMode.Sequential;
				this.logStringEncodingForReadingComboBox.SelectedItem = LogStringEncoding.Plain;
				this.logStringEncodingForWritingComboBox.SelectedItem = LogStringEncoding.Plain;
				this.rawLogLevelPropertyComboBox.SelectedItem = nameof(Log.Level);
				this.sortDirectionComboBox.SelectedItem = SortDirection.Ascending;
				this.sortKeyComboBox.SelectedItem = LogSortKey.Timestamp;
				this.timeSpanCultureInfoForReadingComboBox.SelectedItem = LogProfile.DefaultTimeSpanCultureInfo;
				this.timeSpanCultureInfoForWritingComboBox.SelectedItem = LogProfile.DefaultTimeSpanCultureInfo;
				this.timeSpanEncodingForReadingComboBox.SelectedItem = LogTimeSpanEncoding.Custom;
				this.timestampCategoryGranularityComboBox.SelectedItem = TimestampDisplayableLogCategoryGranularity.Day;
				this.timestampCultureInfoForReadingComboBox.SelectedItem = LogProfile.DefaultTimestampCultureInfo;
				this.timestampCultureInfoForWritingComboBox.SelectedItem = LogProfile.DefaultTimestampCultureInfo;
				this.timestampEncodingForReadingComboBox.SelectedItem = LogTimestampEncoding.Custom;
				this.workingDirPriorityComboBox.SelectedItem = LogProfilePropertyRequirement.Optional;
			}
			else if (!profile.IsBuiltIn)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/LogProfileEditorDialog.Title.Edit"));
				this.adminNeededSwitch.IsChecked = profile.IsAdministratorNeeded;
				this.allowMultipleFilesSwitch.IsChecked = profile.AllowMultipleFiles;
				this.colorIndicatorComboBox.SelectedItem = profile.ColorIndicator;
				this.cooperativeLogAnalysisScriptSet = profile.CooperativeLogAnalysisScriptSet;
				this.dataSourceOptions = profile.DataSourceOptions;
				this.defaultLogLevelComboBox.SelectedItem = profile.DefaultLogLevel;
				this.descriptionTextBox.Text = profile.Description;
				this.embeddedScriptLogDataSourceProvider = profile.EmbeddedScriptLogDataSourceProvider;
				if (this.embeddedScriptLogDataSourceProvider is not null)
					this.dataSourceProviders.Add(this.embeddedScriptLogDataSourceProvider);
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
				this.iconColorComboBox.SelectedItem = profile.IconColor;
				this.iconComboBox.SelectedItem = profile.Icon;
				this.isTemplateSwitch.IsChecked = profile.IsTemplate;
				this.continuousReadingSwitch.IsChecked = profile.IsContinuousReading;
				this.logLevelMapEntriesForReading.AddAll(profile.LogLevelMapForReading);
				this.logLevelMapEntriesForWriting.AddAll(profile.LogLevelMapForWriting);
				this.logPatternMatchingModeComboBox.SelectedItem = profile.LogPatternMatchingMode;
				this.logPatterns.AddRange(profile.LogPatterns);
				this.logStringEncodingForReadingComboBox.SelectedItem = profile.LogStringEncodingForReading;
				this.logStringEncodingForWritingComboBox.SelectedItem = profile.LogStringEncodingForWriting;
				this.logWritingFormats.AddAll(profile.LogWritingFormats);
			this.nameTextBox.Text = profile.Name;
			this.rawLogLevelPropertyComboBox.SelectedItem = profile.RawLogLevelPropertyName;
			this.restartReadingDelayTextBox.Value = profile.RestartReadingDelay;
			this.sortDirectionComboBox.SelectedItem = profile.SortDirection;
			this.sortKeyComboBox.SelectedItem = profile.SortKey;
			this.timeSpanCultureInfoForReadingComboBox.SelectedItem = profile.TimeSpanCultureInfoForReading.Let(it =>
				CultureInfoComboBox.CultureInfos.Contains(it) ? it : LogProfile.DefaultTimeSpanCultureInfo);
			this.timeSpanCultureInfoForWritingComboBox.SelectedItem = profile.TimeSpanCultureInfoForWriting.Let(it =>
				CultureInfoComboBox.CultureInfos.Contains(it) ? it : LogProfile.DefaultTimeSpanCultureInfo);
			this.timeSpanEncodingForReadingComboBox.SelectedItem = profile.TimeSpanEncodingForReading;
			this.timeSpanFormatForDisplayingTextBox.Text = profile.TimeSpanFormatForDisplaying;
			this.timeSpanFormatForWritingTextBox.Text = profile.TimeSpanFormatForWriting;
			this.timeSpanFormatsForReading.AddRange(profile.TimeSpanFormatsForReading);
			this.timestampCategoryGranularityComboBox.SelectedItem = profile.TimestampCategoryGranularity;
			this.timestampCultureInfoForReadingComboBox.SelectedItem = profile.TimestampCultureInfoForReading.Let(it =>
				CultureInfoComboBox.CultureInfos.Contains(it) ? it : LogProfile.DefaultTimestampCultureInfo);
			this.timestampCultureInfoForWritingComboBox.SelectedItem = profile.TimestampCultureInfoForWriting.Let(it =>
				CultureInfoComboBox.CultureInfos.Contains(it) ? it : LogProfile.DefaultTimestampCultureInfo);
			this.timestampEncodingForReadingComboBox.SelectedItem = profile.TimestampEncodingForReading;
			this.timestampFormatForDisplayingTextBox.Text = profile.TimestampFormatForDisplaying;
			this.timestampFormatForWritingTextBox.Text = profile.TimestampFormatForWriting;
			this.timestampFormatsForReading.AddRange(profile.TimestampFormatsForReading);
			this.visibleLogProperties.AddRange(profile.VisibleLogProperties);
			this.workingDirPriorityComboBox.SelectedItem = profile.WorkingDirectoryRequirement;
		}
		
		// update state
		this.SetValue(HasCooperativeLogAnalysisScriptSetProperty, this.cooperativeLogAnalysisScriptSet is not null);
		this.SetValue(HasEmbeddedScriptLogDataSourceProviderProperty, this.embeddedScriptLogDataSourceProvider is not null);
		
		// get defined log property names
		this.UpdateLogPropertyNamesInLogPatterns();
		
		// setup initial size
		this.Screens.Let(screens =>
		{
			(screens.ScreenFromWindow(this) ?? screens.Primary)?.Let(screen =>
			{
				this.Height = Math.Max(screen.WorkingArea.Height / screen.Scaling / 2, this.FindResourceOrDefault("Double/LogProfileEditorDialog.Height", 600.0));
			});
		});
	}


	// Called when state of product changed.
	void OnProductStateChanged(IProductManager? productManager, string productId)
	{
		if (productManager is not null && productId == Products.Professional)
		{
			this.SetValue(IsProVersionActivatedProperty, productManager.IsProductActivated(productId));
			this.InvalidateInput();
		}
	}


	// Called when selected log data source provider changed.
	void OnSelectedDataSourceChanged()
	{
		// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
		if (this.dataSourceProviderComboBox?.SelectedItem is not ILogDataSourceProvider provider)
			return;
		// ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
		this.SetValue(HasDataSourceOptionsProperty, provider.SupportedSourceOptions.IsNotEmpty());
		this.SetValue(IsWorkingDirectorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory))
			&& !provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory)));
		this.InvalidateInput();
	}


	/// <inheritdoc/>
	protected override void OnUpdateNavigationBar()
	{
		// call base
		base.OnUpdateNavigationBar();
		
		// check state
		if (!this.baseScrollViewer.TryGetSmoothScrollingTargetOffset(out var offset))
			offset = this.baseScrollViewer.Offset;
		var viewport = this.baseScrollViewer.Viewport;
		if (viewport.Height <= 0)
			return;
			
		// find button to select
		var viewportCenter = offset.Y + (viewport.Height / 2);
		ToggleButton selectedButton;
		if (offset.Y <= 1)
			selectedButton = this.commonPanelButton;
		else if (offset.Y + viewport.Height >= this.baseScrollViewer.Extent.Height - 1)
			selectedButton = this.logWritingPanelButton;
		else if (this.logWritingPanel.Bounds.Y <= viewportCenter)
			selectedButton = this.logWritingPanelButton;
		else if (this.logDisplayingPanel.Bounds.Y <= viewportCenter)
			selectedButton = this.logDisplayingPanelButton;
		else if (this.logReadingPanel.Bounds.Y <= viewportCenter)
			selectedButton = this.logReadingPanelButton;
		else
			selectedButton = this.commonPanelButton;
			
		// select button
		this.commonPanelButton.IsChecked = (this.commonPanelButton == selectedButton);
		this.logReadingPanelButton.IsChecked = (this.logReadingPanelButton == selectedButton);
		this.logDisplayingPanelButton.IsChecked = (this.logDisplayingPanelButton == selectedButton);
			this.logWritingPanelButton.IsChecked = (this.logWritingPanelButton == selectedButton);
		}


	// Validate input.
	protected override bool OnValidateInput()
	{
		// call base
		if (!base.OnValidateInput())
			return false;
		
		// check data source and options
		var isTemplate = this.isTemplateSwitch.IsChecked.GetValueOrDefault();
		if (this.dataSourceProviderComboBox.SelectedItem is not ILogDataSourceProvider dataSourceProvider)
			return false;
		this.allowMultipleFilesPanel.IsVisible = dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName));
		if (!isTemplate)
		{
			foreach (var optionName in dataSourceProvider.RequiredSourceOptions)
			{
				switch (optionName)
				{
					case nameof(LogDataSourceOptions.Category):
					case nameof(LogDataSourceOptions.ConnectionString):
					case nameof(LogDataSourceOptions.Password):
					case nameof(LogDataSourceOptions.QueryString):
					case nameof(LogDataSourceOptions.UserName):
						this.SetValue(IsValidDataSourceOptionsProperty, this.dataSourceOptions.IsOptionSet(optionName));
						break;
					case nameof(LogDataSourceOptions.Command):
						this.SetValue(IsValidDataSourceOptionsProperty, !this.dataSourceOptions.CheckPlaceholderInCommands());
						break;
					default:
						this.SetValue(IsValidDataSourceOptionsProperty, true);
						break;
				}
			}
		}

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
		
		
	// Remove source of log chart series.
	void RemoveLogChartSeriesSource(ListBoxItem item)
	{ }


	/// <summary>
	/// Command to remove source of log chart series.
	/// </summary>
	public ICommand RemoveLogChartSeriesSourceCommand { get; }


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
		this.UpdateLogPropertyNamesInLogPatterns();
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
	
	
	// Scroll to given panel
	void ScrollToPanel(ToggleButton button)
	{
		// select panel to scroll to
		Panel panel;
		if (button == this.commonPanelButton)
			panel = this.commonPanel;
		else if (button == this.logReadingPanelButton)
			panel = this.logReadingPanel;
		else if (button == this.logDisplayingPanelButton)
			panel = this.logDisplayingPanel;
		else if (button == this.logWritingPanelButton)
			panel = this.logWritingPanel;
		else
			return;
			
		// scroll to panel
		this.baseScrollViewer.SmoothScrollIntoView(panel);
		
		// update navigation bar
		this.InvalidateNavigationBar();
	}


	// Select default log data source provider.
	void SelectDefaultDataSource() =>
		this.dataSourceProviderComboBox.SelectedItem = this.dataSourceProviders.FirstOrDefault(it => it is FileLogDataSourceProvider);


	// Select given item in list box.
	void SelectListBoxItem(Avalonia.Controls.ListBox listBox, int index)
	{
		this.SynchronizationContext.Post(() =>
		{
			listBox.SelectedItems?.Clear();
			if (index < 0 || index >= listBox.ItemCount)
				return;
			listBox.Focus();
			listBox.SelectedIndex = index;
			listBox.ScrollIntoView(index);
		});
	}


	/// <summary>
	/// Set log data source options.
	/// </summary>
	public async Task SetDataSourceOptions()
	{
		var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
		if (dataSourceProvider is null)
			return;
		var options = await new LogDataSourceOptionsDialog
		{
			DataSourceProvider = dataSourceProvider,
			IsTemplate = this.isTemplateSwitch.IsChecked.GetValueOrDefault(),
			Options = this.dataSourceOptions,
		}.ShowDialog<LogDataSourceOptions?>(this);
		if (options is not null)
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
		if (logProfile is not null && NonBlockingDialogs.TryGetValue(logProfile, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}
		dialog = new LogProfileEditorDialog
		{
			LogProfile = logProfile,
		};
		if (logProfile is not null)
			NonBlockingDialogs[logProfile] = dialog;
		if (parent is not null)
			dialog.Show(parent);
		else
			dialog.Show();
	}


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


	// Extract name of log properties defined by log patterns.
	void UpdateLogPropertyNamesInLogPatterns()
	{
		this.logPropertyNamesInLogPatterns.Clear();
		foreach (var logPattern in this.logPatterns)
			this.logPropertyNamesInLogPatterns.AddAll(logPattern.DefinedLogPropertyNames);
	}


	/// <summary>
	/// List of visible log properties.
	/// </summary>
	public IList<LogProperty> VisibleLogProperties { get; }
}