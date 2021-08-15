using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Text;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to show message of log.
	/// </summary>
	partial class LogMessageDialog : BaseDialog
	{
		// Definition of syntax highlighting.
		class HighlightingDefinitionImpl : IHighlightingDefinition
		{
			// Fields.
			Regex? filter;
			readonly HighlightingColor highlightingColor;
			readonly HighlightingRuleSet mainRuleSet = new HighlightingRuleSet();
			readonly Dictionary<string, string> properties = new Dictionary<string, string>();

			// Constructor.
			public HighlightingDefinitionImpl(IApplication app)
			{
				this.highlightingColor = new HighlightingColor().Also(it => 
				{
					var res = (object?)null;
					if ((app as App)?.TryFindResource("Brush.LogMessageDialog.FoundText.Background", out res) == true && res is SolidColorBrush solidColorBrush)
						it.Background = new SimpleHighlightingBrush(solidColorBrush.Color);
				});
				this.mainRuleSet.Let(mainRuleSet =>
				{
					mainRuleSet.Name = "MainRules";
				});
			}

			// Filter.
			public Regex? Filter
			{
				get => this.filter;
				set
				{
					this.filter = value;
					this.mainRuleSet.Rules.Clear();
					if (value != null)
					{
						mainRuleSet.Rules.Add(new HighlightingRule()
						{
							Color = this.highlightingColor,
							Regex = value,
						});
					}
				}
			}

			// Implementations.
			public HighlightingColor? GetNamedColor(string name) => name == "MatchedText"
				? this.highlightingColor
				: null;
			public HighlightingRuleSet? GetNamedRuleSet(string name) => null;
			public HighlightingRuleSet MainRuleSet => this.mainRuleSet;
			public string Name => "LogMessage";
			public IEnumerable<HighlightingColor> NamedHighlightingColors => new HighlightingColor[] { this.highlightingColor };
			public IDictionary<string, string> Properties => this.properties.AsReadOnly();
		}


		// Visual line element generator for text wrapping.
		class VisualLineElementGeneratorImpl : VisualLineElementGenerator
		{
			// Element for line break.
			class LineBreakElement : VisualLineElement
			{
				// Constructor.
				public LineBreakElement() : base(1, 0)
				{ }

				// Create text run
				public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context) => new TextEndOfLine(1);
			}

			// Fields.
			double[] charWidths = new double[0];
			readonly FormattedText formattedText = new FormattedText();
			readonly TextEditor textEditor;
			double viewWidth;
			public bool WrapText = true;

			// Constructor.
			public VisualLineElementGeneratorImpl(TextEditor textEditor) => this.textEditor = textEditor;

			// Implementations.
			public override VisualLineElement ConstructElement(int offset) => new LineBreakElement();
			public override int GetFirstInterestedOffset(int startOffset)
			{
				// check state
				if (!this.WrapText)
					return -1;

				// calculate size of each character.
				var document = this.CurrentContext.Document;
				var documentLine = document.GetLineByOffset(startOffset).Let(it =>
				{
					if (startOffset == it.Offset)
					{
						var textRunProperties = this.CurrentContext.GlobalTextRunProperties;
						var charCount = it.Length;
						this.charWidths = new double[charCount].Also(charWidths =>
						{
							this.formattedText.FontSize = textRunProperties.FontSize;
							this.formattedText.Typeface = textRunProperties.Typeface;
							for (var i = charCount - 1; i >= 0; --i)
							{
								this.formattedText.Text = document.GetText(it.Offset + i, 1);
								charWidths[i] = this.formattedText.Bounds.Width;
							}
						});
						this.viewWidth = this.textEditor.ViewportWidth;
					}
					return it;
				});

				// find position to break line
				var lineLength = documentLine.Length;
				if (lineLength <= 0)
				{
					this.charWidths = new double[0];
					return -1;
				}
				var viewWidth = this.viewWidth;
				var offset = (startOffset - documentLine.Offset);
				if (offset >= lineLength)
				{
					this.charWidths = new double[0];
					return -1;
				}
				var textWidth = this.charWidths[offset++];
				while (offset < lineLength)
				{
					var charWidth = this.charWidths[offset];
					if (textWidth + charWidth > viewWidth - 10)
						return documentLine.Offset + offset;
					textWidth += charWidth;
					++offset;
				}
				this.charWidths = new double[0];
				return -1;
			}
		}


		// Static fields.
		static readonly AvaloniaProperty<string> LogMessageDisplayNameProperty = AvaloniaProperty.Register<LogMessageDialog, string>(nameof(LogMessageDisplayName), "");


		// Fields.
		readonly RegexTextBox findTextTextBox;
		readonly HighlightingDefinitionImpl highlightingDefinition;
		readonly TextEditor messageTextBox;
		readonly VisualLineElementGeneratorImpl visualLineElementGenerator;


		/// <summary>
		/// Initialize new <see cref="LogMessageDialog"/> instance.
		/// </summary>
		public LogMessageDialog()
		{
			this.SetTextWrappingCommand = ReactiveCommand.Create<bool>(this.SetTextWrapping);
			InitializeComponent();
			this.findTextTextBox = this.FindControl<RegexTextBox>("findTextTextBox").AsNonNull();
			this.highlightingDefinition = new HighlightingDefinitionImpl(this.Application);
			this.messageTextBox = this.FindControl<TextEditor>("messageTextBox").AsNonNull().Also(it =>
			{
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == TextBox.BoundsProperty && this.messageTextBox != null)
					{
						this.SynchronizationContext.Post(_ => this.messageTextBox.TextArea.TextView.Redraw(), null); // [Workaround] Force redraw to apply text wrapping.
						this.messageTextBox.Margin = new Thickness(0); // [Workaround] Relayout completed, restore to correct margin.
					}
				};
				it.SyntaxHighlighting = this.highlightingDefinition;
				it.TextArea.Let(textArea =>
				{
					textArea.TextView.ElementGenerators.Add(new VisualLineElementGeneratorImpl(it));
				});
			});
			this.visualLineElementGenerator = (VisualLineElementGeneratorImpl)this.messageTextBox.TextArea.TextView.ElementGenerators.First(it => it is VisualLineElementGeneratorImpl);
			this.SetValue<string>(LogMessageDisplayNameProperty, LogPropertyNameConverter.Default.Convert(nameof(Log.Message)));
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set log to show message.
		/// </summary>
		public Log? Log { get; set; }


		/// <summary>
		/// Get or set display name of <see cref="Log.Message"/>.
		/// </summary>
		public string LogMessageDisplayName
		{
			get => this.GetValue<string>(LogMessageDisplayNameProperty);
			set => this.SetValue<string>(LogMessageDisplayNameProperty, value);
		}


		// Called when property of find text text box changed.
		void OnFindTextTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.RegexProperty)
			{
				this.highlightingDefinition.Filter = this.findTextTextBox.Regex;
				this.messageTextBox.TextArea.TextView.Redraw();
			}
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;


		// Called when pointer released on link text block.
		void OnLinkDescriptionPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton != MouseButton.Left)
				return;
			if ((sender as Control)?.Tag is string uri)
				this.OpenLink(uri);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			this.messageTextBox.Text = this.Log?.Message;
			this.messageTextBox.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden; // [Workaround] Use 'Hidden' for text wrapping to prevent unexpected text aligmnent.
			this.findTextTextBox.Focus();
			base.OnOpened(e);
		}


		// set text wrapping.
		void SetTextWrapping(bool wrap)
		{
			if (this.messageTextBox == null)
				return;
			this.visualLineElementGenerator.WrapText = wrap;
			this.messageTextBox.HorizontalScrollBarVisibility = wrap ? Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto; // [Workaround] Use 'Hidden' for text wrapping to prevent unexpected text aligmnent.
			this.messageTextBox.Margin = new Thickness(1); // [Workaround] Force relayout to apply text wrapping.
		}


		// Command to set text wrapping.
		ICommand SetTextWrappingCommand { get; }
	}
}
