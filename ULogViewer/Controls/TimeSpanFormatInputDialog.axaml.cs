using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.ULogViewer.Controls.Highlighting;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user input time span format.
    /// </summary>
    partial class TimeSpanFormatInputDialog : InputDialog
    {
        // Fields.
        readonly TextBox formatTextBox;


        // Constructor.
        public TimeSpanFormatInputDialog()
        {
            this.TimeSpanFormatSyntaxHighlightingDefinitionSet = TimeSpanFormatSyntaxHighlighting.CreateDefinitionSet(this.Application);
            AvaloniaXamlLoader.Load(this);
            this.formatTextBox = this.Get<TextBox>(nameof(formatTextBox));
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)formatTextBox.Text);


        // Initial datetime format to show.
        public string? InitialFormat { get; set; }


        // Property of format textbox changed.
        void OnFormatTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TextBox.TextProperty)
                this.InvalidateInput();
        }


        // Key up.
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (!e.Handled && e.Source == this.formatTextBox && e.Key == Key.Enter)
                this.GenerateResultCommand.TryExecute();
        }


        // Dialog opened.
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            this.formatTextBox.Text = this.InitialFormat;
            this.SynchronizationContext.Post(_ =>
            {
                this.formatTextBox.Focus();
                this.formatTextBox.SelectAll();
            }, null);
        }


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.formatTextBox.Text);
        

        /// <summary>
		/// Definition set of time span format syntax highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet TimeSpanFormatSyntaxHighlightingDefinitionSet { get; }
    }
}
