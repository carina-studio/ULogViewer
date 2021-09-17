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
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
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
	partial class LogStringPropertyDialog : BaseDialog
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
					if ((app as App)?.TryFindResource("Brush.LogStringPropertyDialog.FoundText.Background", out res) == true && res is SolidColorBrush solidColorBrush)
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
					return -1;
				var viewWidth = this.viewWidth;
				var offset = (startOffset - documentLine.Offset);
				if (offset >= lineLength)
					return -1;
				var textWidth = this.charWidths[offset++];
				while (offset < lineLength)
				{
					var charWidth = this.charWidths[offset];
					if (textWidth + charWidth > viewWidth - 10)
						return documentLine.Offset + offset;
					textWidth += charWidth;
					++offset;
				}
				return -1;
			}
		}


		// Static fields.
		static readonly AvaloniaProperty<bool> IsPropertyValueTextEditorFocusedProperty = AvaloniaProperty.Register<LogStringPropertyDialog, bool>(nameof(IsPropertyValueTextEditorFocused), false);
		static readonly AvaloniaProperty<string> LogPropertyDisplayNameProperty = AvaloniaProperty.Register<LogStringPropertyDialog, string>(nameof(LogPropertyDisplayName), "");


		// Fields.
		readonly RegexTextBox findTextTextBox;
		readonly HighlightingDefinitionImpl highlightingDefinition;
		readonly TextEditor propertyValueTextEditor;
		readonly VisualLineElementGeneratorImpl visualLineElementGenerator;


		/// <summary>
		/// Initialize new <see cref="LogStringPropertyDialog"/> instance.
		/// </summary>
		public LogStringPropertyDialog()
		{
			this.SetTextWrappingCommand = new Command<bool>(this.SetTextWrapping);
			InitializeComponent();
			this.findTextTextBox = this.FindControl<RegexTextBox>("findTextTextBox").AsNonNull();
			this.highlightingDefinition = new HighlightingDefinitionImpl(this.Application);
			this.propertyValueTextEditor = this.FindControl<TextEditor>("propertyValueTextEditor").AsNonNull().Also(it =>
			{
				it.PropertyChanged += (_, e) =>
				{
					if (e.Property == TextBox.BoundsProperty && this.propertyValueTextEditor != null)
					{
						this.SynchronizationContext.Post(_ => this.propertyValueTextEditor.TextArea.TextView.Redraw(), null); // [Workaround] Force redraw to apply text wrapping.
						this.propertyValueTextEditor.Margin = new Thickness(0); // [Workaround] Relayout completed, restore to correct margin.
					}
				};
				it.SyntaxHighlighting = this.highlightingDefinition;
				it.TextArea.Let(textArea =>
				{
					textArea.GotFocus += (sender, e) => this.SetValue<bool>(IsPropertyValueTextEditorFocusedProperty, true);
					textArea.LostFocus += (sender, e) => this.SetValue<bool>(IsPropertyValueTextEditorFocusedProperty, false);
					textArea.Options.EnableEmailHyperlinks = false;
					textArea.Options.EnableHyperlinks = false;
					textArea.TextView.ElementGenerators.Add(new VisualLineElementGeneratorImpl(it));
				});
			});
			this.visualLineElementGenerator = (VisualLineElementGeneratorImpl)this.propertyValueTextEditor.TextArea.TextView.ElementGenerators.First(it => it is VisualLineElementGeneratorImpl);
			this.SetValue<string>(LogPropertyDisplayNameProperty, LogPropertyNameConverter.Default.Convert(nameof(Log.Message)));
			this.ApplySystemAccentColor();
		}


		// Apply system accent color.
		void ApplySystemAccentColor()
        {
			var app = (App)this.Application;
			if (!app.IsSystemAccentColorSupported)
				return;
			if (!app.TryFindResource("SystemAccentColor", out var res) || res is not Color accentColor)
				return;
			this.Resources["Brush.TextArea.Selection.Background"] = new SolidColorBrush(Color.FromArgb(0x3f, accentColor.R, accentColor.G, accentColor.B));
        }


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Check whether propertyValueTextEditor is focused or not.
		bool IsPropertyValueTextEditorFocused { get => this.GetValue<bool>(IsPropertyValueTextEditorFocusedProperty); }


		/// <summary>
		/// Get or set log to show message.
		/// </summary>
		public DisplayableLog? Log { get; set; }


		/// <summary>
		/// Get or set display name of log property.
		/// </summary>
		public string LogPropertyDisplayName
		{
			get => this.GetValue<string>(LogPropertyDisplayNameProperty);
			set => this.SetValue<string>(LogPropertyDisplayNameProperty, value);
		}


		/// <summary>
		/// Get or set name of log property.
		/// </summary>
		public string LogPropertyName { get; set; } = nameof(Logs.Log.Message);


		// Called when property of find text text box changed.
		void OnFindTextTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.RegexProperty)
			{
				this.highlightingDefinition.Filter = this.findTextTextBox.Regex;
				this.propertyValueTextEditor.TextArea.TextView.Redraw();
			}
		}


		// Generate result.
		protected override object? OnGenerateResult() => null;


		// Called when pointer released on link text block.
		void OnLinkDescriptionPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton != Avalonia.Input.MouseButton.Left)
				return;
			if ((sender as Control)?.Tag is Uri uri)
				this.OpenLink(uri);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			this.propertyValueTextEditor.Text = this.LogPropertyName.Let(propertyName =>
			{
				var value = "";
				this.Log?.TryGetProperty(this.LogPropertyName, out value);
				return value;
			});
			this.propertyValueTextEditor.HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden; // [Workaround] Use 'Hidden' for text wrapping to prevent unexpected text aligmnent.
			this.findTextTextBox.Focus();
			base.OnOpened(e);
		}


		// System accent color changed.
        protected override void OnSystemAccentColorChanged()
        {
            base.OnSystemAccentColorChanged();
			this.ApplySystemAccentColor();
        }


        // set text wrapping.
        void SetTextWrapping(bool wrap)
		{
			if (this.propertyValueTextEditor == null)
				return;
			this.visualLineElementGenerator.WrapText = wrap;
			this.propertyValueTextEditor.HorizontalScrollBarVisibility = wrap ? Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto; // [Workaround] Use 'Hidden' for text wrapping to prevent unexpected text aligmnent.
			this.propertyValueTextEditor.Margin = new Thickness(1); // [Workaround] Force relayout to apply text wrapping.
		}


		// Command to set text wrapping.
		ICommand SetTextWrappingCommand { get; }
	}
}
