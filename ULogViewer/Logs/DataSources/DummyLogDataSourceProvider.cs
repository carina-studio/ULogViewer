using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="DummyLogDataSource"/>.
	/// </summary>
	class DummyLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="DummyLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public DummyLogDataSourceProvider(IApplication app) : base(app)
		{ }


		// Update display name.
		protected override string OnUpdateDisplayName() => "Dummy";


		// Implementations.
		public override string Name => "Dummy";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.Undefined;
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new DummyLogDataSource(this);
	}
}
