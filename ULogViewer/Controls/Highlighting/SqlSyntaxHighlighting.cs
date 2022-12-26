using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Controls;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls.Highlighting;

/// <summary>
/// Syntax highlighting for SQL.
/// </summary>
static partial class SqlSyntaxHighlighting
{
    // Static fields.
    static Regex? BracketPattern;
    static Regex? DqStringEndPattern;
    static Regex? DqStringStartPattern;
    static Regex? EscapeCharacterPattern;
    static Regex? KeywordPattern;
    static Regex? OperatorPattern;
    static Regex? SpecialCharacterInStringPattern;
    static Regex? SpecialCharacterPattern;
    static Regex? SqStringEndPattern;
    static Regex? SqStringStartPattern;


    /// <summary>
    /// Create syntax highlighting definition set for SQL.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Syntax highlighting definition set.</returns>
    public static SyntaxHighlightingDefinitionSet CreateDefinitionSet(IAvaloniaApplication app)
    {
        // create patterns
        BracketPattern ??= CreateBracketPattern();
        DqStringEndPattern ??= CreateDqStringEndPattern();
        DqStringStartPattern ??= CreateDqStringStartPattern();
        EscapeCharacterPattern ??= CreateEscapeCharacterPattern();
        KeywordPattern ??= CreateKeywordPattern();
        OperatorPattern ??= CreateOperatorPattern();
        SpecialCharacterInStringPattern ??= CreateSpecialCharacterInStringPattern();
        SpecialCharacterPattern ??= CreateSpecialCharacterPattern();
        SqStringEndPattern ??= CreateSqStringEndPattern();
        SqStringStartPattern ??= CreateSqStringStartPattern();

        // create definition set
        var definitionSet = new SyntaxHighlightingDefinitionSet("SQL");

        // bracket
        definitionSet.TokenDefinitions.Add(new("Bracket")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.Bracket", Brushes.Yellow),
            Pattern = BracketPattern,
        });

        // keyword
        definitionSet.TokenDefinitions.Add(new("Keyword")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.Keyword", Brushes.Blue),
            Pattern = KeywordPattern,
        });

        // operator
        definitionSet.TokenDefinitions.Add(new("Operator")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.Operator", Brushes.Purple),
            Pattern = OperatorPattern,
        });

        // special character
        definitionSet.TokenDefinitions.Add(new(name: "Special Character")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.SpecialCharacter", Brushes.Magenta),
            Pattern = SpecialCharacterPattern,
        });

        // string
        var escapeCharToken = new SyntaxHighlightingToken("Escape Character")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.EscapeCharacter", Brushes.Yellow),
            Pattern = EscapeCharacterPattern,
        };
        var specialCharToken = new SyntaxHighlightingToken("Special Character")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.SpecialCharacter", Brushes.Magenta),
            Pattern = SpecialCharacterInStringPattern,
        };
        definitionSet.SpanDefinitions.Add(new SyntaxHighlightingSpan("Double-Quoted String").Also(it =>
        {
            it.EndPattern = DqStringEndPattern;
            it.Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.String", Brushes.Brown);
            it.StartPattern = DqStringStartPattern;

            // escape character
            it.TokenDefinitions.Add(escapeCharToken);

            // special character
            it.TokenDefinitions.Add(specialCharToken);
        }));
        definitionSet.SpanDefinitions.Add(new SyntaxHighlightingSpan("Single-Quoted String").Also(it =>
        {
            it.EndPattern = SqStringEndPattern;
            it.Foreground = app.FindResourceOrDefault<IBrush>("Brush/SqlSyntaxHighlighting.String", Brushes.Brown);
            it.StartPattern = SqStringStartPattern;

            // escape character
            it.TokenDefinitions.Add(escapeCharToken);

            // special character
            it.TokenDefinitions.Add(specialCharToken);
        }));

        // complete
        return definitionSet;
    }


    // Create patterns.
    [GeneratedRegex(@"\(|\)")]
    private static partial Regex CreateBracketPattern();
    [GeneratedRegex("(?<=[^\\\\](\\\\\\\\)*)\"")]
    private static partial Regex CreateDqStringEndPattern();
    [GeneratedRegex("\"")]
    private static partial Regex CreateDqStringStartPattern();
    [GeneratedRegex(@"\\.")]
    private static partial Regex CreateEscapeCharacterPattern();
    [GeneratedRegex(@"(?<=(^|\s))(add|alter|as|asc|backup|by|check|column|constraint|create|database|default|delete|desc|distinct|drop|exec|foreign|from|full|group|having|index|inner|insert|into|join|key|left|limit|null|on|order|outer|primary|procedure|replace|right|select|set|table|top|truncate|union|unique|update|values|view|where)(?=\b)", RegexOptions.IgnoreCase)]
    private static partial Regex CreateKeywordPattern();
    [GeneratedRegex(@"\+|\-|\*|/|%|&|\||\^|=|>|<|<>|>=|<=|((?<=(^|\s))(all|and|any|between|exists|in|is|like|not|or|some)(?=[\s$]))", RegexOptions.IgnoreCase)]
    private static partial Regex CreateOperatorPattern();
    [GeneratedRegex(@"%")]
    private static partial Regex CreateSpecialCharacterInStringPattern();
    [GeneratedRegex(@"\,|\?")]
    private static partial Regex CreateSpecialCharacterPattern();
    [GeneratedRegex(@"(?<=[^\\](\\\\)*)'")]
    private static partial Regex CreateSqStringEndPattern();
    [GeneratedRegex(@"'")]
    private static partial Regex CreateSqStringStartPattern();
}