using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

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
		/// <param name="app"><see cref="IULogViewerApplication"/>.</param>
		public StandardOutputLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{
		}


		// Interface implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new StandardOutputLogDataSource(this, options);
		public override string Name => "StandardOutput";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Command),
		}.AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Command),
			nameof(LogDataSourceOptions.FormatJsonData),
			//nameof(LogDataSourceOptions.FormatXmlData),
			nameof(LogDataSourceOptions.IncludeStandardError),
			nameof(LogDataSourceOptions.SetupCommands),
			nameof(LogDataSourceOptions.TeardownCommands),
			nameof(LogDataSourceOptions.UseTextShell),
			nameof(LogDataSourceOptions.WorkingDirectory),
		}.AsReadOnly();
	}
}
