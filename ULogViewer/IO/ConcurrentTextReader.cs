using System.IO;
using System.Threading;

namespace CarinaStudio.ULogViewer.IO;

/// <summary>
/// <see cref="TextReader"/> which try reading text concurrently.
/// </summary>
class ConcurrentTextReader : TextReader
{
    class BufferedLines
    {
        public volatile bool AreLinesReady;
        public volatile int End;
        public readonly string?[] Lines = new string?[BufferedLineCount];
        public volatile int Start;
    }


    // Constants.
    const int BufferedLineCount = 8;
    const int RemainingLinesToStartReading = BufferedLineCount >> 1;


    // Fields.
    volatile BufferedLines bufferedLines;
    readonly BufferedLines bufferedLines1 = new();
    readonly BufferedLines bufferedLines2 = new();
    volatile bool endOfStream;
    readonly bool ownsReader;
    readonly TextReader reader;


    /// <summary>
    /// Initialize new <see cref="ConcurrentTextReader"/> instance.
    /// </summary>
    /// <param name="reader"><see cref="TextReader"/> to read text.</param>
    /// <param name="ownsReader">True to own <paramref name="reader"/> by this instance.</param>
    public ConcurrentTextReader(TextReader reader, bool ownsReader = true)
    {
        this.ownsReader = ownsReader;
        this.reader = reader;
        this.bufferedLines = this.bufferedLines1;
        ThreadPool.QueueUserWorkItem(this.ReadLines, this.bufferedLines);
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.endOfStream = true;
        if (this.ownsReader)
            this.reader.Dispose();
        base.Dispose(disposing);
    }


    /// <inheritdoc/>
    public override int Read()
    {
        if (this.endOfStream)
            return -1;
        var bufferedLines = this.bufferedLines;
        if (!bufferedLines.AreLinesReady)
        {
            lock (bufferedLines)
            {
                if (!bufferedLines.AreLinesReady)
                    Monitor.Wait(bufferedLines);
            }
        }
        if (bufferedLines.Start < bufferedLines.End)
        {
            var line = bufferedLines.Lines[bufferedLines.Start];
            if (!string.IsNullOrEmpty(line))
            {
                var c = line[0];
                bufferedLines.Lines[bufferedLines.Start] = line.Substring(1);
                return c;
            }
            ++bufferedLines.Start;
            var remaining = bufferedLines.End - bufferedLines.Start;
            if (remaining <= 0)
                this.bufferedLines = bufferedLines == this.bufferedLines1 ? this.bufferedLines2 : this.bufferedLines1;
            else if (remaining == RemainingLinesToStartReading)
            {
                var nextBufferedLines = bufferedLines == this.bufferedLines1 ? this.bufferedLines2 : this.bufferedLines1;
                nextBufferedLines.AreLinesReady = false;
                ThreadPool.QueueUserWorkItem(this.ReadLines, nextBufferedLines);
            }
            return '\n';
        }
        this.endOfStream = true;
        return -1;
    }

    
    public override string? ReadLine()
    {
        if (this.endOfStream)
            return null;
        var bufferedLines = this.bufferedLines;
        if (!bufferedLines.AreLinesReady)
        {
            lock (bufferedLines)
            {
                if (!bufferedLines.AreLinesReady)
                    Monitor.Wait(bufferedLines);
            }
        }
        if (bufferedLines.Start < bufferedLines.End)
        {
            var line = bufferedLines.Lines[bufferedLines.Start++];
            var remaining = bufferedLines.End - bufferedLines.Start;
            if (remaining <= 0)
                this.bufferedLines = bufferedLines == this.bufferedLines1 ? this.bufferedLines2 : this.bufferedLines1;
            else if (remaining == RemainingLinesToStartReading)
            {
                var nextBufferedLines = bufferedLines == this.bufferedLines1 ? this.bufferedLines2 : this.bufferedLines1;
                nextBufferedLines.AreLinesReady = false;
                ThreadPool.QueueUserWorkItem(this.ReadLines, nextBufferedLines);
            }
            return line;
        }
        this.endOfStream = true;
        return null;
    }


    // Read lines in background.
    void ReadLines(object? state)
    {
        var bufferedLines = (BufferedLines)state.AsNonNull();
        try
        {
            bufferedLines.Start = 0;
            bufferedLines.End = 0;
            var line = this.reader.ReadLine();
            while (line != null)
            {
                bufferedLines.Lines[bufferedLines.End++] = line;
                if (bufferedLines.End >= BufferedLineCount)
                    break;
                line = this.reader.ReadLine();
            }
        }
        catch
        { }
        finally
        {
            lock (bufferedLines)
            {
                bufferedLines.AreLinesReady = true;
                Monitor.PulseAll(bufferedLines);
            }
        }
    }
}