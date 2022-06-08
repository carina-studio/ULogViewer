using Avalonia;
using Avalonia.Collections;
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
using Avalonia.VisualTree;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Input;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// View of <see cref="Session"/>.
	/// </summary>
	partial class SessionView : UserControl<IULogViewerApplication>
	{
		/// <summary>
		/// Property of <see cref="AreAllTutorialsShown"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> AreAllTutorialsShownProperty = AvaloniaProperty.RegisterDirect<SessionView, bool>(nameof(AreAllTutorialsShown), v => v.areAllTutorialsShown);
		/// <summary>
		/// <see cref="IValueConverter"/> to convert log level to readable name.
		/// </summary>
		public static readonly IValueConverter LogLevelNameConverter = new LogLevelNameConverterImpl(App.Current);


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
		const int AutoAddLogFilesDelay = 0;
		const int MaxLogCountForCopying = 65536;
		const int ScrollingToLatestLogInterval = 100;


		// Static fields.
		static readonly AvaloniaProperty<bool> CanFilterLogsByNonTextFiltersProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(CanFilterLogsByNonTextFilters), false);
		static readonly AvaloniaProperty<DateTime?> EarliestSelectedLogTimestampProperty = AvaloniaProperty.Register<SessionView, DateTime?>(nameof(EarliestSelectedLogTimestamp));
		static readonly AvaloniaProperty<bool> HasLogProfileProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(HasLogProfile), false);
		static readonly AvaloniaProperty<bool> HasSelectedLogsDurationProperty = AvaloniaProperty.Register<SessionView, bool>("HasSelectedLogsDuration", false);
		static readonly SettingKey<bool> IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey = new("SessionView.IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShown");
		static readonly AvaloniaProperty<bool> IsProcessInfoVisibleProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsProcessInfoVisible), false);
		static readonly AvaloniaProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<SessionView, bool>("IsProVersionActivated", false);
		static readonly AvaloniaProperty<bool> IsScrollingToLatestLogNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogNeeded), true);
		static readonly SettingKey<bool> IsLogAnalysisPanelTutorialShownKey = new("SessionView.IsLogAnalysisPanelTutorialShown");
		static readonly SettingKey<bool> IsLogFilesPanelTutorialShownKey = new("SessionView.IsLogFilesPanelTutorialShown");
		static readonly SettingKey<bool> IsMarkedLogsPanelTutorialShownKey = new("SessionView.IsMarkedLogsPanelTutorialShown");
		static readonly SettingKey<bool> IsSelectLogAnalysisRuleSetsTutorialShownKey = new("SessionView.IsSelectLogAnalysisRuleSetsTutorialShown");
		static readonly SettingKey<bool> IsSelectingLogProfileToStartTutorialShownKey = new("SessionView.IsSelectingLogProfileToStartTutorialShown");
		static readonly SettingKey<bool> IsShowAllLogsForLogAnalysisResultTutorialShownKey = new("SessionView.IsShowAllLogsForLogAnalysisResultTutorialShown");
		static readonly SettingKey<bool> IsSwitchingSidePanelsTutorialShownKey = new("SessionView.IsSwitchingSidePanelsTutorialShown");
		static readonly SettingKey<bool> IsTimestampCategoriesPanelTutorialShownKey = new("SessionView.IsTimestampCategoriesPanelTutorialShown");
		static readonly AvaloniaProperty<DateTime?> LatestSelectedLogTimestampProperty = AvaloniaProperty.Register<SessionView, DateTime?>(nameof(LatestSelectedLogTimestamp));
		static readonly AvaloniaProperty<FontFamily> LogFontFamilyProperty = AvaloniaProperty.Register<SessionView, FontFamily>(nameof(LogFontFamily));
		static readonly AvaloniaProperty<double> LogFontSizeProperty = AvaloniaProperty.Register<SessionView, double>(nameof(LogFontSize), 10.0);
		static readonly AvaloniaProperty<int> MaxDisplayLineCountForEachLogProperty = AvaloniaProperty.Register<SessionView, int>(nameof(MaxDisplayLineCountForEachLog), 1);
		static readonly AvaloniaProperty<TimeSpan?> SelectedLogsDurationProperty = AvaloniaProperty.Register<SessionView, TimeSpan?>(nameof(SelectedLogsDuration));
		static readonly AvaloniaProperty<SessionViewStatusBarState> StatusBarStateProperty = AvaloniaProperty.Register<SessionView, SessionViewStatusBarState>(nameof(StatusBarState), SessionViewStatusBarState.None);


		// Fields.
		bool areAllTutorialsShown;
		IDisposable? areInitDialogsClosedObserverToken;
		Avalonia.Controls.Window? attachedWindow;
		readonly ScheduledAction autoAddLogFilesAction;
		readonly ScheduledAction autoCloseSidePanelAction;
		readonly ScheduledAction autoSetIPEndPointAction;
		readonly ScheduledAction autoSetUriAction;
		readonly ScheduledAction autoSetWorkingDirectoryAction;
		INotifyCollectionChanged? attachedLogs;
		readonly ObservableCommandState canAddLogFiles = new();
		readonly MutableObservableBoolean canCopyLogProperty = new MutableObservableBoolean();
		readonly MutableObservableBoolean canCopySelectedLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canCopySelectedLogsWithFileNames = new MutableObservableBoolean();
		readonly MutableObservableBoolean canEditLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canFilterLogsByPid = new MutableObservableBoolean();
		readonly MutableObservableBoolean canFilterLogsByTid = new MutableObservableBoolean();
		readonly MutableObservableBoolean canMarkSelectedLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canMarkUnmarkSelectedLogs = new MutableObservableBoolean();
		readonly ObservableCommandState canReloadLogs = new();
		readonly ObservableCommandState canResetLogProfileToSession = new();
		readonly MutableObservableBoolean canRestartAsAdmin = new MutableObservableBoolean(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !App.Current.IsRunningAsAdministrator);
		readonly ObservableCommandState canSaveLogs = new();
		readonly MutableObservableBoolean canSelectMarkedLogs = new MutableObservableBoolean();
		readonly ObservableCommandState canSetIPEndPoint = new();
		readonly ForwardedObservableBoolean canSetLogProfile;
		readonly ObservableCommandState canSetLogProfileToSession = new();
		readonly ObservableCommandState canSetUri = new();
		readonly ObservableCommandState canSetWorkingDirectory = new();
		readonly MutableObservableBoolean canShowFileInExplorer = new MutableObservableBoolean();
		readonly MutableObservableBoolean canShowLogProperty = new MutableObservableBoolean();
		readonly MutableObservableBoolean canShowWorkingDirectoryInExplorer = new MutableObservableBoolean();
		readonly MutableObservableBoolean canUnmarkSelectedLogs = new MutableObservableBoolean();
		readonly MenuItem copyLogPropertyMenuItem;
		readonly Border dragDropReceiverBorder;
		IDisposable? hasDialogsObserverToken;
		bool isAttachedToLogicalTree;
		bool isIPEndPointNeededAfterLogProfileSet;
		bool isLogFileNeededAfterLogProfileSet;
		bool isPidLogPropertyVisible;
		bool isPointerPressedOnLogListBox;
		bool isRestartingAsAdminConfirmed;
		bool isSelectingFileToSaveLogs;
		bool isTidLogPropertyVisible;
		bool isUriNeededAfterLogProfileSet;
		bool isWorkingDirNeededAfterLogProfileSet;
		bool keepSidePanelVisible;
		readonly Avalonia.Controls.ListBox keyLogAnalysisRuleSetListBox;
		Control? lastClickedLogPropertyView;
		readonly ContextMenu logActionMenu;
		readonly Avalonia.Controls.ListBox logAnalysisResultListBox;
		readonly ContextMenu logFileActionMenu;
		readonly AppSuite.Controls.ListBox logFileListBox;
		readonly List<ColumnDefinition> logHeaderColumns = new List<ColumnDefinition>();
		readonly Control logHeaderContainer;
		readonly Grid logHeaderGrid;
		readonly List<MutableObservableValue<GridLength>> logHeaderWidths = new List<MutableObservableValue<GridLength>>();
		readonly ComboBox logLevelFilterComboBox;
		readonly Avalonia.Controls.ListBox logListBox;
		readonly Panel logListBoxContainer;
		readonly ContextMenu logMarkingMenu;
		readonly IntegerTextBox logProcessIdFilterTextBox;
		readonly Panel logProcessIdFilterTextBoxPanel;
		readonly ToggleButton logsSavingButton;
		readonly ContextMenu logsSavingMenu;
		ScrollViewer? logScrollViewer;
		readonly RegexTextBox logTextFilterTextBox;
		readonly IntegerTextBox logThreadIdFilterTextBox;
		readonly Panel logThreadIdFilterTextBoxPanel;
		readonly Avalonia.Controls.ListBox markedLogListBox;
		readonly double minLogListBoxSizeToCloseSidePanel;
		readonly Avalonia.Controls.ListBox operationDurationAnalysisRuleSetListBox;
		readonly ToggleButton otherActionsButton;
		readonly ContextMenu otherActionsMenu;
		readonly Avalonia.Controls.ListBox predefinedLogTextFilterListBox;
		readonly SortedObservableList<PredefinedLogTextFilter> predefinedLogTextFilters;
		readonly Popup predefinedLogTextFiltersPopup;
		readonly HashSet<Avalonia.Input.Key> pressedKeys = new HashSet<Avalonia.Input.Key>();
		readonly ScheduledAction reportSelectedLogsTimeInfoAction;
		readonly ScheduledAction scrollToLatestLogAction;
		
		readonly HashSet<KeyLogAnalysisRuleSet> selectedKeyLogAnalysisRuleSets = new();
		readonly HashSet<OperationDurationAnalysisRuleSet> selectedOperationDurationAnalysisRuleSets = new();
		readonly HashSet<PredefinedLogTextFilter> selectedPredefinedLogTextFilters = new();
		readonly MenuItem showLogPropertyMenuItem;
		readonly ColumnDefinition sidePanelColumn;
		readonly Control sidePanelContainer;
		readonly AppSuite.Controls.ListBox timestampCategoryListBox;
		readonly ToolBarScrollViewer toolBarScrollViewer;
		readonly ScheduledAction updateLogAnalysisAction;
		readonly ScheduledAction updateLogFiltersAction;
		readonly ScheduledAction updateStatusBarStateAction;
		readonly SortedObservableList<Logs.LogLevel> validLogLevels = new SortedObservableList<Logs.LogLevel>((x, y) => (int)x - (int)y);
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
			// prepare command state observables
			this.canAddLogFiles.Subscribe(value =>
			{
				if (value 
					&& this.isLogFileNeededAfterLogProfileSet 
					&& this.isAttachedToLogicalTree)
				{
					this.isLogFileNeededAfterLogProfileSet = false;
					this.autoAddLogFilesAction?.Reschedule(AutoAddLogFilesDelay);
				}
			});
			this.canSetIPEndPoint.Subscribe(value =>
			{
				if (value 
					&& this.isIPEndPointNeededAfterLogProfileSet 
					&& this.isAttachedToLogicalTree)
				{
					this.isIPEndPointNeededAfterLogProfileSet = false;
					this.autoSetIPEndPointAction?.Reschedule(AutoAddLogFilesDelay);
				}
			});
			this.canSetLogProfile = new ForwardedObservableBoolean(ForwardedObservableBoolean.CombinationMode.Or,
				false,
				this.canResetLogProfileToSession,
				this.canSetLogProfileToSession
			);
			this.canSetUri.Subscribe(value =>
			{
				if (value 
					&& this.isUriNeededAfterLogProfileSet 
					&& this.isAttachedToLogicalTree)
				{
					this.isUriNeededAfterLogProfileSet = false;
					this.autoSetUriAction?.Reschedule(AutoAddLogFilesDelay);
				}
			});
			this.canSetWorkingDirectory.Subscribe(value =>
			{
				if (value 
					&& this.isWorkingDirNeededAfterLogProfileSet 
					&& this.isAttachedToLogicalTree)
				{
					this.isWorkingDirNeededAfterLogProfileSet = false;
					this.autoSetWorkingDirectoryAction?.Reschedule(AutoAddLogFilesDelay);
				}
			});

			// create commands
			this.AddLogFilesCommand = new Command(this.AddLogFiles, this.canAddLogFiles);
			this.CopyLogPropertyCommand = new Command(this.CopyLogProperty, this.canCopyLogProperty);
			this.CopySelectedLogsCommand = new Command(this.CopySelectedLogs, this.canCopySelectedLogs);
			this.CopySelectedLogsWithFileNamesCommand = new Command(this.CopySelectedLogsWithFileNames, this.canCopySelectedLogsWithFileNames);
			this.EditLogProfileCommand = new Command(this.EditLogProfile, this.canEditLogProfile);
			this.FilterLogsByProcessIdCommand = new Command<bool>(this.FilterLogsByProcessId, this.canFilterLogsByPid);
			this.FilterLogsByThreadIdCommand = new Command<bool>(this.FilterLogsByThreadId, this.canFilterLogsByTid);
			this.MarkSelectedLogsCommand = new Command<MarkColor>(this.MarkSelectedLogs, this.canMarkSelectedLogs);
			this.MarkUnmarkSelectedLogsCommand = new Command(this.MarkUnmarkSelectedLogs, this.canMarkUnmarkSelectedLogs);
			this.ReloadLogsCommand = new Command(this.ReloadLogs, this.canReloadLogs);
			this.ResetLogFiltersCommand = new Command(this.ResetLogFilters, this.GetObservable<bool>(HasLogProfileProperty));
			this.RestartAsAdministratorCommand = new Command(this.RestartAsAdministrator, this.canRestartAsAdmin);
			this.SaveAllLogsCommand = new Command(() => this.SaveLogs(true), this.canSaveLogs);
			this.SaveLogsCommand = new Command(() => this.SaveLogs(false), this.canSaveLogs);
			this.SelectAndSetIPEndPointCommand = new Command(this.SelectAndSetIPEndPoint, this.canSetIPEndPoint);
			this.SelectAndSetLogProfileCommand = new Command(this.SelectAndSetLogProfile, this.canSetLogProfile);
			this.SelectAndSetUriCommand = new Command(this.SelectAndSetUri, this.canSetUri);
			this.SelectAndSetWorkingDirectoryCommand = new Command(this.SelectAndSetWorkingDirectory, this.canSetWorkingDirectory);
			this.SelectMarkedLogsCommand = new Command(this.SelectMarkedLogs, this.canSelectMarkedLogs);
			this.ShowFileInExplorerCommand = new Command(this.ShowFileInExplorer, this.canShowFileInExplorer);
			this.ShowLogStringPropertyCommand = new Command(() => this.ShowLogStringProperty(), this.canShowLogProperty);
			this.ShowWorkingDirectoryInExplorerCommand = new Command(this.ShowWorkingDirectoryInExplorer, this.canShowWorkingDirectoryInExplorer);
			this.SwitchLogFiltersCombinationModeCommand = new Command(this.SwitchLogFiltersCombinationMode, this.GetObservable<bool>(HasLogProfileProperty));
			this.UnmarkSelectedLogsCommand = new Command(this.UnmarkSelectedLogs, this.canUnmarkSelectedLogs);

			// create collections
			this.predefinedLogTextFilters = new SortedObservableList<PredefinedLogTextFilter>(ComparePredefinedLogTextFilters);

			// setup log font
			this.UpdateLogFontFamily();
			this.UpdateLogFontSize();

			// setup properties
			this.SetValue<bool>(IsProcessInfoVisibleProperty, this.Settings.GetValueOrDefault(AppSuite.SettingKeys.ShowProcessInfo));
			this.SetValue<int>(MaxDisplayLineCountForEachLogProperty, Math.Max(1, this.Settings.GetValueOrDefault(SettingKeys.MaxDisplayLineCountForEachLog)));
			this.ValidLogLevels = this.validLogLevels.AsReadOnly();

			// initialize
			this.InitializeComponent();

			// load resources
			if (this.Application.TryGetResource<double>("Double/SessionView.LogListBox.MinSizeToCloseSidePanel", out var doubleRes))
				this.minLogListBoxSizeToCloseSidePanel = doubleRes.GetValueOrDefault();

			// setup controls
			this.copyLogPropertyMenuItem = this.FindControl<MenuItem>(nameof(copyLogPropertyMenuItem)).AsNonNull();
			this.dragDropReceiverBorder = this.FindControl<Border>(nameof(dragDropReceiverBorder)).AsNonNull();
			this.keyLogAnalysisRuleSetListBox = this.FindControl<Avalonia.Controls.ListBox>(nameof(keyLogAnalysisRuleSetListBox))!.Also(it =>
			{
				it.SelectionChanged += this.OnKeyLogAnalysisRuleSetListBoxSelectionChanged;
			});
			this.logActionMenu = ((ContextMenu)this.Resources[nameof(logActionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuOpened += (_, e) =>
				{
					this.IsScrollingToLatestLogNeeded = false;
					if (this.showLogPropertyMenuItem == null)
						return;
					if (this.lastClickedLogPropertyView?.Tag is DisplayableLogProperty property)
					{
						this.copyLogPropertyMenuItem.Header = this.Application.GetFormattedString("SessionView.CopyLogProperty", property.DisplayName);
						this.showLogPropertyMenuItem.Header = this.Application.GetFormattedString("SessionView.ShowLogProperty", property.DisplayName);
					}
					else
					{
						this.copyLogPropertyMenuItem.Header = this.Application.GetString("SessionView.CopyLogProperty.Disabled");
						this.showLogPropertyMenuItem.Header = this.Application.GetString("SessionView.ShowLogProperty.Disabled");
					}
				};
			});
			this.logAnalysisResultListBox = this.FindControl<Avalonia.Controls.ListBox>(nameof(logAnalysisResultListBox))!.Also(it =>
			{
				it.SelectionChanged += this.OnLogAnalysisResultListBoxSelectionChanged;
			});
			this.logFileActionMenu = ((ContextMenu)this.Resources[nameof(logFileActionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) =>
				{
					if (it.PlacementTarget is ToggleButton button)
						SynchronizationContext.Post(() => button.IsChecked = false);
					it.DataContext = null;
					it.PlacementTarget = null;
				};
				it.MenuOpened += (_, e) =>
				{
					if (it.PlacementTarget is ToggleButton button)
					{
						it.DataContext = button.DataContext;
						SynchronizationContext.Post(() => button.IsChecked = true);
					}
				};
			});
			this.logFileListBox = this.FindControl<AppSuite.Controls.ListBox>(nameof(logFileListBox)).AsNonNull();
			this.logHeaderContainer = this.FindControl<Control>(nameof(logHeaderContainer)).AsNonNull();
			this.logHeaderGrid = this.FindControl<Grid>(nameof(logHeaderGrid)).AsNonNull().Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.ReportLogHeaderColumnWidths());
			});
			this.logLevelFilterComboBox = this.FindControl<ComboBox>(nameof(logLevelFilterComboBox)).AsNonNull().Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logListBoxContainer = this.FindControl<Panel>(nameof(logListBoxContainer)).AsNonNull().Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.autoCloseSidePanelAction?.Schedule());
			});
			this.logListBox = this.logListBoxContainer.FindControl<Avalonia.Controls.ListBox>(nameof(logListBox)).AsNonNull().Also(it =>
			{
				it.AddHandler(Avalonia.Controls.ListBox.PointerPressedEvent, this.OnLogListBoxPointerPressed, RoutingStrategies.Tunnel);
				it.AddHandler(Avalonia.Controls.ListBox.PointerReleasedEvent, this.OnLogListBoxPointerReleased, RoutingStrategies.Tunnel);
				it.AddHandler(Avalonia.Controls.ListBox.PointerWheelChangedEvent, this.OnLogListBoxPointerWheelChanged, RoutingStrategies.Tunnel);
				it.GetObservable(Avalonia.Controls.ListBox.ScrollProperty).Subscribe(_ =>
				{
					this.logScrollViewer = (it.Scroll as ScrollViewer)?.Also(scrollViewer =>
					{
						scrollViewer.AllowAutoHide = false;
						scrollViewer.ScrollChanged += this.OnLogListBoxScrollChanged;
					});
				});
			});
			this.logMarkingMenu = ((ContextMenu)this.Resources[nameof(logMarkingMenu)].AsNonNull()).Also(it =>
			{
				it.MenuOpened += (_, e) =>
				{
					this.IsScrollingToLatestLogNeeded = false;
				};
			});
			this.logProcessIdFilterTextBoxPanel = this.FindControl<Panel>(nameof(logProcessIdFilterTextBoxPanel)).AsNonNull();
			this.logProcessIdFilterTextBox = this.logProcessIdFilterTextBoxPanel.FindControl<IntegerTextBox>(nameof(logProcessIdFilterTextBox)).AsNonNull().Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logsSavingButton = this.FindControl<ToggleButton>(nameof(logsSavingButton)).AsNonNull();
			this.logsSavingMenu = ((ContextMenu)this.Resources[nameof(logsSavingMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.logsSavingButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.logsSavingButton.IsChecked = true);
			});
			this.logTextFilterTextBox = this.FindControl<RegexTextBox>(nameof(logTextFilterTextBox)).AsNonNull().Also(it =>
			{
				it.IgnoreCase = this.Settings.GetValueOrDefault(SettingKeys.IgnoreCaseOfLogTextFilter);
				it.ValidationDelay = this.UpdateLogFilterParamsDelay;
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logThreadIdFilterTextBoxPanel = this.FindControl<Panel>(nameof(logThreadIdFilterTextBoxPanel)).AsNonNull();
			this.logThreadIdFilterTextBox = this.logThreadIdFilterTextBoxPanel.FindControl<IntegerTextBox>(nameof(logThreadIdFilterTextBox)).AsNonNull().Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.markedLogListBox = this.FindControl<Avalonia.Controls.ListBox>(nameof(markedLogListBox)).AsNonNull();
			this.operationDurationAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(operationDurationAnalysisRuleSetListBox)).Also(it =>
			{
				it.SelectionChanged += this.OnOperationDurationAnalysisRuleSetListBoxSelectionChanged;
			});
			this.otherActionsButton = this.FindControl<ToggleButton>(nameof(otherActionsButton)).AsNonNull();
			this.otherActionsMenu = ((ContextMenu)this.Resources[nameof(otherActionsMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.otherActionsButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.otherActionsButton.IsChecked = true);
			});
			this.predefinedLogTextFilterListBox = this.FindControl<Avalonia.Controls.ListBox>(nameof(predefinedLogTextFilterListBox)).AsNonNull();
			this.predefinedLogTextFiltersPopup = this.FindControl<Popup>(nameof(predefinedLogTextFiltersPopup)).AsNonNull().Also(it =>
			{
				it.Closed += (_, sender) => this.logListBox.Focus();
				it.Opened += (_, sender) => this.predefinedLogTextFilterListBox.Focus();
			});
			this.showLogPropertyMenuItem = this.FindControl<MenuItem>(nameof(showLogPropertyMenuItem)).AsNonNull();
			this.sidePanelColumn = this.FindControl<Grid>("RootGrid").AsNonNull().Let(grid =>
			{
				return grid.ColumnDefinitions[2].Also(it =>
				{
					it.GetObservable(ColumnDefinition.WidthProperty).Subscribe(length =>
					{
						if (this.DataContext is Session session)
						{
							if (session.IsLogAnalysisPanelVisible 
								|| session.IsLogFilesPanelVisible 
								|| session.IsMarkedLogsPanelVisible
								|| session.IsTimestampCategoriesPanelVisible)
							{
								session.LogAnalysisPanelSize = length.Value;
								session.LogFilesPanelSize = length.Value;
								session.MarkedLogsPanelSize = length.Value;
								session.TimestampCategoriesPanelSize = length.Value;
							}
						}
					});
				});
			});
			this.sidePanelContainer = this.FindControl<Control>(nameof(sidePanelContainer)).AsNonNull();
#if !DEBUG
			this.FindControl<Button>("testButton").AsNonNull().IsVisible = false;
#endif
			this.timestampCategoryListBox = this.FindControl<AppSuite.Controls.ListBox>(nameof(timestampCategoryListBox)).AsNonNull().Also(it =>
			{
				it.GetObservable(Avalonia.Controls.ListBox.SelectedItemProperty).Subscribe(item =>
					this.OnLogCategoryListBoxSelectedItemChanged(it, item as DisplayableLogCategory));
			});
			this.FindControl<Control>("toolBarContainer").AsNonNull().Let(it =>
			{
				it.AddHandler(Control.PointerReleasedEvent, this.OnToolBarPointerReleased, RoutingStrategies.Tunnel);
			});
			this.toolBarScrollViewer = this.FindControl<ToolBarScrollViewer>(nameof(toolBarScrollViewer)).AsNonNull();
			this.workingDirectoryActionsButton = this.FindControl<ToggleButton>(nameof(workingDirectoryActionsButton)).AsNonNull();
			this.workingDirectoryActionsMenu = ((ContextMenu)this.Resources[nameof(workingDirectoryActionsMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.workingDirectoryActionsButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.workingDirectoryActionsButton.IsChecked = true);
			});

			// create scheduled actions
			this.autoAddLogFilesAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders)
					return;
				this.AddLogFiles();
			});
			this.autoCloseSidePanelAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Session session)
					return;
				if (this.keepSidePanelVisible)
					this.keepSidePanelVisible = false;
				else if (this.logListBoxContainer.Bounds.Width <= this.minLogListBoxSizeToCloseSidePanel)
				{
					session.IsLogAnalysisPanelVisible = false;
					session.IsLogFilesPanelVisible = false;
					session.IsMarkedLogsPanelVisible = false;
					session.IsTimestampCategoriesPanelVisible = false;
				}
			});
			this.autoSetIPEndPointAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders)
					return;
				this.SelectAndSetIPEndPoint();
			});
			this.autoSetUriAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders)
					return;
				this.SelectAndSetUri();
			});
			this.autoSetWorkingDirectoryAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders)
					return;
				this.SelectAndSetWorkingDirectory();
			});
			this.reportSelectedLogsTimeInfoAction = new ScheduledAction(() =>
			{
				var session = (this.DataContext as Session);
				var firstLog = (DisplayableLog?)null;
				var lastLog = (DisplayableLog?)null;
				session?.FindFirstAndLastLog(this.logListBox.SelectedItems.Cast<DisplayableLog>(), out firstLog, out lastLog);
				if (firstLog == null || lastLog == null)
				{
					this.SetValue<TimeSpan?>(SelectedLogsDurationProperty, null);
					this.SetValue<DateTime?>(EarliestSelectedLogTimestampProperty, null);
					this.SetValue<DateTime?>(LatestSelectedLogTimestampProperty, null);
					return;
				}
				var earliestTimestamp = (DateTime?)null;
				var latestTimestamp = (DateTime?)null;
				var minTimeSpan = (TimeSpan?)null;
				var maxTimeSpan = (TimeSpan?)null;
				var duration = session?.CalculateDurationBetweenLogs(firstLog, lastLog, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);
				if (duration != null)
				{
					this.SetValue<TimeSpan?>(SelectedLogsDurationProperty, duration);
					this.SetValue<DateTime?>(EarliestSelectedLogTimestampProperty, earliestTimestamp);
					this.SetValue<DateTime?>(LatestSelectedLogTimestampProperty, latestTimestamp);
				}
				else
				{
					this.SetValue<TimeSpan?>(SelectedLogsDurationProperty, null);
					this.SetValue<DateTime?>(EarliestSelectedLogTimestampProperty, null);
					this.SetValue<DateTime?>(LatestSelectedLogTimestampProperty, null);
				}
			});
			this.scrollToLatestLogAction = new ScheduledAction(() =>
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
					return;
				}

				// find log index
				var logIndex = session.LogProfile.SortDirection == SortDirection.Ascending ? session.Logs.Count - 1 : 0;

				// scroll to latest log
				try
				{
					this.logListBox.ScrollIntoView(logIndex);
					this.scrollToLatestLogAction?.Schedule(ScrollingToLatestLogInterval);
				}
				catch
				{ }
			});
			this.updateLogAnalysisAction = new(() =>
			{
				if (this.DataContext is not Session session)
					return;
				var selectedKlaRuleSets = this.selectedKeyLogAnalysisRuleSets.ToArray();
				var selectedOdaRuleSets = this.selectedOperationDurationAnalysisRuleSets.ToArray();
				session.KeyLogAnalysisRuleSets.Clear();
				session.KeyLogAnalysisRuleSets.AddAll(selectedKlaRuleSets);
				session.OperationDurationAnalysisRuleSets.Clear();
				session.OperationDurationAnalysisRuleSets.AddAll(selectedOdaRuleSets);
			});
			this.updateLogFiltersAction = new ScheduledAction(() =>
			{
				// get session
				if (this.DataContext is not Session session)
					return;

				// set level 
				session.LogLevelFilter = this.logLevelFilterComboBox.SelectedItem is Logs.LogLevel logLevel ? logLevel : Logs.LogLevel.Undefined;

				// set PID
				session.LogProcessIdFilter = (int?)this.logProcessIdFilterTextBox.Value;

				// set TID
				session.LogThreadIdFilter = (int?)this.logThreadIdFilterTextBox.Value;

				// update text filters
				session.LogTextFilter = this.logTextFilterTextBox.Object;
				session.PredefinedLogTextFilters.Clear();
				session.PredefinedLogTextFilters.AddAll(this.selectedPredefinedLogTextFilters);
			});
			this.updateStatusBarStateAction = new ScheduledAction(() =>
			{
				this.SetValue<SessionViewStatusBarState>(StatusBarStateProperty, Global.Run(() =>
				{
					if (this.DataContext is not Session session)
						return SessionViewStatusBarState.None;
					if (session.IsLogsReadingPaused)
						return SessionViewStatusBarState.Paused;
					if (session.HasPartialDataSourceErrors)
						return SessionViewStatusBarState.Warning;
					if (session.HasAllDataSourceErrors)
						return SessionViewStatusBarState.Error;
					if (session.HasLogReaders)
						return SessionViewStatusBarState.Active;
					return SessionViewStatusBarState.None;
				}));
			});
		}


		// Add log files.
		async void AddLogFiles()
		{
			// check state
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to add log files without attaching to window");
				return;
			}

			// cancel scheduled action
			this.autoAddLogFilesAction.Cancel();

			// select files
			var fileNames = await new OpenFileDialog()
			{
				AllowMultiple = true,
				Title = this.Application.GetString("SessionView.AddLogFiles"),
			}.ShowAsync(this.attachedWindow);
			if (fileNames == null || fileNames.Length == 0)
				return;

			// check state
			if (this.DataContext is not Session session)
				return;
			if (!this.canAddLogFiles.Value)
				return;
			
			// exclude added files
			var fileNameList = new List<string>(fileNames);
			fileNameList.RemoveAll(session.IsLogFileAdded);
			if (fileNameList.IsEmpty())
				return;
			
			// select precondition
			var precondition = this.Settings.GetValueOrDefault(SettingKeys.SelectLogReadingPreconditionForFiles) 
				? (await this.SelectLogReadingPreconditionAsync(LogDataSourceType.File, session.LastLogReadingPrecondition, false)).GetValueOrDefault()
				: new Logs.LogReadingPrecondition();

			// sort file names
			fileNameList.Sort();

			// add log files
			foreach (var fileName in fileNameList)
			{
				session.AddLogFileCommand.Execute(new Session.LogDataSourceParams<string>()
				{
					Precondition = precondition,
					Source = fileName,
				});
			}
		}


		// Command to add log files.
		ICommand AddLogFilesCommand { get; }


		/// <summary>
		/// Ceck whether all tutorials are shown or not.
		/// </summary>
		public bool AreAllTutorialsShown { get => this.areAllTutorialsShown; }


		// Attach to predefined log text filter
		void AttachToPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged += this.OnPredefinedLogTextFilterPropertyChanged;


		// Attach to session.
		void AttachToSession(Session session)
		{
			// add event handler
			session.ErrorMessageGenerated += this.OnErrorMessageGeneratedBySession;
			session.ExternalDependencyNotFound += this.OnExternalDependencyNotFound;
			session.PropertyChanged += this.OnSessionPropertyChanged;
			this.attachedLogs = session.Logs as INotifyCollectionChanged;
			if (this.attachedLogs != null)
				this.attachedLogs.CollectionChanged += this.OnLogsChanged;
			(session.KeyLogAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnSessionKeyLogAnalysisRuleSetsChanged);

			// check profile
			var profile = session.LogProfile;
			if (profile != null)
			{
				this.canEditLogProfile.Update(!profile.IsBuiltIn);
				this.SetValue<bool>(HasLogProfileProperty, true);
				if (session.IsIPEndPointNeeded)
					this.isIPEndPointNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectIPEndPointWhenNeeded);
				else if (session.IsLogFileNeeded)
					this.isLogFileNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectLogFilesWhenNeeded);
				else if (session.IsUriNeeded)
					this.isUriNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectUriWhenNeeded);
				else if (session.IsWorkingDirectoryNeeded)
					this.isWorkingDirNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectWorkingDirectoryWhenNeeded);
				this.OnLogProfileSet(profile);
			}

			// attach to command
			this.canAddLogFiles.Bind(session.AddLogFileCommand);
			this.canReloadLogs.Bind(session.ReloadLogsCommand);
			this.canResetLogProfileToSession.Bind(session.ResetLogProfileCommand);
			this.canSaveLogs.Bind(session.SaveLogsCommand);
			this.canSelectMarkedLogs.Update(session.HasMarkedLogs);
			this.canSetIPEndPoint.Bind(session.SetIPEndPointCommand);
			this.canSetLogProfileToSession.Bind(session.SetLogProfileCommand);
			this.canSetUri.Bind(session.SetUriCommand);
			this.canSetWorkingDirectory.Bind(session.SetWorkingDirectoryCommand);
			this.canShowWorkingDirectoryInExplorer.Update(Platform.IsOpeningFileManagerSupported && session.HasWorkingDirectory);

			// start auto scrolling
			session.LogProfile?.Let(profile =>
			{
				if (!profile.IsContinuousReading)
					this.IsScrollingToLatestLogNeeded = false;
			});
			if (session.HasLogs && this.IsScrollingToLatestLogNeeded)
				this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);

			// sync log filters to UI
			this.logProcessIdFilterTextBox.Value = session.LogProcessIdFilter;
			this.logTextFilterTextBox.Object = session.LogTextFilter;
			this.logThreadIdFilterTextBox.Value = session.LogThreadIdFilter;
			this.logLevelFilterComboBox.SelectedItem = session.LogLevelFilter;
			if (session.PredefinedLogTextFilters.IsNotEmpty())
			{
				this.SynchronizationContext.Post(() =>
				{
					foreach (var textFilter in session.PredefinedLogTextFilters)
					{
						this.predefinedLogTextFilterListBox.SelectedItems.Add(textFilter);
						this.selectedPredefinedLogTextFilters.Add(textFilter);
					}
					this.updateLogFiltersAction.Cancel();
				});
			}
			this.updateLogFiltersAction.Cancel();

			// sync side panel state
			if (session.IsLogAnalysisPanelVisible 
				|| session.IsMarkedLogsPanelVisible 
				|| session.IsLogFilesPanelVisible
				|| session.IsTimestampCategoriesPanelVisible)
			{
				sidePanelColumn.Width = new GridLength(new double[] {
					session.LogAnalysisPanelSize,
					session.LogFilesPanelSize,
					session.MarkedLogsPanelSize,
					session.TimestampCategoriesPanelSize,
				}.Max());
				Grid.SetColumnSpan(this.logListBoxContainer, 1);
			}
			else
			{
				sidePanelColumn.Width = new GridLength(0);
				Grid.SetColumnSpan(this.logListBoxContainer, 3);
			}

			// update properties
			this.validLogLevels.AddAll(session.ValidLogLevels);

			// update UI
			this.OnDisplayLogPropertiesChanged();
			this.updateStatusBarStateAction.Schedule();
		}


		// Check whether at least one non-text log filter is supported or not.
		bool CanFilterLogsByNonTextFilters { get => this.GetValue<bool>(CanFilterLogsByNonTextFiltersProperty); }


		// Clear key log analysis rule set selection.
		void ClearKeyLogAnalysisRuleSetSelection()
		{
			this.keyLogAnalysisRuleSetListBox.SelectedItems.Clear();
			this.updateLogAnalysisAction.Reschedule();
		}


		// Clear operation duration analysis rule set selection.
		void ClearOperationDurationAnalysisRuleSetSelection()
		{
			this.operationDurationAnalysisRuleSetListBox.SelectedItems.Clear();
			this.updateLogAnalysisAction.Reschedule();
		}


		// Clear predefined log text fliter selection.
		void ClearPredefinedLogTextFilterSelection()
		{
			this.predefinedLogTextFilterListBox.SelectedItems.Clear();
			this.updateLogFiltersAction.Reschedule();
		}


		// Compare predefined log text filters.
		static int ComparePredefinedLogTextFilters(PredefinedLogTextFilter? x, PredefinedLogTextFilter? y)
		{
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;
			var result = string.Compare(x.Name, y.Name);
			if (result != 0)
				return result;
			return x.GetHashCode() - y.GetHashCode();
		}


		// Show dialog and let user choose whether to restart as administor for given log profile.
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
							this.Logger.LogWarning($"Unable to use profile '{profile.Name}' because application is not running as administrator");
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
				Message = this.Application.GetFormattedString("SessionView.NeedToRestartAsAdministrator", profile.Name),
			}.ShowDialog(this.attachedWindow);
			if (result == MessageDialogResult.Yes)
			{
				this.Logger.LogWarning($"User agreed to restart as administrator for '{profile.Name}'");
				return true;
			}
			this.Logger.LogWarning($"User denied to restart as administrator for '{profile.Name}'");
			return false;
		}


		// Copy file name of log file.
		void CopyLogFileName(string filePath)
		{
			try
			{
				_ = App.Current.Clipboard?.SetTextAsync(Path.GetFileName(filePath));
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to copy file name clipboard");
			}
		}


		// Copy file path of log file.
		void CopyLogFilePath(string filePath)
		{
			try
			{
				_ = App.Current.Clipboard?.SetTextAsync(filePath);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to copy file name clipboard");
			}
		}


		// Copy property of log.
		void CopyLogProperty()
		{
			// check state
			if (!this.canCopyLogProperty.Value)
				return;
			if (this.logListBox.SelectedItems.Count != 1)
				return;

			// find property and log
			var clickedPropertyView = this.lastClickedLogPropertyView;
			if (clickedPropertyView == null || clickedPropertyView.Tag is not DisplayableLogProperty property)
				return;
			var listBoxItem = clickedPropertyView.FindLogicalAncestorOfType<ListBoxItem>();
			if (listBoxItem == null)
				return;
			var log = (listBoxItem.DataContext as DisplayableLog);
			if (log == null || this.logListBox.SelectedItems[0] != log)
				return;

			// copy
			this.CopyLogProperty(log, property);
		}
		void CopyLogProperty(DisplayableLog log, DisplayableLogProperty property)
		{
			// check state
			if (this.Application is not App app)
				return;

			// get property value
			if (!log.TryGetProperty<object?>(property.Name, out var value) || value == null)
				return;

			// copy value
			app.Clipboard?.SetTextAsync(value.ToString() ?? "");
		}


		// Command to copy property of log.
		ICommand CopyLogPropertyCommand { get; }


		// Copy selected logs.
		void CopySelectedLogs()
		{
			if (this.DataContext is not Session session || !this.canCopySelectedLogs.Value)
				return;
			session.CopyLogsCommand.TryExecute(this.logListBox.SelectedItems.Cast<DisplayableLog>().ToArray());
		}


		// Command to copy selected logs.
		ICommand CopySelectedLogsCommand { get; }


		// Copy selected logs with file names.
		void CopySelectedLogsWithFileNames()
		{
			if (this.DataContext is not Session session || !this.canCopySelectedLogsWithFileNames.Value)
				return;
			session.CopyLogsWithFileNamesCommand.TryExecute(this.logListBox.SelectedItems.Cast<DisplayableLog>().ToArray());
		}


		// Command to copy selected logs with file names.
		ICommand CopySelectedLogsWithFileNamesCommand { get; }


		// Create new key log analysis rule set.
		void CreateKeyLogAnalysisRuleSet()
		{
			if (this.attachedWindow != null)
				KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
		}


		// Create item template for item of log list box.
		DataTemplate CreateLogItemTemplate(LogProfile profile, IList<DisplayableLogProperty> logProperties)
		{
			var app = (App)this.Application;
			var isProVersion = this.GetValue<bool>(IsProVersionActivatedProperty);
			var logPropertyCount = logProperties.Count;
			var colorIndicatorBorderBrush = app.TryFindResource("Brush/WorkingArea.Background", out var rawResource) ? (IBrush?)rawResource : default;
			var colorIndicatorBorderThickness = app.TryFindResource("Thickness/SessionView.LogListBox.ColorIndicator.Border", out rawResource) ? (Thickness)rawResource! : default;
			var colorIndicatorWidth = app.TryFindResource("Double/SessionView.LogListBox.ColorIndicator.Width", out rawResource) ? (double)rawResource! : default;
			var analysisResultIndicatorSize = isProVersion && app.TryFindResource("Double/SessionView.LogListBox.LogAnalysisResultIndicator.Size", out rawResource) ? (double)rawResource! : default;
			var analysisResultIndicatorMargin = isProVersion && app.TryFindResource("Thickness/SessionView.LogListBox.LogAnalysisResultIndicator.Margin", out rawResource) ? (Thickness)rawResource! : default;
			var analysisResultIndicatorWidth = (analysisResultIndicatorSize + analysisResultIndicatorMargin.Left + analysisResultIndicatorMargin.Right);
			var markIndicatorSize = app.TryFindResource("Double/SessionView.LogListBox.MarkIndicator.Size", out rawResource) ? (double)rawResource! : default;
			var markIndicatorBorderThickness = app.TryFindResource("Thickness/SessionView.LogListBox.MarkIndicator.Border", out rawResource) ? (Thickness)rawResource! : default;
			var markIndicatorCornerRadius = app.TryFindResource("CornerRadius/SessionView.LogListBox.MarkIndicator.Border", out rawResource) ? (CornerRadius)rawResource! : default;
			var markIndicatorMargin = app.TryFindResource("Thickness/SessionView.LogListBox.MarkIndicator.Margin", out rawResource) ? (Thickness)rawResource! : new Thickness(1);
			var markIndicatorWidth = (markIndicatorSize + markIndicatorMargin.Left + markIndicatorMargin.Right);
			var itemBorderThickness = app.TryFindResource("Thickness/SessionView.LogListBox.Item.Column.Border", out rawResource) ? (Thickness)rawResource! : new Thickness(1);
			var itemCornerRadius = app.TryFindResource("CornerRadius/SessionView.LogListBox.Item.Column.Border", out rawResource) ? (CornerRadius)rawResource! : default;
			var itemPadding = app.TryFindResource("Thickness/SessionView.LogListBox.Item.Padding", out rawResource) ? (Thickness)rawResource! : default;
			var propertyPadding = app.TryFindResource("Thickness/SessionView.LogListBox.Item.Property.Padding", out rawResource) ? (Thickness)rawResource! : default;
			var splitterWidth = app.TryFindResource("Double/GridSplitter.Thickness", out rawResource) ? (double)rawResource! : default;
			var itemStartingContentWidth = (analysisResultIndicatorWidth + markIndicatorWidth);
			if (profile.ColorIndicator != LogColorIndicator.None)
				itemStartingContentWidth += colorIndicatorWidth + colorIndicatorBorderThickness.Left + colorIndicatorBorderThickness.Right;
			itemPadding = new Thickness(itemPadding.Left + itemStartingContentWidth, itemPadding.Top, itemPadding.Right, itemPadding.Bottom);
			var itemTemplateContent = new Func<IServiceProvider, object>(_ =>
			{
				var itemPanel = new Panel().Also(it =>
				{
					it.Children.Add(new Border().Also(border =>
					{
						border.Bind(Border.BackgroundProperty, new Binding()
						{
							Converter = Converters.MarkColorToBackgroundConverter.Default,
							Path = nameof(DisplayableLog.MarkedColor)
						});
					}));
				});
				var itemGrid = new Grid().Also(it =>
				{
					it.Margin = itemPadding;
					itemPanel.Children.Add(it);
				});

				// property views
				new Avalonia.Controls.TextBlock().Let(it =>
				{
					// empty view to reserve height of item
					it.Bind(TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
					it.Opacity = 0;
					it.Padding = propertyPadding;
					it.Text = " ";
					itemGrid.Children.Add(it);
				});
				for (var i = 0; i < logPropertyCount; ++i)
				{
					// define splitter column
					var logPropertyIndex = i;
					if (logPropertyIndex > 0)
						itemGrid.ColumnDefinitions.Add(new ColumnDefinition(splitterWidth, GridUnitType.Pixel));

					// define property column
					var logProperty = logProperties[logPropertyIndex];
					var width = logProperty.Width;
					var propertyColumn = new ColumnDefinition(new GridLength()).Also(it =>
					{
						if (width == null && logPropertyIndex == logPropertyCount - 1)
							it.Width = new GridLength(1, GridUnitType.Star);
						else
							it.Bind(ColumnDefinition.WidthProperty, this.logHeaderWidths[logPropertyIndex]);
					});
					itemGrid.ColumnDefinitions.Add(propertyColumn);

					// create property view
					var isStringProperty = DisplayableLog.HasStringProperty(logProperty.Name);
					var isMultiLineProperty = DisplayableLog.HasMultiLineStringProperty(logProperty.Name);
					var propertyView = logProperty.Name switch
					{
						_ => (Control)new TextBlock().Also(it =>
						{
							it.Bind(TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
							it.Bind(TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
							//it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
							if (isMultiLineProperty)
								it.Bind(TextBlock.MaxLinesProperty, new Binding() { Path = nameof(MaxDisplayLineCountForEachLog), Source = this });
							else
								it.MaxLines = 1;
							it.Padding = propertyPadding;
							it.Bind(TextBlock.TextProperty, new Binding().Also(binding =>
							{
								if (logProperty.Name == nameof(DisplayableLog.Level))
									binding.Converter = Converters.EnumConverters.LogLevel;
								binding.Path = logProperty.Name;
							}));
							it.TextTrimming = TextTrimming.CharacterEllipsis;
							it.TextWrapping = TextWrapping.NoWrap;
							it.VerticalAlignment = VerticalAlignment.Top;
						}),
					};
					var actualPropertyView = propertyView;
					if (isMultiLineProperty)
					{
						propertyView = new StackPanel().Also(it =>
						{
							it.Children.Add(propertyView);
							it.Children.Add(new LinkTextBlock().Also(viewDetails =>
							{
								viewDetails.Command = new Command(() =>
								{
									if (viewDetails.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext is DisplayableLog log)
										this.ShowLogStringProperty(log, logProperty);
								});
								viewDetails.HorizontalAlignment = HorizontalAlignment.Left;
								viewDetails.Bind(TextBlock.IsVisibleProperty, new Binding() { Path = $"HasExtraLinesOf{logProperty.Name}" });
								viewDetails.Bind(TextBlock.TextProperty, viewDetails.GetResourceObservable("String/SessionView.ViewFullLogMessage"));
							}));
							it.Orientation = Orientation.Vertical;
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
						it.GetObservable(Control.IsPointerOverProperty).Subscribe(new Observer<bool>(isPointerOver =>
						{
							if (isPointerOver)
							{
								it.BorderBrush = this.TryFindResource("Brush/SessionView.LogListBox.Item.Column.Border.PointerOver", out var res).Let(_ => res as IBrush);
								it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrushForPointerOver) });
							}
							else
							{
								it.BorderBrush = null;
								it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
							}
						}));
						it.PointerPressed += (_, e) =>
						{
							this.lastClickedLogPropertyView = it;
							if (this.logListBox.SelectedItems.Count == 1)
							{
								this.canCopyLogProperty.Update(true);
								if (isStringProperty)
									this.canShowLogProperty.Update(true);
							}
						};
						if (actualPropertyView is TextBlock textBlock && width != null)
						{
							var tipBinding = (IDisposable?)null;
							void updateToolTip(bool isTextTrimmed)
							{
								if (isTextTrimmed && (Platform.IsNotMacOS || this.attachedWindow?.IsActive == true))
									tipBinding = it.Bind(ToolTip.TipProperty, new Binding() { Source = textBlock, Path = nameof(TextBlock.Text) });
								else
									tipBinding?.Dispose();
							}
							textBlock.GetObservable(TextBlock.IsTextTrimmedProperty).Subscribe(updateToolTip);
							if (Platform.IsMacOS && this.attachedWindow != null)
							{
								this.attachedWindow.GetObservable(Avalonia.Controls.Window.IsActiveProperty).Subscribe(_ => 
									updateToolTip(textBlock.IsTextTrimmed));
							}
						}
					});
					Grid.SetColumn(propertyView, logPropertyIndex * 2);
					itemGrid.Children.Add(propertyView);
				}

				// color indicator
				if (profile.ColorIndicator != LogColorIndicator.None)
				{
					new Border().Also(it =>
					{
						it.Bind(Border.BackgroundProperty, new Binding() { Path = nameof(DisplayableLog.ColorIndicatorBrush) });
						it.BorderBrush = colorIndicatorBorderBrush;
						it.BorderThickness = colorIndicatorBorderThickness;
						it.HorizontalAlignment = HorizontalAlignment.Left;
						it.Bind(ToolTip.TipProperty, new Binding() { Path = nameof(DisplayableLog.ColorIndicatorTip) });
						it.Width = colorIndicatorWidth;
						itemPanel.Children.Add(it);
					});
				}

				// indicators
				itemPanel.Children.Add(new Panel().Also(indicatorsPanel =>
				{
					// setup panel
					indicatorsPanel.HorizontalAlignment = HorizontalAlignment.Left;
					indicatorsPanel.Margin = new Thickness((profile.ColorIndicator != LogColorIndicator.None ? colorIndicatorWidth : 0), 0, 0, 0);
					indicatorsPanel.VerticalAlignment = VerticalAlignment.Stretch;
					indicatorsPanel.Width = (analysisResultIndicatorWidth + markIndicatorWidth);

					// mark indicator
					indicatorsPanel.Children.Add(new Panel().Also(panel =>
					{
						var isLeftPointerDown = false;
						var isRightPointerDown = false;
						var isMenuOpen = false;
						panel.Children.Add(new Border().Also(selectionBackgroundBorder =>
						{
							selectionBackgroundBorder.Bind(Border.BackgroundProperty, this.GetResourceObservable("Brush/SessionView.LogListBox.Item.SelectionIndicator.Background"));
							selectionBackgroundBorder.Bind(Border.IsVisibleProperty, new Binding()
							{
								RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(ListBoxItem) },
								Path = nameof(ListBoxItem.IsSelected)
							});
						}));
						var emptyMarker = new Border().Also(border =>
						{
							border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Brush/Icon"));
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
							border.Bind(Border.BackgroundProperty, new Binding() { Path = nameof(DisplayableLog.MarkedColor), Converter = Converters.MarkColorToIndicatorConverter.Default });
							border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Brush/Icon"));
							border.BorderThickness = markIndicatorBorderThickness;
							border.CornerRadius = markIndicatorCornerRadius;
							border.Height = markIndicatorSize;
							border.Bind(Image.IsVisibleProperty, new Binding() { Path = nameof(DisplayableLog.IsMarked) });
							border.Margin = markIndicatorMargin;
							border.VerticalAlignment = VerticalAlignment.Center;
							border.Width = markIndicatorSize;
						}));
						panel.Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand);
						panel.HorizontalAlignment = HorizontalAlignment.Left;
						panel.PointerEnter += (_, e) => emptyMarker.IsVisible = true;
						panel.PointerLeave += (_, e) => emptyMarker.IsVisible = isMenuOpen;
						panel.PointerPressed += (_, e) =>
						{
							var properties = e.GetCurrentPoint(panel).Properties;
							isLeftPointerDown = properties.IsLeftButtonPressed;
							isRightPointerDown = properties.IsRightButtonPressed;
						};
						panel.AddHandler(Panel.PointerReleasedEvent, (_, e) =>
						{
							if (isLeftPointerDown)
							{
								if (this.DataContext is Session session && itemPanel.DataContext is DisplayableLog log)
								{
									if (log.IsMarked)
										session.UnmarkLogsCommand.TryExecute(new DisplayableLog[] { log });
									else
									{
										if (this.sidePanelContainer.IsVisible)
											session.IsMarkedLogsPanelVisible = true;
										session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams()
										{
											Color = MarkColor.Default,
											Logs = new DisplayableLog[] { log },
										});
									}
								}
							}
							else if (isRightPointerDown)
							{
								e.Handled = true;
								var closedHandler = (EventHandler<RoutedEventArgs>?)null;
								closedHandler = (sender, e2) =>
								{
									this.logMarkingMenu.MenuClosed -= closedHandler;
									isMenuOpen = false;
									emptyMarker.IsVisible = panel.IsPointerOver;
								};
								isMenuOpen = true;
								emptyMarker.IsVisible = true;
								this.logMarkingMenu.MenuClosed += closedHandler;
								this.logMarkingMenu.PlacementTarget = panel;
								this.logMarkingMenu.Open(panel);
							}
							isLeftPointerDown = false;
							isRightPointerDown = false;
						}, RoutingStrategies.Tunnel);
						panel.Bind(ToolTip.TipProperty, this.GetResourceObservable("String/SessionView.MarkUnmarkLog"));
						panel.VerticalAlignment = VerticalAlignment.Stretch;
						panel.Width = markIndicatorWidth;
					}));

					// analysis result indicator
					if (isProVersion)
					{
						indicatorsPanel.Children.Add(new Panel().Also(panel =>
						{
							panel.Height = analysisResultIndicatorSize;
							panel.HorizontalAlignment = HorizontalAlignment.Right;
							panel.Margin = analysisResultIndicatorMargin;
							panel.VerticalAlignment = VerticalAlignment.Center;
							panel.Width = analysisResultIndicatorSize;
							panel.Children.Add(new Border().Also(background =>
							{
								var isLeftPointerDown = false;
								background.Background = Brushes.Transparent;
								background.Cursor = new Avalonia.Input.Cursor(StandardCursorType.Hand);
								background.Bind(Border.IsVisibleProperty, new Binding() { Path = "HasAnalysisResult" });
								background.PointerPressed += (_, e) =>
									isLeftPointerDown = e.GetCurrentPoint(background).Properties.IsLeftButtonPressed;
								background.AddHandler(Border.PointerReleasedEvent, (_, e) =>
								{
									if (isLeftPointerDown)
									{
										isLeftPointerDown = false;
										if (itemPanel.DataContext is DisplayableLog log)
											this.OnLogAnalysisResultIndicatorClicked(log);
									}
								}, RoutingStrategies.Tunnel);
							}));
							panel.Children.Add(new Image().Also(image =>
							{
								image.Classes.Add("Icon");
								image.Bind(Image.IsVisibleProperty, new Binding() { Path = nameof(DisplayableLog.HasAnalysisResult) });
								image.Bind(Image.SourceProperty, new Binding() { Path = nameof(DisplayableLog.AnalysisResultIndicatorIcon) });
							}));
						}));
						}
				}));

				// complete
				return new ControlTemplateResult(itemPanel, null);
			});
			return new DataTemplate()
			{
				Content = itemTemplateContent,
				DataType = typeof(DisplayableLog),
			};
		}


		// Create item template for item of marked log list box.
		DataTemplate CreateMarkedLogItemTemplate(LogProfile profile, IList<DisplayableLogProperty> logProperties)
		{
			// check visible properties
			var hasMessage = false;
			var hasSourceName = false;
			var hasSummary = false;
			var hasTimestamp = false;
			var hasTitle = false;
			foreach (var logProperty in logProperties)
			{
				switch (logProperty.Name)
				{
					case nameof(DisplayableLog.Message):
						hasMessage = true;
						break;
					case nameof(DisplayableLog.SourceName):
						hasSourceName = true;
						break;
					case nameof(DisplayableLog.Summary):
						hasSummary = true;
						break;
					case nameof(DisplayableLog.TimestampString):
						hasTimestamp = true;
						break;
					case nameof(DisplayableLog.Title):
						hasTitle = true;
						break;
				}
			}

			// build item template for marked log list box
			var propertyInMarkedItem = Global.Run(() =>
			{
				if (hasMessage)
					return nameof(DisplayableLog.Message);
				if (hasSummary)
					return nameof(DisplayableLog.Summary);
				if (hasTitle)
					return nameof(DisplayableLog.Title);
				if (hasSourceName)
					return nameof(DisplayableLog.SourceName);
				if (hasTimestamp)
					return nameof(DisplayableLog.TimestampString);
				return nameof(DisplayableLog.LogId);
			});
			var app = (App)this.Application;
			var colorIndicatorBorderBrush = app.TryFindResource("Brush/WorkingArea.Panel.Background", out var rawResource) ? (IBrush)rawResource.AsNonNull() : null;
			var colorIndicatorBorderThickness = app.TryFindResource("Thickness/SessionView.LogListBox.ColorIndicator.Border", out rawResource) ? (Thickness)rawResource.AsNonNull() : new Thickness();
			var colorIndicatorWidth = app.TryFindResource("Double/SessionView.LogListBox.ColorIndicator.Width", out rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var itemPadding = app.TryFindResource("Thickness/SessionView.MarkedLogListBox.Item.Padding", out rawResource) ? (Thickness)rawResource.AsNonNull() : new Thickness();
			if (profile.ColorIndicator != LogColorIndicator.None)
				itemPadding = new Thickness(itemPadding.Left + colorIndicatorWidth, itemPadding.Top, itemPadding.Right, itemPadding.Bottom);
			var itemTemplateContent = new Func<IServiceProvider, object>(_ =>
			{
				var itemPanel = new Panel();
				new Border().Let(it =>
				{
					it.Bind(Border.BackgroundProperty, new Binding()
					{
						Converter = Converters.MarkColorToBackgroundConverter.DefaultWithoutDefaultColor,
						Path = nameof(DisplayableLog.MarkedColor),
					});
					itemPanel.Children.Add(it);
				});
				new Avalonia.Controls.TextBlock().Let(it =>
				{
					// empty view to reserve height of item
					it.Bind(TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
					it.Margin = itemPadding;
					it.Opacity = 0;
					it.Text = " ";
					itemPanel.Children.Add(it);
				});
				var propertyView = new Avalonia.Controls.TextBlock().Also(it =>
				{
					it.Bind(TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
					it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
					it.Bind(TextBlock.TextProperty, new Binding() { Path = propertyInMarkedItem });
					it.Margin = itemPadding;
					it.MaxLines = 1;
					it.TextTrimming = TextTrimming.CharacterEllipsis;
					it.TextWrapping = TextWrapping.NoWrap;
					it.VerticalAlignment = VerticalAlignment.Top;
					if (hasTimestamp)
						it.Bind(ToolTip.TipProperty, new Binding() { Path = nameof(DisplayableLog.TimestampString) });
				});
				itemPanel.Children.Add(propertyView);
				itemPanel.Children.Add(new Border().Also(border =>
				{
					border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("Brush/SessionView.MarkedLogListBox.Item.Border.Selected"));
					border.Bind(Border.BorderThicknessProperty, this.GetResourceObservable("Thickness/SessionView.MarkedLogListBox.Item.Border.Selected"));
					border.Bind(Border.CornerRadiusProperty, this.GetResourceObservable("CornerRadius/SessionView.MarkedLogListBox.Item.Border"));
					border.Bind(Border.IsVisibleProperty, new Binding()
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
						it.Bind(Border.BackgroundProperty, new Binding() { Path = nameof(DisplayableLog.ColorIndicatorBrush) });
						it.BorderBrush = colorIndicatorBorderBrush;
						it.BorderThickness = colorIndicatorBorderThickness;
						it.HorizontalAlignment = HorizontalAlignment.Left;
						it.Bind(ToolTip.TipProperty, new Binding() { Path = nameof(DisplayableLog.ColorIndicatorTip) });
						it.Width = colorIndicatorWidth;
						itemPanel.Children.Add(it);
					});
				}
				return new ControlTemplateResult(itemPanel, null);
			});
			return new DataTemplate()
			{
				Content = itemTemplateContent,
				DataType = typeof(DisplayableLog),
			};
		}


		// Create new operation duration analysis rule set.
		void CreateOperationDurationAnalysisRuleSet()
		{
			if (this.attachedWindow != null)
				OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
		}


		// Create predefined log text fliter.
		void CreatePredefinedLogTextFilter()
		{
			if (this.attachedWindow == null)
				return;
			PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, null, this.logTextFilterTextBox.Object);
		}


		// Detach from predefined log text filter
		void DetachFromPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged -= this.OnPredefinedLogTextFilterPropertyChanged;


		// Detach from session.
		void DetachFromSession(Session session)
		{
			// [Workaround] clear selection to prevent performance issue of de-select multiple items
			this.logListBox.SelectedItems.Clear();

			// remove event handler
			session.ErrorMessageGenerated -= this.OnErrorMessageGeneratedBySession;
			session.ExternalDependencyNotFound -= this.OnExternalDependencyNotFound;
			session.PropertyChanged -= this.OnSessionPropertyChanged;
			if (this.attachedLogs != null)
			{
				this.attachedLogs.CollectionChanged -= this.OnLogsChanged;
				this.attachedLogs = null;
			}
			(session.KeyLogAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnSessionKeyLogAnalysisRuleSetsChanged);

			// detach from commands
			this.canAddLogFiles.Unbind();
			this.canReloadLogs.Unbind();
			this.canResetLogProfileToSession.Unbind();
			this.canSetIPEndPoint.Unbind();
			this.canSaveLogs.Unbind();
			this.canSelectMarkedLogs.Update(false);
			this.canSetLogProfileToSession.Unbind();
			this.canSetUri.Unbind();
			this.canSetWorkingDirectory.Unbind();
			this.canShowWorkingDirectoryInExplorer.Update(false);

			// update properties
			this.canEditLogProfile.Update(false);
			this.SetValue<bool>(HasLogProfileProperty, false);
			this.validLogLevels.Clear();

			// stop auto scrolling
			this.scrollToLatestLogAction.Cancel();

			// update UI
			this.OnDisplayLogPropertiesChanged();
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
			if (!data.HasFileNames())
				return false;

			// bring window to front
			if (this.attachedWindow == null)
				return false;
			this.attachedWindow.ActivateAndBringToFront();

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
			var dropFilePaths = data.GetFileNames().AsNonNull();
			var dirPaths = new List<string>();
			var filePaths = new List<string>();
			await Task.Run(() =>
			{
				foreach (var path in dropFilePaths)
				{
					try
					{
						if (System.IO.File.Exists(path))
							filePaths.Add(path);
						else if (Directory.Exists(path))
							dirPaths.Add(path);
					}
					catch
					{ }
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
						Message = this.Application.GetString("SessionView.NoFilePathDropped")
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
						Message = this.Application.GetString("SessionView.TooManyDirectoryPathsDropped")
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
			var warningMessage = "";
			var currentLogProfile = session.LogProfile;
			var needNewLogProfile = Global.Run(() =>
			{
				if (currentLogProfile == null)
					return true;
				if (session.AreFileBasedLogs)
				{
					if (filePaths.IsEmpty())
						return true;
					if (!currentLogProfile.AllowMultipleFiles)
					{
						if (!session.IsLogFileNeeded || filePaths.Count > 1)
						{
							warningMessage = this.Application.GetString("SessionView.MultipleFilesAreNotAllowed");
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
			if (!string.IsNullOrEmpty(warningMessage))
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
					it.Filter = logProfile =>
					{
						return (logProfile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory))
							|| logProfile.IsWorkingDirectoryNeeded)
							&& !logProfile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory));
					};
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
			{
				// select precondition
				var precondition = this.Settings.GetValueOrDefault(SettingKeys.SelectLogReadingPreconditionForFiles) 
					? (await this.SelectLogReadingPreconditionAsync(LogDataSourceType.File, session.LastLogReadingPrecondition, false)).GetValueOrDefault()
					: new Logs.LogReadingPrecondition();

				// add files
				foreach (var filePath in filePaths)
				{
					session.AddLogFileCommand.TryExecute(new Session.LogDataSourceParams<string>()
					{
						Precondition = precondition,
						Source = filePath,
					});
				}
			}

			// complete
			return true;
		}


		// Earliest timestamp of selected log.
		DateTime? EarliestSelectedLogTimestamp { get => this.GetValue<DateTime?>(EarliestSelectedLogTimestampProperty); }


		// Edit given key log analysis rule set.
		void EditKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Edit current log profile.
		void EditLogProfile()
		{
			// check state
			if (!this.canEditLogProfile.Value)
				return;
			if (this.attachedWindow == null)
				return;

			// get profile
			if (this.DataContext is not Session session)
				return;
			var profile = session.LogProfile;
			if (profile == null)
				return;

			// edit log profile
			LogProfileEditorDialog.Show(this.attachedWindow, profile);
		}


		// Command of editing current log profile.
		ICommand EditLogProfileCommand { get; }


		// Edit given operation duration analysis rule set.
		void EditOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Edit given predefined log text filter.
		void EditPredefinedLogTextFilter(PredefinedLogTextFilter? filter)
		{
			if (filter == null || this.attachedWindow == null)
				return;
			PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, filter, null);
		}


		// Export given key log analysis rule set.
		async void ExportKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
		{
			// check state
			if (ruleSet == null || this.attachedWindow == null)
				return;
			
			// select file
			var fileName = await new SaveFileDialog().Also(it =>
			{
				it.Filters!.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this.attachedWindow);
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// export
			try
			{
				await ruleSet.SaveAsync(fileName, false);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Failed to export key log analysis rule set '{ruleSet.Id}' to '{fileName}'");
				if (this.attachedWindow != null)
				{
					_ = new MessageDialog()
					{
						Icon = MessageDialogIcon.Error,
						Message = this.Application.GetFormattedString("SessionView.FailedToExportLogAnalysisRuleSet", ruleSet.Name, fileName),
					}.ShowDialog(this.attachedWindow);
				}
			}
		}


		// Export given operation duration analysis rule set.
		async void ExportOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
		{
			// check state
			if (ruleSet == null || this.attachedWindow == null)
				return;
			
			// select file
			var fileName = await new SaveFileDialog().Also(it =>
			{
				it.Filters!.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this.attachedWindow);
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// export
			try
			{
				await ruleSet.SaveAsync(fileName, false);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Failed to export operation duration analysis rule set '{ruleSet.Id}' to '{fileName}'");
				if (this.attachedWindow != null)
				{
					_ = new MessageDialog()
					{
						Icon = MessageDialogIcon.Error,
						Message = this.Application.GetFormattedString("SessionView.FailedToExportLogAnalysisRuleSet", ruleSet.Name, fileName),
					}.ShowDialog(this.attachedWindow);
				}
			}
		}


		// Filter logs by process ID.
		void FilterLogsByProcessId(bool resetOtherFilters)
		{
			// check state
			this.VerifyAccess();
			if (!this.canFilterLogsByPid.Value)
				return;
			if (this.logListBox.SelectedItems.Count != 1)
				return;
			var log = (DisplayableLog)this.logListBox.SelectedItem.AsNonNull();
			var pid = log.ProcessId;
			if (pid == null)
				return;

			// filter
			if (resetOtherFilters)
				this.ResetLogFilters();
			this.logProcessIdFilterTextBox.Value = pid;
			this.updateLogFiltersAction.Reschedule();
		}


		// Command to filter logs by selected PID.
		ICommand FilterLogsByProcessIdCommand { get; }


		// Filter logs by thread ID.
		void FilterLogsByThreadId(bool resetOtherFilters)
		{
			// check state
			this.VerifyAccess();
			if (!this.canFilterLogsByTid.Value)
				return;
			if (this.logListBox.SelectedItems.Count != 1)
				return;
			var log = (DisplayableLog)this.logListBox.SelectedItem.AsNonNull();
			var tid = log.ThreadId;
			if (tid == null)
				return;

			// filter
			if (resetOtherFilters)
				this.ResetLogFilters();
			this.logThreadIdFilterTextBox.Value = tid;
			this.updateLogFiltersAction.Reschedule();
		}


		// Command to filter logs by selected TID.
		ICommand FilterLogsByThreadIdCommand { get; }


		// Check whether log profile has been set or not.
		bool HasLogProfile { get => this.GetValue<bool>(HasLogProfileProperty); }


		// Import key log analysis rule set.
		async void ImportKeyLogAnalysisRuleSet()
		{
			// check state
			if (this.attachedWindow == null)
				return;
			
			// select file
			var fileNames = await new OpenFileDialog().Also(dialog =>
			{
				dialog.Filters!.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this.attachedWindow);
			if (fileNames == null || fileNames.IsEmpty())
				return;
			
			// load rule set
			var ruleSet = (KeyLogAnalysisRuleSet?)null;
			try
			{
				ruleSet = await KeyLogAnalysisRuleSet.LoadAsync(this.Application, fileNames[0]);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Failed to load key log analysis rule set from '{fileNames[0]}' to import");
				if (this.attachedWindow != null)
				{
					_ = new MessageDialog()
					{
						Icon = MessageDialogIcon.Error,
						Message = this.Application.GetFormattedString("SessionView.FailedToImportLogAnalysisRuleSet", fileNames[0]),
					}.ShowDialog(this.attachedWindow);
				}
				return;
			}
			if (this.attachedWindow == null)
				return;

			// edit and add rule set
			KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Import operation duration analysis rule set.
		async void ImportOperationDurationAnalysisRuleSet()
		{
			// check state
			if (this.attachedWindow == null)
				return;
			
			// select file
			var fileNames = await new OpenFileDialog().Also(dialog =>
			{
				dialog.Filters!.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this.attachedWindow);
			if (fileNames == null || fileNames.IsEmpty())
				return;
			
			// load rule set
			var ruleSet = (OperationDurationAnalysisRuleSet?)null;
			try
			{
				ruleSet = await OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileNames[0]);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Failed to load operation duration analysis rule set from '{fileNames[0]}' to import");
				if (this.attachedWindow != null)
				{
					_ = new MessageDialog()
					{
						Icon = MessageDialogIcon.Error,
						Message = this.Application.GetFormattedString("SessionView.FailedToImportLogAnalysisRuleSet", fileNames[0]),
					}.ShowDialog(this.attachedWindow);
				}
				return;
			}
			if (this.attachedWindow == null)
				return;

			// edit and add rule set
			OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Check whether process info should be shown or not.
		bool IsProcessInfoVisible { get => this.GetValue<bool>(IsProcessInfoVisibleProperty); }


		// Get or set whether scrolling to latest log is needed or not.
		bool IsScrollingToLatestLogNeeded
		{
			get => this.GetValue<bool>(IsScrollingToLatestLogNeededProperty);
			set => this.SetValue<bool>(IsScrollingToLatestLogNeededProperty, value);
		}


		// Latest timestamp of selected log.
		DateTime? LatestSelectedLogTimestamp { get => this.GetValue<DateTime?>(LatestSelectedLogTimestampProperty); }


		// Get font family of log.
		FontFamily LogFontFamily { get => this.GetValue<FontFamily>(LogFontFamilyProperty); }


		// Get font size of log.
		double LogFontSize { get => this.GetValue<double>(LogFontSizeProperty); }


		// Mark logs with color.
		void MarkSelectedLogs(MarkColor color)
        {
			if (!this.canMarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			if (this.sidePanelContainer.IsVisible)
				session.IsMarkedLogsPanelVisible = true;
			session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams()
			{
				Color = color,
				Logs = this.logListBox.SelectedItems.Cast<DisplayableLog>().ToArray(),
			});
		}


		// Command to mark selected logs with given color.
		ICommand MarkSelectedLogsCommand { get; }


		// Mark or unmark selected logs.
		void MarkUnmarkSelectedLogs()
		{
			if (!this.canMarkUnmarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			var logs = this.logListBox.SelectedItems.Cast<DisplayableLog>().ToArray();
			foreach (var log in logs)
			{
				if (log.MarkedColor == MarkColor.None)
				{
					if (this.sidePanelContainer.IsVisible)
						session.IsMarkedLogsPanelVisible = true;
					break;
				}
			}
			session.MarkUnmarkLogsCommand.TryExecute(logs);
		}


		// Command to mark or unmark selected logs.
		ICommand MarkUnmarkSelectedLogsCommand { get; }


		// Max line count to display for each log.
		int MaxDisplayLineCountForEachLog { get => this.GetValue<int>(MaxDisplayLineCountForEachLogProperty); }


		// Called when application string resources updated.
		void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			// [Worksround] Force update content shown in controls
			var isScheduled = this.updateLogFiltersAction.IsScheduled;
			var selectedIndex = this.logLevelFilterComboBox.SelectedIndex;
			if (selectedIndex > 0)
				this.logLevelFilterComboBox.SelectedIndex = 0;
			else
				this.logLevelFilterComboBox.SelectedIndex = 1;
			this.logLevelFilterComboBox.SelectedIndex = selectedIndex;
			if (!isScheduled)
				this.updateLogFiltersAction.Cancel();
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
				this.hasDialogsObserverToken = (window as CarinaStudio.Controls.Window)?.GetObservable(CarinaStudio.Controls.Window.HasDialogsProperty)?.Subscribe(hasDialogs =>
				{
					if (!hasDialogs)
						this.ShowNextTutorial();
				});
			});

			// add event handlers
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
			this.Application.ProductManager.ProductStateChanged += this.OnProductStateChanged;
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.AddHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.AddHandler(DragDrop.DragLeaveEvent, this.OnDragLeave);
			this.AddHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.AddHandler(DragDrop.DropEvent, this.OnDrop);
			this.AddHandler(KeyDownEvent, this.OnPreviewKeyDown, RoutingStrategies.Tunnel);

			// check product state
			this.SetValue<bool>(IsProVersionActivatedProperty, this.Application.ProductManager.IsProductActivated(Products.Professional));
			if (this.GetValue<bool>(IsProVersionActivatedProperty))
				this.RecreateLogHeadersAndItemTemplate();

			// setup predefined log text filter list
			this.predefinedLogTextFilters.AddAll(PredefinedLogTextFilterManager.Default.Filters);
			foreach (var filter in PredefinedLogTextFilterManager.Default.Filters)
				this.AttachToPredefinedLogTextFilter(filter);
			((INotifyCollectionChanged)PredefinedLogTextFilterManager.Default.Filters).CollectionChanged += this.OnPredefinedLogTextFiltersChanged;

			// select log files or working directory
			if (this.canSetIPEndPoint.Value
				&& this.isIPEndPointNeededAfterLogProfileSet)
			{
				this.isIPEndPointNeededAfterLogProfileSet = false;
				this.autoSetIPEndPointAction.Reschedule(AutoAddLogFilesDelay);
			}
			else if (this.canAddLogFiles.Value 
				&& this.isLogFileNeededAfterLogProfileSet)
			{
				this.isLogFileNeededAfterLogProfileSet = false;
				this.autoAddLogFilesAction.Reschedule(AutoAddLogFilesDelay);
			}
			else if (this.canSetUri.Value 
				&& this.isUriNeededAfterLogProfileSet)
			{
				this.isUriNeededAfterLogProfileSet = false;
				this.autoSetUriAction.Reschedule(AutoAddLogFilesDelay);
			}
			else if (this.canSetWorkingDirectory.Value 
				&& this.isWorkingDirNeededAfterLogProfileSet)
			{
				this.isWorkingDirNeededAfterLogProfileSet = false;
				this.autoSetWorkingDirectoryAction.Reschedule(AutoAddLogFilesDelay);
			}

			// check administrator role
			this.ConfirmRestartingAsAdmin();

			// show tutorial
			this.SynchronizationContext.Post(() => this.ShowNextTutorial());
		}


		// Called when detach from view tree.
		protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			// update state
			this.isAttachedToLogicalTree = false;

			// remove event handlers
			this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
			this.Application.ProductManager.ProductStateChanged -= this.OnProductStateChanged;
			this.Settings.SettingChanged -= this.OnSettingChanged;
			this.RemoveHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.RemoveHandler(DragDrop.DragLeaveEvent, this.OnDragLeave);
			this.RemoveHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.RemoveHandler(DragDrop.DropEvent, this.OnDrop);
			this.RemoveHandler(KeyDownEvent, this.OnPreviewKeyDown);

			// release predefined log text filter list
			((INotifyCollectionChanged)PredefinedLogTextFilterManager.Default.Filters).CollectionChanged -= this.OnPredefinedLogTextFiltersChanged;
			foreach (var filter in this.predefinedLogTextFilters)
				this.DetachFromPredefinedLogTextFilter(filter);
			this.predefinedLogTextFilters.Clear();
			this.selectedPredefinedLogTextFilters.Clear();

			// detach from window
			this.areInitDialogsClosedObserverToken = this.areInitDialogsClosedObserverToken.DisposeAndReturnNull();
			this.hasDialogsObserverToken = this.hasDialogsObserverToken.DisposeAndReturnNull();
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
			var isProVersion = this.GetValue<bool>(IsProVersionActivatedProperty);
			var analysisResultIndicatorSize = isProVersion && app.TryFindResource("Double/SessionView.LogListBox.LogAnalysisResultIndicator.Size", out var rawResource) ? (double)rawResource! : default;
			var analysisResultIndicatorMargin = isProVersion && app.TryFindResource("Thickness/SessionView.LogListBox.LogAnalysisResultIndicator.Margin", out rawResource) ? (Thickness)rawResource! : default;
			var markIndicatorSize = app.TryFindResource("Double/SessionView.LogListBox.MarkIndicator.Size", out rawResource) ? (double)rawResource! : default;
			var markIndicatorMargin = app.TryFindResource("Thickness/SessionView.LogListBox.MarkIndicator.Margin", out rawResource) ? (Thickness)rawResource! : default;
			var splitterWidth = app.TryFindResource("Double/GridSplitter.Thickness", out rawResource) ? (double)rawResource! : default;
			var minHeaderWidth = app.TryFindResource("Double/SessionView.LogHeader.MinWidth", out rawResource) ? (double)rawResource! : default;
			var itemPadding = app.TryFindResource("Thickness/SessionView.LogListBox.Item.Padding", out rawResource) ? (Thickness)rawResource! : default;
			var colorIndicatorWidth = app.TryFindResource("Double/SessionView.LogListBox.ColorIndicator.Width", out rawResource) ? (double)rawResource! : default;
			var headerTemplate = (DataTemplate)this.DataTemplates.First(it => it is DataTemplate dt && dt.DataType == typeof(DisplayableLogProperty));
			var columIndexOffset = 0;
			if (profile.ColorIndicator != LogColorIndicator.None)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(colorIndicatorWidth, GridUnitType.Pixel));
				++columIndexOffset;
			}
			if (markIndicatorSize > 0)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(markIndicatorSize + markIndicatorMargin.Left + markIndicatorMargin.Right, GridUnitType.Pixel));
				this.logHeaderGrid.Children.Add(new Border().Also(border =>
				{
					Grid.SetColumn(border, columIndexOffset);
					border.Classes.Add("Icon");
					border.Child = new Image().Also(image =>
					{
						image.Classes.Add("Icon");
						if (app.TryFindResource("Image/Mark", out rawResource))
							image.Source = (rawResource as IImage);
					});
					border.Height = markIndicatorSize;
					border.Margin = markIndicatorMargin;
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
					border.Classes.Add("Icon");
					border.Child = new Image().Also(image =>
					{
						image.Classes.Add("Icon");
						if (app.TryFindResource("Image/Icon.Analysis", out rawResource))
							image.Source = (rawResource as IImage);
					});
					border.Height = analysisResultIndicatorSize;
					border.Margin = analysisResultIndicatorMargin;
					border.Width = analysisResultIndicatorSize;
					border.VerticalAlignment = VerticalAlignment.Center;
				}));
				++columIndexOffset;
			}
			if (itemPadding.Left > 0)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(itemPadding.Left, GridUnitType.Pixel));
				++columIndexOffset;
			}
			this.logHeaderGrid.Children.Add(new Border().Also(it => // Empty control in order to get first layout event of log header grid
			{
				it.HorizontalAlignment = HorizontalAlignment.Stretch;
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == Border.BoundsProperty)
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
					var splitter = new GridSplitter().Also(it =>
					{
						it.Background = Brushes.Transparent;
						it.DragDelta += (_, e) => this.ReportLogHeaderColumnWidths();
						it.HorizontalAlignment = HorizontalAlignment.Stretch;
						it.IsEnabled = this.logHeaderColumns[logPropertyIndex - 1].Width.GridUnitType == GridUnitType.Pixel
							|| headerColumn.Width.GridUnitType == GridUnitType.Pixel;
						it.VerticalAlignment = VerticalAlignment.Stretch;
					});
					Grid.SetColumn(splitter, logPropertyIndex * 2 - 1 + columIndexOffset);
					this.logHeaderGrid.Children.Add(splitter);
				}

				// create header view
				var headerView = ((Border)headerTemplate.Build(logProperty)).Also(it =>
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
			this.markedLogListBox.ItemTemplate = this.CreateMarkedLogItemTemplate(profile, logProperties);

			// check visible properties
			this.isPidLogPropertyVisible = false;
			this.isTidLogPropertyVisible = false;
			foreach (var logProperty in logProperties)
			{
				switch (logProperty.Name)
				{
					case nameof(DisplayableLog.ProcessId):
						this.isPidLogPropertyVisible = true;
						break;
					case nameof(DisplayableLog.ThreadId):
						this.isTidLogPropertyVisible = true;
						break;
				}
			}

			// show/hide log filters UI
			if (this.isPidLogPropertyVisible)
				this.logProcessIdFilterTextBoxPanel.IsVisible = true;
			else
			{
				this.logProcessIdFilterTextBoxPanel.IsVisible = false;
				this.logProcessIdFilterTextBox.Value = null;
			}
			if (this.isTidLogPropertyVisible)
				this.logThreadIdFilterTextBoxPanel.IsVisible = true;
			else
			{
				this.logThreadIdFilterTextBoxPanel.IsVisible = false;
				this.logThreadIdFilterTextBox.Value = null;
			}

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
			e.DragEffects = e.Data.HasFileNames()
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
			_ = new MessageDialog()
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
			
			// notify user
			await new MessageDialog()
			{
				Icon = MessageDialogIcon.Error,
				Message = this.Application.GetString("Common.ExternalDependencyNotFound"),
			}.ShowDialog(this.attachedWindow);

			// show external dependencies dialog
			if (this.attachedWindow == null || (session != null && !session.IsActivated))
				return;
			await new ExternalDependenciesDialog().ShowDialog(this.attachedWindow);

			// reload logs
			if (this.DataContext == session && session != null && session.HasAllDataSourceErrors)
				session.ReloadLogsCommand.TryExecute();
		}


		// Called when got focus.
        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
			this.Logger.LogTrace("Got focus");
        }


		// Called when user clicked the indicator of log analysis result.
		void OnLogAnalysisResultIndicatorClicked(DisplayableLog log)
		{
			if (this.DataContext is not Session session)
				return;
			var firstResult = (DisplayableLogAnalysisResult?)null;
			this.logAnalysisResultListBox.SelectedItems.Clear();
			foreach (var result in log.AnalysisResults)
			{
				firstResult ??= result;
				this.logAnalysisResultListBox.SelectedItems.Add(result);
			}
			if (firstResult != null)
			{
				session.IsLogAnalysisPanelVisible = true;
				this.logAnalysisResultListBox.ScrollIntoView(firstResult);
			}
		}


		// Called when log analysis result list box selection changed.
		void OnLogAnalysisResultListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			this.SynchronizationContext.Post(() =>
			{
				var count = this.logAnalysisResultListBox.SelectedItems.Count;
				if (count == 0)
					return;
				if (count == 1 
					&& this.logAnalysisResultListBox.SelectedItem is DisplayableLogAnalysisResult result
					&& this.DataContext is Session session)
				{
					result.Log?.Let(new Func<DisplayableLog, Task>(async (log) =>
					{
						// show all logs if needed
						if (!session.Logs.Contains(log))
						{
							// cancel showing marked logs only
							var isLogFound = false;
							var window = this.attachedWindow as MainWindow;
							if (session.IsShowingMarkedLogsTemporarily)
							{
								// cancel showing marked logs only
								if (!session.ToggleShowingMarkedLogsTemporarilyCommand.TryExecute())
									return;
								await Task.Yield();
								
								// show tutorial
								isLogFound = session.Logs.Contains(log);
								if (isLogFound 
									&& !this.PersistentState.GetValueOrDefault(IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey)
									&& window != null)
								{
									window.ShowTutorial(new Tutorial().Also(it =>
									{
										it.Anchor = this.FindControl<Control>("showMarkedLogsOnlyButton");
										it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.CancelShowingMarkedLogsOnlyForSelectingLogAnalysisResult"));
										it.Dismissed += (_, e) =>
											this.PersistentState.SetValue<bool>(IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey, true);
										it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
										it.IsSkippingAllTutorialsAllowed = false;
									}));
								}
							}

							// show all logs
							if (!isLogFound)
							{
								// show all logs
								if (session.IsShowingMarkedLogsTemporarily || !session.ToggleShowingAllLogsTemporarilyCommand.TryExecute())
									return;
								await Task.Yield();
								
								// show tutorial
								if (!this.PersistentState.GetValueOrDefault(IsShowAllLogsForLogAnalysisResultTutorialShownKey)
									&& window != null)
								{
									window.ShowTutorial(new Tutorial().Also(it =>
									{
										it.Anchor = this.FindControl<Control>("showAllLogsTemporarilyButton");
										it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.ShowAllLogsTemporarilyForSelectingLogAnalysisResult"));
										it.Dismissed += (_, e) =>
											this.PersistentState.SetValue<bool>(IsShowAllLogsForLogAnalysisResultTutorialShownKey, true);
										it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
										it.IsSkippingAllTutorialsAllowed = false;
									}));
								}
							}
						}

						// select log
						this.logListBox.SelectedItems.Clear();
						this.logListBox.SelectedItem = log;
						this.logListBox.ScrollIntoView(log);
						this.IsScrollingToLatestLogNeeded = false;
					}));
				}
				this.logListBox.Focus();
			});
		}


		// Called when selected item of log category changed.
		void OnLogCategoryListBoxSelectedItemChanged(Avalonia.Controls.ListBox? listBox, DisplayableLogCategory? category)
		{
			if (category == null)
				return;
			category.Log?.Let(log =>
			{
				this.IsScrollingToLatestLogNeeded = false;
				this.logListBox.SelectedItems.Clear();
				this.logListBox.SelectedItem = log;
				this.logListBox.ScrollIntoView(log);
			});
			this.SynchronizationContext.Post(() =>
			{
				if (listBox != null)
					listBox.SelectedItem = null;
				this.logListBox.Focus();
			});
		}


        // Called when log profile set.
        void OnLogProfileSet(LogProfile profile)
		{
			// reset auto scrolling
			this.IsScrollingToLatestLogNeeded = profile.IsContinuousReading;

			// check administrator role
			this.ConfirmRestartingAsAdmin();
		}


		// Called when log filter text box got focus.
		void OnLogFilterTextBoxGotFocus(object? sender, GotFocusEventArgs e)
		{
			if (sender is IVisual textBox)
				this.toolBarScrollViewer.ScrollIntoView(textBox);
		}


		// Called when property of log filter text box changed.
		void OnLogFilterTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == IntegerTextBox.ValueProperty || e.Property == RegexTextBox.ObjectProperty)
				this.updateLogFiltersAction.Reschedule();
		}


		// Called when selected log level filter has been changed.
		void OnLogLevelFilterComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e) => this.updateLogFiltersAction?.Reschedule();


		// Called when double click on log list box.
		void OnLogListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e)
		{
			this.ShowLogStringProperty();
			e.Handled = true;
		}


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
				if (it == null)
					return (IVisual?)null;
				var listBoxItem = it.FindAncestorOfType<ListBoxItem>(true);
				if (listBoxItem != null)
					return listBoxItem;
				return it.FindAncestorOfType<ScrollBar>(true);
			});
			if (hitControl == null)
				this.SynchronizationContext.Post(() => this.logListBox.SelectedItems.Clear());
			else if (hitControl is ListBoxItem && (e.KeyModifiers & KeyModifiers.Control) == 0 && point.Properties.IsLeftButtonPressed)
			{
				// [Workaround] Clear selection first to prevent performance issue of changing selection from multiple items
				this.logListBox.SelectedItems.Clear();
			}

			// reset clicked log property
			this.lastClickedLogPropertyView = null;
			this.canCopyLogProperty.Update(false);
			this.canShowLogProperty.Update(false);
		}


		// Called when pointer released on log list box.
		void OnLogListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
				this.isPointerPressedOnLogListBox = false;
		}


		// Called when pointer wheel change on log list box.
		void OnLogListBoxPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			this.SynchronizationContext.Post(() => this.UpdateIsScrollingToLatestLogNeeded(-e.Delta.Y));
		}


		// Called when log list box scrolled.
		void OnLogListBoxScrollChanged(object? sender, ScrollChangedEventArgs e)
		{
			// update auto scrolling state
			if (this.isPointerPressedOnLogListBox 
				|| this.pressedKeys.Contains(Avalonia.Input.Key.Down)
				|| this.pressedKeys.Contains(Avalonia.Input.Key.Up)
				|| this.pressedKeys.Contains(Avalonia.Input.Key.Home)
				|| this.pressedKeys.Contains(Avalonia.Input.Key.End))
			{
				this.UpdateIsScrollingToLatestLogNeeded(e.OffsetDelta.Y);
			}

			// sync log header offset
			var logScrollViewer = this.logScrollViewer;
			if (logScrollViewer != null)
				this.logHeaderContainer.Margin = new Thickness(-logScrollViewer.Offset.X, 0, logScrollViewer.Offset.X, 0);
		}


		// Called when log list box selection changed.
		void OnLogListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
			this.OnLogListBoxSelectionChanged();
		void OnLogListBoxSelectionChanged()
		{
			// [Workaround] ListBox.SelectedItems is not update yet when calling this method
			this.SynchronizationContext.Post(() =>
			{
				// check state
				if (this.DataContext is not Session session)
					return;
				var selectionCount = this.logListBox.SelectedItems.Count;
				var hasSelectedItems = (selectionCount > 0);
				var hasSingleSelectedItem = (selectionCount == 1);
				var logProperty = hasSingleSelectedItem
					? this.lastClickedLogPropertyView?.Tag as DisplayableLogProperty
					: null;

				// update command states
				this.canCopyLogProperty.Update(hasSingleSelectedItem && logProperty != null);
				this.canCopySelectedLogs.Update(hasSelectedItems && session.CopyLogsCommand.CanExecute(null) && selectionCount <= MaxLogCountForCopying);
				this.canCopySelectedLogsWithFileNames.Update(hasSelectedItems && session.CopyLogsWithFileNamesCommand.CanExecute(null) && selectionCount <= MaxLogCountForCopying);
				this.canFilterLogsByPid.Update(hasSingleSelectedItem && this.isPidLogPropertyVisible);
				this.canFilterLogsByTid.Update(hasSingleSelectedItem && this.isTidLogPropertyVisible);
				this.canMarkSelectedLogs.Update(hasSelectedItems && session.MarkLogsCommand.CanExecute(null));
				this.canMarkUnmarkSelectedLogs.Update(hasSelectedItems && session.MarkUnmarkLogsCommand.CanExecute(null));
				this.canShowFileInExplorer.Update(hasSelectedItems && session.IsLogFileNeeded);
				this.canShowLogProperty.Update(hasSingleSelectedItem && logProperty != null && DisplayableLog.HasStringProperty(logProperty.Name));
				this.canUnmarkSelectedLogs.Update(hasSelectedItems && session.UnmarkLogsCommand.CanExecute(null));

				// report time information
				this.reportSelectedLogsTimeInfoAction.Schedule();

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
					this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItems.Clear());
			});
		}


		// Called when list of logs changed.
		void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Remove)
			{
				// [Workaround] Force updating item containers
				if (this.logScrollViewer != null)
				{
					var offset = this.logScrollViewer.Offset;
					if (Math.Abs(offset.Y) > 0.1 && !this.IsScrollingToLatestLogNeeded)
					{
						var oldStartIndex = e.OldStartingIndex;
						var oldItems = e.OldItems.AsNonNull();
						var lastVisibleItemIndex = -1;
						var firstVisibleItemIndex = -1;
						this.logListBox.ItemContainerGenerator.Let(it => 
						{
							foreach (var containerInfo in it.Containers)
							{
								if (firstVisibleItemIndex < 0 || containerInfo.Index < firstVisibleItemIndex)
									firstVisibleItemIndex = containerInfo.Index;
								if (lastVisibleItemIndex < 0 || containerInfo.Index > lastVisibleItemIndex)
									lastVisibleItemIndex = containerInfo.Index;
							}
						});
						this.logScrollViewer.PageUp();
						this.logScrollViewer.LineUp();
						this.logScrollViewer.LineDown();
						this.logScrollViewer.PageDown();
						if (firstVisibleItemIndex >= 0 
							&& lastVisibleItemIndex >= 0
							&& oldStartIndex + oldItems.Count <= firstVisibleItemIndex)
						{
							firstVisibleItemIndex -= oldItems.Count;
							this.logListBox.ScrollIntoView(firstVisibleItemIndex);
						}
					}
				}
			}
		}


		// Called when key down.
		protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
		{
			this.pressedKeys.Add(e.Key);
			if (!e.Handled)
			{
				var isCmdPressed = Platform.IsMacOS && (this.pressedKeys.Contains(Avalonia.Input.Key.LWin) || this.pressedKeys.Contains(Avalonia.Input.Key.RWin));
				var isCtrlPressed = Platform.IsMacOS ? isCmdPressed : (e.KeyModifiers & KeyModifiers.Control) != 0;
				if (this.Application.IsDebugMode && e.Source is not TextBox)
					this.Logger.LogTrace($"[KeyDown] {e.Key}, Ctrl/Cmd: {isCtrlPressed}, Shift: {(e.KeyModifiers & KeyModifiers.Shift) != 0}, Alt: {(e.KeyModifiers & KeyModifiers.Alt) != 0}");
				if (isCtrlPressed || isCmdPressed)
				{
					var isAltPressed = ((e.KeyModifiers & KeyModifiers.Alt) != 0);
					switch (e.Key)
					{
						case Avalonia.Input.Key.A:
							e.Handled = true;
							break;
						case Avalonia.Input.Key.C:
							if (e.Source is not TextBox)
							{
								if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
									this.CopySelectedLogsWithFileNames();
								else
									this.CopySelectedLogs();
							}
							break;
						case Avalonia.Input.Key.D0:
							if (isAltPressed)
							{
								this.UnmarkSelectedLogs();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D1:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Red);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D2:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Orange);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D3:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Yellow);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D4:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Green);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D5:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Blue);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D6:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Indigo);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D7:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Purple);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.D8:
							if (isAltPressed)
							{
								this.MarkSelectedLogs(MarkColor.Magenta);
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.F:
							if (this.logTextFilterTextBox.IsEnabled)
							{
								this.logTextFilterTextBox.Focus();
								this.logTextFilterTextBox.SelectAll();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.M:
							this.MarkSelectedLogs(MarkColor.Default);
							e.Handled = true;
							break;
						case Avalonia.Input.Key.N:
							if (!Platform.IsMacOS)
							{
								this.FindAncestorOfType<MainWindow>()?.CreateMainWindow();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.O:
							this.AddLogFiles();
							break;
						case Avalonia.Input.Key.P:
							predefinedLogTextFiltersPopup.IsOpen = !predefinedLogTextFiltersPopup.IsOpen;
							break;
						case Avalonia.Input.Key.S:
							if (e.Source == this.logTextFilterTextBox)
							{
								this.logTextFilterTextBox.Validate();
								if (this.logTextFilterTextBox.IsTextValid && this.logTextFilterTextBox.Object != null)
									this.CreatePredefinedLogTextFilter();
							}
							else
								this.SaveLogs((e.KeyModifiers & KeyModifiers.Shift) != 0);
							break;
					}
				}
				if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
				{
					switch (e.Key)
                    {
						case Avalonia.Input.Key.A:
							(this.DataContext as Session)?.ToggleShowingAllLogsTemporarilyCommand?.TryExecute();
							this.SynchronizationContext.Post(this.Focus); // [Workaround] Get focus back to prevent unexpected focus lost.
							break;
						case Avalonia.Input.Key.M:
							(this.DataContext as Session)?.ToggleShowingMarkedLogsTemporarilyCommand?.TryExecute();
							this.SynchronizationContext.Post(this.Focus); // [Workaround] Get focus back to prevent unexpected focus lost.
							break;
					}
				}
				else
				{
					switch (e.Key)
					{
						case Avalonia.Input.Key.Down:
							if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectNextItem();
								else
								{
									var selectedItems = this.logListBox.SelectedItems;
									if (selectedItems.Count > 1)
									{
										var latestSelectedItem = selectedItems[selectedItems.Count - 1];
										selectedItems.Clear(); // [Workaround] clear selection first to prevent performance issue of de-selecting multiple items
										if (latestSelectedItem != null)
										{
											selectedItems.Add(latestSelectedItem);
											this.logListBox.ScrollIntoView(latestSelectedItem);
										}
									}
									this.logListBox.SelectNextItem();
								}
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.Up:
							if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectPreviousItem();
								else
								{
									var selectedItems = this.logListBox.SelectedItems;
									if (selectedItems.Count > 1)
									{
										var latestSelectedItem = selectedItems[selectedItems.Count - 1];
										selectedItems.Clear(); // [Workaround] clear selection first to prevent performance issue of de-selecting multiple items
										if (latestSelectedItem != null)
										{
											selectedItems.Add(latestSelectedItem);
											this.logListBox.ScrollIntoView(latestSelectedItem);
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
			else if (this.Application.IsDebugMode && e.Source is not TextBox)
				this.Logger.LogTrace($"[KeyDown] {e.Key} was handled by another component");
			base.OnKeyDown(e);
		}


		// Called when selection of list box of key log analysis rule sets has been changed.
		void OnKeyLogAnalysisRuleSetListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			foreach (var ruleSet in e.RemovedItems.Cast<KeyLogAnalysisRuleSet>())
				this.selectedKeyLogAnalysisRuleSets.Remove(ruleSet);
			foreach (var ruleSet in e.AddedItems.Cast<KeyLogAnalysisRuleSet>())
				this.selectedKeyLogAnalysisRuleSets.Add(ruleSet);
			if (this.selectedKeyLogAnalysisRuleSets.Count != this.keyLogAnalysisRuleSetListBox.SelectedItems.Count)
			{
				// [Workaround] Need to sync selection back to control because selection will be cleared when popup opened
				if (this.selectedKeyLogAnalysisRuleSets.IsNotEmpty())
				{
					var isScheduled = this.updateLogAnalysisAction?.IsScheduled ?? false;
					this.selectedKeyLogAnalysisRuleSets.ToArray().Let(it =>
					{
						this.SynchronizationContext.Post(() =>
						{
							this.keyLogAnalysisRuleSetListBox.SelectedItems.Clear();
							foreach (var ruleSet in it)
								this.keyLogAnalysisRuleSetListBox.SelectedItems.Add(ruleSet);
							if (!isScheduled)
								this.updateLogAnalysisAction?.Cancel();
						});
					});
				}
			}
			else
				this.updateLogAnalysisAction.Reschedule(this.UpdateLogAnalysisParamsDelay);
		}


		// Called when key up.
		protected override void OnKeyUp(Avalonia.Input.KeyEventArgs e)
		{
			// [Workaround] skip handling key event if it was handled by context menu
			// check whether key down was received or not
			if (!this.pressedKeys.Contains(e.Key))
			{
				this.Logger.LogTrace($"[KeyUp] Key down of {e.Key} was not received");
				return;
			}

			// handle key event for single key
			if (!e.Handled)
			{
				var isCmdPressed = Platform.IsMacOS && (this.pressedKeys.Contains(Avalonia.Input.Key.LWin) || this.pressedKeys.Contains(Avalonia.Input.Key.RWin));
				var isCtrlPressed = Platform.IsMacOS ? isCmdPressed : (e.KeyModifiers & KeyModifiers.Control) != 0;
				if (this.Application.IsDebugMode && e.Source is not TextBox)
					this.Logger.LogTrace($"[KeyUp] {e.Key}, Ctrl/Cmd: {isCmdPressed}, Shift: {(e.KeyModifiers & KeyModifiers.Shift) != 0}, Alt: {(e.KeyModifiers & KeyModifiers.Alt) != 0}");
				if (!isCmdPressed && e.KeyModifiers == 0)
				{
					switch (e.Key)
					{
						case Avalonia.Input.Key.End:
							if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectLastItem();
								else
									this.logListBox.SelectLastItem();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.Escape:
							if (e.Source is TextBox)
							{
								if (this.Application.IsDebugMode)
									this.Logger.LogTrace($"[KeyUp] {e.Key} on text box");
								this.logListBox.Focus();
								e.Handled = true;
							}
							else if (this.predefinedLogTextFiltersPopup.IsOpen)
							{
								this.predefinedLogTextFiltersPopup.IsOpen = false;
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.F5:
							this.ReloadLogs();
							e.Handled = true;
							break;
						case Avalonia.Input.Key.Home:
							if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectFirstItem();
								else
									this.logListBox.SelectFirstItem();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.M:
							if (e.Source is not TextBox)
							{
								this.MarkUnmarkSelectedLogs();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.P:
							if (e.Source is not TextBox)
							{
								(this.DataContext as Session)?.PauseResumeLogsReadingCommand?.TryExecute();
								e.Handled = true;
							}
							break;
						case Avalonia.Input.Key.S:
							if (e.Source is not TextBox && !this.isSelectingFileToSaveLogs)
							{
								this.SelectMarkedLogs();
								e.Handled = true;
							}
							break;
					}
				}
			}
			else if (this.Application.IsDebugMode && e.Source is not TextBox)
				this.Logger.LogTrace($"[KeyUp] {e.Key} was handled by another component");

			// stop tracking key
			this.pressedKeys.Remove(e.Key);

			// call base
			base.OnKeyUp(e);
		}


		// Called when lost focus.
        protected override void OnLostFocus(RoutedEventArgs e)
        {
			this.Logger.LogTrace("Lost focus");
            base.OnLostFocus(e);
        }


        // Called when selection in marked log list box has been changed.
        void OnMarkedLogListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.DataContext is not Session session 
				|| this.markedLogListBox.SelectedItem is not DisplayableLog log)
			{
				return;
			}
			var index = session.Logs.IndexOf(log);
			this.logListBox.Let(it =>
			{
				it.SelectedItems.Clear();
				if (index >= 0)
				{
					it.SelectedIndex = index;
					it.ScrollIntoView(index);
				}
				else
					this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItems.Clear());
				it.Focus();
			});
			this.IsScrollingToLatestLogNeeded = false;
		}


		// Called when selection of list box of operation duration analysis rule sets has been changed.
		void OnOperationDurationAnalysisRuleSetListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			foreach (var ruleSet in e.RemovedItems.Cast<OperationDurationAnalysisRuleSet>())
				this.selectedOperationDurationAnalysisRuleSets.Remove(ruleSet);
			foreach (var ruleSet in e.AddedItems.Cast<OperationDurationAnalysisRuleSet>())
				this.selectedOperationDurationAnalysisRuleSets.Add(ruleSet);
			if (this.selectedOperationDurationAnalysisRuleSets.Count != this.operationDurationAnalysisRuleSetListBox.SelectedItems.Count)
			{
				// [Workaround] Need to sync selection back to control because selection will be cleared when popup opened
				if (this.selectedOperationDurationAnalysisRuleSets.IsNotEmpty())
				{
					var isScheduled = this.updateLogAnalysisAction?.IsScheduled ?? false;
					this.selectedOperationDurationAnalysisRuleSets.ToArray().Let(it =>
					{
						this.SynchronizationContext.Post(() =>
						{
							this.operationDurationAnalysisRuleSetListBox.SelectedItems.Clear();
							foreach (var ruleSet in it)
								this.operationDurationAnalysisRuleSetListBox.SelectedItems.Add(ruleSet);
							if (!isScheduled)
								this.updateLogAnalysisAction?.Cancel();
						});
					});
				}
			}
			else
				this.updateLogAnalysisAction.Reschedule(this.UpdateLogAnalysisParamsDelay);
		}


		// Called when selection of list box of predefined log text fliter has been changed.
		void OnPredefinedLogTextFilterListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			foreach (var filter in e.RemovedItems.Cast<PredefinedLogTextFilter>())
				this.selectedPredefinedLogTextFilters.Remove(filter);
			foreach (var filter in e.AddedItems.Cast<PredefinedLogTextFilter>())
				this.selectedPredefinedLogTextFilters.Add(filter);
			if (this.selectedPredefinedLogTextFilters.Count != this.predefinedLogTextFilterListBox.SelectedItems.Count)
			{
				// [Workaround] Need to sync selection back to control because selection will be cleared when popup opened
				if (this.selectedPredefinedLogTextFilters.IsNotEmpty())
				{
					var isScheduled = this.updateLogFiltersAction?.IsScheduled ?? false;
					this.selectedPredefinedLogTextFilters.ToArray().Let(it =>
					{
						this.SynchronizationContext.Post(() =>
						{
							this.predefinedLogTextFilterListBox.SelectedItems.Clear();
							foreach (var filter in it)
								this.predefinedLogTextFilterListBox.SelectedItems.Add(filter);
							if (!isScheduled)
								this.updateLogFiltersAction?.Cancel();
						});
					});
				}
			}
			else
				this.updateLogFiltersAction.Reschedule(this.UpdateLogFilterParamsDelay);
		}


		// Called when property of predefined log text filter has been changed.
		void OnPredefinedLogTextFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not PredefinedLogTextFilter filter)
				return;
			switch (e.PropertyName)
			{
				case nameof(PredefinedLogTextFilter.Name):
					this.predefinedLogTextFilters.Sort(filter);
					break;
				case nameof(PredefinedLogTextFilter.Regex):
					if (this.predefinedLogTextFilterListBox.SelectedItems.Contains(filter))
						this.updateLogFiltersAction.Reschedule();
					break;
			}
		}


		// Called when list of predefined log text filters has been changed.
		void OnPredefinedLogTextFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (var filter in e.NewItems.AsNonNull().Cast<PredefinedLogTextFilter>())
					{
						this.AttachToPredefinedLogTextFilter(filter);
						this.predefinedLogTextFilters.Add(filter);
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (var filter in e.OldItems.AsNonNull().Cast<PredefinedLogTextFilter>())
					{
						this.DetachFromPredefinedLogTextFilter(filter);
						this.predefinedLogTextFilters.Remove(filter);
						this.selectedPredefinedLogTextFilters.Remove(filter);
					}
					break;
			}
		}


		// Called to handle key-down before all children.
		async void OnPreviewKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
		{
			// [Workaround] It will take long time to select all items by list box itself
			if (!e.Handled 
				&& e.Source is not TextBox
				&& (e.KeyModifiers & KeyModifiers.Control) != 0 
				&& e.Key == Avalonia.Input.Key.A
				&& this.DataContext is Session session)
			{
				// intercept
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
							Message = this.Application.GetString("SessionView.ConfirmSelectingAllLogs"),
						}.ShowDialog(this.attachedWindow);
						if (result == MessageDialogResult.No)
							return;
					}
				}

				// select all logs
				var selectedItems = this.logListBox.SelectedItems;
				selectedItems.Clear();
				if (selectedItems is AvaloniaList<object> avaliniaList)
					avaliniaList.AddRange(session.Logs);
				else
				{
					foreach (var log in session.Logs)
						selectedItems.Add(log);
				}
				
			}
		}


		// Called when product state changed.
		void OnProductStateChanged(IProductManager? productManager, string productId)
		{
			if (productManager == null || productId != Products.Professional)
				return;
			productManager.TryGetProductState(productId, out var state);
			this.SetValue<bool>(IsProVersionActivatedProperty, state == ProductState.Activated);
			if (state == ProductState.Deactivated || state == ProductState.Activated)
				this.RecreateLogHeadersAndItemTemplate();
		}


		// Property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			var property = change.Property;
			if (property == DataContextProperty)
			{
				(change.OldValue.Value as Session)?.Let(session => this.DetachFromSession(session));
				(change.NewValue.Value as Session)?.Let(session => this.AttachToSession(session));
			}
			else if (property == IsScrollingToLatestLogNeededProperty)
			{
				if (this.IsScrollingToLatestLogNeeded)
				{
					var logProfile = (this.DataContext as Session)?.LogProfile;
					if (logProfile != null && !logProfile.IsContinuousReading)
					{
						this.scrollToLatestLogAction.Execute();
						this.SynchronizationContext.Post(() => this.IsScrollingToLatestLogNeeded = false);
					}
					else
						this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
				}
				else
					this.scrollToLatestLogAction.Cancel();
			}
			else if (property == SelectedLogsDurationProperty)
				this.SetValue<bool>(HasSelectedLogsDurationProperty, change.NewValue.Value != null);
		}


		// Called when list of key log analysis of session changed.
		void OnSessionKeyLogAnalysisRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			var isUpdateShceduled = this.updateLogAnalysisAction.IsScheduled;
			var syncBack = false;
			var selectedItems = this.keyLogAnalysisRuleSetListBox.SelectedItems;
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (var ruleSet in e.NewItems!.Cast<KeyLogAnalysisRuleSet>())
					{
						if (!selectedItems.Contains(ruleSet))
						{
							syncBack = true;
							selectedItems.Add(ruleSet);
						}
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (var ruleSet in e.OldItems!.Cast<KeyLogAnalysisRuleSet>())
					{
						if (selectedItems.Contains(ruleSet))
						{
							syncBack = true;
							selectedItems.Remove(ruleSet);
						}
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					syncBack = true;
					selectedItems.Clear();
					if (this.DataContext is Session session)
					{
						foreach (var ruleSet in session.KeyLogAnalysisRuleSets)
							selectedItems.Add(ruleSet);
					}
					break;
				default:
					this.Logger.LogError($"Unsupported change of key log analysis rule sets: {e.Action}");
					break;
			}
			if (syncBack && !isUpdateShceduled)
				this.updateLogAnalysisAction.Cancel();
		}


		// Called when property of session has been changed.
		void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not Session session)
				return;
			switch (e.PropertyName)
			{
				case nameof(Session.DisplayLogProperties):
					this.OnDisplayLogPropertiesChanged();
					break;
				case nameof(Session.HasAllDataSourceErrors):
				case nameof(Session.HasLogReaders):
				case nameof(Session.HasPartialDataSourceErrors):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.HasLogs):
					if (!session.HasLogs)
						this.scrollToLatestLogAction.Cancel();
					else if (this.IsScrollingToLatestLogNeeded)
						this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
					break;
				case nameof(Session.HasMarkedLogs):
					this.canSelectMarkedLogs.Update(session.HasMarkedLogs);
					break;
				case nameof(Session.HasWorkingDirectory):
					this.canShowWorkingDirectoryInExplorer.Update(Platform.IsOpeningFileManagerSupported && session.HasWorkingDirectory);
					break;
				case nameof(Session.IsActivated):
					if (!session.IsActivated)
						this.scrollToLatestLogAction.Cancel();
					else if (this.HasLogProfile && session.LogProfile?.IsContinuousReading == true && this.IsScrollingToLatestLogNeeded)
						this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
					break;
				case nameof(Session.IsLogAnalysisPanelVisible):
				case nameof(Session.IsLogFilesPanelVisible):
				case nameof(Session.IsMarkedLogsPanelVisible):
				case nameof(Session.IsTimestampCategoriesPanelVisible):
					if (session.IsLogAnalysisPanelVisible 
						|| session.IsLogFilesPanelVisible 
						|| session.IsMarkedLogsPanelVisible
						|| session.IsTimestampCategoriesPanelVisible)
					{
						switch (e.PropertyName)
						{
							case nameof(Session.IsLogAnalysisPanelVisible):
								if (session.IsLogAnalysisPanelVisible)
								{
									session.IsLogFilesPanelVisible = false;
									session.IsMarkedLogsPanelVisible = false;
									session.IsTimestampCategoriesPanelVisible = false;

									// show tutorial
									if (!this.PersistentState.GetValueOrDefault(IsSelectLogAnalysisRuleSetsTutorialShownKey)
										&& this.attachedWindow is MainWindow window)
									{
										window.ShowTutorial(new Tutorial().Also(it =>
										{
											it.Anchor = this.FindControl<Control>("logAnalysisRuleSetsButton");
											it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.SelectLogAnalysisRuleSets"));
											it.Dismissed += (_, e) => 
												this.PersistentState.SetValue<bool>(IsSelectLogAnalysisRuleSetsTutorialShownKey, true);
											it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
											it.IsSkippingAllTutorialsAllowed = false;
										}));
									}
								}
								break;
							case nameof(Session.IsLogFilesPanelVisible):
								if (session.IsLogFilesPanelVisible)
								{
									session.IsLogAnalysisPanelVisible = false;
									session.IsMarkedLogsPanelVisible = false;
									session.IsTimestampCategoriesPanelVisible = false;
								}
								break;
							case nameof(Session.IsMarkedLogsPanelVisible):
								if (session.IsMarkedLogsPanelVisible)
								{
									session.IsLogAnalysisPanelVisible = false;
									session.IsLogFilesPanelVisible = false;
									session.IsTimestampCategoriesPanelVisible = false;
								}
								break;
							case nameof(Session.IsTimestampCategoriesPanelVisible):
								if (session.IsTimestampCategoriesPanelVisible)
								{
									session.IsLogAnalysisPanelVisible = false;
									session.IsLogFilesPanelVisible = false;
									session.IsMarkedLogsPanelVisible = false;
								}
								break;
						}
						this.keepSidePanelVisible = true;
						sidePanelColumn.Width = new GridLength(new double[] {
							session.LogAnalysisPanelSize,
							session.LogFilesPanelSize,
							session.MarkedLogsPanelSize,
							session.TimestampCategoriesPanelSize,
						}.Max());
						Grid.SetColumnSpan(this.logListBoxContainer, 1);
					}
					else
					{
						this.keepSidePanelVisible = false;
						sidePanelColumn.Width = new GridLength(0);
						Grid.SetColumnSpan(this.logListBoxContainer, 3);
					}
					break;
				case nameof(Session.IsLogsReadingPaused):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.LogProfile):
					session.LogProfile?.Let(profile =>
					{
						if (profile != null)
						{
							this.canEditLogProfile.Update(!profile.IsBuiltIn);
							this.SetValue<bool>(HasLogProfileProperty, true);
							if (!profile.IsContinuousReading)
								this.IsScrollingToLatestLogNeeded = false;
						}
						else
						{
							this.canEditLogProfile.Update(false);
							this.SetValue<bool>(HasLogProfileProperty, false);
						}
					});
					break;
				case nameof(Session.Logs):
					// [Workaround] SelectionChange may not be fired after changing items
					this.SynchronizationContext.Post(this.OnLogListBoxSelectionChanged);
					
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
			if (e.Key == SettingKeys.IgnoreCaseOfLogTextFilter)
				this.logTextFilterTextBox.IgnoreCase = (bool)e.Value;
			else if (e.Key == SettingKeys.LogFontFamily)
				this.UpdateLogFontFamily();
			else if (e.Key == SettingKeys.LogFontSize)
				this.UpdateLogFontSize();
			else if (e.Key == SettingKeys.MaxDisplayLineCountForEachLog)
				this.SetValue<int>(MaxDisplayLineCountForEachLogProperty, Math.Max(1, (int)e.Value));
			else if (e.Key == AppSuite.SettingKeys.ShowProcessInfo)
				this.SetValue<bool>(IsProcessInfoVisibleProperty, (bool)e.Value);
			else if (e.Key == SettingKeys.UpdateLogFilterDelay)
				this.logTextFilterTextBox.ValidationDelay = this.UpdateLogFilterParamsDelay;
		}


		// Called when pointer released on item of status bar.
		void OnStatusBarItemPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (sender is not Control control || e.InitialPressMouseButton != Avalonia.Input.MouseButton.Left)
				return;
			this.SynchronizationContext.Post(() => control.ContextMenu?.Open());
		}


		// Called when test button clicked.
		void OnTestButtonClick(object? sender, RoutedEventArgs e)
		{
			//this.Application.Restart(AppSuite.AppSuiteApplication.RestoreMainWindowsArgument);
		}


		// Called when pointer released on tool bar.
		void OnToolBarPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (Avalonia.Input.FocusManager.Instance?.Current is not TextBox)
				this.SynchronizationContext.Post(() => this.logListBox.Focus());
		}


		// Sorted predefined log text filters.
		IList<PredefinedLogTextFilter> PredefinedLogTextFilters { get => this.predefinedLogTextFilters; }


		// Rebuild log header views and template of log item.
		void RecreateLogHeadersAndItemTemplate()
		{
			if (this.DataContext is not Session session)
				return;
			this.logListBox.Items = null;
			this.OnDisplayLogPropertiesChanged();
			this.logListBox.Bind(Avalonia.Controls.ListBox.ItemsProperty, new Binding() { Path = nameof(Session.Logs)} );
		}


		// Reload log file.
		async void ReloadLogFile(string? fileName)
		{
			// check state
			this.VerifyAccess();
			if (this.DataContext is not Session session)
				return;
			if (fileName == null)
				return;
			var logFileInfo = session.LogFiles.FirstOrDefault(it => PathEqualityComparer.Default.Equals(it.FileName, fileName));
			if (logFileInfo == null || logFileInfo.IsPredefined)
				return;
			
			// select precondition
			var precondition = await this.SelectLogReadingPreconditionAsync(LogDataSourceType.File, logFileInfo.LogReadingPrecondition, true);
			if (!precondition.HasValue)
				return;
			
			// reload log file
			session.ReloadLogFileCommand.TryExecute(new Session.LogDataSourceParams<string>()
			{
				Precondition = precondition.Value,
				Source = logFileInfo.FileName,
			});
		}


		// Reload log file without reading precondition.
		void ReloadLogFileWithoutLogReadingPrecondition(string? fileName)
		{
			// check state
			this.VerifyAccess();
			if (this.DataContext is not Session session)
				return;
			if (fileName == null)
				return;
			var logFileInfo = session.LogFiles.FirstOrDefault(it => PathEqualityComparer.Default.Equals(it.FileName, fileName));
			if (logFileInfo == null || logFileInfo.IsPredefined || logFileInfo.LogReadingPrecondition.IsEmpty)
				return;
			
			// reload log file
			session.ReloadLogFileCommand.TryExecute(new Session.LogDataSourceParams<string>()
			{
				Source = logFileInfo.FileName,
			});
		}


		// Reload logs.
		void ReloadLogs()
        {
			// check state
			this.VerifyAccess();
			if (this.DataContext is not Session session)
				return;

			// reload logs
			if (!session.ReloadLogsCommand.TryExecute())
				return;

			// scroll to latest log
			if (session.LogProfile?.IsContinuousReading == true
				&& !this.IsScrollingToLatestLogNeeded
				&& this.Settings.GetValueOrDefault(SettingKeys.EnableScrollingToLatestLogAfterReloadingLogs))
			{
				this.Logger.LogDebug("Enable scrolling to latest log after reloading logs");
				this.IsScrollingToLatestLogNeeded = true;
			}
        }


		// Command to reload logs.
		ICommand ReloadLogsCommand { get; }


		// Remove given key log analysis rule set.
		void RemoveKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null)
				return;
			KeyLogAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
		}


		// Remove given operation duration analysis rule set.
		void RemoveOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null)
				return;
			OperationDurationAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
		}


		// Remove given predefined log text filter.
		void RemovePredefinedLogTextFilter(PredefinedLogTextFilter? filter)
		{
			if (filter == null)
				return;
			PredefinedLogTextFilterManager.Default.RemoveFilter(filter);
		}


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
					headerColumnWidth.Update(new GridLength(headerColumn.ActualWidth, GridUnitType.Pixel));
			}
		}


		// Reset all log filters.
		void ResetLogFilters()
		{
			this.logLevelFilterComboBox.SelectedIndex = 0;
			this.logProcessIdFilterTextBox.Value = null;
			this.logTextFilterTextBox.Object = null;
			this.logThreadIdFilterTextBox.Value = null;
			this.predefinedLogTextFilterListBox.SelectedItems.Clear();
			this.updateLogFiltersAction.Execute();
		}


		// Command to reset all log filters.
		ICommand ResetLogFiltersCommand { get; }


		// Restart application as administrator role.
		void RestartAsAdministrator()
		{
			if (this.canRestartAsAdmin.Value)
			{
				if (this.Application.IsDebugMode)
					this.Application.Restart($"{App.DebugArgument} {App.RestoreMainWindowsArgument}", true);
				else
					this.Application.Restart(App.RestoreMainWindowsArgument, true);
			}
		}


		// Command to restart application as administrator role.
		ICommand RestartAsAdministratorCommand { get; }


		// Command to save all logs to file.
		ICommand SaveAllLogsCommand { get; }


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
			var fileName = await new SaveFileDialog().Also(it =>
			{
				it.Filters!.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("txt");
					filter.Name = app.GetString("FileFormat.Text");
				}));
				it.Filters.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("log");
					filter.Name = app.GetString("FileFormat.Log");
				}));
				it.Filters.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = app.GetString("FileFormat.Json");
				}));
				it.Filters.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("*");
					filter.Name = app.GetString("FileFormat.All");
				}));
				it.Title = saveAllLogs
					? app.GetString("SessionView.SaveAllLogs")
					: app.GetString("SessionView.SaveLogs");
			}).ShowAsync(this.attachedWindow);
			this.isSelectingFileToSaveLogs = false;
			if (fileName == null)
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


		// Command to save logs to file.
		ICommand SaveLogsCommand { get; }


		// Select and set IP endpoint.
		async void SelectAndSetIPEndPoint()
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

			// select IP endpoint
			var endPoint = await new IPEndPointInputDialog()
			{
				InitialIPEndPoint = session.IPEndPoint,
				Title = this.Application.GetString("SessionView.SetIPEndPoint"),
			}.ShowDialog<IPEndPoint>(this.attachedWindow);
			if (endPoint == null)
				return;

			// check state
			if (!this.canSetIPEndPoint.Value)
				return;

			// set end point
			session.SetIPEndPointCommand.TryExecute(endPoint);
		}


		// Commandto select and set IP endpoint.
		ICommand SelectAndSetIPEndPointCommand { get; }


		/// <summary>
		/// Select and set log profile.
		/// </summary>
		public async void SelectAndSetLogProfile()
		{
			// check state
			this.VerifyAccess();
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to set log profile without attaching to window");
				return;
			}

			// select profile
			var logProfile = await new LogProfileSelectionDialog().ShowDialog<LogProfile>(this.attachedWindow);
			if (logProfile == null)
				return;

			// check state
			if (this.DataContext is not Session session)
				return;

			// check administrator role
			var isRestartingAsAdminNeeded = false;
			if (logProfile.IsAdministratorNeeded && !this.Application.IsRunningAsAdministrator)
			{
				if (await this.ConfirmRestartingAsAdmin(logProfile))
					isRestartingAsAdminNeeded = true;
				else
				{
					this.Logger.LogWarning($"Unable to use profile '{logProfile.Name}' because application is not running as administrator");
					return;
				}
			}

			// reset log filters
			this.ResetLogFilters();

			// reset log profile
			this.isIPEndPointNeededAfterLogProfileSet = false;
			this.isLogFileNeededAfterLogProfileSet = false;
			this.isRestartingAsAdminConfirmed = false;
			this.isUriNeededAfterLogProfileSet = false;
			this.isWorkingDirNeededAfterLogProfileSet = false;
			this.autoAddLogFilesAction.Cancel();
			this.autoSetUriAction.Cancel();
			this.autoSetWorkingDirectoryAction.Cancel();
			session.ResetLogProfileCommand.TryExecute(null);

			// set log profile
			if (!session.SetLogProfileCommand.TryExecute(logProfile))
			{
				this.Logger.LogError("Unable to set log profile to session");
				return;
			}
			if (session.IsIPEndPointNeeded)
			{
				this.isIPEndPointNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectIPEndPointWhenNeeded);
				if (this.canSetIPEndPoint.Value 
					&& this.isIPEndPointNeededAfterLogProfileSet
					&& this.isAttachedToLogicalTree)
				{
					this.autoSetIPEndPointAction.Reschedule(AutoAddLogFilesDelay);
				}
			}
			else if (session.IsLogFileNeeded)
			{
				this.isLogFileNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectLogFilesWhenNeeded);
				if (this.canAddLogFiles.Value 
					&& this.isLogFileNeededAfterLogProfileSet
					&& this.isAttachedToLogicalTree)
				{
					this.autoAddLogFilesAction.Reschedule(AutoAddLogFilesDelay);
				}
			}
			else if (session.IsUriNeeded)
			{
				this.isUriNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectUriWhenNeeded);
				if (this.canSetUri.Value 
					&& this.isUriNeededAfterLogProfileSet
					&& this.isAttachedToLogicalTree)
				{
					this.autoSetUriAction.Reschedule(AutoAddLogFilesDelay);
				}
			}
			else if (session.IsWorkingDirectoryNeeded)
			{
				this.isWorkingDirNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(SettingKeys.SelectWorkingDirectoryWhenNeeded);
				if (this.canSetWorkingDirectory.Value 
					&& this.isWorkingDirNeededAfterLogProfileSet
					&& this.isAttachedToLogicalTree)
				{
					this.autoSetWorkingDirectoryAction.Reschedule(AutoAddLogFilesDelay);
				}
			}
			this.OnLogProfileSet(logProfile);

			// restart as administrator role
			if (isRestartingAsAdminNeeded)
				this.RestartAsAdministrator();
		}


		// Command to set log profile.
		ICommand SelectAndSetLogProfileCommand { get; }


		// Select and set URI.
		async void SelectAndSetUri()
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

			// select URI
			var uri = await new UriInputDialog()
			{
				InitialUri = session.Uri,
				Title = this.Application.GetString("SessionView.SetUri"),
			}.ShowDialog<Uri>(this.attachedWindow);
			if (uri == null)
				return;

			// check state
			if (!this.canSetUri.Value)
				return;

			// set URI
			session.SetUriCommand.TryExecute(uri);
		}


		// Commandto select and set URI.
		ICommand SelectAndSetUriCommand { get; }


		// Select and set working directory.
		async void SelectAndSetWorkingDirectory()
		{
			// check state
			if (this.attachedWindow == null)
			{
				this.Logger.LogError("Unable to set working directory without attaching to window");
				return;
			}

			// cancel scheduled action
			this.autoSetWorkingDirectoryAction.Cancel();

			// select directory
			var directory = await new OpenFolderDialog()
			{
				Title = this.Application.GetString("SessionView.SetWorkingDirectory"),
			}.ShowAsync(this.attachedWindow);
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


		// Command to set working directory.
		ICommand SelectAndSetWorkingDirectoryCommand { get; }


		// Duration of selected logs.
		TimeSpan? SelectedLogsDuration { get => this.GetValue<TimeSpan?>(SelectedLogsDurationProperty); }


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


		// Select all marked logs.
		void SelectMarkedLogs()
		{
			if (this.DataContext is not Session session)
				return;
			if (!this.canSelectMarkedLogs.Value)
				return;
			var logs = session.Logs;
			this.logListBox.SelectedItems.Clear();
			foreach (var log in session.MarkedLogs)
			{
				if (logs.Contains(log))
					this.logListBox.SelectedItems.Add(log);
			}
			if (this.logListBox.SelectedItems.Count > 0)
				this.logListBox.ScrollIntoView(this.logListBox.SelectedItems[0].AsNonNull());
		}


		// Command to select marked logs.
		ICommand SelectMarkedLogsCommand { get; }


		// Select log by timestamp.
		async void SelectNearestLogByTimestamp()
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
			var timestamp = await new DateTimeSelectionDialog()
			{
				InitialDateTime = this.EarliestSelectedLogTimestamp ?? session.EarliestLogTimestamp,
				Message = this.Application.GetString("SessionView.SelectNearestLogByTimestamp.Message"),
				Title = this.Application.GetString("SessionView.SelectNearestLogByTimestamp.Title"),
			}.ShowDialog<DateTime?>(this.attachedWindow);
			if (timestamp == null)
				return;

			// clear selection
			this.logListBox.SelectedItems.Clear();

			// find and select log
			var log = session.FindNearestLog(timestamp.Value);
			if (log != null)
			{
				this.logListBox.SelectedItems.Add(log);
				this.logListBox.ScrollIntoView(log);
				this.logListBox.Focus();
				this.IsScrollingToLatestLogNeeded = false;
			}
        }


		// Show file in system file explorer.
		void ShowFileInExplorer()
		{
			// check state
			this.VerifyAccess();
			if (!this.canShowFileInExplorer.Value)
				return;

			// collect paths
			var comparer = IO.PathEqualityComparer.Default;
			var filePath = (string?)null;
			var dirPathSet = new HashSet<string>(comparer);
			foreach (DisplayableLog log in this.logListBox.SelectedItems)
			{
				var fileName = log.FileName;
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


		// Command to show file in system file explorer.
		ICommand ShowFileInExplorerCommand { get; }


		// Show log file action menu.
		void ShowLogFileActionMenu(Control anchor)
		{
			// select log file
			anchor.DataContext?.Let(it =>
				this.logFileListBox.SelectedItem = it);

			// show menu
			this.logFileActionMenu.Close();
			this.logFileActionMenu.PlacementTarget = anchor;
			this.logFileActionMenu.Open(anchor);
		}


		// Show single log file in system file manager.
		void ShowLogFileInExplorer(string filePath) => 
			Platform.OpenFileManager(filePath);


		// Show menu for saving logs.
		void ShowLogsSavingMenu() =>
			this.logsSavingMenu.Open(this.logsSavingButton);


		// Show full log string property.
		bool ShowLogStringProperty()
		{
			// check state
			if (this.logListBox.SelectedItems.Count != 1)
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


		// Command to show string log property.
		ICommand ShowLogStringPropertyCommand { get; }


		// Show next tutorial is available.
		bool ShowNextTutorial()
		{
			// check state
			if (this.areAllTutorialsShown)
				return false;
			var window = this.attachedWindow as CarinaStudio.AppSuite.Controls.Window;
			if (window == null 
				|| (window as MainWindow)?.AreInitialDialogsClosed == false
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
				if (App.CurrentOrNull?.IsFirstLaunch == false)
				{
					// no need to show this tutorial for upgrade case
					persistentState.SetValue<bool>(IsSelectingLogProfileToStartTutorialShownKey, true);
				}
				else
				{
					return window.ShowTutorial(new Tutorial().Also(it =>
					{
						it.Anchor = this.FindControl<Control>("selectAndSetLogProfileButton");
						it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.SelectLogProfileToStart"));
						it.Dismissed += (_, e) => 
						{
							persistentState.SetValue<bool>(IsSelectingLogProfileToStartTutorialShownKey, true);
							this.ShowNextTutorial();
						};
						it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
						it.SkippingAllTutorialRequested += (_, e) => this.SkipAllTutorials();
					}));
				}
			}

			// show "switch side panels"
			if (!persistentState.GetValueOrDefault(IsSwitchingSidePanelsTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("sidePanelBoolBarItemsPanel");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.SwitchSidePanels"));
					it.Dismissed += (_, e) => 
					{
						persistentState.SetValue<bool>(IsSwitchingSidePanelsTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, e) => this.SkipAllTutorials();
				}));
			}

			// show side panel tutorials
			if (!persistentState.GetValueOrDefault(IsMarkedLogsPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("markedLogsPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.MarkedLogsPanel"));
					it.Dismissed += (_, e) => 
					{
						persistentState.SetValue<bool>(IsMarkedLogsPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, e) => this.SkipAllTutorials();
				}));
			}
			if (!persistentState.GetValueOrDefault(IsTimestampCategoriesPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("timestampCategoriesPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.TimestampCategoriesPanel"));
					it.Dismissed += (_, e) => 
					{
						persistentState.SetValue<bool>(IsTimestampCategoriesPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, e) => this.SkipAllTutorials();
				}));
			}
			if (!persistentState.GetValueOrDefault(IsLogAnalysisPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("logAnalysisPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.LogAnalysisPanel"));
					it.Dismissed += (_, e) => 
					{
						persistentState.SetValue<bool>(IsLogAnalysisPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, e) => this.SkipAllTutorials();
				}));
			}
			if (!persistentState.GetValueOrDefault(IsLogFilesPanelTutorialShownKey))
			{
				return window.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = this.FindControl<Control>("logFilesPanelButton");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.LogFilesPanel"));
					it.Dismissed += (_, e) => 
					{
						persistentState.SetValue<bool>(IsLogFilesPanelTutorialShownKey, true);
						this.ShowNextTutorial();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.SkippingAllTutorialRequested += (_, e) => this.SkipAllTutorials();
				}));
			}

			// all tutorials shown
			this.SetAndRaise<bool>(AreAllTutorialsShownProperty, ref this.areAllTutorialsShown, true);
			return false;
		}


		// Show UI of other actions.
		void ShowOtherActions()
		{
			if (this.otherActionsMenu.PlacementTarget == null)
				this.otherActionsMenu.PlacementTarget = this.otherActionsButton;
			this.otherActionsMenu.Open(this);
		}


		// Show working directory actions menu.
		void ShowWorkingDirectoryActions()
        {
			if (this.workingDirectoryActionsMenu.PlacementTarget == null)
				this.workingDirectoryActionsMenu.PlacementTarget = this.workingDirectoryActionsButton;
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


		// Command to show working directory in system file explorer.
		ICommand ShowWorkingDirectoryInExplorerCommand { get; }


		// Skip all tutorials.
		void SkipAllTutorials()
		{
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
			this.SetAndRaise<bool>(AreAllTutorialsShownProperty, ref this.areAllTutorialsShown, true);
		}


		// Get current state of status bar.
		SessionViewStatusBarState StatusBarState { get => this.GetValue<SessionViewStatusBarState>(StatusBarStateProperty); }


		// Switch filters combination mode.
		void SwitchLogFiltersCombinationMode()
		{
			if (this.DataContext is not Session session)
				return;
			session.LogFiltersCombinationMode = session.LogFiltersCombinationMode switch
			{
				FilterCombinationMode.Intersection => FilterCombinationMode.Union,
				_ => FilterCombinationMode.Intersection,
			};
		}


		// Command to switch combination mode of log filters.
		ICommand SwitchLogFiltersCombinationModeCommand { get; }


		// Unmark logs.
		void UnmarkSelectedLogs()
		{
			if (!this.canUnmarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			session.UnmarkLogsCommand.TryExecute(this.logListBox.SelectedItems.Cast<DisplayableLog>().ToArray());
		}


		// Command to unmark selected logs.
		ICommand UnmarkSelectedLogsCommand { get; }


		// Update CanFilterLogsByNonTextFilters property.
		void UpdateCanFilterLogsByNonTextFilters()
		{
			this.SetValue<bool>(CanFilterLogsByNonTextFiltersProperty, this.validLogLevels.Count > 1 || this.isPidLogPropertyVisible || this.isTidLogPropertyVisible);
		}


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
			else if (logProfile != null && logProfile.IsContinuousReading)
			{
				if (logProfile.SortDirection == SortDirection.Ascending)
				{
					if (userScrollingDelta > 0 && ((logScrollViewer.Offset.Y + logScrollViewer.Viewport.Height) / (double)logScrollViewer.Extent.Height) >= 0.999)
					{
						this.Logger.LogDebug("Start auto scrolling because of user scrolling down");
						this.IsScrollingToLatestLogNeeded = true;
					}
				}
				else
				{
					if (userScrollingDelta < 0 && (logScrollViewer.Offset.Y / (double)logScrollViewer.Extent.Height) <= 0.001)
					{
						this.Logger.LogDebug("Start auto scrolling because of user scrolling up");
						this.IsScrollingToLatestLogNeeded = true;
					}
				}
			}
		}


		// Get delay of updating log analysis.
		int UpdateLogAnalysisParamsDelay { get => 500; }


		// Update font family of log.
		void UpdateLogFontFamily()
		{
			var name = this.Settings.GetValueOrDefault(SettingKeys.LogFontFamily);
			if (string.IsNullOrEmpty(name))
				name = SettingKeys.DefaultLogFontFamily;
			this.SetValue<FontFamily>(LogFontFamilyProperty, new FontFamily(name));
		}


		// Update font size of log.
		void UpdateLogFontSize()
		{
			var size = Math.Max(Math.Min(this.Settings.GetValueOrDefault(SettingKeys.LogFontSize), SettingKeys.MaxLogFontSize), SettingKeys.MinLogFontSize);
			this.SetValue<double>(LogFontSizeProperty, size);
		}


		// Get delay of updating log filter.
		int UpdateLogFilterParamsDelay { get => Math.Max(SettingKeys.MinUpdateLogFilterDelay, Math.Min(SettingKeys.MaxUpdateLogFilterDelay, this.Settings.GetValueOrDefault(SettingKeys.UpdateLogFilterDelay))); }


		// LIst of log levels defined by log profile.
		IList<Logs.LogLevel> ValidLogLevels { get; }
	}
}
