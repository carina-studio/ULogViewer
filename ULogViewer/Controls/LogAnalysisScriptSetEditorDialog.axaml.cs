using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
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

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ContextualBasedAnalysisCondition"/>s.
/// </summary>
partial class LogAnalysisScriptSetEditorDialog : CarinaStudio.Controls.Window<IULogViewerApplication>
{
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from type of compilation result to brush.
	/// </summary>
	public static readonly IValueConverter CompilationResultTypeBrushConverter = new FuncValueConverter<CompilationResultType, IBrush?>(type =>
	{
		return type switch
		{
			CompilationResultType.Error => App.Current.FindResource("Brush/LogLevel.Error"),
			CompilationResultType.Warning => App.Current.FindResource("Brush/LogLevel.Warn"),
			CompilationResultType.Information => App.Current.FindResource("Brush/LogLevel.Info"),
			_ => App.Current.FindResource("Brush/LogLevel.Undefined"),
		} as IBrush;
	});
	/// <summary>
	/// <see cref="IValueConverter"/> to convert from type of compilation result to image.
	/// </summary>
	public static readonly IValueConverter CompilationResultTypeIconConverter = new FuncValueConverter<CompilationResultType, IImage?>(type =>
	{
		return type switch
		{
			CompilationResultType.Error => App.Current.FindResource("Image/Icon.Error.Outline.Colored"),
			CompilationResultType.Warning => App.Current.FindResource("Image/Icon.Warning.Outline.Colored"),
			CompilationResultType.Information => App.Current.FindResource("Image/Icon.Information.Outline.Colored"),
			_ => App.Current.FindResource("Image/Icon.Information.Outline"),
		} as IImage;
	});


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
		this.Get<Avalonia.Controls.ListBox>("analysisScriptCompilationResultListBox").Also(it =>
		{
			it.GetObservable(Avalonia.Controls.ListBox.SelectedItemProperty).Subscribe(item =>
			{
				if (item is CompilationResult result)
				{
					result.StartPosition?.Let(startPosition =>
					{
						var endPosition = result.EndPosition ?? (-1, -1);
						if (startPosition.Item1 == endPosition.Item1)
							this.analysisScriptEditor?.SelectAtLine(startPosition.Item1 + 1, startPosition.Item2, endPosition.Item2 - startPosition.Item2);
						else
							this.analysisScriptEditor?.SelectAtLine(startPosition.Item1 + 1, startPosition.Item2, 0);
					});
					this.SynchronizationContext.Post(() => 
					{
						this.analysisScriptEditor?.Focus();
						it.SelectedItem = null;
					});
				}
			});
		});
		this.analysisScriptEditor = this.Get<ScriptEditor>(nameof(analysisScriptEditor)).Also(it =>
		{
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ =>
			{
				analysisScript = null;
				this.SetAndRaise<bool>(IsCompilingAnalysisScriptProperty, ref this.isCompilingAnalysisScript, false);
				this.compileAnalysisScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			});
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
		this.Get<Avalonia.Controls.ListBox>("setupScriptCompilationResultListBox").Also(it =>
		{
			it.GetObservable(Avalonia.Controls.ListBox.SelectedItemProperty).Subscribe(item =>
			{
				if (item is CompilationResult result)
				{
					result.StartPosition?.Let(startPosition =>
					{
						var endPosition = result.EndPosition ?? (-1, -1);
						if (startPosition.Item1 == endPosition.Item1)
							this.setupScriptEditor?.SelectAtLine(startPosition.Item1 + 1, startPosition.Item2, endPosition.Item2 - startPosition.Item2);
						else
							this.setupScriptEditor?.SelectAtLine(startPosition.Item1 + 1, startPosition.Item2, 0);
					});
					this.SynchronizationContext.Post(() => 
					{
						this.setupScriptEditor?.Focus();
						it.SelectedItem = null;
					});
				}
			});
		});
		this.setupScriptEditor = this.Get<ScriptEditor>(nameof(setupScriptEditor)).Also(it =>
		{
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ =>
			{
				setupScript = null;
				this.SetAndRaise<bool>(IsCompilingSetupScriptProperty, ref this.isCompilingSetupScript, false);
				this.compileSetupScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			});
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
		result = string.CompareOrdinal(lhs.Message, rhs.Message);
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
	LogAnalysisScript? CreateScript(ScriptEditor? editor) => editor?.Source?.Trim()?.Let(source =>
	{
		if (string.IsNullOrEmpty(source))
			return null;
		return new LogAnalysisScript(this.Application, Scripting.ScriptLanguage.CSharp, source);
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
		this.SynchronizationContext.Post(() =>
		{
			this.Get<ScrollViewer>("contentScrollViewer").ScrollToHome();
			this.nameTextBox.Focus();
		});
	}


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
