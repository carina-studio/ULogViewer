using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to select <see cref="LogProfile"/>.
	/// </summary>
	class LogProfileSelectionDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		/// <summary>
		/// Converter to convert from <see cref="ILogDataSourceProvider"/> to bool which indicates whether the provider is Pro-version only or not.
		/// </summary>
		public static readonly IValueConverter ProDataSourceProviderConverter = new FuncValueConverter<ILogDataSourceProvider, bool>(provider =>
			provider is not null && (provider.IsProVersionOnly || provider is ScriptLogDataSourceProvider));
		
		
		// Static fields.
		static readonly StyledProperty<Predicate<LogProfile>?> FilterProperty = AvaloniaProperty.Register<LogProfileEditorDialog, Predicate<LogProfile>?>(nameof(Filter));
		static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, bool>(nameof(IsProVersionActivated));
		static readonly StyledProperty<string?> OtherLogProfilesPanelTitleProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, string?>(nameof(OtherLogProfilesPanelTitle));


		// Fields.
		readonly HashSet<LogProfile> attachedLogProfiles = new();
		readonly LogProfileManager logProfileManager = LogProfileManager.Default;
		readonly Avalonia.Controls.ListBox otherLogProfileListBox;
		readonly SortedObservableList<LogProfile> otherLogProfiles = new(CompareLogProfiles);
		readonly ToggleButton otherLogProfilesButton;
		readonly Panel otherLogProfilesPanel;
		readonly Avalonia.Controls.ListBox pinnedLogProfileListBox;
		readonly SortedObservableList<LogProfile> pinnedLogProfiles = new(CompareLogProfiles);
		readonly ToggleButton pinnedLogProfilesButton;
		readonly Panel pinnedLogProfilesPanel;
		readonly Avalonia.Controls.ListBox recentlyUsedLogProfileListBox;
		readonly SortedObservableList<LogProfile> recentlyUsedLogProfiles;
		readonly ToggleButton recentlyUsedLogProfilesButton;
		readonly Panel recentlyUsedLogProfilesPanel;
		readonly ScrollViewer scrollViewer;
		readonly Avalonia.Controls.ListBox templateLogProfileListBox;
		readonly SortedObservableList<LogProfile> templateLogProfiles = new(CompareLogProfiles);
		readonly ToggleButton templateLogProfilesButton;
		readonly Panel templateLogProfilesPanel;
		readonly ScheduledAction updateOtherLogProfilesPanelTitleAction;


		/// <summary>
		/// Initialize new <see cref="LogProfileSelectionDialog"/>.
		/// </summary>
		public LogProfileSelectionDialog()
		{
			// setup properties
			this.HasNavigationBar = true;
			this.OtherLogProfiles = ListExtensions.AsReadOnly(this.otherLogProfiles);
			this.PinnedLogProfiles = ListExtensions.AsReadOnly(this.pinnedLogProfiles);
			this.recentlyUsedLogProfiles = new((lhs, rhs) =>
			{
				var lIndex = this.logProfileManager.RecentlyUsedProfiles.IndexOf(lhs);
				var rIndex = this.logProfileManager.RecentlyUsedProfiles.IndexOf(rhs);
				return lIndex - rIndex;
			});
			this.RecentlyUsedLogProfiles = ListExtensions.AsReadOnly(this.recentlyUsedLogProfiles);
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
			this.otherLogProfilesButton = this.Get<ToggleButton>(nameof(otherLogProfilesButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
			});
			this.otherLogProfilesPanel = this.Get<Panel>(nameof(otherLogProfilesPanel));
			this.pinnedLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(pinnedLogProfileListBox));
			this.pinnedLogProfilesButton = this.Get<ToggleButton>(nameof(pinnedLogProfilesButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
			});
			this.pinnedLogProfilesPanel = this.Get<Panel>(nameof(pinnedLogProfilesPanel));
			this.recentlyUsedLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(recentlyUsedLogProfileListBox));
			this.recentlyUsedLogProfilesButton = this.Get<ToggleButton>(nameof(recentlyUsedLogProfilesButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
			});
			this.recentlyUsedLogProfilesPanel = this.Get<Panel>(nameof(recentlyUsedLogProfilesPanel));
			this.scrollViewer = this.Get<ScrollViewer>(nameof(scrollViewer)).Also(it =>
			{
				it.GetObservable(ScrollViewer.OffsetProperty).Subscribe(this.InvalidateNavigationBar);
				it.GetObservable(ScrollViewer.ViewportProperty).Subscribe(this.InvalidateNavigationBar);
			});
			this.templateLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(templateLogProfileListBox));
			this.templateLogProfilesButton = this.Get<ToggleButton>(nameof(templateLogProfilesButton)).Also(it =>
			{
				it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
			});
			this.templateLogProfilesPanel = this.Get<Panel>(nameof(templateLogProfilesPanel));
			
			// create actions
			this.updateOtherLogProfilesPanelTitleAction = new(() =>
			{
				this.SetValue(OtherLogProfilesPanelTitleProperty, this.pinnedLogProfiles.IsEmpty() && this.recentlyUsedLogProfiles.IsEmpty()
					? this.Application.GetString("LogProfileSelectionDialog.AllLogProfiles")
					: this.Application.GetString("LogProfileSelectionDialog.OtherLogProfiles"));
			});
			
			// attach to application
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;

			// attach to log profiles
			((INotifyCollectionChanged)this.logProfileManager.Profiles).CollectionChanged += this.OnAllLogProfilesChanged;
			((INotifyCollectionChanged)this.logProfileManager.RecentlyUsedProfiles).CollectionChanged += this.OnRecentlyUsedLogProfilesChanged;
			this.RefreshLogProfiles();
			
			// attach to self
			this.pinnedLogProfiles.CollectionChanged += (_, _) => this.updateOtherLogProfilesPanelTitleAction.Schedule();
			this.recentlyUsedLogProfiles.CollectionChanged += (_, _) => this.updateOtherLogProfilesPanelTitleAction.Schedule();
		}


		/// <summary>
		/// Add log profile.
		/// </summary>
		public async void AddLogProfile()
		{
			// create new profile
			var profile = await new LogProfileEditorDialog().ShowDialog<LogProfile?>(this);
			if (profile == null)
				return;

			// add profile
			LogProfileManager.Default.AddProfile(profile);
			this.otherLogProfileListBox.SelectedItem = profile;
		}


		// Categorize log profile.
		void CategorizeLogProfile(LogProfile logProfile) =>
			this.CategorizeLogProfile(logProfile, null);
		void CategorizeLogProfile(LogProfile logProfile, IList<LogProfile>? categoryToExclude)
		{
			if (logProfile.IsTemplate)
			{
				if (categoryToExclude != this.templateLogProfiles && this.Filter == null)
					this.templateLogProfiles.Add(logProfile);
				return;
			}
			if (this.Filter?.Invoke(logProfile) == false)
				return;
			if (logProfile.IsPinned)
			{
				if (categoryToExclude != this.pinnedLogProfiles)
					this.pinnedLogProfiles.Add(logProfile);
			}
			else if (categoryToExclude != this.recentlyUsedLogProfiles)
			{
				if (this.logProfileManager.RecentlyUsedProfiles.Contains(logProfile))
					this.recentlyUsedLogProfiles.Add(logProfile);
				else if (categoryToExclude != this.otherLogProfiles)
					this.otherLogProfiles.Add(logProfile);
			}
			else
			{
				if (categoryToExclude != this.otherLogProfiles)
					this.otherLogProfiles.Add(logProfile);
			}
		}


		// Compare log profiles.
		static int CompareLogProfiles(LogProfile? x, LogProfile? y)
		{
			if (x == null || y == null)
				return 0;
			var result = string.Compare(x.Name, y.Name, true, CultureInfo.InvariantCulture);
			if (result != 0)
				return result;
			result = string.CompareOrdinal(x.Id, y.Id);
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
			var newProfile = await new LogProfileEditorDialog
			{
				LogProfile = new LogProfile(logProfile)
				{
					Name = Utility.GenerateName(logProfile.Name, name =>
						LogProfileManager.Default.Profiles.FirstOrDefault(it => it.Name == name) != null),
				},
			}.ShowDialog<LogProfile?>(this);
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
			logProfile = await new LogProfileEditorDialog
			{
				LogProfile = logProfile
			}.ShowDialog<LogProfile?>(this);
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
			// select a file
			if (logProfile is null)
				return;
			var fileName = await FileSystemItemSelection.SelectFileToExportLogProfileAsync(this);
			if (string.IsNullOrEmpty(fileName))
				return;
			
			// copy and export log profile
			var copiedProfile = new LogProfile(logProfile);
			try
			{
				await copiedProfile.SaveAsync(fileName, false);
				_ = new MessageDialog
				{
					Icon = MessageDialogIcon.Success,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.BindToResource(FormattedString.FormatProperty, this, "String/LogProfileSelectionDialog.LogProfileExported");
					}),
				}.ShowDialog(this);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to export log profile '{name}' to '{fileName}'", this.Name, fileName);
				_ = new MessageDialog
				{
					Icon = MessageDialogIcon.Error,
					Message = new FormattedString().Also(it =>
					{
						it.Arg1 = fileName;
						it.BindToResource(FormattedString.FormatProperty, this, "String/LogProfileSelectionDialog.FailedToExportLogProfile");
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
			get => this.GetValue(FilterProperty);
			set => this.SetValue(FilterProperty, value);
		}


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var logProfile = this.otherLogProfileListBox.SelectedItem as LogProfile
				?? this.pinnedLogProfileListBox.SelectedItem as LogProfile
				?? this.recentlyUsedLogProfileListBox.SelectedItem as LogProfile;
			return Task.FromResult((object?)logProfile);
		}


		/// <summary>
		/// Import log profile.
		/// </summary>
		public async void ImportLogProfile()
		{
			// select file
			var fileName = await FileSystemItemSelection.SelectFileToImportLogProfileAsync(this);
			if (string.IsNullOrEmpty(fileName))
				return;

			// load log profile
			LogProfile? logProfile;
			try
			{
				logProfile = await LogProfile.LoadAsync(this.Application, fileName);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to load log profile from '{fileName}'", fileName);
				_ = new MessageDialog
				{
					Icon = MessageDialogIcon.Error,
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
			if (!this.GetValue(IsProVersionActivatedProperty) && logProfile.IsProVersionOnly)
			{
				_ = new MessageDialog
				{
					Icon = MessageDialogIcon.Warning,
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
			logProfile = await new LogProfileEditorDialog
			{
				LogProfile = logProfile
			}.ShowDialog<LogProfile?>(this);
			if (logProfile == null)
				return;

			// add log profile
			LogProfileManager.Default.AddProfile(logProfile);
			this.otherLogProfileListBox.SelectedItem = logProfile;
		}


		/// <summary>
		/// Check whether ULogViewer Pro has been activated or not.
		/// </summary>
		public bool IsProVersionActivated => this.GetValue(IsProVersionActivatedProperty);


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
						this.CategorizeLogProfile(logProfile);
						logProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (LogProfile logProfile in e.OldItems.AsNonNull())
					{
						if (!this.attachedLogProfiles.Remove(logProfile))
							continue;
						logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
						this.UncategorizeLogProfile(logProfile);
					}
					break;
			}
		}


		// Called when application strings update.
		void OnApplicationStringsUpdated(object? sender, EventArgs e) =>
			this.updateOtherLogProfilesPanelTitleAction.Schedule();
		

		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			// detach from application
			this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
			
			// detach from log profiles
			((INotifyCollectionChanged)this.logProfileManager.Profiles).CollectionChanged -= this.OnAllLogProfilesChanged;
			((INotifyCollectionChanged)this.logProfileManager.RecentlyUsedProfiles).CollectionChanged -= this.OnRecentlyUsedLogProfilesChanged;
			foreach (var profile in this.attachedLogProfiles)
				profile.PropertyChanged -= this.OnLogProfilePropertyChanged;
			this.attachedLogProfiles.Clear();

			// detach from product manager
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
					this.SelectLogProfileCategory(profile).Let(it =>
					{
						it.Remove(profile);
						this.CategorizeLogProfile(profile);
					});
					break;
				case nameof(LogProfile.IsPinned):
					if (profile.IsPinned)
					{
						this.UncategorizeLogProfile(profile);
						this.CategorizeLogProfile(profile);
					}
					else if (this.pinnedLogProfiles.Remove(profile))
						this.CategorizeLogProfile(profile, this.pinnedLogProfiles);
					break;
				case nameof(LogProfile.IsTemplate):
					if (profile.IsTemplate)
						profile.IsPinned = false;
					this.UncategorizeLogProfile(profile);
					this.CategorizeLogProfile(profile);
					break;
				case nameof(LogProfile.Name):
					this.otherLogProfiles.Sort(profile);
					this.pinnedLogProfiles.Sort(profile);
					this.templateLogProfiles.Sort(profile);
					break;
			}
		}


		/// <inheritdoc/>
		protected override void OnOpened(EventArgs e)
		{
			// attach to product manager
			this.Application.ProductManager.Let(it =>
			{
				this.SetValue(IsProVersionActivatedProperty, it.IsProductActivated(Products.Professional));
				it.ProductStateChanged += this.OnProductStateChanged;
			});

			// call base
			base.OnOpened(e);

			// setup focus
			this.SynchronizationContext.Post(() =>
			{
				if (pinnedLogProfiles.IsNotEmpty())
					this.pinnedLogProfileListBox.Focus();
				else if (this.recentlyUsedLogProfiles.IsNotEmpty())
					this.recentlyUsedLogProfileListBox.Focus();
				else if (this.otherLogProfiles.IsNotEmpty())
					this.otherLogProfileListBox.Focus();
				else if (this.templateLogProfiles.IsNotEmpty())
					this.templateLogProfileListBox.Focus();
				else
					this.Close();
			});
		}


		/// <inheritdoc/>
		protected override void OnOpening(EventArgs e)
		{
			this.Screens.Let(it =>
			{
				(it.ScreenFromWindow(this) ?? it.Primary)?.Let(it =>
				{
					this.CanResize = true;
					this.Height = Math.Max(this.MinHeight, it.WorkingArea.Height / it.Scaling * 0.75);
				});
			});
			base.OnOpening(e);
			this.SetValue(IsProVersionActivatedProperty, this.Application.ProductManager.IsProductActivated(Products.Professional));
			this.updateOtherLogProfilesPanelTitleAction.Schedule();
		}


		// Called when selection in other log profiles changed.
		void OnOtherLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.otherLogProfileListBox.SelectedIndex >= 0)
			{
				this.pinnedLogProfileListBox.SelectedIndex = -1;
				this.recentlyUsedLogProfileListBox.SelectedIndex = -1;
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
				this.recentlyUsedLogProfileListBox.SelectedIndex = -1;
				this.templateLogProfileListBox.SelectedIndex = -1;
			}
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		// Called when state of product changed.
		void OnProductStateChanged(IProductManager? productManager, string productId)
		{
			if (productManager != null && productId == Products.Professional)
			{
				this.SetValue(IsProVersionActivatedProperty, productManager.IsProductActivated(productId));
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


		// Called when list of recently used log profiles changed.
		void OnRecentlyUsedLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			var filter = this.Filter;
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (LogProfile logProfile in e.NewItems!)
					{
						if (filter?.Invoke(logProfile) == false || !this.attachedLogProfiles.Contains(logProfile))
							continue;
						this.SelectLogProfileCategory(logProfile).Let(it =>
						{
							if (it == this.recentlyUsedLogProfiles)
							{
								this.UncategorizeLogProfile(logProfile);
								it.Add(logProfile);
							}
						});
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (LogProfile logProfile in e.OldItems.AsNonNull())
					{
						var index = this.recentlyUsedLogProfiles.Let(it =>
						{
							for (var i = it.Count - 1; i >= 0; --i)
							{
								if (it[i] == logProfile)
									return i;
							}
							return -1;
						});
						if (index >= 0)
						{
							this.recentlyUsedLogProfiles.RemoveAt(index);
							this.CategorizeLogProfile(logProfile, this.recentlyUsedLogProfiles);
						}
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					this.logProfileManager.RecentlyUsedProfiles.Let(it =>
					{
						for (var i = this.recentlyUsedLogProfiles.Count - 1; i >= 0; --i)
						{
							var logProfile = this.recentlyUsedLogProfiles[i];
							if (!it.Contains(logProfile))
							{
								this.recentlyUsedLogProfiles.RemoveAt(i);
								this.CategorizeLogProfile(logProfile, this.recentlyUsedLogProfiles);
							}
						}
						foreach (var logProfile in it)
						{
							if (this.attachedLogProfiles.Contains(logProfile) 
								&& !this.recentlyUsedLogProfiles.Contains(logProfile)
								&& filter?.Invoke(logProfile) != false)
							{
								this.SelectLogProfileCategory(logProfile).Let(category =>
								{
									if (category == this.recentlyUsedLogProfiles)
									{
										this.UncategorizeLogProfile(logProfile);
										category.Add(logProfile);
									}
								});
							}
						}
					});
					break;
				default:
					throw new NotSupportedException();
			}
		}


		// Called when selection in recently used log profiles changed.
		void OnRecentlyUsedLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.recentlyUsedLogProfileListBox.SelectedIndex >= 0)
			{
				this.otherLogProfileListBox.SelectedIndex = -1;
				this.pinnedLogProfileListBox.SelectedIndex = -1;
				this.templateLogProfileListBox.SelectedIndex = -1;
			}
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		// Called when selection in template log profiles changed.
		void OnTemplateLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (this.templateLogProfileListBox.SelectedIndex >= 0)
			{
				this.otherLogProfileListBox.SelectedIndex = -1;
				this.pinnedLogProfileListBox.SelectedIndex = -1;
				this.recentlyUsedLogProfileListBox.SelectedIndex = -1;
			}
			this.InvalidateInput();
			this.ScrollToSelectedLogProfile();
		}


		/// <inheritdoc/>
		protected override void OnUpdateNavigationBar()
		{
			// call base
			base.OnUpdateNavigationBar();

			// get state
			if (!this.scrollViewer.TryGetSmoothScrollingTargetOffset(out var offset))
				offset = this.scrollViewer.Offset;
			var viewport = this.scrollViewer.Viewport;
			if (viewport.Height <= 0)
				return;
			var viewportCenter = offset.Y + (viewport.Height / 2);

			// find button to select
			var selectedButton = default(ToggleButton);
			if (offset.Y <= 1)
			{
				if (this.pinnedLogProfilesPanel.IsVisible)
					selectedButton = this.pinnedLogProfilesButton;
				else if (this.recentlyUsedLogProfilesPanel.IsVisible)
					selectedButton = this.recentlyUsedLogProfilesButton;
				else if (this.otherLogProfilesPanel.IsVisible)
					selectedButton = this.otherLogProfilesButton;
				else if (this.templateLogProfilesPanel.IsVisible)
					selectedButton = this.templateLogProfilesButton;
			}
			else if (offset.Y + viewport.Height >= this.scrollViewer.Extent.Height - 1)
			{
				if (this.templateLogProfilesPanel.IsVisible)
					selectedButton = this.templateLogProfilesButton;
				else if (this.otherLogProfilesPanel.IsVisible)
					selectedButton = this.otherLogProfilesButton;
				else if (this.recentlyUsedLogProfilesPanel.IsVisible)
					selectedButton = this.recentlyUsedLogProfilesButton;
				else if (this.pinnedLogProfilesPanel.IsVisible)
					selectedButton = this.pinnedLogProfilesButton;
			}
			else if (this.pinnedLogProfilesPanel.IsVisible && this.pinnedLogProfilesPanel.Bounds.Bottom >= viewportCenter)
				selectedButton = this.pinnedLogProfilesButton;
			else if (this.recentlyUsedLogProfilesPanel.IsVisible && this.recentlyUsedLogProfilesPanel.Bounds.Bottom >= viewportCenter)
				selectedButton = this.recentlyUsedLogProfilesButton;
			else if (this.otherLogProfilesPanel.IsVisible && this.otherLogProfilesPanel.Bounds.Bottom >= viewportCenter)
				selectedButton = this.otherLogProfilesButton;
			else if (this.templateLogProfilesPanel.IsVisible && this.templateLogProfilesPanel.Bounds.Bottom >= viewportCenter)
				selectedButton = this.templateLogProfilesButton;

			// select button
			this.pinnedLogProfilesButton.IsChecked = (this.pinnedLogProfilesButton == selectedButton);
			this.recentlyUsedLogProfilesButton.IsChecked = (this.recentlyUsedLogProfilesButton == selectedButton);
			this.otherLogProfilesButton.IsChecked = (this.otherLogProfilesButton == selectedButton);
			this.templateLogProfilesButton.IsChecked = (this.templateLogProfilesButton == selectedButton);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			if (!base.OnValidateInput())
				return false;
			var selectedItem = this.pinnedLogProfileListBox.SelectedItem 
				?? this.recentlyUsedLogProfileListBox.SelectedItem
				?? this.otherLogProfileListBox.SelectedItem;
			if (selectedItem is not LogProfile logProfile)
				return false;
			return !logProfile.IsProVersionOnly || this.GetValue(IsProVersionActivatedProperty);
		}


		/// <summary>
		/// Get other log profiles.
		/// </summary>
		public IList<LogProfile> OtherLogProfiles { get; }


		/// <summary>
		/// Get title of other log profiles panel.
		/// </summary>
		public string? OtherLogProfilesPanelTitle => this.GetValue(OtherLogProfilesPanelTitleProperty);


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


		/// <summary>
		/// Get recently used log profiles.
		/// </summary>
		public IList<LogProfile> RecentlyUsedLogProfiles { get; }


		// Refresh log profiles.
		void RefreshLogProfiles()
		{
			if (this.IsClosed)
				return;
			var filter = this.Filter;
			var logProfileManager = this.logProfileManager;
			if (filter == null)
			{
				foreach (var profile in logProfileManager.Profiles)
				{
					if (!this.attachedLogProfiles.Add(profile))
						continue;
					this.CategorizeLogProfile(profile);
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
						this.UncategorizeLogProfile(profile);
						profile.PropertyChanged -= this.OnLogProfilePropertyChanged;
					}
				}
				foreach (var profile in LogProfileManager.Default.Profiles)
				{
					if (!this.attachedLogProfiles.Add(profile))
						continue;
					this.CategorizeLogProfile(profile);
					profile.PropertyChanged += this.OnLogProfilePropertyChanged;
				}
			}
		}


		// Remove log profile.
		async void RemoveLogProfile(LogProfile? logProfile)
		{
			if (logProfile == null)
				return;
			var result = await new MessageDialog
			{
				Buttons = MessageDialogButtons.YesNo,
				DefaultResult = MessageDialogResult.No,
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


		// Scroll to given panel
		void ScrollToLogProfilesPanel(ToggleButton button)
		{
			// select panel to scroll to
			Panel panel;
			if (button == this.pinnedLogProfilesButton)
				panel = this.pinnedLogProfilesPanel;
			else if (button == this.recentlyUsedLogProfilesButton)
				panel = this.recentlyUsedLogProfilesPanel;
			else if (button == this.otherLogProfilesButton)
				panel = this.otherLogProfilesPanel;
			else if (button == this.templateLogProfilesButton)
				panel = this.templateLogProfilesPanel;
			else
				return;
			
			// scroll to panel
			this.scrollViewer.SmoothScrollIntoView(panel);
			
			// update navigation bar
			this.InvalidateNavigationBar();
		}


		// Scroll to selected log profile.
		void ScrollToSelectedLogProfile()
		{
			this.SynchronizationContext.PostDelayed(() =>
			{
				// find list box item
				var listBoxItem = (ListBoxItem?)null;
				if (this.pinnedLogProfileListBox.SelectedItem is LogProfile pinnedLogProfile)
					this.pinnedLogProfileListBox.TryFindListBoxItem(pinnedLogProfile, out listBoxItem);
				else if (this.recentlyUsedLogProfileListBox.SelectedItem is LogProfile recentlyUsedLogProfile)
					this.recentlyUsedLogProfileListBox.TryFindListBoxItem(recentlyUsedLogProfile, out listBoxItem);
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
			else if (this.recentlyUsedLogProfiles.Contains(profile))
				this.recentlyUsedLogProfileListBox.SelectedItem = profile;
			else
				this.otherLogProfileListBox.SelectedItem = profile;
			this.ScrollToSelectedLogProfile();
		}


		// Select proper category for given log profile.
		IList<LogProfile> SelectLogProfileCategory(LogProfile logProfile)
		{
			if (logProfile.IsTemplate)
				return this.templateLogProfiles;
			if (logProfile.IsPinned)
				return this.pinnedLogProfiles;
			if (this.logProfileManager.RecentlyUsedProfiles.Contains(logProfile))
				return this.recentlyUsedLogProfiles;
			return this.otherLogProfiles;
		}


		/// <summary>
		/// Get template log profiles.
		/// </summary>
		public IList<LogProfile> TemplateLogProfiles { get; }


		// Remove log profile from all categories.
		// ReSharper disable once IdentifierTypo
		void UncategorizeLogProfile(LogProfile logProfile)
		{
			this.otherLogProfiles.Remove(logProfile);
			this.pinnedLogProfiles.Remove(logProfile);
			this.recentlyUsedLogProfiles.Remove(logProfile);
			this.templateLogProfiles.Remove(logProfile);
		}
	}
}
