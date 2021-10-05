using CarinaStudio.ULogViewer.Logs.DataOutputs;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Interface of object to write <see cref="Log"/>s to <see cref="ILogDataOutput"/>.
	/// </summary>
	interface ILogWriter : IApplicationObject<IULogViewerApplication>, IDisposable, INotifyPropertyChanged
	{
		/// <summary>
		/// Get <see cref="ILogDataOutput"/> to output raw log data.
		/// </summary>
		ILogDataOutput DataOutput { get; }


		/// <summary>
		/// Get or set list of <see cref="Log"/> to be output.
		/// </summary>
		IList<Log> Logs { get; set; }


		/// <summary>
		/// Start writing logs.
		/// </summary>
		void Start();


		/// <summary>
		/// Get current state.
		/// </summary>
		LogWriterState State { get; }
	}


	/// <summary>
	/// State of <see cref="ILogWriter"/>.
	/// </summary>
	enum LogWriterState
	{
		/// <summary>
		/// Preparing.
		/// </summary>
		Preparing,
		/// <summary>
		/// Starting to write logs.
		/// </summary>
		Starting,
		/// <summary>
		/// Writing logs.
		/// </summary>
		WritingLogs,
		/// <summary>
		/// Stopped.
		/// </summary>
		Stopped,
		/// <summary>
		/// Error caused by <see cref="ILogWriter.DataOutput"/>.
		/// </summary>
		DataOutputError,
		/// <summary>
		/// Unclassified error.
		/// </summary>
		UnclassifiedError,
		/// <summary>
		/// Disposed.
		/// </summary>
		Disposed,
	}
}
