using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Context to create displayable logs for testing.
/// </summary>
class DisplayableLogTestContext : IDisposable
{
	// Fields.
	readonly List<string> filePaths = [];
	readonly DisplayableLogGroup group;
	readonly LogBuilder logBuilder = new();
	readonly List<ILogDataSource> logDataSources = [];
	readonly List<LogReader> logReaders = [];


	/// <summary>
	/// Initialize new <see cref="DisplayableLogTestContext"/> instance.
	/// </summary>
	/// <param name="app">Application.</param>
	/// <param name="profileSetup">Action to setup log profile before creating group.</param>
	public DisplayableLogTestContext(IULogViewerApplication app, Action<LogProfile>? profileSetup = null)
	{
		var profile = new LogProfile(app);
		profileSetup?.Invoke(profile);
		this.group = new DisplayableLogGroup(profile);
		this.CreateLogReader();
	}


	/// <summary>
	/// Create displayable log with given properties through default log reader.
	/// </summary>
	/// <param name="setup">Action to setup properties of log.</param>
	/// <returns>Created displayable log.</returns>
	public DisplayableLog CreateLog(Action<LogBuilder>? setup = null) =>
		this.CreateLog(this.logReaders[0], setup);


	/// <summary>
	/// Create displayable log with given properties through given log reader.
	/// </summary>
	/// <param name="reader">Log reader.</param>
	/// <param name="setup">Action to setup properties of log.</param>
	/// <returns>Created displayable log.</returns>
	public DisplayableLog CreateLog(LogReader reader, Action<LogBuilder>? setup = null)
	{
		setup?.Invoke(this.logBuilder);
		return new DisplayableLog(this.group, reader, this.logBuilder.BuildAndReset());
	}


	/// <summary>
	/// Create additional log reader with its own file data source.
	/// </summary>
	/// <returns>Created log reader.</returns>
	public LogReader CreateLogReader()
	{
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");
		var filePath = Path.GetTempFileName();
		this.filePaths.Add(filePath);
		var source = provider.CreateSource(new LogDataSourceOptions { FileName = filePath });
		this.logDataSources.Add(source);
		return new LogReader(this.group, source).Also(this.logReaders.Add);
	}


	/// <summary>
	/// Get default log reader.
	/// </summary>
	public LogReader DefaultLogReader => this.logReaders[0];


	/// <summary>
	/// Dispose context.
	/// </summary>
	public void Dispose()
	{
		// dispose readers and sources
		foreach (var reader in this.logReaders)
			reader.Dispose();
		foreach (var source in this.logDataSources)
			source.Dispose();

		// dispose group
		this.group.Dispose();

		// delete files
		foreach (var filePath in this.filePaths)
			Global.RunWithoutError(() => File.Delete(filePath));
	}


	/// <summary>
	/// Get group of created displayable logs.
	/// </summary>
	public DisplayableLogGroup Group => this.group;


	/// <summary>
	/// Get log profile used by the group.
	/// </summary>
	public LogProfile Profile => this.group.LogProfile;
}
