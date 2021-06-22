using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="FileLogDataSource"/>.
	/// </summary>
	class FileLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initiaize new <see cref="FileLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public FileLogDataSourceProvider(App app) : base(app)
		{ }


		// Implementations.
		public override string Name => "File";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.File;
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new FileLogDataSource(this, options);
	}
}
