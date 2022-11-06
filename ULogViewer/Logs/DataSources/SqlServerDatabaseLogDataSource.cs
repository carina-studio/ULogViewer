using System;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSource"/> to read log data from SQL Server database.
/// </summary>
class SqlServerDatabaseLogDataSource : DatabaseLogDataSource<SqlConnection, SqlDataReader>
{
    // Constructor.
    internal SqlServerDatabaseLogDataSource(SqlServerDatabaseLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
    { 
        if (!options.IsOptionSet(nameof(LogDataSourceOptions.ConnectionString)))
            throw new ArgumentException("");
    }


    /// <inheritdoc/>
    protected override Task<SqlConnection> CreateConnectionAsync(LogDataSourceOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }


    /// <inheritdoc/>
    protected override Task<SqlDataReader> CreateDataReaderAsync(SqlConnection connection, LogDataSourceOptions options, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}