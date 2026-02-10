using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
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

namespace CarinaStudio.ULogViewer.Controls;

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
	static readonly DirectProperty<LogProfileSelectionDialog, bool> HasLogProfilesProperty = AvaloniaProperty.RegisterDirect<LogProfileSelectionDialog, bool>(nameof(HasLogProfiles), d => d.hasLogProfiles);
	static readonly StyledProperty<bool> IsProVersionActivatedProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, bool>(nameof(IsProVersionActivated));
	static readonly StyledProperty<string?> OtherLogProfilesPanelTitleProperty = AvaloniaProperty.Register<LogProfileSelectionDialog, string?>(nameof(OtherLogProfilesPanelTitle));


	// Fields.
	readonly HashSet<LogProfile> attachedLogProfiles = new();
	bool hasLogProfiles;
	readonly List<(Avalonia.Controls.ListBox, IList<LogProfile>)> logProfileListBoxes = new();
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
		this.otherLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(otherLogProfileListBox)).Also(it =>
		{
			it.SelectionChanged += this.OnLogProfilesSelectionChanged;
		});
		this.otherLogProfilesButton = this.Get<ToggleButton>(nameof(otherLogProfilesButton)).Also(it =>
		{
			it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
		});
		this.otherLogProfilesPanel = this.Get<Panel>(nameof(otherLogProfilesPanel));
		this.pinnedLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(pinnedLogProfileListBox)).Also(it =>
		{
			it.SelectionChanged += this.OnLogProfilesSelectionChanged;
		});
		this.pinnedLogProfilesButton = this.Get<ToggleButton>(nameof(pinnedLogProfilesButton)).Also(it =>
		{
			it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
		});
		this.pinnedLogProfilesPanel = this.Get<Panel>(nameof(pinnedLogProfilesPanel));
		this.recentlyUsedLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(recentlyUsedLogProfileListBox)).Also(it =>
		{
			it.SelectionChanged += this.OnLogProfilesSelectionChanged;
		});
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
		this.templateLogProfileListBox = this.Get<Avalonia.Controls.ListBox>(nameof(templateLogProfileListBox)).Also(it =>
		{
			it.SelectionChanged += this.OnLogProfilesSelectionChanged;
		});
		this.templateLogProfilesButton = this.Get<ToggleButton>(nameof(templateLogProfilesButton)).Also(it =>
		{
			it.Click += (_, _) => this.ScrollToLogProfilesPanel(it);
		});
		this.templateLogProfilesPanel = this.Get<Panel>(nameof(templateLogProfilesPanel));
		
		// setup log profiles list boxes
		this.logProfileListBoxes.Add((pinnedLogProfileListBox, pinnedLogProfiles));
		this.logProfileListBoxes.Add((recentlyUsedLogProfileListBox, recentlyUsedLogProfiles));
		this.logProfileListBoxes.Add((otherLogProfileListBox, otherLogProfiles));
		this.logProfileListBoxes.Add((templateLogProfileListBox, templateLogProfiles));
		
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
		
		// attach to self
		this.pinnedLogProfiles.CollectionChanged += (_, _) => this.updateOtherLogProfilesPanelTitleAction.Schedule();
		this.recentlyUsedLogProfiles.CollectionChanged += (_, _) => this.updateOtherLogProfilesPanelTitleAction.Schedule();
		this.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
	}


	/// <summary>
	/// Add log profile.
	/// </summary>
	public async Task AddLogProfile()
	{
		// create new profile
		var profile = await new LogProfileEditorDialog().ShowDialog<LogProfile?>(this);
		if (profile is null)
			return;

		// add profile
		LogProfileManager.Default.AddProfile(profile);
		
		// select profile
		this.SelectLogProfile(profile);
	}
	
	
	// Attach to given log profile.
	void AttachToLogProfile(LogProfile logProfile)
	{
		if (!this.attachedLogProfiles.Add(logProfile))
			return;
		this.CategorizeLogProfile(logProfile);
		logProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
	}


	// Categorize log profile.
	void CategorizeLogProfile(LogProfile logProfile) =>
		this.CategorizeLogProfile(logProfile, null);
	void CategorizeLogProfile(LogProfile logProfile, IList<LogProfile>? categoryToExclude)
	{
		if (logProfile.IsTemplate)
		{
			if (!ReferenceEquals(categoryToExclude, this.templateLogProfiles) && this.Filter is null)
				this.templateLogProfiles.Add(logProfile);
			return;
		}
		if (this.Filter?.Invoke(logProfile) == false)
			return;
		if (logProfile.IsPinned)
		{
			if (!ReferenceEquals(categoryToExclude, this.pinnedLogProfiles))
				this.pinnedLogProfiles.Add(logProfile);
		}
		else if (!ReferenceEquals(categoryToExclude, this.recentlyUsedLogProfiles))
		{
			if (this.logProfileManager.RecentlyUsedProfiles.Contains(logProfile))
				this.recentlyUsedLogProfiles.Add(logProfile);
			else if (!ReferenceEquals(categoryToExclude, this.otherLogProfiles))
				this.otherLogProfiles.Add(logProfile);
		}
		else
		{
			if (!ReferenceEquals(categoryToExclude, this.otherLogProfiles))
				this.otherLogProfiles.Add(logProfile);
		}
	}


	// Compare log profiles.
	static int CompareLogProfiles(LogProfile? x, LogProfile? y)
	{
		if (x is null || y is null)
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
	async Task CopyLogProfile(LogProfile? logProfile)
	{
		// check state
		if (logProfile is null)
			return;

		// copy and edit log profile
		var newProfile = await new LogProfileEditorDialog
		{
			LogProfile = new LogProfile(logProfile)
			{
				Name = Utility.GenerateName(logProfile.Name, name =>
					LogProfileManager.Default.Profiles.FirstOrDefault(it => it.Name == name) is not null),
			},
		}.ShowDialog<LogProfile?>(this);
		if (newProfile is null)
			return;

		// add log profile
		LogProfileManager.Default.AddProfile(newProfile);
		this.SelectLogProfile(newProfile);
	}


	/// <summary>
	/// Command to copy log profile.
	/// </summary>
	public ICommand CopyLogProfileCommand { get; }
	
	
	// Detach from given log profile.
	void DetachFromLogProfile(LogProfile logProfile)
	{
		if (!this.attachedLogProfiles.Remove(logProfile))
			return;
		this.UncategorizeLogProfile(logProfile);
		logProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
	}


	// Edit log profile.
	async Task EditLogProfile(LogProfile? logProfile)
	{
		// edit log profile
		if (logProfile is null)
			return;
		logProfile = await new LogProfileEditorDialog
		{
			LogProfile = logProfile
		}.ShowDialog<LogProfile?>(this);
		if (logProfile is null)
			return;
		
		// select log profile
		this.SelectLogProfile(logProfile);
	}


	/// <summary>
	/// Command to edit log profile.
	/// </summary>
	public ICommand EditLogProfileCommand { get; }


	// Export log profile.
	async Task ExportLogProfile(LogProfile? logProfile)
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
	/// Check whether at least one log profile is valid to be selected or not.
	/// </summary>
	public bool HasLogProfiles => this.hasLogProfiles;


	/// <summary>
	/// Import log profile.
	/// </summary>
	public async Task ImportLogProfile()
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
		if (logProfile is null)
			return;

		// add log profile
		LogProfileManager.Default.AddProfile(logProfile);
		
		// select log profile
		this.SelectLogProfile(logProfile);
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
				foreach (LogProfile logProfile in e.NewItems.AsNonNull())
				{
					if (filter is null || filter(logProfile))
						this.AttachToLogProfile(logProfile);
				}
				this.SetAndRaise(HasLogProfilesProperty, ref this.hasLogProfiles, this.attachedLogProfiles.IsNotEmpty());
				break;
			case NotifyCollectionChangedAction.Remove:
				foreach (LogProfile logProfile in e.OldItems.AsNonNull())
					this.DetachFromLogProfile(logProfile);
				this.SetAndRaise(HasLogProfilesProperty, ref this.hasLogProfiles, this.attachedLogProfiles.IsNotEmpty());
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

		// call base
		base.OnClosed(e);
	}
	
	
	/// <inheritdoc/>
	protected override void OnFirstMeasurementCompleted(Size measuredSize)
	{
		// call base
		base.OnFirstMeasurementCompleted(measuredSize);
		
		// [Workaround] force layout again to prevent insufficient space in scrollViewer
		var margin = this.scrollViewer.Margin;
		this.scrollViewer.Margin = new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom + 1);
		this.scrollViewer.RequestLayoutCallback(() =>
		{
			this.scrollViewer.Margin = margin;
		});
	}


	// Called when double tapped on item of log profile.
	void OnLogProfileItemDoubleTapped(object? sender, TappedEventArgs e) => this.GenerateResultCommand.TryExecute();


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
	
	
	// Called when selection of list box of log profiles changed.
	void OnLogProfilesSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count > 0)
		{
			foreach (var (listBox, _) in this.logProfileListBoxes)
			{
				if (!ReferenceEquals(listBox, sender))
					listBox.SelectedIndex = -1;
			}
			this.ScrollToSelectedLogProfile();
		}
		this.InvalidateInput();
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		// call base
		base.OnOpened(e);

		// setup focus
		this.SynchronizationContext.Post(() =>
		{
			foreach (var (listBox, logProfiles) in this.logProfileListBoxes)
			{
				if (logProfiles.IsNotEmpty())
				{
					listBox.Focus(); // to make sure focusing on the dialog
					this.scrollViewer.ScrollToHome();
					return;
				}
			}
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
		this.RefreshLogProfiles();
		base.OnOpening(e);
		this.SetValue(IsProVersionActivatedProperty, this.Application.ProductManager.IsProductActivated(Products.Professional));
		this.updateOtherLogProfilesPanelTitleAction.Schedule();
	}


	// Called to preview key down event.
	void OnPreviewKeyDown(object? sender, KeyEventArgs e)
	{
		switch (e.Key)
		{
			case Key.Down:
			{
				var hasSelectedListBox = false;
				for (int i = 0, count = this.logProfileListBoxes.Count; i < count; ++i)
				{
					var (listBox, logProfiles) = this.logProfileListBoxes[i];
					if (listBox.SelectedIndex >= 0)
					{
						hasSelectedListBox = true;
						if (listBox.SelectedIndex >= logProfiles.Count - 1)
						{
							for (++i; i < count; ++i)
							{
								var (nextListBox, nextLogProfiles) = this.logProfileListBoxes[i];
								if (nextLogProfiles.IsNotEmpty())
								{
									listBox.SelectedIndex = -1;
									nextListBox.SelectedIndex = 0;
									nextListBox.FocusSelectedItem();
									break;
								}
							}
							e.Handled = true;
						}
						break;
					}
				}
				if (!hasSelectedListBox)
				{
					for (int i = 0, count = this.logProfileListBoxes.Count; i < count; ++i)
					{
						var (listBox, logProfiles) = this.logProfileListBoxes[i];
						if (logProfiles.IsNotEmpty())
						{
							listBox.SelectedIndex = 0;
							listBox.FocusSelectedItem();
							e.Handled = true;
							break;
						}
					}
				}
				break;
			}
			case Key.Up:
			{
				var hasSelectedListBox = false;
				for (var i = this.logProfileListBoxes.Count - 1; i >= 0; --i)
				{
					var (listBox, _) = this.logProfileListBoxes[i];
					if (listBox.SelectedIndex >= 0)
					{
						hasSelectedListBox = true;
						if (listBox.SelectedIndex == 0)
						{
							for (--i; i >= 0; --i)
							{
								var (prevListBox, prevLogProfiles) = this.logProfileListBoxes[i];
								if (prevLogProfiles.IsNotEmpty())
								{
									listBox.SelectedIndex = -1;
									prevListBox.SelectedIndex = prevLogProfiles.Count - 1;
									prevListBox.FocusSelectedItem();
									break;
								}
							}
							e.Handled = true;
						}
						break;
					}
				}
				if (!hasSelectedListBox)
				{
					for (var i = this.logProfileListBoxes.Count - 1; i >= 0; --i)
					{
						var (listBox, logProfiles) = this.logProfileListBoxes[i];
						if (logProfiles.IsNotEmpty())
						{
							listBox.SelectedIndex = logProfiles.Count - 1;
							listBox.FocusSelectedItem();
							e.Handled = true;
							break;
						}
					}
				}
				break;
			}
		}
	}


	// Called when property changed.
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == FilterProperty)
		{
			if (this.IsOpened)
				this.RefreshLogProfiles();
		}
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
						if (ReferenceEquals(it, this.recentlyUsedLogProfiles))
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
							if (ReferenceEquals(it[i], logProfile))
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
								if (ReferenceEquals(category, this.recentlyUsedLogProfiles))
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
		if (logProfile is null)
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
		// prepare filter
		if (this.IsClosed)
			return;
		var filter = this.Filter;
		
		// clear selection
		foreach (var (listBox, _) in this.logProfileListBoxes)
			listBox.SelectedIndex = -1;
		
		// refresh log profiles
		var logProfileManager = this.logProfileManager;
		foreach (var profile in LogProfileManager.Default.Profiles)
		{
			if (filter is null || (filter(profile) && !profile.IsTemplate))
				this.AttachToLogProfile(profile);
			else
				this.DetachFromLogProfile(profile);
		}
		this.SetAndRaise(HasLogProfilesProperty, ref this.hasLogProfiles, this.attachedLogProfiles.IsNotEmpty());
	}


	// Remove log profile.
	async Task RemoveLogProfile(LogProfile? logProfile)
	{
		if (logProfile is null)
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
			var listBoxItem = Global.Run(() =>
			{
				foreach (var (listBox, _) in this.logProfileListBoxes)
				{
					if (listBox.SelectedIndex >= 0)
					{
						this.templateLogProfileListBox.TryFindListBoxItem(listBox, out var listBoxItem);
						return listBoxItem;
					}
				}
				return null;
			});
			if (listBoxItem is null)
				return;

			// scroll to list box item
			this.scrollViewer.ScrollIntoView(listBoxItem);
		}, 100);
	}


	// Select given log profile.
	void SelectLogProfile(LogProfile profile)
	{
		foreach (var (listBox, logProfiles) in logProfileListBoxes)
		{
			var index = logProfiles.IndexOf(profile);
			if (index >= 0)
			{
				listBox.SelectedIndex = index;
				this.ScrollToSelectedLogProfile();
				break;
			}
		}
	}


	// Select proper category for given log profile.
	IList<LogProfile> SelectLogProfileCategory(LogProfile logProfile)
	{
		foreach (var (_, logProfiles) in logProfileListBoxes)
		{
			if (logProfiles.Contains(logProfile))
				return logProfiles;
		}
		throw new InternalStateCorruptedException();
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