using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
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
	readonly ScriptEditor analysisScriptEditor;
	bool areValidParameters;
	readonly ComboBox iconComboBox;
	readonly TextBox nameTextBox;
	LogAnalysisScriptSet? scriptSetToEdit;
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
			//
		});
		this.iconComboBox = this.Get<ComboBox>(nameof(iconComboBox));
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.setupScriptEditor = this.Get<ScriptEditor>(nameof(setupScriptEditor)).Also(it =>
		{
			//
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
		scriptSet.AnalysisScript = this.analysisScriptEditor.Source?.Trim()?.Let(source =>
		{
			if (string.IsNullOrEmpty(source))
				return null;
			return new LogAnalysisScript(Scripting.ScriptLanguage.CSharp, source);
		});
		scriptSet.SetupScript = this.setupScriptEditor.Source?.Trim()?.Let(source =>
		{
			if (string.IsNullOrEmpty(source))
				return null;
			return new LogAnalysisScript(Scripting.ScriptLanguage.CSharp, source);
		});
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
		base.OnOpened(e);
		var scriptSet = this.scriptSetToEdit;
		if (scriptSet != null)
		{
			this.analysisScriptEditor.Source = scriptSet.AnalysisScript?.Source;
			this.iconComboBox.SelectedItem = scriptSet.Icon;
			this.nameTextBox.Text = scriptSet.Name;
			this.setupScriptEditor.Source = scriptSet.SetupScript?.Source;
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
