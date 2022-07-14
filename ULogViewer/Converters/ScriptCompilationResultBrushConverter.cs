using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Scripting;

namespace CarinaStudio.ULogViewer.Converters;

/// <summary>
/// <see cref="IValueConverter"/> to convert from <see cref="CompilationResult"/> to <see cref="IBrush"/>.
/// </summary>
class ScriptCompilationResultBrushConverter : FuncValueConverter<object?, IBrush?>
{
    /// <summary>
    /// Default instance.
    /// </summary>
    public static readonly IValueConverter Default = new ScriptCompilationResultBrushConverter();


    // Constructor.
    ScriptCompilationResultBrushConverter() : base(value =>
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
			CompilationResultType.Error => App.Current.FindResource("Brush/LogLevel.Error"),
			CompilationResultType.Warning => App.Current.FindResource("Brush/LogLevel.Warn"),
			CompilationResultType.Information => App.Current.FindResource("Brush/LogLevel.Info"),
			_ => App.Current.FindResource("Brush/LogLevel.Undefined"),
		} as IBrush;
    })
    { }
}