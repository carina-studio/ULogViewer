using CarinaStudio.Collections;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="UdpServerLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class UdpServerLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new UdpServerLogDataSource(this, options);
	public override string Name => "UDP Server";
	public override ISet<string> RequiredSourceOptions => new HashSet<string>
	{
		nameof(LogDataSourceOptions.IPEndPoint),
	}.AsReadOnly();
	public override ISet<string> SupportedSourceOptions => new HashSet<string>
	{
		nameof(LogDataSourceOptions.Encoding),
		nameof(LogDataSourceOptions.IPEndPoint),
	}.AsReadOnly();
}
