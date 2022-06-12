using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSource"/> which reads logs from Azure CLI.
/// </summary>
class AzureCliLogDataSource : BaseLogDataSource
{
    // Constructor.
    internal AzureCliLogDataSource(AzureCliLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
    { 
        //
    }


    /// <inheritdoc/>
    protected override Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}