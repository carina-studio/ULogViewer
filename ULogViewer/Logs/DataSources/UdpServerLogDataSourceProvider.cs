using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="UdpServerLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class UdpServerLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new UdpServerLogDataSource(this, options);
	public override string Name => "UDP Server";
	public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.IPEndPoint)
	);
	public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.Encoding),
		nameof(LogDataSourceOptions.IPEndPoint)
	);
}
