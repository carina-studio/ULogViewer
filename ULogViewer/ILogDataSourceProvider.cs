using CarinaStudio.Threading;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Provider of <see cref="ILogDataSource"/>.
	/// </summary>
	interface ILogDataSourceProvider : IApplicationObject, INotifyPropertyChanged, IThreadDependent
	{
		/// <summary>
		/// Get number of active <see cref="ILogDataSource"/> instances created by this provider.
		/// </summary>
		int ActiveSourceCount { get; }


		/// <summary>
		/// Check whether multiple active <see cref="ILogDataSource"/> instances created by this provider is allowed or not.
		/// </summary>
		bool AllowMultipleSources { get; }


		/// <summary>
		/// Create <see cref="ILogDataSource"/> instance asynchronously.
		/// </summary>
		/// <param name="options">Options.</param>
		/// <returns>Task of creating <see cref="ILogDataSource"/> instance.</returns>
		Task<ILogDataSource> CreateSourceAsync(LogDataSourceOptions options);


		/// <summary>
		/// Get name for displaying purpose.
		/// </summary>
		string DisplayName { get; }


		/// <summary>
		/// Get unique name to identify this provider.
		/// </summary>
		string Name { get; }


		/// <summary>
		/// Get underlying source of log.
		/// </summary>
		UnderlyingLogDataSource UnderlyingSource { get; }
	}


	/// <summary>
	/// Options to create <see cref="ILogDataSource"/>.
	/// </summary>
	struct LogDataSourceOptions
	{
		/// <summary>
		/// Get or set command to start process.
		/// </summary>
		public string? Command { get; set; }


		/// <summary>
		/// Get or set name of file to open.
		/// </summary>
		public string? FileName { get; set; }


		/// <summary>
		/// Get or set URI to connect.
		/// </summary>
		public Uri? Uri { get; set; }
	}
}
