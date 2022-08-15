using Avalonia;
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
    protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
    {
        base.OnPropertyChanged(change);
		if (change.Property == DataContextProperty)
		{
			(change.OldValue.Value as AppOptions)?.Let(it => it.PropertyChanged -= this.OnAppOptionsPropertyChanged);
			(change.NewValue.Value as AppOptions)?.Let(it => 
			{
				it.PropertyChanged += this.OnAppOptionsPropertyChanged;
			});
		}
    }


	// Show dialog of external dependencies.
	void ShowExternalDependenciesDialog() =>
		_ = new ExternalDependenciesDialog().ShowDialog(this);
}