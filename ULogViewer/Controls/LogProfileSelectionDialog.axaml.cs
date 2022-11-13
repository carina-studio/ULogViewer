using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to select <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileSelectionDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Static fields.
		static readonly StyledProperty<Predicate<LogProfile>?> FilterProperty = AvaloniaProperty.Register<LogProfileEditorDialog, Predicate<LogProfile>?>(nameof(Filter));
		static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, bool>("IsProVersionActivated");


		// Fields.
		readonly HashSet<LogProfile> attachedLogProfiles = new();
		readonly Avalonia.Controls.ListBox otherLogProfileListBox;
		readonly SortedObservableList<LogProfile> otherLogProfiles = new(CompareLogProfiles);
		readonly Avalonia.Controls.ListBox pinnedLogProfileListBox;
		readonly SortedObservableList<LogProfile> pinnedLogProfiles = new(CompareLogProfiles);
		readonly ScrollViewer scrollViewer;
		readonly Avalonia.Controls.ListBox templateLogProfileListBox;
		readonly SortedObservableList<LogProfile> templateLogProfiles = new(CompareLogProfiles);


		/// <summary>
		/// Initialize new <see cref="LogProfileSelectionDialog"/>.
		/// </summary>
		public LogProfileSelectionDialog()
		{
			// setup properties
			this.OtherLogProfiles = ListExtensions.AsReadOnly(this.otherLogProfiles);
			this.PinnedLogProfiles = ListExtensions.AsReadOnly(this.pinnedLogProfiles);
			this.TemplateLogProfiles = ListExtensions.AsReadOnly(this.templateLogProfiles);

			// setup commands
			this.CopyLogProfileCommand = new Command<LogProfile?>(this.CopyLogProfile);
			this.EditLogProfileCommand = new Command<LogProfile?>(this.EditLogProfile);
			this.ExportLogProfileCommand = new Command<LogProfile?>(this.ExportLogProfile);
			this.PinUnpinLogProfileCommand = new Command<LogProfile?>(this.PinUnpinLogProfile);
			this.RemoveLogProfileCommand = new Command<LogProfile?>(this.RemoveLogProfile);

			// initialize
			AvaloniaXamlLoader.Load(this);

			// setup controls
			this.otherLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(otherLogProfileListBox));
			this.pinnedLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(pinnedLogProfileListBox));
			this.scrollViewer = this.Get<ScrollViewer>(nameof(scrollViewer));
			this.templateLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(templateLogProfileListBox));

			// attach to log profiles
			((INotifyCollectionChanged)LogProfileManager.Default.Profiles).CollectionChanged += this.OnAllLogProfilesChanged;
			this.RefreshLogProfiles();
		}


		/// <summary>
		/// Add log profile.
		/// </summary>
		public async void AddLogProfile()
		{
			// create new profile
			var profile = await new LogProfileEditorDialog().ShowDialog<LogProfile>(this);
			if (profile == null)
				return;

			// add profile
			LogProfileManager.Default.AddProfile(profile);
			this.otherLogProfileListBox.SelectedItem = profile;
		}


		// Compare log profiles.
		static int CompareLogProfiles(LogProfile? x, LogProfile? y)
		{
			if (x == null || y == null)
				return 0;
			var result = string.Compare(x.Name, y.Name, true, CultureInfo.InvariantCulture);
			if (result != 0)
				return result;
			result = x.Id.CompareTo(y.Id);
			if (result != 0)
				return result;
			return x.GetHashCode() - y.GetHashCode();
		}


		// Copy log profile.
		async void CopyLogProfile(LogProfile? logProfile)
		{
			// check state
			if (logProfile == null)
				return;

			// copy and edit log profile
			var newProfile = await new LogProfileEditorDialog()
			{
				LogProfile = new LogProfile(logProfile).Also(it => it.Name = $"{logProfile.Name} (2)"),
			}.ShowDialog<LogProfile>(this);
			if (newProfile == null)
				return;

			// add log profile
			LogProfileManager.Default.AddProfile(newProfile);
			this.SelectLogProfile(newProfile);
		}


		/// <summary>
		/// Command to copy log profile.
		/// </summary>
		public ICommand CopyLogProfileCommand { get; }


		// Edit log profile.
		async void EditLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			logProfile = await new LogProfileEditorDialog()
			{
				LogProfile = logProfile
			}.ShowDialog<LogProfile>(this);
			if (logProfile == null)
				return;
			this.SelectLogProfile(logProfile);
		}


		/// <summary>
		/// Command to edit log profile.
		/// </summary>
		public ICommand EditLogProfileCommand { get; }


		// Export log profile.
		async void ExportLogProfile(LogProfile? logProfile)
		{
			// check parameter
			if (logProfile == null)
				return;

			// select file
			var fileName = (await this.StorageProvider.SaveFilePickerAsync(new()
			{
				FileTypeChoices = new FilePickerFileType[]
				{
					new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
					{
						Patterns = new string[] { "*.json" }
					}
				}
			}))?.Let(it =>
			{
				return it.TryGetUri(out var uri) ? uri.LocalPath : null;
			});
			if (string.IsNullOrEmpty(fileName))
				return;

			// copy profile and save
			var copiedProfile = new LogProfile(logProfile);
			try
			{
				await copiedProfile.SaveAsync(fileName, false);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to export log profile '{copiedProfileName}' to '{fileName}'", copiedProfile.Name, fileName);
				_ = new AppSuite.Controls.MessageDialog()
				{
					Icon = AppSuite.Controls.MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionDialog.FailedToExportLogProfile"));
					}),
				}.ShowDialog(this);
			}
		}


		/// <summary>
		/// Command to export log profile.
		/// </summary>
		public ICommand ExportLogProfileCommand { get; }


		/// <summary>
		/// Get or set <see cref="Predicate{T}"/> to filter log profiles.
		/// </summary>
		public Predicate<LogProfile>? Filter
		{
			get => this.GetValue<Predicate<LogProfile>?>(FilterProperty);
			set => this.SetValue<Predicate<LogProfile>?>(FilterProperty, value);
		}


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var logProfile = this.otherLogProfileListBox.SelectedItem as LogProfile
				?? this.pinnedLogProfileListBox.SelectedItem as LogProfile;
			return Task.FromResult((object?)logProfile);
		}


		/// <summary>
		/// Import log profile.
		/// </summary>
		public async void ImportLogProfile()
		{
			// select file
			var fileName = (await this.StorageProvider.OpenFilePickerAsync(new()
			{
				FileTypeFilter = new FilePickerFileType[]
				{
					new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
					{
						Patterns = new string[] { "*.json" }
					}
				}
			}))?.Let(it =>
			{
				return it.Count == 1 && it[0].TryGetUri(out var uri)
					? uri.LocalPath : null;
			});
			if (string.IsNullOrEmpty(fileName))
				return;

			// load log profile
			var logProfile = (LogProfile?)null;
			try
			{
				logProfile = await LogProfile.LoadAsync(this.Application, fileName);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to load log profile from '{fileName}'", fileName);
				_ = new AppSuite.Controls.MessageDialog()
				{
					Icon = AppSuite.Controls.MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionDialog.FailedToImportLogProfile"));
					}),
				}.ShowDialog(this);
				return;
			}
			if (this.IsClosed)
				return;
			
			// check pro-version only parameters
			if (!this.GetValue<bool>(IsProVersionActivatedProperty) && logProfile.DataSourceProvider.IsProVersionOnly)
			{
				_ = new AppSuite.Controls.MessageDialog()
				{
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionDialog.CannotImportProVersionOnlyLogProfile"));
					}),
				}.ShowDialog(this);
				return;
			}

			// edit log profile
			logProfile.IsPinned = false;
			logProfile = await new LogProfileEditorDialog()
			{
				LogProfile = logProfile
			}.ShowDialog<LogProfile>(this);
			if (logProfile == null)
				return;

			// add log profile
			LogProfileManager.Default.AddProfile(logProfile);
			this.otherLogProfileListBox.SelectedItem = logProfile;
		}


		// Called when list of all log profiles changed.
		void OnAllLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					var filter = this.Filter;
					foreach (LogProfile logProfile in e.NewItems!)
					{
						if (filter != null && !filter(logProfile))
							continue;
						if (!this.attachedLogProfiles.Add(logProfile))
							continue;
						if (logProfile.IsTemplate)
						{
							if (this.Filter == null)
								this.templateLogProfiles.Add(logProfile);
						}
						else if (logProfile.IsPinned)
							this.pinnedLogProfiles.Add(logProfile);
						else
							this.otherLogProfiles.Add(logProfile);
						logProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (LogProfile logProfile in e.OldItems.AsNonNull())
					{
						if (!this.attachedLogProfiles.Remove(logProfile))
							continue;
						logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
						this.otherLogProfiles.Remove(logProfile);
						this.pinnedLogProfiles.Remove(logProfile);
						this.templateLogProfiles.Remove(logProfile);
					}
					break;
			}
		}


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// detach from log profiles
			((INotifyCollectionChanged)LogProfileManager.Default.Profiles).CollectionChanged -= this.OnAllLogProfilesChanged;
			foreach (var profile in this.attachedLogProfiles)
				profile.PropertyChanged -= this.OnLogProfilePropertyChanged;
			this.attachedLogProfiles.Clear();

			// detach fron product manager
			this.Application.ProductManager.ProductStateChanged -= this.OnProductStateChanged;

			// call base
			base.OnClosed(e);
		}


		// Called when double tapped on item of log profile.
		void OnLogProfileItemDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e) => this.GenerateResultCommand.TryExecute();


		// Called when property of log profile has been changed.
		void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not LogProfile profile)
				return;
			switch (e.PropertyName)
			{
				case nameof(LogProfile.IconColor):
					Global.Run(() =>
					{
						if (profile.IsTemplate)
							return this.templateLogProfiles;
						if (profile.IsPinned)
							return pinnedLogProfiles;
						return otherLogProfiles;
					}).Let(it =>
					{
						it.Remove(profile);
						it.Add(profile);
					});
					break;
				case nameof(LogProfile.IsPinned):
					if (!profile.IsTemplate)
					{
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
					}
					break;
				case nameof(LogProfile.IsTemplate):
					if (profile.IsTemplate)
					{
						this.otherLogProfiles.Remove(profile);
						this.pinnedLogProfiles.Remove(profile);
						if (this.Filter == null)
							this.templateLogProfiles.Add(profile);
						profile.IsPinned = false;
					}
					else if (profile.IsPinned)
					{
						this.templateLogProfiles.Remove(profile);
						this.pinnedLogProfiles.Add(profile);
					}
					else
					{
						this.templateLogProfiles.Remove(profile);
						this.otherLogProfiles.Add(profile);
					}
					break;
				case nameof(LogProfile.Name):
					this.otherLogProfiles.Sort(profile);
					this.pinnedLogProfiles.Sort(profile);
					this.templateLogProfiles.Sort(profile);
					break;
			}
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			// attach to product manager
			this.Application.ProductManager.Let(it =>
			{
				this.SetValue<bool>(IsProVersionActivatedProperty, it.IsProductActivated(Products.Professional));
				it.ProductStateChanged += this.OnProductStateChanged;
			});

			// call base
			base.OnOpened(e);

			// setup focus
			this.SynchronizationContext.Post(() =>
			{
				if (pinnedLogProfiles.IsNotEmpty())
					this.pinnedLogProfileListBox.Focus();
				else if (this.otherLogProfiles.IsNotEmpty())
					this.otherLogProfileListBox.Focus();
				else if (this.templateLogProfiles.IsNotEmpty())
					this.templateLogProfileListBox.Focus();
				else
					this.Close();
			});
		}


		// Called when selection in other log profiles changed.
		void OnOtherLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.otherLogProfileListBox.SelectedIndex >= 0)
			{
				this.pinnedLogProfileListBox.SelectedIndex = -1;
				this.templateLogProfileListBox.SelectedIndex = -1;
			}
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		// Called when selection in pinned log profiles changed.
		void OnPinnedLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.pinnedLogProfileListBox.SelectedIndex >= 0)
			{
				this.otherLogProfileListBox.SelectedIndex = -1;
				this.templateLogProfileListBox.SelectedIndex = -1;
			}
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		// Called when stte of product changed.
		void OnProductStateChanged(IProductManager? productManager, string productId)
		{
			if (productManager != null && productId == Products.Professional)
			{
				this.SetValue<bool>(IsProVersionActivatedProperty, productManager.IsProductActivated(productId));
				this.InvalidateInput();
			}
		}


		// Called when property changed.
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == FilterProperty)
				this.RefreshLogProfiles();
		}


		// Called when selection in template log profiles changed.
		void OnTemplateLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.templateLogProfileListBox.SelectedIndex >= 0)
			{
				this.otherLogProfileListBox.SelectedIndex = -1;
				this.pinnedLogProfileListBox.SelectedIndex = -1;
			}
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			if (!base.OnValidateInput())
				return false;
			var selectedItem = (this.pinnedLogProfileListBox.SelectedItem ?? this.otherLogProfileListBox.SelectedItem);
			if (selectedItem is not LogProfile logProfile)
				return false;
			return !logProfile.DataSourceProvider.IsProVersionOnly || this.GetValue<bool>(IsProVersionActivatedProperty);
		}


		/// <summary>
		/// Get other log profiles.
		/// </summary>
		public IList<LogProfile> OtherLogProfiles { get; }


		/// <summary>
		/// Get pinned log profiles.
		/// </summary>
		public IList<LogProfile> PinnedLogProfiles { get; }


		// Pin/Unpin log profile.
		void PinUnpinLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			logProfile.IsPinned = !logProfile.IsPinned;
			this.SelectLogProfile(logProfile);
		}


		/// <summary>
		/// Command to pin/unpin log profile.
		/// </summary>
		public ICommand PinUnpinLogProfileCommand { get; }


		// Refresh log profiles.
		void RefreshLogProfiles()
		{
			if (this.IsClosed)
				return;
			var filter = this.Filter;
			if (filter == null)
			{
				foreach (var profile in LogProfileManager.Default.Profiles)
				{
					if (!this.attachedLogProfiles.Add(profile))
						continue;
					if (profile.IsTemplate)
						this.templateLogProfiles.Add(profile);
					else if (profile.IsPinned)
						this.pinnedLogProfiles.Add(profile);
					else
						this.otherLogProfiles.Add(profile);
					profile.PropertyChanged += this.OnLogProfilePropertyChanged;
				}
			}
			else
			{
				foreach (var profile in this.attachedLogProfiles)
				{
					if (!filter(profile) || profile.IsTemplate)
					{
						this.attachedLogProfiles.Remove(profile);
						this.otherLogProfiles.Remove(profile);
						this.pinnedLogProfiles.Remove(profile);
						this.templateLogProfiles.Remove(profile);
						profile.PropertyChanged -= this.OnLogProfilePropertyChanged;
					}
				}
				foreach (var profile in LogProfileManager.Default.Profiles)
				{
					if (!filter(profile) || !this.attachedLogProfiles.Add(profile))
						continue;
					if (!profile.IsTemplate)
					{
						if (profile.IsPinned)
							this.pinnedLogProfiles.Add(profile);
						else
							this.otherLogProfiles.Add(profile);
					}
					profile.PropertyChanged += this.OnLogProfilePropertyChanged;
				}
			}
		}


		// Remove log profile.
		async void RemoveLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			var result = await new MessageDialog()
			{
				Buttons = MessageDialogButtons.YesNo,
				Icon = MessageDialogIcon.Question,
				Message = new FormattedString().Also(it =>
				{
					it.Bind(FormattedString.Arg1Property, new Avalonia.Data.Binding() { Path = nameof(LogProfile.Name), Source = logProfile});
					it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/LogProfileSelectionDialog.ConfirmRemovingLogProfile"));
				}),
			}.ShowDialog(this);
			if (result == MessageDialogResult.Yes)
				LogProfileManager.Default.RemoveProfile(logProfile);
		}
		

		/// <summary>
		/// Command to remove log profile.
		/// </summary>
		public ICommand RemoveLogProfileCommand { get; }


		// Scroll to selected log profile.
		void ScrollToSelectedLogProfile()
		{
			this.SynchronizationContext.PostDelayed(() =>
			{
				// find list box item
				var listBoxItem = (ListBoxItem?)null;
				if (this.pinnedLogProfileListBox.SelectedItem is LogProfile pinnedLogProfile)
					this.pinnedLogProfileListBox.TryFindListBoxItem(pinnedLogProfile, out listBoxItem);
				else if (this.otherLogProfileListBox.SelectedItem is LogProfile otherLogProfile)
					this.otherLogProfileListBox.TryFindListBoxItem(otherLogProfile, out listBoxItem);
				else if (this.templateLogProfileListBox.SelectedItem is LogProfile templateLogProfile)
					this.templateLogProfileListBox.TryFindListBoxItem(templateLogProfile, out listBoxItem);
				if (listBoxItem == null)
					return;

				// scroll to list box item
				this.scrollViewer.ScrollIntoView(listBoxItem);
			}, 100);
		}


		// Select given log profile.
		void SelectLogProfile(LogProfile profile)
		{
			if (profile.IsTemplate)
				this.templateLogProfileListBox.SelectedItem = profile;
			else if (profile.IsPinned)
				this.pinnedLogProfileListBox.SelectedItem = profile;
			else
				this.otherLogProfileListBox.SelectedItem = profile;
			this.ScrollToSelectedLogProfile();
		}


		/// <summary>
		/// Get template log profiles.
		/// </summary>
		public IList<LogProfile> TemplateLogProfiles { get; }
	}
}
