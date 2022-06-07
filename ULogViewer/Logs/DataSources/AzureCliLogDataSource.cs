using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

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
    protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    protected override LogDataSourceState PrepareCore()
    {
        throw new NotImplementedException();
    }
}