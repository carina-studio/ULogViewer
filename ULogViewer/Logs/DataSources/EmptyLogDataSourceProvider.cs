using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="EmptyLogDataSource"/>.
	/// </summary>
	class EmptyLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="EmptyLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public EmptyLogDataSourceProvider(IApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new EmptyLogDataSource(this, options);
		public override string Name => "Empty";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.Undefined;
	}
}
