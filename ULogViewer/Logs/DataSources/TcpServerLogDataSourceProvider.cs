using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="TcpServerLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class TcpServerLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new TcpServerLogDataSource(this, options);
	public override string Name => "TCP Server";
	public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.IPEndPoint)
	);
	public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.Encoding),
		nameof(LogDataSourceOptions.IPEndPoint)
	);
}
