using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.Scripting;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
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
	static readonly AvaloniaProperty<bool> IsCompilingAnalysisScriptProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, bool>("IsCompilingAnalysisScript", d => d.isCompilingAnalysisScript);
	static readonly AvaloniaProperty<bool> IsCompilingSetupScriptProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, bool>("IsCompilingSetupScript", d => d.isCompilingSetupScript);


	// Fields.
	LogAnalysisScript? analysisScript;
	readonly SortedObservableList<CompilationResult> analysisScriptCompilationResults = new(CompareCompilationResult);
	readonly ScriptEditor analysisScriptEditor;
	bool areValidParameters;
	readonly ScheduledAction compileAnalysisScriptAction;
	readonly ScheduledAction compileSetupScriptAction;
	readonly ComboBox iconComboBox;
	bool isCompilingAnalysisScript;
	bool isCompilingSetupScript;
	readonly TextBox nameTextBox;
	LogAnalysisScriptSet? scriptSetToEdit;
	LogAnalysisScript? setupScript;
	readonly SortedObservableList<CompilationResult> setupScriptCompilationResults = new(CompareCompilationResult);
	readonly ScriptEditor setupScriptEditor;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="LogAnalysisScriptSetEditorDialog"/> instance.
	/// </summary>
	public LogAnalysisScriptSetEditorDialog()
	{
		this.AnalysisScriptCompilationResults = this.analysisScriptCompilationResults.AsReadOnly();
		this.SetupScriptCompilationResults = this.setupScriptCompilationResults.AsReadOnly();
		AvaloniaXamlLoader.Load(this);
		this.analysisScriptEditor = this.Get<ScriptEditor>(nameof(analysisScriptEditor)).Also(it =>
		{
			void ScheduleCompilation()
			{
				analysisScript = null;
				this.SetAndRaise<bool>(IsCompilingAnalysisScriptProperty, ref this.isCompilingAnalysisScript, false);
				this.compileAnalysisScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			}
			it.GetObservable(ScriptEditor.LanguageProperty).Subscribe(_ => ScheduleCompilation());
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ => ScheduleCompilation());
		});
		this.compileAnalysisScriptAction = new(async () =>
		{
			var analysisScript = this.analysisScript ?? this.CreateScript(this.analysisScriptEditor).Also(it => this.analysisScript = it);
			if (analysisScript != null)
			{
				this.Logger.LogTrace("Start compiling analysis script");
				this.SetAndRaise<bool>(IsCompilingAnalysisScriptProperty, ref this.isCompilingAnalysisScript, true);
				var result = await analysisScript.CompileAsync();
				if (this.analysisScript != analysisScript)
					return;
				this.Logger.LogTrace("Analysis script compilation " + (result ? "succeeded" : "failed") + ", result count: " + analysisScript.CompilationResults.Count);
				this.SetAndRaise<bool>(IsCompilingAnalysisScriptProperty, ref this.isCompilingAnalysisScript, false);
				this.analysisScriptCompilationResults.Clear();
				this.analysisScriptCompilationResults.AddAll(analysisScript.CompilationResults);
			}
			else
				this.analysisScriptCompilationResults.Clear();
		});
		this.compileSetupScriptAction = new(async () =>
		{
			var setupScript = this.setupScript ?? this.CreateScript(this.setupScriptEditor).Also(it => this.setupScript = it);
			if (setupScript != null)
			{
				this.Logger.LogTrace("Start compiling setup script");
				this.SetAndRaise<bool>(IsCompilingSetupScriptProperty, ref this.isCompilingSetupScript, true);
				var result = await setupScript.CompileAsync();
				if (this.setupScript != setupScript)
					return;
				this.Logger.LogTrace("Setup script compilation " + (result ? "succeeded" : "failed") + ", result count: " + setupScript.CompilationResults.Count);
				this.SetAndRaise<bool>(IsCompilingSetupScriptProperty, ref this.isCompilingSetupScript, false);
				this.setupScriptCompilationResults.Clear();
				this.setupScriptCompilationResults.AddAll(setupScript.CompilationResults);
			}
			else
				this.setupScriptCompilationResults.Clear();
		});
		this.iconComboBox = this.Get<ComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.setupScriptEditor = this.Get<ScriptEditor>(nameof(setupScriptEditor)).Also(it =>
		{
			void ScheduleCompilation()
			{
				setupScript = null;
				this.SetAndRaise<bool>(IsCompilingSetupScriptProperty, ref this.isCompilingSetupScript, false);
				this.compileSetupScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			}
			it.GetObservable(ScriptEditor.LanguageProperty).Subscribe(_ => ScheduleCompilation());
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ => ScheduleCompilation());
		});
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise<bool>(AreValidParametersProperty, ref this.areValidParameters, !string.IsNullOrWhiteSpace(this.nameTextBox.Text));
		});
	}


	// Get compilation results of analysis script.
	IList<CompilationResult> AnalysisScriptCompilationResults { get; }


	/// <summary>
	/// Close all window related to given script set.
	/// </summary>
	/// <param name="scriptSet">Script set.</param>
	public static void CloseAll(LogAnalysisScriptSet scriptSet)
	{
		if (!Dialogs.TryGetValue(scriptSet, out var dialog))
			dialog?.Close();
	}


	// Compare compilation result.
	static int CompareCompilationResult(CompilationResult lhs, CompilationResult rhs)
	{
		var result = (int)rhs.Type - (int)lhs.Type;
		if (result != 0)
			return result;
		result = lhs.StartPosition.GetValueOrDefault().Item1 - rhs.StartPosition.GetValueOrDefault().Item1;
		if (result != 0)
			return result;
		result = lhs.StartPosition.GetValueOrDefault().Item2 - rhs.StartPosition.GetValueOrDefault().Item2;
		if (result != 0)
			return result;
		result = string.Compare(lhs.Message, rhs.Message, true, CultureInfo.InvariantCulture);
		if (result != 0)
			return result;
		return lhs.GetHashCode() - rhs.GetHashCode();
	}


	// Complete editing.
	void CompleteEditing()
	{
		this.compileAnalysisScriptAction.Cancel();
		this.compileSetupScriptAction.Cancel();
		var scriptSet = this.scriptSetToEdit ?? new(this.Application);
		scriptSet.Name = this.nameTextBox.Text?.Trim();
		scriptSet.Icon = (LogProfileIcon)this.iconComboBox.SelectedItem.AsNonNull();
		scriptSet.AnalysisScript = this.analysisScript ?? this.CreateScript(this.analysisScriptEditor);
		scriptSet.SetupScript = this.setupScript ?? this.CreateScript(this.setupScriptEditor);
		if (!LogAnalysisScriptSetManager.Default.ScriptSets.Contains(scriptSet))
			LogAnalysisScriptSetManager.Default.AddScriptSet(scriptSet);
		this.Close(scriptSet);
	}


	// Create script.
	LogAnalysisScript? CreateScript(ScriptEditor? editor) => editor?.Source?.Let(source =>
	{
		if (string.IsNullOrEmpty(source))
			return null;
		return new LogAnalysisScript(this.Application, editor.Language, source);
	});


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
		this.compileAnalysisScriptAction.Cancel();
		this.compileSetupScriptAction.Cancel();
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
			this.analysisScript = scriptSet.AnalysisScript;
			this.analysisScript?.Let(script =>
			{
				this.analysisScriptEditor.Language = script.Language;
				this.analysisScriptEditor.Source = script.Source;
			});
			this.iconComboBox.SelectedItem = scriptSet.Icon;
			this.nameTextBox.Text = scriptSet.Name;
			this.setupScript = scriptSet.SetupScript;
			this.setupScript?.Let(script =>
			{
				this.setupScriptEditor.Language = script.Language;
				this.setupScriptEditor.Source = script.Source;
			});
			this.compileAnalysisScriptAction.Schedule();
			this.compileSetupScriptAction.Schedule();
		}
		else
		{
			this.analysisScriptEditor.Language = this.Settings.GetValueOrDefault(SettingKeys.DefaultScriptLanguage);
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
			this.setupScriptEditor.Language = this.Settings.GetValueOrDefault(SettingKeys.DefaultScriptLanguage);
		}

		// setup initial focus
		this.SynchronizationContext.Post(() =>
		{
			this.Get<ScrollViewer>("contentScrollViewer").ScrollToHome();
			this.nameTextBox.Focus();
		});

		// enable script running
		if (!this.Settings.GetValueOrDefault(SettingKeys.EnableRunningScript))
		{
			this.SynchronizationContext.Post(async () =>
			{
				if (this.Settings.GetValueOrDefault(SettingKeys.EnableRunningScript) || this.IsClosed)
					return;
				var result = await new MessageDialog()
				{
					Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
					DefaultResult = AppSuite.Controls.MessageDialogResult.No,
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = this.Application.GetString("Script.MessageForEnablingRunningScript"),
				}.ShowDialog(this);
				if (result == AppSuite.Controls.MessageDialogResult.Yes)
					this.Settings.SetValue<bool>(SettingKeys.EnableRunningScript, true);
				else
					this.Close();
			});
		}
	}


	// Open online documentation.
	void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#LogAnalysisScript");


	// Get compilation results of setup script.
	IList<CompilationResult> SetupScriptCompilationResults { get; }


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
