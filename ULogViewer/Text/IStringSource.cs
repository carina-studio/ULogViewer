using System;
using System.Diagnostics.CodeAnalysis;

namespace CarinaStudio.ULogViewer.Text;

/// <summary>
/// Source of string.
/// </summary>
public interface IStringSource
{
    /// <summary>
    /// Get number of bytes in memory occupied by the source.
    /// </summary>
    long ByteCount { get; }


    /// <summary>
    /// Default instance for empty string source.
    /// </summary>
    public static readonly IStringSource Empty = new EmptyStringSource();
    
    
    /// <summary>
    /// Get number of characters in string.
    /// </summary>
    int Length { get; }
    
    
    /// <summary>
    /// Try copying string from the source.
    /// </summary>
    /// <param name="buffer">Buffer to receive copied string.</param>
    /// <returns>True if string has been copied to buffer successfully.</returns>
    bool TryCopyTo(Span<char> buffer);
}


/// <summary>
/// Extensions for <see cref="IStringSource"/>.
/// </summary>
public static class StringSourceExtensions
{
    /// <summary>
    /// Check whether given source is Null or contains empty string.
    /// </summary>
    /// <param name="source"><see cref="IStringSource"/>.</param>
    /// <returns>True if the source is null or contains empty string.</returns>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this IStringSource? source) =>
        source is null || source.Length <= 0;
}