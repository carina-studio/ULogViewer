using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="FileLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class FileLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new FileLogDataSource(this, options);
	public override string Name => "File";
	public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.FileName)
	);
	public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.Encoding),
		nameof(LogDataSourceOptions.FileName),
		nameof(LogDataSourceOptions.FormatJsonData)
		//nameof(LogDataSourceOptions.FormatXmlData)
	);
}