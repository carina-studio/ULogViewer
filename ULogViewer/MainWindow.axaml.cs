//#define DEMO

using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Net;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.AppSuite.ViewModels;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Input;
using CarinaStudio.Threading;
using CarinaStudio.VisualTree;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
#if WINDOWS_ONLY
using Microsoft.WindowsAPICodePack.Taskbar;
#endif
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Main window.
	/// </summary>
	partial class MainWindow : MainWindow<IULogViewerApplication, Workspace>
	{
		// Constants.
		const string DraggingSessionKey = "DraggingSettion";


		// Static fields.
		static readonly AvaloniaProperty<bool> HasMultipleSessionsProperty = AvaloniaProperty.Register<MainWindow, bool>("HasMultipleSessions");
		static bool IsNetworkConnForProductActivationNotified;
		static bool IsReActivatingProVersionNeeded;
		static MainWindow? MainWindowToActivateProVersion;


		// Fields.
		Session? attachedActiveSession;
		readonly ScheduledAction focusOnTabItemContentAction;
		bool isReActivatingProVersion;
		readonly ScheduledAction notifyNetworkConnForProductActivationAction;
		readonly ScheduledAction reActivateProVersionAction;
		readonly ScheduledAction selectAndSetLogProfileAction;
		readonly DataTemplate sessionTabItemHeaderTemplate;
		readonly Stopwatch stopwatch = new Stopwatch();
		readonly ScheduledAction updateSysTaskBarAction;
		readonly AppSuite.Controls.TabControl tabControl;
		readonly ScheduledAction tabControlSelectionChangedAction;
		readonly ObservableList<TabItem> tabItems = new ObservableList<TabItem>();
		NativeMenuItem? toolsNativeMenuItem;


		/// <summary>
		/// Initialize new <see cref="MainWindow"/> instance.
		/// </summary>
		public MainWindow()
		{
			// initialize.
			AvaloniaXamlLoader.Load(this);
			if (Platform.IsMacOS)
				NativeMenu.SetMenu(this, this.Resources["nativeMenu"] as NativeMenu);

			// find templates
			this.sessionTabItemHeaderTemplate = (DataTemplate)this.DataTemplates.First(it => it is DataTemplate dt && dt.DataType == typeof(Session));

			// setup controls
			this.tabControl = this.FindControl<AppSuite.Controls.TabControl>(nameof(tabControl)).AsNonNull().Also(it =>
			{
				it.GetObservable(Avalonia.Controls.TabControl.SelectedIndexProperty).Subscribe(_ =>
				{
					// [Workaround] TabControl.SelectionChanged will be raised unexpectedly when selection change in log list box in SessionView
					this.tabControlSelectionChangedAction?.Schedule(); // Should not call OnTabControlSelectionChanged() directly because of timing issue that SelectedIndex will be changed temporarily when attaching to Workspace
				});
			});
			this.tabItems.AddRange(this.tabControl.Items.Cast<TabItem>());
			this.tabControl.Items = this.tabItems;

			// setup native menu items
			if (Platform.IsMacOS)
			{
				NativeMenu.GetMenu(this)?.Let(nativeMenu =>
				{
					for (var i = nativeMenu.Items.Count - 1; i >= 0; --i)
					{
						if (nativeMenu.Items[i] is not NativeMenuItem menuItem)
							continue;
						switch (menuItem.CommandParameter as string)
						{
							case "Tools":
								this.toolsNativeMenuItem = menuItem;
								for (var j = menuItem.Menu.Items.Count - 1; j >= 0; --j)
								{
									if (menuItem.Menu.Items[j] is not NativeMenuItem subMenuItem)
										continue;
									switch (subMenuItem.CommandParameter as string)
									{
										case "EditConfiguration":
											if (!this.Application.IsDebugMode)
												menuItem.Menu.Items.RemoveAt(j);
											break;
										case "EditPersistentState":
											if (!this.Application.IsDebugMode)
											{
												menuItem.Menu.Items.RemoveAt(j);
												menuItem.Menu.Items.RemoveAt(--j); // separator
											}
											break;
									}
								}
								break;
						}
					}
				});
			}
			this.UpdateToolMenuItems();

			// create scheduled actions
			this.focusOnTabItemContentAction = new(() =>
			{
				((this.tabControl.SelectedItem as TabItem)?.Content as IControl)?.Focus();
			});
			this.notifyNetworkConnForProductActivationAction = new(() =>
			{
				if (IsNetworkConnForProductActivationNotified
					|| NetworkManager.Default.IsNetworkConnected
					|| this.Application.ProductManager.IsProductActivated(Products.Professional, true))
				{
					return;
				}
				IsNetworkConnForProductActivationNotified = true;
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Information,
					Message = new FormattedString().Also(it =>
					{
						it.Bind(FormattedString.Arg1Property, this.GetResourceObservable("String/Product.ULogViewer-Pro"));
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/MainWindow.NetworkConnectionNeededForProductActivation"));
					}),
				}.ShowDialog(this);
			});
			this.reActivateProVersionAction = new(async () =>
			{
				if (this.IsClosed || !IsReActivatingProVersionNeeded || this.HasDialogs || !this.IsActive || this.isReActivatingProVersion)
					return;
				IsReActivatingProVersionNeeded = false;
				if (this.Application.ProductManager.TryGetProductState(Products.Professional, out var state)
					&& state == ProductState.Deactivated)
				{
					await new MessageDialog()
					{
						Icon = MessageDialogIcon.Warning,
						Message = new FormattedString().Also(it =>
						{
							it.Bind(FormattedString.Arg1Property, this.GetResourceObservable("String/Product.ULogViewer-Pro"));
							it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/MainWindow.ReActivateProductNeeded"));
						}),
					}.ShowDialog(this);
					this.isReActivatingProVersion = true;
					await this.Application.ProductManager.ActivateProductAsync(Products.Professional, this);
					this.isReActivatingProVersion = false;
				}
			});
			this.selectAndSetLogProfileAction = new(this.SelectAndSetLogProfile);
			this.updateSysTaskBarAction = new(() =>
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
					else if (double.IsFinite(session.LogsFilteringProgress))
					{
						TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
						TaskbarManager.Instance.SetProgressValue((int)(session.LogsFilteringProgress * 100 + 0.5), 100);
					}
					else
						TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.NoProgress);
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
			this.tabControlSelectionChangedAction = new(this.OnTabControlSelectionChanged);

			// attach to property change
			this.GetObservable(HasDialogsProperty).Subscribe(hasDialogs =>
			{
				if (!hasDialogs && IsReActivatingProVersionNeeded)
					this.reActivateProVersionAction.Schedule();
			});
			this.GetObservable(IsActiveProperty).Subscribe(isActive =>
			{
				if (isActive)
				{
					if (FocusManager.Instance?.Current is not TextBox)
						((this.tabControl.SelectedItem as TabItem)?.Content as Control)?.Focus();
					if (IsReActivatingProVersionNeeded)
						this.reActivateProVersionAction.Schedule();
				}
			});

			// start stopwatch
			if (this.Application.IsDebugMode)
				this.stopwatch.Start();
		}


		/// <summary>
		/// Activate Pro version.
		/// </summary>
		public async void ActivateProVersion()
		{
			this.VerifyAccess();
			if (this.IsClosed)
				return;
			if (MainWindowToActivateProVersion != null)
			{
				MainWindowToActivateProVersion.Activate();
				return;
			}
			MainWindowToActivateProVersion = this;
			await this.Application.ProductManager.ActivateProductAsync(Products.Professional, this);
			MainWindowToActivateProVersion = null;
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


		// Reset title of current session.
		void ClearCurrentCustomSessionTitle() =>
			(this.tabControl.SelectedItem as TabItem)?.Let(it => this.ClearCustomSessionTitle(it));


		// Reset title of session.
		void ClearCustomSessionTitle(TabItem tabItem)
		{
			if (tabItem.DataContext is Session session)
				session.CustomTitle = null;
		}


		// Close current session tab item.
		void CloseCurrentSessionTabItem() =>
			(this.tabControl.SelectedItem as TabItem)?.Let(it => this.CloseSessionTabItem(it));


		// Close session tab.
		void CloseSessionTabItem(TabItem tabItem)
        {
			// find index and session
			if (this.DataContext is not Workspace workspace)
				return;
			if (tabItem.DataContext is not Session session)
				return;
			var index = this.tabItems.IndexOf(tabItem);
			if (index < 0)
				return;

			// select neighbor tab item
			if (this.tabControl.SelectedIndex == index)
			{
				if (index < this.tabItems.Count - 2)
					this.tabControl.SelectedIndex = (index + 1);
				else if (index > 0)
					this.tabControl.SelectedIndex = (index - 1);
				else if (!this.HasMultipleMainWindows)
					workspace.ActiveSession = workspace.CreateAndAttachSession();
			}

			// close session
			workspace.DetachAndCloseSession(session);
		}


		/// <summary>
		/// Show new main window.
		/// </summary>
		public void CreateMainWindow() =>
			this.Application.ShowMainWindow();


		// Create new TabItem for given session.
		void CreateSessionTabItem()
		{
			if (this.DataContext is not Workspace workspace)
				return;
			workspace.ActiveSession = workspace.CreateAndAttachSession();
			this.selectAndSetLogProfileAction.Reschedule(300);
		}
		TabItem CreateSessionTabItem(Session session)
		{
			// create header
			var header = this.sessionTabItemHeaderTemplate.Build(session);
			if (Platform.IsMacOS)
			{
				((Control)header).ContextMenu = null;
				(this.Application as App)?.Let(app => 
				{
					header.FindDescendantOfTypeAndName<Panel>("Content")?.Let(content =>
					{
						app.EnsureClosingToolTipIfWindowIsInactive(content);
					});
				});
			}

			// create session view
			var sessionView = new SessionView().Also(it =>
			{
				it.GetObservable(SessionView.AreAllTutorialsShownProperty).Subscribe(shown =>
				{
					if (shown)
						this.selectAndSetLogProfileAction.Schedule();
				});
				it.DataContext = session;
				it.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
				it.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
			});

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


		// Dispose tab item for session.
		void DisposeSessionTabItem(TabItem tabItem)
		{
			if (tabItem.DataContext is not Session)
				return;
			var startTime = this.stopwatch.IsRunning ? this.stopwatch.ElapsedMilliseconds : 0;
			tabItem.DataContext = null;
			if (startTime > 0)
            {
				var time = this.stopwatch.ElapsedMilliseconds;
				this.Logger.LogTrace($"Take {time - startTime} ms to detach session from tab item");
				startTime = time;
            }
			(tabItem.Content as IControl)?.Let(it => it.DataContext = null);
			(tabItem.Header as IControl)?.Let(it => it.DataContext = null);
			if (startTime > 0)
				this.Logger.LogTrace($"Take {this.stopwatch.ElapsedMilliseconds - startTime} ms to detach session from session view and header");
		}


		/// <summary>
		/// Edit application configuration.
		/// </summary>
		public void EditConfiguration()
		{
			var keys = new List<SettingKey>(SettingKey.GetDefinedKeys<AppSuite.ConfigurationKeys>());
			keys.AddRange(SettingKey.GetDefinedKeys<ConfigurationKeys>());
			_ = new SettingsEditorDialog()
			{
				SettingKeys = keys,
				Settings = this.Application.Configuration,
			}.ShowDialog(this);
		}


		// Edit PATH environment variable.
		void EditPathEnvironmentVariable() =>
			_ = new AppSuite.Controls.PathEnvVarEditorDialog().ShowDialog(this);


		/// <summary>
		/// Edit application persistent state.
		/// </summary>
		public void EditPersistentState()
		{
			var keys = new List<SettingKey>(this.Application.PersistentState.Keys);
#if !DEBUG
			keys.RemoveAll(it =>
			{
				return it.Name == "AgreedPrivacyPolicyVersion" || it.Name == "AgreedUserAgreementVersion";
			});
#endif
			_ = new SettingsEditorDialog()
			{
				SettingKeys = keys,
				Settings = this.Application.PersistentState,
			}.ShowDialog(this);
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
		public SessionView? FindSessionView(Session session) => this.FindSessionTabItem(session)?.Content as SessionView;


		// Find index of tab item by dragging position on window.
		int FindTabItemIndex(DragEventArgs e)
		{
			for (var i = this.tabItems.Count - 1; i >= 0; --i)
			{
				var header = this.tabItems[i].Header as Control;
				if (header == null)
					continue;
				var position = e.GetPosition(header);
				var bounds = header.Bounds;
				if (position.X >= 0 && position.Y >= 0 && position.X <= bounds.Width && position.Y <= bounds.Height)
					return i;
			}
			return -1;
		}


		// Move current session to new workspace.
		void MoveCurrentSessionToNewWorkspace() =>
			(this.tabControl.SelectedItem as TabItem)?.Let(it => this.MoveSessionToNewWorkspace(it));


		// Move given session to new workspace.
		void MoveSessionToNewWorkspace(TabItem tabItem)
        {
			// check state
			if (tabItem.DataContext is not Session session)
				return;

			// create new window
			if (!this.Application.ShowMainWindow(newWindow =>
            {
				if (newWindow.DataContext is Workspace newWorkspace)
					this.MoveSessionToNewWorkspace(session, newWorkspace);
			}))
			{
				this.Logger.LogError("Unable to create new main window for session to be moved");
				return;
			}
        }
		void MoveSessionToNewWorkspace(Session session, Workspace newWorkspace)
		{
			// find empty session
			var emptySession = newWorkspace.Sessions.FirstOrDefault();

			// transfer session
			newWorkspace.AttachSession(0, session);
			newWorkspace.ActiveSession = session;

			// close empty session
			if (emptySession != null)
				newWorkspace.DetachAndCloseSession(emptySession);
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


        // Attach to view-model.
        protected override void OnAttachToViewModel(Workspace workspace)
		{
			// call base
			base.OnAttachToViewModel(workspace);

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
			this.SetValue(HasMultipleSessionsProperty, workspace.Sessions.Count > 1);

			// attach to active session
			workspace.ActiveSession?.Let(it => this.AttachToActiveSession(it));

			// update system taskbar
			this.updateSysTaskBarAction.Schedule();
		}


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// cancel scheduled actions
			this.reActivateProVersionAction.Cancel();
			this.tabControlSelectionChangedAction.Cancel();

			// stop stopwatch
			this.stopwatch.Stop();

			// remove event handlers
			NetworkManager.Default.PropertyChanged -= this.OnNetworkManagerPropertyChanged;
			this.Application.ProductManager.ProductActivationChanged -= this.OnProductActivationChanged;
			this.Application.ProductManager.ProductStateChanged -= this.OnProductStateChanged;

			// stop checking network connection for product activation
			this.notifyNetworkConnForProductActivationAction.Cancel();

			// cancel activating Pro-version
			if (MainWindowToActivateProVersion == this)
				MainWindowToActivateProVersion = null;

			// call base
			base.OnClosed(e);
		}


		/// <inheritdoc/>
		protected override ApplicationInfo OnCreateApplicationInfo() => new AppInfo();


        // Detach from view-model.
        protected override void OnDetachFromViewModel(Workspace workspace)
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
			this.SetValue(HasMultipleSessionsProperty, false);

			// update system taskbar
			this.updateSysTaskBarAction.Execute();

			// call base
			base.OnDetachFromViewModel(workspace);
		}


		// Called when drag leave tab item.
		void OnDragLeaveTabItem(object? sender, TabItemEventArgs e)
		{
			if (e.Item is not TabItem tabItem)
				return;
			ItemInsertionIndicator.SetInsertingItemAfter(tabItem, false);
			ItemInsertionIndicator.SetInsertingItemBefore(tabItem, false);
		}


		// Called when drag over.
		void OnDragOverTabItem(object? sender, DragOnTabItemEventArgs e)
		{
			// check state
			if (e.Handled || e.Item is not TabItem tabItem)
				return;
			if (this.HasDialogs)
			{
				e.DragEffects = DragDropEffects.None;
				return;
			}
			
			// setup
			e.DragEffects = DragDropEffects.None;
			e.Handled = true;

			// handle file dragging
			if(e.Data.HasFileNames())
			{
				// select tab item
				if (e.ItemIndex < this.tabItems.Count - 1)
					this.tabControl.SelectedIndex = e.ItemIndex;

				// complete
				e.DragEffects = DragDropEffects.Copy;
				return;
			}

			// handle session dragging
			if (e.Data.TryGetData<Session>(DraggingSessionKey, out var session) 
				&& session != null 
				&& e.ItemIndex < this.tabItems.Count - 1)
			{
				// find source position
				var workspace = (Workspace)session.Owner.AsNonNull();
				var srcIndex = workspace.Sessions.IndexOf(session);
				if (srcIndex < 0)
					return;
				
				// select target position
				var targetIndex = e.PointerPosition.X <= e.HeaderVisual.Bounds.Width / 2
					? e.ItemIndex
					: e.ItemIndex + 1;
				
				// update insertion indicator
				if (workspace != this.DataContext
					|| (srcIndex != targetIndex && srcIndex + 1 != targetIndex))
				{
					var insertAfter = (targetIndex != e.ItemIndex);
					ItemInsertionIndicator.SetInsertingItemAfter(tabItem, insertAfter);
					ItemInsertionIndicator.SetInsertingItemBefore(tabItem, !insertAfter);
				}
				else
				{
					ItemInsertionIndicator.SetInsertingItemAfter(tabItem, false);
					ItemInsertionIndicator.SetInsertingItemBefore(tabItem, false);
				}
				
				// complete
				this.tabControl.ScrollHeaderIntoView(e.ItemIndex);
				e.DragEffects = DragDropEffects.Move;
				return;
			}
		}


		// Called when drop.
		async void OnDropOnTabItem(object? sender, DragOnTabItemEventArgs e)
		{
			// check state
			if (e.Handled || e.Item is not TabItem tabItem)
				return;
			
			// clear insertion indicators
			ItemInsertionIndicator.SetInsertingItemAfter(tabItem, false);
			ItemInsertionIndicator.SetInsertingItemBefore(tabItem, false);
			
			// drop files
			if (e.Data.HasFileNames())
			{
				// find tab and session view
				var sessionView = Global.Run(() =>
				{
					if (e.ItemIndex < this.tabItems.Count - 1)
						return this.tabItems[e.ItemIndex].Content as SessionView;
					if (this.DataContext is not Workspace workspace)
						return null;
					var session = workspace.CreateAndAttachSession();
					workspace.ActiveSession = session;
					return this.FindSessionView(session);
				});
				if (sessionView == null)
					return;

				// drop to session view
				await sessionView.DropAsync(e.KeyModifiers, e.Data);

				// complete
				e.Handled = true;
				return;
			}

			// drop session
			if (e.Data.TryGetData<Session>(DraggingSessionKey, out var session) 
				&& session != null 
				&& e.ItemIndex < this.tabItems.Count - 1)
			{
				// find source position
				var srcWorkspace = (Workspace)session.Owner.AsNonNull();
				var srcIndex = srcWorkspace.Sessions.IndexOf(session);
				if (srcIndex < 0)
					return;
				
				// select target position
				var targetIndex = e.PointerPosition.X <= e.HeaderVisual.Bounds.Width / 2
					? e.ItemIndex
					: e.ItemIndex + 1;
				
				// move session
				if (srcWorkspace == this.DataContext)
				{
					if (srcIndex != targetIndex && srcIndex + 1 != targetIndex)
					{
						if (srcIndex < targetIndex)
							srcWorkspace.MoveSession(srcIndex, targetIndex - 1);
						else
							srcWorkspace.MoveSession(srcIndex, targetIndex);
					}
				}
				else if (this.DataContext is Workspace targetWorkspace)
				{
					// attach to target workspace
					targetWorkspace.AttachSession(targetIndex, session);
					targetWorkspace.ActiveSession = session;
				}
				e.Handled = true;
				return;
			}
		}


		// Called when all initial dialogs of AppSuite closed.
        protected override void OnInitialDialogsClosed()
        {
            base.OnInitialDialogsClosed();
			this.selectAndSetLogProfileAction.Schedule();
        }


        // Called when key down.
        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
		{
			// handle key event for combo keys
			if (!e.Handled && (e.KeyModifiers & KeyModifiers.Control) != 0)
			{
				switch (e.Key)
				{
					case Avalonia.Input.Key.N:
						if (!Platform.IsMacOS)
						{
							this.CreateMainWindow();
							e.Handled = true;
						}
						break;
					case Avalonia.Input.Key.T:
						if (!Platform.IsMacOS)
						{
							this.CreateSessionTabItem();
							e.Handled = true;
						}
						break;
					case Avalonia.Input.Key.Tab:
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
					case Avalonia.Input.Key.W:
						if (!Platform.IsMacOS)
						{
							this.CloseCurrentSessionTabItem();
							e.Handled = true;
						}
						break;
				}
			}

			// call base
			base.OnKeyDown(e);
		}


		// Called when property of network manager changed.
		void OnNetworkManagerPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is NetworkManager networkManager 
				&& e.PropertyName == nameof(NetworkManager.IsNetworkConnected))
			{
				if (networkManager.IsNetworkConnected)
					this.notifyNetworkConnForProductActivationAction.Cancel();
				else if (!this.Application.ProductManager.IsProductActivated(Products.Professional, true)
					&& !IsNetworkConnForProductActivationNotified)
				{
					this.notifyNetworkConnForProductActivationAction.Reschedule(this.Configuration.GetValueOrDefault(ConfigurationKeys.TimeoutToNotifyNetworkConnectionForProductActivation));
				}
			}
		}


		/// <inheritdoc/>
		protected override void OnOpened(EventArgs e)
		{
			// call base
			base.OnOpened(e);

			// add event handlers
			NetworkManager.Default.PropertyChanged += this.OnNetworkManagerPropertyChanged;
			this.Application.ProductManager.ProductActivationChanged += this.OnProductActivationChanged;
			this.Application.ProductManager.ProductStateChanged += this.OnProductStateChanged;
			this.OnProductStateChanged(this.Application.ProductManager, Products.Professional);
			this.UpdateToolMenuItems();

			// check network connection for product activation
			if (!this.Application.ProductManager.IsProductActivated(Products.Professional, true)
				&& !NetworkManager.Default.IsNetworkConnected
				&& !IsNetworkConnForProductActivationNotified)
			{
				this.notifyNetworkConnForProductActivationAction.Schedule(this.Configuration.GetValueOrDefault(ConfigurationKeys.TimeoutToNotifyNetworkConnectionForProductActivation));
			}

			// setup for demo
#if DEMO
			this.SynchronizationContext.Post(() =>
			{
				this.WindowState = WindowState.Normal;
				(this.Screens.ScreenFromWindow(this.PlatformImpl) ?? this.Screens.Primary)?.Let(screen =>
				{
					var workingArea = screen.WorkingArea;
					var w = (workingArea.Width * 0.95) / 4;
					var h = (workingArea.Height * 0.95) / 3;
					var u = Math.Min(w, h);
					var sysDecorSizes = this.GetSystemDecorationSizes();
					if (Platform.IsNotMacOS)
						u /= screen.PixelDensity;
					this.Width = u * 4;
					this.Height = this.ExtendClientAreaToDecorationsHint
						? u * 3
						: u * 3 - sysDecorSizes.Top - sysDecorSizes.Bottom;
				});
			});
#endif
		}


		// Called when product ativation state changed.
		void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
		{
			if (productId != Products.Professional)
				return;
			this.UpdateToolMenuItems();
		}


		// Called when product state changed.
		void OnProductStateChanged(IProductManager productManager, string productId)
		{
			if (productId != Products.Professional
				|| !productManager.TryGetProductState(productId, out var state))
			{
				return;
			}
			switch (state)
			{
				case ProductState.Activated:
					this.notifyNetworkConnForProductActivationAction.Cancel();
					goto default;
				case ProductState.Deactivated:
					if (MainWindowToActivateProVersion == null
						&& productManager.TryGetProductActivationFailure(productId, out var failure)
						&& failure != ProductActivationFailure.NoNetworkConnection
						&& !this.isReActivatingProVersion)
					{
						this.Logger.LogWarning($"Need to reactivate Pro-version because of {failure}");
						IsReActivatingProVersionNeeded = true;
						this.reActivateProVersionAction.Schedule();
					}
					break;
				default:
					IsReActivatingProVersionNeeded = false;
					this.reActivateProVersionAction.Cancel();
					break;
			}
		}


        // Called when list of session changed.
        void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (this.DataContext is not Workspace workspace)
				return;
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
				case NotifyCollectionChangedAction.Move:
					this.tabItems.Move(e.OldStartingIndex, e.NewStartingIndex);
					break;
				case NotifyCollectionChangedAction.Remove:
					{
						var startIndex = e.OldStartingIndex;
						var count = e.OldItems.AsNonNull().Count;
						for (var i = count - 1; i >= 0; --i)
						{
							this.DisposeSessionTabItem((TabItem)this.tabItems[startIndex + i].AsNonNull());
							var startTime = this.stopwatch.IsRunning ? this.stopwatch.ElapsedMilliseconds : 0;
							this.tabItems.RemoveAt(startIndex + i);
							if (startTime > 0)
								this.Logger.LogTrace($"Take {this.stopwatch.ElapsedMilliseconds - startTime} ms to remove tab from position {startIndex + i}");
						}
						if (workspace.Sessions.IsEmpty() && this.HasMultipleMainWindows)
						{
							this.Logger.LogWarning("Close window because all sessions were closed");
							this.Close();
						}
						else
							this.SynchronizationContext.Post(this.SelectActiveSessionIfNeeded);
					}
					break;
				default:
					throw new InvalidOperationException($"Unsupported changed of list of Sessions: {e.Action}.");
			}
			this.SetValue(HasMultipleSessionsProperty, workspace.Sessions.Count > 1);
		}


		// Called when selection of tab control has been changed.
		void OnTabControlSelectionChanged()
		{
			if (this.DataContext is not Workspace workspace)
				return;
			var index = this.tabControl.SelectedIndex;
			if (index == this.tabItems.Count - 1)
				this.CreateSessionTabItem();
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
					workspace.ActiveSession = workspace.CreateAndAttachSession();
			}

			// close session
			workspace.DetachAndCloseSession((Session)tabItem.DataContext.AsNonNull());
		}


		// Called when tab item dragged.
		void OnTabItemDragged(object? sender, TabItemDraggedEventArgs e)
		{
			// dragging is not supported properly on Linux
			if (Platform.IsLinux)
				return;

			// get session
			var session = (e.Item as TabItem)?.DataContext as Session;
			if (session == null)
				return;
			
			// prepare dragging data
			var data = new DataObject();
			data.Set(DraggingSessionKey, session);

			// start dragging session
			DragDrop.DoDragDrop(e.PointerEventArgs, data, DragDropEffects.Move);
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


		/// <summary>
		/// Purchase Pro version.
		/// </summary>
		public void PurchaseProVersion()
		{
			this.VerifyAccess();
			if (this.IsClosed)
				return;
			this.Application.ProductManager.PurchaseProduct(Products.Professional, this);
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
				workspace.ActiveSession = workspace.CreateAndAttachSession();
		}


		// Select log profile and set if current session has no log profile.
		void SelectAndSetLogProfile()
		{
			// check state
			if (this.IsClosed || this.HasDialogs || !this.AreInitialDialogsClosed)
				return;
			if (this.DataContext is not Workspace workspace)
				return;
			var sessionView = workspace.ActiveSession?.Let(it =>
				this.FindSessionView(it));
			if (sessionView?.AreAllTutorialsShown == false 
				|| sessionView?.IsHandlingDragAndDrop == true)
			{
				return;
			}

			// select and set log profile
			workspace.ActiveSession?.Let(it =>
			{
				if (it.LogProfile == null
					&& this.Settings.GetValueOrDefault(SettingKeys.SelectLogProfileForNewSession)
					&& !this.HasDialogs)
				{
					this.FindSessionView(it)?.SelectAndSetLogProfile();
				}
			});
		}


		// Set title of current session.
		void SetCurrentCustomSessionTitle() =>
			(this.tabControl.SelectedItem as TabItem)?.Let(it => this.SetCustomSessionTitle(it));


		// Set title of session.
		async void SetCustomSessionTitle(TabItem tabItem)
		{
			// find index and session
			if (tabItem.DataContext is not Session session)
				return;

			// select name
			var title = await new AppSuite.Controls.TextInputDialog()
			{
				InitialText = session.CustomTitle,
				MaxTextLength = 128,
				Message = this.GetResourceObservable("String/MainWindow.SetCustomSessionTitle.Message"),
			}.ShowDialog(this);
			if (string.IsNullOrWhiteSpace(title))
				return;

			// set title
			session.CustomTitle = title;
		}


		/// <summary>
		/// Show application options.
		/// </summary>
		public async void ShowAppOptions()
		{
			switch (await new AppOptionsDialog().ShowDialog<AppSuite.Controls.ApplicationOptionsDialogResult>(this))
			{
				case AppSuite.Controls.ApplicationOptionsDialogResult.RestartApplicationNeeded:
					this.Logger.LogWarning("Restart application");
					if (this.Application.IsDebugMode)
						this.Application.Restart($"{App.DebugArgument} {App.RestoreMainWindowsArgument}", this.Application.IsRunningAsAdministrator);
					else
						this.Application.Restart(App.RestoreMainWindowsArgument, this.Application.IsRunningAsAdministrator);
					break;
				case AppSuite.Controls.ApplicationOptionsDialogResult.RestartMainWindowsNeeded:
					this.Logger.LogWarning("Restart main windows");
					this.Application.RestartMainWindows();
					break;
			}
		}


		// Show dialog to manage script log data source providers.
		void ShowScriptLogDataSourceProvidersDialog() =>
			_ = new ScriptLogDataSourceProvidersDialog().ShowDialog(this);


		// Update menu items of tools.
		void UpdateToolMenuItems()
		{
		}
	}
}
