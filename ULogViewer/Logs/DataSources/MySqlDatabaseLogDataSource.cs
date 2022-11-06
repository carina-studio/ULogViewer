using MySql.Data.MySqlClient;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSource"/> to read log data from MySQL database.
/// </summary>
class MySqlDatabaseLogDataSource : DatabaseLogDataSource<MySqlConnection, MySqlDataReader>
{
    // Constructor.
    internal MySqlDatabaseLogDataSource(MySqlDatabaseLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
    {
        if (!options.IsOptionSet(nameof(LogDataSourceOptions.ConnectionString)))
            throw new ArgumentException("No connection string specified.");
        if (!options.IsOptionSet(nameof(LogDataSourceOptions.QueryString)))
            throw new ArgumentException("No query string specified.");
    }


    /// <inheritdoc/>
    protected override Task<MySqlConnection> CreateConnectionAsync(LogDataSourceOptions options, CancellationToken cancellationToken) =>
        throw new NotImplementedException();


    /// <inheritdoc/>
    protected override Task<MySqlDataReader> CreateDataReaderAsync(MySqlConnection connection, LogDataSourceOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    protected override async Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken)
    {
        return await base.PrepareCoreAsync(cancellationToken);
    }
}