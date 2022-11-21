using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.ViewModels;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog for application options.
/// </summary>
partial class AppOptionsDialog : BaseApplicationOptionsDialog
{
	/// <summary>
	/// Name of section of default text shell.
	/// </summary>
	public const string DefaultTextShellSection = "DefaultTextShell";


	/// <summary>
	/// Converter to convert from <see cref="TextShell"/> to string.
	/// </summary>
	public static readonly IValueConverter TextShellConverter = new AppSuite.Converters.EnumConverter(App.CurrentOrNull, typeof(TextShell));


	// Fields.
	readonly ScheduledAction refreshDataContextAction;


	/// <summary>
	/// Initialize new <see cref="AppOptionsDialog"/> instance.
	/// </summary>
	public AppOptionsDialog()
	{
		AvaloniaXamlLoader.Load(this);
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
			if (initControl is TextBlock textBlock)
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
			});
		}
    }


	/// <summary>
	/// Show dialog of external dependencies.
	/// </summary>
	public void ShowExternalDependenciesDialog() =>
		_ = new ExternalDependenciesDialog().ShowDialog(this);
}