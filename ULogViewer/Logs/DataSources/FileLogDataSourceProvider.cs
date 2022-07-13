using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

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
		public FileLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new FileLogDataSource(this, options);
		public override string Name => "File";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.FileName),
		}.AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Encoding),
			nameof(LogDataSourceOptions.FileName),
			nameof(LogDataSourceOptions.FormatJsonData),
			//nameof(LogDataSourceOptions.FormatXmlData),
		}.AsReadOnly();
	}
}
