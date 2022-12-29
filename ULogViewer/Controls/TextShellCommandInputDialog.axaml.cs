using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to let user input command of text-shell.
/// </summary>
partial class TextShellCommandInputDialog : InputDialog
{
    // Fields.
    readonly TextBox commandTextBox;


    // Constructor.
    public TextShellCommandInputDialog()
    {
        this.SyntaxHighlightingDefinitionSet = Highlighting.TextShellCommandSyntaxHighlighting.CreateDefinitionSet(this.Application);
        AvaloniaXamlLoader.Load(this);
        this.commandTextBox = this.Get<TextBox>(nameof(commandTextBox)).Also(it =>
        {
            it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
        });
    }


    // Generate result.
    protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
        Task.FromResult((object?)this.commandTextBox.Text);


    // Initial command.
    public string? InitialCommand { get; set; }


    /// <inheritdoc/>
    protected override void OnEnterKeyClickedOnInputControl(IControl control)
    {
        base.OnEnterKeyClickedOnInputControl(control);
        if (control == this.commandTextBox)
            this.GenerateResultCommand.TryExecute();
    }


    /// <inheritdoc/>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.commandTextBox.Text = this.InitialCommand;
        this.SynchronizationContext.Post(this.commandTextBox.Focus);
    }


    // Validate input.
    protected override bool OnValidateInput() =>
        base.OnValidateInput() 
        && !string.IsNullOrWhiteSpace(this.commandTextBox.Text);
    

    /// <summary>
	/// Syntax highlighting definition set for text-shell command.
	/// </summary>
	public SyntaxHighlightingDefinitionSet SyntaxHighlightingDefinitionSet { get; }
}
