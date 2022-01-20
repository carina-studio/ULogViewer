using System;
using System.IO;

namespace CarinaStudio.ULogViewer.Logs.DataOutputs
{
	/// <summary>
	/// <see cref="ILogDataOutput"/> to output log data to string.
	/// </summary>
	class StringLogDataOutput : BaseLogDataOutput
	{
		/// <summary>
		/// Initialize new <see cref="StringLogDataOutput"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public StringLogDataOutput(IULogViewerApplication app) : base(app)
		{ }


		/// <summary>
		/// Get string that contains raw log data.
		/// </summary>
		public string? String { get; private set; }


		// Called when writer closed
		protected override void OnWriterClosed(TextWriter writer)
		{
			if (!this.IsDisposed && this.State == LogDataOutputState.WriterOpened)
				this.String = writer.ToString();
			base.OnWriterClosed(writer);
		}


		// Open writer.
		protected override LogDataOutputState OpenWriterCore(out TextWriter? writer)
		{
			writer = new StringWriter();
			return LogDataOutputState.WriterOpened;
		}


		// Prepare.
		protected override LogDataOutputState PrepareCore() => LogDataOutputState.ReadyToOpenWriter;
	}
}
