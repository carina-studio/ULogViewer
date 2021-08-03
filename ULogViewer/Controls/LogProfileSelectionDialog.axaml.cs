using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

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
		readonly ScrollViewer scrollViewer;


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
			this.scrollViewer = this.FindControl<ScrollViewer>("scrollViewer").AsNonNull();

			// attach to log profiles
			((INotifyCollectionChanged)LogProfiles.All).CollectionChanged += this.OnAllLogProfilesChanged;
			this.RefreshLogProfiles();
		}


		// Add log profile.
		async void AddLogProfile()
		{
			// create new profile
			var profile = await new LogProfileEditorDialog().ShowDialog<LogProfile>(this);
			if (profile == null)
				return;

			// add profile
			LogProfiles.Add(profile);
			this.otherLogProfileListBox.SelectedItem = profile;
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


		// Export log profile.
		async void ExportLogProfile(LogProfile? logProfile)
		{
			// check parameter
			if (logProfile == null)
				return;

			// select file
			var fileName = await new SaveFileDialog().Also(it =>
			{
				it.Filters.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this);
			if (string.IsNullOrEmpty(fileName))
				return;

			// copy profile and save
			var copiedProfile = new LogProfile(logProfile);
			try
			{
				await copiedProfile.SaveAsync(fileName);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Unable to export log profile '{copiedProfile.Name}' to '{fileName}'");
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = this.Application.GetFormattedString("LogProfileSelectionDialog.FailedToExportLogProfile", fileName),
				}.ShowDialog(this);
			}
		}


		/// <summary>
		/// Get or set <see cref="Predicate{T}"/> to filter log profiles.
		/// </summary>
		public Predicate<LogProfile>? Filter
		{
			get => this.GetValue<Predicate<LogProfile>?>(FilterProperty);
			set => this.SetValue<Predicate<LogProfile>?>(FilterProperty, value);
		}


		// Import log profile.
		async void ImportLogProfile()
		{
			// select file
			var fileNames = await new OpenFileDialog().Also(it =>
			{
				it.Filters.Add(new FileDialogFilter().Also(filter =>
				{
					filter.Extensions.Add("json");
					filter.Name = this.Application.GetString("FileFormat.Json");
				}));
			}).ShowAsync(this);
			if (fileNames == null || fileNames.IsEmpty())
				return;

			// find current log profile
			var fileName = fileNames[0];
			var comparer = PathEqualityComparer.Default;
			var logProfile = this.pinnedLogProfiles.FirstOrDefault(it => comparer.Equals(it.FileName, fileName));
			if (logProfile != null)
			{
				this.pinnedLogProfileListBox.SelectedItem = logProfile;
				return;
			}
			logProfile = this.otherLogProfiles.FirstOrDefault(it => comparer.Equals(it.FileName, fileName));
			if (logProfile != null)
			{
				this.otherLogProfileListBox.SelectedItem = logProfile;
				return;
			}

			// load log profile
			try
			{
				logProfile = await LogProfile.LoadProfileAsync(this.Application, fileName);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Unable to load log profile from '{fileName}'");
				_ = new MessageDialog()
				{
					Icon = MessageDialogIcon.Error,
					Message = this.Application.GetFormattedString("LogProfileSelectionDialog.FailedToImportLogProfile", fileName),
				}.ShowDialog(this);
				return;
			}
			if (this.IsClosed)
				return;

			// edit log profile
			logProfile.DetachFromFile();
			logProfile.IsPinned = false;
			logProfile = await new LogProfileEditorDialog()
			{
				LogProfile = logProfile
			}.ShowDialog<LogProfile>(this);
			if (logProfile == null)
				return;

			// add log profile
			LogProfiles.Add(logProfile);
			this.otherLogProfileListBox.SelectedItem = logProfile;
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


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
			this.SynchronizationContext.Post(() =>
			{
				if (pinnedLogProfiles.IsNotEmpty())
					this.pinnedLogProfileListBox.Focus();
				else if (this.otherLogProfiles.IsNotEmpty())
					this.otherLogProfileListBox.Focus();
				else
					this.Close();
			});
		}


		// Called when selection in other log profiles changed.
		void OnOtherLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.otherLogProfileListBox.SelectedIndex >= 0)
				this.pinnedLogProfileListBox.SelectedIndex = -1;
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		// Called when selection in pinned log profiles changed.
		void OnPinnedLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.pinnedLogProfileListBox.SelectedIndex >= 0)
				this.otherLogProfileListBox.SelectedIndex = -1;
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
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


		// Scroll to selected log profile.
		void ScrollToSelectedLogProfile()
		{
			this.SynchronizationContext.PostDelayed(() =>
			{
				// find list box item
				var listBoxItem = (ListBoxItem?)null;
				var logProfile = this.pinnedLogProfileListBox.SelectedItem as LogProfile;
				if (logProfile != null)
					listBoxItem = this.pinnedLogProfileListBox.FindListBoxItem(logProfile);
				else
				{
					logProfile = this.otherLogProfileListBox.SelectedItem as LogProfile;
					if (logProfile != null)
						listBoxItem = this.otherLogProfileListBox.FindListBoxItem(logProfile);
				}
				if (listBoxItem == null)
					return;

				// scroll to list box item
				this.scrollViewer.ScrollIntoView(listBoxItem);
			}, 100);
		}
	}
}
