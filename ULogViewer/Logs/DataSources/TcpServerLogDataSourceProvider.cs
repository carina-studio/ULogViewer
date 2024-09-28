using CarinaStudio.Collections;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="TcpServerLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class TcpServerLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new TcpServerLogDataSource(this, options);
	public override string Name => "TCP Server";
	public override ISet<string> RequiredSourceOptions { get; } = new HashSet<string>
	{
		nameof(LogDataSourceOptions.IPEndPoint),
	}.AsReadOnly();
	public override ISet<string> SupportedSourceOptions { get; } = new HashSet<string>
	{
		nameof(LogDataSourceOptions.Encoding),
		nameof(LogDataSourceOptions.IPEndPoint),
	}.AsReadOnly();
}
