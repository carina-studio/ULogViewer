using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

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
			readonly EventLog eventLog;

			// Constructor.
			public ReaderImpl(EventLog eventLog)
			{
				this.entries = eventLog.Entries;
				this.eventLog = eventLog;
			}

			// Implementations.
			public override string? ReadLine()
			{
				if (this.entryIndex >= this.entries.Count)
					return null;
				var entry = this.entries[this.entryIndex++];
				return $"{entry.TimeGenerated}, {entry.Category}, {entry.Source}, {entry.InstanceId}, {entry.Message}";
			}
		}
#pragma warning restore CA1416


		// Static fields.
		static readonly PropertyInfo[] eventLogPropertyInfos = typeof(EventLog).Let(type =>
		{
			return new PropertyInfo[]
			{
				//
			};
		});


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
			using var identity = WindowsIdentity.GetCurrent();
			var principal = new WindowsPrincipal(identity);
			if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
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
