using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Controls;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
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
		readonly ScheduledAction focusOnTabItemContentAction;
		readonly ScheduledAction reAttachToWorkspaceAction;
		readonly ScheduledAction saveWindowSizeAction;
		readonly DataTemplate sessionTabItemHeaderTemplate;
		readonly TabControl tabControl;
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
			this.tabControl = this.FindControl<TabControl>("tabControl").AsNonNull().Also(it =>
			{
				it.SelectionChanged += this.OnTabControlSelectionChanged;
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
					this.Settings.SetValue(WindowWidthSettingKey, (int)(this.Width + 0.5));
					this.Settings.SetValue(WindowHeightSettingKey, (int)(this.Height + 0.5));
				}
			});

			// restore window state
			this.Settings.Let(it =>
			{
				this.Height = Math.Max(0, it.GetValueOrDefault(WindowHeightSettingKey));
				this.Width = Math.Max(0, it.GetValueOrDefault(WindowWidthSettingKey));
				this.WindowState = it.GetValueOrDefault(WindowStateSettingKey);
			});

			// add handlers
			this.Settings.SettingChanged += this.OnSettingChanged;
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


		// Detach from workspace.
		void DetachFronWorkspace(Workspace workspace)
		{
			// remove event handlers
			workspace.PropertyChanged -= this.OnWorkspacePropertyChanged;
			((INotifyCollectionChanged)workspace.Sessions).CollectionChanged -= this.OnSessionsChanged;

			// remove session tab items
			for (var i = this.tabItems.Count - 2; i >= 0; --i)
			{
				this.DisposeSessionTabItem((TabItem)this.tabItems[i].AsNonNull());
				this.tabItems.RemoveAt(i);
			}
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


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// remove handlers
			this.Settings.SettingChanged -= this.OnSettingChanged;

			// call base
			base.OnClosed(e);
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
					this.Settings.SetValue(WindowStateSettingKey, this.WindowState);
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
		void OnTabControlSelectionChanged(object? sender, RoutedEventArgs e)
		{
			if (this.DataContext is not Workspace workspace)
				return;
			var index = this.tabControl.SelectedIndex;
			if (index == this.tabItems.Count - 1)
				workspace.ActiveSession = workspace.CreateSession();
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
				var index = (workspace.ActiveSession != null ? workspace.Sessions.IndexOf(workspace.ActiveSession) : -1);
				if (index >= 0)
					this.tabControl.SelectedIndex = index;
				else
					this.SynchronizationContext.Post(this.SelectActiveSessionIfNeeded);
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
	}
}
