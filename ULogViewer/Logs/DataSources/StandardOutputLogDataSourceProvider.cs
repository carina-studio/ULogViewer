using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="StandardOutputLogDataSource"/>.
/// </summary>
/// <param name="app"><see cref="IULogViewerApplication"/>.</param>
class StandardOutputLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new StandardOutputLogDataSource(this, options);
	public override string Name => "StandardOutput";
	public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.Command)
	);
	public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.Command),
		nameof(LogDataSourceOptions.EnvironmentVariables),
		nameof(LogDataSourceOptions.FormatJsonData),
		//nameof(LogDataSourceOptions.FormatXmlData),
		nameof(LogDataSourceOptions.IncludeStandardError),
		nameof(LogDataSourceOptions.SetupCommands),
		nameof(LogDataSourceOptions.TeardownCommands),
		nameof(LogDataSourceOptions.UseTextShell),
		nameof(LogDataSourceOptions.WorkingDirectory)
	);
}
