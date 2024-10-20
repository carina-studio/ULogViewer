using evtx;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
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
        
        // Read message from payload.
        static TextReader ReadMessage(string? payload)
        {
            if (string.IsNullOrEmpty(payload))
                return new StringReader("");
            try
            {
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(payload);
                var containerNode = xmlDocument.FirstChild;
                while (containerNode != null)
                {
                    if (containerNode is XmlElement containerElement)
                    {
                        return containerNode.Name switch
                        {
                            "EventData" => ReadMessageFromEventData(containerElement),
                            "UserData" => ReadMessageFromUserData(containerElement),
                            _ => new StringReader(containerNode.OuterXml.Trim()),
                        };
                    }
                    containerNode = containerNode.NextSibling;
                }
                return new StringReader("");
            }
            catch
            {
                return new StringReader("");
            }
        }
        
        // Read message from EventData node.
        static TextReader ReadMessageFromEventData(XmlElement eventDataNode)
        {
            var dataNode = eventDataNode.FirstChild;
            while (dataNode is not null)
            {
                if (dataNode.NodeType == XmlNodeType.Element && dataNode.Name == "Data")
                {
                    if (dataNode.Attributes?.Count == 0 && dataNode.FirstChild is XmlText dataText)
                        return new StringReader(WebUtility.HtmlDecode(dataText.Value) ?? "");
                    var dataLines = new StringBuilder();
                    do
                    {
                        if (dataLines.Length > 0)
                            dataLines.AppendLine();
                        try
                        {
                            if (dataNode.Name == "Data")
                            {
                                var nameAttr = dataNode.Attributes?["Name"];
                                if (nameAttr is not null)
                                {
                                    dataLines.Append(nameAttr.Value);
                                    dataLines.Append(": ");
                                    dataLines.Append(WebUtility.HtmlDecode(dataNode.InnerXml.Trim()));
                                }
                                else if (dataNode.FirstChild is not null)
                                {
                                    dataLines.Append("Data: ");
                                    dataLines.Append(WebUtility.HtmlDecode(dataNode.InnerXml.Trim()));
                                }
                            }
                            else if (dataNode.FirstChild is not null)
                            {
                                dataLines.Append(dataNode.Name);
                                dataLines.Append(": ");
                                dataLines.Append(WebUtility.HtmlDecode(dataNode.InnerXml.Trim()));
                            }
                        }
                        finally
                        {
                            dataNode = dataNode.NextSibling;
                        }
                    } while (dataNode is not null);
                    return new StringReader(dataLines.ToString());
                }
                dataNode = dataNode.NextSibling;
            }
            return new StringReader("");
        }
        
        // Read message from UserData node.
        static TextReader ReadMessageFromUserData(XmlElement userDataNode)
        {
            var messageBuffer = new StringBuilder();
            var childNode = userDataNode.FirstChild;
            while (childNode is not null)
            {
                if (childNode.NodeType == XmlNodeType.Element)
                {
                    var elementName = childNode.Name;
                    if (messageBuffer.Length > 0)
                        messageBuffer.AppendLine();
                    if (childNode.FirstChild is null)
                        messageBuffer.Append(elementName);
                    else
                    {
                        messageBuffer.Append('[');
                        messageBuffer.Append(childNode.Name);
                        messageBuffer.Append(']');
                        var propertyNode = childNode.FirstChild;
                        while (propertyNode is not null)
                        {
                            if (propertyNode.NodeType == XmlNodeType.Element)
                            {
                                messageBuffer.AppendLine();
                                messageBuffer.Append(propertyNode.Name);
                                messageBuffer.Append(": ");
                                var propertyValueNode = propertyNode.FirstChild;
                                if (propertyValueNode?.NodeType == XmlNodeType.Element
                                    && propertyValueNode.ChildNodes.Count <= 1)
                                {
                                    messageBuffer.Append(WebUtility.HtmlDecode(propertyValueNode.InnerXml.Trim()));
                                }
                                else
                                    messageBuffer.Append(WebUtility.HtmlDecode(propertyNode.InnerXml.Trim()));
                            }
                            propertyNode = propertyNode.NextSibling;
                        }
                    }
                }
                childNode = childNode.NextSibling;
            }
            if (messageBuffer.Length > 0)
                return new StringReader(WebUtility.HtmlDecode(messageBuffer.ToString()));
            return new StringReader(WebUtility.HtmlDecode(userDataNode.InnerXml.Trim()));
        }

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
            
            // convert level
            var level = record.Level switch
            {
                "Error" => "e",
                "Info" => "i",
                "Warning" => "w",
                _ => "v",
            };
            
            // generate lines for record
            recordLines.Enqueue($"<Timestamp>{record.Timestamp.DateTime.ToLocalTime():yyyy/MM/dd HH:mm:ss}</Timestamp>");
            recordLines.Enqueue($"<Computer>{record.Computer}</Computer>");
            recordLines.Enqueue($"<UserName>{record.UserName}</UserName>");
            recordLines.Enqueue($"<Category>{record.Channel}</Category>");
            recordLines.Enqueue($"<ProcessId>{record.ProcessId}</ProcessId>");
            recordLines.Enqueue($"<ThreadId>{record.ThreadId}</ThreadId>");
            recordLines.Enqueue($"<EventId>{record.EventId}</EventId>");
            recordLines.Enqueue($"<Level>{level}</Level>");
            recordLines.Enqueue($"<SourceName>{record.Provider}</SourceName>");
            recordLines.Enqueue("<Message>");
            var messageReader = ReadMessage(record.Payload);
            var messageLine = messageReader.ReadLine();
            while (messageLine != null)
            {
                recordLines.Enqueue(messageLine.TrimEnd());
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
            this.Logger.LogError("File '{fileName}' doesn't exist", fileName);
            return Task.FromResult(LogDataSourceState.SourceNotFound);
        }

        // open log reader
        try
        {
            this.Logger.LogTrace("Open reader of '{fileName}'", fileName);
            this.eventLogStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            this.eventLog = new(this.eventLogStream);
            return Task.FromResult(LogDataSourceState.ReadyToOpenReader);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Unable to open reader of '{fileName}'", fileName);
            if (this.eventLogStream != null)
            {
                Global.RunWithoutError(this.eventLogStream.Close);
                this.eventLogStream = null;
            }
            return Task.FromResult(LogDataSourceState.UnclassifiedError);
        }
    }
}