using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Scripting;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Viewer to show <see cref="CompilationResult"/>s.
/// </summary>
partial class ScriptCompilationResultViewer : CarinaStudio.Controls.UserControl<IULogViewerApplication>
{
	/// <summary>
	/// Property of <see cref="CompilationResults"/>.
	/// </summary>
	public static readonly AvaloniaProperty<IList<CompilationResult>?> CompilationResultsProperty = AvaloniaProperty.Register<ContextualBasedAnalysisActionsEditor, IList<CompilationResult>?>(nameof(CompilationResults));
	/// <summary>
	/// Property of <see cref="ScriptEditor"/>.
	/// </summary>
	public static readonly AvaloniaProperty<ScriptEditor?> ScriptEditorProperty = AvaloniaProperty.Register<ContextualBasedAnalysisActionsEditor, ScriptEditor?>(nameof(ScriptEditor));


	/// <summary>
	/// Initialize new <see cref="ScriptCompilationResultViewer"/> instance.
	/// </summary>
	public ScriptCompilationResultViewer()
	{
		AvaloniaXamlLoader.Load(this);
		this.Get<Avalonia.Controls.ListBox>("compilationResultListBox").Also(it =>
		{
			it.GetObservable(Avalonia.Controls.ListBox.SelectedItemProperty).Subscribe(item =>
			{
				if (item is CompilationResult result)
				{
					result.StartPosition?.Let(startPosition =>
					{
						var endPosition = result.EndPosition ?? (-1, -1);
						if (startPosition.Item1 == endPosition.Item1)
							this.ScriptEditor?.SelectAtLine(startPosition.Item1 + 1, startPosition.Item2, endPosition.Item2 - startPosition.Item2);
						else
							this.ScriptEditor?.SelectAtLine(startPosition.Item1 + 1, startPosition.Item2, 0);
					});
					this.SynchronizationContext.Post(() => 
					{
						this.ScriptEditor?.Focus();
						it.SelectedItem = null;
					});
				}
			});
		});
	}


	/// <summary>
	/// Get or set compilation results to be shown.
	/// </summary>
	public IList<CompilationResult>? CompilationResults
	{
		get => this.GetValue<IList<CompilationResult>?>(CompilationResultsProperty);
		set => this.SetValue<IList<CompilationResult>?>(CompilationResultsProperty, value);
	}


	/// <summary>
	/// Get or set <see cref="ScriptEditor"/> which provide script editing functions.
	/// </summary>
	public ScriptEditor? ScriptEditor
	{
		get => this.GetValue<ScriptEditor?>(ScriptEditorProperty);
		set => this.SetValue<ScriptEditor?>(ScriptEditorProperty, value);
	}
}