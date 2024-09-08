using CarinaStudio.Collections;
using System.Collections.Generic;

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
    public override ISet<string> SupportedSourceOptions { get; } = new HashSet<string>
    {
        nameof(LogDataSourceOptions.FileName),
    }.AsReadOnly();
}