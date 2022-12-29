using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Controls;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls.Highlighting;

/// <summary>
/// Syntax highlighting for command of text shell.
/// </summary>
static partial class TextShellCommandSyntaxHighlighting
{
    // Fields.
    static Regex? DqArgEndPattern;
    static Regex? DqArgStartPattern;
    static Regex? FilePathPattern;
    static Regex? IORedirectPattern;
    static Regex? OptionPattern;
    static Regex? PipePattern;
    static Regex? SqArgEndPattern;
    static Regex? SqArgStartPattern;


    /// <summary>
    /// Create syntax highlighting definition set for command of text shell.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Syntax highlighting definition set.</returns>
    public static SyntaxHighlightingDefinitionSet CreateDefinitionSet(IAvaloniaApplication app)
    {
        // create patterns
        DqArgEndPattern ??= CreateDqArgEndPattern();
        DqArgStartPattern ??= CreateDqArgStartPattern();
        FilePathPattern ??= CreateFilePathPattern();
        IORedirectPattern ??= CreateIORedirectPattern();
        OptionPattern ??= CreateOptionPattern();
        PipePattern ??= CreatePipePattern();
        SqArgEndPattern ??= CreateSqArgEndPattern();
        SqArgStartPattern ??= CreateSqArgStartPattern();

        // create definition set
        var definitionSet = new SyntaxHighlightingDefinitionSet("Text-Shell Command");

        // file path
        definitionSet.TokenDefinitions.Add(new(name: "File Path")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TextShellCommandSyntaxHighlighting.FilePath", Brushes.Brown),
            Pattern = FilePathPattern,
        });

        // I/O redirect
        definitionSet.TokenDefinitions.Add(new(name: "I/O Redirection")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TextShellCommandSyntaxHighlighting.IORedirection", Brushes.Magenta),
            Pattern = IORedirectPattern,
        });

        // option
        definitionSet.TokenDefinitions.Add(new(name: "Option")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TextShellCommandSyntaxHighlighting.Option", Brushes.Green),
            Pattern = OptionPattern,
        });

        // pipe
        definitionSet.TokenDefinitions.Add(new(name: "Pipe")
        {
            Foreground = app.FindResourceOrDefault<IBrush>("Brush/TextShellCommandSyntaxHighlighting.Pipe", Brushes.Yellow),
            Pattern = PipePattern,
        });

        // quoted argument
        definitionSet.SpanDefinitions.Add(new SyntaxHighlightingSpan("Double-Quoted Argument").Also(it =>
        {
            it.EndPattern = DqArgEndPattern;
            it.Foreground = app.FindResourceOrDefault<IBrush>("Brush/TextShellCommandSyntaxHighlighting.QuotedArgument", Brushes.Brown);
            it.StartPattern = DqArgStartPattern;
        }));
        definitionSet.SpanDefinitions.Add(new SyntaxHighlightingSpan("Single-Quoted Argument").Also(it =>
        {
            it.EndPattern = SqArgEndPattern;
            it.Foreground = app.FindResourceOrDefault<IBrush>("Brush/TextShellCommandSyntaxHighlighting.QuotedArgument", Brushes.Brown);
            it.StartPattern = SqArgStartPattern;
        }));

        // complete
        return definitionSet;
    }


    // Create patterns.
    [GeneratedRegex("(?<=[^\\\\](\\\\\\\\)*)\"")]
    private static partial Regex CreateDqArgEndPattern();
    [GeneratedRegex("\"")]
    private static partial Regex CreateDqArgStartPattern();
    [GeneratedRegex(@"(?<=^|[\s\|\<\>:;])(\.{1,2}|~|[a-z]:[/\\])?[/\\][^\s\|\<\>:;]*(?=$|[\s\|\<\>:;])", RegexOptions.IgnoreCase)]
    private static partial Regex CreateFilePathPattern();
    [GeneratedRegex(@"(?<=(^|[^\<]))\<(?=($|[^\<]))|(?<=(^|[^\>]))\>{1,2}(?=($|[^\>]))")]
    private static partial Regex CreateIORedirectPattern();
    [GeneratedRegex(@"(?<=\S\s+)\-{1,2}\S+(?=$|\s)")]
    private static partial Regex CreateOptionPattern();
    [GeneratedRegex(@"(?<=(^|[^\|]))\|(?=($|[^\|]))")]
    private static partial Regex CreatePipePattern();
    [GeneratedRegex(@"(?<=[^\\](\\\\)*)'")]
    private static partial Regex CreateSqArgEndPattern();
    [GeneratedRegex(@"'")]
    private static partial Regex CreateSqArgStartPattern();
}