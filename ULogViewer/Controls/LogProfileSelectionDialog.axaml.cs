using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.Profiles;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to select <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileSelectionDialog : BaseDialog
	{
		// Static fields.
		static readonly AvaloniaProperty<bool> HasSelectedLogProfileProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, bool>(nameof(HasSelectedLogProfile), false);
		

		// Fields.
		readonly ListBox otherLogProfileListBox;
		readonly SortedObservableList<LogProfile> otherLogProfiles = new SortedObservableList<LogProfile>(CompareLogProfiles);
		readonly ListBox pinnedLogProfileListBox;
		readonly SortedObservableList<LogProfile> pinnedLogProfiles = new SortedObservableList<LogProfile>(CompareLogProfiles);


		/// <summary>
		/// Initialize new <see cref="LogProfileSelectionDialog"/>.
		/// </summary>
		public LogProfileSelectionDialog()
		{
			// setup properties
			this.OtherLogProfiles = this.otherLogProfiles.AsReadOnly();
			this.PinnedLogProfiles = this.pinnedLogProfiles.AsReadOnly();

			// create commends
			this.ConfirmSelectedLogProfileCommand = ReactiveCommand.Create(this.ConfirmSelectedLogProfile, this.GetObservable<bool>(HasSelectedLogProfileProperty));

			// initialize
			this.InitializeComponent();

			// setup controls
			this.otherLogProfileListBox = this.FindControl<ListBox>("otherLogProfileListBox").AsNonNull();
			this.pinnedLogProfileListBox = this.FindControl<ListBox>("pinnedLogProfileListBox").AsNonNull();

			// attach to log profiles
			((INotifyCollectionChanged)LogProfiles.All).CollectionChanged += this.OnAllLogProfilesChanged;
			foreach(var profile in LogProfiles.All)
			{
				if (profile.IsPinned)
					this.pinnedLogProfiles.Add(profile);
				else
					this.otherLogProfiles.Add(profile);
				profile.PropertyChanged += this.OnLogProdilePropertyChanged;
			}
		}


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


		// Confirm selected log profile and close dialog.
		void ConfirmSelectedLogProfile()
		{
			var logProfile = this.otherLogProfileListBox.SelectedItem as LogProfile;
			if (logProfile == null)
			{
				logProfile = this.pinnedLogProfileListBox.SelectedItem as LogProfile;
				if (logProfile == null)
					return;
			}
			this.Close(logProfile);
		}


		/// <summary>
		/// Command to confirm selected log profile and close dialog.
		/// </summary>
		ICommand ConfirmSelectedLogProfileCommand { get; }


		// Edit log profile.
		void EditLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			//
		}


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
					{
						if (logProfile.IsPinned)
							this.pinnedLogProfiles.Add(logProfile);
						else
							this.otherLogProfiles.Add(logProfile);
						logProfile.PropertyChanged += this.OnLogProdilePropertyChanged;
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (LogProfile logProfile in e.OldItems.AsNonNull())
					{
						logProfile.PropertyChanged -= this.OnLogProdilePropertyChanged;
						this.otherLogProfiles.Remove(logProfile);
						this.pinnedLogProfiles.Remove(logProfile);
					}
					break;
			}
		}


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// detach from log profiles
			((INotifyCollectionChanged)LogProfiles.All).CollectionChanged -= this.OnAllLogProfilesChanged;
			foreach (var profile in LogProfiles.All)
				profile.PropertyChanged -= this.OnLogProdilePropertyChanged;

			// call base
			base.OnClosed(e);
		}


		// Called when double tapped on item of log profile.
		void OnLogProfileItemDoubleTapped(object? sender, RoutedEventArgs e) => this.ConfirmSelectedLogProfile();


		// Called when property of log profile has been changed.
		void OnLogProdilePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not LogProfile profile)
				return;
			switch (e.PropertyName)
			{
				case nameof(LogProfile.IsPinned):
					if (profile.IsPinned)
					{
						this.otherLogProfiles.Remove(profile);
						this.pinnedLogProfiles.Add(profile);
					}
					else
					{
						this.pinnedLogProfiles.Remove(profile);
						this.otherLogProfiles.Add(profile);
					}
					break;
				case nameof(LogProfile.Name):
					this.otherLogProfiles.Sort(profile);
					this.pinnedLogProfiles.Sort(profile);
					break;
			}
		}


		// Called when selection in other log profiles changed.
		void OnOtherLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.otherLogProfileListBox.SelectedIndex >= 0)
				this.pinnedLogProfileListBox.SelectedIndex = -1;
			this.SetValue<bool>(HasSelectedLogProfileProperty, this.otherLogProfileListBox.SelectedIndex >= 0 || this.pinnedLogProfileListBox.SelectedIndex >= 0);
		}


		// Called when selection in pinned log profiles changed.
		void OnPinnedLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.pinnedLogProfileListBox.SelectedIndex >= 0)
				this.otherLogProfileListBox.SelectedIndex = -1;
			this.SetValue<bool>(HasSelectedLogProfileProperty, this.otherLogProfileListBox.SelectedIndex >= 0 || this.pinnedLogProfileListBox.SelectedIndex >= 0);
		}


		/// <summary>
		/// Get other log profiles.
		/// </summary>
		IList<LogProfile> OtherLogProfiles { get; }


		/// <summary>
		/// Get pinned log profiles.
		/// </summary>
		IList<LogProfile> PinnedLogProfiles { get; }


		// Pin/Unpin log profile.
		void PinUnpinLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			logProfile.IsPinned = !logProfile.IsPinned;
		}


		// Remove log profile.
		void RemoveLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			//
		}
	}
}
