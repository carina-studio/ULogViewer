using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.ViewModels;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using CarinaStudio.ULogViewer.Logs.Profiles;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog for application options.
/// </summary>
class AppOptionsDialog : BaseApplicationOptionsDialog
{
	/// <summary>
	/// Name of section of default text shell.
	/// </summary>
	public const string DefaultTextShellSection = "DefaultTextShell";


	/// <summary>
	/// Converter to convert from <see cref="TextShell"/> to string.
	/// </summary>
	public static readonly IValueConverter TextShellConverter = new AppSuite.Converters.EnumConverter(App.CurrentOrNull, typeof(TextShell));


	// Static fields.
	static readonly StyledProperty<bool> HasInitialLogProfileProperty = AvaloniaProperty.Register<AppOptionsDialog, bool>(nameof(HasInitialLogProfile));
	

	// Fields.
	readonly Panel logFilteringPanel;
	readonly ToggleButton logFilteringPanelButton;
	readonly Panel logOperationsPanel;
	readonly ToggleButton logOperationsPanelButton;
	LogProfileSelectionContextMenu? logProfileSelectionMenu;
	readonly Panel othersPanel;
	readonly ToggleButton othersPanelButton;
	readonly ScheduledAction refreshDataContextAction;
	readonly ScrollViewer rootScrollViewer;
	readonly Panel userInterfacePanel;
	readonly ToggleButton userInterfacePanelButton;


	/// <summary>
	/// Initialize new <see cref="AppOptionsDialog"/> instance.
	/// </summary>
	public AppOptionsDialog()
	{
		// setup properties
		this.HasNavigationBar = true;
		
		// load views
		AvaloniaXamlLoader.Load(this);
		static void CoerceIntegerValue(IntegerTextBox textBox)
		{
			if (!textBox.IsTextValid && int.TryParse(textBox.Text, out var size))
				textBox.Value = Math.Max(Math.Min(textBox.Maximum, size), textBox.Minimum);
		}
		this.Get<IntegerTextBox>("continuousReadingUpdateIntervalTextBox").Also(it =>
			it.LostFocus += (_, _) => CoerceIntegerValue(it));
		this.Get<ToggleButton>("initLogProfileButton").Also(it =>
		{
			it.GetObservable(ToggleButton.IsCheckedProperty).Subscribe(async isChecked =>
			{
				if (isChecked != true)
					return;
				var logProfile = await this.SelectLogProfileAsync(it);
				it.IsChecked = false;
				if (logProfile is not null && this.DataContext is AppOptions appOptions)
					appOptions.InitialLogProfile = logProfile;
			});
		});
		this.logFilteringPanel = this.Get<Panel>(nameof(logFilteringPanel));
		this.logFilteringPanelButton = this.Get<ToggleButton>(nameof(logFilteringPanelButton)).Also(it => 
			it.Click += (_, _) => this.ScrollToPanel(it));
		this.logOperationsPanel = this.Get<Panel>(nameof(logOperationsPanel));
		this.logOperationsPanelButton = this.Get<ToggleButton>(nameof(logOperationsPanelButton)).Also(it => 
			it.Click += (_, _) => this.ScrollToPanel(it));
		this.Get<IntegerTextBox>("maxContinuousLogCountTextBox").Also(it =>
			it.LostFocus += (_, _) => CoerceIntegerValue(it));
		this.othersPanel = this.Get<Panel>(nameof(othersPanel));
		this.othersPanelButton = this.Get<ToggleButton>(nameof(othersPanelButton)).Also(it => 
			it.Click += (_, _) => this.ScrollToPanel(it));
		this.rootScrollViewer = this.Get<ScrollViewer>(nameof(rootScrollViewer)).Also(it =>
		{
			it.GetObservable(ScrollViewer.OffsetProperty).Subscribe(this.InvalidateNavigationBar);
			it.GetObservable(ScrollViewer.ViewportProperty).Subscribe(this.InvalidateNavigationBar);
		});
		this.Get<IntegerTextBox>("updateLogFilterDelayTextBox").Also(it =>
			it.LostFocus += (_, _) => CoerceIntegerValue(it));
		this.userInterfacePanel = this.Get<Panel>(nameof(userInterfacePanel));
		this.userInterfacePanelButton = this.Get<ToggleButton>(nameof(userInterfacePanelButton)).Also(it => 
			it.Click += (_, _) => this.ScrollToPanel(it));
		
		// create actions
		this.refreshDataContextAction = new ScheduledAction(() =>
		{
			var dataContext = this.DataContext;
			this.DataContext = null;
			this.DataContext = dataContext;
		});
		
		// attach to application
		this.Application.PropertyChanged += this.OnApplicationPropertyChanged;
		this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
	}
	
	
	// Clear log text filter phrases database.
	public async Task ClearLogTextFilterPhrasesDatabase()
	{
		var deletionCount = await LogTextFilterPhrasesDatabase.ClearAsync();
		_ = new MessageDialog
		{
			Message = new FormattedString().Also(it =>
			{
				it.Arg1 = deletionCount;
				it.BindToResource(FormattedString.FormatProperty, this, "String/AppOptionsDialog.ClearLogTextFilterPhrasesDatabase.Result");
			})
		}.ShowDialog(this);
	}


	/// <summary>
	/// Check whether initial log profile is not empty or not.
	/// </summary>
	public bool HasInitialLogProfile => this.GetValue(HasInitialLogProfileProperty);


	/// <summary>
	/// Get or set name of initial section to be shown.
	/// </summary>
	public string? InitSectionName { get; set; }


	// Called when application property changed.
	void OnApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == nameof(IULogViewerApplication.EffectiveThemeMode))
			this.refreshDataContextAction.Reschedule(500);
	}


	// Called when strings updated.
	void OnApplicationStringsUpdated(object? sender, EventArgs e)
	{
		this.refreshDataContextAction.Schedule();
	}


	// Called when property of AppOptions changed.
	void OnAppOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (sender is not AppOptions appOptions)
			return;
		if (e.PropertyName == nameof(AppOptions.InitialLogProfile))
			this.SetValue(HasInitialLogProfileProperty, appOptions.InitialLogProfile != LogProfileManager.Default.EmptyProfile);
	}


	// Called when closed.
	protected override void OnClosed(EventArgs e)
	{
		// detach from application
		this.Application.PropertyChanged -= this.OnApplicationPropertyChanged;
		this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
		
		// cancel actions
		this.refreshDataContextAction.Cancel();
		
		// call base
		base.OnClosed(e);
	}


	// Create view-model.
	protected override ApplicationOptions OnCreateViewModel() => new AppOptions();


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		// call base
		base.OnOpened(e);

		// scroll to given section
		var initControl = this.InitSectionName switch
		{
			DefaultTextShellSection => this.Get<Control>("defaultTextShellLabel"),
			_ => null,
		};
		if (initControl != null)
		{
			this.rootScrollViewer.ScrollIntoView(initControl, true);
			if (initControl is Avalonia.Controls.TextBlock textBlock)
				this.AnimateTextBlock(textBlock);
			else if (initControl is Border headerBorder)
				this.AnimateHeader(headerBorder);
		}
	}


    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
		if (change.Property == DataContextProperty)
		{
			(change.OldValue as AppOptions)?.Let(it => it.PropertyChanged -= this.OnAppOptionsPropertyChanged);
			(change.NewValue as AppOptions)?.Let(it => 
			{
				it.PropertyChanged += this.OnAppOptionsPropertyChanged;
				this.SetValue(HasInitialLogProfileProperty, it.InitialLogProfile != LogProfileManager.Default.EmptyProfile);
			});
		}
    }


    /// <inheritdoc/>
    protected override void OnUpdateNavigationBar()
    {
	    // call base
	    base.OnUpdateNavigationBar();
	    
	    // check state
	    if (!this.rootScrollViewer.TryGetSmoothScrollingTargetOffset(out var offset))
		    offset = this.rootScrollViewer.Offset;
	    var viewport = this.rootScrollViewer.Viewport;
	    if (viewport.Height <= 0)
		    return;
				
	    // find button to select
	    var viewportCenter = offset.Y + (viewport.Height / 2);
	    ToggleButton selectedButton;
	    if (offset.Y <= 1)
		    selectedButton = this.userInterfacePanelButton;
	    else if (offset.Y + viewport.Height >= this.rootScrollViewer.Extent.Height - 1)
		    selectedButton = this.othersPanelButton;
	    else if (this.othersPanel.Bounds.Y <= viewportCenter)
		    selectedButton = this.othersPanelButton;
	    else if (this.logFilteringPanel.Bounds.Y <= viewportCenter)
		    selectedButton = this.logFilteringPanelButton;
	    else if (this.logOperationsPanel.Bounds.Y <= viewportCenter)
		    selectedButton = this.logOperationsPanelButton;
	    else
		    selectedButton = this.userInterfacePanelButton;
	    
	    // select button
	    this.userInterfacePanelButton.IsChecked = (this.userInterfacePanelButton == selectedButton);
	    this.logOperationsPanelButton.IsChecked = (this.logOperationsPanelButton == selectedButton);
	    this.logFilteringPanelButton.IsChecked = (this.logFilteringPanelButton == selectedButton);
	    this.othersPanelButton.IsChecked = (this.othersPanelButton == selectedButton);
    }


    /// <summary>
    /// Open document of Noto Sans font.
    /// </summary>
    public void OpenNotoSansDocument() =>
	    Platform.OpenLink(this.Application.CultureInfo.Name.Let(name =>
	    {
		    if (name.StartsWith("zh"))
		    {
			    if (name.EndsWith("TW"))
				    return Uris.NotoSansTC;
			    return Uris.NotoSansSC;
		    }
		    return Uris.NotoSans;
	    }));


    // Scroll to given panel
    void ScrollToPanel(ToggleButton button)
    {
	    // select panel to scroll to
	    Panel panel;
	    if (button == this.userInterfacePanelButton)
		    panel = this.userInterfacePanel;
	    else if (button == this.logOperationsPanelButton)
		    panel = this.logOperationsPanel;
	    else if (button == this.logFilteringPanelButton)
		    panel = this.logFilteringPanel;
	    else if (button == this.othersPanelButton)
		    panel = this.othersPanel;
	    else
		    return;
			
	    // scroll to panel
	    this.rootScrollViewer.SmoothScrollIntoView(panel);
			
	    // update navigation bar
	    this.InvalidateNavigationBar();
    }
    
    
    // Select log profile.
    async Task<LogProfile?> SelectLogProfileAsync(Control anchor)
    {
	    if (this.logProfileSelectionMenu?.IsOpen == true)
		    this.logProfileSelectionMenu.Close();
	    var taskSource = new TaskCompletionSource<LogProfile?>();
	    var closedHandler = new EventHandler<RoutedEventArgs>((_, _) =>
		    this.SynchronizationContext.Post(() => taskSource.TrySetResult(null)));
	    var logProfileSelectedHandler = new Action<LogProfileSelectionContextMenu, LogProfile>((_, profile) => taskSource.TrySetResult(profile));
	    this.logProfileSelectionMenu ??= new LogProfileSelectionContextMenu
	    {
		    Placement = PlacementMode.Bottom,
		    ShowEmptyLogProfile = true,
	    };
	    this.logProfileSelectionMenu.Closed += closedHandler;
	    this.logProfileSelectionMenu.LogProfileSelected += logProfileSelectedHandler;
	    this.logProfileSelectionMenu.Open(anchor);
	    var logProfile = await taskSource.Task;
	    this.logProfileSelectionMenu.Closed -= closedHandler;
	    this.logProfileSelectionMenu.LogProfileSelected -= logProfileSelectedHandler;
	    return logProfile;
    }


	/// <summary>
	/// Show dialog of external dependencies.
	/// </summary>
	public void ShowExternalDependenciesDialog() =>
		_ = new ExternalDependenciesDialog().ShowDialog(this);
}