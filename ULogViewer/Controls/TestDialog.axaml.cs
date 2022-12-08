using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog for testing.
/// </summary>
partial class TestDialog : AppSuite.Controls.Dialog<IULogViewerApplication>
{
    // Constructor.
    public TestDialog()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
