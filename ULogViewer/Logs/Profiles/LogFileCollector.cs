using CarinaStudio.Logging;
using CarinaStudio.ULogViewer.IO;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Collector which collects log files in a directory.
/// </summary>
static class LogFileCollector
{
    // Constants.
    const int BinaryProbeSize = 512;
    const int MaxDirectoryCount = 1024;
    const string GZipFileExtension = ".gz";


    // Static fields.
    static ILogger? logger;


    /// <summary>
    /// Collect log files in given directory and its sub-directories.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="directoryName">Name of root directory to collect log files.</param>
    /// <param name="maxFileCount">Maximum number of log files allowed to be collected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of collecting log files, the result is ordered by directory level first and by file name second.</returns>
    /// <remarks>The directories are walked breadth-first, so the log files of the root directory are collected before the log files of its sub-directories. <see cref="TooManyFilesException"/> is thrown when there are more log files than <paramref name="maxFileCount"/>, the caller is responsible for reporting it to user.</remarks>
    public static async Task<IList<string>> CollectAsync(IULogViewerApplication app, string directoryName, int maxFileCount, CancellationToken cancellationToken)
    {
        // prepare state on current thread
        logger ??= app.LoggerFactory.CreateLogger(nameof(LogFileCollector));
        var maxCount = Math.Max(1, maxFileCount);

        // collect in background
        return await Task.Run(IList<string> () =>
        {
            // walk the directories breadth-first
            var fileNames = new List<string>();
            var buffer = new byte[BinaryProbeSize];
            var directoryNames = new Queue<string>();
            var directoryCount = 0;
            directoryNames.Enqueue(directoryName);
            while (directoryNames.TryDequeue(out var currentDirectoryName))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // bound the walk of a tree which consists of directories instead of log files
                ++directoryCount;
                if (directoryCount > MaxDirectoryCount)
                {
                    logger?.LogWarning("Collect: too many directories in '{directoryName}'", directoryName);
                    throw new TooManyFilesException(directoryName, maxCount);
                }

                // list the entries of the directory, an inaccessible sub-directory is skipped instead of failing the whole collection
                var isRootDirectory = directoryCount == 1;
                FileInfo[] fileInfos;
                DirectoryInfo[] subDirectoryInfos;
                try
                {
                    var directoryInfo = new DirectoryInfo(currentDirectoryName);
                    fileInfos = directoryInfo.GetFiles();
                    subDirectoryInfos = directoryInfo.GetDirectories();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    if (isRootDirectory)
                        throw;
                    logger?.LogWarning(ex, "Collect: unable to list the entries in '{directoryName}'", currentDirectoryName);
                    continue;
                }

                // collect the log files of the directory in a stable order
                Array.Sort(fileInfos, (lhs, rhs) => string.CompareOrdinal(lhs.Name, rhs.Name));
                foreach (var fileInfo in fileInfos)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsLogFile(fileInfo, buffer))
                        continue;
                    fileNames.Add(fileInfo.FullName);
                    if (fileNames.Count > maxCount)
                    {
                        logger?.LogWarning("Collect: too many log files in '{directoryName}'", directoryName);
                        throw new TooManyFilesException(directoryName, maxCount);
                    }
                }

                // walk into the sub-directories after the log files of the directory have been collected
                Array.Sort(subDirectoryInfos, (lhs, rhs) => string.CompareOrdinal(lhs.Name, rhs.Name));
                foreach (var subDirectoryInfo in subDirectoryInfos)
                {
                    if (IsCollectableDirectory(subDirectoryInfo))
                        directoryNames.Enqueue(subDirectoryInfo.FullName);
                }
            }

            // complete
            logger?.LogDebug("Collect [ok], files: {fileCount}, directories: {directoryCount}", fileNames.Count, directoryCount);
            return fileNames;
        }, cancellationToken);
    }


    // Check whether the sub-directory is worth walking into or not.
    static bool IsCollectableDirectory(DirectoryInfo directoryInfo)
    {
        try
        {
            // a hidden directory holds the data of tool instead of the logs of user
            if (directoryInfo.Name.StartsWith('.'))
                return false;
            var attributes = directoryInfo.Attributes;
            if ((attributes & FileAttributes.Hidden) != 0)
                return false;

            // walking into a linked directory may never end
            return (attributes & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogTrace(ex, "Collect: unable to check the attributes of '{directoryName}'", directoryInfo.FullName);
            return false;
        }
    }


    // Check whether the file is a log file which is worth reading or not.
    static bool IsLogFile(FileInfo fileInfo, byte[] buffer)
    {
        try
        {
            // a marked logs info file belongs to ULogViewer itself
            if (Session.IsMarkedLogsInfoFile(fileInfo.FullName))
                return false;

            // a hidden file is not a log file which user wants to read, an empty file carries no log
            if (fileInfo.Name.StartsWith('.') || (fileInfo.Attributes & FileAttributes.Hidden) != 0)
                return false;
            if (fileInfo.Length <= 0)
                return false;

            // the head of a gzip file is binary before it is decompressed, FileLogDataSource reads it as plain text
            if (string.Equals(fileInfo.Extension, GZipFileExtension, StringComparison.OrdinalIgnoreCase))
                return true;

            // binary data is not readable as text by any data source
            var byteCount = 0;
            using (var stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                while (byteCount < buffer.Length)
                {
                    var readCount = stream.Read(buffer, byteCount, buffer.Length - byteCount);
                    if (readCount <= 0)
                        break;
                    byteCount += readCount;
                }
            }
            return !LogFileFormatDetector.IsBinaryHead(buffer, byteCount);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogTrace(ex, "Collect: unable to check the file '{fileName}'", fileInfo.FullName);
            return false;
        }
    }
}
