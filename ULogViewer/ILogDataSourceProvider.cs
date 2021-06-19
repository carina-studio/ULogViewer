using CarinaStudio.Threading;
using System;
using System.ComponentModel;
using System.Net;
using System.Text;

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
		/// Create <see cref="ILogDataSource"/> instance.
		/// </summary>
		/// <param name="options">Options.</param>
		/// <returns><see cref="ILogDataSource"/> instance.</returns>
		ILogDataSource CreateSource(LogDataSourceOptions options);


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
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.StandardOutput"/>.</remarks>
		public string? Command { get; set; }


		/// <summary>
		/// Get or set encoding of text.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.File"/>.</remarks>
		public Encoding? Encoding { get; set; }


		/// <summary>
		/// Get or set name of file to open.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.File"/>.</remarks>
		public string? FileName { get; set; }


		/// <summary>
		/// Get or set URI to connect.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.WebRequest"/>.</remarks>
		public Uri? Uri { get; set; }


		/// <summary>
		/// Get or set credentials.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.WebRequest"/>.</remarks>
		public ICredentials? WebRequestCredentials { get; set; }


		/// <summary>
		/// Path of working directory.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.StandardOutput"/>.</remarks>
		public string? WorkingDirectory { get; set; }
	}
}
