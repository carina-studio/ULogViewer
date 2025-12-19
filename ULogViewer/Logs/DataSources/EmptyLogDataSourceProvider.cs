using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="EmptyLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class EmptyLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new EmptyLogDataSource(this, options);
	public override string Name => "Empty";
	public override ISet<string> RequiredSourceOptions => ImmutableHashSet<string>.Empty;
	public override ISet<string> SupportedSourceOptions => ImmutableHashSet<string>.Empty;
}
