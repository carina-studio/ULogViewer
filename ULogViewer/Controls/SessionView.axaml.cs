using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Data.Converters;
using CarinaStudio.Input;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using LiveChartsCore.SkiaSharpView.Avalonia;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Path = System.IO.Path;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// View of <see cref="Session"/>.
	/// </summary>
	sealed partial class SessionView : UserControl<IULogViewerApplication>
	{
		/// <summary>
		/// Property of <see cref="AreAllTutorialsShown"/>.
		/// </summary>
		public static readonly DirectProperty<SessionView, bool> AreAllTutorialsShownProperty = AvaloniaProperty.RegisterDirect<SessionView, bool>(nameof(AreAllTutorialsShown), v => v.areAllTutorialsShown);
		/// <summary>
		/// Property of <see cref="CanEditCurrentScriptLogDataSourceProvider"/>.
		/// </summary>
		public static readonly DirectProperty<SessionView, bool> CanEditCurrentScriptLogDataSourceProviderProperty = AvaloniaProperty.RegisterDirect<SessionView, bool>(nameof(CanEditCurrentScriptLogDataSourceProvider), sv => sv.canEditCurrentScriptLogDataSourceProvider);
		/// <summary>
		/// <see cref="IValueConverter"/> to convert log level to readable name.
		/// </summary>
		public static readonly IValueConverter LogLevelNameConverter = new LogLevelNameConverterImpl(App.Current);
		/// <summary>
		/// <see cref="IValueConverter"/> to convert quantifier of log to text on control.
		/// </summary>
		public static readonly IValueConverter LogQuantifierConverter = new FuncValueConverter<string?, string?>(quantifier =>
		{
			if (string.IsNullOrWhiteSpace(quantifier))
				return null;
			return $" ({quantifier})";
		});
		/// <summary>
		/// <see cref="IValueConverter"/> to convert secondary displayable log name to text on control.
		/// </summary>
		public static readonly IValueConverter LogSecondaryDisplayNameConverter = new FuncValueConverter<string?, string?>(name =>
		{
			if (string.IsNullOrWhiteSpace(name))
				return null;
			return $" - {name}";
		});
		/// <summary>
		/// <see cref="IValueConverter"/> to convert from state of status bar to background brush of status bar.
		/// </summary>
		public static readonly IValueConverter StatusBarBackgroundConverter = new FuncValueConverter<SessionViewStatusBarState, IBrush?>(state =>
		{
			var app = App.CurrentOrNull;
			if (app is null)
				return null;
			IBrush? brush;
			switch (state)
			{
				case SessionViewStatusBarState.Active:
					app.TryGetResource("Brush/StatusBar.Background", out brush);
					break;
				case SessionViewStatusBarState.Error:
					app.TryGetResource("Brush/SessionView.StatusBar.Background.Error", out brush);
					break;
				case SessionViewStatusBarState.Paused:
					app.TryGetResource("Brush/SessionView.StatusBar.Background.Paused", out brush);
					break;
				case SessionViewStatusBarState.Warning:
					app.TryGetResource("Brush/SessionView.StatusBar.Background.Warning", out brush);
					break;
				default:
					app.TryGetResource("Brush/SessionView.StatusBar.Background.Inactive", out brush);
					break;
			}
			return brush;
		});
		/// <summary>
		/// Debug logger for <see cref="CarinaStudio.Controls.TextBlock"/> used by <see cref="SessionView"/>.
		/// </summary>
		public static readonly ILogger TextBlockDebugLogger = App.Current.LoggerFactory.CreateLogger("SessionView.CsTextBlock");


        // Implementation of LogLevelNameConverter.
        class LogLevelNameConverterImpl : IValueConverter
		{
			readonly App app;
			public LogLevelNameConverterImpl(App app)
			{
				this.app = app;
			}
			public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
			{
				if (value is Logs.LogLevel level)
				{
					if (level == Logs.LogLevel.Undefined)
						return app.GetString("SessionView.AllLogLevels");
					return Converters.EnumConverters.LogLevel.Convert(value, targetType, parameter, culture);
				}
				return null;
			}
			public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
		}


		// Type of log data source.
		[Flags]
		enum LogDataSourceType
		{
			File = 0x1,
		}


		// Constants.
		const int InitScrollingToLatestLogDelay = 800;
		const int LogUpdateIntervalStatisticCount = 4;
		const int LogUpdateIntervalToResetStatistic = 1000;
		const int ScrollingToLatestLogInterval = 200;
		const int ScrollingToTargetLogRangeInterval = 200;
		const int SlowScrollingToLatestLogInterval = 400;
		const int SmoothScrollingToLatestLogInterval = 66;


		// Static fields.
		static readonly IValueConverter EliminateLeadingTimestampConverter = new FuncValueConverter<object?, string?>(value =>
		{
			var s = value?.ToString();
			if (string.IsNullOrEmpty(s))
				return s;
			LeadingTimestampPattern ??= CreateLeadingTimestampPattern();
			var match = LeadingTimestampPattern.Match(s);
			if (!match.Success)
				return s.TrimStart();
			var endIndex = match.Index + match.Length;
			if (endIndex < s.Length)
				return s[endIndex..];
			return s.TrimStart();
		});
		static readonly StyledProperty<bool> HasLogProfileProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(HasLogProfile), false);
		static readonly StyledProperty<bool> IsProcessInfoVisibleProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsProcessInfoVisible), false);
		static readonly StyledProperty<bool> IsScriptRunningEnabledProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScriptRunningEnabled), false);
		static readonly StyledProperty<bool> IsScrollingToLatestLogNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogNeeded), true);
		static readonly StyledProperty<bool> IsScrollingToTargetLogRangeProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToTargetLogRange));
		static readonly SettingKey<bool> IsCopyLogTextTutorialShownKey = new("SessionView.IsCopyLogTextTutorialShown");
		static readonly SettingKey<bool> IsLogFilesPanelTutorialShownKey = new("SessionView.IsLogFilesPanelTutorialShown");
		static readonly SettingKey<bool> IsMarkedLogsPanelTutorialShownKey = new("SessionView.IsMarkedLogsPanelTutorialShown");
		static readonly SettingKey<bool> IsSelectingLogProfileToStartTutorialShownKey = new("SessionView.IsSelectingLogProfileToStartTutorialShown");
		static readonly SettingKey<bool> IsSwitchingSidePanelsTutorialShownKey = new("SessionView.IsSwitchingSidePanelsTutorialShown");
		static readonly SettingKey<bool> IsTimestampCategoriesPanelTutorialShownKey = new("SessionView.IsTimestampCategoriesPanelTutorialShown");
		static Regex? LeadingTimestampPattern;
		static readonly StyledProperty<double> LogItemHeightProperty = AvaloniaProperty.Register<SessionView, double>(nameof(LogItemHeight));
		static readonly StyledProperty<int> MaxDisplayLineCountForEachLogProperty = AvaloniaProperty.Register<SessionView, int>(nameof(MaxDisplayLineCountForEachLog), 1);
		static readonly StyledProperty<SessionViewStatusBarState> StatusBarStateProperty = AvaloniaProperty.Register<SessionView, SessionViewStatusBarState>(nameof(StatusBarState), SessionViewStatusBarState.None);

		
		// Fields.
		bool areAllTutorialsShown;
		IDisposable? areInitDialogsClosedObserverToken;
		Avalonia.Controls.Window? attachedWindow;
		readonly ScheduledAction autoAddLogFilesAction;
		readonly ScheduledAction autoCloseSidePanelAction;
		readonly ScheduledAction autoSetCommandAction;
		readonly ScheduledAction autoSetIPEndPointAction;
		readonly ScheduledAction autoSetProcessIdAction;
		readonly ScheduledAction autoSetProcessNameAction;
		readonly ScheduledAction autoSetUriAction;
		readonly ScheduledAction autoSetWorkingDirectoryAction;
		INotifyCollectionChanged? attachedLogs;
		readonly ForwardedObservableBoolean canAddLogFiles;
		readonly ObservableCommandState<Session.LogFileParams> canAddLogFilesToSession = new();
		readonly MutableObservableBoolean canCopyLogProperty = new();
		readonly MutableObservableBoolean canCopyLogText = new();
		bool canEditCurrentScriptLogDataSourceProvider;
		readonly MutableObservableBoolean canEditLogProfile = new();
		readonly MutableObservableBoolean canMarkSelectedLogs = new();
		readonly MutableObservableBoolean canMarkUnmarkSelectedLogs = new();
		readonly ObservableCommandState<object?> canReloadLogs = new();
		readonly ObservableCommandState<object?> canResetLogProfileToSession = new();
		readonly MutableObservableBoolean canRestartAsAdmin = new(Platform.IsWindows && !App.Current.IsRunningAsAdministrator);
		readonly ObservableCommandState<LogsSavingOptions> canSaveLogs = new();
		readonly ObservableCommandState<Session.CommandParams> canSetCommand = new();
		readonly ObservableCommandState<IPEndPoint> canSetIPEndPoint = new();
		// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
		readonly ForwardedObservableBoolean canSetLogProfile;
		readonly ObservableCommandState<LogProfile> canSetLogProfileToSession = new();
		readonly ObservableCommandState<int?> canSetProcessId = new();
		readonly ObservableCommandState<string?> canSetProcessName = new();
		readonly ObservableCommandState<Uri> canSetUri = new();
		readonly ObservableCommandState<string> canSetWorkingDirectory = new();
		readonly MutableObservableBoolean canShowFileInExplorer = new();
		readonly MutableObservableBoolean canShowLogProperty = new();
		readonly MutableObservableBoolean canShowWorkingDirectoryInExplorer = new();
		readonly MutableObservableBoolean canUnmarkSelectedLogs = new();
		readonly MenuItem copyLogPropertyMenuItem;
		readonly Border dragDropReceiverBorder;
		readonly ScheduledAction handleDisplayLogPropertiesChangedAction;
		IDisposable? hasDialogsObserverToken;
		IDisposable? isActiveObserverToken;
		bool isAltKeyPressed;
		bool isAttachedToLogicalTree;
		bool isAutoSettingLogsReadingParameters;
		readonly bool isInitializing = true;
		bool isCommandNeededAfterLogProfileSet;
		bool isIPEndPointNeededAfterLogProfileSet;
		bool isLogFileNeededAfterLogProfileSet;
		readonly MutableObservableBoolean isNotAddingLogFiles = new(true);
		bool isPointerPressedOnLogListBox;
		bool isProcessIdNeededAfterLogProfileSet;
		bool isProcessNameNeededAfterLogProfileSet;
		bool isRestartingAsAdminConfirmed;
		bool isSelectingFileToSaveLogs;
		bool isSmoothScrollingToLatestLog;
		bool isUriNeededAfterLogProfileSet;
		bool isWorkingDirNeededAfterLogProfileSet;
		bool keepSidePanelVisible;
		Control? lastClickedLogPropertyView;
		long lastLogUpdateTime;
		double lastToolBarWidthWhenLayoutItems;
		int latestDisplayedLogCount;
		DisplayableLog[]? latestDisplayedLogRange;
		readonly List<DisplayableLog> latestDisplayedMarkedLogs = new();
		DisplayableLog? latestSelectedDisplayableLog;
		int latestTargetLogRangeScrollingDirection;
		readonly ContextMenu logActionMenu;
		readonly ContextMenu logFileActionMenu;
		readonly AppSuite.Controls.ListBox logFileListBox;
		IDisposable? logFilesPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly List<ColumnDefinition> logHeaderColumns = new();
		readonly Control logHeaderContainer;
		readonly Grid logHeaderGrid;
		readonly List<MutableObservableValue<GridLength>> logHeaderWidths = new();
		readonly Avalonia.Controls.ListBox logListBox;
		readonly Panel logListBoxContainer;
		readonly ContextMenu logMarkingMenu;
		readonly LogProfileSelectionContextMenu logProfileSelectionMenu;
		IBrush? logPropertyViewBorderPointerOverBrush;
		IDisposable? logsBindingToken;
		readonly ToggleButton logsSavingButton;
		readonly ContextMenu logsSavingMenu;
		ScrollViewer? logScrollViewer;
		Vector? logScrollViewerOffsetWhenSelectingItems;
		readonly Queue<long> logUpdateTimeQueue = new(LogUpdateIntervalStatisticCount);
		readonly Avalonia.Controls.ListBox markedLogListBox;
		IDisposable? markedLogsPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly double minLogListBoxSizeToCloseSidePanel;
		readonly double minLogTextFilterItemsPanelWidth;
		readonly ToggleButton otherActionsButton;
		readonly ContextMenu otherActionsMenu;
		readonly HashSet<Key> pressedKeys = new();
		readonly ScheduledAction scrollToLatestLogAction;
		readonly ScheduledAction scrollToTargetLogRangeAction;
		readonly MenuItem searchLogPropertyOnInternetMenuItem;
		IBrush? selectableValueLogItemBackgroundBrush;
		readonly IMultiValueConverter selectableValueLogItemBackgroundConverter;
		readonly ToggleButton selectAndSetLogProfileDropDownButton;
		readonly MenuItem showLogPropertyMenuItem;
		readonly ColumnDefinition sidePanelColumn;
		readonly Control sidePanelContainer;
		readonly ScheduledAction smoothScrollToLatestLogAction;
		readonly Stopwatch stopwatch = new Stopwatch().Also(it => it.Start());
		int targetLogRangeScrollingDirectionChangeCount;
		DisplayableLog[]? targetLogRangeToScrollTo;
		readonly List<DisplayableLog> targetMarkedLogsToScrollTo = new();
		readonly ToggleButton testButton;
		readonly ContextMenu testMenu;
		readonly AppSuite.Controls.ListBox timestampCategoryListBox;
		IDisposable? timestampCategoryPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly Border toolBarContainer;
		readonly Panel toolBarLogActionItemsPanel;
		readonly Panel toolBarLogTextFilterItemsPanel;
		readonly Panel toolBarOtherItemsPanel;
		readonly Panel toolBarOtherLogFilterItemsPanel;
		readonly ScheduledAction updateCanEditCurrentScriptLogDataSourceProviderAction;
		readonly ScheduledAction updateLatestDisplayedLogRangeAction;
		readonly ScheduledAction updateLogHeaderContainerMarginAction;
		readonly ScheduledAction updateLogItemHeightAction;
		readonly ScheduledAction updateStatusBarStateAction;
		readonly SortedObservableList<Logs.LogLevel> validLogLevels = new((x, y) => (int)x - (int)y);
		readonly ToggleButton workingDirectoryActionsButton;
		readonly ContextMenu workingDirectoryActionsMenu;


		// Static initializer.
		static SessionView()
		{
			//App.Current.PersistentState.ResetValue(IsLogAnalysisPanelTutorialShownKey);
			//App.Current.PersistentState.ResetValue(IsLogFilesPanelTutorialShownKey);
			//App.Current.PersistentState.ResetValue(IsMarkedLogsPanelTutorialShownKey);
			//App.Current.PersistentState.ResetValue(IsSelectingLogProfileToStartTutorialShownKey);
			//App.Current.PersistentState.ResetValue(IsSwitchingSidePanelsTutorialShownKey);
			//App.Current.PersistentState.ResetValue(IsTimestampCategoriesPanelTutorialShownKey);
		}


		/// <summary>
		/// Initialize new <see cref="SessionView"/> instance.
		/// </summary>
		public SessionView()
		{
			// prepare command states
			this.canAddLogFiles = new(ForwardedObservableBoolean.CombinationMode.And, false, this.canAddLogFilesToSession, this.isNotAddingLogFiles);
			this.canAddLogFiles.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});
			this.canSetCommand.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});
			this.canSetIPEndPoint.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});
			this.canSetLogProfile = new ForwardedObservableBoolean(ForwardedObservableBoolean.CombinationMode.Or,
				false,
				this.canResetLogProfileToSession,
				this.canSetLogProfileToSession
			);
			this.canSetProcessId.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});
			this.canSetProcessName.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});
			this.canSetUri.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});
			this.canSetWorkingDirectory.Subscribe(value =>
			{
				if (value)
					this.AutoSelectAndSetLogsReadingParameters();
			});

			// create commands
			this.AddLogFilesCommand = new Command(this.AddLogFilesAsync, this.canAddLogFiles);
			this.CopyKeyLogAnalysisRuleSetCommand = new Command<KeyLogAnalysisRuleSet>(this.CopyKeyLogAnalysisRuleSet);
			this.CopyLogAnalysisScriptSetCommand = new Command<LogAnalysisScriptSet>(this.CopyLogAnalysisScriptSet);
			this.CopyLogFileNameCommand = new Command<string>(this.CopyLogFileName);
			this.CopyLogFilePathCommand = new Command<string>(this.CopyLogFilePath);
			this.CopyLogPropertyCommand = new Command(this.CopyLogProperty, this.canCopyLogProperty);
			this.CopyLogTextCommand = new Command(this.CopyLogText, this.canCopyLogText);
			this.CopyOperationCountingAnalysisRuleSetCommand = new Command<OperationCountingAnalysisRuleSet>(this.CopyOperationCountingAnalysisRuleSet);
			this.CopyOperationDurationAnalysisRuleSetCommand = new Command<OperationDurationAnalysisRuleSet>(this.CopyOperationDurationAnalysisRuleSet);
			this.CopyPredefinedLogTextFilterCommand = new Command<PredefinedLogTextFilter>(this.CopyPredefinedLogTextFilter);
			this.EditKeyLogAnalysisRuleSetCommand = new Command<KeyLogAnalysisRuleSet>(this.EditKeyLogAnalysisRuleSet);
			this.EditLogAnalysisScriptSetCommand = new Command<LogAnalysisScriptSet>(this.EditLogAnalysisScriptSet);
			this.EditOperationCountingAnalysisRuleSetCommand = new Command<OperationCountingAnalysisRuleSet>(this.EditOperationCountingAnalysisRuleSet);
			this.EditOperationDurationAnalysisRuleSetCommand = new Command<OperationDurationAnalysisRuleSet>(this.EditOperationDurationAnalysisRuleSet);
			this.EditPredefinedLogTextFilterCommand = new Command<PredefinedLogTextFilter>(this.EditPredefinedLogTextFilter);
			this.ExportKeyLogAnalysisRuleSetCommand = new Command<KeyLogAnalysisRuleSet>(this.ExportKeyLogAnalysisRuleSet);
			this.ExportLogAnalysisScriptSetCommand = new Command<LogAnalysisScriptSet>(this.ExportLogAnalysisScriptSet);
			this.ExportOperationCountingAnalysisRuleSetCommand = new Command<OperationCountingAnalysisRuleSet>(this.ExportOperationCountingAnalysisRuleSet);
			this.ExportOperationDurationAnalysisRuleSetCommand = new Command<OperationDurationAnalysisRuleSet>(this.ExportOperationDurationAnalysisRuleSet);
			this.MarkSelectedLogsCommand = new Command<MarkColor>(this.MarkSelectedLogs, this.canMarkSelectedLogs);
			this.MarkUnmarkSelectedLogsCommand = new Command(this.MarkUnmarkSelectedLogs, this.canMarkUnmarkSelectedLogs);
			this.ReloadLogFileWithNewMaxLogReadingCountCommand = new Command<string>(this.ReloadLogFileWithNewMaxLogReadingCount);
			this.ReloadLogFileWithNewPreconditionCommand = new Command<string>(this.ReloadLogFileWithNewPrecondition);
			this.ReloadLogFileWithoutLogReadingPreconditionCommand = new Command<string>(this.ReloadLogFileWithoutLogReadingPrecondition);
			this.ReloadLogsCommand = new Command(this.ReloadLogs, this.canReloadLogs);
			this.RemoveKeyLogAnalysisRuleSetCommand = new Command<KeyLogAnalysisRuleSet>(this.RemoveKeyLogAnalysisRuleSet);
			this.RemoveLogAnalysisScriptSetCommand = new Command<LogAnalysisScriptSet>(this.RemoveLogAnalysisScriptSet);
			this.RemoveOperationCountingAnalysisRuleSetCommand = new Command<OperationCountingAnalysisRuleSet>(this.RemoveOperationCountingAnalysisRuleSet);
			this.RemoveOperationDurationAnalysisRuleSetCommand = new Command<OperationDurationAnalysisRuleSet>(this.RemoveOperationDurationAnalysisRuleSet);
			this.RemovePredefinedLogTextFilterCommand = new Command<PredefinedLogTextFilter>(this.RemovePredefinedLogTextFilter);
			this.RenamePredefinedLogTextFilterGroupCommand = new Command<PredefinedLogTextFilterGroup>(this.RenamePredefinedLogTextFilterGroup);
			this.RestartAsAdministratorCommand = new Command(this.RestartAsAdministrator, this.canRestartAsAdmin);
			this.SaveAllLogsCommand = new Command(() => this.SaveLogs(true), this.canSaveLogs);
			this.SaveLogsCommand = new Command(() => this.SaveLogs(false), this.canSaveLogs);
			this.SelectAndSetCommandCommand = new Command(this.SelectAndSetCommandAsync, this.canSetCommand);
			this.SelectAndSetIPEndPointCommand = new Command(this.SelectAndSetIPEndPointAsync, this.canSetIPEndPoint);
			this.SelectAndSetLogProfileCommand = new Command(this.SelectAndSetLogProfileAsync, this.canSetLogProfile);
			this.SelectAndSetProcessIdCommand = new Command(this.SelectAndSetProcessIdAsync, this.canSetProcessId);
			this.SelectAndSetProcessNameCommand = new Command(this.SelectAndSetProcessNameAsync, this.canSetProcessName);
			this.SelectAndSetUriCommand = new Command(this.SelectAndSetUriAsync, this.canSetUri);
			this.SelectAndSetWorkingDirectoryCommand = new Command(this.SelectAndSetWorkingDirectoryAsync, this.canSetWorkingDirectory);
			this.ShowFileInExplorerCommand = new Command(this.ShowFileInExplorer, this.canShowFileInExplorer);
			this.ShowLogFileInExplorerCommand = new Command<string>(this.ShowLogFileInExplorer);
			this.ShowLogStringPropertyCommand = new Command(() => this.ShowLogStringProperty(), this.canShowLogProperty);
			this.ShowWorkingDirectoryInExplorerCommand = new Command(this.ShowWorkingDirectoryInExplorer, this.canShowWorkingDirectoryInExplorer);
			this.TestCommand = new Command<string>(this.Test);
			this.UnmarkSelectedLogsCommand = new Command(this.UnmarkSelectedLogs, this.canUnmarkSelectedLogs);

			// setup properties
			this.SetValue(IsProcessInfoVisibleProperty, this.Settings.GetValueOrDefault(AppSuite.SettingKeys.ShowProcessInfo));
			this.LogChartXAxes = ListExtensions.AsReadOnly(new[] { this.logChartXAxis });
			this.LogChartYAxes = ListExtensions.AsReadOnly(new[] { this.logChartYAxis });
			this.ValidLogLevels = ListExtensions.AsReadOnly(this.validLogLevels);

			// create value converters
			this.selectableValueLogItemBackgroundConverter = new FuncMultiValueConverter<bool, IBrush?>(values =>
			{
#pragma warning disable CS8619
				if (values is not IList<bool> list)
					list = values.ToArray();
#pragma warning restore CS8619
				if (list.Count >= 2 
					&& list[0] /* IsValueSelected */ 
					&& !list[1] /* IsListBoxItemSelected */)
				{
					this.selectableValueLogItemBackgroundBrush ??= this.Application.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.Background.SelectedValue");
					return this.selectableValueLogItemBackgroundBrush ?? Brushes.Transparent;
				}
				return Brushes.Transparent;
			});

			// create syntax highlighting definitions
			this.RegexSyntaxHighlightingDefinitionSet = RegexSyntaxHighlighting.CreateDefinitionSet(this.Application);

			// initialize
			this.IsToolsMenuItemVisible = this.Application.IsDebugMode || AppSuite.Controls.PathEnvVarEditorDialog.IsSupported;
			AvaloniaXamlLoader.Load(this);

			// load resources
			if (this.Application.TryFindResource<double>("Double/SessionView.LogListBox.MinSizeToCloseSidePanel", out var doubleRes))
				this.minLogListBoxSizeToCloseSidePanel = doubleRes.GetValueOrDefault();
			this.minLogTextFilterItemsPanelWidth = this.FindResourceOrDefault<double>("Double/SessionView.ToolBar.LogTextFilterItemsPanel.MinWidth", 300);

			// setup containers
			this.logListBoxContainer = this.Get<Panel>(nameof(logListBoxContainer)).Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.autoCloseSidePanelAction?.Schedule());
			});
			this.toolBarContainer = this.Get<Border>(nameof(toolBarContainer)).Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.UpdateToolBarItemsLayout());
				it.AddHandler(PointerReleasedEvent, this.OnToolBarPointerReleased, RoutingStrategies.Tunnel);
			});

			// setup controls
			this.clearLogTextFilterButton = this.toolBarContainer.FindControl<Button>(nameof(clearLogTextFilterButton)).AsNonNull();
			this.createLogAnalysisRuleSetButton = this.Get<ToggleButton>(nameof(createLogAnalysisRuleSetButton));
			this.createLogAnalysisRuleSetMenu = ((ContextMenu)this.Resources[nameof(createLogAnalysisRuleSetMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.createLogAnalysisRuleSetButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.createLogAnalysisRuleSetButton, false);
					this.createLogAnalysisRuleSetButton.IsChecked = true;
				});
			});
			this.dragDropReceiverBorder = this.Get<Border>(nameof(dragDropReceiverBorder));
			this.ignoreLogTextFilterCaseButton = this.toolBarContainer.FindControl<Button>(nameof(ignoreLogTextFilterCaseButton)).AsNonNull();
			this.keyLogAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(keyLogAnalysisRuleSetListBox)).Also(this.SetupLogAnalysisRuleSetListBox);
			this.Get<Expander>("keyLogAnalysisRuleSetsExpander").Also(this.SetupLogAnalysisRuleSetsExpander);
			this.logAnalysisResultListBox = this.Get<AppSuite.Controls.ListBox>(nameof(logAnalysisResultListBox)).Also(it =>
			{
				it.DoubleClickOnItem += this.OnLogAnalysisResultListBoxDoubleClickOnItem;
				it.AddHandler(PointerPressedEvent, this.OnLogAnalysisResultListBoxPointerPressed, RoutingStrategies.Tunnel);
				it.AddHandler(PointerReleasedEvent, this.OnLogAnalysisResultListBoxPointerReleased, RoutingStrategies.Tunnel);
				it.AddHandler(PointerWheelChangedEvent, this.OnLogAnalysisResultListBoxPointerWheelChanged, RoutingStrategies.Tunnel);
				it.GetObservable(Avalonia.Controls.ListBox.ScrollProperty).Subscribe(_ =>
				{
					this.logAnalysisResultScrollViewer = (it.Scroll as ScrollViewer)?.Also(scrollViewer =>
					{
						scrollViewer.ScrollChanged += this.OnLogAnalysisResultListBoxScrollChanged;
					});
				});
				it.SelectionChanged += this.OnLogAnalysisResultListBoxSelectionChanged;
			});
			this.logAnalysisRuleSetsButton = this.Get<ToggleButton>(nameof(logAnalysisRuleSetsButton)).Also(it =>
			{
				it.GetObservable(IsVisibleProperty).Subscribe(isVisible =>
				{
					if (isVisible)
						this.SynchronizationContext.Post(this.ShowLogAnalysisRuleSetsTutorial);
				});
			});
			this.logAnalysisRuleSetsPopup = this.Get<Popup>(nameof(logAnalysisRuleSetsPopup)).Also(it =>
			{
				it.Closed += (_, _) => this.logListBox?.Focus();
				it.Opened += (_, _) => 
				{
					if (Platform.IsMacOS)
					{
						this.SynchronizationContext.PostDelayed(() =>
						{
							ToolTip.SetIsOpen(this.logAnalysisRuleSetsButton, true);
							ToolTip.SetIsOpen(this.logAnalysisRuleSetsButton, false);
						}, 100);
					}
					this.keyLogAnalysisRuleSetListBox.Focus();
				};
			});
			this.logAnalysisScriptSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(logAnalysisScriptSetListBox)).Also(this.SetupLogAnalysisRuleSetListBox);
			this.Get<Expander>("logAnalysisScriptSetsExpander").Also(this.SetupLogAnalysisRuleSetsExpander);
			this.logChart = this.Get<CartesianChart>(nameof(logChart)).Also(it =>
			{
				it.DataPointerDown += (_, e) => this.OnLogChartDataPointerDown(e);
				it.DoubleTapped += (_, e) =>
				{
					this.isLogChartDoubleTapped = true;
					e.Handled = true;
				};
				it.GetObservable(IsPointerOverProperty).Subscribe(isPointerOver =>
				{
					if (!this.isInitializing)
						this.OnLogChartPointerOverChanged(isPointerOver);
				});
				it.AddHandler(PointerMovedEvent, this.OnLogChartPointerMoved, RoutingStrategies.Tunnel);
				it.AddHandler(PointerPressedEvent, this.OnLogChartPointerPressed, RoutingStrategies.Tunnel);
				it.AddHandler(PointerReleasedEvent, this.OnLogChartPointerReleased, RoutingStrategies.Tunnel);
				it.SizeChanged += (_, e) => this.OnLogChartSizeChanged(e);
				it.Legend = new LogChartLegend(this);
				it.Tooltip = new LogChartToolTip(this);
				this.logChartXAxis.PropertyChanged += this.OnLogChartAxisPropertyChanged;
				this.logChartYAxis.PropertyChanged += this.OnLogChartAxisPropertyChanged;
			});
			this.logChartGridRow = this.Get<Panel>("logChartPanel").Let(it => it.FindLogicalAncestorOfType<Grid>().AsNonNull().RowDefinitions[Grid.GetRow(it)]);
			this.logChartTypeButton = this.toolBarContainer.FindControl<ToggleButton>(nameof(logChartTypeButton)).AsNonNull();
			this.logFileListBox = this.Get<AppSuite.Controls.ListBox>(nameof(logFileListBox)).Also(it =>
			{
				it.AddHandler(PointerPressedEvent, (_, e) =>
				{
					var position = e.GetPosition(it);
					var item = (it.InputHitTest(position) as Visual)?.FindAncestorOfType<ListBoxItem>();
					if (item is null)
						this.SynchronizationContext.Post(() => it.SelectedItem = null);
				}, RoutingStrategies.Tunnel);
			});
			this.logFilteringHelpButton = this.toolBarContainer.FindControl<Button>(nameof(logFilteringHelpButton)).AsNonNull();
			this.logHeaderContainer = this.Get<Control>(nameof(logHeaderContainer));
			this.logHeaderGrid = this.Get<Grid>(nameof(logHeaderGrid)).Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.ReportLogHeaderColumnWidths());
			});
			this.logFilterCombinationModeButton = toolBarContainer.FindControl<ToggleButton>(nameof(logFilterCombinationModeButton)).AsNonNull();
			this.logLevelFilterComboBox = this.Get<ComboBox>(nameof(logLevelFilterComboBox)).Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logListBox = this.logListBoxContainer.FindControl<Avalonia.Controls.ListBox>(nameof(logListBox))!.Also(it =>
			{
				it.AddHandler(PointerPressedEvent, this.OnLogListBoxPointerPressed, RoutingStrategies.Tunnel);
				it.AddHandler(PointerReleasedEvent, this.OnLogListBoxPointerReleased, RoutingStrategies.Tunnel);
				it.AddHandler(PointerWheelChangedEvent, this.OnLogListBoxPointerWheelChanged, RoutingStrategies.Tunnel);
				it.GetObservable(Avalonia.Controls.ListBox.ScrollProperty).Subscribe(_ =>
				{
					this.logScrollViewer = (it.Scroll as ScrollViewer)?.Also(scrollViewer =>
					{
						scrollViewer.AllowAutoHide = false;
						scrollViewer.ScrollChanged += this.OnLogListBoxScrollChanged;
					});
				});
			});
			this.logProcessIdFilterTextBox = toolBarContainer.FindControl<IntegerTextBox>(nameof(logProcessIdFilterTextBox))!.Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logsSavingButton = this.Get<ToggleButton>(nameof(logsSavingButton));
			this.logTextFilterTextBox = this.Get<RegexTextBox>(nameof(logTextFilterTextBox)).Also(it =>
			{
				it.GetObservable(RegexTextBox.HasOpenedAssistanceMenusProperty).Subscribe(hasOpenedMenus =>
				{
					(this.DataContext as Session)?.LogFiltering.Let(it => it.IsTemporaryTextFilter = hasOpenedMenus);
				});
				it.PhraseInputAssistanceProvider = new LogTextFilterPhraseInputAssistanceProvider(this);
			});
			this.logThreadIdFilterTextBox = toolBarContainer.FindControl<IntegerTextBox>(nameof(logThreadIdFilterTextBox)).AsNonNull().Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.markedLogListBox = this.Get<Avalonia.Controls.ListBox>(nameof(markedLogListBox));
			this.operationCountingAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(operationCountingAnalysisRuleSetListBox)).Also(this.SetupLogAnalysisRuleSetListBox);
			this.Get<Expander>("operationCountingAnalysisRuleSetsExpander").Also(this.SetupLogAnalysisRuleSetsExpander);
			this.operationDurationAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(operationDurationAnalysisRuleSetListBox)).Also(this.SetupLogAnalysisRuleSetListBox);
			this.Get<Expander>("operationDurationAnalysisRuleSetsExpander").Also(this.SetupLogAnalysisRuleSetsExpander);
			this.otherActionsButton = this.Get<ToggleButton>(nameof(otherActionsButton));
			this.predefinedLogTextFilterListBox = this.Get<Avalonia.Controls.ListBox>(nameof(predefinedLogTextFilterListBox)).Also(it =>
			{
				it.AddHandler(PointerPressedEvent, this.OnPredefinedLogTextFilterListBoxPointerPressed, RoutingStrategies.Tunnel);
				it.SelectionChanged += this.OnPredefinedLogTextFilterListBoxSelectionChanged;
			});
			this.predefinedLogTextFiltersAndGroupsPanel = this.Get<Panel>(nameof(predefinedLogTextFiltersAndGroupsPanel));
			this.predefinedLogTextFiltersButton = this.Get<ToggleButton>(nameof(predefinedLogTextFiltersButton));
			this.predefinedLogTextFiltersPopup = this.Get<Popup>(nameof(predefinedLogTextFiltersPopup)).Also(it =>
			{
				it.Closed += (_, _) => this.logListBox.Focus();
				it.Opened += (_, _) => this.SynchronizationContext.Post(() =>
				{
					if (Platform.IsMacOS)
					{
						this.SynchronizationContext.PostDelayed(() =>
						{
							ToolTip.SetIsOpen(this.predefinedLogTextFiltersButton, true);
							ToolTip.SetIsOpen(this.predefinedLogTextFiltersButton, false);
						}, 100);
					}
					this.predefinedLogTextFilterListBox.Focus();
				});
			});
			this.selectAndSetLogProfileDropDownButton = toolBarContainer.FindControl<ToggleButton>(nameof(selectAndSetLogProfileDropDownButton)).AsNonNull();
			this.sidePanelColumn = this.Get<Grid>("RootGrid").Let(grid =>
			{
				return grid.ColumnDefinitions[2].Also(it =>
				{
					it.GetObservable(ColumnDefinition.WidthProperty).Subscribe(length =>
					{
						if (this.DataContext is Session session)
						{
							if (session.LogAnalysis.IsPanelVisible 
								|| session.IsLogFilesPanelVisible 
								|| session.IsMarkedLogsPanelVisible
								|| session.LogCategorizing.IsTimestampCategoriesPanelVisible)
							{
								session.LogAnalysis.PanelSize = length.Value;
								session.LogFilesPanelSize = length.Value;
								session.MarkedLogsPanelSize = length.Value;
								session.LogCategorizing.TimestampCategoriesPanelSize = length.Value;
							}
						}
					});
				});
			});
			this.sidePanelContainer = this.Get<Control>(nameof(sidePanelContainer));
#if !DEBUG
			this.Get<Button>("testButton").IsVisible = false;
#endif
			this.testButton = toolBarContainer.FindControl<ToggleButton>(nameof(testButton)).AsNonNull();
			this.testMenu = ((ContextMenu)this.Resources[nameof(testMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.testButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.testButton, false);
					this.testButton.IsChecked = true;
				});
			});
			this.timestampCategoryListBox = this.Get<AppSuite.Controls.ListBox>(nameof(timestampCategoryListBox)).Also(it =>
			{
				it.GetObservable(Avalonia.Controls.ListBox.SelectedItemProperty).Subscribe(item =>
					this.OnLogCategoryListBoxSelectedItemChanged(it, item as DisplayableLogCategory));
			});
			this.toolBarLogActionItemsPanel = this.toolBarContainer.FindControl<Panel>(nameof(toolBarLogActionItemsPanel)).AsNonNull().Also(it =>
			{
				it.SizeChanged += (_, _) => 
				{
					this.lastToolBarWidthWhenLayoutItems = default;
					this.UpdateToolBarItemsLayout();
				};
			});
			this.toolBarLogTextFilterItemsPanel = this.toolBarContainer.FindControl<Panel>(nameof(toolBarLogTextFilterItemsPanel)).AsNonNull();
			this.toolBarOtherItemsPanel = this.toolBarContainer.FindControl<Panel>(nameof(toolBarOtherItemsPanel)).AsNonNull().Also(it =>
			{
				it.SizeChanged += (_, _) => 
				{
					this.lastToolBarWidthWhenLayoutItems = default;
					this.UpdateToolBarItemsLayout();
				};
			});
			this.toolBarOtherLogFilterItemsPanel = this.toolBarContainer.FindControl<Panel>(nameof(toolBarOtherLogFilterItemsPanel)).AsNonNull().Also(it =>
			{
				it.SizeChanged += (_, _) => 
				{
					this.lastToolBarWidthWhenLayoutItems = default;
					this.UpdateToolBarItemsLayout();
				};
			});
			this.workingDirectoryActionsButton = this.Get<ToggleButton>(nameof(workingDirectoryActionsButton));

			// setup menus
			this.logActionMenu = ((ContextMenu)this.Resources[nameof(logActionMenu)].AsNonNull()).Also(it =>
			{
				it.Opened += (_, _) =>
				{
					this.IsScrollingToLatestLogNeeded = false;
					if (this.showLogPropertyMenuItem == null)
						return;
					var selectionCount = this.logListBox.SelectedItems?.Count ?? 0;
					var log = selectionCount == 1 
						? (this.logListBox.SelectedItems?[0] as DisplayableLog)
						: null;
					var property = this.lastClickedLogPropertyView?.Tag as DisplayableLogProperty;
					var propertyDisplayName = property?.DisplayName.Let(it =>
					{
						if (string.IsNullOrWhiteSpace(it))
							it = Converters.LogPropertyNameConverter.Default.Convert(property.Name);
						return it;
					});
					if (log is not null && property is not null)
					{
						var propertyValue = (this.DataContext as Session)?.LogSelection.SelectedLogStringPropertyValue?.ToString();
						if (string.IsNullOrWhiteSpace(propertyValue))
							propertyValue = propertyDisplayName;
						else if (propertyValue.Length > 16)
							propertyValue = $"{propertyValue[..16]}…";
						this.filterByLogPropertyMenuItem!.Header = this.Application.GetFormattedString("SessionView.FilterByLogProperty", propertyValue);
						this.searchLogPropertyOnInternetMenuItem!.Header = this.Application.GetFormattedString("SessionView.SearchLogPropertyOnInternet", propertyValue);
						this.showLogPropertyMenuItem.Header = this.Application.GetFormattedString("SessionView.ShowLogProperty", propertyDisplayName);
					}
					else
					{
						this.filterByLogPropertyMenuItem!.Header = this.Application.GetString("SessionView.FilterByLogProperty.Disabled");
						this.searchLogPropertyOnInternetMenuItem!.Header = this.Application.GetString("SessionView.SearchLogPropertyOnInternet.Disabled");
						this.showLogPropertyMenuItem.Header = this.Application.GetString("SessionView.ShowLogProperty.Disabled");
					}
					if (selectionCount > 0 && property is not null)
						this.copyLogPropertyMenuItem!.Header = this.Application.GetFormattedString("SessionView.CopyLogProperty", propertyDisplayName);
					else
						this.copyLogPropertyMenuItem!.Header = this.Application.GetString("SessionView.CopyLogProperty.Disabled");
				};
			});
			this.logChartTypeMenu = ((ContextMenu)this.Resources[nameof(logChartTypeMenu)].AsNonNull()).Also(it =>
			{
				it.ItemsSource = this.SetupLogChartTypeMenuItems(it);
				it.Closed += (_, _) => this.SynchronizationContext.Post(() =>
				{
					this.logChartTypeButton.IsChecked = false;
					it.DataContext = null;
				});
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.logChartTypeButton, false);
					this.logChartTypeButton.IsChecked = true;
				});
			});
			this.logFileActionMenu = ((ContextMenu)this.Resources[nameof(logFileActionMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) =>
				{
					it.DataContext = null;
				};
				it.Opened += (_, _) =>
				{
					it.DataContext = this.logFileListBox.SelectedItem;
				};
			});
			this.logFilterCombinationModeMenu = ((ContextMenu)this.Resources[nameof(logFilterCombinationModeMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.logFilterCombinationModeButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.logFilterCombinationModeButton, false);
					this.logFilterCombinationModeButton.IsChecked = true;
				});
			});
			this.logMarkingMenu = ((ContextMenu)this.Resources[nameof(logMarkingMenu)].AsNonNull()).Also(it =>
			{
				it.Opened += (_, _) =>
				{
					this.IsScrollingToLatestLogNeeded = false;
				};
			});
			this.logProfileSelectionMenu = ((LogProfileSelectionContextMenu)this.Resources[nameof(logProfileSelectionMenu)].AsNonNull()).Also(it =>
			{
				it.LogProfileCreated += (_, logProfile) =>
					this.OnLogProfileCreatedByLogProfileSelectionMenu(logProfile);
				it.LogProfileExported += (_, _, fileName) =>
				{
					if (this.attachedWindow is not MainWindow mainWindow)
						return false;
					mainWindow.AddNotification(new Notification().Also(it =>
					{
						if (Platform.IsOpeningFileManagerSupported)
						{
							it.Actions = new[]
							{
								new NotificationAction().Also(it =>
								{
									it.Command = new Command(() => Platform.OpenFileManager(fileName));
									it.Bind(NotificationAction.NameProperty, this.Application.GetObservableString("SessionView.ShowFileInExplorer"));
								})
							};
						}
						it.BindToResource(Notification.IconProperty, this, "Image/Icon.Success.Colored");
						it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
						{
							it.Arg1 = fileName;
							it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("LogProfileSelectionDialog.LogProfileExported"));
						}));
					}));
					return true;
				};
				it.LogProfileExportingFailed += (_, _, fileName) =>
				{
					if (this.attachedWindow is not MainWindow mainWindow)
						return false;
					mainWindow.AddNotification(new Notification().Also(it =>
					{
						it.BindToResource(Notification.IconProperty, this, "Image/Icon.Error.Colored");
						it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
						{
							it.Arg1 = fileName;
							it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("LogProfileSelectionDialog.FailedToExportLogProfile"));
						}));
					}));
					return true;
				};
				it.LogProfileSelected += async (_, logProfile) =>
					await this.SetLogProfileAsync(logProfile);
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.selectAndSetLogProfileDropDownButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() =>
				{
					ToolTip.SetIsOpen(this.selectAndSetLogProfileDropDownButton, false);
					this.selectAndSetLogProfileDropDownButton.IsChecked = true;
				});
			});
			this.logsSavingMenu = ((ContextMenu)this.Resources[nameof(logsSavingMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.logsSavingButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.logsSavingButton, false);
					this.logsSavingButton.IsChecked = true;
				});
			});
			this.otherActionsMenu = ((ContextMenu)this.Resources[nameof(otherActionsMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.otherActionsButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.otherActionsButton, false);
					this.otherActionsButton.IsChecked = true;
				});
			});
			this.workingDirectoryActionsMenu = ((ContextMenu)this.Resources[nameof(workingDirectoryActionsMenu)].AsNonNull()).Also(it =>
			{
				it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.workingDirectoryActionsButton.IsChecked = false);
				it.Opened += (_, _) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.workingDirectoryActionsButton, false);
					this.workingDirectoryActionsButton.IsChecked = true;
				});
			});
			foreach (var item in this.logActionMenu.Items)
			{
				switch ((item as Control)?.Name)
				{
					case nameof(copyLogPropertyMenuItem):
						this.copyLogPropertyMenuItem = (MenuItem)item;
						break;
					case nameof(filterByLogPropertyMenuItem):
						this.filterByLogPropertyMenuItem = (MenuItem)item;
						break;
					case nameof(searchLogPropertyOnInternetMenuItem):
						this.searchLogPropertyOnInternetMenuItem = ((MenuItem)item).Also(it =>
						{
							var providers = Net.SearchProviderManager.Default.Providers;
							var subItems = new MenuItem[providers.Count];
							for (var i = 0; i < providers.Count; ++i)
							{
								var provider = providers[i];
								var commandBindingToken = default(IDisposable);
								var headerBindingToken = default(IDisposable);
								var iconBindingToken = default(IDisposable);
								subItems[i] = new MenuItem().Also(subItem =>
								{
									var iconImage = new Avalonia.Controls.Image().Also(it =>
									{
										it.Classes.Add("MenuItem_Icon");
									});
									subItem.CommandParameter = provider;
									subItem.Icon = iconImage;
									subItem.AttachedToLogicalTree += (_, _) =>
									{
										if (this.DataContext is Session session)
											commandBindingToken = subItem.Bind(MenuItem.CommandProperty, new Binding() { Path = nameof(Session.SearchLogPropertyOnInternetCommand), Source = session });
										headerBindingToken = subItem.Bind(MenuItem.HeaderProperty, new Binding() { Path = nameof(Net.SearchProvider.Name), Source = provider });
										iconBindingToken = iconImage.Bind(Avalonia.Controls.Image.SourceProperty, new Binding() { Source = provider, Converter = Converters.SearchProviderIconConverters.Default });
									};
									subItem.DetachedFromLogicalTree += (_, _) =>
									{
										commandBindingToken = commandBindingToken.DisposeAndReturnNull();
										headerBindingToken = headerBindingToken.DisposeAndReturnNull();
										iconBindingToken = iconBindingToken.DisposeAndReturnNull();
									};
								});
							}
							it.ItemsSource = subItems;
						});
						break;
					case nameof(showLogPropertyMenuItem):
						this.showLogPropertyMenuItem = (MenuItem)item;
						break;
				}
			}
			this.copyLogPropertyMenuItem.AsNonNull();
			this.filterByLogPropertyMenuItem.AsNonNull();
			this.searchLogPropertyOnInternetMenuItem.AsNonNull();
			this.showLogPropertyMenuItem.AsNonNull();
			
			// create scheduled actions
			this.autoAddLogFilesAction = new(async () =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders || this.isAutoSettingLogsReadingParameters)
					return;
				this.isLogFileNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.AddLogFilesAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.autoCloseSidePanelAction = new(() =>
			{
				if (this.DataContext is not Session session)
					return;
				if (this.keepSidePanelVisible)
					this.keepSidePanelVisible = false;
				else if (this.logListBoxContainer.Bounds.Width <= this.minLogListBoxSizeToCloseSidePanel)
				{
					session.LogAnalysis.IsPanelVisible = false;
					session.IsLogFilesPanelVisible = false;
					session.IsMarkedLogsPanelVisible = false;
					session.LogCategorizing.IsTimestampCategoriesPanelVisible = false;
				}
			});
			this.autoSetCommandAction = new(async () =>
			{
				if (this.DataContext is not Session || this.isAutoSettingLogsReadingParameters)
					return;
				this.isCommandNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.SelectAndSetCommandAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.autoSetIPEndPointAction = new(async () =>
			{
				if (this.DataContext is not Session || this.isAutoSettingLogsReadingParameters)
					return;
				this.isIPEndPointNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.SelectAndSetIPEndPointAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.autoSetProcessIdAction = new(async () =>
			{
				if (this.DataContext is not Session || this.isAutoSettingLogsReadingParameters)
					return;
				this.isProcessIdNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.SelectAndSetProcessIdAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.autoSetProcessNameAction = new(async () =>
			{
				if (this.DataContext is not Session || this.isAutoSettingLogsReadingParameters)
					return;
				this.isProcessNameNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.SelectAndSetProcessNameAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.autoSetUriAction = new(async () =>
			{
				if (this.DataContext is not Session || this.isAutoSettingLogsReadingParameters)
					return;
				this.isUriNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.SelectAndSetUriAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.autoSetWorkingDirectoryAction = new(async () =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders || this.isAutoSettingLogsReadingParameters)
					return;
				this.isWorkingDirNeededAfterLogProfileSet = false;
				this.isAutoSettingLogsReadingParameters = true;
				await this.SelectAndSetWorkingDirectoryAsync();
				this.isAutoSettingLogsReadingParameters = false;
				this.AutoSelectAndSetLogsReadingParameters();
			});
			this.commitLogFiltersAction = new(this.CommitLogFilters);
			this.handleDisplayLogPropertiesChangedAction = new(this.OnDisplayLogPropertiesChanged);
			this.scrollToLatestLogAction = new(() =>
			{
				this.smoothScrollToLatestLogAction!.Cancel();
				this.ScrollToLatestLog(false);
			});
			this.scrollToLatestLogAnalysisResultAction = new(() =>
			{
				this.smoothScrollToLatestLogAnalysisResultAction!.Cancel();
				this.ScrollToLatestLogAnalysisResult(false);
			});
			this.scrollToTargetLogRangeAction = new(this.ScrollToTargetLogRange);
			this.smoothScrollToLatestLogAction = new(() =>
			{
				this.scrollToLatestLogAction.Cancel();
				this.ScrollToLatestLog(true);
			});
			this.smoothScrollToLatestLogAnalysisResultAction = new(() =>
			{
				this.scrollToLatestLogAnalysisResultAction.Cancel();
				this.ScrollToLatestLogAnalysisResult(true);
			});
			this.startLogChartAnimationsAction = new(this.StartLogChartAnimations);
			this.updateCanEditCurrentScriptLogDataSourceProviderAction = new(() =>
			{
				var session = this.DataContext as Session;
				var logProfile = session?.LogProfile;
				if (logProfile?.IsBuiltIn != false || !session!.IsScriptLogDataSourceProvider)
				{
					this.SetAndRaise(CanEditCurrentScriptLogDataSourceProviderProperty, ref this.canEditCurrentScriptLogDataSourceProvider, false);
					return;
				}
				this.SetAndRaise(CanEditCurrentScriptLogDataSourceProviderProperty,
					ref this.canEditCurrentScriptLogDataSourceProvider,
					!session.IsEmbeddedScriptLogDataSourceProvider
					|| session.IsProVersionActivated);
			});
			this.updateLogAnalysisAction = new(() =>
			{
				if (this.DataContext is not Session session)
					return;
				var selectedKlaRuleSets = this.selectedKeyLogAnalysisRuleSets.ToArray();
				var selectedOcaRuleSets = this.selectedOperationCountingAnalysisRuleSets.ToArray();
				var selectedOdaRuleSets = this.selectedOperationDurationAnalysisRuleSets.ToArray();
				var selectedLaScriptSets = this.selectedLogAnalysisScriptSets.ToArray();
				session.LogAnalysis.KeyLogAnalysisRuleSets.Clear();
				session.LogAnalysis.KeyLogAnalysisRuleSets.AddAll(selectedKlaRuleSets);
				session.LogAnalysis.OperationDurationAnalysisRuleSets.Clear();
				session.LogAnalysis.OperationDurationAnalysisRuleSets.AddAll(selectedOdaRuleSets);
				session.LogAnalysis.OperationCountingAnalysisRuleSets.Clear();
				session.LogAnalysis.OperationCountingAnalysisRuleSets.AddAll(selectedOcaRuleSets);
				session.LogAnalysis.LogAnalysisScriptSets.Clear();
				session.LogAnalysis.LogAnalysisScriptSets.AddAll(selectedLaScriptSets);
			});
			this.updateLogChartXAxisLimitAction = new(this.UpdateLogChartXAxisLimit);
			this.updateLogChartYAxisLimitAction = new(this.UpdateLogChartYAxisLimit);
			this.updateLatestDisplayedLogRangeAction = new(this.UpdateLatestDisplayedLogRange);
			this.updateLogHeaderContainerMarginAction = new(() =>
			{
				var logScrollViewer = this.logScrollViewer;
				if (logScrollViewer != null)
					this.logHeaderContainer.Margin = new Thickness(-logScrollViewer.Offset.X, 0, Math.Min(0, logScrollViewer.Offset.X + logScrollViewer.Viewport.Width - logScrollViewer.Extent.Width), 0);
			});
			this.updateLogItemHeightAction = new(() =>
			{
				var fontSizeDiff = this.Settings.GetValueOrDefault(SettingKeys.LogFontSize) - SettingKeys.LogFontSize.DefaultValue;
				this.SetValue(LogItemHeightProperty, this.FindResourceOrDefault<double>("Double/SessionView.LogListBox.Item.Height") + fontSizeDiff);
			});
			this.updateStatusBarStateAction = new(() =>
			{
				this.SetValue(StatusBarStateProperty, Global.Run(() =>
				{
					if (this.DataContext is not Session session)
						return SessionViewStatusBarState.None;
					if (session.IsHighMemoryUsageToStopReadingLogs)
						return SessionViewStatusBarState.Warning;
					if (session.IsLogsReadingPaused)
						return SessionViewStatusBarState.Paused;
					if (session.HasPartialDataSourceErrors)
						return SessionViewStatusBarState.Warning;
					if (session.HasAllDataSourceErrors)
						return SessionViewStatusBarState.Error;
					if (session.HasLogReaders)
					{
						if (session.IsReadingLogs || !session.IsReadingLogsContinuously)
							return SessionViewStatusBarState.Active;
					}
					return SessionViewStatusBarState.None;
				}));
			});
			
			// setup axes of log chart
			this.UpdateLogChartAxes();
			
			// complete
			this.isInitializing = false;
		}


		// Add log files.
		async Task AddLogFilesAsync()
		{
			// check state
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to add log files without attaching to window");
				return;
			}
			if (!this.isNotAddingLogFiles.Value)
				return;

			// cancel scheduled action
			this.autoAddLogFilesAction.Cancel();

			// select files
			var fileNames = (await this.attachedWindow.StorageProvider.OpenFilePickerAsync(new()
			{
				AllowMultiple = true,
				Title = this.Application.GetString("SessionView.AddLogFiles"),
			})).Let(it =>
			{
				var fileNameList = new List<string>();
				foreach (var file in it)
				{
					var fileName = file.TryGetLocalPath();
					if (!string.IsNullOrEmpty(fileName))
						fileNameList.Add(fileName);
				}
				return fileNameList;
			});
			if (fileNames.IsNullOrEmpty())
				return;
			
			// add log files
			await this.AddLogFilesAsync(fileNames);
		}


		// Add log files.
		async Task<bool> AddLogFilesAsync(IEnumerable<string> fileNames)
		{
			// check state
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to add log files without attaching to window");
				return false;
			}
			if (!this.isNotAddingLogFiles.Value)
				return false;
			if (this.DataContext is not Session session)
				return false;

			// cancel scheduled action
			this.autoAddLogFilesAction.Cancel();

			// update state
			this.isNotAddingLogFiles.Update(false);

			// add files
			try
			{
				// exclude added files
				var fileNameList = new List<string>(fileNames);
				fileNameList.RemoveAll(session.IsLogFileAdded);

				// exclude .ulvmark files
				fileNameList.RemoveAll(it => it.EndsWith(".ulvmark", Platform.IsWindows, CultureInfo.InvariantCulture));
				if (fileNameList.IsEmpty())
					return false;
				
				// check file count
				if (session.LogFiles.Count + fileNameList.Count > session.MaxLogFileCount)
				{
					await new MessageDialog
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.Application.GetObservableString("SessionView.WillExceedMaxLogFileCount"),
					}.ShowDialog(this.attachedWindow);
					return false;
				}
				
				// sort file names
				fileNameList.Sort();
				
				// select precondition
				var precondition = this.Settings.GetValueOrDefault(SettingKeys.SelectLogReadingPreconditionForFiles) 
					? (await this.SelectLogReadingPreconditionAsync(LogDataSourceType.File, session.LastLogReadingPrecondition, false)).GetValueOrDefault()
					: new Logs.LogReadingPrecondition();
				if (this.attachedWindow == null || this.DataContext != session)
					return false;

				// add log files
				foreach (var fileName in fileNameList)
				{
					session.AddLogFileCommand.Execute(new Session.LogFileParams
					{
						FileName = fileName,
						Precondition = precondition,
					});
				}
				return true;
			}
			finally
			{
				this.isNotAddingLogFiles.Update(true);
			}
		}


		/// <summary>
		/// Command to add log files.
		/// </summary>
		public ICommand AddLogFilesCommand { get; }


		/// <summary>
		/// Check whether all tutorials are shown or not.
		/// </summary>
		public bool AreAllTutorialsShown => this.areAllTutorialsShown;


		// Attach to session.
		void AttachToSession(Session session)
		{
			this.Logger.LogWarning("Attaching to session {sessionId}", session.Id);
			
			// bind to logs
			this.logsBindingToken = this.logsBindingToken.DisposeAndReturnNull();
			this.logsBindingToken = this.logListBox.Bind(Avalonia.Controls.ListBox.ItemsSourceProperty, new Binding { Path = nameof(Session.Logs)} ); 
			
			// add event handler
			session.DebugMessageGenerated += this.OnDebugMessageGeneratedBySession;
			session.ErrorMessageGenerated += this.OnErrorMessageGeneratedBySession;
			session.ExternalDependencyNotFound += this.OnExternalDependencyNotFound;
			session.LogDataSourceScriptRuntimeErrorOccurred += this.OnLogDataSourceScriptRuntimeErrorOccurred;
			session.LogsSavingCompleted += this.OnLogsSavingCompleted;
			session.PropertyChanged += this.OnSessionPropertyChanged;
			this.attachedLogs = session.Logs as INotifyCollectionChanged;
			if (this.attachedLogs != null)
				this.attachedLogs.CollectionChanged += this.OnLogsChanged;
			(session.MarkedLogs as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnMarkedLogsChanged);
			this.AttachToLogAnalysis(session.LogAnalysis);
			this.AttachToLogFiltering(session.LogFiltering);
			this.AttachToLogChart(session.LogChart);
			
			// attach to properties
			var isAttaching = true;
			this.logAnalysisPanelVisibilityObserverToken = session.LogAnalysis.GetValueAsObservable(LogAnalysisViewModel.IsPanelVisibleProperty).Subscribe(_ =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(LogAnalysisViewModel.IsPanelVisibleProperty);
			});
			this.logFilesPanelVisibilityObserverToken = session.GetValueAsObservable(Session.IsLogFilesPanelVisibleProperty).Subscribe(_ =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(Session.IsLogFilesPanelVisibleProperty);
			});
			this.markedLogsPanelVisibilityObserverToken = session.GetValueAsObservable(Session.IsMarkedLogsPanelVisibleProperty).Subscribe(_ =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(Session.IsMarkedLogsPanelVisibleProperty);
			});
			this.timestampCategoryPanelVisibilityObserverToken = session.LogCategorizing.GetValueAsObservable(LogCategorizingViewModel.IsTimestampCategoriesPanelVisibleProperty).Subscribe(_ =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(LogCategorizingViewModel.IsTimestampCategoriesPanelVisibleProperty);
			});
			isAttaching = false;

			// check profile
			var profile = session.LogProfile;
			if (profile != null)
			{
				this.canEditLogProfile.Update(true);
				this.SetValue(HasLogProfileProperty, true);
				this.OnLogProfileSet(profile);
			}

			// attach to command
			this.canAddLogFilesToSession.Bind(session.AddLogFileCommand, new Session.LogFileParams());
			this.canReloadLogs.Bind(session.ReloadLogsCommand);
			this.canResetLogProfileToSession.Bind(session.ResetLogProfileCommand);
			this.canSaveLogs.Bind(session.SaveLogsCommand, new LogsSavingOptions(Array.Empty<DisplayableLog>()));
			this.canSetCommand.Bind(session.SetCommandCommand, new Session.CommandParams());
			this.canSetIPEndPoint.Bind(session.SetIPEndPointCommand, new IPEndPoint(IPAddress.Loopback, 80));
			this.canSetLogProfileToSession.Bind(session.SetLogProfileCommand, LogProfileManager.Default.EmptyProfile);
			this.canSetProcessId.Bind(session.SetProcessIdCommand, 0);
			this.canSetProcessName.Bind(session.SetProcessNameCommand, "");
			this.canSetUri.Bind(session.SetUriCommand, new Uri("dummy://localhost"));
			this.canSetWorkingDirectory.Bind(session.SetWorkingDirectoryCommand, "");
			this.canShowWorkingDirectoryInExplorer.Update(Platform.IsOpeningFileManagerSupported && session.HasWorkingDirectory);

			// start auto scrolling
			session.LogProfile?.Let(profile =>
			{
				if (!profile.IsContinuousReading)
					this.IsScrollingToLatestLogNeeded = false;
			});
			if (this.IsScrollingToLatestLogNeeded)
			{
				this.isSmoothScrollingToLatestLog = false;
				this.scrollToLatestLogAction.Schedule(InitScrollingToLatestLogDelay);
				this.smoothScrollToLatestLogAction.Cancel();
			}

			// sync side panel state
			if (session.LogAnalysis.IsPanelVisible 
				|| session.IsMarkedLogsPanelVisible 
				|| session.IsLogFilesPanelVisible
				|| session.LogCategorizing.IsTimestampCategoriesPanelVisible)
			{
				// select proper width of side panel
				sidePanelColumn.Width = new GridLength(new[] {
					session.LogAnalysis.PanelSize,
					session.LogFilesPanelSize,
					session.MarkedLogsPanelSize,
					session.LogCategorizing.TimestampCategoriesPanelSize,
				}.Max());
				Grid.SetColumnSpan(this.logListBoxContainer, 1);
				
				// make sure that only one panel is visible
				if (session.IsMarkedLogsPanelVisible)
				{
					session.LogCategorizing.IsTimestampCategoriesPanelVisible = false;
					session.LogAnalysis.IsPanelVisible = false;
					session.IsLogFilesPanelVisible = false;
				}
				else if (session.LogCategorizing.IsTimestampCategoriesPanelVisible)
				{
					session.LogAnalysis.IsPanelVisible = false;
					session.IsLogFilesPanelVisible = false;
				}
				else if (session.LogAnalysis.IsPanelVisible)
					session.IsLogFilesPanelVisible = false;
			}
			else
			{
				sidePanelColumn.Width = new GridLength(0);
				Grid.SetColumnSpan(this.logListBoxContainer, 3);
			}

			// update properties
			this.updateCanEditCurrentScriptLogDataSourceProviderAction.Schedule();
			this.validLogLevels.AddAll(session.ValidLogLevels);
			this.logProfileSelectionMenu.CurrentLogProfile = session.LogProfile;

			// update UI
			this.handleDisplayLogPropertiesChangedAction.Execute();
			this.updateStatusBarStateAction.Schedule();
		}
		
		
		// Show UI automatically to let user set parameters for logs reading.
		void AutoSelectAndSetLogsReadingParameters()
		{
			if (this.attachedWindow is null || this.isAutoSettingLogsReadingParameters)
				return;
			if (this.canSetCommand.Value && this.isCommandNeededAfterLogProfileSet)
				this.autoSetCommandAction.Schedule();
			else if (this.canSetIPEndPoint.Value && this.isIPEndPointNeededAfterLogProfileSet)
				this.autoSetIPEndPointAction.Schedule();
			else if (this.canAddLogFiles.Value && this.isLogFileNeededAfterLogProfileSet)
				this.autoAddLogFilesAction.Schedule();
			else if (this.canSetUri.Value && this.isUriNeededAfterLogProfileSet)
				this.autoSetUriAction.Schedule();
			else if (this.canSetWorkingDirectory.Value && this.isWorkingDirNeededAfterLogProfileSet)
				this.autoSetWorkingDirectoryAction.Schedule();
			else if (this.canSetProcessId.Value && this.isProcessIdNeededAfterLogProfileSet)
				this.autoSetProcessIdAction.Schedule();
			else if (this.canSetProcessName.Value && this.isProcessNameNeededAfterLogProfileSet)
				this.autoSetProcessNameAction.Schedule();
		}


		// Calculate file size.
		static Task<long> CalculateTotalFileSizeAsync(IEnumerable<string> fileNames, CancellationToken cancellationToken) => Task.Run(() =>
		{
			long size = 0;
			foreach (var fileName in fileNames)
			{
				if (cancellationToken.IsCancellationRequested)
					throw new TaskCanceledException();
				try
				{
					size += new FileInfo(fileName).Length;
				}
				// ReSharper disable EmptyGeneralCatchClause
				catch
				{ }
				// ReSharper restore EmptyGeneralCatchClause
			}
			return size;
		}, cancellationToken);


		// Clear target log range to scroll to.
		void ClearTargetLogRangeToScrollTo()
		{
			if (this.targetLogRangeToScrollTo == null)
				return;
			this.Logger.LogWarning("Clear target range of log to scroll to");
			this.latestTargetLogRangeScrollingDirection = 0;
			this.targetLogRangeScrollingDirectionChangeCount = 0;
			this.targetLogRangeToScrollTo = null;
			this.targetMarkedLogsToScrollTo.Clear();
			this.scrollToTargetLogRangeAction.Cancel();
			this.SetValue(IsScrollingToTargetLogRangeProperty, false);
		}


		/// <summary>
		/// Check whether editing current script log data source provider is allowed or not.
		/// </summary>
		public bool CanEditCurrentScriptLogDataSourceProvider => this.canEditCurrentScriptLogDataSourceProvider;


		// Enable script running if needed.
		async Task<bool> ConfirmEnablingRunningScript(LogProfile logProfile)
		{
			if (logProfile.DataSourceProvider is not ScriptLogDataSourceProvider)
				return true;
			if (this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript))
				return true;
			if (this.attachedWindow == null)
				return false;
			await new MessageDialog()
			{
				Icon = MessageDialogIcon.Information,
				Message = new FormattedString().Also(it =>
				{
					it.Arg1 = logProfile.Name;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.NeedToEnableRunningScript"));
				}),
			}.ShowDialog(this.attachedWindow);
			if (this.attachedWindow == null)
				return false;
			return await new EnableRunningScriptDialog().ShowDialog(this.attachedWindow);
		}


		// Confirm log reading window and limitation.
		Task<(bool, Logs.LogReadingWindow, int?)> ConfirmMaxLogReadingCountFromLargeFilesAsync(long fileSize) =>
			Task.FromResult<(bool, Logs.LogReadingWindow, int?)>(default);


		// Show UI to confirm removing log analysis rule set.
		async Task<bool> ConfirmRemovingLogAnalysisRuleSetAsync(string? ruleSetName, bool isScriptSet)
		{
			if (this.attachedWindow == null)
				return false;
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				DefaultResult = MessageDialogResult.No,
				Icon = MessageDialogIcon.Question,
				Message = isScriptSet
					? new FormattedString().Also(it =>
					{
						it.Arg1 = ruleSetName;
						it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.ConfirmRemovingLogAnalysisScriptSet"));
					})
					: new FormattedString().Also(it =>
					{
						it.Arg1 = ruleSetName;
						it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.ConfirmRemovingLogAnalysisRuleSet"));
					}),
			}.ShowDialog(this.attachedWindow);
			return (result == MessageDialogResult.Yes);
		}


		// Show dialog and let user choose whether to restart as administrator for given log profile.
		void ConfirmRestartingAsAdmin()
		{
			if (this.isRestartingAsAdminConfirmed || !this.isAttachedToLogicalTree)
				return;
			if (this.DataContext is not Session session)
				return;
			var profile = session.LogProfile;
			if (profile != null && profile.IsAdministratorNeeded && !this.Application.IsRunningAsAdministrator)
			{
				this.isRestartingAsAdminConfirmed = true;
				this.SynchronizationContext.PostDelayed(async () =>
				{
					if (this.DataContext == session && session.LogProfile == profile)
					{
						if (await this.ConfirmRestartingAsAdmin(profile))
							this.RestartAsAdministrator();
						else
							this.Logger.LogWarning("Unable to use profile '{profileName}' because application is not running as administrator", profile.Name);
					}
				}, 1000); // Delay to make sure that owner window has been shown
			}
		}
		async Task<bool> ConfirmRestartingAsAdmin(LogProfile profile)
		{
			// check state
			if (!profile.IsAdministratorNeeded || this.Application.IsRunningAsAdministrator)
				return false;
			if (this.attachedWindow == null)
				return false;

			// show dialog
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				Icon = MessageDialogIcon.Question,
				Message = new FormattedString().Also(it =>
				{
					it.Bind(FormattedString.Arg1Property, new Binding() { Path = nameof(LogProfile.Name), Source = profile });
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.NeedToRestartAsAdministrator"));
				}),
			}.ShowDialog(this.attachedWindow);
			if (result == MessageDialogResult.Yes)
			{
				this.Logger.LogWarning("User agreed to restart as administrator for '{profileName}'", profile.Name);
				return true;
			}
			this.Logger.LogWarning("User denied to restart as administrator for '{profileName}'", profile.Name);
			return false;
		}


		// Copy file name of log file.
		void CopyLogFileName(string filePath)
		{
			try
			{
				_ = this.attachedWindow?.Clipboard?.SetTextAsync(Path.GetFileName(filePath));
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to copy file name clipboard");
			}
		}


		/// <summary>
		/// Command to copy file name of log file.
		/// </summary>
		public ICommand CopyLogFileNameCommand { get; }


		// Copy file path of log file.
		void CopyLogFilePath(string filePath)
		{
			try
			{
				_ = this.attachedWindow?.Clipboard?.SetTextAsync(filePath);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to copy file name clipboard");
			}
		}


		/// <summary>
		/// Command to copy file path of log file.
		/// </summary>
		public ICommand CopyLogFilePathCommand { get; }


		// Copy property of log.
		void CopyLogProperty()
		{
			// check state
			if (!this.canCopyLogProperty.Value)
				return;
			var selection = this.logListBox.SelectedItems;
			if (selection is null || selection.Count == 0)
				return;

			// find property
			var clickedPropertyView = this.lastClickedLogPropertyView;
			if (clickedPropertyView == null || clickedPropertyView.Tag is not DisplayableLogProperty property)
				return;
			var listBoxItem = clickedPropertyView.FindLogicalAncestorOfType<ListBoxItem>();
			if (listBoxItem == null)
				return;

			// copy
			this.CopyLogProperty(selection.Cast<DisplayableLog>(), property);
		}
		void CopyLogProperty(IList<DisplayableLog> logs, DisplayableLogProperty property)
		{
			// check state
			if (logs.IsEmpty())
				return;
			
			// copy
			if (logs.Count == 1)
			{
				// get property value
				if (!logs[0].TryGetProperty<object?>(property.Name, out var value) || value is null)
					return;

				// copy value
				this.attachedWindow?.Clipboard?.SetTextAsync(value.ToString() ?? "");
			}
			else
			{
				// collect property values
				var buffer = new StringBuilder();
				foreach (var log in logs)
				{
					if (!log.TryGetProperty<object?>(property.Name, out var value) || value is null)
						continue;
					if (buffer.Length > 0)
						buffer.AppendLine();
					buffer.Append(value);
				}
				
				// copy value
				this.attachedWindow?.Clipboard?.SetTextAsync(buffer.ToString());
			}
		}


		/// <summary>
		/// Command to copy property of log.
		/// </summary>
		public ICommand CopyLogPropertyCommand { get; }


		// Copy text value of selected log.
		void CopyLogText()
		{
			// check state
			if (this.logListBox.SelectedItems?.Count != 1)
				return;
			if (this.logListBox.SelectedItem is not DisplayableLog log)
				return;
			if (this.DataContext is not Session session)
				return;
			var logProperties = session.DisplayLogProperties;
			if (logProperties.IsEmpty())
				return;
			
			// show tutorial
			if (!this.PersistentState.GetValueOrDefault(IsCopyLogTextTutorialShownKey)
				&& this.attachedWindow != null)
			{
				this.PersistentState.SetValue<bool>(IsCopyLogTextTutorialShownKey, true);
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Information,
					Message = this.Application.GetObservableString("SessionView.Tutorial.CopyLogText"),
				}.ShowDialog(this.attachedWindow);
			}
			
			// generate text
			var textBuffer = new StringBuilder();
			for (int i = 0, count = logProperties.Count; i < count; ++i)
			{
				if (i > 0)
					textBuffer.Append("$$");
				//var name = 
				var getter = DisplayableLog.CreateLogPropertyGetter<object?>(logProperties[i].Name);
				textBuffer.Append(getter(log));
			}

			// put to clipboard
			try
			{
				_ = this.attachedWindow?.Clipboard?.SetTextAsync(textBuffer.ToString());
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Failed to set text to clipboard");
			}
		}


		/// <summary>
		/// Command to copy log text.
		/// </summary>
		public ICommand CopyLogTextCommand { get; }


		// Generate pattern to find leading timestamp.
		[GeneratedRegex(@"^\s*([0-9\-/]+\s+)?([0-9:\-]+(\.[0-9]+)?(\s+[\+\-][0-9:]+)?)?(\s+|$)")]
		private static partial Regex CreateLeadingTimestampPattern();


		// Create item template for item of log list box.
		IDataTemplate CreateLogItemTemplate(LogProfile profile, IList<DisplayableLogProperty> logProperties)
		{
			// load resources
			var app = (App)this.Application;
			var logPropertyCount = logProperties.Count;
			var toolTipTemplate = (DataTemplate)this.Resources["logPropertyToolTipTemplate"].AsNonNull();
			var colorIndicatorBorderBrush = app.FindResourceOrDefault<IBrush?>("Brush/WorkingArea.Background");
			var colorIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.ColorIndicator.Border");
			var colorIndicatorWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.ColorIndicator.Width");
			var markIndicatorSize = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.MarkIndicator.Size");
			var markIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.MarkIndicator.Border");
			var markIndicatorCornerRadius = app.FindResourceOrDefault<CornerRadius>("CornerRadius/SessionView.LogListBox.MarkIndicator.Border");
			var markIndicatorMargin = app.FindResourceOrDefault("Thickness/SessionView.LogListBox.MarkIndicator.Margin", new Thickness(1));
			var markIndicatorWidth = (markIndicatorSize + markIndicatorMargin.Left + markIndicatorMargin.Right);
			var analysisResultIndicatorSize = 0.0;
			var analysisResultIndicatorMargin = new Thickness();
			var analysisResultIndicatorWidth = (analysisResultIndicatorSize + analysisResultIndicatorMargin.Left + analysisResultIndicatorMargin.Right);
			var multiLineIndicatorMargin = app.FindResourceOrDefault("Thickness/SessionView.LogListBox.MultiLineIndicator.Margin", new Thickness());
			var multiLineIndicatorOpacity = app.FindResourceOrDefault("Double/SessionView.LogListBox.MultiLineIndicator.Opacity", 0.5);
			var itemBorderThickness = app.FindResourceOrDefault("Thickness/SessionView.LogListBox.Item.Column.Border", new Thickness(1));
			var itemCornerRadius = app.FindResourceOrDefault<CornerRadius>("CornerRadius/SessionView.LogListBox.Item.Column.Border");
			var itemPadding = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.Item.Padding");
			var itemMaxWidth = app.FindResourceOrDefault("Double/SessionView.LogListBox.Item.MaxWidth", double.NaN);
			var propertyPadding = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.Item.Property.Padding");
			var splitterWidth = app.FindResourceOrDefault<double>("Double/GridSplitter.Thickness");
			var itemStartingContentWidth = (analysisResultIndicatorWidth + markIndicatorWidth);
			var hasColorIndicator = (profile.ColorIndicator != LogColorIndicator.None);
			if (hasColorIndicator)
				itemStartingContentWidth += colorIndicatorWidth + colorIndicatorBorderThickness.Left + colorIndicatorBorderThickness.Right;
			var levelForegroundBrush = app.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.Level.Foreground");
			var selectionIndicatorBrush = app.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.SelectionIndicator.Background");
			var iconBrush = app.FindResourceOrDefault<IBrush>("Brush/Icon");
			var logSeparators = app.Settings.GetValueOrDefault(SettingKeys.LogSeparators);
			var hasHorizontalSeparator = logSeparators switch
			{
				LogSeparatorType.Horizontal
					or LogSeparatorType.HorizontalAndVertical => true,
				_ => false,
			};
			var hasVerticalSeparator = logSeparators switch
			{
				LogSeparatorType.HorizontalAndVertical
					or LogSeparatorType.Vertical => true,
				_ => false,
			};
			var separatorBrush = logSeparators != LogSeparatorType.None ? app.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.Separator") : null;
			var trimLogPropertyText = app.Configuration.GetValueOrDefault(ConfigurationKeys.TrimLogPropertyViewText);
			
			// adjust margins and paddings
			itemPadding = new(
				itemPadding.Left + itemStartingContentWidth, 
				itemPadding.Top, 
				itemPadding.Right, 
				hasHorizontalSeparator ? itemPadding.Bottom + 1 : itemPadding.Bottom);
			
			// create template
			return new FuncDataTemplate(typeof(DisplayableLog), (_, _) =>
			{
				var itemPanel = new Panel().Also(it =>
				{
					it.Bind(Panel.BackgroundProperty, new Binding
					{
						Converter = Converters.MarkColorToBackgroundConverter.Default,
						Path = nameof(DisplayableLog.MarkedColor)
					});
				});
				var itemGrid = new Grid().Also(it =>
				{
					it.ClipToBounds = false;
					it.Margin = itemPadding;
					itemPanel.Children.Add(it);
				});
				
				// placeholder view at position 0
				itemPanel.Children.Add(new Border().Also(border =>
				{
					border.BorderThickness = itemBorderThickness;
					border.Child = new Avalonia.Controls.TextBlock().Also(it =>
					{
						it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
						it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding { Path = nameof(ControlFonts.LogFontSize), Source = ControlFonts.Default });
						it.Padding = propertyPadding;
						it.Text = "A";
					});
					border.Margin = new(-itemBorderThickness.Left / 2, -itemBorderThickness.Top / 2, -itemBorderThickness.Right / 2, -itemBorderThickness.Bottom / 2);
					border.Opacity = 0;
					border.VerticalAlignment = VerticalAlignment.Center;
				}));

				// property views
				var propertyColumns = new ColumnDefinition[logPropertyCount];
				var propertyColumnWidthBindingTokens = new IDisposable?[logPropertyCount];
				for (var i = 0; i < logPropertyCount; ++i)
				{
					// define splitter column
					var logPropertyIndex = i;
					if (logPropertyIndex > 0)
						itemGrid.ColumnDefinitions.Add(new ColumnDefinition(splitterWidth, GridUnitType.Pixel));

					// define property column
					var logProperty = logProperties[logPropertyIndex];
					var width = logProperty.Width;
					var isAutoWidth = width is null;
					var propertyColumn = new ColumnDefinition(new GridLength()).Also(it =>
					{
						if (isAutoWidth && logPropertyIndex == logPropertyCount - 1)
							it.Width = new GridLength(1, GridUnitType.Star);
					});
					propertyColumns[logPropertyIndex] = propertyColumn;
					itemGrid.ColumnDefinitions.Add(propertyColumn);

					// create property view
					var isStringProperty = DisplayableLog.HasStringProperty(logProperty.Name);
					var isMultiLineProperty = DisplayableLog.HasMultiLineStringProperty(logProperty.Name);
					var propertyView = logProperty.Name switch
					{
						nameof(DisplayableLog.LevelString) => (Control)new CarinaStudio.Controls.TextBlock().Also(it =>
						{
							it.Bind(BackgroundProperty, new Binding { Path = nameof(DisplayableLog.LevelBackgroundBrush) });
							it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
							it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding { Path = nameof(ControlFonts.LogFontSize), Source = ControlFonts.Default });
							it.DebugLogger = TextBlockDebugLogger;
							it.Foreground = levelForegroundBrush;
							it.MaxLines = 1;
							it.MaxWidth = itemMaxWidth;
							it.Padding = propertyPadding;
							it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding { Path = logProperty.Name });
							it.TextAlignment = TextAlignment.Center;
							it.TextTrimming = isAutoWidth ? TextTrimming.None : TextTrimming.CharacterEllipsis;
							it.TextWrapping = TextWrapping.NoWrap;
							it.ToolTipTemplate = toolTipTemplate;
							it.VerticalAlignment = VerticalAlignment.Center;
						}),
						_ => new SyntaxHighlightingTextBlock().Also(it =>
						{
							it.Bind(SyntaxHighlightingTextBlock.DefinitionSetProperty, new Binding { Path = nameof(DisplayableLog.TextHighlightingDefinitionSet) });
							it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
							it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding { Path = nameof(ControlFonts.LogFontSize), Source = ControlFonts.Default });
							it.DebugLogger = TextBlockDebugLogger;
							if (logProperty.ForegroundColor == LogPropertyForegroundColor.Level)
								it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding { Path = nameof(DisplayableLog.LevelForegroundBrush) });
							if (isMultiLineProperty)
								it.Bind(Avalonia.Controls.TextBlock.MaxLinesProperty, new Binding { Path = nameof(MaxDisplayLineCountForEachLog), Source = this });
							else
								it.MaxLines = 1;
							it.MaxWidth = itemMaxWidth;
							it.Padding = propertyPadding;
							if (logProperty.Name == nameof(DisplayableLog.Level))
							{
								it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding
								{
									Converter = Converters.EnumConverters.LogLevel,
									Path = logProperty.Name,
								});
							}
							else if (DisplayableLog.HasFrozenProperty(logProperty.Name))
							{
								it.DataContextChanged += (_, _) =>
								{
									if (it.DataContext is DisplayableLog log
									    && log.TryGetProperty<object?>(logProperty.Name, out var value))
									{
										it.Text = value?.ToString();
									}
									else
										it.Text = null;
								};
							}
							else
							{
								it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding
								{
									Path = logProperty.Name,
								});
							}
							it.TextTrimming = isAutoWidth || !trimLogPropertyText ? TextTrimming.None : TextTrimming.CharacterEllipsis;
							it.TextWrapping = TextWrapping.NoWrap;
							it.ToolTipTemplate = toolTipTemplate;
							it.VerticalAlignment = VerticalAlignment.Center;
						}),
					};
					if (propertyView is CarinaStudio.Controls.TextBlock 
					    && width.HasValue
					    && Platform.IsMacOS)
					{
						app.EnsureClosingToolTipIfWindowIsInactive(propertyView);
					}
					if (isMultiLineProperty)
					{
						propertyView = new DockPanel().Also(it =>
						{
							it.Background = Brushes.Transparent;
							it.Children.Add(new Avalonia.Controls.Image().Also(multiLineIcon =>
							{
								DockPanel.SetDock(multiLineIcon, Dock.Left);
								multiLineIcon.Classes.Add("Icon");
								multiLineIcon.Height = markIndicatorSize;
								multiLineIcon.IsHitTestVisible = true;
								multiLineIcon.Margin = multiLineIndicatorMargin;
								multiLineIcon.Bind(IsVisibleProperty, new Binding { Path = $"HasExtraLinesOf{logProperty.Name}" });
								multiLineIcon.Opacity = multiLineIndicatorOpacity;
								multiLineIcon.BindToResource(Avalonia.Controls.Image.SourceProperty, "Image/MultiLine");
								multiLineIcon.Width = markIndicatorSize;
								multiLineIcon.BindToResource(ToolTip.TipProperty, "String/SessionView.MultiLineLogProperty");
								multiLineIcon.VerticalAlignment = VerticalAlignment.Center;
								if (Platform.IsMacOS)
									app.EnsureClosingToolTipIfWindowIsInactive(multiLineIcon);
							}));
							it.Children.Add(propertyView);
							it.HorizontalAlignment = HorizontalAlignment.Stretch;
						});
					}
					propertyView = new Border().Also(it =>
					{
						it.Background = Brushes.Transparent;
						it.BorderThickness = itemBorderThickness;
						it.Child = propertyView;
						it.CornerRadius = itemCornerRadius;
						it.Tag = logProperty;
						it.VerticalAlignment = VerticalAlignment.Stretch;
						if (logSeparators != LogSeparatorType.HorizontalAndVertical)
						{
							it.GetObservable(IsPointerOverProperty).Subscribe(new Observer<bool>(isPointerOver =>
							{
								if (isPointerOver)
								{
									this.logPropertyViewBorderPointerOverBrush ??= this.Application.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.Column.Border.PointerOver");
									it.BorderBrush = this.logPropertyViewBorderPointerOverBrush;
									//it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrushForPointerOver) });
								}
								else
								{
									it.BorderBrush = null;
									//it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
								}
							}));
						}
						it.PointerPressed += (_, _) =>
						{
							var selectionCount = this.logListBox.SelectedItems?.Count ?? 0;
							this.lastClickedLogPropertyView = it;
							(this.DataContext as Session)?.LogSelection.Let(it => it.SelectedLogProperty = logProperty);
							this.canCopyLogProperty.Update(selectionCount > 0);
							this.canShowLogProperty.Update(selectionCount == 1 && isStringProperty);
						};
					});
					switch (logProperty.Name)
					{
						case nameof(DisplayableLog.ProcessId):
						case nameof(DisplayableLog.ThreadId):
						{
							var isSelectedValuePropertyName = logProperty.Name switch
							{
								nameof(DisplayableLog.ProcessId) => nameof(DisplayableLog.IsProcessIdSelected),
								nameof(DisplayableLog.ThreadId) => nameof(DisplayableLog.IsThreadIdSelected),
								_ => "",
							};
							propertyView.Bind(Border.BackgroundProperty, new MultiBinding().Also(binding =>
							{
								binding.Converter = this.selectableValueLogItemBackgroundConverter;
								binding.Bindings.Add(new Binding { Path = isSelectedValuePropertyName });
								binding.Bindings.Add(new Binding
								{ 
									Path = nameof(ListBoxItem.IsSelected), 
									RelativeSource = new(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBoxItem) } 
								});
							}));
							break;
						}
					}
					Grid.SetColumn(propertyView, logPropertyIndex * 2);
					itemGrid.Children.Add(propertyView);

					// create vertical separator
					if (hasVerticalSeparator && logPropertyIndex > 0)
					{
						new Border().Let(separator =>
						{
							separator.Background = separatorBrush;
							separator.HorizontalAlignment = HorizontalAlignment.Center;
							separator.VerticalAlignment = VerticalAlignment.Stretch;
							separator.Width = 1;
							Grid.SetColumn(separator, logPropertyIndex * 2 - 1);
							itemGrid.Children.Add(separator);
						});
					}
				}

				// color indicator
				if (hasColorIndicator)
				{
					itemPanel.Children.Add(new Border().Also(it =>
					{
						it.Bind(Border.BackgroundProperty, new Binding { Path = nameof(DisplayableLog.ColorIndicatorBrush) });
						it.BorderBrush = colorIndicatorBorderBrush;
						it.BorderThickness = colorIndicatorBorderThickness;
						it.HorizontalAlignment = HorizontalAlignment.Left;
						it.Bind(ToolTip.TipProperty, new Binding { Path = nameof(DisplayableLog.ColorIndicatorTip) });
						it.Width = colorIndicatorWidth;
						if (Platform.IsMacOS)
							app.EnsureClosingToolTipIfWindowIsInactive(it);
					}));
				}
				
				// mark indicator
				var markIndicatorSelectionBackground = default(Border);
				itemPanel.Children.Add(new Panel().Also(panel =>
				{
					var isLeftPointerDown = false;
					var isRightPointerDown = false;
					var isMenuOpen = false;
					markIndicatorSelectionBackground = new Border().Also(selectionBackgroundBorder =>
					{
						selectionBackgroundBorder.Background = selectionIndicatorBrush;
					});
					panel.Children.Add(markIndicatorSelectionBackground);
					var emptyMarker = new Border().Also(border =>
					{
						border.BorderBrush = iconBrush;
						border.BorderThickness = markIndicatorBorderThickness;
						border.CornerRadius = markIndicatorCornerRadius;
						border.Height = markIndicatorSize;
						border.IsVisible = false;
						border.Margin = markIndicatorMargin;
						border.Opacity = 0.5;
						border.VerticalAlignment = VerticalAlignment.Center;
						border.Width = markIndicatorSize;
					});
					panel.Background = Brushes.Transparent;
					panel.Children.Add(emptyMarker);
					panel.Children.Add(new Border().Also(border =>
					{
						border.Bind(Border.BackgroundProperty, new Binding { Path = nameof(DisplayableLog.MarkedColor), Converter = Converters.MarkColorToIndicatorConverter.Default });
						border.BorderBrush = iconBrush;
						border.BorderThickness = markIndicatorBorderThickness;
						border.CornerRadius = markIndicatorCornerRadius;
						border.Height = markIndicatorSize;
						border.Bind(IsVisibleProperty, new Binding { Path = nameof(DisplayableLog.IsMarked) });
						border.Margin = markIndicatorMargin;
						border.VerticalAlignment = VerticalAlignment.Center;
						border.Width = markIndicatorSize;
					}));
					panel.Cursor = new Cursor(StandardCursorType.Hand);
					panel.HorizontalAlignment = HorizontalAlignment.Left;
					panel.Margin = new(
                        hasColorIndicator ? colorIndicatorWidth : 0, 
                        0, 
                        0, 
                        hasHorizontalSeparator ? 1 : 0);
					panel.PointerEntered += (_, _) => emptyMarker.IsVisible = true;
					panel.PointerExited += (_, _) => emptyMarker.IsVisible = isMenuOpen;
					panel.PointerPressed += (_, e) =>
					{
						var properties = e.GetCurrentPoint(panel).Properties;
						isLeftPointerDown = properties.IsLeftButtonPressed;
						isRightPointerDown = properties.IsRightButtonPressed;
					};
					panel.AddHandler(PointerReleasedEvent, (_, e) =>
					{
						if (isLeftPointerDown)
						{
							if (this.DataContext is Session session && itemPanel.DataContext is DisplayableLog log)
							{
								if (log.IsMarked)
									session.UnmarkLogsCommand.TryExecute(new[] { log });
								else
								{
									if (this.sidePanelContainer.IsVisible && this.Settings.GetValueOrDefault(SettingKeys.SwitchToMarkedLogsPanelAfterMarkingLogs))
										session.IsMarkedLogsPanelVisible = true;
									session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams
									{
										Color = MarkColor.Default,
										Logs = new[] { log },
									});
								}
							}
						}
						else if (isRightPointerDown)
						{
							e.Handled = true;
							var closedHandler = (EventHandler<RoutedEventArgs>?)null;
							closedHandler = (_, _) =>
							{
								this.logMarkingMenu.Closed -= closedHandler;
								isMenuOpen = false;
								emptyMarker.IsVisible = panel.IsPointerOver;
							};
							isMenuOpen = true;
							emptyMarker.IsVisible = true;
							this.logMarkingMenu.Closed += closedHandler;
							this.logMarkingMenu.PlacementTarget = panel;
							this.logMarkingMenu.Open(panel);
						}
						isLeftPointerDown = false;
						isRightPointerDown = false;
					}, RoutingStrategies.Tunnel);
					panel.BindToResource(ToolTip.TipProperty, "String/SessionView.MarkUnmarkLog");
					panel.VerticalAlignment = VerticalAlignment.Stretch;
					panel.Width = markIndicatorWidth;
					if (Platform.IsMacOS)
						app.EnsureClosingToolTipIfWindowIsInactive(panel);
				}));
				
				// analysis result indicator
				itemPanel.Children.Add(new Border().Also(background =>
				{
					var isLeftPointerDown = false;
					background.Background = Brushes.Transparent;
					background.Cursor = new Cursor(StandardCursorType.Hand);
					background.Height = analysisResultIndicatorSize;
					background.HorizontalAlignment = HorizontalAlignment.Left;
					background.Bind(IsVisibleProperty, new Binding { Path = nameof(DisplayableLog.HasAnalysisResult) });
					background.Margin = analysisResultIndicatorMargin.Let(it =>
						new Thickness(
							it.Left + (hasColorIndicator ? colorIndicatorWidth : 0) + markIndicatorWidth, 
							it.Top, 
							it.Right, 
							(hasHorizontalSeparator ? 1 : 0) + it.Bottom));
					background.PointerPressed += (_, e) =>
						isLeftPointerDown = e.GetCurrentPoint(background).Properties.IsLeftButtonPressed;
					background.AddHandler(PointerReleasedEvent, (_, _) =>
					{
						if (isLeftPointerDown)
						{
							isLeftPointerDown = false;
							if (itemPanel.DataContext is DisplayableLog log)
								this.OnLogAnalysisResultIndicatorClicked(log);
						}
					}, RoutingStrategies.Tunnel);
					background.VerticalAlignment = VerticalAlignment.Center;
					background.Width = analysisResultIndicatorSize;
				}));
				itemPanel.Children.Add(new Avalonia.Controls.Image().Also(image =>
				{
					image.Classes.Add("Icon");
					image.Height = analysisResultIndicatorSize;
					image.HorizontalAlignment = HorizontalAlignment.Left;
					image.Bind(IsVisibleProperty, new Binding { Path = nameof(DisplayableLog.HasAnalysisResult) });
					image.Margin = analysisResultIndicatorMargin.Let(it =>
						new Thickness(it.Left + (hasColorIndicator ? colorIndicatorWidth : 0) + markIndicatorWidth, it.Top, it.Right, it.Bottom));
					image.Bind(Avalonia.Controls.Image.SourceProperty, new Binding { Path = nameof(DisplayableLog.AnalysisResultIndicatorIcon) });
					image.VerticalAlignment = VerticalAlignment.Center;
					image.Width = analysisResultIndicatorSize;
				}));
				
				// horizontal separator
				switch (logSeparators)
				{
					case LogSeparatorType.Horizontal:
					case LogSeparatorType.HorizontalAndVertical:
						itemPanel.Children.Add(new Border().Also(separator =>
						{
							separator.BorderBrush = separatorBrush;
							separator.BorderThickness = new(0, 0, 0, 1);
							separator.HorizontalAlignment = HorizontalAlignment.Stretch;
							separator.IsHitTestVisible = false;
							separator.VerticalAlignment = VerticalAlignment.Bottom;
						}));
						break;
				}

				// update according to selection of list box item
				var isSelectedObserverToken = EmptyDisposable.Default;
				itemPanel.AttachedToLogicalTree += (_, _) =>
				{
					// bind to ListBoxItem
					if (itemPanel.Parent is ListBoxItem listBoxItem)
					{
						isSelectedObserverToken = listBoxItem.GetObservable(ListBoxItem.IsSelectedProperty).Subscribe(isSelected =>
						{
							markIndicatorSelectionBackground!.IsVisible = isSelected;
						});
					}
					
					// bind to column widths
					for (var i = logPropertyCount - 1; i >= 0; --i)
					{
						if (logProperties[i].Width.HasValue || i != logPropertyCount - 1)
							propertyColumnWidthBindingTokens[i] = propertyColumns[i].Bind(ColumnDefinition.WidthProperty, this.logHeaderWidths[i]);
					}
				};
				itemPanel.DetachedFromLogicalTree += (_, _) =>
				{
					// clear binding from ListBoxItem
					isSelectedObserverToken.Dispose();

					// clear bindings to column widths
					for (var i = propertyColumnWidthBindingTokens.Length - 1; i >= 0; --i)
						propertyColumnWidthBindingTokens[i] = propertyColumnWidthBindingTokens[i].DisposeAndReturnNull();
				};

				// complete
				return itemPanel;
			}, true);
		}


		// Create item template for item of marked log list box.
		IDataTemplate CreateMarkedLogItemTemplate(Session session, LogProfile profile, IList<DisplayableLogProperty> logProperties)
		{
			// check visible properties
			var messageProperty = default(DisplayableLogProperty);
			var sourceNameProperty = default(DisplayableLogProperty);
			var summaryProperty = default(DisplayableLogProperty);
			var timeSpanStringProperty = default(DisplayableLogProperty);
			var timestampStringProperty = default(DisplayableLogProperty);
			var titleProperty = default(DisplayableLogProperty);
			var userIdProperty = default(DisplayableLogProperty);
			var userNameProperty = default(DisplayableLogProperty);
			foreach (var logProperty in logProperties)
			{
				switch (logProperty.Name)
				{
					case nameof(DisplayableLog.Message):
						messageProperty = logProperty;
						break;
					case nameof(DisplayableLog.SourceName):
						sourceNameProperty = logProperty;
						break;
					case nameof(DisplayableLog.Summary):
						summaryProperty = logProperty;
						break;
					case nameof(DisplayableLog.TimeSpanString):
						timeSpanStringProperty = logProperty;
						break;
					case nameof(DisplayableLog.TimestampString):
						timestampStringProperty = logProperty;
						break;
					case nameof(DisplayableLog.Title):
						titleProperty = logProperty;
						break;
					case nameof(DisplayableLog.UserId):
						userIdProperty = logProperty;
						break;
					case nameof(DisplayableLog.UserName):
						userNameProperty = logProperty;
						break;
				}
			}
			
			// select property to be shown in item
			var propertyInMarkedItem = Global.Run(() => 
				messageProperty
	            ?? summaryProperty
	            ?? titleProperty
	            ?? sourceNameProperty
	            ?? userNameProperty
	            ?? userIdProperty
	            ?? timestampStringProperty
	            ?? timeSpanStringProperty
	            ?? new DisplayableLogProperty(this.Application, nameof(DisplayableLog.LogId), null, null)
			);
			
			// load resources
			var app = (App)this.Application;
			var colorIndicatorBorderBrush = app.FindResourceOrDefault<IBrush?>("Brush/WorkingArea.Panel.Background");
			var colorIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.ColorIndicator.Border");
			var colorIndicatorWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.ColorIndicator.Width");
			var itemPadding = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.MarkedLogListBox.Item.Padding");
			if (profile.ColorIndicator != LogColorIndicator.None)
				itemPadding = new Thickness(itemPadding.Left + colorIndicatorWidth, itemPadding.Top, itemPadding.Right, itemPadding.Bottom);
			var itemBorderBrush = app.FindResourceOrDefault<IBrush>("Brush/SessionView.MarkedLogListBox.Item.Border.Selected");
			var itemBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.MarkedLogListBox.Item.Border.Selected");
			var itemCornerRadius = app.FindResourceOrDefault<CornerRadius>("CornerRadius/SessionView.MarkedLogListBox.Item.Border");
			
			// build item template for marked log list box
			return new FuncDataTemplate(typeof(DisplayableLog), (_, _) =>
			{
				var itemPanel = new Panel();
				var areDisplayLogPropertiesDefinedByLogProfile = session.AreDisplayLogPropertiesDefinedByLogProfile;
				new Border().Let(it =>
				{
					it.Bind(Border.BackgroundProperty, new Binding
					{
						Converter = Converters.MarkColorToBackgroundConverter.DefaultWithoutDefaultColor,
						Path = nameof(DisplayableLog.MarkedColor),
					});
					itemPanel.Children.Add(it);
				});
				new Avalonia.Controls.TextBlock().Let(it =>
				{
					// empty view to reserve height of item
					it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
					it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding { Path = nameof(ControlFonts.LogFontSize), Source = ControlFonts.Default });
					it.Margin = itemPadding;
					it.Opacity = 0;
					it.Text = " ";
					itemPanel.Children.Add(it);
				});
				var propertyView = timestampStringProperty is not null || !areDisplayLogPropertiesDefinedByLogProfile
					? new Avalonia.Controls.TextBlock()
					: new CarinaStudio.Controls.TextBlock();
				itemPanel.Children.Add(propertyView.Also(it =>
				{
					it.Background = Brushes.Transparent;
					it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
					it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding { Path = nameof(ControlFonts.LogFontSize), Source = ControlFonts.Default });
					if (propertyInMarkedItem.ForegroundColor == LogPropertyForegroundColor.Level)
						it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding { Path = nameof(DisplayableLog.LevelForegroundBrush) });
					if (DisplayableLog.HasFrozenProperty(propertyInMarkedItem.Name))
					{
						it.DataContextChanged += (_, _) =>
						{
							if (it.DataContext is DisplayableLog log
							    && log.TryGetProperty<object?>(propertyInMarkedItem.Name, out var value))
							{
								it.Text = areDisplayLogPropertiesDefinedByLogProfile
									? value?.ToString()
									: EliminateLeadingTimestampConverter.Convert<string>(value);
							}
							else
								it.Text = null;
						};
					}
					else
					{
						it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding
						{
							Converter = areDisplayLogPropertiesDefinedByLogProfile
								? null
								: EliminateLeadingTimestampConverter,
							Path = propertyInMarkedItem.Name,
						});
					}
					it.MaxLines = areDisplayLogPropertiesDefinedByLogProfile
						? 1
						: Math.Max(1, this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.MaxLinesOfMarkedLogWithoutDisplayLogProperties));
					it.Padding = itemPadding;
					it.Resources.Add("ToolTipContentMaxWidth", app.FindResourceOrDefault<double>("Double/SessionView.MarkedLogListBox.ToolTip.MaxWidth"));
					it.TextTrimming = TextTrimming.CharacterEllipsis;
					it.TextWrapping = areDisplayLogPropertiesDefinedByLogProfile
						? TextWrapping.NoWrap
						: TextWrapping.Wrap;
					it.VerticalAlignment = VerticalAlignment.Center;
					if (it is CarinaStudio.Controls.TextBlock csTextBlock)
					{
						csTextBlock.ToolTipTemplate = new FuncDataTemplate(typeof(object), (_, _) =>
						{
							return new Avalonia.Controls.TextBlock().Also(it =>
							{
								it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
								it.MaxLines = 8;
								it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding());
								it.TextTrimming = TextTrimming.CharacterEllipsis;
								it.TextWrapping = TextWrapping.Wrap;
							});
						});
					}
					else
					{
						ToolTip.SetTip(it, new Avalonia.Controls.TextBlock().Also(it =>
						{
							it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding { Path = nameof(ControlFonts.LogFontFamily), Source = ControlFonts.Default });
							it.MaxLines = 8;
							if (timestampStringProperty is not null)
								it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding { Path = timestampStringProperty.Name });
							else
								it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding { Path = propertyInMarkedItem.Name });
							it.TextTrimming = TextTrimming.CharacterEllipsis;
							it.TextWrapping = TextWrapping.Wrap;
						}));
					}
				}));
				itemPanel.Children.Add(new Border().Also(border =>
				{
					border.BorderBrush = itemBorderBrush;
					border.BorderThickness = itemBorderThickness;
					border.CornerRadius = itemCornerRadius;
					border.Bind(IsVisibleProperty, new Binding
					{
						RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBoxItem) },
						Path = nameof(ListBoxItem.IsSelected)
					});
					if (profile.ColorIndicator != LogColorIndicator.None)
						border.Margin = new Thickness(colorIndicatorWidth + colorIndicatorBorderThickness.Left + colorIndicatorBorderThickness.Right, 0, 0, 0);
				}));
				if (profile.ColorIndicator != LogColorIndicator.None)
				{
					new Border().Also(it =>
					{
						it.Bind(Border.BackgroundProperty, new Binding { Path = nameof(DisplayableLog.ColorIndicatorBrush) });
						it.BorderBrush = colorIndicatorBorderBrush;
						it.BorderThickness = colorIndicatorBorderThickness;
						it.HorizontalAlignment = HorizontalAlignment.Left;
						it.Bind(ToolTip.TipProperty, new Binding { Path = nameof(DisplayableLog.ColorIndicatorTip) });
						it.Width = colorIndicatorWidth;
						itemPanel.Children.Add(it);
					});
				}
				return itemPanel;
			}, true);
		}


		// Detach from session.
		void DetachFromSession(Session session)
		{
			// [Workaround] clear selection to prevent performance issue of de-select multiple items
			this.logListBox.SelectedItems?.Clear();
			
			// unbind from logs
			this.logsBindingToken = this.logsBindingToken.DisposeAndReturnNull();

			// remove event handler
			session.DebugMessageGenerated -= this.OnDebugMessageGeneratedBySession;
			session.ErrorMessageGenerated -= this.OnErrorMessageGeneratedBySession;
			session.ExternalDependencyNotFound -= this.OnExternalDependencyNotFound;
			session.LogDataSourceScriptRuntimeErrorOccurred -= this.OnLogDataSourceScriptRuntimeErrorOccurred;
			session.LogsSavingCompleted -= this.OnLogsSavingCompleted;
			session.PropertyChanged -= this.OnSessionPropertyChanged;
			if (this.attachedLogs != null)
			{
				this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
				this.attachedLogs = null;
			}
			(session.MarkedLogs as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnMarkedLogsChanged);
			this.DetachFromLogAnalysis(session.LogAnalysis);
			this.DetachFromLogFiltering(session.LogFiltering);
			this.DetachFromLogChart(session.LogChart);
			
			// detach from properties
			this.logAnalysisPanelVisibilityObserverToken = this.logAnalysisPanelVisibilityObserverToken.DisposeAndReturnNull();
			this.logFilesPanelVisibilityObserverToken = this.logFilesPanelVisibilityObserverToken.DisposeAndReturnNull();
			this.markedLogsPanelVisibilityObserverToken = this.markedLogsPanelVisibilityObserverToken.DisposeAndReturnNull();
			this.timestampCategoryPanelVisibilityObserverToken = this.timestampCategoryPanelVisibilityObserverToken.DisposeAndReturnNull();

			// detach from commands
			this.canAddLogFilesToSession.Unbind();
			this.canReloadLogs.Unbind();
			this.canResetLogProfileToSession.Unbind();
			this.canSetCommand.Unbind();
			this.canSetIPEndPoint.Unbind();
			this.canSaveLogs.Unbind();
			this.canSetLogProfileToSession.Unbind();
			this.canSetProcessId.Unbind();
			this.canSetProcessName.Unbind();
			this.canSetUri.Unbind();
			this.canSetWorkingDirectory.Unbind();
			this.canShowWorkingDirectoryInExplorer.Update(false);

			// update properties
			this.canEditLogProfile.Update(false);
			this.SetValue(HasLogProfileProperty, false);
			this.updateCanEditCurrentScriptLogDataSourceProviderAction.Execute();
			this.validLogLevels.Clear();
			this.logProfileSelectionMenu.CurrentLogProfile = null;

			// clear target range of log to scroll to
			this.ClearTargetLogRangeToScrollTo();

			// reset displayed log range info
			this.updateLatestDisplayedLogRangeAction.Execute();

			// stop auto scrolling
			this.lastLogUpdateTime = 0;
			this.logUpdateTimeQueue.Clear();
			this.isSmoothScrollingToLatestLog = false;
			this.scrollToLatestLogAction.Cancel();
			this.smoothScrollToLatestLogAction.Cancel();

			// update UI
			this.handleDisplayLogPropertiesChangedAction.Execute();
			this.updateStatusBarStateAction.Schedule();
		}


		/// <summary>
		/// Drop dragged data to this view asynchronously.
		/// </summary>
		/// <param name="keyModifiers">Key modifiers.</param>
		/// <param name="data">Data to be dropped.</param>
		/// <returns>True if dragged data is accepted by this view.</returns>
		public async Task<bool> DropAsync(KeyModifiers keyModifiers, IDataObject data)
		{
			// check data
			var hasFileNames = Global.RunOrDefault(() => data.HasFileNames()); // [Workaround] Prevent NRE when dragging without files on macOS
			if (!hasFileNames || this.IsHandlingDragAndDrop)
				return false;

			// bring window to front
			if (this.attachedWindow == null)
				return false;
			this.attachedWindow.ActivateAndBringToFront();

			// update state
			this.IsHandlingDragAndDrop = true;

			// handling drag-and-drop
			try
			{
				// [Workaround] clone data to prevent underlying resource being released later
				if (data is not DataObject)
				{
					var dataObject = new DataObject();
					foreach (var format in data.GetDataFormats())
					{
						if (data.TryGetData(format, out object? value) && value != null)
							dataObject.Set(format, value);
					}
					data = dataObject;
				}

				// collect files
				var dropFilePaths = data.GetFiles().Let(it =>
				{
					if (it is null)
						return (IList<string>)Array.Empty<string>();
					var list = new List<string>();
					foreach (var file in it)
					{
						var fileName = file.TryGetLocalPath();
						if (!string.IsNullOrEmpty(fileName))
							list.Add(fileName);
					}
					return list;
				});
				var dirPaths = new List<string>();
				var filePaths = new List<string>();
				await Task.Run(() =>
				{
					foreach (var path in dropFilePaths)
					{
						Global.RunWithoutError(() =>
						{
							if (System.IO.File.Exists(path))
								filePaths.Add(path);
							else if (System.IO.Directory.Exists(path))
								dirPaths.Add(path);
						});
					}
					return filePaths;
				});
				if (filePaths.IsEmpty())
				{
					if (dirPaths.IsEmpty())
					{
						_ = new MessageDialog()
						{
							Icon = MessageDialogIcon.Information,
							Message = this.Application.GetObservableString("SessionView.NoFilePathDropped")
						}.ShowDialog(this.attachedWindow);
						return false;
					}
					if (this.attachedWindow == null)
						return false;
					if (dirPaths.Count > 1)
					{
						_ = new MessageDialog()
						{
							Icon = MessageDialogIcon.Information,
							Message = this.Application.GetObservableString("SessionView.TooManyDirectoryPathsDropped")
						}.ShowDialog(this.attachedWindow);
						return false;
					}
				}

				// check state
				if (this.DataContext is not Session session)
					return false;
				
				// exclude added files
				if (filePaths.IsNotEmpty())
				{
					filePaths.RemoveAll(session.IsLogFileAdded);
					if (filePaths.IsEmpty())
					{
						this.Logger.LogTrace("All dropped files have been added to session before");
						return true;
					}
				}

				// check whether new log profile is needed or not
				var warningMessage = (IObservable<object?>?)null;
				var currentLogProfile = session.LogProfile;
				var needNewLogProfile = Global.Run(() =>
				{
					if (currentLogProfile == null)
						return true;
					if (session.IsLogFileSupported)
					{
						if (filePaths.IsEmpty())
							return true;
						if (!currentLogProfile.AllowMultipleFiles)
						{
							if (!session.IsLogFileNeeded || filePaths.Count > 1)
							{
								warningMessage = this.Application.GetObservableString("SessionView.MultipleFilesAreNotAllowed");
								return true;
							}
						}
						return false;
					}
					if (session.IsWorkingDirectoryNeeded)
						return filePaths.IsNotEmpty();
					return true;
				});

				// show warning message
				if (this.attachedWindow == null)
					return false;
				if (warningMessage != null)
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = warningMessage,
					}.ShowDialog(this.attachedWindow);
				}

				// select new log profile
				if (this.attachedWindow == null)
					return false;
				var newLogProfile = !needNewLogProfile ? null : await new LogProfileSelectionDialog().Also(it =>
				{
					if (filePaths.IsEmpty())
					{
						it.Filter = logProfile => logProfile.DataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory))
						                          && logProfile.WorkingDirectoryRequirement != LogProfilePropertyRequirement.Ignored
						                          && !logProfile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory));
					}
					else if (filePaths.Count > 1)
					{
						it.Filter = logProfile => logProfile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName))
							&& logProfile.AllowMultipleFiles
							&& !logProfile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName));
					}
					else
					{
						it.Filter = logProfile => logProfile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName))
							&& !logProfile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName));
					}
				}).ShowDialog<LogProfile>(this.attachedWindow);

				// set log profile or create new session
				if (newLogProfile != null)
				{
					var workspace = (Workspace)session.Owner.AsNonNull();
					var newIndex = workspace.Sessions.IndexOf(session).Let(it =>
					{
						if (it >= 0)
							return it + 1;
						return workspace.Sessions.Count;
					});
					if (currentLogProfile == null)
					{
						if (!session.SetLogProfileCommand.TryExecute(newLogProfile))
							return false;
					}
					else if (filePaths.IsNotEmpty())
					{
						if (this.attachedWindow is MainWindow mainWindow)
						{
							// create session
							var newSession = workspace.CreateAndAttachSession(newIndex, newLogProfile);
							workspace.ActiveSession = newSession;

							// drop files to new session
							mainWindow.FindSessionView(newSession)?.Let(view => 
								view.DropAsync(keyModifiers, data));
						}
						else
						{
							var newSession = workspace.CreateAndAttachSessionWithLogFiles(newIndex, newLogProfile, filePaths);
							workspace.ActiveSession = newSession;
						}
						return true;
					}
					else
					{
						var newSession = workspace.CreateAndAttachSessionWithWorkingDirectory(newIndex, newLogProfile, dirPaths[0]);
						workspace.ActiveSession = newSession;
						return true;
					}
				}
				else if (needNewLogProfile)
					return false;

				// set working directory or add log files
				if (session.SetWorkingDirectoryCommand.CanExecute(null))
					session.SetWorkingDirectoryCommand.TryExecute(dirPaths[0]);
				else if (session.AddLogFileCommand.CanExecute(null))
					return await this.AddLogFilesAsync(filePaths);

				// complete
				return true;
			}
			finally
			{
				this.IsHandlingDragAndDrop = false;
			}
		}


		/// <summary>
		/// Edit script log data source provider which is currently used.
		/// </summary>
		public void EditCurrentScriptLogDataSourceProvider()
		{ }


		/// <summary>
		/// Edit PATH environment variable.
		/// </summary>
		public void EditPathEnvVar()
		{
			if (!PathEnvVarEditorDialog.IsSupported || this.attachedWindow == null)
				return;
			_ = new PathEnvVarEditorDialog().ShowDialog(this.attachedWindow);
		}


		// Enable script running.
		public async void EnableRunningScript()
		{
			if (this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript))
				return;
			if (this.attachedWindow == null)
				return;
			await new EnableRunningScriptDialog().ShowDialog(this.attachedWindow);
		}


		// Trigger layout of all log list box items.
		void ForceLayoutLogListBoxContainers()
		{
			if (this.GetVisualParent() is null)
				return;
			var itemContainers = new HashSet<Control>();
			void OnLogListBoxContainerLayoutUpdated(object? sender, EventArgs e)
			{
				if (sender is not Control itemContainer)
					return;
				if (itemContainer.Margin != default)
				{
					itemContainers.Remove(itemContainer);
					itemContainer.LayoutUpdated -= OnLogListBoxContainerLayoutUpdated;
					itemContainer.Margin = default;
				}
			}
			foreach (var itemContainer in this.logListBox.GetRealizedContainers())
			{
				itemContainers.Add(itemContainer);
				itemContainer.LayoutUpdated += OnLogListBoxContainerLayoutUpdated;
				itemContainer.Margin = new(-1);
			}
			this.Logger.LogTrace("Force relayout {count} log item containers", itemContainers.Count);
			this.SynchronizationContext.PostDelayed(() =>
			{
				if (itemContainers.IsEmpty())
					return;
				this.Logger.LogWarning("Layout of {count} log item containers were not updated", itemContainers.Count);
				foreach (var itemContainer in itemContainers)
					itemContainer.LayoutUpdated -= OnLogListBoxContainerLayoutUpdated;
				itemContainers.Clear();
			}, 1000);
		}


		/// <summary>
		/// Check whether log profile has been set or not.
		/// </summary>
		public bool HasLogProfile => this.GetValue(HasLogProfileProperty);


		// Check whether one of popup is opened or not.
		bool HasOpenedPopup => this.logActionMenu.IsOpen
		                       || this.logAnalysisRuleSetsPopup.IsOpen
		                       || this.logChartTypeMenu.IsOpen
		                       || this.logFileActionMenu.IsOpen
		                       || this.logFilterCombinationModeMenu.IsOpen
		                       || this.logMarkingMenu.IsOpen
		                       || this.logsSavingMenu.IsOpen
		                       || this.otherActionsMenu.IsOpen
		                       || this.predefinedLogTextFiltersPopup.IsOpen
		                       || this.visibleLogChartSeriesMenu?.IsOpen == true;


		/// <summary>
		/// Check whether the view is handling drag-and-drop data or not.
		/// </summary>
		public bool IsHandlingDragAndDrop { get; private set; }


		// Check whether at least one key for multi-selection has been pressed or not.
		bool IsMultiSelectionKeyPressed(KeyModifiers keyModifiers)
		{
			if ((keyModifiers & KeyModifiers.Shift) != 0
				|| this.pressedKeys.Contains(Key.LeftShift) 
				|| this.pressedKeys.Contains(Key.RightShift))
			{
				return true;
			}
			if (Platform.IsMacOS)
			{
				if ((keyModifiers & KeyModifiers.Meta) != 0
					|| this.pressedKeys.Contains(Key.LWin) 
					|| this.pressedKeys.Contains(Key.RWin))
				{
					return true;
				}
			}
			else
			{
				if ((keyModifiers & KeyModifiers.Control) != 0
					|| this.pressedKeys.Contains(Key.LeftCtrl) 
					|| this.pressedKeys.Contains(Key.RightCtrl))
				{
					return true;
				}
			}
			return false;
		}


		// Check whether process info should be shown or not.
		bool IsProcessInfoVisible => this.GetValue(IsProcessInfoVisibleProperty);


		/// <summary>
		/// Check whether script running is enabled or not.
		/// </summary>
		public bool IsScriptRunningEnabled => this.GetValue(IsScriptRunningEnabledProperty);


		// Get or set whether scrolling to latest log is needed or not.
		public bool IsScrollingToLatestLogNeeded
		{
			get => this.GetValue(IsScrollingToLatestLogNeededProperty);
			set => this.SetValue(IsScrollingToLatestLogNeededProperty, value);
		}


		// Check whether auto scrolling to target range of log is on-going or not.
		public bool IsScrollingToTargetLogRange => 
			this.GetValue(IsScrollingToTargetLogRangeProperty);


		/// <summary>
		/// Whether "Tools" menu item is visible or not.
		/// </summary>
		public bool IsToolsMenuItemVisible { get; }


		/// <summary>
		/// Log in to Azure.
		/// </summary>
		public void LoginToAzure()
		{ }


		/// <summary>
		/// Get height of item of log list box.
		/// </summary>
		public double LogItemHeight => this.GetValue(LogItemHeightProperty);


		/// <summary>
		/// Log out from Azure.
		/// </summary>
		public void LogoutFromAzure()
		{ }


		// Mark logs with color.
		void MarkSelectedLogs(MarkColor color)
        {
			if (!this.canMarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			if (this.sidePanelContainer.IsVisible && this.Settings.GetValueOrDefault(SettingKeys.SwitchToMarkedLogsPanelAfterMarkingLogs))
				session.IsMarkedLogsPanelVisible = true;
			session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams()
			{
				Color = color,
				Logs = this.logListBox.SelectedItems!.Cast<DisplayableLog>().ToArray(),
			});
		}


		/// <summary>
		/// Command to mark selected logs with given color.
		/// </summary>
		public ICommand MarkSelectedLogsCommand { get; }


		// Mark or unmark selected logs.
		void MarkUnmarkSelectedLogs()
		{
			if (!this.canMarkUnmarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			var logs = this.logListBox.SelectedItems!.Cast<DisplayableLog>().ToArray();
			foreach (var log in logs)
			{
				if (log.MarkedColor == MarkColor.None)
				{
					if (this.sidePanelContainer.IsVisible && this.Settings.GetValueOrDefault(SettingKeys.SwitchToMarkedLogsPanelAfterMarkingLogs))
						session.IsMarkedLogsPanelVisible = true;
					break;
				}
			}
			session.MarkUnmarkLogsCommand.TryExecute(logs);
		}


		/// <summary>
		/// Command to mark or unmark selected logs.
		/// </summary>
		public ICommand MarkUnmarkSelectedLogsCommand { get; }


		// Max line count to display for each log.
		int MaxDisplayLineCountForEachLog => this.GetValue(MaxDisplayLineCountForEachLogProperty);


		// Called when application string resources updated.
		void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			this.UpdateLogChartPaints();
			this.SynchronizationContext.Post(() => // To make sure that display name of log property has been updated
			{
				this.areLogChartAxesReady = false;
				this.UpdateLogChartSeries();
			});
			this.UpdateLogLevelFilterComboBoxStrings();
			
			// invalidate menu of visible series of log chart
			if (this.visibleLogChartSeriesMenu is not null)
			{
				this.visibleLogChartSeriesMenu.Close();
				this.visibleLogChartSeriesMenu = null;
			}
		}


		// Called when attaching to view tree.
		protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			// update state
			this.isAttachedToLogicalTree = true;

			// call base
			base.OnAttachedToLogicalTree(e);

			// attach to window
			this.attachedWindow = this.FindLogicalAncestorOfType<Avalonia.Controls.Window>().AsNonNull().Also(window =>
			{
				this.areInitDialogsClosedObserverToken = (window as MainWindow)?.GetObservable(MainWindow.AreInitialDialogsClosedProperty).Subscribe(closed =>
				{
					if (closed)
						this.ShowNextTutorial();
				});
				this.hasDialogsObserverToken = (window as CarinaStudio.Controls.Window)?.GetObservable(CarinaStudio.Controls.Window.HasDialogsProperty).Subscribe(hasDialogs =>
				{
					if (!hasDialogs)
						this.ShowNextTutorial();
				});
				this.isActiveObserverToken = window.GetObservable(Avalonia.Controls.Window.IsActiveProperty).Subscribe(isActive =>
				{
					if (isActive)
					{
						// [Workaround] Force relayout to prevent incorrect layout in background
						this.ForceLayoutLogListBoxContainers();
						
						// show tutorials
						this.SynchronizationContext.Post(() => 
						{
							this.ShowLogAnalysisRuleSetsTutorial();
							this.ShowLogChartTutorial();
							if (this.isShowingHelpButtonOnLogTextFilterConfirmationNeeded)
							{
								this.isShowingHelpButtonOnLogTextFilterConfirmationNeeded = false;
								this.ConfirmShowingHelpButtonOnLogTextFilter();
							}
						});
					}
				});
			});

			// attach to predefined log text filter list
			this.AttachToPredefinedLogTextFilters();
			
			// sync log filters to UI
			this.SyncLogTextFiltersBack();
			this.commitLogFiltersAction.Cancel();

			// add event handlers
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
			this.Application.Configuration.SettingChanged += this.OnConfigurationChanged;
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.AddHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.AddHandler(DragDrop.DragLeaveEvent, this.OnDragLeave);
			this.AddHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.AddHandler(DragDrop.DropEvent, this.OnDrop);
			
			// invalidate resources
			this.logPropertyViewBorderPointerOverBrush = null;
			this.selectableValueLogItemBackgroundBrush = null;
			
			// setup height og log item
			this.updateLogItemHeightAction.Execute();

			// [Workaround] Prevent text not showing in TextBox
			if (!string.IsNullOrEmpty(this.logTextFilterTextBox.Text))
			{
				this.SynchronizationContext.Post(() =>
				{
					this.logTextFilterTextBox.Focus();
					this.logTextFilterTextBox.SelectAll();
				});
				this.SynchronizationContext.Post(() =>
				{
					this.logTextFilterTextBox.SelectionStart = 0;
					this.logTextFilterTextBox.SelectionEnd = 0;
					this.logListBox.Focus();
				});
			}

			// check settings
			this.SetValue(ShowHelpButtonOnLogTextFilterProperty, this.Settings.GetValueOrDefault(SettingKeys.ShowHelpButtonOnLogTextFilter));

			// update menu items
			this.UpdateToolsMenuItems();

			// check script running
			this.SetValue(IsScriptRunningEnabledProperty, this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript));

			// select parameters for reading logs
			this.AutoSelectAndSetLogsReadingParameters();

			// check administrator role
			this.ConfirmRestartingAsAdmin();
			
			// update resources for log chart
			this.UpdateLogChartPaints();

			// show tutorial
			this.SynchronizationContext.Post(() => this.ShowNextTutorial());
		}


		/// <inheritdoc/>
		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			// call base
			base.OnAttachedToVisualTree(e);

			// [Workaround] Force relayout to prevent incorrect layout on extended screen
			this.ForceLayoutLogListBoxContainers();
		}


		// Called when configuration changed.
		void OnConfigurationChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == ConfigurationKeys.MaxLinesOfMarkedLogWithoutDisplayLogProperties)
				this.handleDisplayLogPropertiesChangedAction.Schedule();
			else if (e.Key == ConfigurationKeys.TrimLogPropertyViewText)
				this.RecreateLogItemTemplate();
		}
		
		
		// Called when debug message generated.
		void OnDebugMessageGeneratedBySession(object? sender, MessageEventArgs e)
		{
			if (this.attachedWindow is INotificationPresenter notificationPresenter)
			{
				notificationPresenter.AddNotification(new Notification
				{
					Icon = this.Application.FindResourceOrDefault<IImage>("Image/Icon.Debug.Colored"),
					Message = e.Message,
					Timeout = null,
				});
			}
			else if (this.attachedWindow is not null)
			{
				_ = new MessageDialog
				{
					Buttons = MessageDialogButtons.OK,
					CustomIcon = this.Application.FindResourceOrDefault<IImage>("Image/Icon.Debug.Colored"),
					Icon = MessageDialogIcon.Custom,
					Message = e.Message,
				}.ShowDialog(this.attachedWindow);
			}
		}


		// Called when detach from view tree.
		protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			// update state
			this.isAttachedToLogicalTree = false;

			// remove event handlers
			this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
			this.Application.Configuration.SettingChanged -= this.OnConfigurationChanged;
			this.Settings.SettingChanged -= this.OnSettingChanged;
			this.RemoveHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.RemoveHandler(DragDrop.DragLeaveEvent, this.OnDragLeave);
			this.RemoveHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.RemoveHandler(DragDrop.DropEvent, this.OnDrop);
			
			// release predefined log text filter list
			this.DetachFromPredefinedLogTextFilters();
			this.selectedPredefinedLogTextFilters.Clear();

			// detach from window
			this.areInitDialogsClosedObserverToken = this.areInitDialogsClosedObserverToken.DisposeAndReturnNull();
			this.hasDialogsObserverToken = this.hasDialogsObserverToken.DisposeAndReturnNull();
			this.isActiveObserverToken = this.isActiveObserverToken.DisposeAndReturnNull();
			this.attachedWindow = null;

			// call base
			base.OnDetachedFromLogicalTree(e);
		}


		// Called when display log properties changed.
		void OnDisplayLogPropertiesChanged()
		{
			// clear headers
			foreach (var control in this.logHeaderGrid.Children)
				control.DataContext = null;
			this.logHeaderGrid.Children.Clear();
			this.logHeaderGrid.ColumnDefinitions.Clear();
			this.logHeaderColumns.Clear();
			this.logHeaderWidths.Clear();

			// clear item template
			this.logListBox.ItemTemplate = null;
			this.markedLogListBox.ItemTemplate = null;

			// get profile
			if (this.DataContext is not Session session)
				return;
			var profile = session.LogProfile;
			if (profile == null)
				return;

			// get display log properties
			var logProperties = session.DisplayLogProperties;
			if (logProperties.IsEmpty())
				return;
			var logPropertyCount = logProperties.Count;
			
			// build headers
			var app = (App)this.Application;
			var analysisResultIndicatorSize = 0.0;
			var analysisResultIndicatorMargin = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.LogAnalysisResultIndicator.Margin");
			var markIndicatorSize = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.MarkIndicator.Size");
			var markIndicatorMargin = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.MarkIndicator.Margin");
			var splitterWidth = app.FindResourceOrDefault<double>("Double/GridSplitter.Thickness");
			var separatorBrush = app.FindResourceOrDefault<IBrush>("Brush/SessionView.LogHeader.Separator");
			var separatorMargin = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogHeader.Separator.Margin");
			var minHeaderWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogHeader.MinWidth");
			var colorIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.ColorIndicator.Border");
			var colorIndicatorWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.ColorIndicator.Width");
			var headerTemplate = (DataTemplate)this.Resources["logHeaderTemplate"].AsNonNull();
			var columIndexOffset = 0;
			if (profile.ColorIndicator != LogColorIndicator.None)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(colorIndicatorWidth + colorIndicatorBorderThickness.Left + colorIndicatorBorderThickness.Right, GridUnitType.Pixel));
				++columIndexOffset;
			}
			if (markIndicatorSize > 0)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(markIndicatorSize + markIndicatorMargin.Left + markIndicatorMargin.Right, GridUnitType.Pixel));
				this.logHeaderGrid.Children.Add(new Border().Also(border =>
				{
					Grid.SetColumn(border, columIndexOffset);
					border.Background = Brushes.Transparent;
					border.Classes.Add("Icon");
					border.Child = new Avalonia.Controls.Image().Also(image =>
					{
						image.Classes.Add("Icon");
						image.Source = app.FindResourceOrDefault<IImage>("Image/Mark.Outline");
					});
					border.Height = markIndicatorSize;
					border.Margin = markIndicatorMargin;
					border.BindToResource(ToolTip.TipProperty, "String/SessionView.MarkLog");
					border.Width = markIndicatorSize;
					border.VerticalAlignment = VerticalAlignment.Center;
				}));
				++columIndexOffset;
			}
			if (analysisResultIndicatorSize > 0)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(analysisResultIndicatorSize + analysisResultIndicatorMargin.Left + analysisResultIndicatorMargin.Right, GridUnitType.Pixel));
				this.logHeaderGrid.Children.Add(new Border().Also(border =>
				{
					Grid.SetColumn(border, columIndexOffset);
					border.Background = Brushes.Transparent;
					border.Classes.Add("Icon");
					border.Child = new Avalonia.Controls.Image().Also(image =>
					{
						image.Classes.Add("Icon");
						image.Source = app.FindResourceOrDefault<IImage>("Image/Icon.Analysis.Outline");
					});
					border.Height = analysisResultIndicatorSize;
					border.Margin = analysisResultIndicatorMargin;
					border.BindToResource(ToolTip.TipProperty, "String/SessionView.LogAnalysisResult");
					border.Width = analysisResultIndicatorSize;
					border.VerticalAlignment = VerticalAlignment.Center;
				}));
				++columIndexOffset;
			}
			this.logHeaderGrid.Children.Add(new Border().Also(it => // Empty control in order to get first layout event of log header grid
			{
				it.HorizontalAlignment = HorizontalAlignment.Stretch;
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == BoundsProperty)
						this.ReportLogHeaderColumnWidths();
				};
			}));
			for (var i = 0; i < logPropertyCount; ++i)
			{
				// define splitter column
				var logPropertyIndex = i;
				if (logPropertyIndex > 0)
					this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(splitterWidth, GridUnitType.Pixel));

				// define header column
				var logProperty = logProperties[logPropertyIndex];
				var width = logProperty.Width;
				if (width == null)
					this.logHeaderWidths.Add(new MutableObservableValue<GridLength>(new GridLength(1, GridUnitType.Star)));
				else
					this.logHeaderWidths.Add(new MutableObservableValue<GridLength>(new GridLength(width.Value, GridUnitType.Pixel)));
				var headerColumn = new ColumnDefinition(this.logHeaderWidths[logPropertyIndex].Value).Also(it =>
				{
					it.MinWidth = minHeaderWidth;
				});
				this.logHeaderColumns.Add(headerColumn);
				this.logHeaderGrid.ColumnDefinitions.Add(headerColumn);

				// create splitter view
				if (logPropertyIndex > 0)
				{
					var border = new Border().Also(border =>
					{
						border.Background = separatorBrush;
						border.HorizontalAlignment = HorizontalAlignment.Center;
						border.IsHitTestVisible = false;
						border.VerticalAlignment = VerticalAlignment.Stretch;
						border.Margin = separatorMargin;
						border.Width = 1;
					});
					var splitter = new GridSplitter().Also(it =>
					{
						it.DragDelta += (_, _) => this.ReportLogHeaderColumnWidths();
						it.IsEnabled = this.logHeaderColumns[logPropertyIndex - 1].Width.GridUnitType == GridUnitType.Pixel
							|| headerColumn.Width.GridUnitType == GridUnitType.Pixel;
					});
					Grid.SetColumn(splitter, logPropertyIndex * 2 - 1 + columIndexOffset);
					Grid.SetColumn(border, logPropertyIndex * 2 - 1 + columIndexOffset);
					this.logHeaderGrid.Children.Add(border);
					this.logHeaderGrid.Children.Add(splitter);
				}

				// create header view
				var headerView = ((Border)headerTemplate.Build(logProperty).AsNonNull()).Also(it =>
				{
					it.DataContext = logProperty;
					if (logPropertyIndex == 0)
						it.BorderThickness = new Thickness();
				});
				Grid.SetColumn(headerView, logPropertyIndex * 2 + columIndexOffset);
				this.logHeaderGrid.Children.Add(headerView);
			}

			// build item template for log list box
			this.logListBox.ItemTemplate = this.CreateLogItemTemplate(profile, logProperties);

			// build item template for marked log list box
			this.markedLogListBox.ItemTemplate = this.CreateMarkedLogItemTemplate(session, profile, logProperties);

			// update filter availability
			this.UpdateCanFilterLogsByNonTextFilters();
		}


		// Called when drag enter.
		void OnDragEnter(object? sender, DragEventArgs e) =>
			this.dragDropReceiverBorder.IsVisible = true;


		// Called when drag leave.
		void OnDragLeave(object? sender, EventArgs e) =>
			this.dragDropReceiverBorder.IsVisible = false;


		// Called when drag over.
		void OnDragOver(object? sender, DragEventArgs e)
		{
			// check state
			if ((this.attachedWindow as AppSuite.Controls.Window)?.HasDialogs == true)
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}

			// mark as handled
			e.Handled = true;

			// check session
			if (this.DataContext is not Session)
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}

			// check data
			e.DragEffects = Global.RunOrDefault(() => e.Data.HasFileNames()) // [Workaround] Prevent NRE when dragging without files on macOS
				? DragDropEffects.Copy
				: DragDropEffects.None;
		}


		// Called when drop data on view.
		async void OnDrop(object? sender, DragEventArgs e)
		{
			this.dragDropReceiverBorder.IsVisible = false;
			if ((this.attachedWindow as AppSuite.Controls.Window)?.HasDialogs == true)
				return;
			e.Handled = true;
			await this.DropAsync(e.KeyModifiers, e.Data);
		}


		// Called when error message generated.
		void OnErrorMessageGeneratedBySession(object? sender, MessageEventArgs e)
		{
			if (this.attachedWindow == null)
				return;
			_ = new MessageDialog
			{
				Buttons = MessageDialogButtons.OK,
				Icon = MessageDialogIcon.Error,
				Message = e.Message,
			}.ShowDialog(this.attachedWindow);
		}


		// Called when external dependency not found.
		async void OnExternalDependencyNotFound(object? sender, EventArgs e)
		{
			// check state
			if (this.attachedWindow == null)
				return;
			var session = sender as Session;
			if (session != null && !session.IsActivated)
				return;

			// show external dependencies dialog
			await Task.Delay(300);
			if (this.attachedWindow is MainWindow mainWindow && mainWindow.HasDialogs)
			{
				var waitingTask = new TaskCompletionSource();
				var observableToken = (IDisposable?)null;
				observableToken = mainWindow.GetObservable(MainWindow.HasDialogsProperty).Subscribe(_ =>
				{
					this.SynchronizationContext.Post(() =>
					{
						if (!mainWindow.HasDialogs)
						{
							observableToken?.Dispose();
							waitingTask.SetResult();
						}
					});
				});
				await waitingTask.Task;
			}
			if (this.attachedWindow == null || (session != null && !session.IsActivated))
				return;
			await new ExternalDependenciesDialog().ShowDialog(this.attachedWindow);
		}


		// Called when got focus.
        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
			this.Logger.LogTrace("Got focus");
        }


		// Called when selected item of log category changed.
		void OnLogCategoryListBoxSelectedItemChanged(Avalonia.Controls.ListBox? listBox, DisplayableLogCategory? category)
		{
			if (category == null)
				return;
			category.Log?.Let(log =>
			{
				this.IsScrollingToLatestLogNeeded = false;
				this.logListBox.SelectedItems!.Clear();
				this.logListBox.SelectedItem = log;
				this.ScrollToLog(log, true);
			});
			this.SynchronizationContext.Post(() =>
			{
				if (listBox != null)
					listBox.SelectedItem = null;
				this.logListBox.Focus();
			});
		}


		// Called when runtime error occurred by script of log data source.
		void OnLogDataSourceScriptRuntimeErrorOccurred(object? sender, ScriptRuntimeErrorEventArgs e)
		{
			// check state
			if (this.attachedWindow == null)
				return;
			if (e.ScriptContainer is not ScriptLogDataSourceProvider provider)
				return;
			string scriptType;
			if (provider.ClosingReaderScript == e.Script)
				scriptType = "ClosingReaderScript";
			else if (provider.OpeningReaderScript == e.Script)
				scriptType = "OpeningReaderScript";
			else if (provider.ReadingLineScript == e.Script)
				scriptType = "ReadingLineScript";
			else
				scriptType = "UnknownScript";
			
			// generate message
			var message = new FormattedString()
			{
				Arg1 = ((ILogDataSourceProvider)provider).DisplayName,
			};
			message.Bind(FormattedString.FormatProperty, this.Application.GetObservableString($"SessionView.LogDataSourceScript.RuntimeError.{scriptType}"));

			// show dialog
			_ = new ScriptRuntimeErrorDialog()
			{
				Error = e.Error,
				Message = message,
			}.ShowDialog(this.attachedWindow);
		}


        // Called when log profile set.
        void OnLogProfileSet(LogProfile profile)
		{
			// show UI automatically to let user select parameters
			if (this.DataContext is not Session session)
				return;
			this.isCommandNeededAfterLogProfileSet = session.IsCommandNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectCommandWhenNeeded);
			this.isIPEndPointNeededAfterLogProfileSet = session.IsIPEndPointNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectIPEndPointWhenNeeded);
			this.isLogFileNeededAfterLogProfileSet = session.IsLogFileNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectLogFilesWhenNeeded);
			this.isProcessIdNeededAfterLogProfileSet = session.IsProcessIdNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectProcessIdWhenNeeded);
			this.isProcessNameNeededAfterLogProfileSet = session.IsProcessNameNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectProcessNameWhenNeeded);
			this.isUriNeededAfterLogProfileSet = session.IsUriNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectUriWhenNeeded);
			this.isWorkingDirNeededAfterLogProfileSet = session.IsWorkingDirectoryNeeded && this.Settings.GetValueOrDefault(SettingKeys.SelectWorkingDirectoryWhenNeeded);
			this.AutoSelectAndSetLogsReadingParameters();
			
			// reset auto scrolling
			this.IsScrollingToLatestLogNeeded = profile.IsContinuousReading;

			// check administrator role
			this.ConfirmRestartingAsAdmin();
		}


		// Called when double click on log list box.
		// ReSharper disable UnusedParameter.Local
		void OnLogListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e)
		{
			this.ShowLogStringProperty();
			e.Handled = true;
		}
		// ReSharper restore UnusedParameter.Local


		// Called when pointer pressed on log list box.
		void OnLogListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			// check pointer state
			var point = e.GetCurrentPoint(this.logListBox);
			if (point.Properties.IsLeftButtonPressed)
				this.isPointerPressedOnLogListBox = true;

			// clear selection
			var hitControl = this.logListBox.InputHitTest(point.Position).Let(it =>
			{
				if (it is not Visual visual)
					return default(Visual);
				var listBoxItem = visual.FindAncestorOfType<ListBoxItem>(true);
				if (listBoxItem != null)
					return listBoxItem;
				return visual.FindAncestorOfType<ScrollBar>(true);
			});
			if (hitControl is null)
			{
				this.logScrollViewerOffsetWhenSelectingItems = this.logScrollViewer?.Offset;
				this.SynchronizationContext.Post(() => this.logListBox.SelectedItems!.Clear());
			}
			else if (hitControl is ListBoxItem && !this.IsMultiSelectionKeyPressed(e.KeyModifiers) && point.Properties.IsLeftButtonPressed)
			{
				this.logScrollViewerOffsetWhenSelectingItems = this.logScrollViewer?.Offset;
				
				// [Workaround] Clear selection first to prevent performance issue of changing selection from multiple items
				this.logListBox.SelectedItems!.Clear();
			}

			// reset clicked log property
			this.lastClickedLogPropertyView = null;
			(this.DataContext as Session)?.LogSelection.Let(it => it.SelectedLogProperty = null);
			this.canCopyLogProperty.Update(false);
			this.canShowLogProperty.Update(false);
			
			// stop auto scrolling
			this.IsScrollingToLatestLogNeeded = false;
			
			// focus to list box
			this.logListBox.Focus();
		}


		// Called when pointer released on log list box.
		void OnLogListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton == MouseButton.Left)
				this.isPointerPressedOnLogListBox = false;
			this.logScrollViewerOffsetWhenSelectingItems = null;
		}


		// Called when pointer wheel change on log list box.
		void OnLogListBoxPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			this.ClearTargetLogRangeToScrollTo();
			this.SynchronizationContext.Post(() => this.UpdateIsScrollingToLatestLogNeeded(-e.Delta.Y));
		}


		// Called when log list box scrolled.
		void OnLogListBoxScrollChanged(object? sender, ScrollChangedEventArgs e)
		{
			// update auto scrolling state
			if (this.isPointerPressedOnLogListBox 
				|| this.pressedKeys.Contains(Key.Down)
				|| this.pressedKeys.Contains(Key.Up)
				|| this.pressedKeys.Contains(Key.Home)
				|| this.pressedKeys.Contains(Key.End))
			{
				this.UpdateIsScrollingToLatestLogNeeded(e.OffsetDelta.Y);
				this.ClearTargetLogRangeToScrollTo();
			}

			// keep current log time info
			this.updateLatestDisplayedLogRangeAction.Execute();

			// scroll to target
			if (this.targetLogRangeToScrollTo != null)
				this.scrollToTargetLogRangeAction.Schedule(ScrollingToTargetLogRangeInterval);

			// sync log header offset
			if (Math.Abs(e.OffsetDelta.X) > 0.1 || Math.Abs(e.ExtentDelta.X) > 0.1)
				this.updateLogHeaderContainerMarginAction.Schedule();
		}


		// Called when log list box selection changed.
		// ReSharper disable UnusedParameter.Local
		void OnLogListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count > 0)
				this.latestSelectedDisplayableLog = e.AddedItems[^1] as DisplayableLog;
			else if (this.logListBox.SelectedItems?.Count == 0)
				this.latestSelectedDisplayableLog = null;
			else if (this.latestSelectedDisplayableLog is null && e.RemovedItems.Contains(this.latestSelectedDisplayableLog))
				this.latestSelectedDisplayableLog = null;
			this.OnLogListBoxSelectionChanged();
		}
		// ReSharper restore UnusedParameter.Local
		void OnLogListBoxSelectionChanged()
		{
			// [Workaround] Keep horizontal scroll position after selection
			var scrollViewerOffset = this.logScrollViewerOffsetWhenSelectingItems 
			                         ?? this.logScrollViewer?.Offset 
			                         ?? default;
			
			// [Workaround] ListBox.SelectedItems is not update yet when calling this method
			this.SynchronizationContext.Post(() =>
			{
				// check state
				if (this.DataContext is not Session session)
					return;
				var selectionCount = this.logListBox.SelectedItems!.Count;
				var hasSelectedItems = (selectionCount > 0);
				var hasSingleSelectedItem = (selectionCount == 1);
				var logProperty = this.lastClickedLogPropertyView?.Tag as DisplayableLogProperty;
				
				// [Workaround] Keep horizontal scroll position after selection
				this.SynchronizationContext.Post(() =>
				{
					if (scrollViewerOffset.X > 0.5 && this.logScrollViewer is not null)
						this.logScrollViewer.Offset = scrollViewerOffset;
				});

				// update command states
				this.canCopyLogProperty.Update(hasSelectedItems && logProperty is not null);
				this.canCopyLogText.Update(hasSingleSelectedItem);
				this.canMarkSelectedLogs.Update(hasSelectedItems && session.MarkLogsCommand.CanExecute(null));
				this.canMarkUnmarkSelectedLogs.Update(hasSelectedItems && session.MarkUnmarkLogsCommand.CanExecute(null));
				this.canShowFileInExplorer.Update(hasSelectedItems && session.IsLogFileNeeded);
				this.canShowLogProperty.Update(hasSingleSelectedItem && logProperty is not null && DisplayableLog.HasStringProperty(logProperty.Name));
				this.canUnmarkSelectedLogs.Update(hasSelectedItems && session.UnmarkLogsCommand.CanExecute(null));

				// select single marked log
				if (hasSingleSelectedItem && this.logListBox.SelectedItem is DisplayableLog log && log.IsMarked)
				{
					this.SynchronizationContext.Post(() => 
					{
						this.markedLogListBox.SelectedItem = log;
						this.markedLogListBox.ScrollIntoView(log);
					});
				}
				else
					this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItems!.Clear());
				
				// scroll to selected log
				if (!session.LogSelection.IsAllLogsSelectionRequested)
					this.ScrollToSelectedLog();
			});
		}


		// Called when new log profile created by log profile selection menu.
		async void OnLogProfileCreatedByLogProfileSelectionMenu(LogProfile logProfile)
		{
			// check state
			if (this.attachedWindow == null)
				return;
			if (this.DataContext is not Session)
				return;
			
			// switch to new log profile
			var result = await new MessageDialog
			{
				Buttons = MessageDialogButtons.YesNo,
				Icon = MessageDialogIcon.Question,
				Message = new FormattedString().Also(it =>
				{
					it.Arg1 = logProfile.Name;
					it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.ConfirmSwitchingToCopiedLogProfile"));
				}),
				Title = this.Application.GetObservableString("LogProfileSelectionDialog.EditLogProfile"),
			}.ShowDialog(this.attachedWindow);
			if (result == MessageDialogResult.Yes)
				await this.SetLogProfileAsync(logProfile);
		}


		// Called when list of logs changed.
		void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				// statistic update time
				var currentTime = this.stopwatch.ElapsedMilliseconds;
				if (this.lastLogUpdateTime > 0)
				{
					var interval = (currentTime - this.lastLogUpdateTime);
					if (interval >= LogUpdateIntervalToResetStatistic)
						this.logUpdateTimeQueue.Clear();
					else
					{
						while (this.logUpdateTimeQueue.Count >= LogUpdateIntervalStatisticCount)
							this.logUpdateTimeQueue.Dequeue();
						this.logUpdateTimeQueue.Enqueue(interval);
					}
				}
				this.lastLogUpdateTime = currentTime;
				
				// trigger scrolling to target/latest log
				if (this.targetLogRangeToScrollTo != null)
					this.scrollToTargetLogRangeAction.Schedule(ScrollingToTargetLogRangeInterval);
				else if (this.IsScrollingToLatestLogNeeded && !this.scrollToLatestLogAction.IsScheduled)
				{
					// select scrolling interval
					var averageInternal = this.logUpdateTimeQueue.Count > 0 
						? this.logUpdateTimeQueue.Sum() / (double)this.logUpdateTimeQueue.Count 
						: -1;
					var scrollingInterval = averageInternal < 0 || averageInternal >= ScrollingToLatestLogInterval
						? ScrollingToLatestLogInterval
						: SlowScrollingToLatestLogInterval;
					
					// scroll
					if (this.isSmoothScrollingToLatestLog)
					{
						this.isSmoothScrollingToLatestLog = false;
						this.smoothScrollToLatestLogAction.Reschedule(scrollingInterval);
					}
					else
						this.smoothScrollToLatestLogAction.Schedule(scrollingInterval);
				}
			}
			else if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				// cancel scrolling to target log range
				if (this.targetLogRangeToScrollTo != null)
				{
					var removedLogs = e.OldItems.AsNonNull();
					if (removedLogs.Contains(this.targetLogRangeToScrollTo[0]) || removedLogs.Contains(this.targetLogRangeToScrollTo[1]))
						this.ClearTargetLogRangeToScrollTo();
				}
			}
		}
		
		
		// Called when logs saving completed.
		void OnLogsSavingCompleted(Session session, string fileName, bool success)
		{
			if (this.attachedWindow is MainWindow mainWindow)
			{
				mainWindow.AddNotification(new Notification().Also(it =>
				{
					if (success)
					{
						if (Platform.IsOpeningFileManagerSupported)
						{
							it.Actions = new[]
							{
								new NotificationAction().Also(it =>
								{
									it.Command = new Command(() => Platform.OpenFileManager(fileName));
									it.Bind(NotificationAction.NameProperty, this.Application.GetObservableString("SessionView.ShowFileInExplorer"));
								})
							};
						}
						it.BindToResource(Notification.IconProperty, this, "Image/Icon.Success.Colored");
						it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
						{
							it.Arg1 = fileName;
							it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogsSaved"));
						}));
					}
					else
					{
						it.BindToResource(Notification.IconProperty, this, "Image/Icon.Error.Colored");
						it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
						{
							it.Arg1 = fileName;
							it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogsSavingFailed"));
						}));
					}
				}));
			}
			else if (this.attachedWindow is not null)
			{
				new MessageDialog().Also(it =>
				{
					if (success)
					{
						it.Icon = MessageDialogIcon.Success;
						it.Message = new FormattedString().Also(it =>
						{
							it.Arg1 = fileName;
							it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogsSaved"));
						});
					}
					else
					{
						it.Icon = MessageDialogIcon.Error;
						it.Message = new FormattedString().Also(it =>
						{
							it.Arg1 = fileName;
							it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogsSavingFailed"));
						});
					}
				}).ShowDialog(this.attachedWindow);
			}
		}


		// Called when lost focus.
        protected override void OnLostFocus(RoutedEventArgs e)
        {
			this.Logger.LogTrace("Lost focus");
			if (this.pressedKeys.IsNotEmpty())
			{
				if (this.Application.IsDebugMode)
				{
					foreach (var key in this.pressedKeys)
						this.Logger.LogWarning("Drop pressed key {key}", key);
				}
				else
					this.Logger.LogWarning("Drop {pressedKeyCount} pressed keys", this.pressedKeys.Count);
				this.pressedKeys.Clear();
			}
            base.OnLostFocus(e);
        }


		// Called when double clicked on item of marked log list box.
		// ReSharper disable UnusedParameter.Local
		void OnMarkedLogListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e)
		{
			if (e.Item is DisplayableLog log)
				this.OnMarkedLogSelected(log, true);
		}
		// ReSharper restore UnusedParameter.Local


        // Called when selection in marked log list box has been changed.
        // ReSharper disable UnusedParameter.Local
        void OnMarkedLogListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.markedLogListBox.SelectedItem is DisplayableLog log)
				this.OnMarkedLogSelected(log, this.markedLogListBox.IsFocused);
		}
        // ReSharper restore UnusedParameter.Local


		// Called when marked logs changed.
		void OnMarkedLogsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
			this.updateLatestDisplayedLogRangeAction.Schedule();
		

		// Called when selecting a marked log.
		void OnMarkedLogSelected(DisplayableLog log, bool selectedByUser)
		{
			if (this.DataContext is not Session session)
				return;
			var index = session.Logs.IndexOf(log);
			this.logListBox.Let(it =>
			{
				if (selectedByUser || it.SelectedItems?.Count != 1 || it.SelectedItems[0] != log)
				{
					it.SelectedItems!.Clear();
					if (index >= 0)
					{
						it.SelectedIndex = index;
						this.ScrollToLog(session, index, log, true);
					}
					else
						this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItems!.Clear());
				}
				it.Focus();
			});
			this.IsScrollingToLatestLogNeeded = false;
		}


		// Called to handle key-down before all children.
		internal async void OnPreviewKeyDown(KeyEventArgs e)
		{
			// save key
			this.pressedKeys.Add(e.Key);
			
			// check modifier keys
			if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
				this.isAltKeyPressed = true;
			
			// log event
#if DEBUG
			this.Logger.LogTrace("[PreviewKeyDown] {key}, Modifiers: {keyModifiers}", e.Key, e.KeyModifiers);
#endif
			
			// handle key event
			var hasOpenedPopup = this.HasOpenedPopup;
			var isCtrlPressed = Platform.IsMacOS 
				? (e.KeyModifiers & KeyModifiers.Meta) != 0
				: (e.KeyModifiers & KeyModifiers.Control) != 0;
			var isAltPressed = (e.KeyModifiers & KeyModifiers.Alt) != 0;
			if (this.DataContext is Session session && !e.Handled)
			{
				if (isCtrlPressed)
				{
					switch (e.Key)
					{
						case Key.A:
							// [Workaround] It will take long time to select all items by list box itself
							if (e.Source is not TextBox && !hasOpenedPopup)
							{
								// intercept
								if (Platform.IsMacOS)
									this.Logger.LogTrace("Intercept Cmd+A");
								else
									this.Logger.LogTrace("Intercept Ctrl+A");
								e.Handled = true;

								// confirm selection
								if (session.Logs.Count >= 500000)
								{
									if (this.attachedWindow != null)
									{
										var result = await new MessageDialog()
										{
											Buttons = MessageDialogButtons.YesNo,
											Icon = MessageDialogIcon.Question,
											Message = this.Application.GetObservableString("SessionView.ConfirmSelectingAllLogs"),
										}.ShowDialog(this.attachedWindow);
										if (result == MessageDialogResult.No)
											return;
									}
								}

								// select all logs
								session.LogSelection.SelectAllLogs();
							}
							break;
						case Key.C:
							if (e.Source is not TextBox && !hasOpenedPopup)
							{
								if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
									(this.DataContext as Session)?.LogSelection.CopySelectedLogsWithFileNames();
								else
									(this.DataContext as Session)?.LogSelection.CopySelectedLogs();
							}
							break;
						case Key.D0:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.UnmarkSelectedLogs();
								e.Handled = true;
							}
							break;
						case Key.D1:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Red);
								e.Handled = true;
							}
							break;
						case Key.D2:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Orange);
								e.Handled = true;
							}
							break;
						case Key.D3:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Yellow);
								e.Handled = true;
							}
							break;
						case Key.D4:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Green);
								e.Handled = true;
							}
							break;
						case Key.D5:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Blue);
								e.Handled = true;
							}
							break;
						case Key.D6:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Indigo);
								e.Handled = true;
							}
							break;
						case Key.D7:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Purple);
								e.Handled = true;
							}
							break;
						case Key.D8:
							if (isAltPressed && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Magenta);
								e.Handled = true;
							}
							break;
						case Key.F:
							if (this.logTextFilterTextBox.IsEnabled && !hasOpenedPopup)
							{
								this.logTextFilterTextBox.Focus();
								this.logTextFilterTextBox.SelectAll();
								e.Handled = true;
							}
							break;
						case Key.M:
							if (e.Source is not TextBox && !hasOpenedPopup)
							{
								this.MarkSelectedLogs(MarkColor.Default);
								e.Handled = true;
							}
							break;
						case Key.O:
							if (!hasOpenedPopup)
							{
								_ = this.AddLogFilesAsync();
								e.Handled = true;
							}
							break;
						case Key.P:
							predefinedLogTextFiltersPopup.IsOpen = !predefinedLogTextFiltersPopup.IsOpen;
							e.Handled = true;
							break;
						case Key.S:
							if (e.Source == this.logTextFilterTextBox)
							{
								this.logTextFilterTextBox.Validate();
								if (this.logTextFilterTextBox.IsTextValid && this.logTextFilterTextBox.Object != null)
									this.CreatePredefinedLogTextFilter();
								e.Handled = true;
							}
							else if (!hasOpenedPopup)
							{
								this.SaveLogs((e.KeyModifiers & KeyModifiers.Shift) != 0);
								e.Handled = true;
							}
							break;
						case Key.Y:
							if (ReferenceEquals(e.Source, this.logTextFilterTextBox) && Platform.IsNotMacOS)
							{
								if ((e.KeyModifiers & KeyModifiers.Shift) == 0)
								{
									if (session.LogFiltering.UseNextTextFilterInHistoryCommand.TryExecute())
										this.SynchronizationContext.Post(this.logTextFilterTextBox.SelectAll);
								}
								e.Handled = true;
							}
							break;
						case Key.Z:
							if (ReferenceEquals(e.Source, this.logTextFilterTextBox))
							{
								if ((e.KeyModifiers & KeyModifiers.Shift) == 0)
								{
									if (session.LogFiltering.UsePreviousTextFilterInHistoryCommand.TryExecute())
										this.SynchronizationContext.Post(this.logTextFilterTextBox.SelectAll);
								}
								else if (Platform.IsMacOS)
								{
									if (session.LogFiltering.UseNextTextFilterInHistoryCommand.TryExecute())
										this.SynchronizationContext.Post(this.logTextFilterTextBox.SelectAll);
								}
								e.Handled = true;
							}
							break;
					}
				}
				else if (isAltPressed)
				{
					if (!ReferenceEquals(e.Source, this.logTextFilterTextBox) && !hasOpenedPopup)
					{
						switch (e.Key)
						{
							case Key.A:
								session.ToggleShowingAllLogsTemporarilyCommand.TryExecute();
								this.SynchronizationContext.Post(() => this.Focus()); // [Workaround] Get focus back to prevent unexpected focus lost.
								e.Handled = true;
								break;
							case Key.M:
								session.ToggleShowingMarkedLogsTemporarilyCommand.TryExecute();
								this.SynchronizationContext.Post(() => this.Focus()); // [Workaround] Get focus back to prevent unexpected focus lost.
								e.Handled = true;
								break;
							case Key.R:
								session.ResetTemporarilyShownLogsCommand.TryExecute();
								this.SynchronizationContext.Post(() => this.Focus()); // [Workaround] Get focus back to prevent unexpected focus lost.
								e.Handled = true;
								break;
						}
					}
				}
				else
				{
					switch (e.Key)
					{
						case Key.Down:
							if (ReferenceEquals(e.Source, this.logTextFilterTextBox))
							{
								if (!this.logTextFilterTextBox.HasOpenedAssistanceMenus)
								{
									if (session.LogFiltering.UseNextTextFilterInHistoryCommand.TryExecute())
									{
										this.SynchronizationContext.Post(() =>
										{
											this.logTextFilterTextBox.SelectionStart = 0; // [Workaround] make SelectAll() works expectedly
											this.logTextFilterTextBox.SelectionEnd = 0; // [Workaround] make SelectAll() works expectedly
											this.logTextFilterTextBox.SelectAll();
										});
									}
									e.Handled = true;
								}
							}
							else if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectNextItem();
								else if (!hasOpenedPopup)
								{
									var selectedItems = this.logListBox.SelectedItems;
									if (selectedItems!.Count > 1)
									{
										var latestSelectedItem = selectedItems[^1];
										selectedItems.Clear(); // [Workaround] clear selection first to prevent performance issue of de-selecting multiple items
										if (latestSelectedItem is DisplayableLog log)
										{
											selectedItems.Add(log);
											this.ScrollToLog(log);
										}
									}
									this.logListBox.SelectNextItem();
								}
								e.Handled = true;
							}
							break;
						case Key.Escape:
							if (e.Source is TextBox)
							{
								if (this.Application.IsDebugMode)
									this.Logger.LogTrace("[KeyDown] {key} on text box", e.Key);
								if (e.Source != this.logTextFilterTextBox || !this.logTextFilterTextBox.HasOpenedAssistanceMenus)
								{
									this.logListBox.Focus();
									e.Handled = true;
								}
							}
							break;
						case Key.Up:
							if (ReferenceEquals(e.Source, this.logTextFilterTextBox))
							{
								if (!this.logTextFilterTextBox.HasOpenedAssistanceMenus)
								{
									if (session.LogFiltering.UsePreviousTextFilterInHistoryCommand.TryExecute())
									{
										this.SynchronizationContext.Post(() =>
										{
											this.logTextFilterTextBox.SelectionStart = 0; // [Workaround] make SelectAll() works expectedly
											this.logTextFilterTextBox.SelectionEnd = 0; // [Workaround] make SelectAll() works expectedly
											this.logTextFilterTextBox.SelectAll();
										});
									}
									e.Handled = true;
								}
							}
							else if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectPreviousItem();
								else
								{
									var selectedItems = this.logListBox.SelectedItems;
									if (selectedItems!.Count > 1)
									{
										var latestSelectedItem = selectedItems[^1];
										selectedItems.Clear(); // [Workaround] clear selection first to prevent performance issue of de-selecting multiple items
										if (latestSelectedItem is DisplayableLog log)
										{
											selectedItems.Add(log);
											this.ScrollToLog(log);
										}
									}
									this.logListBox.SelectPreviousItem();
								}
								e.Handled = true;
							}
							break;
					}
				}
			}
		}


		// Called to handle key-up before all children.
		internal void OnPreviewKeyUp(KeyEventArgs e)
		{
			// log event
#if DEBUG
			this.Logger.LogTrace("[PreviewKeyUp] {key}, Modifiers: {keyModifiers}", e.Key, e.KeyModifiers);
#endif
			
			// [Workaround] skip handling key event if it was handled by context menu
			// check whether key down was received or not
			if (!this.pressedKeys.Contains(e.Key))
			{
				this.Logger.LogTrace("[PreviewKeyUp] Key down of {key} was not received", e.Key);
				return;
			}
			
			// check modifier keys
			if ((e.KeyModifiers & KeyModifiers.Alt) == 0)
				this.isAltKeyPressed = false;
			
			// handle key event for single key
			var hasOpenedPopup = this.HasOpenedPopup;
			if (!e.Handled)
			{
				if (!hasOpenedPopup)
				{
					if (!this.IsMultiSelectionKeyPressed(e.KeyModifiers))
					{
						switch (e.Key)
						{
							case Key.End:
								if (e.Source is not TextBox)
								{
									if (this.predefinedLogTextFiltersPopup.IsOpen)
										this.predefinedLogTextFilterListBox.SelectLastItem();
									else
										this.logListBox.SelectLastItem();
									e.Handled = true;
								}
								break;
							case Key.F5:
								this.ReloadLogs();
								e.Handled = true;
								break;
							case Key.Home:
								if (e.Source is not TextBox)
								{
									if (this.predefinedLogTextFiltersPopup.IsOpen)
										this.predefinedLogTextFilterListBox.SelectFirstItem();
									else
										this.logListBox.SelectFirstItem();
									e.Handled = true;
								}
								break;
							case Key.M:
								if (e.Source is not TextBox)
								{
									this.MarkUnmarkSelectedLogs();
									e.Handled = true;
								}
								break;
							case Key.P:
								if (e.Source is not TextBox)
								{
									(this.DataContext as Session)?.PauseResumeLogsReadingCommand.TryExecute();
									e.Handled = true;
								}
								break;
							case Key.S:
								if (e.Source is not TextBox && !this.isSelectingFileToSaveLogs)
								{
									(this.DataContext as Session)?.LogSelection.SelectMarkedLogs();
									e.Handled = true;
								}
								break;
						}
					}
				}
				else if (e.Key == Key.Escape)
				{
					if (this.predefinedLogTextFiltersPopup.IsOpen)
					{
						this.predefinedLogTextFiltersPopup.Close();
						e.Handled = true;
					}
					else if (this.logAnalysisRuleSetsPopup.IsOpen)
					{
						this.logAnalysisRuleSetsPopup.Close();
						e.Handled = true;
					}
				}
			}

			// stop tracking key
			this.pressedKeys.Remove(e.Key);
		}


		// Property changed.
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			var property = change.Property;
			if (property == BoundsProperty)
			{
				if (this.IsScrollingToLatestLogNeeded)
				{
					if (!this.scrollToLatestLogAction.IsScheduled)
					{
						if (this.isSmoothScrollingToLatestLog)
						{
							this.isSmoothScrollingToLatestLog = false;
							this.smoothScrollToLatestLogAction.Reschedule(ScrollingToLatestLogInterval);
						}
						else
							this.smoothScrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
					}
				}
				if (this.IsScrollingToLatestLogAnalysisResultNeeded)
				{
					if (!this.scrollToLatestLogAnalysisResultAction.IsScheduled)
					{
						if (this.isSmoothScrollingToLatestLogAnalysisResult)
						{
							this.isSmoothScrollingToLatestLogAnalysisResult = false;
							this.smoothScrollToLatestLogAnalysisResultAction.Reschedule(ScrollingToLatestLogInterval);
						}
						else
							this.smoothScrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
					}
				}
			}
			else if (property == DataContextProperty)
			{
				(change.OldValue as Session)?.Let(this.DetachFromSession);
				(change.NewValue as Session)?.Let(this.AttachToSession);
			}
			else if (property == IsScrollingToLatestLogNeededProperty)
			{
				if ((bool)change.NewValue!)
				{
					this.isSmoothScrollingToLatestLog = false;
					this.scrollToLatestLogAction.Reschedule(ScrollingToLatestLogInterval);
					this.smoothScrollToLatestLogAction.Cancel();
					var logProfile = (this.DataContext as Session)?.LogProfile;
					if (logProfile?.IsContinuousReading == false)
					{
						this.scrollToLatestLogAction.Execute();
						this.SynchronizationContext.Post(() => this.IsScrollingToLatestLogNeeded = false);
					}
					this.ClearTargetLogRangeToScrollTo();
				}
				else
				{
					this.isSmoothScrollingToLatestLog = false;
					this.scrollToLatestLogAction.Cancel();
					this.smoothScrollToLatestLogAction.Cancel();
				}
			}
			else if (property == IsScrollingToLatestLogAnalysisResultNeededProperty)
			{
				if ((bool)change.NewValue!)
					this.ScrollToLatestLogAnalysisResult(false);
				else
				{
					this.isSmoothScrollingToLatestLogAnalysisResult = false;
					this.scrollToLatestLogAnalysisResultAction.Cancel();
					this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
				}
			}
		}


		// Called when property of session has been changed.
		void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not Session session)
				return;
			switch (e.PropertyName)
			{
				case nameof(Session.AreDisplayLogPropertiesDefinedByLogProfile):
				case nameof(Session.DisplayLogProperties):
					this.handleDisplayLogPropertiesChangedAction.Schedule();
					break;
				case nameof(Session.HasAllDataSourceErrors):
				case nameof(Session.HasLogReaders):
				case nameof(Session.HasPartialDataSourceErrors):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.HasLogs):
					if (!session.HasLogs)
					{
						this.lastLogUpdateTime = 0;
						this.logUpdateTimeQueue.Clear();
						this.isSmoothScrollingToLatestLog = false;
						this.scrollToLatestLogAction.Cancel();
						this.smoothScrollToLatestLogAction.Cancel();
					}
					else if (this.IsScrollingToLatestLogNeeded)
					{
						if (!this.scrollToLatestLogAction.IsScheduled)
						{
							if (this.isSmoothScrollingToLatestLog)
							{
								this.isSmoothScrollingToLatestLog = false;
								this.smoothScrollToLatestLogAction.Reschedule(ScrollingToLatestLogInterval);
							}
							else
								this.smoothScrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
						}
					}
					break;
				case nameof(Session.HasWorkingDirectory):
					this.canShowWorkingDirectoryInExplorer.Update(Platform.IsOpeningFileManagerSupported && session.HasWorkingDirectory);
					break;
				case nameof(Session.IsActivated):
					if (!session.IsActivated)
					{
						this.isSmoothScrollingToLatestLog = false;
						this.isSmoothScrollingToLatestLogAnalysisResult = false;
						this.scrollToLatestLogAction.Cancel();
						this.smoothScrollToLatestLogAction.Cancel();
						this.scrollToLatestLogAnalysisResultAction.Cancel();
						this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
					}
					else
					{
						this.ShowLogAnalysisRuleSetsTutorial();
						this.ShowLogChartTutorial();
						if (this.HasLogProfile)
						{
							if (session.LogProfile?.IsContinuousReading == true && this.IsScrollingToLatestLogNeeded)
							{
								this.isSmoothScrollingToLatestLog = false;
								this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
								this.smoothScrollToLatestLogAction.Cancel();
							}
							if (session.LogAnalysis.AnalysisResults.IsNotEmpty() && this.IsScrollingToLatestLogAnalysisResultNeeded)
							{
								this.isSmoothScrollingToLatestLogAnalysisResult = false;
								this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
								this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
							}
						}
					}
					break;
				case nameof(Session.IsEmbeddedScriptLogDataSourceProvider):
				case nameof(Session.IsScriptLogDataSourceProvider):
					this.updateCanEditCurrentScriptLogDataSourceProviderAction.Schedule();
					break;
				case nameof(Session.IsHighMemoryUsageToStopReadingLogs):
					if (session.IsHighMemoryUsageToStopReadingLogs 
					    && session.IsActivated 
					    && this.attachedWindow?.IsActive == true)
					{
						_ = new MessageDialog
						{
							Icon = MessageDialogIcon.Warning,
							Message = new FormattedString().Also(it =>
							{
								it.Arg1 = this.Application.ProcessInfo.PrivateMemoryUsage.GetValueOrDefault().ToFileSizeString();
								it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogsReadingStoppedDueToHighMemoryUsage"));
							}),
						}.ShowDialog(this.attachedWindow);
					}
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.IsLogsReadingPaused):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.IsProVersionActivated):
					this.ShowLogChartTutorial();
					this.UpdateToolsMenuItems();
					if (session.LogChart.IsMaxTotalSeriesValueCountReached)
						this.PromptForMaxLogChartSeriesValueCountReached();
					this.updateCanEditCurrentScriptLogDataSourceProviderAction.Execute();
					break;
				case nameof(Session.IsReadingLogs):
					if (session.IsReadingLogs)
						this.ClearTargetLogRangeToScrollTo();
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.IsReadingLogsContinuously):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.IsRemovingLogFiles):
					if (session.IsRemovingLogFiles)
					{
						this.logAnalysisResultListBox.SelectedItems?.Clear(); // [Workaround] Prevent NRE caused by Avalonia
						this.logAnalysisResultListBox.ItemsSource = null;
					}
					else
					{
						this.logAnalysisResultListBox.Bind(Avalonia.Controls.ListBox.ItemsSourceProperty, new Binding
						{
							Path = $"{nameof(Session.LogAnalysis)}.{nameof(LogAnalysisViewModel.AnalysisResults)}"
						});
					}
					break;
				case nameof(Session.LogProfile):
					session.LogProfile.Let(profile =>
					{
						if (profile != null)
						{
							// update state
							this.canEditLogProfile.Update(true);
							this.SetValue(HasLogProfileProperty, true);
							
							// cancel auto scrolling
							if (!profile.IsContinuousReading)
								this.IsScrollingToLatestLogNeeded = false;
							
							// invalidate colors of log chart series
							this.InvalidateLogChartSeriesColors();
						}
						else
						{
							this.canEditLogProfile.Update(false);
							this.SetValue(HasLogProfileProperty, false);
						}
						this.logProfileSelectionMenu.CurrentLogProfile = profile;
					});
					break;
				case nameof(Session.Logs):
					// start scrolling to log around current position
					this.StartKeepingCurrentDisplayedLogRange();

					// update time and selection info
					this.SynchronizationContext.Post(() =>
					{
						// update displayed log range and start scrolling to target range
						this.updateLatestDisplayedLogRangeAction.Execute();
						if (this.targetLogRangeToScrollTo != null)
							this.scrollToTargetLogRangeAction.Schedule(ScrollingToTargetLogRangeInterval);

						// [Workaround] SelectionChange may not be fired after changing items
						this.OnLogListBoxSelectionChanged();
					});
					
					// attach to new list of logs
					if (this.attachedLogs != null)
						this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
					this.attachedLogs = (session.Logs as INotifyCollectionChanged);
					if (this.attachedLogs != null)
						this.attachedLogs.CollectionChanged += this.OnLogsChanged;
					break;
				case nameof(Session.ValidLogLevels):
					this.validLogLevels.Clear();
					this.validLogLevels.AddAll(session.ValidLogLevels);
					this.UpdateCanFilterLogsByNonTextFilters();
					if (this.validLogLevels.IsNotEmpty() && this.logLevelFilterComboBox.SelectedIndex < 0)
						this.logLevelFilterComboBox.SelectedIndex = 0;
					break;
			}
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == AppSuite.SettingKeys.EnableRunningScript)
				this.SetValue(IsScriptRunningEnabledProperty, (bool)e.Value);
			else if (e.Key == SettingKeys.LogFontSize)
				this.updateLogItemHeightAction.Schedule();
			else if (e.Key == SettingKeys.LogSeparators)
				this.RecreateLogItemTemplate();
			else if (e.Key == SettingKeys.ShowHelpButtonOnLogTextFilter)
				this.OnShowHelpButtonOnLogTextFilterSettingChanged((bool)e.Value);
			else if (e.Key == AppSuite.SettingKeys.ShowProcessInfo)
				this.SetValue(IsProcessInfoVisibleProperty, (bool)e.Value);
			else if (e.Key == SettingKeys.UpdateLogFilterDelay)
				this.OnUpdateLogFilterDelaySettingChanged();
		}


		// Called when pointer released on tool bar.
		void OnToolBarPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (this.attachedWindow?.FocusManager?.GetFocusedElement() is not TextBox)
				this.SynchronizationContext.Post(() => this.logListBox.Focus());
		}


		/// <summary>
		/// Perform full GC.
		/// </summary>
		public void PerformGC() =>
			this.Application.PerformGC(GCCollectionMode.Forced);
		
		
		// Recreate item template for log item.
		void RecreateLogItemTemplate()
		{
			var session = this.DataContext as Session;
			var logProfile = session?.LogProfile;
			if (logProfile is not null)
				this.logListBox.ItemTemplate = this.CreateLogItemTemplate(logProfile, session!.DisplayLogProperties);
		}


		// Reload log file.
		bool ReloadLogFile(Session.LogFileInfo logFileInfo, Logs.LogReadingPrecondition precondition, Logs.LogReadingWindow? readingWindow, int? maxLogReadingCount)
		{
			return false;
		}


		// Reload log file.
		void ReloadLogFileWithNewMaxLogReadingCount(string? fileName)
		{ }


		/// <summary>
		/// Command to reload log file with maximum log reading count.
		/// </summary>
		public ICommand ReloadLogFileWithNewMaxLogReadingCountCommand { get; }


		// Reload log file.
		void ReloadLogFileWithNewPrecondition(string? fileName)
		{ }


		/// <summary>
		/// Command to reload log file with new precondition.
		/// </summary>
		public ICommand ReloadLogFileWithNewPreconditionCommand { get; }


		// Reload log file without reading precondition.
		void ReloadLogFileWithoutLogReadingPrecondition(string? fileName)
		{ }


		/// <summary>
		/// Command to reload log file without reading precondition.
		/// </summary>
		public ICommand ReloadLogFileWithoutLogReadingPreconditionCommand { get; }


		// Reload logs.
		void ReloadLogs()
        {
			// check state
			this.VerifyAccess();
			if (this.DataContext is not Session session)
				return;

			// reload logs
			var hasSelectedLogs = this.logListBox.SelectedItems!.Count > 0;
			if (!session.ReloadLogsCommand.TryExecute())
				return;
			
			// [Workaround] Make sure log selection information will be updated
			if (hasSelectedLogs)
				this.OnLogListBoxSelectionChanged();

			// scroll to latest log
			if (session.LogProfile?.IsContinuousReading == true
				&& !this.IsScrollingToLatestLogNeeded
				&& this.Settings.GetValueOrDefault(SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs))
			{
				this.Logger.LogDebug("Enable scrolling to latest log after reloading logs");
				this.IsScrollingToLatestLogNeeded = true;
			}
        }


		/// <summary>
		/// Command to reload logs.
		/// </summary>
		public ICommand ReloadLogsCommand { get; }


		// Report width of each log header so that items in log list box can change width of each column.
		void ReportLogHeaderColumnWidths()
		{
			if (this.DataContext is not Session session)
				return;
			var lastLogPropertyIndex = session.DisplayLogProperties.Count - 1;
			for (var columnIndex = 0; columnIndex <= lastLogPropertyIndex; ++columnIndex)
			{
				var headerColumn = this.logHeaderColumns[columnIndex];
				var headerColumnWidth = this.logHeaderWidths[columnIndex];
				if (headerColumnWidth.Value.GridUnitType == GridUnitType.Pixel || columnIndex < lastLogPropertyIndex)
				{
					if (Math.Abs(headerColumnWidth.Value.Value - headerColumn.ActualWidth) > 0.5)
						headerColumnWidth.Update(new GridLength(headerColumn.ActualWidth, GridUnitType.Pixel));
				}
			}
		}


		// Restart application as administrator role.
		void RestartAsAdministrator()
		{
			if (this.canRestartAsAdmin.Value)
				App.Current.Restart(true);
		}


		/// <summary>
		/// Command to restart application as administrator role.
		/// </summary>
		public ICommand RestartAsAdministratorCommand { get; }


		/// <summary>
		/// Command to save all logs to file.
		/// </summary>
		public ICommand SaveAllLogsCommand { get; }


		// Save logs to file.
		async void SaveLogs(bool saveAllLogs)
		{
			// check state
			this.VerifyAccess();
			if (this.isSelectingFileToSaveLogs)
				return;
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to save logs without attaching to window");
				return;
			}
			if (this.DataContext is not Session session)
				return;

			// select target file
			this.isSelectingFileToSaveLogs = true;
			var app = this.Application;
			var fileName = await FileSystemItemSelection.SelectFileToExportLogsAsync(
				this.attachedWindow,
				saveAllLogs
					? app.GetString("SessionView.SaveAllLogs")
					: app.GetString("SessionView.SaveLogs"));
			this.isSelectingFileToSaveLogs = false;
			if (string.IsNullOrEmpty(fileName))
				return;

			// prepare options
			var logs = saveAllLogs ? session.AllLogs : session.Logs;
			var options = Path.GetExtension(fileName).ToLower() == ".json"
				? new JsonLogsSavingOptions(logs).Also(it =>
				{
					it.LogPropertyMap = new Dictionary<string, string>().Also(map =>
					{
						foreach (var logProperty in session.DisplayLogProperties)
							map[logProperty.ToLogProperty().Name] = logProperty.DisplayName;
					});
				})
				: new LogsSavingOptions(logs);
			options.FileName = fileName;

			// setup options by user
			if (options is JsonLogsSavingOptions jsonLogsSavingOptions)
			{
				if (this.attachedWindow == null)
					return;
				if (await new JsonLogsSavingOptionsDialog()
				{
					LogsSavingOptions = jsonLogsSavingOptions,
				}.ShowDialog<JsonLogsSavingOptions?>(this.attachedWindow) == null)
				{
					return;
				}
			}

			// save logs
			session.SaveLogsCommand.TryExecute(options);
		}


		/// <summary>
		/// Command to save logs to file.
		/// </summary>
		public ICommand SaveLogsCommand { get; }


		// Scroll to latest log.
		void ScrollToLatestLog(bool smoothScrolling = true)
		{
			// check state
			if (!this.IsScrollingToLatestLogNeeded)
				return;
			if (this.DataContext is not Session session)
				return;
			if (session.Logs.IsEmpty() || session.LogProfile == null || !session.IsActivated)
				return;
				
			// cancel scrolling
			if (this.logActionMenu.IsOpen || this.logMarkingMenu.IsOpen)
			{
				this.IsScrollingToLatestLogNeeded = false;
				this.isSmoothScrollingToLatestLog = false;
				return;
			}

			// scroll to latest log
			this.logScrollViewer?.Let(scrollViewer =>
			{
				var extent = scrollViewer.Extent;
				var viewport = scrollViewer.Viewport;
				if (extent.Height > viewport.Height)
				{
					var currentOffset = scrollViewer.Offset;
					var targetOffset = session.LogProfile.SortDirection == SortDirection.Ascending
						? (extent.Height - viewport.Height)
						: 0;
					var distanceY = targetOffset - currentOffset.Y;
					if (Math.Abs(distanceY) < 1)
					{
						this.isSmoothScrollingToLatestLog = false;
						this.scrollToLatestLogAction.Cancel();
						this.smoothScrollToLatestLogAction.Cancel();
					}
					else if (Math.Abs(distanceY) <= 5 
					         || !smoothScrolling 
					         || !this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.UseSmoothLogScrolling))
					{
						scrollViewer.Offset = new(currentOffset.X, currentOffset.Y + distanceY);
						this.isSmoothScrollingToLatestLog = false;
						this.scrollToLatestLogAction.Cancel();
						this.smoothScrollToLatestLogAction.Cancel();
					}
					else
					{
						distanceY /= 2;
						if (Math.Abs(distanceY) > 200)
						{
							targetOffset = session.LogProfile.SortDirection == SortDirection.Ascending
								? (extent.Height - viewport.Height) - 200
								: 200;
							scrollViewer.Offset = new(currentOffset.X, targetOffset);
						}
						else
							scrollViewer.Offset = new(currentOffset.X, currentOffset.Y + distanceY);
						this.isSmoothScrollingToLatestLog = true;
						this.scrollToLatestLogAction.Cancel();
						this.smoothScrollToLatestLogAction.Schedule(SmoothScrollingToLatestLogInterval);
					}
				}
			});
		}


		// Scroll to specific log.
		void ScrollToLog(DisplayableLog log, bool moveToCenter = false)
		{
			if (this.DataContext is not Session session)
				return;
			var index = session.Logs.IndexOf(log);
			if (index < 0)
				return;
			this.ScrollToLog(session, index, log, moveToCenter);
		}
		void ScrollToLog(int index, bool moveToCenter = false)
		{
			if (this.DataContext is not Session session)
				return;
			if (index < 0 || index >= session.Logs.Count)
				return;
			this.ScrollToLog(session, index, session.Logs[index], moveToCenter);
		}
		void ScrollToLog(Session session, int index, DisplayableLog log, bool moveToCenter)
		{
			if (this.targetLogRangeToScrollTo is not null)
			{
				if (this.targetLogRangeToScrollTo[0] != this.targetLogRangeToScrollTo[1]
				    || this.targetLogRangeToScrollTo[0] != log)
				{
					this.ClearTargetLogRangeToScrollTo();
				}
			}
			bool isLogVisible = this.logListBox.Let(logListBox =>
			{
				foreach (var container in logListBox.GetRealizedContainers())
				{
					if (logListBox.IndexFromContainer(container) == index)
						return true;
				}
				return false;
			});
			if (isLogVisible && !moveToCenter)
				return;
			this.targetLogRangeToScrollTo = new[] { log, log };
			this.Logger.LogTrace("Start scrolling to log at position {index}", index);
			this.SetValue(IsScrollingToTargetLogRangeProperty, true);
			this.scrollToTargetLogRangeAction.Execute();
		}


		// Scroll to first selected log.
		void ScrollToSelectedLog()
		{
			if (this.DataContext is not Session)
				return;
			if (this.latestSelectedDisplayableLog is null)
				return;
			this.ScrollToLog(this.latestSelectedDisplayableLog);
			this.logScrollViewer?.Let(scrollViewer =>
			{
				if (scrollViewer.Extent.Height > scrollViewer.Viewport.Height)
					this.IsScrollingToLatestLogNeeded = false;
			});
		}


		// Scroll to target log range if available.
		void ScrollToTargetLogRange()
		{
			// check state
			if (this.targetLogRangeToScrollTo == null || this.latestDisplayedLogRange == null)
				return;
			if (this.DataContext is not Session session)
				return;
			var logs = session.Logs;
			if (logs.IsEmpty())
				return;
			
			// find target range in current list
			var targetStartIndex = logs.BinarySearch(this.targetLogRangeToScrollTo[0], session.CompareDisplayableLogs);
			if (targetStartIndex < 0)
				targetStartIndex = ~targetStartIndex;
			var targetEndIndex = logs.BinarySearch(this.targetLogRangeToScrollTo[1], session.CompareDisplayableLogs);
			if (targetEndIndex < 0)
				targetEndIndex = ~targetEndIndex;
			var targetCenterIndex = this.targetMarkedLogsToScrollTo.IsEmpty() 
				? (targetStartIndex + targetEndIndex) >> 1
				: logs.BinarySearch(this.targetMarkedLogsToScrollTo[this.targetMarkedLogsToScrollTo.Count >> 1], session.CompareDisplayableLogs).Let(it =>
					it >= 0 ? it : ~it);

			// find current range in list
			var displayedStartIndex = logs.BinarySearch(this.latestDisplayedLogRange[0], session.CompareDisplayableLogs);
			var displayedEndIndex = logs.BinarySearch(this.latestDisplayedLogRange[1], session.CompareDisplayableLogs);
			var displayedCenterIndex = (displayedStartIndex + displayedEndIndex) >> 1;
			this.Logger.LogTrace("Displayed log range: [{displayedStartIndex}, {displayedEndIndex}], center: {displayedCenterIndex}", displayedStartIndex, displayedEndIndex, displayedCenterIndex);
			
			// complete scrolling
			if (Math.Abs(targetCenterIndex - displayedCenterIndex) <= 2)
			{
				this.Logger.LogTrace("Complete scrolling to target log range: [{targetStartIndex}, {targetEndIndex}], center: {targetCenterIndex}", targetStartIndex, targetEndIndex, targetCenterIndex);
				this.ClearTargetLogRangeToScrollTo();
				return;
			}

			// scroll to target range
			int indexToScrollTo;
			if (targetCenterIndex >= displayedCenterIndex)
			{
				indexToScrollTo = Math.Min(logs.Count - 1, targetCenterIndex + (this.latestDisplayedLogCount >> 1));
				this.Logger.LogTrace("Scroll down to target log at position {index}, target range: [{targetStartIndex}, {targetEndIndex}], center: {targetCenterIndex}", indexToScrollTo, targetStartIndex, targetEndIndex, targetCenterIndex);
				if (displayedEndIndex == logs.Count - 1 && !session.LogFiltering.IsFiltering)
				{
					this.Logger.LogTrace("Cannot scroll down anymore");
					this.ClearTargetLogRangeToScrollTo();
					return;
				}
			}
			else
			{
				indexToScrollTo = Math.Max(0, targetCenterIndex - (this.latestDisplayedLogCount >> 1));
				this.Logger.LogTrace("Scroll up to target log at position {index}, target range: [{targetStartIndex}, {targetEndIndex}], center: {targetCenterIndex}", indexToScrollTo, targetStartIndex, targetEndIndex, targetCenterIndex);
				if (displayedStartIndex == 0 && !session.LogFiltering.IsFiltering)
				{
					this.Logger.LogTrace("Cannot scroll up anymore");
					this.ClearTargetLogRangeToScrollTo();
					return;
				}
			}
			var scrollDirection = (targetCenterIndex - displayedCenterIndex);
			if (this.latestTargetLogRangeScrollingDirection != 0
			    && (this.latestTargetLogRangeScrollingDirection > 0) != (scrollDirection > 0))
			{
				++this.targetLogRangeScrollingDirectionChangeCount;
				if (this.targetLogRangeScrollingDirectionChangeCount >= 3)
				{
					this.Logger.LogWarning("Scrolling direction has been changed too many times");
					this.Logger.LogTrace("Complete scrolling to target log range: [{targetStartIndex}, {targetEndIndex}], center: {targetCenterIndex}", targetStartIndex, targetEndIndex, targetCenterIndex);
					if (targetCenterIndex < displayedStartIndex || targetCenterIndex >= displayedEndIndex)
						this.logListBox.ScrollIntoView(targetCenterIndex);
					this.ClearTargetLogRangeToScrollTo();
					return;
				}
			}
			else
				this.targetLogRangeScrollingDirectionChangeCount = 0;
			this.scrollToTargetLogRangeAction.Schedule(ScrollingToTargetLogRangeInterval);
			this.latestTargetLogRangeScrollingDirection = scrollDirection;
			this.logListBox.ScrollIntoView(indexToScrollTo);
		}
		
		
		// Select and set command.
		async Task SelectAndSetCommandAsync()
		{
			// check state
			if (this.attachedWindow is null)
			{
				this.Logger.LogError("Unable to set command without attaching to window");
				return;
			}
			if (this.DataContext is not Session session)
				return;

			// cancel scheduled action
			this.autoSetCommandAction.Cancel();

			// select command
			var dialog = new StandardOutputCommandDialog { Command = session.Command };
			session.LogProfile?.Let(profile =>
			{
				if (profile.DataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UseTextShell)))
					dialog.UseTextShell = session.UseTextShellToExecuteCommand ?? profile.DataSourceOptions.UseTextShell;
			});
			var command = await dialog.ShowDialog<string?>(this.attachedWindow);
			if (string.IsNullOrEmpty(command) || this.DataContext != session)
				return;

			// check state
			if (!this.canSetCommand.Value)
				return;

			// set end point
			session.SetCommandCommand.TryExecute(new Session.CommandParams
			{
				Command = command,
				UseTextShell = dialog.UseTextShell,
			});
		}


		/// <summary>
		/// Command to select and set command.
		/// </summary>
		public ICommand SelectAndSetCommandCommand { get; }


		// Select and set IP endpoint.
		async Task SelectAndSetIPEndPointAsync()
		{
			// check state
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to set IP endpoint without attaching to window");
				return;
			}
			if (this.DataContext is not Session session)
				return;

			// cancel scheduled action
			this.autoSetIPEndPointAction.Cancel();
			
			// check clipboard
			var ipEndpoint = session.IPEndPoint;
			var useIPAddressInClipboard = false;
			//

			// select IP endpoint
			var endPoint = await new IPEndPointInputDialog().Also(it =>
			{
				it.FocusOnPort = useIPAddressInClipboard;
				it.InitialIPEndPoint = ipEndpoint;
				it.BindToResource(Avalonia.Controls.Window.TitleProperty, "String/SessionView.SetIPEndPoint");
			}).ShowDialog<IPEndPoint?>(this.attachedWindow);
			if (endPoint is null)
				return;

			// check state
			if (!this.canSetIPEndPoint.Value)
				return;

			// set end point
			session.SetIPEndPointCommand.TryExecute(endPoint);
		}


		/// <summary>
		/// Command to select and set IP endpoint.
		/// </summary>
		public ICommand SelectAndSetIPEndPointCommand { get; }


		/// <summary>
		/// Select and set log profile.
		/// </summary>
		public async Task SelectAndSetLogProfileAsync()
		{
			// check state
			this.VerifyAccess();
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to set log profile without attaching to window");
				return;
			}

			// select profile
			var logProfile = await new LogProfileSelectionDialog().ShowDialog<LogProfile?>(this.attachedWindow);
			if (logProfile == null)
				return;
			
			// set log profile
			await this.SetLogProfileAsync(logProfile);
		}


		/// <summary>
		/// Command to set log profile.
		/// </summary>
		public ICommand SelectAndSetLogProfileCommand { get; }


		// Select and set process ID.
		async Task SelectAndSetProcessIdAsync()
		{
			// check state
			this.VerifyAccess();
			if (this.attachedWindow is null)
			{
				this.Logger.LogError("Unable to set process ID without attaching to window");
				return;
			}
			if (this.DataContext is not Session session)
			{
				this.Logger.LogError("Unable to set process ID without session");
				return;
			}
			
			// select process ID
			this.autoSetProcessIdAction.Cancel();
			int? processId;
			var processIdText = session.ProcessId?.ToString();
			while (true)
			{
				processIdText = (await new TextInputDialog
				{
					InitialText = processIdText,
					Message = this.Application.GetObservableString("SessionView.SetProcessId.Message"),
					MaxTextLength = 10,
					Title = this.Application.GetObservableString("SessionView.SetProcessId"),
				}.ShowDialog(this.attachedWindow))?.Trim();
				if (string.IsNullOrEmpty(processIdText) || this.attachedWindow is null)
					return;
				if (int.TryParse(processIdText, out var pid) && pid >= 0)
				{
					processId = pid;
					break;
				}
				await new MessageDialog
				{
					Icon = MessageDialogIcon.Warning,
					Message = this.Application.GetObservableString("SessionView.SetProcessId.Invalid"),
				}.ShowDialog(this.attachedWindow);
			}
			if (this.DataContext != session)
				return;
			
			// set process ID
			session.SetProcessIdCommand.TryExecute(processId);
		}
		
		
		/// <summary>
		/// Command to select and set process ID.
		/// </summary>
		public ICommand SelectAndSetProcessIdCommand { get; }
		
		
		// Select and set process name.
		async Task SelectAndSetProcessNameAsync()
		{
			// check state
			this.VerifyAccess();
			if (this.attachedWindow is null)
			{
				this.Logger.LogError("Unable to set process name without attaching to window");
				return;
			}
			if (this.DataContext is not Session session)
			{
				this.Logger.LogError("Unable to set process name without session");
				return;
			}
			
			// select process name
			this.autoSetProcessNameAction.Cancel();
			var processName = (await new TextInputDialog
			{
				InitialText = session.ProcessName,
				Message = this.Application.GetObservableString("SessionView.SetProcessName.Message"),
				MaxTextLength = 128,
				Title = this.Application.GetObservableString("SessionView.SetProcessName"),
			}.ShowDialog(this.attachedWindow))?.Trim();
			if (string.IsNullOrEmpty(processName) || this.DataContext != session)
				return;
			
			// set process name
			session.SetProcessNameCommand.TryExecute(processName);
		}
		
		
		/// <summary>
		/// Command to select and set process name.
		/// </summary>
		public ICommand SelectAndSetProcessNameCommand { get; }


		// Select and set URI.
		async Task SelectAndSetUriAsync()
        {
			// check state
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to set URI without attaching to window");
				return;
			}
			if (this.DataContext is not Session session)
				return;

			// cancel scheduled action
			this.autoSetUriAction.Cancel();
			
			// check clipboard
			var uri = default(Uri);
			//

			// select URI
			if (uri is null)
			{
				uri = await new UriInputDialog().Also(it =>
				{
					it.DefaultScheme = session.LogProfile?.DataSourceProvider.Name == "Http" ? "https" : null;
					it.InitialUri = session.Uri;
					it.BindToResource(Avalonia.Controls.Window.TitleProperty, "String/SessionView.SetUri");
				}).ShowDialog<Uri?>(this.attachedWindow);
				if (uri is null)
					return;
			}

			// check state
			if (!this.canSetUri.Value)
				return;

			// set URI
			session.SetUriCommand.TryExecute(uri);
		}


		/// <summary>
		/// Command to select and set URI.
		/// </summary>
		public ICommand SelectAndSetUriCommand { get; }


		// Select and set working directory.
		async Task SelectAndSetWorkingDirectoryAsync()
		{
			// check state
			if (this.attachedWindow is null)
			{
				this.Logger.LogError("Unable to set working directory without attaching to window");
				return;
			}

			// cancel scheduled action
			this.autoSetWorkingDirectoryAction.Cancel();

			// select directory
			var directory = await FileSystemItemSelection.SelectWorkingDirectory(this.attachedWindow);
			if (string.IsNullOrWhiteSpace(directory))
				return;

			// check state
			if (!this.canSetWorkingDirectory.Value)
				return;
			if (this.DataContext is not Session session)
				return;

			// set working directory
			session.SetWorkingDirectoryCommand.TryExecute(directory);
		}


		/// <summary>
		/// Command to set working directory.
		/// </summary>
		public ICommand SelectAndSetWorkingDirectoryCommand { get; }


		// Let user select the precondition of log reading.
		async Task<Logs.LogReadingPrecondition?> SelectLogReadingPreconditionAsync(LogDataSourceType sourceType, Logs.LogReadingPrecondition initPrecondition, bool isCancellable)
		{
			// check state
			if (this.attachedWindow == null || this.DataContext is not Session session)
				return new();
			
			// check whether precondition is needed or not
			if (!session.HasTimestampDisplayableLogProperty)
				return new();
			
			// select precondition
			var precondition = await new LogReadingPreconditionDialog()
			{
				IsCancellationAllowed = isCancellable,
				IsReadingFromFiles = (sourceType & LogDataSourceType.File) != 0,
				Precondition = initPrecondition,
			}.ShowDialog<Logs.LogReadingPrecondition?>(this.attachedWindow);
			return precondition;
		}


		/// <summary>
		/// Select log by timestamp.
		/// </summary>
		public async void SelectNearestLogByTimestamp()
        {
			// check state
			if (this.DataContext is not Session session)
				return;
			if (!session.AreLogsSortedByTimestamp)
				return;

			// get window
			if (this.attachedWindow == null)
				return;

			// select timestamp
			var timestamp = await new DateTimeSelectionDialog().Also(it =>
			{
				it.InitialDateTime = session.LogSelection.EarliestSelectedLogTimestamp ?? session.EarliestLogTimestamp;
				it.Message = this.Application.GetString("SessionView.SelectNearestLogByTimestamp.Message");
				it.BindToResource(Avalonia.Controls.Window.TitleProperty, "String/SessionView.SelectNearestLogByTimestamp.Title");
			}).ShowDialog<DateTime?>(this.attachedWindow);
			if (timestamp == null)
				return;

			// select log
			if (session.LogSelection.SelectNearestLog(timestamp.Value) == null)
				this.logListBox.SelectedItems!.Clear();
        }


		/// <summary>
		/// Set given log profile to <see cref="Session"/> hosted by this view.
		/// </summary>
		/// <param name="logProfile">Log profile.</param>
		/// <returns>True if log profile has been set successfully.</returns>
		public async Task<bool> SetLogProfileAsync(LogProfile logProfile)
		{
			// check state
			this.VerifyAccess();
			if (this.DataContext is not Session session)
				return false;
			if (session.LogProfile == logProfile)
				return true;

			// check administrator role
			var isRestartingAsAdminNeeded = false;
			if (logProfile.IsAdministratorNeeded && !this.Application.IsRunningAsAdministrator)
			{
				if (await this.ConfirmRestartingAsAdmin(logProfile))
					isRestartingAsAdminNeeded = true;
				else
				{
					this.Logger.LogWarning("Unable to use profile '{logProfileName}' ({logProfileId}) because application is not running as administrator", logProfile.Name, logProfile.Id);
					return false;
				}
			}

			// enable running script
			if (!await this.ConfirmEnablingRunningScript(logProfile))
			{
				this.Logger.LogWarning("Unable to use profile '{logProfileName}' ({logProfileId}) because script running is not enabled yet", logProfile.Name, logProfile.Id);
				return false;
			}

			// reset log profile
			this.isCommandNeededAfterLogProfileSet = false;
			this.isIPEndPointNeededAfterLogProfileSet = false;
			this.isLogFileNeededAfterLogProfileSet = false;
			this.isRestartingAsAdminConfirmed = false;
			this.isProcessIdNeededAfterLogProfileSet = false;
			this.isProcessNameNeededAfterLogProfileSet = false;
			this.isUriNeededAfterLogProfileSet = false;
			this.isWorkingDirNeededAfterLogProfileSet = false;
			this.autoAddLogFilesAction.Cancel();
			this.autoSetCommandAction.Cancel();
			this.autoSetIPEndPointAction.Cancel();
			this.autoSetProcessIdAction.Cancel();
			this.autoSetProcessNameAction.Cancel();
			this.autoSetUriAction.Cancel();
			this.autoSetWorkingDirectoryAction.Cancel();
			session.ResetLogProfileCommand.TryExecute(null);

			// set log profile
			if (!session.SetLogProfileCommand.TryExecute(logProfile))
			{
				this.Logger.LogError("Unable to set log profile '{logProfileName}' ({logProfileId}) to session", logProfile.Name, logProfile.Id);
				return false;
			}
			this.OnLogProfileSet(logProfile);

			// restart as administrator role
			if (isRestartingAsAdminNeeded)
				this.RestartAsAdministrator();

			// complete
			return true;
		}


		// Show file in system file explorer.
		void ShowFileInExplorer()
		{
			// check state
			this.VerifyAccess();
			if (!this.canShowFileInExplorer.Value)
				return;

			// collect paths
			var comparer = PathEqualityComparer.Default;
			var filePath = (string?)null;
			var dirPathSet = new HashSet<string>(comparer);
			foreach (DisplayableLog log in this.logListBox.SelectedItems!)
			{
				var fileName = log.FileName?.ToString();
				if (string.IsNullOrEmpty(fileName))
					continue;
				if (filePath == null && dirPathSet.IsEmpty())
					filePath = fileName;
				else if (filePath != null && !comparer.Equals(filePath, fileName))
				{
					dirPathSet.Add(Path.GetDirectoryName(filePath) ?? "");
					dirPathSet.Add(Path.GetDirectoryName(fileName) ?? "");
					filePath = null;
				}
				else
					dirPathSet.Add(Path.GetDirectoryName(fileName) ?? "");
			}

			// show in system file explorer
			if (filePath != null)
				Platform.OpenFileManager(filePath);
			else if (dirPathSet.IsNotEmpty())
			{
				foreach (var path in dirPathSet)
					Platform.OpenFileManager(path);
			}
		}


		/// <summary>
		/// Command to show file in system file explorer.
		/// </summary>
		public ICommand ShowFileInExplorerCommand { get; }


		// Show single log file in system file manager.
#pragma warning disable CA1822
		void ShowLogFileInExplorer(string filePath) => 
			Platform.OpenFileManager(filePath);
#pragma warning restore CA1822


		/// <summary>
		/// Command to show single log file in system file manager.
		/// </summary>
		public ICommand ShowLogFileInExplorerCommand { get; }
		

		/// <summary>
		/// Show menu to select log profile.
		/// </summary>
		public void ShowLogProfileSelectionMenu() =>
			this.logProfileSelectionMenu.Open(this.selectAndSetLogProfileDropDownButton);


		/// <summary>
		/// Show menu for saving logs.
		/// </summary>
		public void ShowLogsSavingMenu() =>
			this.logsSavingMenu.Open(this.logsSavingButton);


		// Show full log string property.
		bool ShowLogStringProperty()
		{
			// check state
			if (this.logListBox.SelectedItems!.Count != 1)
				return false;

			// find property and log
			var clickedPropertyView = this.lastClickedLogPropertyView;
			if (clickedPropertyView == null || clickedPropertyView.Tag is not DisplayableLogProperty property)
				return false;
			var listBoxItem = clickedPropertyView.FindLogicalAncestorOfType<ListBoxItem>();
			if (listBoxItem == null)
				return false;
			var log = (listBoxItem.DataContext as DisplayableLog);
			if (log == null || this.logListBox.SelectedItems[0] != log)
				return false;
			if (!DisplayableLog.HasStringProperty(property.Name))
				return false;

			// show property
			return this.ShowLogStringProperty(log, property);
		}
		bool ShowLogStringProperty(DisplayableLog log, DisplayableLogProperty property)
		{
			if (this.attachedWindow == null)
				return false;
			new LogStringPropertyDialog()
			{
				Log = log,
				LogPropertyDisplayName = property.DisplayName,
				LogPropertyName = property.Name,
			}.ShowDialog(this.attachedWindow);
			return true;
		}


		/// <summary>
		/// Command to show string log property.
		/// </summary>
		public ICommand ShowLogStringPropertyCommand { get; }


		// Show next tutorial is available.
		bool ShowNextTutorial()
		{
			// check state
			if (this.areAllTutorialsShown)
				return false;
			if (this.attachedWindow is not CarinaStudio.AppSuite.Controls.Window window)
				return false;
			if ((window as MainWindow)?.AreInitialDialogsClosed == false
				|| window.HasDialogs
				|| window.CurrentTutorial != null)
			{
				return false;
			}
			if ((this.DataContext as Session)?.IsActivated != true)
				return false;
			
			// show "select log profile to start"
			var persistentState = this.PersistentState;
			if (!persistentState.GetValueOrDefault(IsSelectingLogProfileToStartTutorialShownKey))
			{
				if (IAppSuiteApplication.CurrentOrNull?.IsFirstLaunch == false)
				{
					// no need to show this tutorial for upgrade case
					persistentState.SetValue<bool>(IsSelectingLogProfileToStartTutorialShownKey, true);
				}
				else
				{
					return window.ShowTutorial(new Tutorial().Also(it =>
					{
						it.Anchor = this.FindControl<Control>("selectAndSetLogProfileButton");
						it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("SessionView.Tutorial.SelectLogProfileToStart"));
						it.Dismissed += (_, _) => 
						{
							persistentState.SetValue<bool>(IsSelectingLogProfileToStartTutorialShownKey, true);
							this.ShowNextTutorial();
						};
						it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
						it.SkippingAllTutorialRequested += (_, _) => this.SkipAllTutorials();
					}));
				}
			}

			// show "use add tab button to select log profile"
			if ((window as MainWindow)?.ShowTutorialOfUsingAddTabButtonToSelectLogProfile(() => this.ShowNextTutorial(), this.SkipAllTutorials) == true)
				return true;

			// show "switch side panels"
			if (!persistentState.GetValueOrDefault(IsSwitchingSidePanelsTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("sidePanelBoolBarItemsPanel");
					it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("SessionView.Tutorial.SwitchSidePanels"));
					it.Dismissed += (_, _) => 
					{
						persistentState.SetValue<bool>(IsSwitchingSidePanelsTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, _) => this.SkipAllTutorials();
				}));
			}

			// show side panel tutorials
			if (!persistentState.GetValueOrDefault(IsMarkedLogsPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("markedLogsPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("SessionView.Tutorial.MarkedLogsPanel"));
					it.Dismissed += (_, _) => 
					{
						persistentState.SetValue<bool>(IsMarkedLogsPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, _) => this.SkipAllTutorials();
				}));
			}
			if (!persistentState.GetValueOrDefault(IsTimestampCategoriesPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("timestampCategoriesPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("SessionView.Tutorial.TimestampCategoriesPanel"));
					it.Dismissed += (_, _) => 
					{
						persistentState.SetValue<bool>(IsTimestampCategoriesPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, _) => this.SkipAllTutorials();
				}));
			}
			if (!persistentState.GetValueOrDefault(IsLogAnalysisPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("logAnalysisPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("SessionView.Tutorial.LogAnalysisPanel"));
					it.Dismissed += (_, _) => 
					{
						persistentState.SetValue<bool>(IsLogAnalysisPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, _) => this.SkipAllTutorials();
				}));
			}
			if (!persistentState.GetValueOrDefault(IsLogFilesPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("logFilesPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.Application.GetObservableString("SessionView.Tutorial.LogFilesPanel"));
					it.Dismissed += (_, _) => 
					{
						persistentState.SetValue<bool>(IsLogFilesPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, _) => this.SkipAllTutorials();
				}));
			}

			// all tutorials shown
			this.SetAndRaise(AreAllTutorialsShownProperty, ref this.areAllTutorialsShown, true);
			return false;
		}


		/// <summary>
		/// Show UI of other actions.
		/// </summary>
		public void ShowOtherActions()
		{
			this.otherActionsMenu.PlacementTarget ??= this.otherActionsButton;
			this.otherActionsMenu.Open(this);
		}


		/// <summary>
		/// Show dialog to manage script log data source providers.
		/// </summary>
		public void ShowScriptLogDataSourceProvidersDialog()
		{
			if (this.attachedWindow != null)
				_ = new ScriptLogDataSourceProvidersDialog().ShowDialog(this.attachedWindow);
		}


		/// <summary>
		/// Show test menu.
		/// </summary>
		public void ShowTestMenu()
		{
			if (!this.Application.IsDebugMode)
				return;
			this.testMenu.Open(this.testButton);
		}


		/// <summary>
		/// Show working directory actions menu.
		/// </summary>
		public void ShowWorkingDirectoryActions()
        {
			this.workingDirectoryActionsMenu.PlacementTarget ??= this.workingDirectoryActionsButton;
			this.workingDirectoryActionsMenu.Open(this.workingDirectoryActionsButton);
        }


		// Show working directory in system file explorer.
		void ShowWorkingDirectoryInExplorer()
		{
			// check state
			this.VerifyAccess();
			if (!this.canShowWorkingDirectoryInExplorer.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			var workingDirectory = session.WorkingDirectoryPath;
			if (string.IsNullOrEmpty(workingDirectory))
				return;

			// open system file explorer
			Platform.OpenFileManager(workingDirectory);
		}


		/// <summary>
		/// Command to show working directory in system file explorer.
		/// </summary>
		public ICommand ShowWorkingDirectoryInExplorerCommand { get; }


		// Skip all tutorials.
		void SkipAllTutorials()
		{
			(this.attachedWindow as MainWindow)?.SkipAllTutorials();
			if (this.areAllTutorialsShown)
				return;
			this.PersistentState.Let(it =>
			{
				it.SetValue<bool>(IsLogAnalysisPanelTutorialShownKey, true);
				it.SetValue<bool>(IsLogFilesPanelTutorialShownKey, true);
				it.SetValue<bool>(IsMarkedLogsPanelTutorialShownKey, true);
				it.SetValue<bool>(IsSelectingLogProfileToStartTutorialShownKey, true);
				it.SetValue<bool>(IsSwitchingSidePanelsTutorialShownKey, true);
				it.SetValue<bool>(IsTimestampCategoriesPanelTutorialShownKey, true);
			});
			this.SetAndRaise(AreAllTutorialsShownProperty, ref this.areAllTutorialsShown, true);
		}
		
		
		// Keep current displayed log range.
		void StartKeepingCurrentDisplayedLogRange()
		{
			if (!this.IsScrollingToLatestLogNeeded)
			{
				if (this.targetLogRangeToScrollTo is null)
					this.targetLogRangeToScrollTo = this.latestDisplayedLogRange?.Clone() as DisplayableLog[];
				else if (this.latestDisplayedLogRange is null)
					return;
				this.targetMarkedLogsToScrollTo.Clear();
				this.targetMarkedLogsToScrollTo.AddRange(this.latestDisplayedMarkedLogs);
				if (this.targetLogRangeToScrollTo != null)
				{
					this.Logger.LogTrace("Setup target range of log to scroll to, marked log(s): {count}", this.targetMarkedLogsToScrollTo.Count);
					this.SetValue(IsScrollingToTargetLogRangeProperty, true);
				}
			}
		}


		/// <summary>
		/// Get current state of status bar.
		/// </summary>
		public SessionViewStatusBarState StatusBarState => this.GetValue(StatusBarStateProperty);


		/// <summary>
		/// Take single memory snapshot.
		/// </summary>
		public void TakeMemorySnapshot()
		{
			if (this.attachedWindow != null)
				((App) this.Application).TakeMemorySnapshotAsync(this.attachedWindow);
		}


		// Test functions.
		void Test(string command)
		{
			if (!this.Application.IsDebugMode)
				return;
			switch (command)
			{
				case "ExportImageResource":
					if (this.attachedWindow is not null)
						_ = Utility.ExportImage(this.attachedWindow);
					break;
				case "RestartApp":
					(IAppSuiteApplication.CurrentOrNull as App)?.Restart();
					break;
				case "RestartRootWindows":
					this.Application.RestartRootWindowsAsync();
					break;
			}
		}


		/// <summary>
		/// Command to perform test command.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="String"/>.</remarks>
		public ICommand TestCommand { get; }


		// Unmark logs.
		void UnmarkSelectedLogs()
		{
			if (!this.canUnmarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			session.UnmarkLogsCommand.TryExecute(this.logListBox.SelectedItems!.Cast<DisplayableLog>().ToArray());
		}


		/// <summary>
		/// Command to unmark selected logs.
		/// </summary>
		public ICommand UnmarkSelectedLogsCommand { get; }


		// Update auto scrolling state according to user scrolling state.
		void UpdateIsScrollingToLatestLogNeeded(double userScrollingDelta)
		{
			var logScrollViewer = this.logScrollViewer;
			if (logScrollViewer == null)
				return;
			var logProfile = (this.HasLogProfile ? (this.DataContext as Session)?.LogProfile : null);
			if (logProfile == null)
				return;
			var offset = logScrollViewer.Offset;
			if (Math.Abs(offset.Y) < 0.5 && offset.Y + logScrollViewer.Viewport.Height >= logScrollViewer.Extent.Height)
				return;
			if (this.IsScrollingToLatestLogNeeded)
			{
				if (logProfile.SortDirection == SortDirection.Ascending)
				{
					if (userScrollingDelta < 0)
					{
						this.Logger.LogDebug("Cancel auto scrolling because of user scrolling up");
						this.IsScrollingToLatestLogNeeded = false;
					}
				}
				else
				{
					if (userScrollingDelta > 0)
					{
						this.Logger.LogDebug("Cancel auto scrolling because of user scrolling down");
						this.IsScrollingToLatestLogNeeded = false;
					}
				}
			}
			else if (logProfile.IsContinuousReading)
			{
				if (logProfile.SortDirection == SortDirection.Ascending)
				{
					if (userScrollingDelta > 0 && Math.Abs(logScrollViewer.Offset.Y + logScrollViewer.Viewport.Height - logScrollViewer.Extent.Height) <= 5)
					{
						this.Logger.LogDebug("Start auto scrolling because of user scrolling down");
						this.IsScrollingToLatestLogNeeded = true;
					}
				}
				else
				{
					if (userScrollingDelta < 0 && logScrollViewer.Offset.Y <= 5)
					{
						this.Logger.LogDebug("Start auto scrolling because of user scrolling up");
						this.IsScrollingToLatestLogNeeded = true;
					}
				}
			}
		}


		// Get and keep range of logs which are currently displayed.
		void UpdateLatestDisplayedLogRange()
		{
			if (this.logListBox.ItemCount <= 0 || this.DataContext is not Session session)
			{
				if (this.latestDisplayedLogRange != null)
				{
					this.SynchronizationContext.Post(() =>
					{
						if (this.logListBox.ItemCount <= 0 || this.DataContext is not Session)
						{
							if (this.latestDisplayedLogRange != null)
							{
								this.Logger.LogTrace("Clear range of latest displayed logs");
								this.latestDisplayedLogRange = null;
								this.latestDisplayedLogCount = 0;
								this.latestDisplayedMarkedLogs.Clear();
							}
						}
					});
				}
				return;
			}
			var firstLog = default(DisplayableLog);
			var lastLog = default(DisplayableLog);
			var logCount = 0;
			var logListBox = this.logListBox;
			this.latestDisplayedMarkedLogs.Clear();
			foreach (var itemContainer in logListBox.GetRealizedContainers())
			{
				if (logListBox.ItemFromContainer(itemContainer) is not DisplayableLog log)
					continue;
				++logCount;
				if (firstLog == null || session.CompareDisplayableLogs(log, firstLog) < 0)
					firstLog = log;
				if (lastLog == null || session.CompareDisplayableLogs(log, lastLog) > 0)
					lastLog = log;
				if (log.MarkedColor != MarkColor.None)
					this.latestDisplayedMarkedLogs.Add(log);
			}
			if (firstLog != null)
			{
				this.latestDisplayedLogRange ??= new DisplayableLog[2];
				this.latestDisplayedLogRange[0] = firstLog;
				this.latestDisplayedLogRange[1] = lastLog!;
				this.latestDisplayedLogCount = logCount;
				this.latestDisplayedMarkedLogs.Sort(session.CompareDisplayableLogs);
			}
			else
			{
				this.Logger.LogInformation("Clear range of latest displayed logs");
				this.latestDisplayedLogRange = null;
				this.latestDisplayedLogCount = 0;
			}
		}


		// Update layout of items on toolbar.
		void UpdateToolBarItemsLayout()
		{
			// check state
			// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (this.toolBarLogTextFilterItemsPanel == null) // Need to check because method may be called from constructor
				return;
			// ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			
			// get toolbar size
			var toolBarPadding = this.toolBarContainer.Padding;
			var toolBarWidth = this.toolBarContainer.Bounds.Width - toolBarPadding.Left - toolBarPadding.Right - 1;
			if (Math.Abs(this.lastToolBarWidthWhenLayoutItems - toolBarWidth) <= 0.01)
				return;
			
			// update panel size
			var minItemsPanelWidth = this.minLogTextFilterItemsPanelWidth;
			var itemsPanelWidth = minItemsPanelWidth;
			var currentItemsPanelWidth = this.toolBarLogTextFilterItemsPanel.Width;
			if (toolBarWidth > minItemsPanelWidth)
			{
				// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
				var prevItemsPanelWidth1 = (this.toolBarLogActionItemsPanel?.Bounds).GetValueOrDefault().Width;
				var prevItemsPanelWidth2 = (this.toolBarOtherLogFilterItemsPanel?.Bounds).GetValueOrDefault().Width;
				var nextItemsPanelWidth1 = (this.toolBarOtherItemsPanel?.Bounds).GetValueOrDefault().Width;
				// ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
				var remainingWidth = toolBarWidth
					- prevItemsPanelWidth1
					- prevItemsPanelWidth2
					- nextItemsPanelWidth1;
				if (remainingWidth >= minItemsPanelWidth)
					itemsPanelWidth = remainingWidth;
				else
				{
					var prevRemainingWidth = toolBarWidth - prevItemsPanelWidth1;
					if (prevRemainingWidth >= prevItemsPanelWidth2)
						prevRemainingWidth -= prevItemsPanelWidth2;
					else
						prevRemainingWidth = toolBarWidth - prevItemsPanelWidth2;
					var nextRemainingWidth = toolBarWidth - nextItemsPanelWidth1;
					remainingWidth = Math.Max(prevRemainingWidth, nextRemainingWidth);
					itemsPanelWidth = Math.Max(remainingWidth, minItemsPanelWidth);
				}
			}
			if (double.IsNaN(currentItemsPanelWidth)
				|| currentItemsPanelWidth > itemsPanelWidth + 0.01
				|| currentItemsPanelWidth < itemsPanelWidth + 1)
			{
				this.toolBarLogTextFilterItemsPanel.Width = itemsPanelWidth;
			}
			this.lastToolBarWidthWhenLayoutItems = toolBarWidth;
		}


		// Update state of side panel.
		void UpdateSidePanelState(ObservableProperty<bool>? changedProperty)
		{
			if (this.DataContext is not Session session)
				return;
			if (session.LogAnalysis.IsPanelVisible
				|| session.IsLogFilesPanelVisible 
				|| session.IsMarkedLogsPanelVisible
				|| session.LogCategorizing.IsTimestampCategoriesPanelVisible)
			{
				if (changedProperty == LogAnalysisViewModel.IsPanelVisibleProperty)
				{
					if (session.LogAnalysis.IsPanelVisible)
					{
						session.IsLogFilesPanelVisible = false;
						session.IsMarkedLogsPanelVisible = false;
						session.LogCategorizing.IsTimestampCategoriesPanelVisible = false;

						// show tutorial
						this.ShowLogAnalysisRuleSetsTutorial();
					}
				}
				else if (changedProperty == Session.IsLogFilesPanelVisibleProperty)
				{
					if (session.IsLogFilesPanelVisible)
					{
						session.LogAnalysis.IsPanelVisible = false;
						session.IsMarkedLogsPanelVisible = false;
						session.LogCategorizing.IsTimestampCategoriesPanelVisible = false;
					}
				}
				else if (changedProperty == Session.IsMarkedLogsPanelVisibleProperty)
				{
					if (session.IsMarkedLogsPanelVisible)
					{
						session.LogAnalysis.IsPanelVisible = false;
						session.IsLogFilesPanelVisible = false;
						session.LogCategorizing.IsTimestampCategoriesPanelVisible = false;
					}
				}
				else if (changedProperty == LogCategorizingViewModel.IsTimestampCategoriesPanelVisibleProperty)
				{
					if (session.LogCategorizing.IsTimestampCategoriesPanelVisible)
					{
						session.LogAnalysis.IsPanelVisible = false;
						session.IsLogFilesPanelVisible = false;
						session.IsMarkedLogsPanelVisible = false;
					}
				}
				this.keepSidePanelVisible = true;
				sidePanelColumn.Width = new GridLength(new[] {
					session.LogAnalysis.PanelSize,
					session.LogFilesPanelSize,
					session.MarkedLogsPanelSize,
					session.LogCategorizing.TimestampCategoriesPanelSize,
				}.Max());
				Grid.SetColumnSpan(this.logListBoxContainer, 1);
			}
			else
			{
				this.keepSidePanelVisible = false;
				sidePanelColumn.Width = new GridLength(0);
				Grid.SetColumnSpan(this.logListBoxContainer, 3);
			}
		}


		// Update menu items of tools.
		void UpdateToolsMenuItems()
		{ }


		/// <summary>
		/// List of log levels defined by log profile.
		/// </summary>
		public IList<Logs.LogLevel> ValidLogLevels { get; }
	}
}
