using System;

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
		public SQLiteLogDataSourceProvider(IApplication app) : base(app)
		{ }


		// Update display name.
		protected override string OnUpdateDisplayName() => "SQLite";


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new SQLiteLogDataSource(this, options);
		public override string Name => "SQLite";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.Database;
	}
}
