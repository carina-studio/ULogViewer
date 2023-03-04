using System;
using System.Text;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Tokenizer.
/// </summary>
ref struct Tokenizer
{
    /// <summary>
    /// Pattern of decimal number.
    /// </summary>
    public static readonly string DecimalNumberPattern = "([0-9]+(\\.[0-9]+)?|\\.[0-9]+)";
    /// <summary>
    /// Pattern of phrase consist of CJK characters.
    /// </summary>
    public static readonly string CjkPhrasePattern;
    /// <summary>
    /// Pattern of hexadecimal number.
    /// </summary>
    public static readonly string HexNumberPattern = "((0x|#|u+)[a-f0-9]+|&#x[a-f0-9]+;|[a-f0-9]+h(?=$|\\W)|[0-9]*[a-f][0-9]+[a-f0-9]+)";
    /// <summary>
    /// Pattern of phrase consist of non-CJK characters.
    /// </summary>
    public static readonly string PhrasePattern = @"[\p{L}\p{IsArabic}\p{IsCyrillic}\p{IsDevanagari}\-_]+";


    // Constants.
    const string CjkCharacterPattern = @"(\p{IsCJKRadicalsSupplement}|\p{IsCJKSymbolsandPunctuation}|\p{IsEnclosedCJKLettersandMonths}|\p{IsCJKCompatibility}|\p{IsCJKUnifiedIdeographsExtensionA}|\p{IsCJKUnifiedIdeographs}|\p{IsCJKCompatibilityIdeographs}|\p{IsCJKCompatibilityForms})";


    // Static fields.
    static readonly Regex TokenRegex;


    // Fields.
    Token current;
    readonly string? source;
    readonly ReadOnlySpan<char> sourceSpan;
    Match? tokenMatch;


    // Static initializer.
    static Tokenizer()
    {
        CjkPhrasePattern = $"{CjkCharacterPattern}+";
        var patternBuffer = new StringBuilder();
        patternBuffer.Append($"(?<{nameof(TokenType.HexNumber)}>{HexNumberPattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.DecimalNumber)}>{DecimalNumberPattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>'[^']*')");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\"[^\"]*\")");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\([^\\)]*\\))");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\[[^\\]]*\\])");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\{{[^\\}}]*\\}})");
        patternBuffer.Append($"|(?<{nameof(TokenType.VaryingString)}>\\<[^\\>]*\\>)");
        patternBuffer.Append($"|(?<{nameof(TokenType.CjkPhrese)}>{CjkPhrasePattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.Phrase)}>{PhrasePattern})");
        patternBuffer.Append($"|(?<{nameof(TokenType.Symbol)}>[^\\s])");
        TokenRegex = new(patternBuffer.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }


    /// <summary>
    /// Initialize new <see cref="Tokenizer"/> structure.
    /// </summary>
    /// <param name="source">Source text to be tokenized.</param>
    public Tokenizer(string source)
    {
        this.source = source;
        this.sourceSpan = source.AsSpan();
    }


    /// <summary>
    /// Get current token.
    /// </summary>
    public Token Current => this.current.Type != TokenType.Undefined ? this.current : throw new InvalidOperationException();


    /// <summary>
    /// Get token enumerator.
    /// </summary>
    /// <returns>Enumerator.</returns>
    public Tokenizer GetEnumerator() =>
        this;
    

    /// <summary>
    /// Move to next token.
    /// </summary>
    /// <returns>True if next token has been found.</returns>
    public bool MoveNext()
    {
        // move to next token
        if (this.tokenMatch == null)
        {
            if (this.source == null)
                return false;
            this.tokenMatch = TokenRegex.Match(this.source);
            if (!this.tokenMatch.Success)
                return false;
        }
        else
        {
            if (!this.tokenMatch.Success)
                return false;
            this.tokenMatch = this.tokenMatch.NextMatch();
            if (!this.tokenMatch.Success)
            {
                this.current = default;
                return false;
            }
        }

        // get token
        foreach (Group group in this.tokenMatch.Groups)
        {
            if (group.Success 
                && !int.TryParse(group.Name, out var _) 
                && Enum.TryParse<TokenType>(group.Name, out var tokenType))
            {
                this.current = new(tokenType, group.ValueSpan);
                return true;
            }
        }

        // complete
        return false;
    }
}