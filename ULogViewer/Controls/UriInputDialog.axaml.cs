using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user input an URI.
    /// </summary>
    partial class UriInputDialog : AppSuite.Controls.InputDialog
    {
        // Fields.
        readonly UriTextBox uriTextBox;


        // Constructor.
        public UriInputDialog()
        {
            InitializeComponent();
            this.uriTextBox = this.FindControl<UriTextBox>(nameof(uriTextBox));
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)this.uriTextBox.Object);


        // Initialize.
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


        // Initial URI to show,
        public Uri? InitialUri { get; set; }


        // Key down.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Source == this.uriTextBox && e.Key == Key.Enter)
            {
                this.uriTextBox.Validate();
                this.GenerateResultCommand.TryExecute();
            }
        }


        // Window opened
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            this.uriTextBox.Object = this.InitialUri;
            this.SynchronizationContext.Post(_ =>
            {
                this.uriTextBox.SelectAll();
                this.uriTextBox.Focus();
            }, null);
        }


        // Property of URI text box changed.
        void OnUriTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == UriTextBox.IsTextValidProperty || e.Property == UriTextBox.ObjectProperty)
                this.InvalidateInput();
        }


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() && this.uriTextBox.IsTextValid && this.uriTextBox.Object != null;
    }
}
