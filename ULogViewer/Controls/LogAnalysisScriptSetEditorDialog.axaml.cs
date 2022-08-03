using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ContextualBasedAnalysisCondition"/>s.
/// </summary>
partial class LogAnalysisScriptSetEditorDialog : CarinaStudio.Controls.Window<IULogViewerApplication>
{
	// Static fields.
	static readonly AvaloniaProperty<bool> AreValidParametersProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, bool>("AreValidParameters", d => d.areValidParameters);
	static readonly Dictionary<LogAnalysisScriptSet, LogAnalysisScriptSetEditorDialog> Dialogs = new();


	// Fields.
	bool areValidParameters;
	readonly ComboBox iconComboBox;
	readonly TextBox nameTextBox;
	LogAnalysisScriptSet? scriptSetToEdit;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="LogAnalysisScriptSetEditorDialog"/> instance.
	/// </summary>
	public LogAnalysisScriptSetEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.iconComboBox = this.Get<ComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise<bool>(AreValidParametersProperty, ref this.areValidParameters, !string.IsNullOrWhiteSpace(this.nameTextBox.Text));
		});
	}


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
	void CompleteEditing()
	{
		var scriptSet = this.scriptSetToEdit ?? new(this.Application);
		scriptSet.Name = this.nameTextBox.Text?.Trim();
		scriptSet.Icon = (LogProfileIcon)this.iconComboBox.SelectedItem.AsNonNull();
		if (!LogAnalysisScriptSetManager.Default.ScriptSets.Contains(scriptSet))
			LogAnalysisScriptSetManager.Default.AddScriptSet(scriptSet);
		this.Close(scriptSet);
	}


	// Get available icons.
	LogProfileIcon[] Icons { get; } = Enum.GetValues<LogProfileIcon>();


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.scriptSetToEdit != null 
			&& Dialogs.TryGetValue(this.scriptSetToEdit, out var dialog)
			&& this == dialog)
		{
			Dialogs.Remove(this.scriptSetToEdit);
		}
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		// call base
		base.OnOpened(e);

		// setup initial window size and position
		(this.Screens.ScreenFromWindow(this.PlatformImpl) ?? this.Screens.Primary)?.Let(screen =>
		{
			var workingArea = screen.WorkingArea;
			var widthRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitWidthRatio);
			var heightRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitHeightRatio);
			var pixelDensity = Platform.IsMacOS ? 1.0 : screen.PixelDensity;
			var left = (workingArea.TopLeft.X + workingArea.Width * (1 - widthRatio) / 2); // in device pixels
			var top = (workingArea.TopLeft.Y + workingArea.Height * (1 - heightRatio) / 2); // in device pixels
			var width = (workingArea.Width * widthRatio) / pixelDensity;
			var height = (workingArea.Height * heightRatio) / pixelDensity;
			var sysDecorSize = this.GetSystemDecorationSizes();
			this.Position = new((int)(left + 0.5), (int)(top + 0.5));
			this.Width = width;
			this.Height = (height - sysDecorSize.Top - sysDecorSize.Bottom);
		});

		// show script
		var scriptSet = this.scriptSetToEdit;
		if (scriptSet != null)
		{
			this.iconComboBox.SelectedItem = scriptSet.Icon;
			this.nameTextBox.Text = scriptSet.Name;
		}
		else
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;

		// setup initial focus
		this.SynchronizationContext.Post(() =>
		{
			this.Get<ScrollViewer>("contentScrollViewer").ScrollToHome();
			this.nameTextBox.Focus();
		});

		// enable script running
		if (!this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript))
		{
			this.SynchronizationContext.PostDelayed(async () =>
			{
				if (this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript) || this.IsClosed)
					return;
				var result = await new MessageDialog()
				{
					Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
					DefaultResult = AppSuite.Controls.MessageDialogResult.No,
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/ApplicationOptions.EnableRunningScript.ConfirmEnabling"),
				}.ShowDialog(this);
				if (result == AppSuite.Controls.MessageDialogResult.Yes)
					this.Settings.SetValue<bool>(AppSuite.SettingKeys.EnableRunningScript, true);
				else
					this.Close();
			}, 300);
		}
	}


	// Open online documentation.
	void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#LogAnalysisScript");


	/// <summary>
	/// Show dialog to edit script set.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="scriptSet">Script set to edit.</param>
	public static void Show(Avalonia.Controls.Window parent, LogAnalysisScriptSet? scriptSet)
	{
		if (scriptSet != null && Dialogs.TryGetValue(scriptSet, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}
		dialog = new LogAnalysisScriptSetEditorDialog()
		{
			scriptSetToEdit = scriptSet,
		};
		if (scriptSet != null)
			Dialogs[scriptSet] = dialog;
		dialog.Show(parent);
	}
}
