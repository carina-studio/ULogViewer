using System;
namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="StandardOutputLogDataSource"/>.
	/// </summary>
	class StandardOutputLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="StandardOutputLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app"><see cref="IApplication"/>.</param>
		public StandardOutputLogDataSourceProvider(IApplication app) : base(app)
		{
		}


		// Interface implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new StandardOutputLogDataSource(this, options);
		public override string Name => "StandardOutput";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.StandardOutput;
	}
}
