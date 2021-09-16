using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.Input;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
#if WINDOWS_ONLY
using Microsoft.WindowsAPICodePack.Taskbar;
#endif
using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Main window.
	/// </summary>
	partial class MainWindow : BaseWindow
	{
		// Static fields.
		static readonly SettingKey<int> WindowHeightSettingKey = new SettingKey<int>("MainWindow.Height", 600);
		static readonly SettingKey<WindowState> WindowStateSettingKey = new SettingKey<WindowState>("MainWindow.State", WindowState.Maximized);
		static readonly SettingKey<int> WindowWidthSettingKey = new SettingKey<int>("MainWindow.Width", 800);


		// Constants.
		const int ReAttachToWorkspaceDelay = 1000;
		const int SaveWindowSizeDelay = 300;


		// Fields.
		Session? attachedActiveSession;
		bool canShowDialogs;
		readonly ScheduledAction focusOnTabItemContentAction;
		readonly ScheduledAction reAttachToWorkspaceAction;
		readonly ScheduledAction saveWindowSizeAction;
		readonly DataTemplate sessionTabItemHeaderTemplate;
		readonly ScheduledAction updateSysTaskBarAction;
		readonly TabControl tabControl;
		readonly ScheduledAction tabControlSelectionChangedAction;
		readonly IList tabItems;


		/// <summary>
		/// Initialize new <see cref="MainWindow"/> instance.
		/// </summary>
		public MainWindow()
		{
			// initialize.
			InitializeComponent();

			// find templates
			this.sessionTabItemHeaderTemplate = (DataTemplate)this.DataTemplates.First(it => it is DataTemplate dt && dt.DataType == typeof(Session));

			// setup controls
			this.tabControl = this.FindControl<TabControl>(nameof(tabControl)).AsNonNull().Also(it =>
			{
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == TabControl.SelectedIndexProperty)
					{
						// [Workaround] TabControl.SelectionChanged will be raised unexpectedly when selection change in log list box in SessionView
						this.tabControlSelectionChangedAction?.Schedule(); // Should not call OnTabControlSelectionChanged() directly because of timing issue that SelectedIndex will be changed temporarily when attaching to Workspace
					}
				};
			});
			this.tabItems = (IList)this.tabControl.Items;

			// create scheduled actions
			this.focusOnTabItemContentAction = new ScheduledAction(() =>
			{
				((this.tabControl.SelectedItem as TabItem)?.Content as IControl)?.Focus();
			});
			this.reAttachToWorkspaceAction = new ScheduledAction(() =>
			{
				if (this.DataContext is not Workspace workspace)
					return;
				this.Logger.LogWarning("Re-attach to workspace");
				this.DataContext = null;
				this.DataContext = workspace;
			});
			this.saveWindowSizeAction = new ScheduledAction(() =>
			{
				if (this.WindowState == WindowState.Normal)
				{
					this.PersistentState.SetValue<int>(WindowWidthSettingKey, (int)(this.Width + 0.5));
					this.PersistentState.SetValue<int>(WindowHeightSettingKey, (int)(this.Height + 0.5));
				}
			});
			this.updateSysTaskBarAction = new ScheduledAction(() =>
			{
				// check state
				if (this.IsClosed)
					return;

#if WINDOWS_ONLY
				// check platform
				if (!TaskbarManager.IsPlatformSupported)
					return;

				// get session
				var session = (this.DataContext as Workspace)?.ActiveSession;
				if (session == null)
				{
					TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
					return;
				}

				// update task bar
				if (session.HasAllDataSourceErrors)
				{
					TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
					TaskbarManager.Instance.SetProgressValue(1, 1);
				}
				else if (session.IsProcessingLogs)
				{
					if (session.IsReadingLogs)
						TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Indeterminate);
					else
					{
						TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
						TaskbarManager.Instance.SetProgressValue((int)(session.LogsFilteringProgress * 100 + 0.5), 100);
					}
				}
				else if (session.IsLogsReadingPaused || session.IsWaitingForDataSources)
				{
					TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Paused);
					TaskbarManager.Instance.SetProgressValue(1, 1);
				}
				else
					TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
#endif
			});
			this.tabControlSelectionChangedAction = new ScheduledAction(this.OnTabControlSelectionChanged);

			// restore window state
			this.PersistentState.Let(it =>
			{
				this.Height = Math.Max(0, it.GetValueOrDefault(WindowHeightSettingKey));
				this.Width = Math.Max(0, it.GetValueOrDefault(WindowWidthSettingKey));
				this.WindowState = it.GetValueOrDefault(WindowStateSettingKey);
			});

			// add handlers
			this.Application.PropertyChanged += this.OnAppPropertyChanged;
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.AddHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.AddHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.AddHandler(DragDrop.DropEvent, this.OnDrop);

			// reset latest version notified to user
			this.PersistentState.ResetValue(AppUpdateDialog.LatestNotifiedVersionKey);
		}


		// Attach to active session.
		void AttachToActiveSession(Session session)
		{
			if (this.attachedActiveSession == session)
				return;
			this.DetachFromActiveSession();
			session.PropertyChanged += this.OnActiveSessionPropertyChanged;
			this.attachedActiveSession = session;
			this.updateSysTaskBarAction.Schedule();
		}


		// Attach to workspace.
		void AttachToWorkspace(Workspace workspace)
		{
			// add event handlers
			workspace.PropertyChanged += this.OnWorkspacePropertyChanged;
			((INotifyCollectionChanged)workspace.Sessions).CollectionChanged += this.OnSessionsChanged;

			// create session tabs
			for (int i = 0, count = workspace.Sessions.Count; i < count; ++i)
			{
				var tabItem = this.CreateSessionTabItem(workspace.Sessions[i]);
				this.tabItems.Insert(i, tabItem);
			}

			// select tab item of active session
			var selectedIndex = (workspace.ActiveSession != null ? workspace.Sessions.IndexOf(workspace.ActiveSession) : -1);
			if (selectedIndex >= 0)
				this.tabControl.SelectedIndex = selectedIndex;
			else
				this.SelectActiveSessionIfNeeded();

			// attach to active session
			workspace.ActiveSession?.Let(it => this.AttachToActiveSession(it));

			// update system taskbar
			this.updateSysTaskBarAction.Schedule();
		}


		// Create new TabItem for given session.
		TabItem CreateSessionTabItem(Session session)
		{
			// create header
			var header = this.sessionTabItemHeaderTemplate.Build(session);

			// create session view
			var sessionView = new SessionView()
			{
				DataContext = session,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
			};

			// create tab item
			return new TabItem()
			{
				Content = sessionView,
				DataContext = session,
				Header = header,
			};
		}


		// Detach from active session
		void DetachFromActiveSession()
        {
			if (this.attachedActiveSession == null)
				return;
			this.attachedActiveSession.PropertyChanged -= this.OnActiveSessionPropertyChanged;
			this.attachedActiveSession = null;
			this.updateSysTaskBarAction.Reschedule();
        }


		// Detach from workspace.
		void DetachFronWorkspace(Workspace workspace)
		{
			// detach from active session
			this.DetachFromActiveSession();

			// remove event handlers
			workspace.PropertyChanged -= this.OnWorkspacePropertyChanged;
			((INotifyCollectionChanged)workspace.Sessions).CollectionChanged -= this.OnSessionsChanged;

			// remove session tab items
			for (var i = this.tabItems.Count - 2; i >= 0; --i)
			{
				this.DisposeSessionTabItem((TabItem)this.tabItems[i].AsNonNull());
				this.tabItems.RemoveAt(i);
			}

			// update system taskbar
			this.updateSysTaskBarAction.Execute();
		}


		// Dispose tab item for session.
		void DisposeSessionTabItem(TabItem tabItem)
		{
			if (tabItem.DataContext is not Session)
				return;
			tabItem.DataContext = null;
			(tabItem.Content as IControl)?.Let(it => it.DataContext = null);
			(tabItem.Header as IControl)?.Let(it => it.DataContext = null);
		}


		// Find tab item for specific session.
		TabItem? FindSessionTabItem(Session session)
		{
			foreach (var candidate in this.tabItems)
			{
				if (candidate is TabItem tabItem && tabItem.DataContext == session)
					return tabItem;
			}
			return null;
		}


		// Find SessionView for specific session.
		SessionView? FindSessionView(Session session) => this.FindSessionTabItem(session)?.Content as SessionView;


		// Find index of tab item by dragging position on window.
		int FindTabItemIndex(DragEventArgs e)
		{
			for (var i = this.tabItems.Count - 1; i >= 0; --i)
			{
				var header = (this.tabItems[i] as TabItem)?.Header as Control;
				if (header == null)
					continue;
				var position = e.GetPosition(header);
				var bounds = header.Bounds;
				if (position.X >= 0 && position.Y >= 0 && position.X <= bounds.Width && position.Y <= bounds.Height)
					return i;
			}
			return -1;
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Notify user that new application update is found.
		void NotifyAppUpdate()
		{
			// check state
			if (this.IsClosed || this.HasDialogs || !this.canShowDialogs)
				return;

			// check update
			var updateInfo = this.Application.UpdateInfo;
			if (updateInfo == null)
				return;
			var latestNotifiedVersionString = this.PersistentState.GetValueOrDefault(AppUpdateDialog.LatestNotifiedVersionKey);
			if (Version.TryParse(latestNotifiedVersionString, out var version) && version == updateInfo.Version)
				return;

			// notify
			new AppUpdateDialog().ShowDialog(this);
		}


		// Called when property of active session changed.
		void OnActiveSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(Session.LogsFilteringProgress):
					this.updateSysTaskBarAction.Schedule(100);
					break;
				case nameof(Session.HasAllDataSourceErrors):
				case nameof(Session.IsLogsReadingPaused):
				case nameof(Session.IsProcessingLogs):
				case nameof(Session.IsReadingLogs):
				case nameof(Session.IsWaitingForDataSources):
					this.updateSysTaskBarAction.Schedule();
					break;
			}
		}


		// Called when application property changed.
		void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IApplication.UpdateInfo))
				this.NotifyAppUpdate();
		}


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// remove handlers
			this.Application.PropertyChanged -= this.OnAppPropertyChanged;
			this.Settings.SettingChanged -= this.OnSettingChanged;
			this.RemoveHandler(DragDrop.DragEnterEvent, this.OnDragEnter);
			this.RemoveHandler(DragDrop.DragOverEvent, this.OnDragOver);
			this.RemoveHandler(DragDrop.DropEvent, this.OnDrop);

			// cancel scheduled actions
			this.tabControlSelectionChangedAction.Cancel();

			// call base
			base.OnClosed(e);
		}


		// Called when dialog closed.
		protected internal override void OnDialogClosed(BaseDialog dialog)
		{
			base.OnDialogClosed(dialog);
			this.NotifyAppUpdate();
		}


		// Called when drag enter.
		void OnDragEnter(object? sender, DragEventArgs e)
		{
			if (!e.Handled)
			{
				e.Handled = true;
				this.ActivateAndBringToFront();
			}
		}


		// Called when drag over.
		void OnDragOver(object? sender, DragEventArgs e)
		{
			// check state
			if (e.Handled)
				return;
			if (this.HasDialogs)
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}

			// check workspace
			e.Handled = true;
			if (this.DataContext is not Workspace)
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}

			// check file names
			if (!e.Data.HasFileNames())
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}

			// find tab
			var index = this.FindTabItemIndex(e);
			if (index < 0)
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}

			// switch to tab
			if (index < this.tabItems.Count - 1)
				this.tabControl.SelectedIndex = index;

			// accept dragging
			e.DragEffects = DragDropEffects.Copy;
		}


		// Called when drop data on view.
		async void OnDrop(object? sender, DragEventArgs e)
		{
			// check state
			if (this.HasDialogs || e.Handled)
				return;

			// check workspace
			e.Handled = true;
			if (this.DataContext is not Workspace workspace)
				return;

			// find tab and session view
			var index = this.FindTabItemIndex(e);
			if (index < 0)
				return;
			var sessionView = Global.Run(() =>
			{
				if (index < this.tabItems.Count - 1)
					return (this.tabItems[index] as TabItem)?.Content as SessionView;
				var session = workspace.CreateSession();
				workspace.ActiveSession = session;
				return this.FindSessionView(session);
			});
			if (sessionView == null)
				return;

			// drop to session view
			await sessionView.DropAsync(e);
		}


		// Called when key down.
		protected override void OnKeyDown(KeyEventArgs e)
		{
			// handle key event for combo keys
			if (!e.Handled && (e.KeyModifiers & KeyModifiers.Control) != 0)
			{
				switch (e.Key)
				{
					case Key.Tab:
						if (this.tabItems.Count > 2)
						{
							var index = this.tabControl.SelectedIndex;
							if ((e.KeyModifiers & KeyModifiers.Shift) == 0)
							{
								++index;
								if (index >= this.tabItems.Count - 1)
									index = 0;
							}
							else
							{
								--index;
								if (index < 0)
									index = this.tabItems.Count - 2;
							}
							this.tabControl.SelectedIndex = index;
						}
						e.Handled = true;
						break;
				}
			}

			// call base
			base.OnKeyDown(e);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			// call base
			base.OnOpened(e);

			// show dialogs
			this.SynchronizationContext.PostDelayed(() =>
			{
				this.canShowDialogs = true;
				this.NotifyAppUpdate();
				this.SelectAndSetLogProfile();
			}, 1000); // In order to show dialog at correct position on Linux, we need delay to make sure bounds of main window is set.
		}


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			var property = change.Property;
			if (property == DataContextProperty)
			{
				(change.OldValue.Value as Workspace)?.Let(it => this.DetachFronWorkspace(it));
				(change.NewValue.Value as Workspace)?.Let(it => this.AttachToWorkspace(it));
			}
			else if (property == HeightProperty || property == WidthProperty)
				this.saveWindowSizeAction.Reschedule(SaveWindowSizeDelay);
			else if (property == WindowStateProperty)
			{
				if (this.WindowState != WindowState.Minimized)
					this.PersistentState.SetValue<WindowState>(WindowStateSettingKey, this.WindowState);
			}
		}


		// Called when list of session changed.
		void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					{
						var startIndex = e.NewStartingIndex;
						var newSessions = e.NewItems.AsNonNull();
						for (var i = 0; i < newSessions.Count; ++i)
							this.tabItems.Insert(startIndex + i, this.CreateSessionTabItem((Session)newSessions[i].AsNonNull()));
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					{
						var startIndex = e.OldStartingIndex;
						var count = e.OldItems.AsNonNull().Count;
						for (var i = count - 1; i >= 0; --i)
						{
							this.DisposeSessionTabItem((TabItem)this.tabItems[startIndex + i].AsNonNull());
							this.tabItems.RemoveAt(startIndex + i);
						}
						this.SynchronizationContext.Post(this.SelectActiveSessionIfNeeded);
					}
					break;
				default:
					throw new InvalidOperationException($"Unsupported changed of list of Sessions: {e.Action}.");
			}
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == Settings.ThemeMode)
			{
				this.Logger.LogWarning("Theme mode changed");
				this.reAttachToWorkspaceAction.Reschedule(ReAttachToWorkspaceDelay);
			}
		}


		// Called when selection of tab control has been changed.
		void OnTabControlSelectionChanged()
		{
			if (this.DataContext is not Workspace workspace)
				return;
			var index = this.tabControl.SelectedIndex;
			if (index == this.tabItems.Count - 1)
			{
				var session = workspace.CreateSession();
				workspace.ActiveSession = session;
				if (this.Settings.GetValueOrDefault(Settings.SelectLogProfileForNewSession))
					this.FindSessionView(session)?.SelectAndSetLogProfile();
			}
			else
				workspace.ActiveSession = (Session)((TabItem)this.tabItems[index].AsNonNull()).DataContext.AsNonNull();
			this.focusOnTabItemContentAction.Schedule();
		}


		// Called when close button on tab item clicked.
		void OnTabItemCloseButtonClick(object? sender, RoutedEventArgs e)
		{
			// check state
			if (this.DataContext is not Workspace workspace)
				throw new InternalStateCorruptedException();

			// find tab item
			var tabItem = (sender as IControl)?.FindAncestorOfType<TabItem>();
			if (tabItem == null)
				return;
			var index = this.tabItems.IndexOf(tabItem);

			// select neighbor tab item
			if (this.tabControl.SelectedIndex == index)
			{
				if (index < this.tabItems.Count - 2)
					this.tabControl.SelectedIndex = (index + 1);
				else if (index > 0)
					this.tabControl.SelectedIndex = (index - 1);
				else
					workspace.ActiveSession = workspace.CreateSession();
			}

			// close session
			workspace.CloseSession((Session)tabItem.DataContext.AsNonNull());
		}


		// Called when property of workspace has been changed.
		void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not Workspace workspace || this.DataContext != workspace)
				return;
			if (e.PropertyName == nameof(Workspace.ActiveSession))
			{
				var activeSession = workspace.ActiveSession;
				var index = (activeSession != null ? workspace.Sessions.IndexOf(activeSession) : -1);
				if (index >= 0)
				{
					this.tabControl.SelectedIndex = index;
					this.AttachToActiveSession(activeSession.AsNonNull());
				}
				else
				{
					this.SynchronizationContext.Post(this.SelectActiveSessionIfNeeded);
					this.DetachFromActiveSession();
				}
			}
		}


		// Select an active session if needed.
		void SelectActiveSessionIfNeeded()
		{
			if (this.DataContext is not Workspace workspace)
				return;
			if (workspace.ActiveSession != null && workspace.Sessions.IndexOf(workspace.ActiveSession) >= 0)
				return;
			if (workspace.Sessions.IsNotEmpty())
				workspace.ActiveSession = workspace.Sessions[0];
			else
				workspace.ActiveSession = workspace.CreateSession();
		}


		// Select log profile and set if current session has no log profile.
		void SelectAndSetLogProfile()
		{
			// check state
			if (this.IsClosed || this.HasDialogs || !this.canShowDialogs)
				return;
			if (this.DataContext is not Workspace workspace)
				return;

			// select and set log profile
			workspace.ActiveSession?.Let(it =>
			{
				if (it.LogProfile == null
					&& this.Settings.GetValueOrDefault(Settings.SelectLogProfileForNewSession)
					&& !this.HasDialogs)
				{
					this.FindSessionView(it)?.SelectAndSetLogProfile();
				}
			});
		}
	}
}
