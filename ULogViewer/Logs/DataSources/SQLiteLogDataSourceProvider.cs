using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// <see cref="ILogDataSourceProvider"/> for <see cref="SQLiteLogDataSource"/>.
/// </summary>
/// <param name="app">Application.</param>
class SQLiteLogDataSourceProvider(IULogViewerApplication app) : BaseLogDataSourceProvider(app)
{
	/// <inheritdoc/>
	public override Uri? GetSourceOptionReferenceUri(string name) => name switch
	{
		nameof(LogDataSourceOptions.QueryString) => new Uri("https://www.sqlite.org/lang_select.html"),
		_ => base.GetSourceOptionReferenceUri(name),
	};


	// Implementations.
	protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new SQLiteLogDataSource(this, options);
	public override string Name => "SQLite";
	public override ISet<string> RequiredSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.FileName),
		nameof(LogDataSourceOptions.QueryString)
	);
	public override ISet<string> SupportedSourceOptions { get; } = ImmutableHashSet.Create(
		nameof(LogDataSourceOptions.FileName),
		nameof(LogDataSourceOptions.Password),
		nameof(LogDataSourceOptions.QueryString)
	);
}
