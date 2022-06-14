using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Provider for <see cref="SqlServerDatabaseLogDataSource"/>.
/// </summary>
class SqlServerDatabaseLogDataSourceProvider : BaseLogDataSourceProvider
{
    /// <summary>
    /// Initialize new <see cref="SqlServerDatabaseLogDataSourceProvider"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public SqlServerDatabaseLogDataSourceProvider(IULogViewerApplication app) : base(app)
    { }


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
    public override ISet<string> RequiredSourceOptions => new HashSet<string>()
    {
        nameof(LogDataSourceOptions.ConnectionString),
        nameof(LogDataSourceOptions.QueryString),
    }.AsReadOnly();


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions =>
        this.RequiredSourceOptions;
}