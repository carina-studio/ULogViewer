using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

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
		public TcpServerLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new TcpServerLogDataSource(this, options);
		public override string Name => "TCP Server";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.IPEndPoint),
		}.AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Encoding),
			nameof(LogDataSourceOptions.IPEndPoint),
		}.AsReadOnly();
	}
}
