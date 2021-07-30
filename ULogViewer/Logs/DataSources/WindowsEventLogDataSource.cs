using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	class WindowsEventLogDataSource : BaseLogDataSource
	{
#pragma warning disable CA1416
		// Reader implementation.
		class ReaderImpl : TextReader
		{
			// Fields.
			readonly EventLogEntryCollection entries;
			int entryIndex;

			// Constructor.
			public ReaderImpl(EventLog eventLog)
			{
				this.entries = eventLog.Entries;
			}

			// Implementations.
			public override string? ReadLine()
			{
				if (this.entryIndex >= this.entries.Count)
					return null;
				var entry = this.entries[this.entryIndex++];
#pragma warning disable CS0618
				var eventId = entry.EventID;
#pragma warning restore CS0618
				var level = entry.EntryType switch
				{
					EventLogEntryType.Error => "e",
					EventLogEntryType.FailureAudit => "e",
					EventLogEntryType.Information => "i",
					EventLogEntryType.SuccessAudit => "i",
					EventLogEntryType.Warning => "w",
					_ => "u",
				};
				var message = entry.Message;
				var sourceName = entry.Source;
				var timestamp = entry.TimeGenerated;
				return $"{timestamp.ToString("yyyy/MM/dd HH:mm:ss")} {eventId} {level} {sourceName}:{message}";
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
			this.eventLog = this.eventLog.DisposeAndReturnNull();
		}


		// Open reader.
		protected override LogDataSourceState OpenReaderCore(out TextReader? reader)
		{
			var eventLog = this.eventLog;
			if (eventLog == null)
			{
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}
			reader = new ReaderImpl(eventLog);
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
