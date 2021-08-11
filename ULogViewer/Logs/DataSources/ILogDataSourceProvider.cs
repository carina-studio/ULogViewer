using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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
		/// Get the set of name of options which are required by creating <see cref="ILogDataSource"/>.
		/// </summary>
		ISet<string> RequiredSourceOptions { get; }


		/// <summary>
		/// Get the set of name of options which are supported by this provider and created <see cref="ILogDataSource"/>.
		/// </summary>
		ISet<string> SupportedSourceOptions { get; }


		/// <summary>
		/// Get underlying source of log.
		/// </summary>
		UnderlyingLogDataSource UnderlyingSource { get; }


		/// <summary>
		/// Validate whether given options are valid for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="options">Options to check.</param>
		/// <returns>True if options are valid for creating <see cref="ILogDataSource"/>.</returns>
		bool ValidateSourceOptions(LogDataSourceOptions options);
	}


	/// <summary>
	/// Options to create <see cref="ILogDataSource"/>.
	/// </summary>
	struct LogDataSourceOptions
	{
		// Static fields.
		static readonly IList<string> emptyCommands = new string[0];
		static volatile bool isOptionPropertyInfoMapReady;
		static readonly Dictionary<string, PropertyInfo> optionPropertyInfoMap = new Dictionary<string, PropertyInfo>();


		// Fields.
		IList<string>? setupCommands;
		IList<string>? teardownCommands;


		/// <summary>
		/// Get or set category to read log data.
		/// </summary>
		public string? Category { get; set; }


		/// <summary>
		/// Get or set command to start process.
		/// </summary>
		public string? Command { get; set; }


		/// <summary>
		/// Get or set encoding of text.
		/// </summary>
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
					&& this.QueryString == options.QueryString
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
		/// Get specific option.
		/// </summary>
		/// <param name="optionName">Name of option to get.</param>
		/// <returns>Value of option.</returns>
		public object? GetOption(string optionName)
		{
			SetupOptionPropertyInfoMap();
			if (optionPropertyInfoMap.TryGetValue(optionName, out var propertyInfo))
				return propertyInfo?.GetValue(this);
			return null;
		}


		/// <summary>
		/// Check whether given option has been set with value or not.
		/// </summary>
		/// <param name="optionName">Name of option.</param>
		/// <returns>True if option has been set with value.</returns>
		public bool IsOptionSet(string optionName)
		{
			SetupOptionPropertyInfoMap();
			if (!optionPropertyInfoMap.TryGetValue(optionName, out var propertyInfo) || propertyInfo == null)
				return false;
			var type = propertyInfo.PropertyType;
			if (type == typeof(string))
				return !string.IsNullOrWhiteSpace(propertyInfo.GetValue(this) as string);
			if (type == typeof(IList<string>))
				return (propertyInfo.GetValue(this) as IList<string>)?.Count > 0;
			return propertyInfo.GetValue(this) != null;
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
		public string? Password { get; set; }


		/// <summary>
		/// Get or set query string.
		/// </summary>
		public string? QueryString { get; set; }


		/// <summary>
		/// Get or set commands before executing <see cref="Command"/>.
		/// </summary>
		public IList<string> SetupCommands
		{
			get => this.setupCommands ?? emptyCommands;
			set => this.setupCommands = value.IsNotEmpty() ? new List<string>(value).AsReadOnly() : emptyCommands;
		}


		// Setup table of property info of options.
		static void SetupOptionPropertyInfoMap()
		{
			if (isOptionPropertyInfoMapReady)
				return;
			lock (typeof(LogDataSourceOptions))
			{
				if (isOptionPropertyInfoMapReady)
					return;
				foreach (var propertyInfo in typeof(LogDataSourceOptions).GetProperties())
					optionPropertyInfoMap[propertyInfo.Name] = propertyInfo;
				isOptionPropertyInfoMapReady = true;
			}
		}


		/// <summary>
		/// Get or set commands after executing <see cref="Command"/>.
		/// </summary>
		public IList<string> TeardownCommands
		{
			get => this.teardownCommands ?? emptyCommands;
			set => this.teardownCommands = value.IsNotEmpty() ? new List<string>(value).AsReadOnly() : emptyCommands;
		}


		/// <summary>
		/// Get or set user name.
		/// </summary>
		public string? UserName { get; set; }


		/// <summary>
		/// Get or set URI to connect.
		/// </summary>
		public Uri? Uri { get; set; }


		/// <summary>
		/// Path of working directory.
		/// </summary>
		public string? WorkingDirectory { get; set; }
	}


	/// <summary>
	/// Extensions for <see cref="ILogDataSourceProvider"/>.
	/// </summary>
	static class LogDataSourceProviderExtensions
	{
		/// <summary>
		/// Check whether given option is reqired for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/>.</param>
		/// <param name="optionName">Name of option to check.</param>
		/// <returns>True if given option is reqired for creating <see cref="ILogDataSource"/>.</returns>
		public static bool IsSourceOptionRequired(this ILogDataSourceProvider provider, string optionName) => provider.RequiredSourceOptions.Contains(optionName);


		/// <summary>
		/// Check whether given option is supported for creating <see cref="ILogDataSource"/> or not.
		/// </summary>
		/// <param name="provider"><see cref="ILogDataSourceProvider"/>.</param>
		/// <param name="optionName">Name of option to check.</param>
		/// <returns>True if given option is supported for creating <see cref="ILogDataSource"/>.</returns>
		public static bool IsSourceOptionSupported(this ILogDataSourceProvider provider, string optionName) => provider.SupportedSourceOptions.Contains(optionName);
	}
}
