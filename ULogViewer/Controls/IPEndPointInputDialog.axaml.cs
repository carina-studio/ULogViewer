using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to let user input an <see cref="IPEndPoint"/>.
/// </summary>
class IPEndPointInputDialog : AppSuite.Controls.InputDialog
{
    // Fields.
    readonly ToggleButton commonIPAddressesButton;
    readonly ContextMenu commonIPAddressesMenu;
    readonly IPAddressTextBox ipAddressTextBox;
    readonly IntegerTextBox portTextBox;


    // Constructor.
    public IPEndPointInputDialog()
    {
        this.SetIPAddressCommand = new Command<string>(this.SetIPAddress);
        AvaloniaXamlLoader.Load(this);
        this.commonIPAddressesButton = this.Get<ToggleButton>(nameof(commonIPAddressesButton));
        this.commonIPAddressesMenu = ((ContextMenu)this.Resources[nameof(commonIPAddressesMenu)]!).Also(it =>
        {
            it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.commonIPAddressesButton.IsChecked = false);
            it.Opened += (_, _) => this.SynchronizationContext.Post(() => this.commonIPAddressesButton.IsChecked = true);
        });
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


    // Set to given IP address.
    void SetIPAddress(string s)
    {
        if (IPAddress.TryParse(s, out var ipAddress))
        {
            this.ipAddressTextBox.Object = ipAddress;
            this.ipAddressTextBox.Validate();
            this.ipAddressTextBox.Focus();
            this.ipAddressTextBox.SelectAll();
        }
    }
    
    
    /// <summary>
    /// Command to set given IP address.
    /// </summary>
    public ICommand SetIPAddressCommand { get; }

    
    /// <summary>
    /// Show menu to set common IP address.
    /// </summary>
    public void ShowCommonIPAddressesMenu() =>
        this.commonIPAddressesMenu.Open(this.commonIPAddressesButton);
}
