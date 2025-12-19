using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Provider for <see cref="SqlServerDatabaseLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class SqlServerDatabaseLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
    /// <inheritdoc/>
    protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) =>
        new SqlServerDatabaseLogDataSource(this, options);


    /// <inheritdoc/>
    public override bool IsProVersionOnly => true;


    /// <inheritdoc/>
    public override Uri? GetSourceOptionReferenceUri(string name) => name switch
    {
        nameof(LogDataSourceOptions.ConnectionString) => new Uri("https://docs.microsoft.com/dotnet/api/system.data.sqlclient.sqlconnection.connectionstring"),
        nameof(LogDataSourceOptions.QueryString) => new Uri("https://docs.microsoft.com/sql/t-sql/queries/select-transact-sql"),
        _ => base.GetSourceOptionReferenceUri(name),
    };


    /// <inheritdoc/>
    public override string Name => "SqlServerDatabase";


    /// <inheritdoc/>
    public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
        nameof(LogDataSourceOptions.ConnectionString),
        nameof(LogDataSourceOptions.QueryString)
    );


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions =>
        this.RequiredSourceOptions;
}