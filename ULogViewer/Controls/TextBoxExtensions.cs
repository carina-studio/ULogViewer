using Avalonia.Controls;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Extensions for <see cref="TextBox"/>.
/// </summary>
static class TextBoxExtensions
{
    /// <summary>
    /// Copy all text if it is not null nor empty.
    /// </summary>
    /// <param name="textBox"><see cref="TextBox"/>.</param>
    public static void CopyTextIfNotEmpty(this TextBox textBox)
    {
        try
        {
            var text = textBox.Text;
            if (!string.IsNullOrEmpty(text))
                _ = App.Current.Clipboard.AsNonNull().SetTextAsync(text);
        }
        catch
        { }
    }
}