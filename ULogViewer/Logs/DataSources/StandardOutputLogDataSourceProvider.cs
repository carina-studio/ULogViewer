﻿using CarinaStudio.Collections;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="StandardOutputLogDataSource"/>.
/// </summary>
/// <param name="app"><see cref="IULogViewerApplication"/>.</param>
class StandardOutputLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new StandardOutputLogDataSource(this, options);
	public override string Name => "StandardOutput";
	public override ISet<string> RequiredSourceOptions { get; } = new HashSet<string>
	{
		nameof(LogDataSourceOptions.Command),
	}.AsReadOnly();
	public override ISet<string> SupportedSourceOptions { get; } = new HashSet<string>
	{
		nameof(LogDataSourceOptions.Command),
		nameof(LogDataSourceOptions.EnvironmentVariables),
		nameof(LogDataSourceOptions.FormatJsonData),
		//nameof(LogDataSourceOptions.FormatXmlData),
		nameof(LogDataSourceOptions.IncludeStandardError),
		nameof(LogDataSourceOptions.SetupCommands),
		nameof(LogDataSourceOptions.TeardownCommands),
		nameof(LogDataSourceOptions.UseTextShell),
		nameof(LogDataSourceOptions.WorkingDirectory),
	}.AsReadOnly();
}
