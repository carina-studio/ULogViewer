using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog for testing.
/// </summary>
partial class TestDialog : AppSuite.Controls.Dialog<IULogViewerApplication>
{
    // Fields.
    readonly TextShellView textShellView;


    // Constructor.
    public TestDialog()
    {
        AvaloniaXamlLoader.Load(this);
        this.textShellView = this.Get<TextShellView>(nameof(textShellView)).Also(it =>
        {
            //
        });
    }
}
