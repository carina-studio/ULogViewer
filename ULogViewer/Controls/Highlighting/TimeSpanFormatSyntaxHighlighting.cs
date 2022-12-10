using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Controls;
using System;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls.Highlighting;

/// <summary>
/// Syntax highlighting for format of <see cref="TimeSpan"/>.
/// </summary>
static partial class TimeSpanFormatSyntaxHighlighting
{
    // Fields.
    static Regex? ConstantFormatPattern;
    static Regex? DaysPattern;
    static Regex? EscapeCharacterPattern;
    static Regex? HoursPattern;
    static Regex? LongFormatPattern;
    static Regex? MinutesPattern;
    static Regex? SecondsPattern;
    static Regex? ShortFormatPattern;
    static Regex? SubSecondsPattern;


    /// <summary>
    /// Create definition set of syntax highlighting.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Definition set of syntax highlighting.</returns>
    public static SyntaxHighlightingDefinitionSet CreateDefinitionSet(IAvaloniaApplication app)
    {
        // create patterns
        ConstantFormatPattern ??= CreateConstantFormatPattern();
        DaysPattern ??= CreateDaysPattern();
        EscapeCharacterPattern ??= CreateEscapeCharacterPattern();
        HoursPattern ??= CreateHoursPattern();
        LongFormatPattern ??= CreateLongFormatPattern();
        MinutesPattern ??= CreateMinutesPattern();
        SecondsPattern ??= CreateSecondsPattern();
        ShortFormatPattern ??= CreateShortFormatPattern();
        SubSecondsPattern ??= CreateSubSecondsPattern();

        // create definition set
        var definitionSet = new SyntaxHighlightingDefinitionSet(name: "TimeSpan Format");
        definitionSet.TokenDefinitions.Add(new(name: "Constant Format")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.ConstantFormat", Brushes.Green),
            Pattern = ConstantFormatPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Short Format")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.ShortFormat", Brushes.Green),
            Pattern = ShortFormatPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Long Format")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.LongFormat", Brushes.Green),
            Pattern = LongFormatPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Days")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.Days", Brushes.Blue),
            Pattern = DaysPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Hours")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.Hours", Brushes.Blue),
            Pattern = HoursPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Minutes")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.Minutes", Brushes.Blue),
            Pattern = MinutesPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Seconds")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.Seconds", Brushes.Blue),
            Pattern = SecondsPattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Sub-Seconds")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.SubSeconds", Brushes.Blue),
            Pattern = SubSecondsPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Escape Character")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TimeSpanFormatSyntaxHighlighting.EscapeCharacter", Brushes.Magenta),
            Pattern = EscapeCharacterPattern,
        });

        // complete
        return definitionSet;
    }


    // Create patterns.
    [GeneratedRegex(@"(?<!c)c")]
    private static partial Regex CreateConstantFormatPattern();
    [GeneratedRegex(@"(?<!d)d{1,8}")]
    private static partial Regex CreateDaysPattern();
    [GeneratedRegex(@"\\.")]
    private static partial Regex CreateEscapeCharacterPattern();
    [GeneratedRegex(@"(?<!h)h{1,2}")]
    private static partial Regex CreateHoursPattern();
    [GeneratedRegex(@"(?<!G)G")]
    private static partial Regex CreateLongFormatPattern();
    [GeneratedRegex(@"(?<!m)m{1,2}")]
    private static partial Regex CreateMinutesPattern();
    [GeneratedRegex(@"(?<!s)s{1,2}")]
    private static partial Regex CreateSecondsPattern();
    [GeneratedRegex(@"(?<!g)g")]
    private static partial Regex CreateShortFormatPattern();
    [GeneratedRegex(@"(?<!f)f{1,7}|(?<!F)F{1,7}")]
    private static partial Regex CreateSubSecondsPattern();
}