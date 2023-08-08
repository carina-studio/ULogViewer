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
	private LogProfileSelectionContextMenu? logProfileSelectionMenu;
	readonly ScheduledAction refreshDataContextAction;


	/// <summary>
	/// Initialize new <see cref="AppOptionsDialog"/> instance.
	/// </summary>
	public AppOptionsDialog()
	{
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
		this.Get<IntegerTextBox>("maxContinuousLogCountTextBox").Also(it =>
			it.LostFocus += (_, _) => CoerceIntegerValue(it));
		this.Get<IntegerTextBox>("updateLogFilterDelayTextBox").Also(it =>
			it.LostFocus += (_, _) => CoerceIntegerValue(it));
		this.Application.PropertyChanged += this.OnApplicationPropertyChanged;
		this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
		this.refreshDataContextAction = new ScheduledAction(() =>
		{
			var dataContext = this.DataContext;
			this.DataContext = null;
			this.DataContext = dataContext;
		});
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
		this.Application.PropertyChanged -= this.OnApplicationPropertyChanged;
		this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
		this.refreshDataContextAction.Cancel();
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
			this.ScrollToControl(initControl);
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