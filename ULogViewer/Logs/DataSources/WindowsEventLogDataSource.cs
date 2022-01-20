using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	class WindowsEventLogDataSource : BaseLogDataSource
	{
#pragma warning disable CA1416
		// Reader implementation.
		class ReaderImpl : TextReader
		{
			// Fields.
			int entryLineIndex = 0;
			readonly List<string> entryLines = new List<string>();
			readonly EventLogEntryCollection entries;
			int entryIndex;

			// Constructor.
			public ReaderImpl(WindowsEventLogDataSource source, EventLog eventLog)
			{
				this.entries = eventLog.Entries;
				this.entryIndex = this.entries.Count;
				source.Logger.LogDebug($"{this.entries.Count} entries found in '{eventLog.Log}'");
			}

			// Implementations.
			public override string? ReadLine()
			{
				// check state
				if (this.entryIndex < 0)
					return null;

				// move to next entry
				if (this.entryLineIndex >= this.entryLines.Count)
				{
					--this.entryIndex;
					if (this.entryIndex < 0)
						return "</Message>";
					var entry = this.entries[this.entryIndex];
#pragma warning disable CS0618
					var eventId = entry.EventID;
#pragma warning restore CS0618
					var level = entry.EntryType switch
					{
						EventLogEntryType.Error => "e",
						EventLogEntryType.FailureAudit => "f",
						EventLogEntryType.SuccessAudit => "s",
						EventLogEntryType.Warning => "w",
						_ => "i",
					};
					var message = entry.Message;
					var sourceName = entry.Source;
					var timestamp = entry.TimeGenerated;
					this.entryLineIndex = 0;
					this.entryLines.Let(it =>
					{
						it.Clear();
						it.Add($"<Timestamp>{timestamp.ToString("yyyy/MM/dd HH:mm:ss")}</Timestamp>");
						it.Add($"<EventId>{eventId}</EventId>");
						it.Add($"<Level>{level}</Level>");
						it.Add($"<Source>{WebUtility.HtmlEncode(sourceName)}</Source>");
						it.Add("<Message>");
						foreach (var messageLine in message.Split('\n'))
							it.Add($"{WebUtility.HtmlEncode(messageLine.TrimEnd())}");
					});
					return "</Message>";
				}

				// read line of entry
				return this.entryLines[this.entryLineIndex++];
			}
		}
#pragma warning restore CA1416


		// Fields.
		volatile EventLog? eventLog;


		/// <summary>
		/// Initialize new <see cref="WindowsEventLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider">Provider.</param>
		public WindowsEventLogDataSource(WindowsEventLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{
			if (options.Category == null)
				throw new ArgumentException("No category specified.");
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			this.eventLog?.Dispose();
			base.Dispose(disposing);
		}


		// Called when reader closed.
		protected override void OnReaderClosed()
		{
			base.OnReaderClosed();
#pragma warning disable CA1416
			this.eventLog = this.eventLog.DisposeAndReturnNull();
#pragma warning restore CA1416
		}


		// Open reader.
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			var eventLog = this.eventLog;
			if (eventLog == null)
			{
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}
			reader = new ReaderImpl(this, eventLog);
			return LogDataSourceState.ReaderOpened;
		}


#pragma warning disable CA1416
		// Prepare.
		protected override LogDataSourceState PrepareCore()
		{
			// check platform
			if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				this.Logger.LogError("OS is not Windows");
				return LogDataSourceState.UnclassifiedError;
			}

			// check permission
			if (!this.Application.IsRunningAsAdministrator)
			{
				this.Logger.LogError("Current user is not administrator");
				return LogDataSourceState.UnclassifiedError;
			}

			// find event log
			var category = this.CreationOptions.Category;
			this.eventLog = EventLog.GetEventLogs().Let(it =>
			{
				var eventLog = (EventLog?)null;
				foreach (var candidate in it)
				{
					if (candidate.Log == category)
						eventLog = candidate;
					else
						candidate.Dispose();
				}
				return eventLog;
			});

			// complete
			if (this.eventLog != null)
				return LogDataSourceState.ReadyToOpenReader;
			return LogDataSourceState.SourceNotFound;
		}
#pragma warning restore CA1416
	}
}
