using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="TcpServerLogDataSource"/>.
	/// </summary>
	class TcpServerLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="TcpServerLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public TcpServerLogDataSourceProvider(IApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new TcpServerLogDataSource(this, options);
		public override string Name => "TCP Server";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.Tcp;
	}
}
