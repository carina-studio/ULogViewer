using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

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
	LogAnalysisScript? analysisScript;
	readonly ScriptEditor analysisScriptEditor;
	bool areValidParameters;
	readonly ScheduledAction compileAnalysisScriptAction;
	readonly ScheduledAction compileSetupScriptAction;
	readonly ComboBox iconComboBox;
	readonly TextBox nameTextBox;
	LogAnalysisScriptSet? scriptSetToEdit;
	LogAnalysisScript? setupScript;
	readonly ScriptEditor setupScriptEditor;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="LogAnalysisScriptSetEditorDialog"/> instance.
	/// </summary>
	public LogAnalysisScriptSetEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.analysisScriptEditor = this.Get<ScriptEditor>(nameof(analysisScriptEditor)).Also(it =>
		{
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ =>
			{
				analysisScript = null;
				this.compileAnalysisScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			});
		});
		this.compileAnalysisScriptAction = new(async () =>
		{
			var analysisScript = this.analysisScript ?? this.CreateScript(this.analysisScriptEditor).Also(it => this.analysisScript = it);
			if (analysisScript != null)
			{
				this.Logger.LogTrace("Start compiling analysis script");
				var result = await analysisScript.CompileAsync();
				if (this.analysisScript != analysisScript)
					return;
				this.Logger.LogTrace("Analysis script compilation " + (result ? "succeeded" : "failed") + ", result count: " + analysisScript.CompilationResults.Count);
			}
		});
		this.compileSetupScriptAction = new(async () =>
		{
			var setupScript = this.setupScript ?? this.CreateScript(this.setupScriptEditor).Also(it => this.setupScript = it);
			if (setupScript != null)
			{
				this.Logger.LogTrace("Start compiling setup script");
				var result = await setupScript.CompileAsync();
				if (this.setupScript != setupScript)
					return;
				this.Logger.LogTrace("Setup script compilation " + (result ? "succeeded" : "failed") + ", result count: " + setupScript.CompilationResults.Count);
			}
		});
		this.iconComboBox = this.Get<ComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.setupScriptEditor = this.Get<ScriptEditor>(nameof(setupScriptEditor)).Also(it =>
		{
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ =>
			{
				setupScript = null;
				this.compileSetupScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			});
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
	LogAnalysisScript? CreateScript(ScriptEditor? editor) => editor?.Source?.Trim()?.Let(source =>
	{
		if (string.IsNullOrEmpty(source))
			return null;
		return new LogAnalysisScript(Scripting.ScriptLanguage.CSharp, source);
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
		base.OnOpened(e);
		var scriptSet = this.scriptSetToEdit;
		if (scriptSet != null)
		{
			this.analysisScript = scriptSet.AnalysisScript;
			this.analysisScriptEditor.Source = this.analysisScript?.Source;
			this.iconComboBox.SelectedItem = scriptSet.Icon;
			this.nameTextBox.Text = scriptSet.Name;
			this.setupScript = scriptSet.SetupScript;
			this.setupScriptEditor.Source = this.setupScript?.Source;
			this.compileAnalysisScriptAction.Schedule();
			this.compileSetupScriptAction.Schedule();
		}
		else
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
		this.SynchronizationContext.Post(this.nameTextBox.Focus);
	}


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
