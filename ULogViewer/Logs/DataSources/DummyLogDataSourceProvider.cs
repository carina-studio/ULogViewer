using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="DummyLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class DummyLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	// Update display name.
	protected override string OnUpdateDisplayName() => "Dummy";


	// Implementations.
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new DummyLogDataSource(this);
	public override string Name => "Dummy";
	public override ISet<string> RequiredSourceOptions => ImmutableHashSet<string>.Empty;
	public override ISet<string> SupportedSourceOptions => ImmutableHashSet<string>.Empty;
}
