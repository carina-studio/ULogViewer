using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="AzureCliLogDataSource"/>.
/// </summary>
class AzureCliLogDataSourceProvider : BaseLogDataSourceProvider
{
    /// <summary>
    /// Initialize new <see cref="AzureCliLogDataSourceProvider"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    public AzureCliLogDataSourceProvider(IULogViewerApplication app) : base(app)
    { 
        this.ExternalDependencies = app.ExternalDependencies.Where(it => it.Id == "AzureCLI").ToArray().AsReadOnly();
    }


    /// <inheritdoc/>
    protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) =>
        new AzureCliLogDataSource(this, options);


    /// <inheritdoc/>
    public override IEnumerable<ExternalDependency> ExternalDependencies { get; }


    /// <inheritdoc/>
    public override string Name => "AzureCLI";


    /// <inheritdoc/>
    public override ISet<string> RequiredSourceOptions => new HashSet<string>()
    {
        nameof(LogDataSourceOptions.Command),
    }.AsReadOnly(); 


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions => new HashSet<string>()
    {
        nameof(LogDataSourceOptions.Command),
    }.AsReadOnly(); 
}