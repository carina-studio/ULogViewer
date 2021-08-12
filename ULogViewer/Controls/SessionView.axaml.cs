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
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Input;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// View of <see cref="Session"/>.
	/// </summary>
	partial class SessionView : BaseView
	{
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
			public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
			{
				if (value is Logs.LogLevel level)
				{
					if (level == Logs.LogLevel.Undefined)
						return app.GetString("SessionView.AllLogLevels");
					return Converters.EnumConverter.LogLevel.Convert(value, targetType, parameter, culture);
				}
				return null;
			}
			public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => null;
		}


		// Constants.
		const int ScrollingToLatestLogInterval = 100;


		// Static fields.
		static readonly AvaloniaProperty<bool> CanFilterLogsByNonTextFiltersProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(CanFilterLogsByNonTextFilters), false);
		static readonly AvaloniaProperty<bool> HasLogProfileProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(HasLogProfile), false);
		static readonly AvaloniaProperty<bool> IsScrollingToLatestLogNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogNeeded), true);
		static readonly AvaloniaProperty<FontFamily> LogFontFamilyProperty = AvaloniaProperty.Register<SessionView, FontFamily>(nameof(LogFontFamily));
		static readonly AvaloniaProperty<double> LogFontSizeProperty = AvaloniaProperty.Register<SessionView, double>(nameof(LogFontSize), 10.0);
		static readonly AvaloniaProperty<SessionViewStatusBarState> StatusBarStateProperty = AvaloniaProperty.Register<SessionView, SessionViewStatusBarState>(nameof(StatusBarState), SessionViewStatusBarState.None);


		// Fields.
		readonly ScheduledAction autoAddLogFilesAction;
		readonly ScheduledAction autoSetWorkingDirectoryAction;
		readonly MutableObservableBoolean canAddLogFiles = new MutableObservableBoolean();
		readonly MutableObservableBoolean canCopySelectedLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canCopySelectedLogsWithFileNames = new MutableObservableBoolean();
		readonly MutableObservableBoolean canFilterLogsByPid = new MutableObservableBoolean();
		readonly MutableObservableBoolean canFilterLogsByTid = new MutableObservableBoolean();
		readonly MutableObservableBoolean canMarkUnmarkSelectedLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSaveLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSelectMarkedLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSetLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSetWorkingDirectory = new MutableObservableBoolean();
		readonly MutableObservableBoolean canShowLogMessage = new MutableObservableBoolean();
		bool isAttachedToLogicalTree;
		bool isLogFileNeededAfterLogProfileSet;
		bool isMessageLogPropertyVisible;
		bool isPidLogPropertyVisible;
		bool isPointerPressedOnLogListBox;
		bool isRestartingAsAdminConfirmed;
		bool isSelectingFileToSaveLogs;
		bool isTidLogPropertyVisible;
		bool isWorkingDirNeededAfterLogProfileSet;
		readonly ContextMenu logActionMenu;
		readonly List<ColumnDefinition> logHeaderColumns = new List<ColumnDefinition>();
		readonly Control logHeaderContainer;
		readonly Grid logHeaderGrid;
		readonly List<MutableObservableValue<GridLength>> logHeaderWidths = new List<MutableObservableValue<GridLength>>();
		readonly ComboBox logLevelFilterComboBox;
		readonly ListBox logListBox;
		readonly TextBox logProcessIdFilterTextBox;
		ScrollViewer? logScrollViewer;
		readonly RegexTextBox logTextFilterTextBox;
		readonly TextBox logThreadIdFilterTextBox;
		readonly ListBox markedLogListBox;
		readonly ToggleButton otherActionsButton;
		readonly ContextMenu otherActionsMenu;
		readonly ListBox predefinedLogTextFilterListBox;
		readonly SortedObservableList<PredefinedLogTextFilter> predefinedLogTextFilters;
		readonly Popup predefinedLogTextFiltersPopup;
		readonly ScheduledAction scrollToLatestLogAction;
		readonly HashSet<PredefinedLogTextFilter> selectedPredefinedLogTextFilters = new HashSet<PredefinedLogTextFilter>();
		readonly MenuItem showLogMessageMenuItem;
		readonly ScheduledAction updateLogFiltersAction;
		readonly ScheduledAction updateStatusBarStateAction;
		readonly SortedObservableList<Logs.LogLevel> validLogLevels = new SortedObservableList<Logs.LogLevel>((x, y) => (int)x - (int)y);


		/// <summary>
		/// Initialize new <see cref="SessionView"/> instance.
		/// </summary>
		public SessionView()
		{
			// create commands
			this.AddLogFilesCommand = ReactiveCommand.Create(this.AddLogFiles, this.canAddLogFiles);
			this.CopySelectedLogsCommand = ReactiveCommand.Create(this.CopySelectedLogs, this.canCopySelectedLogs);
			this.CopySelectedLogsWithFileNamesCommand = ReactiveCommand.Create(this.CopySelectedLogsWithFileNames, this.canCopySelectedLogsWithFileNames);
			this.FilterLogsByProcessIdCommand = ReactiveCommand.Create<bool>(this.FilterLogsByProcessId, this.canFilterLogsByPid);
			this.FilterLogsByThreadIdCommand = ReactiveCommand.Create<bool>(this.FilterLogsByThreadId, this.canFilterLogsByTid);
			this.MarkUnmarkSelectedLogsCommand = ReactiveCommand.Create(this.MarkUnmarkSelectedLogs, this.canMarkUnmarkSelectedLogs);
			this.ResetLogFiltersCommand = ReactiveCommand.Create(this.ResetLogFilters, this.GetObservable<bool>(HasLogProfileProperty));
			this.SaveLogsCommand = ReactiveCommand.Create(this.SaveLogs, this.canSaveLogs);
			this.SelectAndSetLogProfileCommand = ReactiveCommand.Create(this.SelectAndSetLogProfile, this.canSetLogProfile);
			this.SelectAndSetWorkingDirectoryCommand = ReactiveCommand.Create(this.SelectAndSetWorkingDirectory, this.canSetWorkingDirectory);
			this.SelectMarkedLogsCommand = ReactiveCommand.Create(this.SelectMarkedLogs, this.canSelectMarkedLogs);
			this.ShowLogMessageCommand = ReactiveCommand.Create(this.ShowLogMessage, this.canShowLogMessage);
			this.SwitchLogFiltersCombinationModeCommand = ReactiveCommand.Create(this.SwitchLogFiltersCombinationMode, this.GetObservable<bool>(HasLogProfileProperty));

			// create collections
			this.predefinedLogTextFilters = new SortedObservableList<PredefinedLogTextFilter>(ComparePredefinedLogTextFilters);

			// setup log font
			this.UpdateLogFontFamily();
			this.UpdateLogFontSize();

			// setup properties
			this.ValidLogLevels = this.validLogLevels.AsReadOnly();

			// initialize
			this.InitializeComponent();

			// setup controls
			this.logActionMenu = (ContextMenu)this.Resources["logActionMenu"].AsNonNull();
			this.logHeaderContainer = this.FindControl<Control>("logHeaderContainer").AsNonNull();
			this.logHeaderGrid = this.FindControl<Grid>("logHeaderGrid").AsNonNull().Also(it =>
			{
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == Grid.BoundsProperty)
						this.ReportLogHeaderColumnWidths();
				};
			});
			this.logLevelFilterComboBox = this.FindControl<ComboBox>("logLevelFilterComboBox").AsNonNull();
			this.logListBox = this.FindControl<ListBox>("logListBox").AsNonNull().Also(it =>
			{
				it.AddHandler(ListBox.PointerPressedEvent, this.OnLogListBoxPointerPressed, RoutingStrategies.Tunnel);
				it.AddHandler(ListBox.PointerReleasedEvent, this.OnLogListBoxPointerReleased, RoutingStrategies.Tunnel);
				it.AddHandler(ListBox.PointerWheelChangedEvent, this.OnLogListBoxPointerWheelChanged, RoutingStrategies.Tunnel);
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == ListBox.ScrollProperty)
					{
						this.logScrollViewer = (it.Scroll as ScrollViewer)?.Also(scrollViewer =>
						{
							scrollViewer.AllowAutoHide = false;
							scrollViewer.ScrollChanged += this.OnLogListBoxScrollChanged;
						});
					}
				};
			});
			this.logProcessIdFilterTextBox = this.FindControl<TextBox>("logProcessIdFilterTextBox").AsNonNull().Also(it =>
			{
				it.AddHandler(TextBox.TextInputEvent, this.OnLogProcessIdTextBoxTextInput, RoutingStrategies.Tunnel);
			});
			this.logTextFilterTextBox = this.FindControl<RegexTextBox>("logTextFilterTextBox").AsNonNull().Also(it =>
			{
				it.IgnoreCase = this.Settings.GetValueOrDefault(Settings.IgnoreCaseOfLogTextFilter);
				it.ValidationDelay = this.UpdateLogFilterParamsDelay;
			});
			this.logThreadIdFilterTextBox = this.FindControl<TextBox>("logThreadIdFilterTextBox").AsNonNull().Also(it =>
			{
				it.AddHandler(TextBox.TextInputEvent, this.OnLogProcessIdTextBoxTextInput, RoutingStrategies.Tunnel);
			});
			this.markedLogListBox = this.FindControl<ListBox>("markedLogListBox").AsNonNull();
			this.otherActionsButton = this.FindControl<ToggleButton>("otherActionsButton").AsNonNull();
			this.otherActionsMenu = ((ContextMenu)this.Resources["otherActionsMenu"].AsNonNull()).Also(it =>
			{
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.otherActionsButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.otherActionsButton.IsChecked = true);
			});
			this.predefinedLogTextFilterListBox = this.FindControl<ListBox>("predefinedLogTextFilterListBox").AsNonNull();
			this.predefinedLogTextFiltersPopup = this.FindControl<Popup>("predefinedLogTextFiltersPopup").AsNonNull().Also(it =>
			{
				it.Closed += (_, sender) => this.logListBox.Focus();
				it.Opened += (_, sender) => this.predefinedLogTextFilterListBox.Focus();
			});
			this.showLogMessageMenuItem = this.FindControl<MenuItem>("showLogMessageMenuItem").AsNonNull().Also(it => this.UpdateShowLogMessageMenuItem());
#if !DEBUG
			this.FindControl<Button>("testButton").AsNonNull().IsVisible = false;
#endif
			this.FindControl<Control>("toolBarContainer").AsNonNull().Let(it =>
			{
				it.AddHandler(Control.PointerReleasedEvent, this.OnToolBarPointerReleased, RoutingStrategies.Tunnel);
			});

			// create scheduled actions
			this.autoAddLogFilesAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Session session || session.HasLogReaders)
					return;
				this.AddLogFiles();
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
			this.updateLogFiltersAction = new ScheduledAction(() =>
			{
				// get session
				if (this.DataContext is not Session session)
					return;

				// set level 
				session.LogLevelFilter = this.logLevelFilterComboBox.SelectedItem is Logs.LogLevel logLevel ? logLevel : Logs.LogLevel.Undefined;

				// set PID
				this.logProcessIdFilterTextBox.Text.Let(it =>
				{
					if (it.Length > 0 && int.TryParse(it, out var pid))
						session.LogProcessIdFilter = pid;
					else
						session.LogProcessIdFilter = null;
				});

				// set TID
				this.logThreadIdFilterTextBox.Text.Let(it =>
				{
					if (it.Length > 0 && int.TryParse(it, out var tid))
						session.LogThreadIdFilter = tid;
					else
						session.LogThreadIdFilter = null;
				});

				// update text filters
				session.LogTextFilter = this.logTextFilterTextBox.Regex;
				session.PredefinedLogTextFilters.Clear();
				foreach (var filter in this.selectedPredefinedLogTextFilters)
					session.PredefinedLogTextFilters.Add(filter);
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
			if (!this.canAddLogFiles.Value)
				return;
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
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
			}.ShowAsync(window);
			if (fileNames == null || fileNames.Length == 0)
				return;

			// check state
			if (this.DataContext is not Session session)
				return;
			if (!this.canAddLogFiles.Value)
				return;

			// add log files
			foreach (var fileName in fileNames)
				session.AddLogFileCommand.Execute(fileName);
		}


		// Command to add log files.
		ICommand AddLogFilesCommand { get; }


		// Attach to predefined log text filter
		void AttachToPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged += this.OnPredefinedLogTextFilterPropertyChanged;


		// Attach to session.
		void AttachToSession(Session session)
		{
			// add event handler
			session.PropertyChanged += this.OnSessionPropertyChanged;

			// check profile
			var profile = session.LogProfile;
			if (profile != null)
			{
				this.SetValue<bool>(HasLogProfileProperty, true);
				if (session.IsLogFileNeeded)
					this.isLogFileNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(Settings.SelectLogFilesWhenNeeded);
				else if (session.IsWorkingDirectoryNeeded)
					this.isWorkingDirNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(Settings.SelectWorkingDirectoryWhenNeeded);
				this.OnLogProfileSet(profile);
			}

			// attach to command
			session.AddLogFileCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.MarkUnmarkLogsCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.ResetLogProfileCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.SaveLogsCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.SetLogProfileCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.SetWorkingDirectoryCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			if (session.AddLogFileCommand.CanExecute(null))
			{
				this.canAddLogFiles.Update(true);
				if (this.isLogFileNeededAfterLogProfileSet && this.isAttachedToLogicalTree)
				{
					this.isLogFileNeededAfterLogProfileSet = false;
					this.autoAddLogFilesAction.Reschedule();
				}
			}
			else
				this.canAddLogFiles.Update(false);
			this.canMarkUnmarkSelectedLogs.Update(session.MarkUnmarkLogsCommand.CanExecute(null));
			this.canSaveLogs.Update(session.SaveLogsCommand.CanExecute(null));
			this.canSetLogProfile.Update(session.ResetLogProfileCommand.CanExecute(null) || session.SetLogProfileCommand.CanExecute(null));
			this.canSelectMarkedLogs.Update(session.HasMarkedLogs);
			if (session.SetWorkingDirectoryCommand.CanExecute(null))
			{
				this.canSetWorkingDirectory.Update(true);
				if (this.isWorkingDirNeededAfterLogProfileSet && this.isAttachedToLogicalTree)
				{
					this.isWorkingDirNeededAfterLogProfileSet = false;
					this.autoSetWorkingDirectoryAction.Reschedule();
				}
			}
			else
				this.canSetWorkingDirectory.Update(false);

			// start auto scrolling
			session.LogProfile?.Let(profile =>
			{
				if (!profile.IsContinuousReading)
					this.IsScrollingToLatestLogNeeded = false;
			});
			if (session.HasLogs && this.IsScrollingToLatestLogNeeded)
				this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);

			// sync log filters to UI
			this.logProcessIdFilterTextBox.Text = session.LogProcessIdFilter?.ToString() ?? "";
			this.logTextFilterTextBox.Regex = session.LogTextFilter;
			this.logThreadIdFilterTextBox.Text = session.LogThreadIdFilter?.ToString() ?? "";
			this.logLevelFilterComboBox.SelectedItem = session.LogLevelFilter;
			this.updateLogFiltersAction.Cancel();

			// update properties
			this.validLogLevels.AddAll(session.ValidLogLevels);

			// update UI
			this.OnDisplayLogPropertiesChanged();
			this.updateStatusBarStateAction.Schedule();
		}


		// Check whether at least one non-text log filter is supported or not.
		bool CanFilterLogsByNonTextFilters { get => this.GetValue<bool>(CanFilterLogsByNonTextFiltersProperty); }


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
			var result = x.Name.CompareTo(y.Name);
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
							this.Application.Restart($"-profile {profile.Id}", true);
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
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
				return false;

			// show dialog
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				Icon = MessageDialogIcon.Question,
				Message = this.Application.GetFormattedString("SessionView.NeedToRestartAsAdministrator", profile.Name),
			}.ShowDialog<MessageDialogResult>(window);
			if (result == MessageDialogResult.Yes)
			{
				this.Logger.LogWarning($"User agreed to restart as administrator for '{profile.Name}'");
				return true;
			}
			this.Logger.LogWarning($"User denied to restart as administrator for '{profile.Name}'");
			return false;
		}


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


		// Create item template for item of log list box.
		DataTemplate CreateLogItemTemplate(LogProfile profile, IList<DisplayableLogProperty> logProperties)
		{
			var app = (App)this.Application;
			var logPropertyCount = logProperties.Count;
			var colorIndicatorWidth = app.Resources.TryGetResource("Double.SessionView.LogListBox.ColorIndicator.Width", out var rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var itemPadding = app.Resources.TryGetResource("Thickness.SessionView.LogListBox.Item.Padding", out rawResource) ? (Thickness)rawResource.AsNonNull() : new Thickness();
			var propertyPadding = app.Resources.TryGetResource("Thickness.SessionView.LogListBox.Item.Property.Padding", out rawResource) ? (Thickness)rawResource.AsNonNull() : new Thickness();
			var splitterWidth = app.Resources.TryGetResource("Double.GridSplitter.Thickness", out rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			if (profile.ColorIndicator != LogColorIndicator.None)
				itemPadding = new Thickness(itemPadding.Left + colorIndicatorWidth, itemPadding.Top, itemPadding.Right, itemPadding.Bottom);
			var itemTemplateContent = new Func<IServiceProvider, object>(_ =>
			{
				var itemPanel = new Panel().Also(it =>
				{
					it.Children.Add(new Border().Also(border =>
					{
						border.Bind(Border.BackgroundProperty, border.GetResourceObservable("Brush.SessionView.LogListBox.Item.Background.Marked"));
						border.Bind(Border.IsVisibleProperty, new Binding() { Path = nameof(DisplayableLog.IsMarked) });
					}));
				});
				var itemGrid = new Grid().Also(it =>
				{
					it.Margin = itemPadding;
					itemPanel.Children.Add(it);
				});
				new TextBlock().Also(it =>
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
					var propertyView = logProperty.Name switch
					{
						_ => (Control)new TextBlock().Also(it =>
						{
							it.Bind(TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
							it.Bind(TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
							it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
							it.Bind(TextBlock.TextProperty, new Binding().Also(binding =>
							{
								if (logProperty.Name == nameof(DisplayableLog.Level))
									binding.Converter = Converters.EnumConverter.LogLevel;
								binding.Path = logProperty.Name;
							}));
							it.MaxLines = DisplayableLog.MaxDisplayableLineCount;
							it.Padding = propertyPadding;
							it.TextTrimming = TextTrimming.CharacterEllipsis;
							it.TextWrapping = TextWrapping.NoWrap;
							it.VerticalAlignment = VerticalAlignment.Top;
						}),
					};
					if (logProperty.Name == nameof(DisplayableLog.Message))
					{
						propertyView = new StackPanel().Also(it =>
						{
							it.Children.Add(propertyView);
							it.Children.Add(new TextBlock().Also(viewDetails =>
							{
								viewDetails.Bind(TextBlock.ForegroundProperty, viewDetails.GetResourceObservable("Brush.TextBlock.Foreground.Link"));
								viewDetails.Cursor = new Cursor(StandardCursorType.Hand);
								viewDetails.Bind(TextBlock.IsVisibleProperty, new Binding() { Path = nameof(DisplayableLog.HasExtraLinesOfMessage) });
								viewDetails.Bind(TextBlock.TextProperty, viewDetails.GetResourceObservable("String.SessionView.ViewFullLogMessage"));
								viewDetails.PointerReleased += (_, e) =>
								{
									if (e.InitialPressMouseButton == MouseButton.Left 
										&& viewDetails.FindLogicalAncestorOfType<ListBoxItem>()?.DataContext is DisplayableLog log)
									{
										this.ShowLogMessage(log);
									}
								};
							}));
							it.Orientation = Orientation.Vertical;
						});
					}
					Grid.SetColumn(propertyView, logPropertyIndex * 2);
					itemGrid.Children.Add(propertyView);
				}
				if (profile.ColorIndicator != LogColorIndicator.None)
				{
					new Border().Also(it =>
					{
						it.Bind(Border.BackgroundProperty, new Binding() { Path = nameof(DisplayableLog.ColorIndicatorBrush) });
						it.HorizontalAlignment = HorizontalAlignment.Left;
						it.Bind(ToolTip.TipProperty, new Binding() { Path = profile.ColorIndicator.ToString() });
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
			var colorIndicatorWidth = app.Resources.TryGetResource("Double.SessionView.LogListBox.ColorIndicator.Width", out var rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var itemPadding = app.Resources.TryGetResource("Thickness.SessionView.MarkedLogListBox.Item.Padding", out rawResource) ? (Thickness)rawResource.AsNonNull() : new Thickness();
			if (profile.ColorIndicator != LogColorIndicator.None)
				itemPadding = new Thickness(itemPadding.Left + colorIndicatorWidth, itemPadding.Top, itemPadding.Right, itemPadding.Bottom);
			var itemTemplateContent = new Func<IServiceProvider, object>(_ =>
			{
				var itemPanel = new Panel();
				new TextBlock().Also(it =>
				{
					// empty view to reserve height of item
					it.Bind(TextBlock.FontFamilyProperty, new Binding() { Path = nameof(LogFontFamily), Source = this });
					it.Bind(TextBlock.FontSizeProperty, new Binding() { Path = nameof(LogFontSize), Source = this });
					it.Margin = itemPadding;
					it.Opacity = 0;
					it.Text = " ";
					itemPanel.Children.Add(it);
				});
				var propertyView = new TextBlock().Also(it =>
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
				if (profile.ColorIndicator != LogColorIndicator.None)
				{
					new Border().Also(it =>
					{
						it.Bind(Border.BackgroundProperty, new Binding() { Path = nameof(DisplayableLog.ColorIndicatorBrush) });
						it.HorizontalAlignment = HorizontalAlignment.Left;
						it.Bind(ToolTip.TipProperty, new Binding() { Path = profile.ColorIndicator.ToString() });
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


		// Create predefined log text fliter.
		async void CreatePredefinedLogTextFilter()
		{
			// check state
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
				return;

			// create filter
			var filter = await new PredefinedLogTextFilterEditorDialog()
			{
				Regex = this.logTextFilterTextBox.Regex
			}.ShowDialog<PredefinedLogTextFilter>(window);
			if (filter == null)
				return;
			ViewModels.PredefinedLogTextFilters.Add(filter);
		}


		// Detach from predefined log text filter
		void DetachFromPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged -= this.OnPredefinedLogTextFilterPropertyChanged;


		// Detach from session.
		void DetachFromSession(Session session)
		{
			// remove event handler
			session.PropertyChanged -= this.OnSessionPropertyChanged;

			// detach from commands
			session.AddLogFileCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.MarkUnmarkLogsCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SaveLogsCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SetLogProfileCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SetLogProfileCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SetWorkingDirectoryCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			this.canAddLogFiles.Update(false);
			this.canMarkUnmarkSelectedLogs.Update(false);
			this.canSaveLogs.Update(false);
			this.canSelectMarkedLogs.Update(false);
			this.canSetLogProfile.Update(false);
			this.canSetWorkingDirectory.Update(false);

			// update properties
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
		/// <param name="e">Dragging event data.</param>
		/// <returns>True if dragged data is accepted by this view.</returns>
		public async Task<bool> DropAsync(DragEventArgs e)
		{
			// check data
			if (!e.Data.HasFileNames())
				return false;

			// bring window to front
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
				return false;
			window.ActivateAndBringToFront();

			// collect files
			var dropFilePaths = e.Data.GetFileNames().AsNonNull();
			var dirPaths = new List<string>();
			var filePaths = new List<string>();
			await Task.Run(() =>
			{
				foreach (var path in dropFilePaths)
				{
					try
					{
						if (File.Exists(path))
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
					}.ShowDialog(window);
					return false;
				}
				if (dirPaths.Count > 1)
				{
					_ = new MessageDialog()
					{
						Icon = MessageDialogIcon.Information,
						Message = this.Application.GetString("SessionView.TooManyDirectoryPathsDropped")
					}.ShowDialog(window);
					return false;
				}
			}

			// check state
			if (this.DataContext is not Session session)
				return false;

			// select new log profile
			var currentLogProfile = session.LogProfile;
			var needNewLogProfile = Global.Run(() =>
			{
				if (currentLogProfile == null)
					return true;
				if (session.IsLogFileNeeded)
					return filePaths.IsEmpty();
				if (session.IsWorkingDirectoryNeeded)
					return filePaths.IsNotEmpty();
				return false;
			});
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
				else
					it.Filter = logProfile => logProfile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName));
			}).ShowDialog<LogProfile>(window);

			// set log profile or create new session
			if (newLogProfile != null)
			{
				var workspace = session.Workspace;
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
					var newSession = workspace.CreateSessionWithLogFiles(newIndex, newLogProfile, filePaths);
					workspace.ActiveSession = newSession;
					return true;
				}
				else
				{
					var newSession = workspace.CreateSessionWithWorkingDirectory(newIndex, newLogProfile, dirPaths[0]);
					workspace.ActiveSession = newSession;
					return true;
				}
			}

			// set working directory or add log files
			if (session.SetWorkingDirectoryCommand.CanExecute(null))
				session.SetWorkingDirectoryCommand.TryExecute(dirPaths[0]);
			else if (session.AddLogFileCommand.CanExecute(null))
			{
				foreach (var filePath in filePaths)
					session.AddLogFileCommand.TryExecute(filePath);
			}

			// complete
			return true;
		}


		// Edit given predefined log text filter.
		void EditPredefinedLogTextFilter(PredefinedLogTextFilter? filter)
		{
			// check state
			if (filter == null)
				return;
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
				return;

			// edit filter
			new PredefinedLogTextFilterEditorDialog()
			{
				Filter = filter
			}.ShowDialog(window);
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
			this.logProcessIdFilterTextBox.Text = pid.Value.ToString();
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
			this.logThreadIdFilterTextBox.Text = tid.Value.ToString();
			this.updateLogFiltersAction.Reschedule();
		}


		// Command to filter logs by selected TID.
		ICommand FilterLogsByThreadIdCommand { get; }


		// Check whether log profile has been set or not.
		bool HasLogProfile { get => this.GetValue<bool>(HasLogProfileProperty); }


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Get or set whether scrolling to latest log is needed or not.
		bool IsScrollingToLatestLogNeeded
		{
			get => this.GetValue<bool>(IsScrollingToLatestLogNeededProperty);
			set => this.SetValue<bool>(IsScrollingToLatestLogNeededProperty, value);
		}


		// Get font family of log.
		FontFamily LogFontFamily { get => this.GetValue<FontFamily>(LogFontFamilyProperty); }


		// Get font size of log.
		double LogFontSize { get => this.GetValue<double>(LogFontSizeProperty); }


		// Mark or unmark selected logs.
		void MarkUnmarkSelectedLogs()
		{
			if (!this.canMarkUnmarkSelectedLogs.Value)
				return;
			if (this.DataContext is not Session session)
				return;
			var logs = this.logListBox.SelectedItems.Cast<DisplayableLog>();
			session.MarkUnmarkLogsCommand.TryExecute(logs);
		}


		// Command to mark or unmark selected logs.
		ICommand MarkUnmarkSelectedLogsCommand { get; }


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

			// update menu
			this.UpdateShowLogMessageMenuItem();
		}


		// Called when attaching to view tree.
		protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			// update state
			this.isAttachedToLogicalTree = true;

			// call base
			base.OnAttachedToLogicalTree(e);

			// add event handlers
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.AddHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.AddHandler(DragDrop.DropEvent, this.OnDrop);

			// setup predefined log text filter list
			this.predefinedLogTextFilters.AddAll(ViewModels.PredefinedLogTextFilters.All);
			foreach (var filter in ViewModels.PredefinedLogTextFilters.All)
				this.AttachToPredefinedLogTextFilter(filter);
			((INotifyCollectionChanged)ViewModels.PredefinedLogTextFilters.All).CollectionChanged += this.OnPredefinedLogTextFiltersChanged;

			// select log files or working directory
			if (this.isLogFileNeededAfterLogProfileSet)
			{
				this.isLogFileNeededAfterLogProfileSet = false;
				this.autoAddLogFilesAction.Reschedule();
			}
			else if (this.isWorkingDirNeededAfterLogProfileSet)
			{
				this.isWorkingDirNeededAfterLogProfileSet = false;
				this.autoSetWorkingDirectoryAction.Reschedule();
			}

			// check administrator role
			this.ConfirmRestartingAsAdmin();
		}


		// Called when detach from view tree.
		protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
		{
			// update state
			this.isAttachedToLogicalTree = false;

			// remove event handlers
			this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
			this.Settings.SettingChanged -= this.OnSettingChanged;
			this.RemoveHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.RemoveHandler(DragDrop.DropEvent, this.OnDrop);

			// release predefined log text filter list
			((INotifyCollectionChanged)ViewModels.PredefinedLogTextFilters.All).CollectionChanged -= this.OnPredefinedLogTextFiltersChanged;
			foreach (var filter in this.predefinedLogTextFilters)
				this.DetachFromPredefinedLogTextFilter(filter);
			this.predefinedLogTextFilters.Clear();
			this.selectedPredefinedLogTextFilters.Clear();

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
			var splitterWidth = app.Resources.TryGetResource("Double.GridSplitter.Thickness", out var rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var minHeaderWidth = app.Resources.TryGetResource("Double.SessionView.LogHeader.MinWidth", out rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var colorIndicatorWidth = app.Resources.TryGetResource("Double.SessionView.LogListBox.ColorIndicator.Width", out rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var headerTemplate = (DataTemplate)this.DataTemplates.First(it => it is DataTemplate dt && dt.DataType == typeof(DisplayableLogProperty));
			var columIndexOffset = 0;
			if (profile.ColorIndicator != LogColorIndicator.None)
			{
				this.logHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(colorIndicatorWidth, GridUnitType.Pixel));
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
			this.isMessageLogPropertyVisible = false;
			this.isPidLogPropertyVisible = false;
			this.isTidLogPropertyVisible = false;
			foreach (var logProperty in logProperties)
			{
				switch (logProperty.Name)
				{
					case nameof(DisplayableLog.Message):
						this.isMessageLogPropertyVisible = true;
						break;
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
				this.logProcessIdFilterTextBox.IsVisible = true;
			else
			{
				this.logProcessIdFilterTextBox.IsVisible = false;
				this.logProcessIdFilterTextBox.Text = "";
			}
			if (this.isTidLogPropertyVisible)
				this.logThreadIdFilterTextBox.IsVisible = true;
			else
			{
				this.logThreadIdFilterTextBox.IsVisible = false;
				this.logThreadIdFilterTextBox.Text = "";
			}

			// update menu
			this.UpdateShowLogMessageMenuItem();

			// update filter availability
			this.UpdateCanFilterLogsByNonTextFilters();
		}


		// Called when drag over.
		void OnDragOver(object? sender, DragEventArgs e)
		{
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
			e.Handled = true;
			await this.DropAsync(e);
		}


		// Called when log profile set.
		void OnLogProfileSet(LogProfile profile)
		{
			// reset auto scrolling
			this.IsScrollingToLatestLogNeeded = profile.IsContinuousReading;

			// check administrator role
			this.ConfirmRestartingAsAdmin();
		}


		// Called when property of log filter text box changed.
		void OnLogFilterTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == TextBlock.TextProperty && sender != this.logTextFilterTextBox)
				this.updateLogFiltersAction.Reschedule(this.UpdateLogFilterParamsDelay);
			else if (e.Property == RegexTextBox.RegexProperty)
				this.updateLogFiltersAction.Reschedule();
		}


		// Called when selected log level filter has been changed.
		void OnLogLevelFilterComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e) => this.updateLogFiltersAction?.Reschedule();


		// Called when double click on log list box.
		void OnLogListBoxDoubleTapped(object? sender, RoutedEventArgs e)
		{
			var selectedItem = this.logListBox.SelectedItem;
			if (selectedItem == null)
				return;
			var listBoxItem = this.logListBox.FindListBoxItem(selectedItem);
			if (listBoxItem != null && listBoxItem.IsPointerOver)
			{
				this.ShowLogMessage();
				e.Handled = true;
			}
		}


		// Called when pointer pressed on log list box.
		void OnLogListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
		{
			var point = e.GetCurrentPoint(this.logListBox);
			if (point.Properties.IsLeftButtonPressed)
				this.isPointerPressedOnLogListBox = true;
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
		}


		// Called when pointer released on log list box.
		void OnLogListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton == MouseButton.Left)
				this.isPointerPressedOnLogListBox = false;
			if (e.InitialPressMouseButton == MouseButton.Right)
				this.ShowLogActionsMenu();
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
			if (this.isPointerPressedOnLogListBox)
				this.UpdateIsScrollingToLatestLogNeeded(e.OffsetDelta.Y);

			// sync log header offset
			var logScrollViewer = this.logScrollViewer;
			if (logScrollViewer != null)
				this.logHeaderContainer.Margin = new Thickness(-logScrollViewer.Offset.X, 0, logScrollViewer.Offset.X, 0);
		}


		// Called when log list box selection changed.
		void OnLogListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
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

				// update command states
				this.canCopySelectedLogs.Update(hasSelectedItems && session.CopyLogsCommand.CanExecute(null));
				this.canCopySelectedLogsWithFileNames.Update(hasSelectedItems && session.CopyLogsWithFileNamesCommand.CanExecute(null));
				this.canFilterLogsByPid.Update(hasSingleSelectedItem && this.isPidLogPropertyVisible);
				this.canFilterLogsByTid.Update(hasSingleSelectedItem && this.isTidLogPropertyVisible);
				this.canMarkUnmarkSelectedLogs.Update(hasSelectedItems && session.MarkUnmarkLogsCommand.CanExecute(null));
				this.canShowLogMessage.Update(hasSingleSelectedItem && this.isMessageLogPropertyVisible);
			});
		}


		// Called when PID/TID text box input.
		void OnLogProcessIdTextBoxTextInput(object? sender, TextInputEventArgs e)
		{
			if (!char.IsDigit(e.Text?[0] ?? '\0'))
			{
				e.Handled = true;
				return;
			}
			this.updateLogFiltersAction.Reschedule(this.UpdateLogFilterParamsDelay);
		}


		// Called when key down.
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (!e.Handled)
			{
				if ((e.KeyModifiers & KeyModifiers.Control) != 0)
				{
					switch (e.Key)
					{
						case Key.C:
							if (e.Source is not TextBox)
							{
								if ((e.KeyModifiers & KeyModifiers.Shift) != 0)
									this.CopySelectedLogsWithFileNames();
								else
									this.CopySelectedLogs();
							}
							break;
						case Key.F:
							this.logTextFilterTextBox.Focus();
							this.logTextFilterTextBox.SelectAll();
							e.Handled = true;
							break;
						case Key.O:
							this.AddLogFiles();
							break;
						case Key.P:
							predefinedLogTextFiltersPopup.IsOpen = !predefinedLogTextFiltersPopup.IsOpen;
							break;
						case Key.S:
							if (e.Source == this.logTextFilterTextBox)
							{
								this.logTextFilterTextBox.Validate();
								if (this.logTextFilterTextBox.IsTextValid && this.logTextFilterTextBox.Regex != null)
									this.CreatePredefinedLogTextFilter();
							}
							else
								this.SaveLogs();
							break;
					}
				}
				else
				{
					switch (e.Key)
					{
						case Key.Down:
							if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectNextItem();
								else
									this.logListBox.SelectNextItem();
								e.Handled = true;
							}
							break;
						case Key.Up:
							if (e.Source is not TextBox)
							{
								if (this.predefinedLogTextFiltersPopup.IsOpen)
									this.predefinedLogTextFilterListBox.SelectPreviousItem();
								else
									this.logListBox.SelectPreviousItem();
								e.Handled = true;
							}
							break;
					}
				}
			}
			base.OnKeyDown(e);
		}


		// Called when key up.
		protected override void OnKeyUp(KeyEventArgs e)
		{
			// handle key event for single key
			if (!e.Handled && e.KeyModifiers == KeyModifiers.None)
			{
				switch (e.Key)
				{
					case Key.Apps:
						if (e.Source is not TextBox)
						{
							this.ShowLogActionsMenu();
							e.Handled = true;
						}
						break;
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
					case Key.Escape:
						if (e.Source is TextBox)
						{
							this.logListBox.Focus();
							e.Handled = true;
						}
						else if (this.predefinedLogTextFiltersPopup.IsOpen)
						{
							this.predefinedLogTextFiltersPopup.IsOpen = false;
							e.Handled = true;
						}
						break;
					case Key.F5:
						if (e.Source is not TextBox)
						{
							(this.DataContext as Session)?.ReloadLogsCommand?.TryExecute();
							e.Handled = true;
						}
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
							(this.DataContext as Session)?.PauseResumeLogsReadingCommand?.TryExecute();
							e.Handled = true;
						}
						break;
					case Key.S:
						if (e.Source is not TextBox && !this.isSelectingFileToSaveLogs)
						{
							this.SelectMarkedLogs();
							e.Handled = true;
						}
						break;
				}
			}

			// call base
			base.OnKeyUp(e);
		}


		// Called when selection in marked log list box has been changed.
		void OnMarkedLogListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			var log = this.markedLogListBox.SelectedItem as DisplayableLog;
			if (log == null)
				return;
			this.SynchronizationContext.Post(() => this.markedLogListBox.SelectedItem = null);
			this.logListBox.Let(it =>
			{
				it.SelectedItems.Clear();
				it.SelectedItem = log;
				it.ScrollIntoView(log);
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
		}


		// Called when CanExecute of command of Session has been changed.
		void OnSessionCommandCanExecuteChanged(object? sender, EventArgs e)
		{
			if (this.DataContext is not Session session)
				return;
			if (sender == session.AddLogFileCommand)
			{
				if (session.AddLogFileCommand.CanExecute(null))
				{
					this.canAddLogFiles.Update(true);
					if (this.isLogFileNeededAfterLogProfileSet && this.isAttachedToLogicalTree)
					{
						this.isLogFileNeededAfterLogProfileSet = false;
						this.autoAddLogFilesAction.Reschedule();
					}
				}
				else
					this.canAddLogFiles.Update(false);
			}
			else if (sender == session.MarkUnmarkLogsCommand)
				this.canMarkUnmarkSelectedLogs.Update(this.logListBox.SelectedItems.Count > 0 && session.MarkUnmarkLogsCommand.CanExecute(null));
			else if (sender == session.ResetLogProfileCommand || sender == session.SetLogProfileCommand)
				this.canSetLogProfile.Update(session.ResetLogProfileCommand.CanExecute(null) || session.SetLogProfileCommand.CanExecute(null));
			else if (sender == session.SaveLogsCommand)
				this.canSaveLogs.Update(session.SaveLogsCommand.CanExecute(null));
			else if (sender == session.SetWorkingDirectoryCommand)
			{
				if (session.SetWorkingDirectoryCommand.CanExecute(null))
				{
					this.canSetWorkingDirectory.Update(true);
					if (this.isWorkingDirNeededAfterLogProfileSet && this.isAttachedToLogicalTree)
					{
						this.isWorkingDirNeededAfterLogProfileSet = false;
						this.autoSetWorkingDirectoryAction.Reschedule();
					}
				}
				else
					this.canSetWorkingDirectory.Update(false);
			}
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
				case nameof(Session.IsActivated):
					if (!session.IsActivated)
						this.scrollToLatestLogAction.Cancel();
					else if (this.HasLogProfile && session.LogProfile?.IsContinuousReading == true && this.IsScrollingToLatestLogNeeded)
						this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
					break;
				case nameof(Session.IsLogsReadingPaused):
					this.updateStatusBarStateAction.Schedule();
					break;
				case nameof(Session.LogProfile):
					session.LogProfile?.Let(profile =>
					{
						if (profile != null)
						{
							this.SetValue<bool>(HasLogProfileProperty, true);
							if (!profile.IsContinuousReading)
								this.IsScrollingToLatestLogNeeded = false;
						}
						else
							this.SetValue<bool>(HasLogProfileProperty, false);
					});
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
			if (e.Key == Settings.IgnoreCaseOfLogTextFilter)
				this.logTextFilterTextBox.IgnoreCase = (bool)e.Value;
			else if (e.Key == Settings.LogFontFamily)
				this.UpdateLogFontFamily();
			else if (e.Key == Settings.LogFontSize)
				this.UpdateLogFontSize();
			else if (e.Key == Settings.UpdateLogFilterDelay)
				this.logTextFilterTextBox.ValidationDelay = this.UpdateLogFilterParamsDelay;
		}


		// Called when test button clicked.
		void OnTestButtonClick(object? sender, RoutedEventArgs e)
		{ }


		// Called when pointer released on tool bar.
		void OnToolBarPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (FocusManager.Instance.Current is not TextBox)
				this.SynchronizationContext.Post(() => this.logListBox.Focus());
		}


		// Sorted predefined log text filters.
		IList<PredefinedLogTextFilter> PredefinedLogTextFilters { get => this.predefinedLogTextFilters; }


		// Remove given predefined log text filter.
		void RemovePredefinedLogTextFilter(PredefinedLogTextFilter? filter)
		{
			if (filter == null)
				return;
			ViewModels.PredefinedLogTextFilters.Remove(filter);
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
			this.logProcessIdFilterTextBox.Text = "";
			this.logTextFilterTextBox.Regex = null;
			this.logThreadIdFilterTextBox.Text = "";
			this.predefinedLogTextFilterListBox.SelectedItems.Clear();
			this.updateLogFiltersAction.Execute();
		}


		// Command to reset all log filters.
		ICommand ResetLogFiltersCommand { get; }


		// Save logs to file.
		async void SaveLogs()
		{
			// check state
			this.VerifyAccess();
			if (!this.canSaveLogs.Value || this.isSelectingFileToSaveLogs)
				return;
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
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
				it.Filters.Add(new FileDialogFilter().Also(filter =>
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
					filter.Extensions.Add("*");
					filter.Name = app.GetString("FileFormat.All");
				}));
				it.Title = app.GetString("SessionView.SaveLogs");
			}).ShowAsync(window);
			this.isSelectingFileToSaveLogs = false;
			if (fileName == null)
				return;

			// save logs
			session.SaveLogsCommand.TryExecute(fileName);
		}


		// Command to save logs to file.
		ICommand SaveLogsCommand { get; }


		/// <summary>
		/// Select and set log profile.
		/// </summary>
		public async void SelectAndSetLogProfile()
		{
			// check state
			this.VerifyAccess();
			if (!this.canSetLogProfile.Value)
				return;
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
			{
				this.Logger.LogError("Unable to set log profile without attaching to window");
				return;
			}

			// select profile
			var logProfile = await new LogProfileSelectionDialog().ShowDialog<LogProfile>(window);
			if (logProfile == null)
				return;

			// check state
			if (this.DataContext is not Session session)
				return;

			// check administrator role
			if (logProfile.IsAdministratorNeeded && !this.Application.IsRunningAsAdministrator)
			{
				if (await this.ConfirmRestartingAsAdmin(logProfile))
					this.Application.Restart($"-profile {logProfile.Id}", true);
				else
					this.Logger.LogWarning($"Unable to use profile '{logProfile.Name}' because application is not running as administrator");
				return;
			}

			// reset log filters
			this.ResetLogFilters();

			// reset log profile
			this.isLogFileNeededAfterLogProfileSet = false;
			this.isRestartingAsAdminConfirmed = false;
			this.isWorkingDirNeededAfterLogProfileSet = false;
			this.autoAddLogFilesAction.Cancel();
			this.autoSetWorkingDirectoryAction.Cancel();
			if (session.ResetLogProfileCommand.CanExecute(null))
				session.ResetLogProfileCommand.Execute(null);

			// check state
			if (!this.canSetLogProfile.Value)
				return;

			// set log profile
			if (!session.SetLogProfileCommand.TryExecute(logProfile))
			{
				this.Logger.LogError("Unable to set log profile to session");
				return;
			}
			if (session.IsLogFileNeeded)
			{
				this.isLogFileNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(Settings.SelectLogFilesWhenNeeded);
				if (this.isLogFileNeededAfterLogProfileSet)
					this.autoAddLogFilesAction.Schedule();
			}
			else if (session.IsWorkingDirectoryNeeded)
			{
				this.isWorkingDirNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(Settings.SelectWorkingDirectoryWhenNeeded);
				if (this.isWorkingDirNeededAfterLogProfileSet)
					this.autoSetWorkingDirectoryAction.Schedule();
			}
			this.OnLogProfileSet(logProfile);
		}


		// Command to set log profile.
		ICommand SelectAndSetLogProfileCommand { get; }


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


		// Select and set working directory.
		async void SelectAndSetWorkingDirectory()
		{
			// check state
			if (!this.canSetWorkingDirectory.Value)
				return;
			var window = this.FindLogicalAncestorOfType<Window>();
			if (window == null)
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
			}.ShowAsync(window);
			if (string.IsNullOrWhiteSpace(directory))
				return;

			// check state
			if (!this.canSetWorkingDirectory.Value)
				return;
			if (this.DataContext is not Session session)
				return;

			// set working directory
			session.SetWorkingDirectoryCommand.Execute(directory);
		}


		// Command to set working directory.
		ICommand SelectAndSetWorkingDirectoryCommand { get; }


		// Command to select marked logs.
		ICommand SelectMarkedLogsCommand { get; }


		// Show application info.
		void ShowAppInfo()
		{
			this.FindLogicalAncestorOfType<Window>()?.Let(window =>
			{
				new AppInfoDialog().ShowDialog(window);
			});
		}


		// Show application options.
		void ShowAppOptions()
		{
			this.FindLogicalAncestorOfType<Window>()?.Let(window =>
			{
				new AppOptionsDialog().ShowDialog(window);
			});
		}


		// Show menu of log actions.
		void ShowLogActionsMenu()
		{
			if (this.logListBox.IsPointerOver && this.HasLogProfile)
				this.logActionMenu.Open(this);
		}


		// Show full log message.
		bool ShowLogMessage()
		{
			if (!this.canShowLogMessage.Value)
				return false;
			if (this.logListBox.SelectedItems.Count != 1)
				return false;
			return this.ShowLogMessage((DisplayableLog)this.logListBox.SelectedItems[0].AsNonNull());
		}
		bool ShowLogMessage(DisplayableLog log)
		{
			// check state
			var displayLogProperties = (this.DataContext as Session)?.DisplayLogProperties;
			if (displayLogProperties == null)
				return false;

			// find displayable property of message
			var logProperty = displayLogProperties.FirstOrDefault(it => it.Name == nameof(DisplayableLog.Message));
			if (logProperty == null)
				return false;

			// show dialog
			this.FindLogicalAncestorOfType<Window>()?.Let(window =>
			{
				new LogMessageDialog() 
				{
					Log = log.Log,
					LogMessageDisplayName = logProperty.DisplayName
				}.ShowDialog(window);
			});
			return true;
		}


		// Command to show message of log.
		ICommand ShowLogMessageCommand { get; }


		// Show UI of other actions.
		void ShowOtherActions()
		{
			if (this.otherActionsMenu.PlacementTarget == null)
				this.otherActionsMenu.PlacementTarget = this.otherActionsButton;
			this.otherActionsMenu.Open(this);
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


		// Update font family of log.
		void UpdateLogFontFamily()
		{
			var name = this.Settings.GetValueOrDefault(Settings.LogFontFamily);
			if (string.IsNullOrEmpty(name))
				name = Settings.DefaultLogFontFamily;
			this.SetValue<FontFamily>(LogFontFamilyProperty, new FontFamily(name));
		}


		// Update font size of log.
		void UpdateLogFontSize()
		{
			var size = Math.Max(Math.Min(this.Settings.GetValueOrDefault(Settings.LogFontSize), Settings.MaxLogFontSize), Settings.MinLogFontSize);
			this.SetValue<double>(LogFontSizeProperty, size);
		}


		// Get delay of updating log filter.
		int UpdateLogFilterParamsDelay { get => Math.Max(Settings.MinUpdateLogFilterDelay, Math.Min(Settings.MaxUpdateLogFilterDelay, this.Settings.GetValueOrDefault(Settings.UpdateLogFilterDelay))); }


		// Update menu item of showing log message.
		void UpdateShowLogMessageMenuItem()
		{
			var displayName = (this.DataContext as Session)?.DisplayLogProperties?.FirstOrDefault(it =>
			{
				return it.Name == nameof(DisplayableLog.Message);
			})?.DisplayName ?? LogPropertyNameConverter.Default.Convert(nameof(DisplayableLog.Message));
			this.showLogMessageMenuItem?.Let(it => it.Header = this.Application.GetFormattedString("SessionView.ShowLogMessage", displayName));
		}


		// LIst of log levels defined by log profile.
		IList<Logs.LogLevel> ValidLogLevels { get; }
	}
}
