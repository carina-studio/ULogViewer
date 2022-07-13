using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

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
		public HttpLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Update display name.
		protected override string OnUpdateDisplayName() => "HTTP/HTTPS";


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new HttpLogDataSource(this, options);
		public override string Name => "Http";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Uri),
		}.AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.FormatJsonData),
			//nameof(LogDataSourceOptions.FormatXmlData),
			nameof(LogDataSourceOptions.Password),
			nameof(LogDataSourceOptions.Uri),
			nameof(LogDataSourceOptions.UserName),
		}.AsReadOnly();
	}
}
