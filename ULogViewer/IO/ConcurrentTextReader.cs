using CarinaStudio.AppSuite;
using CarinaStudio.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CarinaStudio.ULogViewer.IO;

/// <summary>
/// <see cref="TextReader"/> which try reading text concurrently.
/// </summary>
class ConcurrentTextReader : TextReader
{
    // Static fields.
    static readonly SettingKey<int> BufferedCharactersThresholdKey = new("ConcurrentTextReader.BufferedCharactersThreshold", 32768);


    // Fields.
    volatile int bufferedCharCount;
    readonly int bufferedCharThreshold;
    readonly Queue<string> bufferedLines;
    volatile string? currentLine;
    volatile int currentLineStart;
    volatile bool endOfStream;
    readonly bool ownsReader;
    readonly TextReader reader;
    readonly object syncLock = new();


    /// <summary>
    /// Initialize new <see cref="ConcurrentTextReader"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="reader"><see cref="TextReader"/> to read text.</param>
    /// <param name="ownsReader">True to own <paramref name="reader"/> by this instance.</param>
    public ConcurrentTextReader(IAppSuiteApplication app, TextReader reader, bool ownsReader = true)
    {
        this.bufferedCharThreshold = app.Configuration.GetValueOrDefault(BufferedCharactersThresholdKey);
        this.bufferedLines = new(Math.Min(512, this.bufferedCharThreshold / 1024));
        this.ownsReader = ownsReader;
        this.reader = reader;
        ThreadPool.QueueUserWorkItem(this.ReadLines, null);
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        lock (this.syncLock)
        {
            this.endOfStream = true;
            Monitor.PulseAll(this.syncLock);
        }
        if (this.ownsReader)
            this.reader.Dispose();
        base.Dispose(disposing);
    }


    /// <inheritdoc/>
    public override int Read()
    {
        // wait and get current line
        if (this.currentLine == null)
        {
            if (this.bufferedLines.Count == 0)
                Thread.Yield();
            lock (this.syncLock)
            {
                string? line;
                while (!this.bufferedLines.TryDequeue(out line))
                {
                    if (this.endOfStream)
                        return -1;
                    Monitor.Wait(this.syncLock);
                }
                this.bufferedCharCount -= line.Length + 1;
                Monitor.PulseAll(this.syncLock);
                this.currentLine = line;
            }
        }

        // get character from current line
        lock (this.syncLock)
        {
            if (this.currentLineStart < this.currentLine.Length)
                return this.currentLine[this.currentLineStart++];
            this.currentLine = null;
            this.currentLineStart = 0;
            return '\n';
        }
    }

    
    /// <inheritdoc/>
    public override string? ReadLine()
    {
        // use current line
        string? line;
        if (this.currentLine != null)
        {
            lock (this.syncLock)
            {
                if (this.currentLine != null)
                {
                    line = this.currentLine;
                    if (this.currentLineStart > 0)
                        line = line[this.currentLineStart..^0];
                    this.currentLine = null;
                    this.currentLineStart = 0;
                    return line;
                }
            }
        }

        // dequeue line from buffer
        if (this.bufferedLines.Count == 0)
            Thread.Yield();
        lock (this.syncLock)
        {
            while (!this.bufferedLines.TryDequeue(out line))
            {
                if (this.endOfStream)
                    return null;
                Monitor.Wait(this.syncLock);
            }
            this.bufferedCharCount -= line.Length + 1;
            Monitor.PulseAll(this.syncLock);
        }
        return line;
    }


    // Read lines in background.
    void ReadLines(object? state)
    {
        ref var bufferedCharCount = ref this.bufferedCharCount;
        var bufferedCharThreshold = this.bufferedCharThreshold;
        var bufferedLines = this.bufferedLines;
        ref var endOfStream = ref this.endOfStream;
        var reader = this.reader;
        var syncLock = this.syncLock;
        try
        {
            while (true)
            {
                // read next line
                if (endOfStream)
                    break;
                var line = reader.ReadLine();
                if (line == null)
                    break;
                
                // add to queue and wait
                lock (syncLock)
                {
                    bufferedCharCount += line.Length + 1;
                    bufferedLines.Enqueue(line);
                    Monitor.PulseAll(syncLock);
                }
                if (bufferedCharCount >= bufferedCharThreshold)
                {
                    Thread.Yield();
                    lock (syncLock)
                    {
                        while (bufferedCharCount >= bufferedCharThreshold)
                        {
                            Monitor.Wait(syncLock);
                            if (endOfStream)
                                break;
                        }
                    }
                }
            }
        }
        catch
        { }
        finally
        {
            lock (syncLock)
            {
                endOfStream = true;
                Monitor.PulseAll(syncLock);
            }
        }
    }
}