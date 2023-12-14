using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.Windows.Input;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to let user input an <see cref="IPEndPoint"/>.
/// </summary>
class IPEndPointInputDialog : AppSuite.Controls.InputDialog
{
    // Fields.
    readonly IPAddressTextBox ipAddressTextBox;
    readonly IntegerTextBox portTextBox;


    // Constructor.
    public IPEndPointInputDialog()
    {
        AvaloniaXamlLoader.Load(this);
        this.ipAddressTextBox = this.Get<IPAddressTextBox>(nameof(ipAddressTextBox));
        this.portTextBox = this.Get<IntegerTextBox>(nameof(portTextBox));
    }


    // Generate result.
    protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
        Task.FromResult((object?)new IPEndPoint(this.ipAddressTextBox.Object.AsNonNull(), (int)this.portTextBox.Value.GetValueOrDefault()));


    // Initial IP endpoint to show,
    public IPEndPoint? InitialIPEndPoint { get; set; }
    
    
    // Whether initial focus should be on port TextBox or not.
    public bool FocusOnPort { get; set; }


    // Property of IPAddress text box changed.
    void OnIPAddressTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == IPAddressTextBox.IsTextValidProperty || e.Property == IPAddressTextBox.ObjectProperty)
            this.InvalidateInput();
    }


    /// <inheritdoc/>
    protected override void OnEnterKeyClickedOnInputControl(Control control)
    {
        base.OnEnterKeyClickedOnInputControl(control);
        if (control == this.ipAddressTextBox)
        {
            if (!this.portTextBox.Validate())
                this.portTextBox.Focus();
            else
                this.GenerateResultCommand.TryExecute();
        }
        else if (control == this.portTextBox)
        {
            if (!this.ipAddressTextBox.Validate())
                this.ipAddressTextBox.Focus();
            else
                this.GenerateResultCommand.TryExecute();
        }
    }


    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.SynchronizationContext.Post(_ =>
        {
            if (this.FocusOnPort)
            {
                this.portTextBox.Focus();
                this.portTextBox.SelectAll();
            }
            else
            {
                this.ipAddressTextBox.Focus();
                this.ipAddressTextBox.SelectAll();
            }
        }, null);
    }


    /// <inheritdoc/>
    protected override void OnOpening(EventArgs e)
    {
        base.OnOpening(e);
        this.InitialIPEndPoint?.Let(it =>
        {
            this.ipAddressTextBox.Object = it.Address;
            this.portTextBox.Value = it.Port;
        });
    }


    // Validate input.
    protected override bool OnValidateInput() =>
        base.OnValidateInput() && this.ipAddressTextBox.IsTextValid && this.ipAddressTextBox.Object != null;
}
