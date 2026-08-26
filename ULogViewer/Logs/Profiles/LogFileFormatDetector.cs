using Newtonsoft.Json;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Result of detecting format of log file.
/// </summary>
/// <param name="Format">Detected format of log file.</param>
/// <param name="Encoding">Encoding of text detected from the byte-order mark of log file, or UTF-8 if there is no byte-order mark.</param>
readonly record struct LogFileFormatDetectionResult(LogFileFormat Format, Encoding Encoding);


/// <summary>
/// Detector of format of log file.
/// </summary>
static class LogFileFormatDetector
{
    // Constants.
    const int MaxPooledBufferSize = 256 * 1024;
    const int MinBufferSize = 1024;


    // Static fields.
    [ThreadStatic]
    static byte[]? cachedBuffer;
    static readonly byte[] windowsEventLogSignature = "ElfFile\0"u8.ToArray();


    /// <summary>
    /// Detect format of given log file.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="fileName">Name of log file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of detecting format of log file.</returns>
    public static async Task<LogFileFormatDetectionResult> DetectAsync(IULogViewerApplication app, string fileName, CancellationToken cancellationToken)
    {
        // get state on current thread
        var maxByteCount = Math.Max(MinBufferSize, app.Configuration.GetValueOrDefault(ConfigurationKeys.MaxBytesToDetectLogFileFormat));
        var isGZipFile = Path.GetExtension(fileName).ToLower() == ".gz";

        // detect in background
        return await Task.Run(() =>
        {
            // read head of file
            var buffer = RentBuffer(maxByteCount);
            try
            {
                // open file, decompressing it when it is a gzip file to mirror what FileLogDataSource does
                var byteCount = 0;
                using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var headStream = isGZipFile ? new GZipStream(stream, CompressionMode.Decompress) : (Stream)stream)
                    {
                        while (byteCount < maxByteCount)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var readCount = headStream.Read(buffer, byteCount, maxByteCount - byteCount);
                            if (readCount <= 0)
                                break;
                            byteCount += readCount;
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();

                // detect encoding from byte-order mark
                var (encoding, preambleSize) = DetectEncoding(buffer, byteCount);

                // check signature of Windows event log file, which is binary and never wrapped in gzip by FileLogDataSource
                if (!isGZipFile && IsWindowsEventLogFile(buffer, byteCount))
                    return new LogFileFormatDetectionResult(LogFileFormat.WindowsEventLog, encoding);

                // a gzip file is only readable through the plain text path of FileLogDataSource
                if (isGZipFile)
                    return new LogFileFormatDetectionResult(LogFileFormat.PlainText, encoding);

                // check JSON data
                if (TryDetectJsonData(buffer, preambleSize, byteCount, encoding))
                    return new LogFileFormatDetectionResult(LogFileFormat.Json, encoding);

                // complete
                return new LogFileFormatDetectionResult(LogFileFormat.PlainText, encoding);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new LogFileFormatDetectionResult(LogFileFormat.PlainText, Encoding.UTF8);
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }, cancellationToken);
    }


    // Detect encoding of text from the byte-order mark at the head of file, and report the size of that mark in bytes.
    static (Encoding, int) DetectEncoding(byte[] buffer, int byteCount)
    {
        // check 4-byte marks before 2-byte marks, UTF-32 LE starts with the UTF-16 LE mark
        if (byteCount >= 4)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                return (new UTF32Encoding(false, true), 4);
            if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
                return (new UTF32Encoding(true, true), 4);
        }

        // check 3-byte mark
        if (byteCount >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return (Encoding.UTF8, 3);

        // check 2-byte marks
        if (byteCount >= 2)
        {
            if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                return (Encoding.Unicode, 2);
            if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                return (Encoding.BigEndianUnicode, 2);
        }

        // there is no byte-order mark
        return (Encoding.UTF8, 0);
    }


    // Check whether the head of file is the signature of Windows event log file or not.
    static bool IsWindowsEventLogFile(byte[] buffer, int byteCount)
    {
        // check size
        var signature = windowsEventLogSignature;
        if (byteCount < signature.Length)
            return false;

        // compare signature
        for (var i = signature.Length - 1; i >= 0; --i)
        {
            if (buffer[i] != signature[i])
                return false;
        }
        return true;
    }


    // Rent a buffer from the thread-local cache, or allocate a new one when the cached buffer is absent or too small.
    static byte[] RentBuffer(int size)
    {
        var buffer = cachedBuffer;
        if (buffer is null || buffer.Length < size)
            return new byte[size];
        cachedBuffer = null;
        return buffer;
    }


    // Return a buffer to the thread-local cache, dropping it on the floor if it is larger than MaxPooledBufferSize to avoid unbounded per-thread growth.
    static void ReturnBuffer(byte[] buffer)
    {
        if (buffer.Length > MaxPooledBufferSize)
            return;
        cachedBuffer = buffer;
    }


    // Check whether the head of file is JSON data or not.
    static bool TryDetectJsonData(byte[] buffer, int preambleSize, int byteCount, Encoding encoding)
    {
        try
        {
            // the first token must open an object or an array, a bare value is indistinguishable from plain text
            using var stream = new MemoryStream(buffer, preambleSize, byteCount - preambleSize);
            using var textReader = new StreamReader(stream, encoding);
            using var jsonReader = new JsonTextReader(textReader).Setup(it =>
            {
                it.DateParseHandling = DateParseHandling.None;
                it.SupportMultipleContent = true;
            });
            if (!jsonReader.Read() || (jsonReader.TokenType != JsonToken.StartObject && jsonReader.TokenType != JsonToken.StartArray))
                return false;

            // accept either a completed top-level value, or a value which parsed cleanly until the head was truncated
            var hasToken = false;
            while (jsonReader.Read())
            {
                hasToken = true;
                if (jsonReader.Depth == 0 && jsonReader.TokenType is JsonToken.EndObject or JsonToken.EndArray)
                    return true;
            }
            return hasToken;
        }
        catch (JsonReaderException)
        {
            return false;
        }
    }
}
