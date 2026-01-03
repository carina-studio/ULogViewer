using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CarinaStudio.AppSuite.Data;
using Microsoft.Extensions.Logging;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit log analysis script set.
/// </summary>
class LogAnalysisScriptSetEditorDialog : Dialog<IULogViewerApplication>
{
	// Static fields.
	static readonly DirectProperty<LogAnalysisScriptSetEditorDialog, Uri?> AnalysisScriptDocumentUriProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, Uri?>(nameof(AnalysisScriptDocumentUri), d => d.analysisScriptDocumentUri);
	static readonly DirectProperty<LogAnalysisScriptSetEditorDialog, bool> AreValidParametersProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, bool>(nameof(AreValidParameters), d => d.areValidParameters);
	static readonly Dictionary<LogAnalysisScriptSet, LogAnalysisScriptSetEditorDialog> Dialogs = new();
	static readonly SettingKey<bool> DonotShowRestrictionsWithNonProVersionKey = new("LogAnalysisScriptSetEditorDialog.DonotShowRestrictionsWithNonProVersion");
	static readonly StyledProperty<bool> IsEmbeddedScriptSetProperty = AvaloniaProperty.Register<LogAnalysisScriptSetEditorDialog, bool>(nameof(IsEmbeddedScriptSet));
	static readonly DirectProperty<LogAnalysisScriptSetEditorDialog, bool> IsNewScriptSetProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, bool>(nameof(IsNewScriptSet), d => d.isNewScriptSet);
	static readonly DirectProperty<LogAnalysisScriptSetEditorDialog, Uri?> SetupScriptDocumentUriProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, Uri?>(nameof(SetupScriptDocumentUri), d => d.setupScriptDocumentUri);


	// Fields.
	Uri? analysisScriptDocumentUri;
	bool areValidParameters;
	readonly ToggleSwitch contextBasedToggleSwitch;
	string? fileNameToSave;
	readonly LogProfileIconColorComboBox iconColorComboBox;
	readonly LogProfileIconComboBox iconComboBox;
	bool isNewScriptSet;
	bool isScriptSetShown;
	readonly TextBox nameTextBox;
	LogAnalysisScriptSet? scriptSetToEdit;
	Uri? setupScriptDocumentUri;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="LogAnalysisScriptSetEditorDialog"/> instance.
	/// </summary>
	public LogAnalysisScriptSetEditorDialog()
	{
		var isInit = true;
		this.ApplyCommand = new Command(async() => await this.ApplyAsync(false), this.GetObservable(AreValidParametersProperty));
		this.CompleteEditingCommand = new Command(this.CompleteEditing, this.GetObservable(AreValidParametersProperty));
		AvaloniaXamlLoader.Load(this);
		if (Platform.IsLinux)
			this.WindowStartupLocation = WindowStartupLocation.Manual;
		this.contextBasedToggleSwitch = this.Get<ToggleSwitch>(nameof(contextBasedToggleSwitch));
		this.iconColorComboBox = this.Get<LogProfileIconColorComboBox>(nameof(iconColorComboBox));
		this.iconComboBox = this.Get<LogProfileIconComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise(AreValidParametersProperty, ref this.areValidParameters, this.IsEmbeddedScriptSet || !string.IsNullOrWhiteSpace(this.nameTextBox.Text));
		});
		this.GetObservable(IsEmbeddedScriptSetProperty).Subscribe(_ => this.validateParametersAction.Schedule());
		this.UpdateDocumentUris();
		isInit = false;
	}


	// URI of document of analysis script.
	public Uri? AnalysisScriptDocumentUri => this.GetValue(AnalysisScriptDocumentUriProperty);
	
	
	// Command to apply current script set.
	public ICommand ApplyCommand { get; }
	
	
	// Apply current script set.
	async Task<LogAnalysisScriptSet?> ApplyAsync(bool willClose)
	{
		// check compilation error
		//
		
		// create or update script set
		var scriptSet = this.scriptSetToEdit;
		if (scriptSet is null)
		{
			if (!this.isNewScriptSet)
				return null;
			scriptSet = new(this.Application);
		}
		scriptSet.Name = this.nameTextBox.Text?.Trim();
		scriptSet.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
		scriptSet.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
		scriptSet.IsContextualBased = this.contextBasedToggleSwitch.IsChecked.GetValueOrDefault();
		
		// add script set or save to file
		if (string.IsNullOrEmpty(this.fileNameToSave))
		{
			if (!this.IsEmbeddedScriptSet && !LogAnalysisScriptSetManager.Default.ScriptSets.Contains(scriptSet))
			{
				if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
				    && !LogAnalysisScriptSetManager.Default.CanAddScriptSet)
				{
					await new MessageDialog
					{
						Icon = MessageDialogIcon.Warning,
						Message = this.GetResourceObservable("String/LogAnalysisScriptSetEditorDialog.CannotAddMoreScriptSetWithoutProVersion"),
					}.ShowDialog(this);
					return null;
				}
				LogAnalysisScriptSetManager.Default.AddScriptSet(scriptSet);
			}
		}
		else
		{
			try
			{
				await scriptSet.SaveAsync(this.fileNameToSave, true);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to save script set to '{fileName}'", this.fileNameToSave);
				_ = new MessageDialog
				{
					Icon = MessageDialogIcon.Error,
					Message = $"Unable to save script set to '{this.fileNameToSave}'",
				}.ShowDialog(this);
				return null;
			}
		}
		
		// complete
		return scriptSet;
	}


	// Whether all parameters are valid or not.
	public bool AreValidParameters => this.GetValue(AreValidParametersProperty);


	/// <summary>
	/// Close all window related to given script set.
	/// </summary>
	/// <param name="scriptSet">Script set.</param>
	public static void CloseAll(LogAnalysisScriptSet scriptSet)
	{
		if (!Dialogs.TryGetValue(scriptSet, out var dialog))
			dialog?.Close();
	}


	// Complete editing.
	async Task CompleteEditing()
	{
		var scriptSet = await this.ApplyAsync(true);
		if (scriptSet is not null)
			this.Close(scriptSet);
	}
	
	
	/// <summary>
	/// Command to complete editing.
	/// </summary>
	public ICommand CompleteEditingCommand { get; }


	/// <summary>
	/// Get or set whether the script set is embedded in another container or not.
	/// </summary>
	public bool IsEmbeddedScriptSet
	{
		get => this.GetValue(IsEmbeddedScriptSetProperty);
		init => this.SetValue(IsEmbeddedScriptSetProperty, value);
	}


	/// <summary>
	/// Check whether the editing script is newly created or not.
	/// </summary>
	public bool IsNewScriptSet => this.isNewScriptSet;


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.scriptSetToEdit != null 
			&& Dialogs.TryGetValue(this.scriptSetToEdit, out var dialog)
			&& ReferenceEquals(this, dialog))
		{
			Dialogs.Remove(this.scriptSetToEdit);
		}
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnFirstMeasurementCompleted(Size measuredSize)
	{
		// call base
		base.OnFirstMeasurementCompleted(measuredSize);
		
		// setup initial window size and position
		(this.Screens.ScreenFromWindow(this) ?? this.Screens.Primary)?.Let(screen =>
		{
			var workingArea = screen.WorkingArea;
			var widthRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitWidthRatio);
			var heightRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitHeightRatio);
			var scaling = screen.Scaling;
			var left = (workingArea.TopLeft.X + workingArea.Width * (1 - widthRatio) / 2); // in device pixels
			var top = (workingArea.TopLeft.Y + workingArea.Height * (1 - heightRatio) / 2); // in device pixels
			var sysDecorSize = this.GetSystemDecorationSizes();
			this.Position = new((int)(left + 0.5), (int)(top + 0.5));
			this.SynchronizationContext.Post(() =>
			{
				this.Width = (workingArea.Width * widthRatio) / scaling;
				this.Height = ((workingArea.Height * heightRatio) / scaling) - sysDecorSize.Top - sysDecorSize.Bottom;
			}, DispatcherPriority.Send);
		});
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		_ = this.OnOpenedAsync();
	}
	
	
	// Handle dialog opened asynchronously.
	async Task OnOpenedAsync()
	{
		// check number of script
		if (this.scriptSetToEdit == null
		    && !this.Application.ProductManager.IsProductActivated(Products.Professional))
		{
			if (!LogAnalysisScriptSetManager.Default.CanAddScriptSet)
			{
				await new MessageDialog
				{
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/LogAnalysisScriptSetEditorDialog.CannotAddMoreScriptSetWithoutProVersion"),
				}.ShowDialog(this);
				this.IsEnabled = false;
				this.SynchronizationContext.PostDelayed(this.Close, 300); // [Workaround] Prevent crashing on macOS.
				return;
			}
			if (!this.PersistentState.GetValueOrDefault(DonotShowRestrictionsWithNonProVersionKey))
			{
				var messageDialog = new MessageDialog
				{
					DoNotAskOrShowAgain = false,
					Icon = MessageDialogIcon.Information,
					Message = this.GetResourceObservable("String/LogAnalysisScriptSetEditorDialog.RestrictionsOfNonProVersion"),
				};
				await messageDialog.ShowDialog(this);
				if (messageDialog.DoNotAskOrShowAgain == true)
					this.PersistentState.SetValue(DonotShowRestrictionsWithNonProVersionKey, true);
			}
		}
		
		// enable running script
		await this.RequestEnablingRunningScriptAsync();

		// setup initial focus
		this.SynchronizationContext.Post(() =>
		{
			this.nameTextBox.Focus();
		});
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		// call base
		base.OnOpening(e);
		
		// show script
		var scriptSet = this.scriptSetToEdit;
		if (scriptSet is not null)
		{
			if (!this.IsEmbeddedScriptSet)
			{
				this.iconColorComboBox.SelectedItem = scriptSet.IconColor;
				this.iconComboBox.SelectedItem = scriptSet.Icon;
				this.nameTextBox.Text = scriptSet.Name;
			}
			this.contextBasedToggleSwitch.IsChecked = scriptSet.IsContextualBased;
		}
		else
		{
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
			this.SetAndRaise(IsNewScriptSetProperty, ref this.isNewScriptSet, true);
			this.contextBasedToggleSwitch.IsChecked = true;
		}
		this.isScriptSetShown = true;
	}


	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#LogAnalysisScript");
#pragma warning restore CA1822


	// Request running script.
	async Task RequestEnablingRunningScriptAsync()
	{
		if (!this.IsOpened || this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript))
			return;
		if (!await new EnableRunningScriptDialog().ShowDialog(this))
		{
			this.IsEnabled = false;
			this.SynchronizationContext.PostDelayed(this.Close, 300); // [Workaround] Prevent crashing on macOS.
		}
	}


	/// <summary>
	/// Get or set log analysis script set to be edited.
	/// </summary>
	public LogAnalysisScriptSet? ScriptSetToEdit
	{
		get => this.scriptSetToEdit;
		set
		{
			this.VerifyAccess();
			if (this.IsOpened)
				throw new InvalidOperationException();
			this.scriptSetToEdit = value;
		}
	}
	
	
	// URI of document of setup script.
	public Uri? SetupScriptDocumentUri => this.GetValue(SetupScriptDocumentUriProperty);


	/// <summary>
	/// Show dialog to edit script set.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="scriptSet">Script set to edit.</param>
	public static void Show(Avalonia.Controls.Window parent, LogAnalysisScriptSet? scriptSet)
	{
		if (scriptSet is not null && Dialogs.TryGetValue(scriptSet, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}
		dialog = new LogAnalysisScriptSetEditorDialog
		{
			scriptSetToEdit = scriptSet,
		};
		if (scriptSet is not null)
			Dialogs[scriptSet] = dialog;
		dialog.Show(parent);
	}
	
	
#if DEBUG
	/// <summary>
	/// Show dialog asynchronously to edit script set stored in file.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="fileName">Name of file which stored the script set.</param>
	/// <returns>True if dialog has been shown as expected.</returns>
	public static async Task<bool> ShowAsync(Avalonia.Controls.Window parent, string fileName)
	{
		// load script set
		var scriptSet = await Global.RunOrDefaultAsync(async () => await LogAnalysisScriptSet.LoadAsync(App.Current, fileName));
		if (scriptSet is null)
		{
			_ = new MessageDialog
			{
				Icon = MessageDialogIcon.Error,
				Message = $"Unable to load script set from '{fileName}'.",
			}.ShowDialog(parent);
			return false;
		}
		
		// edit
		var dialog = new LogAnalysisScriptSetEditorDialog
		{
			fileNameToSave = fileName,
			scriptSetToEdit = scriptSet,
		};
		dialog.Show(parent);
		return true;
	}
#endif


	// Update URIs of document of script.
	void UpdateDocumentUris()
	{ }
}
