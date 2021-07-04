using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to select <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileSelectionDialog : BaseDialog
	{
		/// <summary>
		/// Property of <see cref="AllLogProfiles"/>.
		/// </summary>
		public static readonly AvaloniaProperty<IList<LogProfile>> AllLogProfilesProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, IList<LogProfile>>(nameof(AllLogProfiles), new LogProfile[0]);
		/// <summary>
		/// Property of <see cref="HasPinnedLogProfiles"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> HasPinnedLogProfilesProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, bool>(nameof(HasPinnedLogProfiles), false);
		/// <summary>
		/// Property of <see cref="HasSelectedLogProfile"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> HasSelectedLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, bool>(nameof(HasSelectedLogProfile), false);
		/// <summary>
		/// Property of <see cref="PinnedLogProfiles"/>.
		/// </summary>
		public static readonly AvaloniaProperty<IList<LogProfile>> PinnedLogProfilesProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, IList<LogProfile>>(nameof(PinnedLogProfiles), new LogProfile[0]);


		// Fields.
		readonly ListBox allLogProfileListBox;
		readonly SortedObservableList<LogProfile> allLogProfiles = new SortedObservableList<LogProfile>(CompareLogProfiles);
		readonly ListBox pinnedLogProfileListBox;
		readonly SortedObservableList<LogProfile> pinnedLogProfiles = new SortedObservableList<LogProfile>(CompareLogProfiles);


		/// <summary>
		/// Initialize new <see cref="LogProfileSelectionDialog"/>.
		/// </summary>
		public LogProfileSelectionDialog()
		{
			// setup properties
			this.SetValue<IList<LogProfile>>(AllLogProfilesProperty, this.allLogProfiles.AsReadOnly());
			this.SetValue<IList<LogProfile>>(PinnedLogProfilesProperty, this.pinnedLogProfiles.AsReadOnly());

			// initialize
			this.InitializeComponent();

			// setup controls
			this.allLogProfileListBox = this.FindControl<ListBox>("allLogProfileListBox").AsNonNull();
			this.pinnedLogProfileListBox = this.FindControl<ListBox>("pinnedLogProfileListBox").AsNonNull();

			// attach to log profiles
			((INotifyCollectionChanged)LogProfiles.All).CollectionChanged += this.OnAllLogProfilesChanged;
			((INotifyCollectionChanged)LogProfiles.Pinned).CollectionChanged += this.OnPinnedLogProfilesChanged;
			this.allLogProfiles.AddAll(LogProfiles.All);
			this.pinnedLogProfiles.AddAll(LogProfiles.Pinned);
			this.SetValue<bool>(HasPinnedLogProfilesProperty, this.pinnedLogProfiles.IsNotEmpty());
		}


		/// <summary>
		/// Get all log profiles.
		/// </summary>
		public IList<LogProfile> AllLogProfiles { get => this.GetValue<IList<LogProfile>>(AllLogProfilesProperty); }


		// Compare log profiles.
		static int CompareLogProfiles(LogProfile? x, LogProfile? y)
		{
			if (x == null || y == null)
				return 0;
			var result = x.Name.CompareTo(y.Name);
			if (result != 0)
				return result;
			return x.GetHashCode() - y.GetHashCode();
		}


		/// <summary>
		/// Check whether at least one log profile is pinned or not.
		/// </summary>
		public bool HasPinnedLogProfiles { get => this.GetValue<bool>(HasPinnedLogProfilesProperty); }


		/// <summary>
		/// Check whether one log profile is selected or not.
		/// </summary>
		public bool HasSelectedLogProfile { get => this.GetValue<bool>(HasSelectedLogProfileProperty); }


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when clicking add log profile.
		void OnAddLogProfileClick(object? sender, RoutedEventArgs e)
		{
			//
		}


		// Called when list of all log profiles changed.
		void OnAllLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (LogProfile logProfile in e.NewItems.AsNonNull())
						this.allLogProfiles.Add(logProfile);
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (LogProfile logProfile in e.OldItems.AsNonNull())
						this.allLogProfiles.Remove(logProfile);
					break;
			}
		}


		// Called when selection in all log profiles changed.
		void OnAllLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.allLogProfileListBox.SelectedIndex >= 0)
				this.pinnedLogProfileListBox.SelectedIndex = -1;
			this.SetValue<bool>(HasSelectedLogProfileProperty, this.allLogProfileListBox.SelectedIndex >= 0 || this.pinnedLogProfileListBox.SelectedIndex >= 0);
		}


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// detach from log profiles
			((INotifyCollectionChanged)LogProfiles.All).CollectionChanged -= this.OnAllLogProfilesChanged;
			((INotifyCollectionChanged)LogProfiles.Pinned).CollectionChanged -= this.OnPinnedLogProfilesChanged;

			// call base
			base.OnClosed(e);
		}


		// Called when clicking OK.
		void OnOKClick(object? sender, RoutedEventArgs e)
		{
			var logProfile = this.allLogProfileListBox.SelectedItem as LogProfile;
			if (logProfile == null)
				logProfile = this.pinnedLogProfileListBox.SelectedItem as LogProfile;
			this.Close(logProfile);
		}


		// Called when list of pinned log profiles changed.
		void OnPinnedLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (LogProfile logProfile in e.NewItems.AsNonNull())
						this.pinnedLogProfiles.Add(logProfile);
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (LogProfile logProfile in e.OldItems.AsNonNull())
						this.pinnedLogProfiles.Remove(logProfile);
					break;
			}
			this.SetValue<bool>(HasPinnedLogProfilesProperty, this.pinnedLogProfiles.IsNotEmpty());
		}


		// Called when selection in pinned log profiles changed.
		void OnPinnedLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.pinnedLogProfileListBox.SelectedIndex >= 0)
				this.allLogProfileListBox.SelectedIndex = -1;
			this.SetValue<bool>(HasSelectedLogProfileProperty, this.allLogProfileListBox.SelectedIndex >= 0 || this.pinnedLogProfileListBox.SelectedIndex >= 0);
		}


		/// <summary>
		/// Get pinned log profiles.
		/// </summary>
		public IList<LogProfile> PinnedLogProfiles { get => this.GetValue<IList<LogProfile>>(PinnedLogProfilesProperty); }
	}
}
