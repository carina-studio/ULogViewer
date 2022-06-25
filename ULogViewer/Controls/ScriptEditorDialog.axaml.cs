using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Scripting;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="Script{TContext}"/>.
/// </summary>
partial class ScriptEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
{
	// Static fields.
	static readonly AvaloniaProperty<bool> AreValidParametersProperty = AvaloniaProperty.RegisterDirect<ScriptEditorDialog, bool>("AreValidParameters", d => d.areValidParameters);
	static readonly AvaloniaProperty<bool> IsSourceEditorFocusedProperty = AvaloniaProperty.RegisterDirect<ScriptEditorDialog, bool>("IsSourceEditorFocused", d => d.isSourceEditorFocused);


	// Fields.
	bool areValidParameters;
	bool isSourceEditorFocused;
	readonly TextEditor sourceEditor;
	readonly Dictionary<ScriptLanguage, IHighlightingDefinition> syntaxHighlightingDefs = new()
	{
		{ ScriptLanguage.CSharp, HighlightingManager.Instance.GetDefinition("C#") },
	};
	readonly ScheduledAction updateSyntaxHighlightingAction;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="ScriptEditorDialog"/> instance.
	/// </summary>
	public ScriptEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.sourceEditor = this.Get<TextEditor>(nameof(sourceEditor)).Also(it =>
		{
			it.ShowLineNumbers = true;
			it.TextArea.Let(textArea =>
			{
				textArea.GotFocus += (sender, e) => this.SetAndRaise<bool>(IsSourceEditorFocusedProperty, ref this.isSourceEditorFocused, true);
				textArea.LostFocus += (sender, e) => this.SetAndRaise<bool>(IsSourceEditorFocusedProperty, ref this.isSourceEditorFocused, false);
				textArea.Options.EnableEmailHyperlinks = true;
				textArea.Options.EnableHyperlinks = true;
				textArea.Options.EnableImeSupport = true;
				textArea.Options.EnableRectangularSelection = true;
				textArea.Options.EnableTextDragDrop = true;
				textArea.Options.HighlightCurrentLine = true;
				textArea.Options.RequireControlModifierForHyperlinkClick = true;
			});
			it.TextChanged += (_, e) => this.validateParametersAction?.Schedule(300);
		});
		this.updateSyntaxHighlightingAction = new(() =>
		{
			if (this.syntaxHighlightingDefs.TryGetValue(this.Language, out var definition))
				this.sourceEditor.SyntaxHighlighting = definition;
		});
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise<bool>(AreValidParametersProperty, ref this.areValidParameters, !string.IsNullOrEmpty(this.sourceEditor.Text));
		});
		foreach (var (language, definition) in this.syntaxHighlightingDefs)
		{
			switch (language)
			{
				case ScriptLanguage.CSharp:
					{
						var baseColor = (Color)this.FindResource("SystemBaseHighColor").AsNonNull();
						var cfKeywordColor = (Color)this.FindResource("Color/SyntaxHighlighting.CSharp.Keyword.ControlFlow").AsNonNull();
						var commentColor = (Color)this.FindResource("Color/SyntaxHighlighting.CSharp.Comment").AsNonNull();
						var keywordColor = (Color)this.FindResource("Color/SyntaxHighlighting.CSharp.Keyword").AsNonNull();
						var numberColor = (Color)this.FindResource("Color/SyntaxHighlighting.CSharp.Number").AsNonNull();
						var stringColor = (Color)this.FindResource("Color/SyntaxHighlighting.CSharp.String").AsNonNull();
						foreach (var rule in definition.MainRuleSet.Rules)
						{
							var pattern = rule.Regex.ToString();
							if (pattern.StartsWith("\\b(?>this") 
								|| pattern.StartsWith("\\b(?>stackalloc")
								|| pattern.StartsWith("\\b(?>false")
								|| pattern.StartsWith("\\b(?>descending")
								|| pattern.StartsWith("\\b(?>unchecked")
								|| pattern.StartsWith("\\b(?>unsafe")
								|| pattern.StartsWith("\\b(?>decimal")
								|| pattern.StartsWith("\\b(?>interface")
								|| pattern.StartsWith("\\b(?>explicit")
								|| pattern.StartsWith("\\b(?>params")
								|| pattern.StartsWith("\\b(?>abstract")
								|| pattern.StartsWith("\\b(?>protected")
								|| pattern.StartsWith("\\b(?>namespace")
								|| pattern.StartsWith("\\b(?>remove")
								|| pattern.StartsWith("\\b(?>value")
								|| pattern.StartsWith("\\b(?>nameof"))
							{
								rule.Color = new() { Foreground = new SimpleHighlightingBrush(keywordColor) };
							}
							else if (pattern.StartsWith("\\b(?>default")
								|| pattern.StartsWith("\\b(?>finally")
								|| pattern.StartsWith("\\b(?>continue"))
							{
								rule.Color = new() { Foreground = new SimpleHighlightingBrush(cfKeywordColor) };
							}
							else if (pattern.StartsWith("\n\t\t\t\\b0[xX]"))
								rule.Color = new() { Foreground = new SimpleHighlightingBrush(numberColor) };
							else
								rule.Color = new() { Foreground = new SimpleHighlightingBrush(baseColor) };
						}
						foreach (var span in definition.MainRuleSet.Spans)
						{
							var pattern = span.StartExpression.ToString();
							if (pattern.StartsWith("//")
								|| pattern.StartsWith("/\\*"))
							{
								span.SpanColor = new HighlightingColor() { Foreground = new SimpleHighlightingBrush(commentColor) };
							}
							else if (pattern.StartsWith("\"")
								|| pattern.StartsWith("@\"")
								|| pattern.StartsWith("$\"")
								|| pattern.StartsWith("\'"))
							{
								span.SpanColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
							}
							else
								span.SpanColor = new HighlightingColor() { Foreground = new SimpleHighlightingBrush(baseColor) };
						}
					}
					break;
			}
		}
		if (this.TryFindResource("SystemAccentColor", out var res) && res is Color accentColor)
			this.Resources["Brush/TextArea.Selection.Background"] = new SolidColorBrush(Color.FromArgb(0x70, accentColor.R, accentColor.G, accentColor.B));
	}


	// Complete script editing.
	void CompleteEditing()
	{
		// check state
		this.validateParametersAction.ExecuteIfScheduled();
		if (!this.areValidParameters)
			return;
		
		// complete
		this.Source = this.sourceEditor.Text;
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
		this.sourceEditor.Text = this.Source;
		this.updateSyntaxHighlightingAction.Schedule();
		this.validateParametersAction.Reschedule();
		this.SynchronizationContext.Post(this.sourceEditor.Focus);
	}
	

	/// <summary>
	/// Get or set source code of script.
	/// </summary>
	public string? Source { get; set; }
}