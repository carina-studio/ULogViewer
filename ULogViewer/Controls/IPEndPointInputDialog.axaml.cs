using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user input an <see cref="IPEndPoint"/>.
    /// </summary>
    partial class IPEndPointInputDialog : InputDialog
    {
        // Fields.
        readonly IPAddressTextBox ipAddressTextBox;
        readonly NumericUpDown portUpDown;


        // Constructor.
        public IPEndPointInputDialog()
        {
            InitializeComponent();
            this.ipAddressTextBox = this.FindControl<IPAddressTextBox>(nameof(ipAddressTextBox)).AsNonNull();
            this.portUpDown = this.FindControl<NumericUpDown>(nameof(portUpDown));
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)new IPEndPoint(this.ipAddressTextBox.IPAddress.AsNonNull(), (int)this.portUpDown.Value));


        // Initialize.
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


        // Initial IP endpoint to show,
        public IPEndPoint? InitialIPEndPoint { get; set; }


        // Property of IPAddress text box changed.
        void OnIPAddressTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IPAddressTextBox.IsTextValidProperty || e.Property == IPAddressTextBox.IPAddressProperty)
                this.InvalidateInput();
        }


        // Window opened
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            this.InitialIPEndPoint?.Let(it =>
            {
                this.ipAddressTextBox.IPAddress = it.Address;
                this.portUpDown.Value = it.Port;
            });
            this.SynchronizationContext.Post(_ =>
            {
                this.ipAddressTextBox.SelectAll();
                this.ipAddressTextBox.Focus();
            }, null);
        }


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() && this.ipAddressTextBox.IsTextValid && this.ipAddressTextBox.IPAddress != null;
    }
}
