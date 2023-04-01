using evtx;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

class WindowsEventLogFileDataSource : BaseLogDataSource
{
    // Reader of raw log.
    class Reader : TextReader
    {
        // Fields.
        readonly IEnumerator<EventRecord> eventLogEnumerator;
        readonly Queue<string> recordLines = new();

        // Constructor.
        public Reader(EventLog eventLog) =>
            this.eventLogEnumerator = eventLog.GetEventRecords().GetEnumerator();

        /// <inheritdoc/>
        public override string? ReadLine()
        {
            // read line from current record
            var recordLines = this.recordLines;
            if (recordLines.TryDequeue(out var line))
                return line;
            
            // move to next record
            if (!this.eventLogEnumerator.MoveNext())
                return null;
            var record = this.eventLogEnumerator.Current;
            if (record == null)
                return null;
            
            // convert level
            var level = record.Level switch
            {
                "Error" => "e",
                "Info" => "i",
                "Warning" => "w",
                _ => "v",
            };
            
            // extract message
            var messageReader = record.Payload?.Let(payload =>
            {
                if (string.IsNullOrEmpty(payload))
                    return new StringReader("");
                try
                {
                    var xmlDocument = new XmlDocument();
                    xmlDocument.LoadXml(payload);
                    var eventDataNode = xmlDocument.FirstChild;
                    while (eventDataNode != null)
                    {
                        if (eventDataNode.NodeType == XmlNodeType.Element && eventDataNode.Name == "EventData")
                        {
                            var dataNode = eventDataNode.FirstChild;
                            while (dataNode != null)
                            {
                                if (dataNode.NodeType == XmlNodeType.Element && dataNode.Name == "Data")
                                {
                                    if (dataNode.Attributes?.Count > 0)
                                        return new StringReader(eventDataNode.InnerXml.Trim());
                                    if (dataNode.FirstChild is XmlText dataText)
                                        return new StringReader(dataText.Value ?? "");
                                    break;
                                }
                                dataNode = dataNode.NextSibling;
                            }
                            break;
                        }
                        eventDataNode = eventDataNode.NextSibling;
                    }
                    return new StringReader("");
                }
                catch
                {
                    return new StringReader("");
                }
            }) ?? new StringReader("");
            
            // generate lines for record
            recordLines.Enqueue($"<Timestamp>{record.Timestamp.DateTime:yyyy/MM/dd HH:mm:ss}</Timestamp>");
            recordLines.Enqueue($"<Computer>{WebUtility.HtmlEncode(record.Computer)}</Computer>");
            recordLines.Enqueue($"<UserName>{WebUtility.HtmlEncode(record.UserName)}</UserName>");
            recordLines.Enqueue($"<Category>{WebUtility.HtmlEncode(record.Channel)}</Category>");
            recordLines.Enqueue($"<ProcessId>{record.ProcessId}</ProcessId>");
            recordLines.Enqueue($"<ThreadId>{record.ThreadId}</ThreadId>");
            recordLines.Enqueue($"<EventId>{record.EventId}</EventId>");
            recordLines.Enqueue($"<Level>{level}</Level>");
            recordLines.Enqueue($"<SourceName>{WebUtility.HtmlEncode(record.Provider)}</SourceName>");
            recordLines.Enqueue("<Message>");
            var messageLine = messageReader.ReadLine();
            while (messageLine != null)
            {
                recordLines.Enqueue(WebUtility.HtmlEncode(messageLine).TrimEnd());
                messageLine = messageReader.ReadLine();
            }
            recordLines.Enqueue("</Message>");
            return recordLines.Dequeue();
        }
    }


    // Fields.
    volatile EventLog? eventLog;
    volatile Stream? eventLogStream;


    // Constructor.
    public WindowsEventLogFileDataSource(WindowsEventLogFileDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
    {
        if (!options.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
            throw new ArgumentException("No file name specified.");
    }


    /// <inheritdoc/>
    protected override void OnReaderClosed()
    {
        if (this.eventLogStream != null)
        {
            Global.RunWithoutError(this.eventLogStream.Close);
            this.eventLogStream = null;
        }
        this.eventLog = null;
        base.OnReaderClosed();
    }


    /// <inheritdoc/>
    protected override Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
    {
        if (this.eventLog != null)
            return Task.FromResult((LogDataSourceState.ReaderOpened, (TextReader?)new Reader(this.eventLog)));
        return Task.FromResult((LogDataSourceState.UnclassifiedError, default(TextReader)));
    }


    /// <inheritdoc/>
    protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken)
    {
        // check file existence
        var fileName = this.CreationOptions.FileName;
        if (!File.Exists(fileName))
        {
            this.Logger.LogError($"File '{fileName}' doesn't exist");
            return Task.FromResult(LogDataSourceState.SourceNotFound);
        }

        // open log reader
        try
        {
            this.Logger.LogTrace($"Open reader of '{fileName}'");
            this.eventLogStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            this.eventLog = new(this.eventLogStream);
            return Task.FromResult(LogDataSourceState.ReadyToOpenReader);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, $"Unable to open reader of '{fileName}'");
            if (this.eventLogStream != null)
            {
                Global.RunWithoutError(this.eventLogStream.Close);
                this.eventLogStream = null;
            }
            return Task.FromResult(LogDataSourceState.UnclassifiedError);
        }
    }
}