using CarinaStudio.AppSuite;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
        this.ExternalDependencies = ImmutableList.CreateRange(app.ExternalDependencies.Where(it => it.Id == "AzureCLI"));
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
	public override bool IsProVersionOnly => true;


    /// <inheritdoc/>
    public override string Name => "AzureCLI";


    /// <inheritdoc/>
    public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
        nameof(LogDataSourceOptions.Command)
    );


    /// <inheritdoc/>
    public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
        nameof(LogDataSourceOptions.Command)
    );
}