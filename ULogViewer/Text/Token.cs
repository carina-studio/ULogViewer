using System;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Token.
/// </summary>
ref struct Token
{
    /// <summary>
    /// Initialize new <see cref="Token"/> structure.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <param name="value">Value.</param>
    public Token(TokenType type, ReadOnlySpan<char> value)
    {
        this.Type = type;
        this.Value = value;
    }


    /// <summary>
    /// Type of token.
    /// </summary>
    public TokenType Type { get; }


    /// <summary>
    /// Value of token.
    /// </summary>
    public ReadOnlySpan<char> Value { get; }
}


/// <summary>
/// Type of token.
/// </summary>
enum TokenType
{
    Undefined,
    HexNumber,
    DecimalNumber,
    VaryingString,
    CjkPhrese,
    Phrase,
    Symbol,
}