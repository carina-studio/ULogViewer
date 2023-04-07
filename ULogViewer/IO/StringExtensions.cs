using System.Collections.Generic;
using System.IO;

namespace CarinaStudio.ULogViewer.IO;

/// <summary>
/// Extensions for file name and path.
/// </summary>
static class StringExtensions
{
    // Fields.
    private static ISet<char>? InvalidFileNameChars;
    private static ISet<char>? InvalidPathChars;


    /// <summary>
    /// Check whether the given string can represent a valid file name or not.
    /// </summary>
    /// <param name="s">String.</param>
    /// <returns>True if the given string can represent a valid file name.</returns>
    public static bool IsValidFileName(this string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        InvalidFileNameChars ??= new HashSet<char>(Path.GetInvalidFileNameChars());
        for (var i = s.Length - 1; i >= 0; --i)
        {
            if (InvalidFileNameChars.Contains(s[i]))
                return false;
        }
        return true;
    }
    
    
    /// <summary>
    /// Check whether the given string can represent a valid file path or not.
    /// </summary>
    /// <param name="s">String.</param>
    /// <returns>True if the given string can represent a valid file path.</returns>
    public static bool IsValidFilePath(this string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return false;
        InvalidPathChars ??= new HashSet<char>(Path.GetInvalidPathChars());
        for (var i = s.Length - 1; i >= 0; --i)
        {
            if (InvalidPathChars.Contains(s[i]))
                return false;
        }
        return true;
    }
}