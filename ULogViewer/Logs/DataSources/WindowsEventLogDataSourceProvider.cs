using CarinaStudio.Collections;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="WindowsEventLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class WindowsEventLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new WindowsEventLogDataSource(this, options);
	public override string Name => "WindowsEventLogs";
	public override ISet<string> RequiredSourceOptions => new HashSet<string>
	{
		nameof(LogDataSourceOptions.Category),
	}.AsReadOnly();
	public override ISet<string> SupportedSourceOptions => new HashSet<string>
	{
		nameof(LogDataSourceOptions.Category),
	}.AsReadOnly();
}
