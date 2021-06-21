using CarinaStudio.Threading;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Interface of source providing log data.
	/// </summary>
	interface ILogDataSource : IApplicationObject, IDisposable, INotifyPropertyChanged, IThreadDependent
	{
		/// <summary>
		/// Get <see cref="LogDataSourceOptions"/> of creating this source.
		/// </summary>
		LogDataSourceOptions CreationOptions { get; }


		/// <summary>
		/// Get name for displaying purpose.
		/// </summary>
		string? DisplayName { get; }


		/// <summary>
		/// Open <see cref="TextReader"/> to read log data asynchronously.
		/// </summary>
		/// <returns>Task of opening <see cref="TextReader"/>.</returns>
		/// <exception cref="InvalidOperationException">Current state is not <see cref="LogDataSourceState.ReadyToOpenReader"/>.</exception>
		/// <exception cref="ObjectDisposedException">Instance has been disposed.</exception>
		Task<TextReader> OpenReaderAsync();


		/// <summary>
		/// Get <see cref="ILogDataSourceProvider"/> which creates this instance.
		/// </summary>
		ILogDataSourceProvider Provider { get; }


		/// <summary>
		/// Get current state.
		/// </summary>
		LogDataSourceState State { get; }


		/// <summary>
		/// Get underlying source of log.
		/// </summary>
		UnderlyingLogDataSource UnderlyingSource { get; }
	}


	/// <summary>
	/// State of <see cref="ILogDataSource"/>.
	/// </summary>
	enum LogDataSourceState
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
		/// Ready to call <see cref="ILogDataSource.OpenReaderAsync"/>.
		/// </summary>
		ReadyToOpenReader,
		/// <summary>
		/// Opening reader
		/// </summary>
		OpeningReader,
		/// <summary>
		/// Reader has been opened successfully.
		/// </summary>
		ReaderOpened,
		/// <summary>
		/// Closing opened reader.
		/// </summary>
		ClosingReader,
		/// <summary>
		/// Source cannot be found.
		/// </summary>
		SourceNotFound,
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
