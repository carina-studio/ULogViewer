using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Controls;
using System;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls.Highlighting;

/// <summary>
/// Syntax highlighting for format of <see cref="DateTime"/>.
/// </summary>
static partial class DateTimeFormatSyntaxHighlighting
{
    // Fields.
    static Regex? AmPmDesignatorPattern;
    static Regex? DayOfMonthPattern;
    static Regex? DayOfWeekPattern;
    static Regex? EraPattern;
    static Regex? EscapeCharacterPattern;
    static Regex? HourPattern;
    static Regex? MinutePattern;
    static Regex? MonthNamePattern;
    static Regex? MonthPattern;
    static Regex? SecondPattern;
    static Regex? SeparatorPattern;
    static Regex? SubSecondPattern;
    static Regex? TimeZoneOffsetPattern;
    static Regex? TimeZonePattern;
    static Regex? YearPattern;


    /// <summary>
    /// Create definition set of syntax highlighting.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Definition set of syntax highlighting.</returns>
    public static SyntaxHighlightingDefinitionSet CreateDefinitionSet(IAvaloniaApplication app)
    {
        // create patterns
        AmPmDesignatorPattern ??= CreateAmPmDesignatorPattern();
        DayOfMonthPattern ??= CreateDayOfMonthPattern();
        DayOfWeekPattern ??= CreateDayOfWeekPattern();
        EraPattern ??= CreateEraPattern();
        EscapeCharacterPattern ??= CreateEscapeCharacterPattern();
        HourPattern ??= CreateHourPattern();
        MinutePattern ??= CreateMinutePattern();
        MonthNamePattern ??= CreateMonthNamePattern();
        MonthPattern ??= CreateMonthPattern();
        SecondPattern ??= CreateSecondPattern();
        SeparatorPattern ??= CreateSeparatorPattern();
        SubSecondPattern ??= CreateSubSecondPattern();
        TimeZoneOffsetPattern ??= CreateTimeZoneOffsetPattern();
        TimeZonePattern ??= CreateTimeZonePattern();
        YearPattern ??= CreateYearPattern();

        // create definition set
        var definitionSet = new SyntaxHighlightingDefinitionSet("DateTime Format");
        definitionSet.TokenDefinitions.Add(new("Year")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Year", Brushes.Red),
            Pattern = YearPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Month")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Month", Brushes.Orange),
            Pattern = MonthPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Name of Month")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.MonthName", Brushes.Orange),
            Pattern = MonthNamePattern,
        });
        definitionSet.TokenDefinitions.Add(new("Day of Month")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.DayOfMonth", Brushes.Yellow),
            Pattern = DayOfMonthPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Day of Week")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.DayOfWeek", Brushes.Yellow),
            Pattern = DayOfWeekPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Hour")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Hour", Brushes.Green),
            Pattern = HourPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Minute")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Minute", Brushes.Blue),
            Pattern = MinutePattern,
        });
        definitionSet.TokenDefinitions.Add(new(name: "Second")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Second", Brushes.Indigo),
            Pattern = SecondPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Sub-Second")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.SubSecond", Brushes.Purple),
            Pattern = SubSecondPattern,
        });
        definitionSet.TokenDefinitions.Add(new("AM/PM Designator")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.AmPmDesignator", Brushes.LightGreen),
            Pattern = AmPmDesignatorPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Era")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Era", Brushes.DarkRed),
            Pattern = EraPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Escape Character")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.EscapeCharacter", Brushes.LightYellow),
            Pattern = EscapeCharacterPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Separator")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.Separator", Brushes.Magenta),
            Pattern = SeparatorPattern,
        });
        definitionSet.TokenDefinitions.Add(new("Time Zone")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.TimeZone", Brushes.Navy),
            Pattern = TimeZonePattern,
        });
        definitionSet.TokenDefinitions.Add(new("Time Zone Offset")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/DateTimeFormatSyntaxHighlighting.TimeZoneOffset", Brushes.Navy),
            Pattern = TimeZoneOffsetPattern,
        });

        // complete
        return definitionSet;
    }


    // Create patterns
    [GeneratedRegex(@"(?<!t)t{1,2}")]
    private static partial Regex CreateAmPmDesignatorPattern();
    [GeneratedRegex(@"(?<!d)d{1,2}")]
    private static partial Regex CreateDayOfMonthPattern();
    [GeneratedRegex(@"(?<!d)d{3,4}")]
    private static partial Regex CreateDayOfWeekPattern();
    [GeneratedRegex(@"(?<!g)g{1,2}")]
    private static partial Regex CreateEraPattern();
    [GeneratedRegex(@"\\.")]
    private static partial Regex CreateEscapeCharacterPattern();
    [GeneratedRegex(@"(?<!h)h{1,2}|(?<!H)H{1,2}")]
    private static partial Regex CreateHourPattern();
    [GeneratedRegex(@"(?<!m)m{1,2}")]
    private static partial Regex CreateMinutePattern();
    [GeneratedRegex(@"(?<!M)M{3,4}")]
    private static partial Regex CreateMonthNamePattern();
    [GeneratedRegex(@"(?<!M)M{1,2}")]
    private static partial Regex CreateMonthPattern();
    [GeneratedRegex(@"(?<!s)s{1,2}")]
    private static partial Regex CreateSecondPattern();
    [GeneratedRegex(@"[:/]")]
    private static partial Regex CreateSeparatorPattern();
    [GeneratedRegex(@"(?<!f)f{1,7}|(?<!F)F{1,7}")]
    private static partial Regex CreateSubSecondPattern();
    [GeneratedRegex(@"(?<!z)z{1,3}")]
    private static partial Regex CreateTimeZoneOffsetPattern();
    [GeneratedRegex(@"(?<!K)K")]
    private static partial Regex CreateTimeZonePattern();
    [GeneratedRegex(@"(?<!y)y{1,5}")]
    private static partial Regex CreateYearPattern();
}