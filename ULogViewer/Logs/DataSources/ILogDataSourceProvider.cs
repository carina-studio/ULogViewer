using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace CarinaStudio.ULogViewer.Logs.DataSources
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
		// Static fields.
		static readonly IList<string> emptyCommands = new string[0];


		// Fields.
		IList<string>? setupCommands;
		IList<string>? teardownCommands;


		/// <summary>
		/// Get or set command to start process.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.StandardOutput"/>.</remarks>
		public string? Command { get; set; }


		/// <summary>
		/// Create <see cref="LogDataSourceOptions"/> for <see cref="UnderlyingLogDataSource.Database"/> case.
		/// </summary>
		/// <param name="fileName">File name.</param>
		/// <param name="password">Password.</param>
		public static LogDataSourceOptions CreateForDatabase(string fileName, string? password = null) => new LogDataSourceOptions()
		{
			FileName = fileName,
			Password = password,
		};


		/// <summary>
		/// Create <see cref="LogDataSourceOptions"/> for <see cref="UnderlyingLogDataSource.Database"/> case.
		/// </summary>
		/// <param name="uri">URI of database.</param>
		/// <param name="userName">User name.</param>
		/// <param name="password">Password.</param>
		public static LogDataSourceOptions CreateForDatabase(Uri uri, string? userName, string? password = null) => new LogDataSourceOptions()
		{
			Password = password,
			Uri = uri,
			UserName = userName,
		};


		/// <summary>
		/// Create <see cref="LogDataSourceOptions"/> for <see cref="UnderlyingLogDataSource.File"/> case.
		/// </summary>
		/// <param name="fileName">File name.</param>
		/// <param name="encoding">Text encoding.</param>
		public static LogDataSourceOptions CreateForFile(string fileName, Encoding? encoding = null) => new LogDataSourceOptions()
		{
			Encoding = encoding,
			FileName = fileName,
		};


		/// <summary>
		/// Create <see cref="LogDataSourceOptions"/> for <see cref="UnderlyingLogDataSource.StandardOutput"/> case.
		/// </summary>
		/// <param name="command">Command.</param>
		/// <param name="workingDirectory">Working directory.</param>
		/// <param name="setupCommands">Commands before executing <paramref name="command"/>.</param>
		/// <param name="teardownCommands">Commands after executing <paramref name="command"/>.</param>
		public static LogDataSourceOptions CreateForStandardOutput(string command, string? workingDirectory = null, IList<string>? setupCommands = null, IList<string>? teardownCommands = null) => new LogDataSourceOptions()
		{
			Command = command,
			SetupCommands = setupCommands ?? emptyCommands,
			TeardownCommands = teardownCommands ?? emptyCommands,
			WorkingDirectory = workingDirectory,
		};


		/// <summary>
		/// Create <see cref="LogDataSourceOptions"/> for <see cref="UnderlyingLogDataSource.WebRequest"/> case.
		/// </summary>
		/// <param name="uri">Uri of request.</param>
		/// <param name="userName">User name.</param>
		/// <param name="password">Password.</param>
		public static LogDataSourceOptions CreateForWebRequest(Uri uri, string? userName = null, string? password = null) => new LogDataSourceOptions()
		{
			Password = password,
			Uri = uri,
			UserName = userName,
		};


		/// <summary>
		/// Create <see cref="LogDataSourceOptions"/> for <see cref="UnderlyingLogDataSource.WindowsEventLogs"/> case.
		/// </summary>
		/// <param name="category">Category of windows event logs.</param>
		public static LogDataSourceOptions CreateForWindowsEventLogs(string category) => new LogDataSourceOptions()
		{
			Category = category,
		};


		/// <summary>
		/// Get or set category to read log data.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.WindowsEventLogs"/>.</remarks>
		public string? Category { get; set; }


		/// <summary>
		/// Get or set encoding of text.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.File"/>.</remarks>
		public Encoding? Encoding { get; set; }


		// Check equality.
		public override bool Equals(object? obj)
		{
			if (obj is LogDataSourceOptions options)
			{
				return this.Category == options.Category
					&& this.Command == options.Command
					&& this.Encoding == options.Encoding
					&& this.FileName == options.FileName
					&& this.Password == options.Password
					&& this.SetupCommands.SequenceEqual(options.SetupCommands)
					&& this.TeardownCommands.SequenceEqual(options.TeardownCommands)
					&& this.Uri == options.Uri
					&& this.UserName == options.UserName
					&& this.WorkingDirectory == options.WorkingDirectory;
			}
			return false;
		}


		/// <summary>
		/// Get or set name of file to open.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.Database"/> and <see cref="UnderlyingLogDataSource.File"/>.</remarks>
		public string? FileName { get; set; }


		// Get hash code.
		public override int GetHashCode()
		{
			if (this.Category != null)
				return this.Category.GetHashCode();
			if (this.Command != null)
				return this.Command.GetHashCode();
			if (this.FileName != null)
				return this.FileName.GetHashCode();
			if (this.Uri != null)
				return this.Uri.GetHashCode();
			return 0;
		}


		/// <summary>
		/// Equality operator.
		/// </summary>
		public static bool operator ==(LogDataSourceOptions x, LogDataSourceOptions y) => x.Equals(y);


		/// <summary>
		/// Inequality operator.
		/// </summary>
		public static bool operator !=(LogDataSourceOptions x, LogDataSourceOptions y) => !x.Equals(y);


		/// <summary>
		/// Get or set command to start process.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.Database"/> and <see cref="UnderlyingLogDataSource.WebRequest"/>.</remarks>
		public string? Password { get; set; }


		/// <summary>
		/// Get or set commands before executing <see cref="Command"/>.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.StandardOutput"/>.</remarks>
		public IList<string> SetupCommands
		{
			get => this.setupCommands ?? emptyCommands;
			set => this.setupCommands = value.IsNotEmpty() ? new List<string>(value).AsReadOnly() : emptyCommands;
		}


		/// <summary>
		/// Get or set commands after executing <see cref="Command"/>.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.StandardOutput"/>.</remarks>
		public IList<string> TeardownCommands
		{
			get => this.teardownCommands ?? emptyCommands;
			set => this.teardownCommands = value.IsNotEmpty() ? new List<string>(value).AsReadOnly() : emptyCommands;
		}


		/// <summary>
		/// Get or set user name.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.Database"/> and <see cref="UnderlyingLogDataSource.WebRequest"/>.</remarks>
		public string? UserName { get; set; }


		/// <summary>
		/// Get or set URI to connect.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.Database"/> and <see cref="UnderlyingLogDataSource.WebRequest"/>.</remarks>
		public Uri? Uri { get; set; }


		/// <summary>
		/// Path of working directory.
		/// </summary>
		/// <remarks>Available for <see cref="UnderlyingLogDataSource.StandardOutput"/>.</remarks>
		public string? WorkingDirectory { get; set; }
	}
}
