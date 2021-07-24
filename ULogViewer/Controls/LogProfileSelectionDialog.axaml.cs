using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to select <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileSelectionDialog : BaseDialog
	{
		// Static fields.
		static readonly AvaloniaProperty<Predicate<LogProfile>?> FilterProperty = AvaloniaProperty.Register<LogProfileEditorDialog, Predicate<LogProfile>?>(nameof(Filter));


		// Fields.
		readonly HashSet<LogProfile> attachedLogProfiles = new HashSet<LogProfile>();
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

			// initialize
			this.InitializeComponent();

			// setup controls
			this.otherLogProfileListBox = this.FindControl<ListBox>("otherLogProfileListBox").AsNonNull();
			this.pinnedLogProfileListBox = this.FindControl<ListBox>("pinnedLogProfileListBox").AsNonNull();

			// attach to log profiles
			((INotifyCollectionChanged)LogProfiles.All).CollectionChanged += this.OnAllLogProfilesChanged;
			this.RefreshLogProfiles();
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


		// Copy log profile.
		void CopyLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			var newProfile = new LogProfile(logProfile);
			newProfile.Name = $"{logProfile.Name} (2)";
			LogProfiles.Add(newProfile);
		}


		// Edit log profile.
		async void EditLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			await new LogProfileEditorDialog()
			{
				LogProfile = logProfile
			}.ShowDialog(this);
		}


		/// <summary>
		/// Get or set <see cref="Predicate{T}"/> to filter log profiles.
		/// </summary>
		public Predicate<LogProfile>? Filter
		{
			get => this.GetValue<Predicate<LogProfile>?>(FilterProperty);
			set => this.SetValue<Predicate<LogProfile>?>(FilterProperty, value);
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when clicking add log profile.
		async void OnAddLogProfileClick(object? sender, RoutedEventArgs e)
		{
			// create new profile
			var profile = await new LogProfileEditorDialog().ShowDialog<LogProfile>(this);
			if (profile == null)
				return;
		}


		// Called when list of all log profiles changed.
		void OnAllLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					var filter = this.Filter;
					foreach (LogProfile logProfile in e.NewItems.AsNonNull())
					{
						if (filter != null && !filter(logProfile))
							continue;
						if (!this.attachedLogProfiles.Add(logProfile))
							continue;
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
						if (!this.attachedLogProfiles.Remove(logProfile))
							continue;
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
			foreach (var profile in this.attachedLogProfiles)
				profile.PropertyChanged -= this.OnLogProdilePropertyChanged;
			this.attachedLogProfiles.Clear();

			// call base
			base.OnClosed(e);
		}


		// Generate result.
		protected override object? OnGenerateResult()
		{
			var logProfile = this.otherLogProfileListBox.SelectedItem as LogProfile;
			if (logProfile == null)
				logProfile = this.pinnedLogProfileListBox.SelectedItem as LogProfile;
			return logProfile;
		}


		// Called when double tapped on item of log profile.
		void OnLogProfileItemDoubleTapped(object? sender, RoutedEventArgs e) => this.GenerateResultCommand.TryExecute();


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


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			if (this.otherLogProfiles.IsEmpty() && this.pinnedLogProfiles.IsEmpty())
				this.SynchronizationContext.Post(this.Close);
		}


		// Called when selection in other log profiles changed.
		void OnOtherLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.otherLogProfileListBox.SelectedIndex >= 0)
				this.pinnedLogProfileListBox.SelectedIndex = -1;
			this.InvalidateInput();
		}


		// Called when selection in pinned log profiles changed.
		void OnPinnedLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.pinnedLogProfileListBox.SelectedIndex >= 0)
				this.otherLogProfileListBox.SelectedIndex = -1;
			this.InvalidateInput();
		}


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == FilterProperty)
				this.RefreshLogProfiles();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && (this.pinnedLogProfileListBox.SelectedItem != null || this.otherLogProfileListBox.SelectedItem != null);
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


		// Refresh log profiles.
		void RefreshLogProfiles()
		{
			if (this.IsClosed)
				return;
			var filter = this.Filter;
			if (filter == null)
			{
				foreach (var profile in LogProfiles.All)
				{
					if (!this.attachedLogProfiles.Add(profile))
						continue;
					if (profile.IsPinned)
						this.pinnedLogProfiles.Add(profile);
					else
						this.otherLogProfiles.Add(profile);
					profile.PropertyChanged += this.OnLogProdilePropertyChanged;
				}
			}
			else
			{
				foreach (var profile in this.attachedLogProfiles)
				{
					if (!filter(profile))
					{
						this.attachedLogProfiles.Remove(profile);
						this.otherLogProfiles.Remove(profile);
						this.pinnedLogProfiles.Remove(profile);
						profile.PropertyChanged -= this.OnLogProdilePropertyChanged;
					}
				}
				foreach (var profile in LogProfiles.All)
				{
					if (!filter(profile) || !this.attachedLogProfiles.Add(profile))
						continue;
					if (profile.IsPinned)
						this.pinnedLogProfiles.Add(profile);
					else
						this.otherLogProfiles.Add(profile);
					profile.PropertyChanged += this.OnLogProdilePropertyChanged;
				}
			}
		}


		// Remove log profile.
		void RemoveLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			LogProfiles.Remove(logProfile);
		}
	}
}
