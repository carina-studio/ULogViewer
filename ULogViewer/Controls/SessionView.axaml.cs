using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// View of <see cref="Session"/>.
	/// </summary>
	partial class SessionView : BaseView
	{
		/// <summary>
		/// Property of <see cref="HasLogProfile"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> HasLogProfileProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(HasLogProfile), false);
		/// <summary>
		/// Property of <see cref="IsLogTextFilterValid"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsLogTextFilterValidProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsLogTextFilterValid), true);
		/// <summary>
		/// Property of <see cref="IsScrollingToLatestLogNeeded"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsScrollingToLatestLogNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogNeeded), true);


		// Constants.
		const int ScrollingToLatestLogInterval = 100;


		// Fields.
		readonly MutableObservableBoolean canAddLogFiles = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSetLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSetWorkingDirectory = new MutableObservableBoolean();
		bool isWorkingDirNeededAfterLogProfileSet;
		readonly Control logHeaderContainer;
		readonly Grid logHeaderGrid;
		readonly ListBox logListBox;
		ScrollViewer? logScrollViewer;
		readonly TextBox logTextFilterTextBox;
		readonly ContextMenu otherActionsMenu;
		readonly ScheduledAction scrollToLatestLogAction;
		readonly ScheduledAction updateLogTextFilterAction;


		/// <summary>
		/// Initialize new <see cref="SessionView"/> instance.
		/// </summary>
		public SessionView()
		{
			// create commands
			this.AddLogFilesCommand = ReactiveCommand.Create(this.AddLogFiles, this.canAddLogFiles);
			this.ResetLogFiltersCommand = ReactiveCommand.Create(this.ResetLogFilters, this.GetObservable<bool>(HasLogProfileProperty));
			this.SetLogProfileCommand = ReactiveCommand.Create(this.SetLogProfile, this.canSetLogProfile);
			this.SetWorkingDirectoryCommand = ReactiveCommand.Create(this.SetWorkingDirectory, this.canSetWorkingDirectory);
			this.ShowOtherActionsCommand = ReactiveCommand.Create(this.ShowOtherActions);

			// initialize
			this.InitializeComponent();

			// setup controls
			this.logHeaderContainer = this.FindControl<Control>("logHeaderContainer").AsNonNull();
			this.logHeaderGrid = this.FindControl<Grid>("logHeaderGrid").AsNonNull();
			this.logListBox = this.FindControl<ListBox>("logListBox").AsNonNull().Also(it =>
			{
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
			this.logTextFilterTextBox = this.FindControl<TextBox>("logTextFilterTextBox").AsNonNull();
			this.otherActionsMenu = (ContextMenu)this.Resources["otherActionsMenu"].AsNonNull();

			// create scheduled actions
			this.scrollToLatestLogAction = new ScheduledAction(() =>
			{
				// check state
				if (!this.IsScrollingToLatestLogNeeded)
					return;
				if (this.DataContext is not Session session)
					return;
				if (session.Logs.IsEmpty() || session.LogProfile == null)
					return;

				// find log index
				var logIndex = session.LogProfile.SortDirection == SortDirection.Ascending ? session.Logs.Count - 1 : 0;

				// scroll to latest log
				this.logListBox.ScrollIntoView(logIndex);
				this.scrollToLatestLogAction?.Schedule(ScrollingToLatestLogInterval);
			});
			this.updateLogTextFilterAction = new ScheduledAction(() =>
			{
				// get session
				if (this.DataContext is not Session session)
					return;

				// create regex
				var regex = (Regex?)null;
				var pattern = this.logTextFilterTextBox.Text?.Trim();
				if (!string.IsNullOrEmpty(pattern))
				{
					try
					{
						regex = new Regex(pattern, this.Settings.GetValueOrDefault(Settings.IgnoreCaseOfLogTextFilter) ? RegexOptions.IgnoreCase : RegexOptions.None);
					}
					catch
					{
						this.SetValue<bool>(IsLogTextFilterValidProperty, false);
						return;
					}
				}
				this.SetValue<bool>(IsLogTextFilterValidProperty, true);

				// update session
				session.LogTextFilterRegex = regex;
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
			if (!this.canSetLogProfile.Value)
				return;

			// add log files
			foreach (var fileName in fileNames)
				session.AddLogFileCommand.Execute(fileName);
		}


		/// <summary>
		/// Command to add log files.
		/// </summary>
		public ICommand AddLogFilesCommand { get; }


		// Attach to session.
		void AttachToSession(Session session)
		{
			// add event handler
			session.PropertyChanged += this.OnSessionPropertyChanged;

			// attach to command
			session.AddLogFileCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.ResetLogProfileCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.SetLogProfileCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			session.SetWorkingDirectoryCommand.CanExecuteChanged += this.OnSessionCommandCanExecuteChanged;
			this.canAddLogFiles.Update(session.AddLogFileCommand.CanExecute(null));
			this.canSetLogProfile.Update(session.ResetLogProfileCommand.CanExecute(null) || session.SetLogProfileCommand.CanExecute(null));
			this.canSetWorkingDirectory.Update(session.SetWorkingDirectoryCommand.CanExecute(null));

			// update properties
			this.SetValue<bool>(HasLogProfileProperty, session.LogProfile != null);

			// start auto scrolling
			if (session.HasLogs && this.IsScrollingToLatestLogNeeded)
				this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);

			// sync log filters to UI
			this.logTextFilterTextBox.Text = session.LogTextFilterRegex?.ToString() ?? "";
			this.updateLogTextFilterAction.Cancel();

			// update UI
			this.OnDisplayLogPropertiesChanged();
		}


		// Detach from session.
		void DetachFromSession(Session session)
		{
			// remove event handler
			session.PropertyChanged -= this.OnSessionPropertyChanged;

			// detach from commands
			session.AddLogFileCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SetLogProfileCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SetLogProfileCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			session.SetWorkingDirectoryCommand.CanExecuteChanged -= this.OnSessionCommandCanExecuteChanged;
			this.canAddLogFiles.Update(false);
			this.canSetLogProfile.Update(false);
			this.canSetWorkingDirectory.Update(false);

			// update properties
			this.SetValue<bool>(HasLogProfileProperty, false);

			// stop auto scrolling
			this.scrollToLatestLogAction.Cancel();

			// update UI
			this.OnDisplayLogPropertiesChanged();
		}


		/// <summary>
		/// Check whether log profile has been set or not.
		/// </summary>
		public bool HasLogProfile { get => this.GetValue<bool>(HasLogProfileProperty); }


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Check whether pattern of log text filter is valid or not.
		/// </summary>
		public bool IsLogTextFilterValid { get => this.GetValue<bool>(IsLogTextFilterValidProperty); }


		/// <summary>
		/// Get or set whether scrolling to latest log is needed or not.
		/// </summary>
		public bool IsScrollingToLatestLogNeeded
		{
			get => this.GetValue<bool>(IsScrollingToLatestLogNeededProperty);
			set => this.SetValue<bool>(IsScrollingToLatestLogNeededProperty, value);
		}


		// Called when display log properties changed.
		void OnDisplayLogPropertiesChanged()
		{
			// clear headers
			foreach (var control in this.logHeaderGrid.Children)
				control.DataContext = null;
			this.logHeaderGrid.Children.Clear();
			this.logHeaderGrid.ColumnDefinitions.Clear();

			// clear item template
			this.logListBox.ItemTemplate = null;

			// get display log properties
			var logProperties = (this.DataContext as Session)?.DisplayLogProperties;
			if (logProperties == null || logProperties.IsEmpty())
				return;
			var logPropertyCount = logProperties.Count;

			// build headers
			var app = (App)this.Application;
			var splitterWidth = app.Resources.TryGetResource("Double.GridSplitter.Thickness", out var rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var minHeaderWidth = app.Resources.TryGetResource("Double.SessionView.LogHeader.MinWidth", out rawResource) ? (double)rawResource.AsNonNull() : 0.0;
			var headerTemplate = (DataTemplate)this.DataTemplates.First(it => it is DataTemplate dt && dt.DataType == typeof(DisplayableLogProperty));
			var headerColumns = new ColumnDefinition[logPropertyCount];
			var headerColumnWidths = new MutableObservableValue<GridLength>[logPropertyCount];
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
					headerColumnWidths[logPropertyIndex] = new MutableObservableValue<GridLength>(new GridLength(1, GridUnitType.Star));
				else
					headerColumnWidths[logPropertyIndex] = new MutableObservableValue<GridLength>(new GridLength(width.Value, GridUnitType.Pixel));
				var headerColumn = new ColumnDefinition(headerColumnWidths[logPropertyIndex].Value).Also(it =>
				{
					it.MinWidth = minHeaderWidth;
				});
				headerColumns[logPropertyIndex] = headerColumn;
				this.logHeaderGrid.ColumnDefinitions.Add(headerColumn);

				// create splitter view
				if (logPropertyIndex > 0)
				{
					var splitter = new GridSplitter().Also(it =>
					{
						it.Background = Brushes.Transparent;
						it.DragDelta += (_, e) =>
						{
							var headerColumnWidth = headerColumnWidths[logPropertyIndex - 1];
							if (headerColumnWidth.Value.GridUnitType == GridUnitType.Pixel)
								headerColumnWidth.Update(new GridLength(headerColumns[logPropertyIndex - 1].ActualWidth, GridUnitType.Pixel));
						};
						it.HorizontalAlignment = HorizontalAlignment.Stretch;
						it.VerticalAlignment = VerticalAlignment.Stretch;
					});
					Grid.SetColumn(splitter, logPropertyIndex * 2 - 1);
					this.logHeaderGrid.Children.Add(splitter);
				}

				// create header view
				var headerView = ((Border)headerTemplate.Build(logProperty)).Also(it =>
				{
					it.DataContext = logProperty;
					if (logPropertyIndex == 0)
						it.BorderThickness = new Thickness();
				});
				Grid.SetColumn(headerView, logPropertyIndex * 2);
				this.logHeaderGrid.Children.Add(headerView);
			}

			// build item template
			var itemPadding = app.Resources.TryGetResource("Thickness.SessionView.LogListBox.Item.Padding", out rawResource) ? (Thickness)rawResource.AsNonNull() : new Thickness();
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
						if (width == null)
							it.Width = new GridLength(1, GridUnitType.Star);
						else
							it.Bind(ColumnDefinition.WidthProperty, headerColumnWidths[logPropertyIndex]);
					});
					itemGrid.ColumnDefinitions.Add(propertyColumn);

					// create property view
					var propertyView = logProperty.Name switch
					{
						_ => new TextBlock().Also(it =>
						{
							it.Bind(TextBlock.ForegroundProperty, new Binding() { Path = nameof(DisplayableLog.LevelBrush) });
							it.Bind(TextBlock.TextProperty, new Binding() { Path = logProperty.Name });
							it.TextTrimming = TextTrimming.CharacterEllipsis;
							it.TextWrapping = TextWrapping.NoWrap;
							it.VerticalAlignment = VerticalAlignment.Top;
						}),
					};
					Grid.SetColumn(propertyView, logPropertyIndex * 2);
					itemGrid.Children.Add(propertyView);
				}
				return new ControlTemplateResult(itemPanel, null);
			});
			this.logListBox.ItemTemplate = new DataTemplate()
			{
				Content = itemTemplateContent,
				DataType = typeof(DisplayableLog),
			};
		}


		// Called when log list box scrolled.
		void OnLogListBoxScrollChanged(object? sender, ScrollChangedEventArgs e)
		{
			// check state
			var logScrollViewer = this.logScrollViewer;
			if (logScrollViewer == null)
				return;

			// update auto scrolling state
			var logProfile = (this.HasLogProfile ? (this.DataContext as Session)?.LogProfile : null);
			if (this.logListBox.IsPointerOver && logProfile != null)
			{
				if (this.IsScrollingToLatestLogNeeded)
				{
					if (logProfile.SortDirection == SortDirection.Ascending)
					{
						if (e.OffsetDelta.Y < 0)
						{
							this.Logger.LogDebug("Cancel auto scrolling because of user scrolling up");
							this.IsScrollingToLatestLogNeeded = false;
						}
					}
					else
					{
						if (e.OffsetDelta.Y > 0)
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
						if (e.OffsetDelta.Y > 0 && ((logScrollViewer.Offset.Y + logScrollViewer.Viewport.Height) / (double)logScrollViewer.Extent.Height) >= 0.999)
						{
							this.Logger.LogDebug("Start auto scrolling because of user scrolling down");
							this.IsScrollingToLatestLogNeeded = true;
						}
					}
					else
					{
						if (e.OffsetDelta.Y < 0 && (logScrollViewer.Offset.Y / (double)logScrollViewer.Extent.Height) <= 0.001)
						{
							this.Logger.LogDebug("Start auto scrolling because of user scrolling up");
							this.IsScrollingToLatestLogNeeded = true;
						}
					}
				}
			}

			// sync log header offset
			this.logHeaderContainer.Margin = new Thickness(-logScrollViewer.Offset.X, 0, 0, 0);
		}


		// Called when property of log text filter text box changed.
		void OnLogTextFilterTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == TextBlock.TextProperty)
				this.updateLogTextFilterAction.Reschedule(this.UpdateLogFilterParamsDelay);
		}


		// Called when key down.
		protected override void OnKeyDown(KeyEventArgs e)
		{
			// handle key event for combo keys
			if (!e.Handled && (e.KeyModifiers & KeyModifiers.Control) != 0)
			{
				switch (e.Key)
				{
					case Key.F:
						if (e.Source is not TextBox)
						{
							this.logTextFilterTextBox.Focus();
							this.logTextFilterTextBox.SelectAll();
							e.Handled = true;
						}
						break;
				}
			}

			// call base
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
					case Key.Escape:
						if (e.Source is TextBox)
						{
							this.logListBox.Focus();
							e.Handled = true;
						}
						break;
				}
			}

			// call base
			base.OnKeyUp(e);
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
				this.canAddLogFiles.Update(session.AddLogFileCommand.CanExecute(null));
			else if (sender == session.ResetLogProfileCommand || sender == session.SetLogProfileCommand)
				this.canSetLogProfile.Update(session.ResetLogProfileCommand.CanExecute(null) || session.SetLogProfileCommand.CanExecute(null));
			else if (sender == session.SetWorkingDirectoryCommand)
			{
				if (session.SetWorkingDirectoryCommand.CanExecute(null))
				{
					this.canSetWorkingDirectory.Update(true);
					if (this.isWorkingDirNeededAfterLogProfileSet)
					{
						this.isWorkingDirNeededAfterLogProfileSet = false;
						this.SetWorkingDirectory();
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
				case nameof(Session.HasLogs):
					if (!session.HasLogs)
						this.scrollToLatestLogAction.Cancel();
					else if (this.IsScrollingToLatestLogNeeded)
						this.scrollToLatestLogAction.Schedule(ScrollingToLatestLogInterval);
					break;
				case nameof(Session.LogProfile):
					this.SetValue<bool>(HasLogProfileProperty, session.LogProfile != null);
					break;
			}
		}


		// Reset all log filters.
		void ResetLogFilters()
		{
			this.logTextFilterTextBox.Text = "";
			this.updateLogTextFilterAction.Execute();
		}


		/// <summary>
		/// Command to reset all log filters.
		/// </summary>
		public ICommand ResetLogFiltersCommand { get; }


		// Set log profile.
		async void SetLogProfile()
		{
			// check state
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

			// reset log filters
			this.ResetLogFilters();

			// reset log profile
			this.isWorkingDirNeededAfterLogProfileSet = false;
			if (session.ResetLogProfileCommand.CanExecute(null))
				session.ResetLogProfileCommand.Execute(null);

			// check state
			if (!this.canSetLogProfile.Value)
				return;

			// set log profile
			this.isWorkingDirNeededAfterLogProfileSet = this.Settings.GetValueOrDefault(Settings.SelectWorkingDirectoryWhenNeeded);
			if (!session.SetLogProfileCommand.TryExecute(logProfile))
			{
				this.Logger.LogError("Unable to set log profile to session");
				return;
			}

			// reset auto scrolling
			this.IsScrollingToLatestLogNeeded = logProfile.IsContinuousReading;
		}


		/// <summary>
		/// Command to set log profile.
		/// </summary>
		public ICommand SetLogProfileCommand { get; }


		// Set working directory.
		async void SetWorkingDirectory()
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


		/// <summary>
		/// Command to set working directory.
		/// </summary>
		public ICommand SetWorkingDirectoryCommand { get; }


		// Show UI of other actions.
		void ShowOtherActions()
		{
			this.otherActionsMenu.Open(this);
		}


		/// <summary>
		/// Command to show UI of other actions.
		/// </summary>
		public ICommand ShowOtherActionsCommand { get; }


		// Get delay of updating log filter.
		int UpdateLogFilterParamsDelay { get => Math.Max(500, Math.Min(1000, this.Settings.GetValueOrDefault(Settings.UpdateLogFilterDelay))); }
	}
}
