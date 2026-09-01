using System.IO;

namespace CarinaStudio.ULogViewer.IO;

/// <summary>
/// Exception thrown when there are too many files in a directory to be handled.
/// </summary>
/// <param name="rootDirectoryName">Name of root directory.</param>
/// <param name="maxFileCount">Maximum number of files allowed.</param>
sealed class TooManyFilesException(string rootDirectoryName, int maxFileCount) : IOException($"Too many files in directory '{rootDirectoryName}', maximum: {maxFileCount}.")
{
    /// <summary>
    /// Get maximum number of files allowed.
    /// </summary>
    public int MaxFileCount { get; } = maxFileCount;


    /// <summary>
    /// Get name of root directory.
    /// </summary>
    public string RootDirectoryName { get; } = rootDirectoryName;
}
