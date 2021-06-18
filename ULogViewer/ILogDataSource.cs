using CarinaStudio.Threading;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Interface of source providing log data.
	/// </summary>
	interface ILogDataSource : IApplicationObject, IDisposable, INotifyPropertyChanged, IThreadDependent
	{
		/// <summary>
		/// Get name for displaying purpose.
		/// </summary>
		string? DisplayName { get; }


		/// <summary>
		/// Check whether data in source are generated continuously or not.
		/// </summary>
		bool IsContinuousStream { get; }


		/// <summary>
		/// Check whether it is valid to reading data from source or not.
		/// </summary>
		bool IsValid { get; }


		/// <summary>
		/// Open <see cref="StreamReader"/> to read log data asynchronously.
		/// </summary>
		/// <returns>Task of opening <see cref="StreamReader"/>.</returns>
		Task<StreamReader> OpenReaderAsync();


		/// <summary>
		/// Get <see cref="ILogDataSourceProvider"/> which creates this instance.
		/// </summary>
		ILogDataSourceProvider Provider { get; }


		/// <summary>
		/// Get underlying source of log.
		/// </summary>
		UnderlyingLogDataSource UnderlyingSource { get; }
	}
}
