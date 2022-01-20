using System;
using System.IO;
using System.Text;

namespace CarinaStudio.ULogViewer.Logs.DataOutputs
{
	/// <summary>
	/// <see cref="ILogDataOutput"/> based-on file-system.
	/// </summary>
	class FileLogDataOutput : BaseLogDataOutput
	{
		/// <summary>
		/// Initialize new <see cref="FileLogDataOutput"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="fileName">Name of file.</param>
		public FileLogDataOutput(IULogViewerApplication app, string fileName) : this(app, fileName, Encoding.UTF8)
		{ }


		/// <summary>
		/// Initialize new <see cref="FileLogDataOutput"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="fileName">Name of file.</param>
		/// <param name="encoding">Encoding.</param>
		public FileLogDataOutput(IULogViewerApplication app, string fileName, Encoding encoding) : base(app)
		{
			this.Encoding = encoding;
			this.FileName = fileName;
		}


		/// <summary>
		/// Get encoding to output logs.
		/// </summary>
		public Encoding Encoding { get; }


		/// <summary>
		/// Get name of file to output logs.
		/// </summary>
		public string FileName { get; }


		// Open writer.
		protected override LogDataOutputState OpenWriterCore(out TextWriter? writer)
		{
			writer = new StreamWriter(this.FileName, false, this.Encoding);
			return LogDataOutputState.WriterOpened;
		}


		// Prepare.
		protected override LogDataOutputState PrepareCore()
		{
			if (File.Exists(this.FileName) || !Directory.Exists(this.FileName))
				return LogDataOutputState.ReadyToOpenWriter;
			return LogDataOutputState.TargetNotFound;
		}
	}
}
