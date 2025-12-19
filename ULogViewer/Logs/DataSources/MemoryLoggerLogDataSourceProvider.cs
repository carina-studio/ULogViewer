using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="MemoryLoggerLogDataSource"/>.
/// </summary>
class MemoryLoggerLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new MemoryLoggerLogDataSource(this);
	public override string Name => "MemoryLogger";
	public override ISet<string> RequiredSourceOptions => ImmutableHashSet<string>.Empty;
	public override ISet<string> SupportedSourceOptions => ImmutableHashSet<string>.Empty;
}
