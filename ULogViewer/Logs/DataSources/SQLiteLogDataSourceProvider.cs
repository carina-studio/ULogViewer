using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="SQLiteLogDataSource"/>.
	/// </summary>
	class SQLiteLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="SQLiteLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public SQLiteLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		/// <inheritdoc/>
		public override Uri? GetSourceOptionReferenceUri(string name) => name switch
		{
			nameof(LogDataSourceOptions.QueryString) => new Uri("https://www.sqlite.org/lang_select.html"),
			_ => base.GetSourceOptionReferenceUri(name),
		};


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new SQLiteLogDataSource(this, options);
		public override string Name => "SQLite";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.FileName),
			nameof(LogDataSourceOptions.QueryString),
		}.AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.FileName),
			nameof(LogDataSourceOptions.Password),
			nameof(LogDataSourceOptions.QueryString),
		}.AsReadOnly();
	}
}
