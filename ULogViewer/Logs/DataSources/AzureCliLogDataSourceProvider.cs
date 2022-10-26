using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using System;
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
        this.ExternalDependencies = ListExtensions.AsReadOnly(app.ExternalDependencies.Where(it => it.Id == "AzureCLI").ToArray());
    }


    /// <inheritdoc/>
    protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) =>
        new AzureCliLogDataSource(this, options);


    /// <inheritdoc/>
    public override IEnumerable<ExternalDependency> ExternalDependencies { get; }


    /// <inheritdoc/>
    public override Uri? GetSourceOptionReferenceUri(string name) => name switch
    {
        nameof(LogDataSourceOptions.Command) => new Uri("https://docs.microsoft.com/cli/azure/reference-index"),
        _ => base.GetSourceOptionReferenceUri(name),
    };


    /// <inheritdoc/>
	public override bool IsProVersionOnly { get => true; }


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