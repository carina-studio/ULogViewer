using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

class WindowsEventLogFileDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
    /// <inheritdoc/>
    protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) =>
        new WindowsEventLogFileDataSource(this, options);


    /// <inheritdoc/>
    public override string Name => "WindowsEventLogFile";


    /// <inheritdoc/>
    public override ISet<string> RequiredSourceOptions => this.SupportedSourceOptions;


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
        nameof(LogDataSourceOptions.FileName)
    );
}