using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Provider for <see cref="MySqlDatabaseLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class MySqlDatabaseLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
    /// <inheritdoc/>
    protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) =>
        new MySqlDatabaseLogDataSource(this, options);


    public override Uri? GetSourceOptionReferenceUri(string name) => name switch
    {
        nameof(LogDataSourceOptions.ConnectionString) => new Uri("https://dev.mysql.com/doc/connector-net/en/connector-net-connections-string.html"),
        nameof(LogDataSourceOptions.QueryString) => new Uri("https://www.w3schools.com/mysql/mysql_select.asp"),
        _ => base.GetSourceOptionReferenceUri(name),
    };


    /// <inheritdoc/>
    public override bool IsProVersionOnly => true;


    /// <inheritdoc/>
    public override string Name => "MySqlDatabase";


    /// <inheritdoc/>
    public override ISet<string> RequiredSourceOptions { get; } = new HashSet<string>
    {
        nameof(LogDataSourceOptions.ConnectionString),
        nameof(LogDataSourceOptions.QueryString),
    }.AsReadOnly();


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions { get; } = new HashSet<string>
    {
        nameof(LogDataSourceOptions.ConnectionString),
        nameof(LogDataSourceOptions.IsResourceOnAzure),
        nameof(LogDataSourceOptions.QueryString),
    }.AsReadOnly();
}