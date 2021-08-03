using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="HttpLogDataSource"/>.
	/// </summary>
	class HttpLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="HttpLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public HttpLogDataSourceProvider(IApplication app) : base(app)
		{ }


		// Update display name.
		protected override string OnUpdateDisplayName() => "HTTP";


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new HttpLogDataSource(this, options);
		public override string Name => "Http";
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.WebRequest;
	}
}
