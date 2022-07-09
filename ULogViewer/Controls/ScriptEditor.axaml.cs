using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Scripting;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ContextualBasedAnalysisCondition"/>s.
/// </summary>
partial class ScriptEditor : CarinaStudio.Controls.UserControl<IULogViewerApplication>
{
	/// <summary>
	/// Property of <see cref="CanScrollHorizontally"/>.
	/// </summary>
	public static readonly AvaloniaProperty<bool> CanScrollHorizontallyProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, bool>(nameof(CanScrollHorizontally), c => c.canScrollHorizontally);
	/// <summary>
	/// Property of <see cref="CanScrollVertically"/>.
	/// </summary>
	public static readonly AvaloniaProperty<bool> CanScrollVerticallyProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, bool>(nameof(CanScrollVertically), c => c.canScrollVertically);
	/// <summary>
	/// Property of <see cref="HorizontalScrollBarVisibility"/>.
	/// </summary>
	public static readonly AvaloniaProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, ScrollBarVisibility>(nameof(HorizontalScrollBarVisibility), c => c.horzScrollBarVisibility);
	/// <summary>
	/// Property of <see cref="IsReadOnly"/>.
	/// </summary>
	public static readonly AvaloniaProperty<bool> IsReadOnlyProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, bool>(nameof(IsReadOnly), c => c.isReadOnly);
	/// <summary>
	/// Property of <see cref="Language"/>.
	/// </summary>
	public static readonly AvaloniaProperty<ScriptLanguage> LanguageProperty = AvaloniaProperty.Register<ScriptEditor, ScriptLanguage>(nameof(Language), ScriptLanguage.CSharp);
	/// <summary>
	/// Property of <see cref="Source"/>.
	/// </summary>
	public static readonly AvaloniaProperty<string?> SourceProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, string?>(nameof(Source), c => c.source);
	/// <summary>
	/// Property of <see cref="VerticalScrollBarVisibility"/>.
	/// </summary>
	public static readonly AvaloniaProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, ScrollBarVisibility>(nameof(VerticalScrollBarVisibility), c => c.vertScrollBarVisibility);


	// Static fields.
	static readonly AvaloniaProperty<bool> IsSourceEditorFocusedProperty = AvaloniaProperty.RegisterDirect<ScriptEditor, bool>("IsSourceEditorFocused", d => d.isSourceEditorFocused);


	// Fields.
	bool canScrollHorizontally;
	bool canScrollVertically;
	readonly List<HighlightingSpan> cjkSpans = new();
	string? defaultCjkFontFamilyName;
	IObservable<object?>? defaultCjkFontFamilyNameObservable;
	IDisposable? defaultCjkFontFamilyNameObserverToken;
	string? defaultFontFamilyName;
	IObservable<object?>? defaultFontFamilyNameObservable;
	IDisposable? defaultFontFamilyNameObserverToken;
	ScrollBarVisibility horzScrollBarVisibility;
	bool isReadOnly;
	bool isSourceEditorFocused;
	string? source;
	readonly TextEditor sourceEditor;
	readonly TextArea sourceEditorArea;
	readonly Dictionary<ScriptLanguage, IHighlightingDefinition> syntaxHighlightingDefs = new()
	{
		{ ScriptLanguage.CSharp, HighlightingManager.Instance.GetDefinition("C#") },
		{ ScriptLanguage.JavaScript, HighlightingManager.Instance.GetDefinition("JavaScript") },
	};
	readonly ScheduledAction updateCjkSpanFontFamilies;
	readonly ScheduledAction updateFontFamilyAndSizeAction;
	readonly ScheduledAction updateSyntaxHighlightingAction;
	ScrollBarVisibility vertScrollBarVisibility;


	/// <summary>
	/// Initialize new <see cref="ScriptEditor"/> instance.
	/// </summary>
	public ScriptEditor()
	{
		AvaloniaXamlLoader.Load(this);
		this.AddHandler(PointerWheelChangedEvent, (object? sender, PointerWheelEventArgs e) =>
		{
			var intercept = false;
			if (!this.GetValue<bool>(IsSourceEditorFocusedProperty))
				intercept = true;
			else if (Math.Abs(e.Delta.Y) >= Math.Abs(e.Delta.X))
				intercept = !this.canScrollVertically;
			else
				intercept = !this.canScrollHorizontally;
			if (intercept)
			{
				this.Parent?.RaiseEvent(e);
				e.Handled = true;
			}
		}, RoutingStrategies.Tunnel);
		this.sourceEditor = this.Get<TextEditor>(nameof(sourceEditor)).Also(it =>
		{
			it.EffectiveViewportChanged += (_, e) => this.ReportCanScrolling();
			it.GetObservable(TextEditor.HorizontalScrollBarVisibilityProperty).Subscribe(visibility => 
			{
				this.SetAndRaise<ScrollBarVisibility>(HorizontalScrollBarVisibilityProperty, ref this.horzScrollBarVisibility, visibility);
				this.ReportCanScrolling();
			});
			it.ShowLineNumbers = true;
			it.TextChanged += (_, e) => 
			{
				this.SetAndRaise<string?>(SourceProperty, ref this.source, it.Text);
				this.ReportCanScrolling();
			};
			it.GetObservable(TextEditor.VerticalScrollBarVisibilityProperty).Subscribe(visibility => 
			{
				this.SetAndRaise<ScrollBarVisibility>(VerticalScrollBarVisibilityProperty, ref this.vertScrollBarVisibility, visibility);
				this.ReportCanScrolling();
			});
		});
		this.sourceEditorArea = this.sourceEditor.TextArea.Also(it =>
		{
			it.GotFocus += (sender, e) => 
			{
				this.SynchronizationContext.Post(() => it.Options.HighlightCurrentLine = true);
				this.SetAndRaise<bool>(IsSourceEditorFocusedProperty, ref this.isSourceEditorFocused, true);
			};
			it.LostFocus += (sender, e) => 
			{
				this.SynchronizationContext.Post(() => it.Options.HighlightCurrentLine = false);
				this.SetAndRaise<bool>(IsSourceEditorFocusedProperty, ref this.isSourceEditorFocused, false);
			};
			it.TextEntered += (_, e) =>
			{
				var text = e.Text;
			};
			it.TextEntering += this.OnTextEntering;
			it.Options.EnableEmailHyperlinks = true;
			it.Options.EnableHyperlinks = true;
			it.Options.EnableImeSupport = true;
			it.Options.EnableRectangularSelection = true;
			it.Options.EnableTextDragDrop = true;
			it.Options.RequireControlModifierForHyperlinkClick = true;
		});
		this.updateCjkSpanFontFamilies = new(() =>
		{
			if (this.cjkSpans.IsEmpty())
				return;
			var fontFamilyNames = this.Settings.GetValueOrDefault(SettingKeys.ScriptEditorFontFamily).Let(it =>
			{
				if (string.IsNullOrWhiteSpace(it))
					return this.defaultCjkFontFamilyName;
				return it;
			});
			if (!string.IsNullOrWhiteSpace(fontFamilyNames))
			{
				var fontFamily = new FontFamily(fontFamilyNames);
				foreach (var span in this.cjkSpans)
					span.SpanColor.FontFamily = fontFamily;
			}
		});
		this.updateFontFamilyAndSizeAction = new(() =>
		{
			var fontFamilyNames = this.Settings.GetValueOrDefault(SettingKeys.ScriptEditorFontFamily).Let(it =>
			{
				if (string.IsNullOrWhiteSpace(it))
					return this.defaultFontFamilyName;
				return it;
			});
			var fontSize = this.Settings.GetValueOrDefault(SettingKeys.ScriptEditorFontSize).Let(it =>
			{
				if (it < SettingKeys.MinScriptEditorFontSize)
					return SettingKeys.MinScriptEditorFontSize;
				if (it > SettingKeys.MaxScriptEditorFontSize)
					return SettingKeys.MaxScriptEditorFontSize;
				return it;
			});
			if (!string.IsNullOrWhiteSpace(fontFamilyNames))
				this.sourceEditor.FontFamily = new(fontFamilyNames);
			this.sourceEditor.FontSize = fontSize;
		});
		this.updateSyntaxHighlightingAction = new(() =>
		{
			if (this.syntaxHighlightingDefs.TryGetValue(this.Language, out var definition))
				this.sourceEditor.SyntaxHighlighting = definition;
		});
		this.GetObservable(LanguageProperty).Subscribe(_ => this.updateSyntaxHighlightingAction.Schedule());
		this.updateSyntaxHighlightingAction.Schedule();
		this.SynchronizationContext.Post(this.ReportCanScrolling);
	}


	/// <summary>
	/// Check whether user can scroll content horizontally or not.
	/// </summary>
	public bool CanScrollHorizontally { get => this.canScrollHorizontally; }


	/// <summary>
	/// Check whether user can scroll content vertically or not.
	/// </summary>
	public bool CanScrollVertically { get => this.canScrollVertically; }


	// Create indentation.
	string CreateIndentation() =>
		this.CreateIndentation(1);
	string CreateIndentation(int indentation) => indentation > 0
		? new string(new char[indentation].Also(it =>
		{
			for (var i = it.Length - 1; i >= 0; --i)
				it[i] = '\t';
		}))
		: "";


	// Get current line.
	DocumentLine GetCurrentLine()
	{
		var position = this.sourceEditor.SelectionStart + this.sourceEditor.SelectionLength;
		var lines = this.sourceEditorArea.Document.Lines;
		for (var i = lines.Count - 1; i > 0; --i)
		{
			if (lines[i].Offset <= position)
				return lines[i];
		}
		return lines[0];
	}


	// Get indentation of given line.
	int GetIndentation(DocumentLine line)
	{
		var indentation = 0;
		var indentationSize = this.sourceEditorArea.Options.IndentationSize;
		var text = this.sourceEditor.Text;
		for (int i = line.Offset, end = line.EndOffset; i < end; ++i)
		{
			var c = text[i];
			if (c == '\t')
				indentation += indentationSize;
			else if (char.IsWhiteSpace(c))
				++indentation;
			else
				break;
		}
		return (indentation / indentationSize);
	}


	/// <summary>
	/// Get or set horizontal scroll bar visibility.
	/// </summary>
	public ScrollBarVisibility HorizontalScrollBarVisibility
	{
		get => this.horzScrollBarVisibility;
		set => this.sourceEditor.HorizontalScrollBarVisibility = value;
	}


	/// <summary>
	/// Get or set whether editor is read-only mode or not.
	/// </summary>
	public bool IsReadOnly
	{
		get => this.isReadOnly;
		set
		{
			this.VerifyAccess();
			this.sourceEditor.IsReadOnly = value;
			this.SetAndRaise<bool>(IsReadOnlyProperty, ref this.isReadOnly, value);
		}
	}


	// Check whether given line contains whitespace only or not.
	bool IsWhitespace(DocumentLine line)
	{
		var text = this.sourceEditor.Text;
		for (int i = line.Offset, end = line.EndOffset; i < end; ++i)
		{
			if (!char.IsWhiteSpace(text[i]))
				return false;
		}
		return true;
	} 


	/// <summary>
	/// Get or set language of script.
	/// </summary>
	public ScriptLanguage Language
	{
		get => this.GetValue<ScriptLanguage>(LanguageProperty);
		set => this.SetValue<ScriptLanguage>(LanguageProperty, value);
	}


	/// <inheritdoc/>
	protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		// call base
		base.OnAttachedToLogicalTree(e);

		// updating font
		this.Settings.SettingChanged += this.OnSettingChanged;
		this.defaultFontFamilyNameObservable = this.GetResourceObservable("String/ScriptEditor.DefaultFontFamilies");
		this.defaultFontFamilyNameObserverToken = this.defaultFontFamilyNameObservable.Subscribe(value =>
		{
			if (value is string name)
			{
				this.defaultFontFamilyName = name;
				this.updateFontFamilyAndSizeAction.Execute();
			}
		});
		this.defaultCjkFontFamilyNameObservable = this.GetResourceObservable("String/ScriptEditor.DefaultFontFamilies.CJK");
		this.defaultCjkFontFamilyNameObserverToken = this.defaultCjkFontFamilyNameObservable.Subscribe(value =>
		{
			if (value is string name)
			{
				this.defaultCjkFontFamilyName = name;
				this.updateCjkSpanFontFamilies.Execute();
			}
		});

		// setup syntax highlighting
		this.cjkSpans.Clear();
		foreach (var (language, definition) in this.syntaxHighlightingDefs)
		{
			switch (language)
			{
				case ScriptLanguage.CSharp:
					{
						var baseColor = (Color)this.FindResource("SystemBaseHighColor").AsNonNull();
						var cfKeywordColor = (Color)this.FindResource("Color/SyntaxHighlighting.Keyword.ControlFlow").AsNonNull();
						var commentColor = (Color)this.FindResource("Color/SyntaxHighlighting.Comment").AsNonNull();
						var directiveColor = (Color)this.FindResource("Color/SyntaxHighlighting.CompilerDirective").AsNonNull();
						var ecColor = (Color)this.FindResource("Color/SyntaxHighlighting.String.EscapeCharacter").AsNonNull();
						var keywordColor = (Color)this.FindResource("Color/SyntaxHighlighting.Keyword").AsNonNull();
						var numberColor = (Color)this.FindResource("Color/SyntaxHighlighting.Number").AsNonNull();
						var stringColor = (Color)this.FindResource("Color/SyntaxHighlighting.String").AsNonNull();
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
								cjkSpans.Add(span);
							}
							else if (pattern.StartsWith("\"")
								|| pattern.StartsWith("@\"")
								|| pattern.StartsWith("\\$\"")
								|| pattern.StartsWith("\'"))
							{
								span.SpanColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
								foreach (var subSpan in span.RuleSet.Spans)
								{
									var subPattern = subSpan.StartExpression.ToString();
									if (subPattern.StartsWith($"\\"))
										subSpan.SpanColor = new() { Foreground = new SimpleHighlightingBrush(ecColor) };
									else if (subPattern.StartsWith("{"))
									{
										subSpan.StartColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
										subSpan.EndColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
										subSpan.SpanColor = new() { Foreground = new SimpleHighlightingBrush(baseColor) };
									}
									else
									{
										subSpan.StartColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
										subSpan.EndColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
										subSpan.SpanColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
									}
								}
								cjkSpans.Add(span);
							}
							else if (pattern.StartsWith("\\#"))
								span.SpanColor = new HighlightingColor() { Foreground = new SimpleHighlightingBrush(directiveColor) };
							else
								span.SpanColor = new HighlightingColor() { Foreground = new SimpleHighlightingBrush(baseColor) };
						}
					}
					break;
				case ScriptLanguage.JavaScript:
					{
						var baseColor = (Color)this.FindResource("SystemBaseHighColor").AsNonNull();
						var commentColor = (Color)this.FindResource("Color/SyntaxHighlighting.Comment").AsNonNull();
						var keywordColor = (Color)this.FindResource("Color/SyntaxHighlighting.Keyword").AsNonNull();
						var numberColor = (Color)this.FindResource("Color/SyntaxHighlighting.Number").AsNonNull();
						var stringColor = (Color)this.FindResource("Color/SyntaxHighlighting.String").AsNonNull();
						var typeColor = (Color)this.FindResource("Color/SyntaxHighlighting.Type").AsNonNull();
						foreach (var rule in definition.MainRuleSet.Rules)
						{
							var pattern = rule.Regex.ToString();
							if (pattern.StartsWith("\\b(?>synchronized")
								|| pattern.StartsWith("\\b(?>Infinity"))
							{
								rule.Color = new() { Foreground = new SimpleHighlightingBrush(keywordColor) };
							}
							else if (pattern.StartsWith("\\b(?>Function"))
								rule.Color = new() { Foreground = new SimpleHighlightingBrush(typeColor) };
							else if (pattern.StartsWith("\\b0[xX][0-9a-fA-F]+"))
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
								cjkSpans.Add(span);
							}
							else if (pattern.StartsWith("\"")
								|| pattern.StartsWith("'"))
							{
								span.SpanColor = new() { Foreground = new SimpleHighlightingBrush(stringColor) };
								cjkSpans.Add(span);
							}
							else
								span.SpanColor = new HighlightingColor() { Foreground = new SimpleHighlightingBrush(baseColor) };
						}
					}
					break;
			}
		}
		this.updateCjkSpanFontFamilies.Execute();
		this.updateSyntaxHighlightingAction.Execute();

		// setup selection color
		if (this.TryFindResource("SystemAccentColor", out var res) && res is Color accentColor)
			this.Resources["Brush/TextArea.Selection.Background"] = new SolidColorBrush(Color.FromArgb(0x70, accentColor.R, accentColor.G, accentColor.B));
	}


	/// <inheritdoc/>
	protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		this.Settings.SettingChanged -= this.OnSettingChanged;
		this.defaultFontFamilyNameObservable = null;
		this.defaultFontFamilyNameObserverToken = this.defaultFontFamilyNameObserverToken.DisposeAndReturnNull();
		this.defaultCjkFontFamilyNameObservable = null;
		this.defaultCjkFontFamilyNameObserverToken = this.defaultCjkFontFamilyNameObserverToken.DisposeAndReturnNull();
		base.OnDetachedFromLogicalTree(e);
	}


	/// <inheritdoc/>
	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);
		this.sourceEditor.Focus();
	}


	// Called when setting changed.
	void OnSettingChanged(object? sender, SettingChangedEventArgs e)
	{
		if (e.Key == SettingKeys.ScriptEditorFontFamily || e.Key == SettingKeys.ScriptEditorFontSize)
			this.updateFontFamilyAndSizeAction.Schedule();
	}


	// Called when text entering.
	void OnTextEntering(object? sender, TextInputEventArgs e)
	{
		switch (e.Text)
		{
			case "\n":
				{
					var line = this.GetCurrentLine();
					var indentation = this.GetIndentation(line);
					(var prevChar, var nextChar) = this.sourceEditor.Text.Let(text =>
					{
						var prevChar = '\0';
						var nextChar = '\0';
						for (int i = this.sourceEditor.SelectionStart - 1, start = line.Offset; i >= start; --i)
						{
							var c = text[i];
							if (!char.IsWhiteSpace(c))
							{
								prevChar = c;
								break;
							}
						}
						for (int i = this.sourceEditor.SelectionStart + this.sourceEditor.SelectionLength, end = line.EndOffset; i < end; ++i)
						{
							var c = text[i];
							if (!char.IsWhiteSpace(c))
							{
								nextChar = c;
								break;
							}
						}
						return (prevChar, nextChar);
					});
					if (prevChar != '{')
						e.Text = $"\n{this.CreateIndentation(indentation)}";
					else if (nextChar != '}')
						e.Text = $"\n{this.CreateIndentation(indentation + 1)}";
					else
					{
						var textForNextLine = $"\n{this.CreateIndentation(indentation)}";
						e.Text = $"\n{this.CreateIndentation(indentation + 1)}{textForNextLine}";
						this.SynchronizationContext.Post(() =>
						{
							this.sourceEditor.SelectionStart -= textForNextLine.Length;
						});
					}
				}
				break;
			case "{":
				if (this.sourceEditor.SelectionLength == 0)
				{
					var position = this.sourceEditor.SelectionStart;
					var text = this.sourceEditor.Text;
					if (position == 0 || (text[position - 1] != '{' && text[position - 1] != '\\'))
					{
						e.Text = "{}";
						this.SynchronizationContext.Post(() =>
						{
							this.sourceEditor.SelectionStart = position + 1;
							this.sourceEditor.SelectionLength = 0;
						});
					}
				}
				break;
			case "}":
				{
					var line = this.GetCurrentLine();
					var prevLine = line.PreviousLine;
					if (IsWhitespace(line) && prevLine != null)
					{
						var position = this.sourceEditor.SelectionStart;
						var indentation = this.GetIndentation(prevLine);
						var newText = $"{this.CreateIndentation(Math.Max(0, indentation - 1))}}}";
						e.Text = "";
						this.SynchronizationContext.Post(() =>
						{
							this.sourceEditor.Select(line.Offset, line.Length);
							this.sourceEditor.SelectedText = newText;
							this.sourceEditor.SelectionLength = 0;
							this.sourceEditor.SelectionStart = line.Offset + newText.Length;
						});
					}
				}
				break;
			case "(":
			case "[":
			case "'":
			case "\"":
				if (this.sourceEditor.SelectionLength == 0)
				{
					var position = this.sourceEditor.SelectionStart;
					if (position == 0 || this.sourceEditor.Text[position - 1] != '\\')
					{
						var startingStr = e.Text;
						var endingStr = startingStr switch
						{
							"(" => ")",
							"[" => "]",
							"'" => "'",
							"\"" => "\"",
							_ => "",
						};
						e.Text = $"{startingStr}{endingStr}";
						this.SynchronizationContext.Post(() =>
						{
							this.sourceEditor.SelectionStart = position + startingStr.Length;
							this.sourceEditor.SelectionLength = 0;
						});
					}
				}
				break;
		}
	}


	// Report can scrolling state.
	void ReportCanScrolling()
	{
		if (this.sourceEditor == null)
			return;
		this.SetAndRaise<bool>(CanScrollHorizontallyProperty, 
			ref this.canScrollHorizontally,
			(this.horzScrollBarVisibility == ScrollBarVisibility.Auto || this.horzScrollBarVisibility == ScrollBarVisibility.Visible)
				&& this.sourceEditor.ExtentWidth > this.sourceEditor.ViewportWidth);
		this.SetAndRaise<bool>(CanScrollVerticallyProperty, 
			ref this.canScrollVertically,
			(this.vertScrollBarVisibility == ScrollBarVisibility.Auto || this.vertScrollBarVisibility == ScrollBarVisibility.Visible)
				&& this.sourceEditor.ExtentHeight > this.sourceEditor.ViewportHeight);
	}


	/// <summary>
	/// Select text at given line.
	/// </summary>
	/// <param name="lineNumber">Line number starting from 1.</param>
	/// <param name="start">Position of start character at line.</param>
	/// <param name="length">Length of selection.</param>
	public void SelectAtLine(int lineNumber, int start, int length)
	{
		this.VerifyAccess();
		if (start < 0 || length < 0)
			throw new ArgumentOutOfRangeException();
		if (lineNumber <= 0)
		{
			this.sourceEditor.Select(0, 0);
			this.sourceEditor.TextArea.Caret.BringCaretToView();
			return;
		}
		var lines = this.sourceEditor.Document.Lines;
		if (lineNumber > lines.Count)
		{
			var lastLine = lines[lines.Count - 1];
			this.sourceEditor.Select(this.sourceEditor.Document.TextLength, 0);
			this.sourceEditor.TextArea.Caret.BringCaretToView();
			return;
		}
		var line = lines[lineNumber - 1];
		if (start > line.Length)
		{
			start = line.Length;
			length = 0;
		}
		else if (start + length > line.Length)
			length = line.Length - start;
		this.sourceEditor.Select(line.Offset + start, length);
		this.sourceEditor.TextArea.Caret.BringCaretToView();
	}


	/// <summary>
	/// Get or set source code of script.
	/// </summary>
	public string? Source
	{
		get => this.source;
		set
		{
			this.VerifyAccess();
			this.sourceEditor.Text = value;
		}
	}


	/// <summary>
	/// Get or set vertical scroll bar visibility.
	/// </summary>
	public ScrollBarVisibility VerticalScrollBarVisibility
	{
		get => this.vertScrollBarVisibility;
		set => this.sourceEditor.VerticalScrollBarVisibility = value;
	}
}
