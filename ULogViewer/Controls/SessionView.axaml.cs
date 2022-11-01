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
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
		const int ScrollingToLatestLogInterval = 200;


		// Static fields.
		static readonly Regex BaseNameRegex = new("^(?<Name>.+)\\s+\\(\\d+\\)\\s*$");
		static readonly AvaloniaProperty<bool> CanFilterLogsByNonTextFiltersProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(CanFilterLogsByNonTextFilters), false);
		static readonly AvaloniaProperty<bool> EnableRunningScriptProperty = AvaloniaProperty.Register<SessionView, bool>("EnableRunningScript", false);
		static readonly AvaloniaProperty<bool> HasLogProfileProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(HasLogProfile), false);
		static readonly SettingKey<bool> IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey = new("SessionView.IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShown");
		static readonly AvaloniaProperty<bool> IsProcessInfoVisibleProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsProcessInfoVisible), false);
		static readonly AvaloniaProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.RegisterDirect<SessionView, bool>("IsProVersionActivated", c => c.isProVersionActivated);
		static readonly AvaloniaProperty<bool> IsScrollingToLatestLogNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogNeeded), true);
		static readonly AvaloniaProperty<bool> IsScrollingToLatestLogAnalysisResultNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogAnalysisResultNeeded), true);
		static readonly SettingKey<bool> IsCopyLogTextTutorialShownKey = new("SessionView.IsCopyLogTextTutorialShown");
		static readonly SettingKey<bool> IsLogAnalysisPanelTutorialShownKey = new("SessionView.IsLogAnalysisPanelTutorialShown");
		static readonly SettingKey<bool> IsLogFilesPanelTutorialShownKey = new("SessionView.IsLogFilesPanelTutorialShown");
		static readonly SettingKey<bool> IsMarkedLogsPanelTutorialShownKey = new("SessionView.IsMarkedLogsPanelTutorialShown");
		static readonly SettingKey<bool> IsSelectLogAnalysisRuleSetsTutorialShownKey = new("SessionView.IsSelectLogAnalysisRuleSetsTutorialShown");
		static readonly SettingKey<bool> IsSelectingLogProfileToStartTutorialShownKey = new("SessionView.IsSelectingLogProfileToStartTutorialShown");
		static readonly SettingKey<bool> IsShowAllLogsForLogAnalysisResultTutorialShownKey = new("SessionView.IsShowAllLogsForLogAnalysisResultTutorialShown");
		static readonly SettingKey<bool> IsSwitchingSidePanelsTutorialShownKey = new("SessionView.IsSwitchingSidePanelsTutorialShown");
		static readonly SettingKey<bool> IsTimestampCategoriesPanelTutorialShownKey = new("SessionView.IsTimestampCategoriesPanelTutorialShown");
		static readonly AvaloniaProperty<FontFamily> LogFontFamilyProperty = AvaloniaProperty.Register<SessionView, FontFamily>(nameof(LogFontFamily));
		static readonly AvaloniaProperty<double> LogFontSizeProperty = AvaloniaProperty.Register<SessionView, double>(nameof(LogFontSize), 10.0);
		static readonly AvaloniaProperty<int> MaxDisplayLineCountForEachLogProperty = AvaloniaProperty.Register<SessionView, int>(nameof(MaxDisplayLineCountForEachLog), 1);
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
		readonly MutableObservableBoolean canCopyLogText = new();
		readonly MutableObservableBoolean canEditLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canFilterByLogProperty = new MutableObservableBoolean();
		readonly MutableObservableBoolean canMarkSelectedLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canMarkUnmarkSelectedLogs = new MutableObservableBoolean();
		readonly ObservableCommandState canReloadLogs = new();
		readonly ObservableCommandState canResetLogProfileToSession = new();
		readonly MutableObservableBoolean canRestartAsAdmin = new MutableObservableBoolean(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !App.Current.IsRunningAsAdministrator);
		readonly ObservableCommandState canSaveLogs = new();
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
		readonly ToggleButton createLogAnalysisRuleSetButton;
		readonly ContextMenu createLogAnalysisRuleSetMenu;
		readonly Border dragDropReceiverBorder;
		readonly MenuItem filterByLogPropertyMenuItem;
		IDisposable? hasDialogsObserverToken;
		IDisposable? isActiveObserverToken;
		bool isAltKeyPressed;
		bool isAttachedToLogicalTree;
		bool isIPEndPointNeededAfterLogProfileSet;
		bool isLogFileNeededAfterLogProfileSet;
		bool isPointerPressedOnLogAnalysisResultListBox;
		bool isPointerPressedOnLogListBox;
		bool isProVersionActivated;
		bool isRestartingAsAdminConfirmed;
		bool isSelectingFileToSaveLogs;
		bool isUriNeededAfterLogProfileSet;
		bool isWorkingDirNeededAfterLogProfileSet;
		bool keepSidePanelVisible;
		readonly Avalonia.Controls.ListBox keyLogAnalysisRuleSetListBox;
		Control? lastClickedLogPropertyView;
		readonly ContextMenu logActionMenu;
		IDisposable logAnalysisPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly Avalonia.Controls.ListBox logAnalysisResultListBox;
		ScrollViewer? logAnalysisResultScrollViewer;
		readonly ToggleButton logAnalysisRuleSetsButton;
		readonly Popup logAnalysisRuleSetsPopup;
		readonly Avalonia.Controls.ListBox logAnalysisScriptSetListBox;
		readonly ContextMenu logFileActionMenu;
		readonly AppSuite.Controls.ListBox logFileListBox;
		IDisposable logFilesPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly List<ColumnDefinition> logHeaderColumns = new List<ColumnDefinition>();
		readonly Control logHeaderContainer;
		readonly Grid logHeaderGrid;
		readonly List<MutableObservableValue<GridLength>> logHeaderWidths = new List<MutableObservableValue<GridLength>>();
		readonly ComboBox logLevelFilterComboBox;
		readonly Avalonia.Controls.ListBox logListBox;
		readonly Panel logListBoxContainer;
		readonly ContextMenu logMarkingMenu;
		readonly IntegerTextBox logProcessIdFilterTextBox;
		readonly ContextMenu logProfileSelectionMenu;
		readonly ToggleButton logsSavingButton;
		readonly ContextMenu logsSavingMenu;
		ScrollViewer? logScrollViewer;
		readonly RegexTextBox logTextFilterTextBox;
		readonly IntegerTextBox logThreadIdFilterTextBox;
		readonly Avalonia.Controls.ListBox markedLogListBox;
		IDisposable markedLogsPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly double minLogListBoxSizeToCloseSidePanel;
		readonly Avalonia.Controls.ListBox operationCountingAnalysisRuleSetListBox;
		readonly Avalonia.Controls.ListBox operationDurationAnalysisRuleSetListBox;
		readonly ToggleButton otherActionsButton;
		readonly ContextMenu otherActionsMenu;
		readonly Avalonia.Controls.ListBox predefinedLogTextFilterListBox;
		readonly SortedObservableList<PredefinedLogTextFilter> predefinedLogTextFilters;
		readonly ToggleButton predefinedLogTextFiltersButton;
		readonly Popup predefinedLogTextFiltersPopup;
		readonly HashSet<Avalonia.Input.Key> pressedKeys = new HashSet<Avalonia.Input.Key>();
		readonly ScheduledAction scrollToLatestLogAction;
		readonly ScheduledAction scrollToLatestLogAnalysisResultAction;
		readonly ToggleButton selectAndSetLogProfileDropDownButton;
		readonly HashSet<KeyLogAnalysisRuleSet> selectedKeyLogAnalysisRuleSets = new();
		readonly HashSet<LogAnalysisScriptSet> selectedLogAnalysisScriptSets = new();
		readonly HashSet<OperationCountingAnalysisRuleSet> selectedOperationCountingAnalysisRuleSets = new();
		readonly HashSet<OperationDurationAnalysisRuleSet> selectedOperationDurationAnalysisRuleSets = new();
		readonly HashSet<PredefinedLogTextFilter> selectedPredefinedLogTextFilters = new();
		readonly MenuItem showLogPropertyMenuItem;
		readonly ColumnDefinition sidePanelColumn;
		readonly Control sidePanelContainer;
		readonly AppSuite.Controls.ListBox timestampCategoryListBox;
		IDisposable timestampCategoryPanelVisibilityObserverToken = EmptyDisposable.Default;
		readonly ToolBarScrollViewer toolBarScrollViewer;
		readonly ScheduledAction updateLogAnalysisAction;
		readonly ScheduledAction updateLogFiltersAction;
		readonly ScheduledAction updateLogHeaderContainerMarginAction;
		readonly ScheduledAction updateStatusBarStateAction;
		readonly SortedObservableList<Logs.LogLevel> validLogLevels = new SortedObservableList<Logs.LogLevel>((x, y) => (int)x - (int)y);
		readonly ToggleButton workingDirectoryActionsButton;
		readonly ContextMenu workingDirectoryActionsMenu;


		// Static initializer.
		static SessionView()
		{
			App.Current.PersistentState.SetValue<bool>(IsLogAnalysisPanelTutorialShownKey, true);
			App.Current.PersistentState.SetValue<bool>(IsLogFilesPanelTutorialShownKey, true);
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
			this.CopyLogTextCommand = new Command(this.CopyLogText, this.canCopyLogText);
			this.EditLogProfileCommand = new Command(this.EditLogProfile, this.canEditLogProfile);
			this.FilterByLogPropertyCommand = new Command(this.FilterByLogProperty, this.canFilterByLogProperty);
			this.MarkSelectedLogsCommand = new Command<MarkColor>(this.MarkSelectedLogs, this.canMarkSelectedLogs);
			this.MarkUnmarkSelectedLogsCommand = new Command(this.MarkUnmarkSelectedLogs, this.canMarkUnmarkSelectedLogs);
			this.ReloadLogsCommand = new Command(this.ReloadLogs, this.canReloadLogs);
			this.RestartAsAdministratorCommand = new Command(this.RestartAsAdministrator, this.canRestartAsAdmin);
			this.SaveAllLogsCommand = new Command(() => this.SaveLogs(true), this.canSaveLogs);
			this.SaveLogsCommand = new Command(() => this.SaveLogs(false), this.canSaveLogs);
			this.SelectAndSetIPEndPointCommand = new Command(this.SelectAndSetIPEndPoint, this.canSetIPEndPoint);
			this.SelectAndSetLogProfileCommand = new Command(this.SelectAndSetLogProfileAsync, this.canSetLogProfile);
			this.SelectAndSetUriCommand = new Command(this.SelectAndSetUri, this.canSetUri);
			this.SelectAndSetWorkingDirectoryCommand = new Command(this.SelectAndSetWorkingDirectory, this.canSetWorkingDirectory);
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
			this.ValidLogLevels = ListExtensions.AsReadOnly(this.validLogLevels);

			// initialize
			this.IsToolsMenuItemVisible = this.Application.IsDebugMode || AppSuite.Controls.PathEnvVarEditorDialog.IsSupported;
			AvaloniaXamlLoader.Load(this);

			// load resources
			if (this.Application.TryGetResource<double>("Double/SessionView.LogListBox.MinSizeToCloseSidePanel", out var doubleRes))
				this.minLogListBoxSizeToCloseSidePanel = doubleRes.GetValueOrDefault();

			// setup containers
			this.logListBoxContainer = this.Get<Panel>(nameof(logListBoxContainer)).Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.autoCloseSidePanelAction?.Schedule());
			});
			var toolBarContainer = this.Get<Control>("toolBarContainer").Also(it =>
			{
				it.AddHandler(Control.PointerReleasedEvent, this.OnToolBarPointerReleased, RoutingStrategies.Tunnel);
			});

			// setup controls
			this.copyLogPropertyMenuItem = this.Get<MenuItem>(nameof(copyLogPropertyMenuItem));
			this.createLogAnalysisRuleSetButton = this.Get<ToggleButton>(nameof(createLogAnalysisRuleSetButton));
			this.createLogAnalysisRuleSetMenu = ((ContextMenu)this.Resources[nameof(createLogAnalysisRuleSetMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.createLogAnalysisRuleSetButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.createLogAnalysisRuleSetButton, false);
					this.createLogAnalysisRuleSetButton.IsChecked = true;
				});
			});
			this.dragDropReceiverBorder = this.Get<Border>(nameof(dragDropReceiverBorder));
			this.filterByLogPropertyMenuItem = this.Get<MenuItem>(nameof(filterByLogPropertyMenuItem));
			this.keyLogAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(keyLogAnalysisRuleSetListBox)).Also(it =>
			{
				it.SelectionChanged += this.OnLogAnalysisRuleSetListBoxSelectionChanged;
			});
			this.logActionMenu = ((ContextMenu)this.Resources[nameof(logActionMenu)].AsNonNull()).Also(it =>
			{
				it.MenuOpened += (_, e) =>
				{
					this.IsScrollingToLatestLogNeeded = false;
					if (this.showLogPropertyMenuItem == null)
						return;
					var log = this.logListBox!.SelectedItems.Count == 1 
						? (this.logListBox.SelectedItems[0] as DisplayableLog)
						: null;
					if (log != null && this.lastClickedLogPropertyView?.Tag is DisplayableLogProperty property)
					{
						var propertyValue = DisplayableLog.HasStringProperty(property.Name) 
							&& !property.Name.EndsWith("String")
							&& log.TryGetProperty<string?>(property.Name, out var s)
								? s
								: null;
						if (propertyValue == null)
							propertyValue = property.DisplayName;
						else if (propertyValue.Length > 16)
							propertyValue = $"{propertyValue.Substring(0, 16)}…";
						this.copyLogPropertyMenuItem.Header = this.Application.GetFormattedString("SessionView.CopyLogProperty", property.DisplayName);
						this.filterByLogPropertyMenuItem.Header = this.Application.GetFormattedString("SessionView.FilterByLogProperty", propertyValue);
						this.showLogPropertyMenuItem.Header = this.Application.GetFormattedString("SessionView.ShowLogProperty", property.DisplayName);
					}
					else
					{
						this.copyLogPropertyMenuItem.Header = this.Application.GetString("SessionView.CopyLogProperty.Disabled");
						this.filterByLogPropertyMenuItem.Header = this.Application.GetString("SessionView.FilterByLogProperty.Disabled");
						this.showLogPropertyMenuItem.Header = this.Application.GetString("SessionView.ShowLogProperty.Disabled");
					}
				};
			});
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
				it.GetObservable(Control.IsVisibleProperty).Subscribe(isVisible =>
				{
					if (isVisible)
						this.SynchronizationContext.Post(() => this.ShowLogAnalysisRuleSetsTutorial());
				});
			});
			this.logAnalysisRuleSetsPopup = this.Get<Popup>(nameof(logAnalysisRuleSetsPopup)).Also(it =>
			{
				it.Closed += (_, e) => this.logListBox?.Focus();
				it.Opened += (_, e) => 
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
			this.logAnalysisScriptSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(logAnalysisScriptSetListBox)).Also(it =>
			{
				it.SelectionChanged += this.OnLogAnalysisRuleSetListBoxSelectionChanged;
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
			this.logFileListBox = this.Get<AppSuite.Controls.ListBox>(nameof(logFileListBox));
			this.logHeaderContainer = this.Get<Control>(nameof(logHeaderContainer));
			this.logHeaderGrid = this.Get<Grid>(nameof(logHeaderGrid)).Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ => this.ReportLogHeaderColumnWidths());
			});
			this.logLevelFilterComboBox = this.Get<ComboBox>(nameof(logLevelFilterComboBox)).Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logListBox = this.logListBoxContainer.FindControl<Avalonia.Controls.ListBox>(nameof(logListBox))!.Also(it =>
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
			this.logProfileSelectionMenu = ((LogProfileSelectionContextMenu)this.Resources[nameof(logProfileSelectionMenu)].AsNonNull()).Also(it =>
			{
				it.LogProfileSelected += async (_, logProfile) => 
				{
					await this.SetLogProfileAsync(logProfile);
				};
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.selectAndSetLogProfileDropDownButton!.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() =>
				{
					ToolTip.SetIsOpen(this.selectAndSetLogProfileDropDownButton!, false);
					this.selectAndSetLogProfileDropDownButton!.IsChecked = true;
				});
			});
			this.logProcessIdFilterTextBox = toolBarContainer.FindControl<IntegerTextBox>(nameof(logProcessIdFilterTextBox))!.Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logsSavingButton = this.Get<ToggleButton>(nameof(logsSavingButton));
			this.logsSavingMenu = ((ContextMenu)this.Resources[nameof(logsSavingMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.logsSavingButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.logsSavingButton, false);
					this.logsSavingButton.IsChecked = true;
				});
			});
			this.logTextFilterTextBox = this.Get<RegexTextBox>(nameof(logTextFilterTextBox)).Also(it =>
			{
				it.ValidationDelay = this.UpdateLogFilterParamsDelay;
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.logThreadIdFilterTextBox = toolBarContainer.FindControl<IntegerTextBox>(nameof(logThreadIdFilterTextBox)).AsNonNull().Also(it =>
			{
				if (Platform.IsMacOS)
					(this.Application as AppSuite.AppSuiteApplication)?.EnsureClosingToolTipIfWindowIsInactive(it);
			});
			this.markedLogListBox = this.Get<Avalonia.Controls.ListBox>(nameof(markedLogListBox));
			this.operationCountingAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(operationCountingAnalysisRuleSetListBox)).Also(it =>
			{
				it.SelectionChanged += this.OnLogAnalysisRuleSetListBoxSelectionChanged;
			});
			this.operationDurationAnalysisRuleSetListBox = this.Get<Avalonia.Controls.ListBox>(nameof(operationDurationAnalysisRuleSetListBox)).Also(it =>
			{
				it.SelectionChanged += this.OnLogAnalysisRuleSetListBoxSelectionChanged;
			});
			this.otherActionsButton = this.Get<ToggleButton>(nameof(otherActionsButton));
			this.otherActionsMenu = ((ContextMenu)this.Resources[nameof(otherActionsMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.otherActionsButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.otherActionsButton, false);
					this.otherActionsButton.IsChecked = true;
				});
			});
			this.predefinedLogTextFilterListBox = this.Get<Avalonia.Controls.ListBox>(nameof(predefinedLogTextFilterListBox));
			this.predefinedLogTextFiltersButton = this.Get<ToggleButton>(nameof(predefinedLogTextFiltersButton));
			this.predefinedLogTextFiltersPopup = this.Get<Popup>(nameof(predefinedLogTextFiltersPopup)).Also(it =>
			{
				it.Closed += (_, sender) => this.logListBox.Focus();
				it.Opened += (_, sender) => this.SynchronizationContext.Post(() =>
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
			this.showLogPropertyMenuItem = this.Get<MenuItem>(nameof(showLogPropertyMenuItem));
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
			this.timestampCategoryListBox = this.Get<AppSuite.Controls.ListBox>(nameof(timestampCategoryListBox)).Also(it =>
			{
				it.GetObservable(Avalonia.Controls.ListBox.SelectedItemProperty).Subscribe(item =>
					this.OnLogCategoryListBoxSelectedItemChanged(it, item as DisplayableLogCategory));
			});
			this.toolBarScrollViewer = this.Get<ToolBarScrollViewer>(nameof(toolBarScrollViewer));
			this.workingDirectoryActionsButton = this.Get<ToggleButton>(nameof(workingDirectoryActionsButton));
			this.workingDirectoryActionsMenu = ((ContextMenu)this.Resources[nameof(workingDirectoryActionsMenu)].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.workingDirectoryActionsButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => 
				{
					ToolTip.SetIsOpen(this.workingDirectoryActionsButton, false);
					this.workingDirectoryActionsButton.IsChecked = true;
				});
			});

			// find menu items
			var toolsMenuItem = this.otherActionsMenu.Items.Let(it =>
			{
				foreach (var item in it)
				{
					if (item is MenuItem menuItem && menuItem.Name == "toolsMenuItem")
						return menuItem;
				}
				return null;
			}).AsNonNull();

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
					session.LogAnalysis.IsPanelVisible = false;
					session.IsLogFilesPanelVisible = false;
					session.IsMarkedLogsPanelVisible = false;
					session.LogCategorizing.IsTimestampCategoriesPanelVisible = false;
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

				// scroll to latest log
				this.logScrollViewer?.Let(scrollViewer =>
				{
					var extent = scrollViewer.Extent;
					var viewport = scrollViewer.Viewport;
					if (extent.Height > viewport.Height)
					{
						var currentOffset = scrollViewer.Offset;
						var newOffset = session.LogProfile.SortDirection == SortDirection.Ascending
							? new Point(currentOffset.X, extent.Height - viewport.Height)
							: new Point(currentOffset.X, 0);
						scrollViewer.Offset = newOffset;
					}
				});
				this.scrollToLatestLogAction!.Schedule(ScrollingToLatestLogInterval);
			});
			this.scrollToLatestLogAnalysisResultAction = new(() =>
			{
				// check state
				if (!this.IsScrollingToLatestLogAnalysisResultNeeded)
					return;
				if (this.DataContext is not Session session)
					return;
				if (session.LogAnalysis.AnalysisResults.IsEmpty() || session.LogProfile == null || !session.IsActivated)
					return;
				
				// cancel scrolling
				if (this.logAnalysisResultListBox.ContextMenu?.IsOpen == true || this.logMarkingMenu.IsOpen)
				{
					this.IsScrollingToLatestLogAnalysisResultNeeded = false;
					return;
				}

				// scroll to latest analysis result
				this.logAnalysisResultScrollViewer?.Let(scrollViewer =>
				{
					var extent = scrollViewer.Extent;
					var viewport = scrollViewer.Viewport;
					if (extent.Height > viewport.Height)
					{
						var currentOffset = scrollViewer.Offset;
						scrollViewer.Offset = new Point(currentOffset.X, extent.Height - viewport.Height);
					}
				});
				this.scrollToLatestLogAnalysisResultAction!.Schedule(ScrollingToLatestLogInterval);
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
			this.updateLogFiltersAction = new ScheduledAction(() =>
			{
				// get session
				if (this.DataContext is not Session session)
					return;

				// update text filters
				session.LogFiltering.PredefinedTextFilters.Clear();
				session.LogFiltering.PredefinedTextFilters.AddAll(this.selectedPredefinedLogTextFilters);
			});
			this.updateLogHeaderContainerMarginAction = new(() =>
			{
				var logScrollViewer = this.logScrollViewer;
				if (logScrollViewer != null)
					this.logHeaderContainer.Margin = new Thickness(-logScrollViewer.Offset.X, 0, Math.Min(0, logScrollViewer.Offset.X + logScrollViewer.Viewport.Width - logScrollViewer.Extent.Width), 0);
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
			(session.LogAnalysis.AnalysisResults as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnLogAnalysisResultsChanged);
			(session.LogAnalysis.KeyLogAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
			(session.LogAnalysis.LogAnalysisScriptSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
			(session.LogAnalysis.OperationCountingAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
			(session.LogAnalysis.OperationDurationAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
			
			// attach to properties
			var isAttaching = true;
			this.logAnalysisPanelVisibilityObserverToken = session.LogAnalysis.GetValueAsObservable(LogAnalysisViewModel.IsPanelVisibleProperty).Subscribe(isVisible =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(LogAnalysisViewModel.IsPanelVisibleProperty);
			});
			this.logFilesPanelVisibilityObserverToken = session.GetValueAsObservable(Session.IsLogFilesPanelVisibleProperty).Subscribe(isVisible =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(Session.IsLogFilesPanelVisibleProperty);
			});
			this.markedLogsPanelVisibilityObserverToken = session.GetValueAsObservable(Session.IsMarkedLogsPanelVisibleProperty).Subscribe(isVisible =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(Session.IsMarkedLogsPanelVisibleProperty);
			});
			this.timestampCategoryPanelVisibilityObserverToken = session.LogCategorizing.GetValueAsObservable(LogCategorizingViewModel.IsTimestampCategoriesPanelVisibleProperty).Subscribe(isVisible =>
			{
				if (!isAttaching)
					this.UpdateSidePanelState(LogCategorizingViewModel.IsTimestampCategoriesPanelVisibleProperty);
			});
			isAttaching = false;

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
			if (session.LogAnalysis.AnalysisResults.IsNotEmpty() && this.IsScrollingToLatestLogAnalysisResultNeeded)
				this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);

			// sync log filters to UI
			if (session.LogFiltering.PredefinedTextFilters.IsNotEmpty())
			{
				this.SynchronizationContext.Post(() =>
				{
					foreach (var textFilter in session.LogFiltering.PredefinedTextFilters)
					{
						this.predefinedLogTextFilterListBox.SelectedItems.Add(textFilter);
						this.selectedPredefinedLogTextFilters.Add(textFilter);
					}
					this.updateLogFiltersAction.Cancel();
				});
			}
			this.updateLogFiltersAction.Cancel();

			// sync log analysis rule sets to UI
			this.keyLogAnalysisRuleSetListBox.SelectedItems.Let(it =>
			{
				it.Clear();
				foreach (var ruleSet in session.LogAnalysis.KeyLogAnalysisRuleSets)
					it.Add(ruleSet);
			});
			this.logAnalysisScriptSetListBox.SelectedItems.Let(it =>
			{
				it.Clear();
				foreach (var scriptSet in session.LogAnalysis.LogAnalysisScriptSets)
					it.Add(scriptSet);
			});
			this.operationCountingAnalysisRuleSetListBox.SelectedItems.Let(it =>
			{
				it.Clear();
				foreach (var ruleSet in session.LogAnalysis.OperationCountingAnalysisRuleSets)
					it.Add(ruleSet);
			});
			this.operationDurationAnalysisRuleSetListBox.SelectedItems.Let(it =>
			{
				it.Clear();
				foreach (var ruleSet in session.LogAnalysis.OperationDurationAnalysisRuleSets)
					it.Add(ruleSet);
			});
			this.updateLogAnalysisAction.Cancel();

			// sync side panel state
			if (session.LogAnalysis.IsPanelVisible 
				|| session.IsMarkedLogsPanelVisible 
				|| session.IsLogFilesPanelVisible
				|| session.LogCategorizing.IsTimestampCategoriesPanelVisible)
			{
				sidePanelColumn.Width = new GridLength(new double[] {
					session.LogAnalysis.PanelSize,
					session.LogFilesPanelSize,
					session.MarkedLogsPanelSize,
					session.LogCategorizing.TimestampCategoriesPanelSize,
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


		// Clear log analysis rule set selection.
		void ClearLogAnalysisRuleSetSelection()
		{
			this.keyLogAnalysisRuleSetListBox.SelectedItems.Clear();
			this.logAnalysisScriptSetListBox.SelectedItems.Clear();
			this.operationCountingAnalysisRuleSetListBox.SelectedItems.Clear();
			this.operationDurationAnalysisRuleSetListBox.SelectedItems.Clear();
			this.updateLogAnalysisAction.Reschedule();
			this.IsScrollingToLatestLogAnalysisResultNeeded = true;
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
			var result = string.Compare(x.Name, y.Name, true, CultureInfo.InvariantCulture);
			if (result != 0)
				return result;
			return x.GetHashCode() - y.GetHashCode();
		}


		// Show UI to confirm removing log analysis rule set.
		async Task<bool> ConfirmRemovingLogAnalysisRuleSetAsync(string? ruleSetName, bool isScriptSet)
		{
			if (this.attachedWindow == null)
				return false;
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				Icon = MessageDialogIcon.Question,
				Message = isScriptSet
					? new FormattedString().Also(it =>
					{
						it.Arg1 = ruleSetName;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/SessionView.ConfirmRemovingLogAnalysisScriptSet"));
					})
					: new FormattedString().Also(it =>
					{
						it.Arg1 = ruleSetName;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/SessionView.ConfirmRemovingLogAnalysisRuleSet"));
					}),
			}.ShowDialog(this.attachedWindow);
			return (result == MessageDialogResult.Yes);
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
				Message = new FormattedString().Also(it =>
				{
					it.Bind(FormattedString.Arg1Property, new Binding() { Path = nameof(LogProfile.Name), Source = profile });
					it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/SessionView.NeedToRestartAsAdministrator"));
				}),
			}.ShowDialog(this.attachedWindow);
			if (result == MessageDialogResult.Yes)
			{
				this.Logger.LogWarning($"User agreed to restart as administrator for '{profile.Name}'");
				return true;
			}
			this.Logger.LogWarning($"User denied to restart as administrator for '{profile.Name}'");
			return false;
		}


		// Copy selected log analysis rule set.
		void CopyKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet ruleSet)
		{
			if (this.attachedWindow == null)
				return;
			var baseName = BaseNameRegex.Match(ruleSet.Name ?? "").Let(it =>
				it.Success ? it.Groups["Name"].Value : ruleSet.Name ?? "");
			var newName = baseName;
			for (var n = 2; n <= 10; ++n)
			{
				var candidateName = $"{baseName} ({n})";
				if (KeyLogAnalysisRuleSetManager.Default.RuleSets.FirstOrDefault(it => it.Name == candidateName) == null)
				{
					newName = candidateName;
					break;
				}
			}
			var newRuleSet = new KeyLogAnalysisRuleSet(ruleSet, newName);
			KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, newRuleSet);
		}


		// Copy selected log analysis script set.
		void CopyLogAnalysisScriptSet(LogAnalysisScriptSet scriptSet)
		{
			if (this.attachedWindow == null)
				return;
			var baseName = BaseNameRegex.Match(scriptSet.Name ?? "").Let(it =>
				it.Success ? it.Groups["Name"].Value : scriptSet.Name ?? "");
			var newName = baseName;
			for (var n = 2; n <= 10; ++n)
			{
				var candidateName = $"{baseName} ({n})";
				if (LogAnalysisScriptSetManager.Default.ScriptSets.FirstOrDefault(it => it.Name == candidateName) == null)
				{
					newName = candidateName;
					break;
				}
			}
			var newScriptSet = new LogAnalysisScriptSet(scriptSet, newName);
			LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, newScriptSet);
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


		// Copy text value of selected log.
		void CopyLogText()
		{
			// check state
			if (this.logListBox.SelectedItems.Count != 1)
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
					Message = this.GetResourceObservable("String/SessionView.Tutorial.CopyLogText"),
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
				_ = App.Current.Clipboard?.SetTextAsync(textBuffer.ToString());
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Failed to set text to clipboard");
			}
		}


		// Command to copy log text.
		ICommand CopyLogTextCommand { get; }


		// Copy selected log analysis rule set.
		void CopyOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet ruleSet)
		{
			if (this.attachedWindow == null)
				return;
			var baseName = BaseNameRegex.Match(ruleSet.Name ?? "").Let(it =>
				it.Success ? it.Groups["Name"].Value : ruleSet.Name ?? "");
			var newName = baseName;
			for (var n = 2; n <= 10; ++n)
			{
				var candidateName = $"{baseName} ({n})";
				if (OperationCountingAnalysisRuleSetManager.Default.RuleSets.FirstOrDefault(it => it.Name == candidateName) == null)
				{
					newName = candidateName;
					break;
				}
			}
			var newRuleSet = new OperationCountingAnalysisRuleSet(ruleSet, newName);
			OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, newRuleSet);
		}


		// Copy selected log analysis rule set.
		void CopyOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet ruleSet)
		{
			if (this.attachedWindow == null)
				return;
			var baseName = BaseNameRegex.Match(ruleSet.Name ?? "").Let(it =>
				it.Success ? it.Groups["Name"].Value : ruleSet.Name ?? "");
			var newName = baseName;
			for (var n = 2; n <= 10; ++n)
			{
				var candidateName = $"{baseName} ({n})";
				if (OperationDurationAnalysisRuleSetManager.Default.RuleSets.FirstOrDefault(it => it.Name == candidateName) == null)
				{
					newName = candidateName;
					break;
				}
			}
			var newRuleSet = new OperationDurationAnalysisRuleSet(ruleSet, newName);
			OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, newRuleSet);
		}


		// Copy selected predefined log text filter.
		void CopyPredefinedLogTextFilter(PredefinedLogTextFilter filter)
		{
			if (this.attachedWindow == null)
				return;
			var baseName = BaseNameRegex.Match(filter.Name ?? "").Let(it =>
				it.Success ? it.Groups["Name"].Value : filter.Name ?? "");
			var newName = baseName;
			for (var n = 2; n <= 10; ++n)
			{
				var candidateName = $"{baseName} ({n})";
				if (PredefinedLogTextFilterManager.Default.Filters.FirstOrDefault(it => it.Name == candidateName) == null)
				{
					newName = candidateName;
					break;
				}
			}
			var newFilter = new PredefinedLogTextFilter(this.Application, newName, filter.Regex);
			PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, newFilter, null);
		}


		// Create new key log analysis rule set.
		void CreateKeyLogAnalysisRuleSet()
		{
			if (this.attachedWindow != null)
				KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
		}


		// Create new log analysis rule set.
		void CreateLogAnalysisRuleSet() =>
			this.createLogAnalysisRuleSetMenu.Open(this.createLogAnalysisRuleSetButton);
		

		// Create new log analysis script set.
		void CreateLogAnalysisScriptSet()
		{
			if (this.attachedWindow != null)
				LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, null);
		}


		// Create item template for item of log list box.
		DataTemplate CreateLogItemTemplate(LogProfile profile, IList<DisplayableLogProperty> logProperties)
		{
			var app = (App)this.Application;
			var logPropertyCount = logProperties.Count;
			var colorIndicatorBorderBrush = app.FindResourceOrDefault<IBrush?>("Brush/WorkingArea.Background");
			var colorIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.ColorIndicator.Border");
			var colorIndicatorWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.ColorIndicator.Width");
			var analysisResultIndicatorSize = 0.0;
			var analysisResultIndicatorMargin = new Thickness();
			var analysisResultIndicatorWidth = (analysisResultIndicatorSize + analysisResultIndicatorMargin.Left + analysisResultIndicatorMargin.Right);
			var markIndicatorSize = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.MarkIndicator.Size");
			var markIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.MarkIndicator.Border");
			var markIndicatorCornerRadius = app.FindResourceOrDefault<CornerRadius>("CornerRadius/SessionView.LogListBox.MarkIndicator.Border");
			var markIndicatorMargin = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.MarkIndicator.Margin", new Thickness(1));
			var markIndicatorWidth = (markIndicatorSize + markIndicatorMargin.Left + markIndicatorMargin.Right);
			var itemBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.Item.Column.Border", new Thickness(1));
			var itemCornerRadius = app.FindResourceOrDefault<CornerRadius>("CornerRadius/SessionView.LogListBox.Item.Column.Border");
			var itemPadding = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.Item.Padding");
			var itemMaxWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.Item.MaxWidth", double.NaN);
			var propertyPadding = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.Item.Property.Padding");
			var splitterWidth = app.FindResourceOrDefault<double>("Double/GridSplitter.Thickness");
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
					it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
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
						_ => (Control)new CarinaStudio.Controls.TextBlock().Also(it =>
						{
							it.Bind(CarinaStudio.Controls.TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
							it.Bind(CarinaStudio.Controls.TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
							//it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
							if (isMultiLineProperty)
								it.Bind(CarinaStudio.Controls.TextBlock.MaxLinesProperty, new Binding() { Path = nameof(MaxDisplayLineCountForEachLog), Source = this });
							else
								it.MaxLines = 1;
							it.MaxWidth = itemMaxWidth;
							it.Padding = propertyPadding;
							it.Bind(CarinaStudio.Controls.TextBlock.TextProperty, new Binding().Also(binding =>
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
								viewDetails.Bind(LinkTextBlock.IsVisibleProperty, new Binding() { Path = $"HasExtraLinesOf{logProperty.Name}" });
								viewDetails.Bind(LinkTextBlock.TextProperty, viewDetails.GetResourceObservable("String/SessionView.ViewFullLogMessage"));
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
								it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrushForPointerOver) });
							}
							else
							{
								it.BorderBrush = null;
								it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
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
						if (actualPropertyView is CarinaStudio.Controls.TextBlock textBlock 
							&& width != null
							&& Platform.IsMacOS 
							&& this.attachedWindow != null)
						{
							this.attachedWindow.GetObservable(Avalonia.Controls.Window.IsActiveProperty).Subscribe(isActive => 
							{
								if (!isActive)
									ToolTip.SetIsOpen(textBlock, false);
							});
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
							border.Bind(Avalonia.Controls.Image.IsVisibleProperty, new Binding() { Path = nameof(DisplayableLog.IsMarked) });
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
						panel.Children.Add(new Avalonia.Controls.Image().Also(image =>
						{
							image.Classes.Add("Icon");
							image.Bind(Avalonia.Controls.Image.IsVisibleProperty, new Binding() { Path = nameof(DisplayableLog.HasAnalysisResult) });
							image.Bind(Avalonia.Controls.Image.SourceProperty, new Binding() { Path = nameof(DisplayableLog.AnalysisResultIndicatorIcon) });
						}));
					}));
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
			var colorIndicatorBorderBrush = app.FindResourceOrDefault<IBrush?>("Brush/WorkingArea.Panel.Background");
			var colorIndicatorBorderThickness = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogListBox.ColorIndicator.Border");
			var colorIndicatorWidth = app.FindResourceOrDefault<double>("Double/SessionView.LogListBox.ColorIndicator.Width");
			var itemPadding = app.FindResourceOrDefault<Thickness>("Thickness/SessionView.MarkedLogListBox.Item.Padding");
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
					it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
					it.Margin = itemPadding;
					it.Opacity = 0;
					it.Text = " ";
					itemPanel.Children.Add(it);
				});
				var propertyView = new Avalonia.Controls.TextBlock().Also(it =>
				{
					it.Bind(Avalonia.Controls.TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(Avalonia.Controls.TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
					it.Bind(Avalonia.Controls.TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
					it.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding() { Path = propertyInMarkedItem });
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


		// Create new operation counting analysis rule set.
		void CreateOperationCountingAnalysisRuleSet()
		{
			if (this.attachedWindow != null)
				OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
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
			(session.LogAnalysis.AnalysisResults as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnLogAnalysisResultsChanged);
			(session.LogAnalysis.KeyLogAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
			(session.LogAnalysis.LogAnalysisScriptSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
			(session.LogAnalysis.OperationCountingAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
			(session.LogAnalysis.OperationDurationAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
			
			// detach from properties
			this.logAnalysisPanelVisibilityObserverToken.Dispose();
			this.logFilesPanelVisibilityObserverToken.Dispose();
			this.markedLogsPanelVisibilityObserverToken.Dispose();
			this.timestampCategoryPanelVisibilityObserverToken.Dispose();

			// detach from commands
			this.canAddLogFiles.Unbind();
			this.canReloadLogs.Unbind();
			this.canResetLogProfileToSession.Unbind();
			this.canSetIPEndPoint.Unbind();
			this.canSaveLogs.Unbind();
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
			this.scrollToLatestLogAnalysisResultAction.Cancel();

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
			if (!data.HasFileNames() || this.IsHandlingDragAndDrop)
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
							Message = this.GetResourceObservable("String/SessionView.NoFilePathDropped")
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
							Message = this.GetResourceObservable("String/SessionView.TooManyDirectoryPathsDropped")
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
					if (session.AreFileBasedLogs)
					{
						if (filePaths.IsEmpty())
							return true;
						if (!currentLogProfile.AllowMultipleFiles)
						{
							if (!session.IsLogFileNeeded || filePaths.Count > 1)
							{
								warningMessage = this.GetResourceObservable("String/SessionView.MultipleFilesAreNotAllowed");
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
			finally
			{
				this.IsHandlingDragAndDrop = false;
			}
		}


		// Edit given key log analysis rule set.
		void EditKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Edit given log analysis script set.
		void EditLogAnalysisScriptSet(LogAnalysisScriptSet? scriptSet)
		{
			if (scriptSet == null || this.attachedWindow == null)
				return;
			LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, scriptSet);
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


		// Edit given operation counting analysis rule set.
		void EditOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Edit given operation duration analysis rule set.
		void EditOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
		}


		// Edit PATH environment variable.
		void EditPathEnvVar()
		{
			if (!PathEnvVarEditorDialog.IsSupported || this.attachedWindow == null)
				return;
			_ = new PathEnvVarEditorDialog().ShowDialog(this.attachedWindow);
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
			var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
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
				this.OnExportLogAnalysisRuleSetFailed(ruleSet.Name, fileName);
			}
		}


		// Export given log analysis script set.
		async void ExportLogAnalysisScriptSet(LogAnalysisScriptSet? scriptSet)
		{
			// check state
			if (scriptSet == null || this.attachedWindow == null)
				return;
			
			// select file
			var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// export
			try
			{
				await scriptSet.SaveAsync(fileName, false);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Failed to export log analysis script set '{scriptSet.Id}' to '{fileName}'");
				this.OnExportLogAnalysisRuleSetFailed(scriptSet.Name, fileName);
			}
		}


		// Export given operation counting analysis rule set.
		async void ExportOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet? ruleSet)
		{
			// check state
			if (ruleSet == null || this.attachedWindow == null)
				return;
			
			// select file
			var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// export
			try
			{
				await ruleSet.SaveAsync(fileName, false);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Failed to export operation counting analysis rule set '{ruleSet.Id}' to '{fileName}'");
				this.OnExportLogAnalysisRuleSetFailed(ruleSet.Name, fileName);
			}
		}


		// Export given operation duration analysis rule set.
		async void ExportOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
		{
			// check state
			if (ruleSet == null || this.attachedWindow == null)
				return;
			
			// select file
			var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
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
				this.OnExportLogAnalysisRuleSetFailed(ruleSet.Name, fileName);
			}
		}


		// Filter by property of selected log.
		void FilterByLogProperty()
		{
			if (this.lastClickedLogPropertyView?.Tag is not DisplayableLogProperty property)
				return;
			(this.DataContext as Session)?.LogFiltering?.FilterBySelectedPropertyCommand?.TryExecute(property);
		}


		// Command to filter by property of selected log.
		ICommand FilterByLogPropertyCommand { get; }


		// Check whether log profile has been set or not.
		bool HasLogProfile { get => this.GetValue<bool>(HasLogProfileProperty); }


		// Import log analysis rule set.
		async void ImportLogAnalysisRuleSet()
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
			
			// try loading rule set
			var klaRuleSet = (KeyLogAnalysisRuleSet?)null;
			try
			{
				klaRuleSet = await KeyLogAnalysisRuleSet.LoadAsync(this.Application, fileNames[0], true);
			}
			catch
			{ }
			var laScriptSet = (LogAnalysisScriptSet?)null;
			if (klaRuleSet == null)
			{
				try
				{
					laScriptSet = await LogAnalysisScriptSet.LoadAsync(this.Application, fileNames[0]);
				}
				catch
				{ }
			}
			var odaRuleSet = (OperationDurationAnalysisRuleSet?)null;
			if (klaRuleSet == null && laScriptSet == null)
			{
				try
				{
					odaRuleSet = await OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileNames[0], true);
				}
				catch
				{ }
			}
			if (this.attachedWindow == null)
				return;
			if (klaRuleSet == null 
				&& laScriptSet == null
				&& odaRuleSet == null)
			{
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileNames[0];
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/SessionView.FailedToImportLogAnalysisRuleSet"));
					}),
				}.ShowDialog(this.attachedWindow);
				return;
			}

			// edit and add rule set
			if (klaRuleSet != null)
				KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, klaRuleSet);
			else if (laScriptSet != null)
				LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, laScriptSet);
			else if (odaRuleSet != null)
				OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, odaRuleSet);
		}


		/// <summary>
		/// Check whether the view is handling drag-and-drop data or not.
		/// </summary>
		public bool IsHandlingDragAndDrop { get; private set; }


		// Check whether at least one key for multi-selection has been pressed or not.
		bool IsMultiSelectionKeyPressed(KeyModifiers keyModifiers)
		{
			if ((keyModifiers & KeyModifiers.Shift) != 0
				|| this.pressedKeys.Contains(Avalonia.Input.Key.LeftShift) 
				|| this.pressedKeys.Contains(Avalonia.Input.Key.RightShift))
			{
				return true;
			}
			if (Platform.IsMacOS)
			{
				if ((keyModifiers & KeyModifiers.Meta) != 0
					|| this.pressedKeys.Contains(Avalonia.Input.Key.LWin) 
					|| this.pressedKeys.Contains(Avalonia.Input.Key.RWin))
				{
					return true;
				}
			}
			else
			{
				if ((keyModifiers & KeyModifiers.Control) != 0
					|| this.pressedKeys.Contains(Avalonia.Input.Key.LeftCtrl) 
					|| this.pressedKeys.Contains(Avalonia.Input.Key.RightCtrl))
				{
					return true;
				}
			}
			return false;
		}


		// Check whether process info should be shown or not.
		bool IsProcessInfoVisible { get => this.GetValue<bool>(IsProcessInfoVisibleProperty); }


		// Get or set whether scrolling to latest log is needed or not.
		public bool IsScrollingToLatestLogNeeded
		{
			get => this.GetValue<bool>(IsScrollingToLatestLogNeededProperty);
			set => this.SetValue<bool>(IsScrollingToLatestLogNeededProperty, value);
		}


		// Get or set whether scrolling to latest log analysis result is needed or not.
		public bool IsScrollingToLatestLogAnalysisResultNeeded
		{
			get => this.GetValue<bool>(IsScrollingToLatestLogAnalysisResultNeededProperty);
			set => this.SetValue<bool>(IsScrollingToLatestLogAnalysisResultNeededProperty, value);
		}


		// Whether "Tools" menu item is visible or not.
		bool IsToolsMenuItemVisible { get; }


		// Get font family of log.
		FontFamily LogFontFamily { get => this.GetValue<FontFamily>(LogFontFamilyProperty); }


		// Get font size of log.
		double LogFontSize { get => this.GetValue<double>(LogFontSizeProperty); }


		// Log in to Azure.
		void LoginToAzure()
		{
		}


		// Log out from Azure.
		void LogoutFromAzure()
		{
		}


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
				this.isActiveObserverToken = window.GetObservable(Avalonia.Controls.Window.IsActiveProperty).Subscribe(isActive =>
				{
					if (isActive)
						this.SynchronizationContext.Post(() => this.ShowLogAnalysisRuleSetsTutorial());
				});
			});

			// add event handlers
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
			this.Application.ProductManager.ProductActivationChanged += this.OnProductActivationChanged;
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.AddHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.AddHandler(DragDrop.DragLeaveEvent, this.OnDragLeave);
			this.AddHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.AddHandler(DragDrop.DropEvent, this.OnDrop);
			this.AddHandler(KeyDownEvent, this.OnPreviewKeyDown, RoutingStrategies.Tunnel);
			this.AddHandler(KeyUpEvent, this.OnPreviewKeyUp, RoutingStrategies.Tunnel);

			// check product state
			this.SetAndRaise<bool>(IsProVersionActivatedProperty, ref this.isProVersionActivated, this.Application.ProductManager.IsProductActivated(Products.Professional));
			if (this.isProVersionActivated)
				this.RecreateLogHeadersAndItemTemplate();
			this.UpdateToolsMenuItems();

			// check script running
			this.SetValue<bool>(EnableRunningScriptProperty, this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript));

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
			this.Application.ProductManager.ProductActivationChanged -= this.OnProductActivationChanged;
			this.Settings.SettingChanged -= this.OnSettingChanged;
			this.RemoveHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.RemoveHandler(DragDrop.DragLeaveEvent, this.OnDragLeave);
			this.RemoveHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.RemoveHandler(DragDrop.DropEvent, this.OnDrop);
			this.RemoveHandler(KeyDownEvent, this.OnPreviewKeyDown);
			this.RemoveHandler(KeyUpEvent, this.OnPreviewKeyUp);

			// release predefined log text filter list
			((INotifyCollectionChanged)PredefinedLogTextFilterManager.Default.Filters).CollectionChanged -= this.OnPredefinedLogTextFiltersChanged;
			foreach (var filter in this.predefinedLogTextFilters)
				this.DetachFromPredefinedLogTextFilter(filter);
			this.predefinedLogTextFilters.Clear();
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
			var analysisResultIndicatorMargin = new Thickness();
			var markIndicatorSize = app.TryFindResource("Double/SessionView.LogListBox.MarkIndicator.Size", out var rawResource) ? (double)rawResource! : default;
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
					border.Background = Brushes.Transparent;
					border.Classes.Add("Icon");
					border.Child = new Avalonia.Controls.Image().Also(image =>
					{
						image.Classes.Add("Icon");
						if (app.TryFindResource("Image/Mark", out rawResource))
							image.Source = (rawResource as IImage);
					});
					border.Height = markIndicatorSize;
					border.Margin = markIndicatorMargin;
					border.Bind(ToolTip.TipProperty, this.GetResourceObservable("String/SessionView.MarkLog"));
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
						if (app.TryFindResource("Image/Icon.Analysis", out rawResource))
							image.Source = (rawResource as IImage);
					});
					border.Height = analysisResultIndicatorSize;
					border.Margin = analysisResultIndicatorMargin;
					border.Bind(ToolTip.TipProperty, this.GetResourceObservable("String/SessionView.LogAnalysisResult"));
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


		// Called when failed to export log analysis rule set.
		void OnExportLogAnalysisRuleSetFailed(string? ruleSetName, string fileName)
		{
			if (this.attachedWindow != null)
			{
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = ruleSetName;
						it.Arg2 = fileName;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/SessionView.FailedToExportLogAnalysisRuleSet"));
					}),
				}.ShowDialog(this.attachedWindow);
			}
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
				observableToken = mainWindow.GetObservable(MainWindow.HasDialogsProperty).Subscribe(hasDialogs =>
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
				session.LogAnalysis.IsPanelVisible = true;
				this.logAnalysisResultListBox.ScrollIntoView(firstResult);
			}
		}


		// Called when pointer pressed on log analysis result list box.
		void OnLogAnalysisResultListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			// clear selection
			var point = e.GetCurrentPoint(this.logAnalysisResultListBox);
			var hitControl = this.logAnalysisResultListBox.InputHitTest(point.Position).Let(it =>
			{
				if (it == null)
					return (IVisual?)null;
				var listBoxItem = it.FindAncestorOfType<ListBoxItem>(true);
				if (listBoxItem != null)
					return listBoxItem;
				return it.FindAncestorOfType<ScrollBar>(true);
			});
			if (hitControl == null)
				this.SynchronizationContext.Post(() => this.logAnalysisResultListBox.SelectedItems.Clear());
			else if (hitControl is ListBoxItem && !this.IsMultiSelectionKeyPressed(e.KeyModifiers) && point.Properties.IsLeftButtonPressed)
			{
				// [Workaround] Clear selection first to prevent performance issue of changing selection from multiple items
				this.logAnalysisResultListBox.SelectedItems.Clear();
			}
			this.isPointerPressedOnLogAnalysisResultListBox = point.Properties.IsLeftButtonPressed;
		}


		// Called when pointer released on log analysis result list box.
		void OnLogAnalysisResultListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
				this.isPointerPressedOnLogAnalysisResultListBox = false;
		}


		// Called when pointer wheel change on log analysis result list box.
		void OnLogAnalysisResultListBoxPointerWheelChanged(object? sender, PointerWheelEventArgs e)
		{
			this.SynchronizationContext.Post(() => this.UpdateIsScrollingToLatestLogAnalysisResultNeeded(-e.Delta.Y));
		}


		// Called when log analysis result list box scrolled.
		void OnLogAnalysisResultListBoxScrollChanged(object? sender, ScrollChangedEventArgs e)
		{
			if (this.isPointerPressedOnLogAnalysisResultListBox 
				|| this.pressedKeys.Contains(Avalonia.Input.Key.Down)
				|| this.pressedKeys.Contains(Avalonia.Input.Key.Up)
				|| this.pressedKeys.Contains(Avalonia.Input.Key.Home)
				|| this.pressedKeys.Contains(Avalonia.Input.Key.End))
			{
				this.UpdateIsScrollingToLatestLogAnalysisResultNeeded(e.OffsetDelta.Y);
			}
		}


		// Called when double clicked on item in log analysis result list box.
		void OnLogAnalysisResultListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e) =>
			this.OnLogAnalysisResultListBoxSelectionChanged(true);


		// Called when log analysis result list box selection changed.
		void OnLogAnalysisResultListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
			this.OnLogAnalysisResultListBoxSelectionChanged(false);
		void OnLogAnalysisResultListBoxSelectionChanged(bool forceReSelection)
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
					// select log to focus
					var log = this.isAltKeyPressed
						? (result.EndingLog ?? result.Log ?? result.BeginningLog)
						: (result.BeginningLog ?? result.Log ?? result.EndingLog);
					var isLogSelected = log != null && this.logListBox.SelectedItems.Count == 1
						? Global.Run(() =>
						{
							var selectedLog = this.logListBox.SelectedItem as DisplayableLog;
							if (result.Log != null && result.Log == selectedLog)
								return true;
							if (result.BeginningLog != null && result.BeginningLog == selectedLog)
								return true;
							if (result.EndingLog != null && result.EndingLog == selectedLog)
								return true;
							return false;
						})
						: false;

					// focus on log
					if (log != null && (forceReSelection || !isLogSelected))
					{
						log.Let(new Func<DisplayableLog, Task>(async (log) =>
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
									if (session.IsShowingAllLogsTemporarily || !session.ToggleShowingAllLogsTemporarilyCommand.TryExecute())
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
							this.ScrollToLog(log);
							this.IsScrollingToLatestLogNeeded = false;
						}));
					}

					// cancel auto scrolling
					this.IsScrollingToLatestLogAnalysisResultNeeded = false;
				}
				this.logListBox.Focus();
			});
		}


		// Called when collection of log analysis results has been changed.
		void OnLogAnalysisResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (this.DataContext is not Session session)
				return;
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (this.IsScrollingToLatestLogAnalysisResultNeeded)
						this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
					break;
				case NotifyCollectionChangedAction.Remove:
					if (session.LogAnalysis.AnalysisResults.IsEmpty())
						this.scrollToLatestLogAnalysisResultAction.Cancel();
					break;
				case NotifyCollectionChangedAction.Reset:
					if (session.LogAnalysis.AnalysisResults.IsEmpty())
						this.scrollToLatestLogAnalysisResultAction.Cancel();
					else
						this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
					break;
			}
		}


		// Called when selection of list box of log analysis rule sets has been changed.
		void OnLogAnalysisRuleSetListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			// update selected rule sets
			var listBox = (Avalonia.Controls.ListBox)sender.AsNonNull();
			foreach (var ruleSet in e.RemovedItems!)
			{
				if (ruleSet is KeyLogAnalysisRuleSet klaRuleSets)
					this.selectedKeyLogAnalysisRuleSets.Remove(klaRuleSets);
				else if (ruleSet is LogAnalysisScriptSet laScriptSet)
					this.selectedLogAnalysisScriptSets.Remove(laScriptSet);
				else if (ruleSet is OperationCountingAnalysisRuleSet ocaRuleSets)
					this.selectedOperationCountingAnalysisRuleSets.Remove(ocaRuleSets);
				else if (ruleSet is OperationDurationAnalysisRuleSet odaRuleSets)
					this.selectedOperationDurationAnalysisRuleSets.Remove(odaRuleSets);
				else if (ruleSet != null)
					throw new NotImplementedException();
			}
			foreach (var ruleSet in e.AddedItems!)
			{
				if (ruleSet is KeyLogAnalysisRuleSet klaRuleSets)
					this.selectedKeyLogAnalysisRuleSets.Add(klaRuleSets);
				else if (ruleSet is LogAnalysisScriptSet laScriptSet)
					this.selectedLogAnalysisScriptSets.Add(laScriptSet);
				else if (ruleSet is OperationCountingAnalysisRuleSet ocaRuleSets)
					this.selectedOperationCountingAnalysisRuleSets.Add(ocaRuleSets);
				else if (ruleSet is OperationDurationAnalysisRuleSet odaRuleSets)
					this.selectedOperationDurationAnalysisRuleSets.Add(odaRuleSets);
				else if (ruleSet != null)
					throw new NotImplementedException();
			}

			// sync back to UI if needed
			var selectedRuleSetCount = 0;
			var copiedSelectedRuleSets = (IEnumerable)new object[0];
			var syncBackToUI = Global.Run(() =>
			{
				if (listBox == this.keyLogAnalysisRuleSetListBox)
				{
					selectedRuleSetCount = this.selectedKeyLogAnalysisRuleSets.Count;
					if (selectedRuleSetCount != listBox.SelectedItems.Count)
					{
						copiedSelectedRuleSets = this.selectedKeyLogAnalysisRuleSets.ToArray();
						return true;
					}
					return false;
				}
				else if (listBox == this.logAnalysisScriptSetListBox)
				{
					selectedRuleSetCount = this.selectedLogAnalysisScriptSets.Count;
					if (selectedRuleSetCount != listBox.SelectedItems.Count)
					{
						copiedSelectedRuleSets = this.selectedLogAnalysisScriptSets.ToArray();
						return true;
					}
					return false;
				}
				else if (listBox == this.operationCountingAnalysisRuleSetListBox)
				{
					selectedRuleSetCount = this.selectedOperationCountingAnalysisRuleSets.Count;
					if (selectedRuleSetCount != listBox.SelectedItems.Count)
					{
						copiedSelectedRuleSets = this.selectedOperationCountingAnalysisRuleSets.ToArray();
						return true;
					}
					return false;
				}
				else if (listBox == this.operationDurationAnalysisRuleSetListBox)
				{
					selectedRuleSetCount = this.selectedOperationDurationAnalysisRuleSets.Count;
					if (selectedRuleSetCount != listBox.SelectedItems.Count)
					{
						copiedSelectedRuleSets = this.selectedOperationDurationAnalysisRuleSets.ToArray();
						return true;
					}
					return false;
				}
				throw new NotImplementedException();
			});
			if (syncBackToUI)
			{
				// [Workaround] Need to sync selection back to control because selection will be cleared when popup opened
				if (selectedRuleSetCount > 0)
				{
					var isScheduled = this.updateLogAnalysisAction?.IsScheduled ?? false;
					this.SynchronizationContext.Post(() =>
					{
						listBox.SelectedItems.Clear();
						foreach (var ruleSet in copiedSelectedRuleSets)
							listBox.SelectedItems.Add(ruleSet);
						if (!isScheduled)
							this.updateLogAnalysisAction?.Cancel();
					});
				}
			}
			else
				this.updateLogAnalysisAction.Reschedule(this.UpdateLogAnalysisParamsDelay);
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
				this.ScrollToLog(log);
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
			else if (hitControl is ListBoxItem && !this.IsMultiSelectionKeyPressed(e.KeyModifiers) && point.Properties.IsLeftButtonPressed)
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
			if (Math.Abs(e.OffsetDelta.X) > 0.1 || Math.Abs(e.ExtentDelta.X) > 0.1)
				this.updateLogHeaderContainerMarginAction.Schedule();
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
				this.canCopyLogText.Update(hasSingleSelectedItem);
				this.canFilterByLogProperty.Update(logProperty != null 
					&& DisplayableLog.HasStringProperty(logProperty.Name)
					&& !logProperty.Name.EndsWith("String")
				);
				this.canMarkSelectedLogs.Update(hasSelectedItems && session.MarkLogsCommand.CanExecute(null));
				this.canMarkUnmarkSelectedLogs.Update(hasSelectedItems && session.MarkUnmarkLogsCommand.CanExecute(null));
				this.canShowFileInExplorer.Update(hasSelectedItems && session.IsLogFileNeeded);
				this.canShowLogProperty.Update(hasSingleSelectedItem && logProperty != null && DisplayableLog.HasStringProperty(logProperty.Name));
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
					this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItems.Clear());
				
				// scroll to selected log
				if (!session.LogSelection.IsAllLogsSelectionRequested)
					this.ScrollToSelectedLog();
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
							this.ScrollToLog(firstVisibleItemIndex);
						}
					}
				}
			}
		}


		// Called when key down.
		protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
		{
			this.pressedKeys.Add(e.Key);
			if (!e.Handled && !logAnalysisRuleSetsPopup.IsOpen)
			{
				if (this.Application.IsDebugMode && e.Source is not TextBox)
					this.Logger.LogTrace($"[KeyDown] {e.Key}, Modifiers: {e.KeyModifiers}");
				if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
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
									(this.DataContext as Session)?.LogSelection?.CopySelectedLogsWithFileNames();
								else
									(this.DataContext as Session)?.LogSelection?.CopySelectedLogs();
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
			else if (this.Application.IsDebugMode && e.Source is not TextBox)
				this.Logger.LogTrace($"[KeyDown] {e.Key} was handled by another component");
			base.OnKeyDown(e);
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
				if (!logAnalysisRuleSetsPopup.IsOpen)
				{
					if (this.Application.IsDebugMode && e.Source is not TextBox)
						this.Logger.LogTrace($"[KeyUp] {e.Key}, Modifiers: {e.KeyModifiers}");
					if (!this.IsMultiSelectionKeyPressed(e.KeyModifiers))
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
								}
								break;
							case Avalonia.Input.Key.Escape:
								if (e.Source is TextBox)
								{
									if (this.Application.IsDebugMode)
										this.Logger.LogTrace($"[KeyUp] {e.Key} on text box");
									this.logListBox.Focus();
								}
								else if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFiltersPopup.IsOpen = false;
								break;
							case Avalonia.Input.Key.F5:
								this.ReloadLogs();
								break;
							case Avalonia.Input.Key.Home:
								if (e.Source is not TextBox)
								{
									if (this.predefinedLogTextFiltersPopup.IsOpen)
										this.predefinedLogTextFilterListBox.SelectFirstItem();
									else
										this.logListBox.SelectFirstItem();
								}
								break;
							case Avalonia.Input.Key.M:
								if (e.Source is not TextBox)
									this.MarkUnmarkSelectedLogs();
								break;
							case Avalonia.Input.Key.P:
								if (e.Source is not TextBox)
									(this.DataContext as Session)?.PauseResumeLogsReadingCommand?.TryExecute();
								break;
							case Avalonia.Input.Key.S:
								if (e.Source is not TextBox && !this.isSelectingFileToSaveLogs)
									(this.DataContext as Session)?.LogSelection?.SelectMarkedLogs();
								break;
						}
					}
				}
				else if (e.Key == Avalonia.Input.Key.Escape)
					this.logAnalysisRuleSetsPopup.Close();
			}
			else if (this.Application.IsDebugMode && e.Source is not TextBox)
				this.Logger.LogTrace($"[KeyUp] {e.Key} was handled by another component");

			// stop tracking key
			this.pressedKeys.Remove(e.Key);
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
						this.Logger.LogWarning($"Drop pressed key {key}");
				}
				else
					this.Logger.LogWarning($"Drop {this.pressedKeys.Count} pressed keys");
				this.pressedKeys.Clear();
			}
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
					this.ScrollToLog(index);
				}
				else
					this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItems.Clear());
				it.Focus();
			});
			this.IsScrollingToLatestLogNeeded = false;
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
			// check modifier keys
			if ((e.KeyModifiers & KeyModifiers.Alt) != 0)
				this.isAltKeyPressed = true;
			
			// log event
#if DEBUG
			this.Logger.LogTrace($"[PreviewKeyDown] {e.Key}, Modifiers: {e.KeyModifiers}");
#endif

			// [Workaround] It will take long time to select all items by list box itself
			if (!e.Handled 
				&& e.Source is not TextBox
				&& (e.KeyModifiers & (Platform.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control)) != 0
				&& e.Key == Avalonia.Input.Key.A
				&& this.DataContext is Session session)
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
							Message = this.GetResourceObservable("String/SessionView.ConfirmSelectingAllLogs"),
						}.ShowDialog(this.attachedWindow);
						if (result == MessageDialogResult.No)
							return;
					}
				}

				// select all logs
				session.LogSelection.SelectAllLogs();
			}
		}


		// Called to handle key-up before all children.
		void OnPreviewKeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
		{
			// log event
#if DEBUG
			this.Logger.LogTrace($"[PreviewKeyUp] {e.Key}, Modifiers: {e.KeyModifiers}");
#endif
			
			// check modifier keys
			if ((e.KeyModifiers & KeyModifiers.Alt) == 0)
				this.isAltKeyPressed = false;
		}


		// Called when product state changed.
		void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
		{
			// update state
			if (productId != Products.Professional)
				return;
			this.SetAndRaise<bool>(IsProVersionActivatedProperty, ref this.isProVersionActivated, isActivated);
			
			// update UI
			this.UpdateToolsMenuItems();
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
				if ((bool)(object)change.NewValue.Value!)
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
			else if (property == IsScrollingToLatestLogAnalysisResultNeededProperty)
			{
				if ((bool)(object)change.NewValue.Value!)
					this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
				else
					this.scrollToLatestLogAnalysisResultAction.Cancel();
			}
		}


		// Called when list of log analysis of session changed.
		void OnSessionLogAnalysisRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (this.DataContext is not Session session)
				return;
			var isUpdateShceduled = this.updateLogAnalysisAction.IsScheduled;
			var syncBack = false;
			var selectedItems = Global.Run(() =>
			{
				if (sender == session.LogAnalysis.KeyLogAnalysisRuleSets)
					return this.keyLogAnalysisRuleSetListBox.SelectedItems;
				else if (sender == session.LogAnalysis.LogAnalysisScriptSets)
					return this.logAnalysisScriptSetListBox.SelectedItems;
				else if (sender == session.LogAnalysis.OperationCountingAnalysisRuleSets)
					return this.operationCountingAnalysisRuleSetListBox.SelectedItems;
				else if (sender == session.LogAnalysis.OperationDurationAnalysisRuleSets)
					return this.operationDurationAnalysisRuleSetListBox.SelectedItems;
				else
					throw new NotImplementedException();
			});
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (var ruleSet in e.NewItems!)
					{
						if (!selectedItems.Contains(ruleSet))
						{
							syncBack = true;
							selectedItems.Add(ruleSet);
						}
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (var ruleSet in e.OldItems!)
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
					foreach (var ruleSet in (IList)sender.AsNonNull())
						selectedItems.Add(ruleSet);
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
				case nameof(Session.HasWorkingDirectory):
					this.canShowWorkingDirectoryInExplorer.Update(Platform.IsOpeningFileManagerSupported && session.HasWorkingDirectory);
					break;
				case nameof(Session.IsActivated):
					if (!session.IsActivated)
					{
						this.scrollToLatestLogAction.Cancel();
						this.scrollToLatestLogAnalysisResultAction.Cancel();
					}
					else
					{
						this.ShowLogAnalysisRuleSetsTutorial();
						if (this.HasLogProfile)
						{
							if (session.LogProfile?.IsContinuousReading == true && this.IsScrollingToLatestLogNeeded)
								this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
							if (session.LogAnalysis.AnalysisResults.IsNotEmpty() && this.IsScrollingToLatestLogAnalysisResultNeeded)
								this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
						}
					}
					break;
				case nameof(Session.IsLogsReadingPaused):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.LogProfile):
					session.LogProfile.Let(profile =>
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
			if (e.Key == AppSuite.SettingKeys.EnableRunningScript)
			{
				var isEnabled = (bool)e.Value;
				this.SetValue<bool>(EnableRunningScriptProperty, isEnabled);
				if (!isEnabled)
					this.logAnalysisScriptSetListBox.SelectedItems.Clear();
			}
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


		// Open online documentation.
		void OpenLogAnalysisDocumentation() =>
			Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis");
		

		// Open online documentation.
		void OpenLogFilteringDocumentation() =>
			Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogFiltering");
		

		// Open online documentation.
		void OpenPredefinedTextFiltersDocumentation() =>
			Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogFiltering#PredefinedTextFilters");


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
		void ReloadLogFile(string? fileName)
		{
		}


		// Reload log file without reading precondition.
		void ReloadLogFileWithoutLogReadingPrecondition(string? fileName)
		{
		}


		// Reload logs.
		void ReloadLogs()
        {
			// check state
			this.VerifyAccess();
			if (this.DataContext is not Session session)
				return;

			// reload logs
			var hasSelectedLogs = this.logListBox.SelectedItems.Count > 0;
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


		// Command to reload logs.
		ICommand ReloadLogsCommand { get; }


		// Remove given key log analysis rule set.
		async void RemoveKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(ruleSet.Name, false))
			{
				KeyLogAnalysisRuleSetEditorDialog.CloseAll(ruleSet);
				KeyLogAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
			}
		}


		// Remove given log analysis script set.
		async void RemoveLogAnalysisScriptSet(LogAnalysisScriptSet? scriptSet)
		{
			if (scriptSet == null || this.attachedWindow == null)
				return;
			if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(scriptSet.Name, true))
			{
				LogAnalysisScriptSetEditorDialog.CloseAll(scriptSet);
				LogAnalysisScriptSetManager.Default.RemoveScriptSet(scriptSet);
			}
		}


		// Remove given operation counting analysis rule set.
		async void RemoveOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(ruleSet.Name, false))
			{
				OperationCountingAnalysisRuleSetEditorDialog.CloseAll(ruleSet);
				OperationCountingAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
			}
		}


		// Remove given operation duration analysis rule set.
		async void RemoveOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
		{
			if (ruleSet == null || this.attachedWindow == null)
				return;
			if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(ruleSet.Name, false))
			{
				OperationDurationAnalysisRuleSetEditorDialog.CloseAll(ruleSet);
				OperationDurationAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
			}
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


		// Scroll to specific log.
		void ScrollToLog(DisplayableLog log)
		{
			if (this.DataContext is not Session session)
				return;
			this.ScrollToLog(session, session.Logs.IndexOf(log));
		}
		void ScrollToLog(int index)
		{
			if (this.DataContext is not Session session)
				return;
			this.ScrollToLog(session, index);
		}
		void ScrollToLog(Session session, int index)
		{
			if (index < 0 || index >= session.Logs.Count)
				return;
			var itemContainerGenerator = this.logListBox.ItemContainerGenerator;
			foreach (var container in itemContainerGenerator.Containers)
			{
				if (container.Index == index)
					return;
			}
			this.logScrollViewer?.Let(scrollViewer => // [Workaround] Move closer to log first to make sure the scroll bar position will be correct
			{
				var extentHeight = scrollViewer.Extent.Height;
				var viewportHeight = scrollViewer.Viewport.Height;
				var offset = extentHeight * (index + 0.5) / session.Logs.Count + (viewportHeight / 2);
				if (offset < 0)
					offset = 0;
				else if (offset + viewportHeight > extentHeight)
					offset = extentHeight - viewportHeight;
				scrollViewer.Offset = new(scrollViewer.Offset.X, offset);
			});
			this.logListBox.ScrollIntoView(index);
		}


		// Scroll to first selected log.
		void ScrollToSelectedLog()
		{
			if (this.DataContext is not Session session)
				return;
			var selectedItems = this.logListBox.SelectedItems;
			if (selectedItems.Count == 0)
				return;
			this.ScrollToLog((DisplayableLog)selectedItems[0]!);
			this.logScrollViewer?.Let(scrollViewer =>
			{
				if (scrollViewer.Extent.Height > scrollViewer.Viewport.Height)
					this.IsScrollingToLatestLogNeeded = false;
			});
		}


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
			var endPoint = await new IPEndPointInputDialog().Also(it =>
			{
				it.InitialIPEndPoint = session.IPEndPoint;
				it.Bind(Avalonia.Controls.Window.TitleProperty, this.GetResourceObservable("String/SessionView.SetIPEndPoint"));
			}).ShowDialog<IPEndPoint>(this.attachedWindow);
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
		public async void SelectAndSetLogProfileAsync()
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
			
			// set log profile
			await this.SetLogProfileAsync(logProfile);
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
			var uri = await new UriInputDialog().Also(it =>
			{
				it.DefaultScheme = session.LogProfile?.DataSourceProvider?.Name == "Http" ? "https" : null;
				it.InitialUri = session.Uri;
				it.Bind(Avalonia.Controls.Window.TitleProperty, this.GetResourceObservable("String/SessionView.SetUri"));
			}).ShowDialog<Uri>(this.attachedWindow);
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


		// Show UI for user to select file to export log analysis rule set.
		async Task<string?> SelectFileToExportLogAnalysisRuleSetAsync()
		{
			if (this.attachedWindow == null)
				return null;
			return await new SaveFileDialog().Also(it =>
			{
				it.Filters!.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this.attachedWindow);
		}


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
			var timestamp = await new DateTimeSelectionDialog().Also(it =>
			{
				it.InitialDateTime = session.LogSelection.EarliestSelectedLogTimestamp ?? session.EarliestLogTimestamp;
				it.Message = this.Application.GetString("SessionView.SelectNearestLogByTimestamp.Message");
				it.Bind(Avalonia.Controls.Window.TitleProperty, this.GetResourceObservable("String/SessionView.SelectNearestLogByTimestamp.Title"));
			}).ShowDialog<DateTime?>(this.attachedWindow);
			if (timestamp == null)
				return;

			// select log
			if (session.LogSelection.SelectNearestLog(timestamp.Value) == null)
				this.logListBox.SelectedItems.Clear();
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
					this.Logger.LogWarning($"Unable to use profile '{logProfile.Name}' ({logProfile.Id}) because application is not running as administrator");
					return false;
				}
			}

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
				this.Logger.LogError($"Unable to set log profile '{logProfile.Name}' ({logProfile.Id}) to session");
				return false;
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
			var comparer = CarinaStudio.IO.PathEqualityComparer.Default;
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


		// Show tutorial of log analysis rule sets if needed.
		void ShowLogAnalysisRuleSetsTutorial()
		{
			// check state
			if (this.PersistentState.GetValueOrDefault(IsSelectLogAnalysisRuleSetsTutorialShownKey))
				return;
			if (this.attachedWindow is not MainWindow window || window.CurrentTutorial != null || !window.IsActive)
				return;
			if (this.DataContext is not Session session || !session.IsActivated || !session.LogAnalysis.IsPanelVisible)
				return;
			if (!this.logAnalysisRuleSetsButton.IsVisible)
				return;

			// show tutorial
			window.ShowTutorial(new Tutorial().Also(it =>
			{
				it.Anchor = this.logAnalysisRuleSetsButton;
				it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.SelectLogAnalysisRuleSets"));
				it.Dismissed += (_, e) => 
					this.PersistentState.SetValue<bool>(IsSelectLogAnalysisRuleSetsTutorialShownKey, true);
				it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
				it.IsSkippingAllTutorialsAllowed = false;
			}));
		}


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
		

		// Show menu to select log profile.
		public void ShowLogProfileSelectionMenu() =>
			this.logProfileSelectionMenu.Open(this.selectAndSetLogProfileDropDownButton);


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

			// show "use add tab button to select log profile"
			if ((window as MainWindow)?.ShowTutorialOfUsingAddTabButtonToSelectLogProfile(() => this.ShowNextTutorial(), this.SkipAllTutorials) == true)
				return true;

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


		// Show dialog to manage script log data source providers.
		void ShowScriptLogDataSourceProvidersDialog()
		{
			if (this.attachedWindow != null)
				_ = new ScriptLogDataSourceProvidersDialog().ShowDialog(this.attachedWindow);
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
			this.SetAndRaise<bool>(AreAllTutorialsShownProperty, ref this.areAllTutorialsShown, true);
		}


		// Get current state of status bar.
		SessionViewStatusBarState StatusBarState { get => this.GetValue<SessionViewStatusBarState>(StatusBarStateProperty); }


		// Switch filters combination mode.
		void SwitchLogFiltersCombinationMode()
		{
			if (this.DataContext is not Session session)
				return;
			session.LogFiltering.FiltersCombinationMode = session.LogFiltering.FiltersCombinationMode switch
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
			if (this.DataContext is Session session)
			{
				this.SetValue(CanFilterLogsByNonTextFiltersProperty, this.validLogLevels.Count > 1 
					|| session.LogFiltering.IsProcessIdFilterEnabled 
					|| session.LogFiltering.IsThreadIdFilterEnabled);
			}
			else
				this.SetValue(CanFilterLogsByNonTextFiltersProperty, false);
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


		// Update auto scrolling state according to user scrolling state.
		void UpdateIsScrollingToLatestLogAnalysisResultNeeded(double userScrollingDelta)
		{
			var scrollViewer = this.logAnalysisResultScrollViewer;
			if (scrollViewer == null)
				return;
			var logProfile = (this.HasLogProfile ? (this.DataContext as Session)?.LogProfile : null);
			if (logProfile == null)
				return;
			var offset = scrollViewer.Offset;
			if (Math.Abs(offset.Y) < 0.5 && offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height)
				return;
			if (this.IsScrollingToLatestLogAnalysisResultNeeded)
			{
				if (userScrollingDelta < 0)
				{
					this.Logger.LogDebug("Cancel auto scrolling of log analysis result because of user scrolling up");
					this.IsScrollingToLatestLogAnalysisResultNeeded = false;
				}
			}
			else
			{
				if (userScrollingDelta > 0 && ((scrollViewer.Offset.Y + scrollViewer.Viewport.Height) / (double)scrollViewer.Extent.Height) >= 0.999)
				{
					this.Logger.LogDebug("Start auto scrolling of log analysis result because of user scrolling down");
					this.IsScrollingToLatestLogAnalysisResultNeeded = true;
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
				sidePanelColumn.Width = new GridLength(new double[] {
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
		{
		}


		// LIst of log levels defined by log profile.
		IList<Logs.LogLevel> ValidLogLevels { get; }
	}
}
