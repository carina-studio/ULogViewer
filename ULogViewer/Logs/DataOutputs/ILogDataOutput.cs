using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataOutputs
{
	/// <summary>
	/// Output of raw log data.
	/// </summary>
	interface ILogDataOutput : IApplicationObject<IULogViewerApplication>, IDisposable, INotifyPropertyChanged
	{
		/// <summary>
		/// Open <see cref="TextWriter"/> asynchronously to write raw log data.
		/// </summary>
		/// <returns>Task of opening writer.</returns>
		/// <exception cref="InvalidOperationException">Current state is not <see cref="LogDataOutputState.ReadyToOpenWriter"/>.</exception>
		/// <exception cref="ObjectDisposedException">Instance has been disposed.</exception>
		Task<TextWriter> OpenWriterAsync();


		/// <summary>
		/// Get current state.
		/// </summary>
		LogDataOutputState State { get; }
	}


	/// <summary>
	/// State of <see cref="ILogDataOutput"/>.
	/// </summary>
	enum LogDataOutputState
	{
		/// <summary>
		/// Initializing. This is the initial state.
		/// </summary>
		Initializing,
		/// <summary>
		/// Preparing.
		/// </summary>
		Preparing,
		/// <summary>
		/// Ready to call <see cref="ILogDataOutput.OpenWriterAsync"/>.
		/// </summary>
		ReadyToOpenWriter,
		/// <summary>
		/// Opening writer
		/// </summary>
		OpeningWriter,
		/// <summary>
		/// Writer has been opened successfully.
		/// </summary>
		WriterOpened,
		/// <summary>
		/// Closing opened writer.
		/// </summary>
		ClosingWriter,
		/// <summary>
		/// Target of output cannot be found.
		/// </summary>
		TargetNotFound,
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
