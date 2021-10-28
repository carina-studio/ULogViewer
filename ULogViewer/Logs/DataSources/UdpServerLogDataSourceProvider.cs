using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="UdpServerLogDataSource"/>.
	/// </summary>
	class UdpServerLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="UdpServerLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public UdpServerLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new UdpServerLogDataSource(this, options);
		public override string Name => "UDP Server";
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
