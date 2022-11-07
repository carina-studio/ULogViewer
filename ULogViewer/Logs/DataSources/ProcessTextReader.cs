using System.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="TextReader"/> to read text from standard error/output of process.
/// </summary>
class ProcessTextReader : TextReader
{
    // Fields.
    string? bufferedLineFromStderr;
    string? bufferedLineFromStdout;
    volatile bool endOfStream;
    volatile bool isReadingLine;
    readonly ILogger logger;
    readonly Process process;
    readonly object stderrReadingLock = new();
    readonly TextReader? stdoutReader;
    readonly object stdoutReadingLock = new();
    readonly object syncLock = new();


    /// <summary>
    /// Initialize new <see cref="ProcessTextReader"/> instance.
    /// </summary>
    /// <param name="source">Data source.</param>
    /// <param name="process">Process.</param>
    /// <param name="includeStderr">True to include stderr.</param>
    public ProcessTextReader(ILogDataSource source, Process process, bool includeStderr)
    {
        this.logger = source.Application.LoggerFactory.CreateLogger($"{source}-ProcessTextReader");
        this.process = process;
        if (includeStderr)
        {
            new Thread(() => this.ReadLinesFromProcess("stderr", process.BeginErrorReadLine, typeof(Process).GetEvent("ErrorDataReceived").AsNonNull(), ref this.bufferedLineFromStderr, this.stderrReadingLock))
            {
                IsBackground = true,
                Name = $"{source}-stderr",
            }.Start();
            new Thread(() => this.ReadLinesFromProcess("stdout", process.BeginOutputReadLine, typeof(Process).GetEvent("OutputDataReceived").AsNonNull(), ref this.bufferedLineFromStdout, this.stdoutReadingLock))
            {
                IsBackground = true,
                Name = $"{source}-stderr",
            }.Start();
        }
        else
            this.stdoutReader = process.StandardOutput;
        this.Source = source;
    }

    
    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        lock (this.syncLock)
        {
            this.endOfStream = true;
            this.isReadingLine = false;
            if (this.stdoutReader == null)
            {
                this.logger.LogTrace("Notify to complete reading lines from stderr/stdout");
                Global.RunWithoutError(this.process.CancelErrorRead);
                Global.RunWithoutError(this.process.CancelOutputRead);
                lock (this.stderrReadingLock)
                    Monitor.PulseAll(this.stderrReadingLock);
                lock (this.stdoutReadingLock)
                    Monitor.PulseAll(this.stdoutReadingLock);
            }
        }
        if (disposing)
            this.stdoutReader?.Close();
        Global.RunWithoutError(() => this.process.Kill());
        Global.RunWithoutError(() => this.process.WaitForExit(1000));
        base.Dispose(disposing);
    }


    /// <inheritdoc/>
    public override int Read()
    {
        if (this.stdoutReader != null)
            return this.stdoutReader.Read();
        if (this.endOfStream)
            return -1;
        lock (this.syncLock)
        {
            // start reading line
            if (this.endOfStream)
                return -1;
            if (this.isReadingLine)
                throw new InvalidOperationException();
            if (this.bufferedLineFromStderr != null)
            {
                var line = this.bufferedLineFromStderr;
                if (line.Length > 0)
                {
                    this.bufferedLineFromStderr = line[1..^0];
                    return line[0];
                }
                this.bufferedLineFromStderr = null;
                return '\n';
            }
            if (this.bufferedLineFromStdout != null)
            {
                var line = this.bufferedLineFromStdout;
                if (line.Length > 0)
                {
                    this.bufferedLineFromStdout = line[1..^0];
                    return line[0];
                }
                this.bufferedLineFromStdout = null;
                return '\n';
            }
            this.isReadingLine = true;
            Monitor.PulseAll(this.syncLock);

            // wait for reading line
            try
            {
                Monitor.Wait(this.syncLock);
            }
            finally
            {
                this.isReadingLine = false;
            }

            // get read line
            if (this.bufferedLineFromStderr != null)
            {
                var line = this.bufferedLineFromStderr;
                if (line.Length > 0)
                {
                    this.bufferedLineFromStderr = line[1..^0];
                    return line[0];
                }
                this.bufferedLineFromStderr = null;
                return '\n';
            }
            if (this.bufferedLineFromStdout != null)
            {
                var line = this.bufferedLineFromStdout;
                if (line.Length > 0)
                {
                    this.bufferedLineFromStdout = line[1..^0];
                    return line[0];
                }
                this.bufferedLineFromStdout = null;
                return '\n';
            }
            Monitor.PulseAll(this.syncLock);
            this.endOfStream = true;
            return -1;
        }
    }

    
    /// <inheritdoc/>
    public override string? ReadLine()
    {
        if (this.stdoutReader != null)
            return this.stdoutReader.ReadLine();
        if (this.endOfStream)
            return null;
        lock (this.syncLock)
        {
            // start reading line
            if (this.endOfStream)
                return null;
            if (this.isReadingLine)
                throw new InvalidOperationException();
            this.isReadingLine = true;
            Monitor.PulseAll(this.syncLock);

            // wait for reading line
            try
            {
                Monitor.Wait(this.syncLock);
            }
            finally
            {
                this.isReadingLine = false;
            }

            // get read line
            if (this.bufferedLineFromStderr != null)
            {
                var line = this.bufferedLineFromStderr;
                this.bufferedLineFromStderr = null;
                return line;
            }
            if (this.bufferedLineFromStdout != null)
            {
                var line = this.bufferedLineFromStdout;
                this.bufferedLineFromStdout = null;
                return line;
            }
            Monitor.PulseAll(this.syncLock);
            this.endOfStream = true;
            return null;
        }
    }


    // Read lines from given reader.
    void ReadLinesFromProcess(string name, Action beginReadLineAction, EventInfo lineReadEvent, ref string? bufferedLine, object readingLock)
    {
        // setup event handler to receive read line
        this.logger.LogTrace("Start reading lines from {name}", name);
        var lineQueue = new Queue<string?>();
        var lineReadHandler = new DataReceivedEventHandler((_, e) =>
        {
            lock (readingLock)
            {
                lineQueue.Enqueue(e.Data);
                Monitor.Pulse(readingLock);
            }
        });
        lineReadEvent.AddEventHandler(this.process, lineReadHandler);

        // read lines
        try
        {
            beginReadLineAction();
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to start reading lines from {name}", name);
            lock (this.syncLock)
                Monitor.PulseAll(this.syncLock);
            return;
        }
        var isReadingFirstLine = true;
        while (true)
        {
            // wait for start reading line
            while (true)
            {
                lock (this.syncLock)
                {
                    if (this.endOfStream)
                        break;
                    else if (!this.isReadingLine || bufferedLine != null)
                        Monitor.Wait(this.syncLock);
                    else
                        break;
                }
            }
            if (this.endOfStream)
                break;

            // read line
            var line = (string?)null;
            try
            {
                if (isReadingFirstLine)
                    this.logger.LogTrace("Wait for first line from {name}", name);
                lock (readingLock)
                {
                    if (!lineQueue.TryDequeue(out line))
                    {
                        Monitor.Wait(readingLock);
                        lineQueue.TryDequeue(out line);
                    }
                    if (isReadingFirstLine)
                    {
                        isReadingFirstLine = false;
                        this.logger.LogTrace("First line read from {name}", name);
                    }
                }
            }
            catch
            { }

            // notify
            Monitor.Enter(this.syncLock);
            bufferedLine = line;
            if (line == null)
            {
                Monitor.Exit(this.syncLock);
                Thread.Sleep(500); // [Workaround] Report text read from other stream first
                Monitor.Enter(this.syncLock);
            }
            Monitor.PulseAll(this.syncLock);
            Monitor.Exit(this.syncLock);
            if (line == null)
                break;
        }
        this.logger.LogTrace("Complete reading lines from {name}", name);
    }


    /// <inheritdoc/>
    public override string ReadToEnd()
    {
        if (this.stdoutReader != null)
            return this.stdoutReader.ReadToEnd();
        var buffer = new StringBuilder();
        var line = this.ReadLine();
        var isFirstLine = true;
        while (line != null)
        {
            if (!isFirstLine)
                buffer.AppendLine();
            buffer.Append(line);
            line = this.ReadLine();
        }
        return buffer.ToString();
    }


    /// <summary>
    /// Get data source.
    /// </summary>
    public ILogDataSource Source { get; }
}