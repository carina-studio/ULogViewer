using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Scripting;
using System;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="Script{TContext}"/>.
/// </summary>
partial class ScriptEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
{
	// Static fields.
	static readonly AvaloniaProperty<bool> AreValidParametersProperty = AvaloniaProperty.RegisterDirect<ScriptEditorDialog, bool>("AreValidParameters", d => d.areValidParameters);


	// Fields.
	bool areValidParameters;
	readonly ScriptEditor sourceEditor;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="ScriptEditorDialog"/> instance.
	/// </summary>
	public ScriptEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.sourceEditor = this.Get<ScriptEditor>(nameof(sourceEditor)).Also(it =>
		{
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ => this.validateParametersAction?.Schedule(300));
		});
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise<bool>(AreValidParametersProperty, ref this.areValidParameters, !string.IsNullOrEmpty(this.sourceEditor.Source));
		});
	}


	// Complete script editing.
	void CompleteEditing()
	{
		// check state
		this.validateParametersAction.ExecuteIfScheduled();
		if (!this.areValidParameters)
			return;
		
		// complete
		this.Language = this.sourceEditor.Language;
		this.Source = this.sourceEditor.Source;
		this.Close();
	}


	/// <summary>
	/// Get or set language of script.
	/// </summary>
	public ScriptLanguage Language { get; set; } = ScriptLanguage.CSharp;


	// Dialog opened.
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.sourceEditor.Source = this.Source;
		this.validateParametersAction.Reschedule();
		this.SynchronizationContext.Post(this.sourceEditor.Focus);
	}
	

	/// <summary>
	/// Get or set source code of script.
	/// </summary>
	public string? Source { get; set; }
}