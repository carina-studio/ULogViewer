using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Scripting;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// <see cref="IValueConverter"/> to convert from <see cref="CompilationResult"/> to <see cref="IImage"/>.
/// </summary>
class ScriptCompilationResultIconConverter : FuncValueConverter<object?, IImage?>
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly IValueConverter Default = new ScriptCompilationResultIconConverter();


    // Constructor.
    ScriptCompilationResultIconConverter() : base(value =>
    {
        var type = CompilationResultType.Information;
        if (value is CompilationResultType t)
            type = t;
        else if (value is CompilationResult result)
            type = result.Type;
        else
            return null;
        return type switch
		{
			CompilationResultType.Error => App.Current.FindResource("Image/Icon.Error.Outline.Colored"),
			CompilationResultType.Warning => App.Current.FindResource("Image/Icon.Warning.Outline.Colored"),
			CompilationResultType.Information => App.Current.FindResource("Image/Icon.Information.Outline.Colored"),
			_ => App.Current.FindResource("Image/Icon.Information.Outline"),
		} as IImage;
    })
    { }
}