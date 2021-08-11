using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="WindowsEventLogDataSource"/>.
	/// </summary>
	class WindowsEventLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="WindowsEventLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public WindowsEventLogDataSourceProvider(IApplication app) : base(app)
		{ }


		// Update display name.
		protected override string OnUpdateDisplayName() => this.Application.GetStringNonNull("WindowsEventLogDataSourceProvider.DisplayName");


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new WindowsEventLogDataSource(this, options);
		public override string Name => "WindowsEventLogs";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Category),
		}.AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>()
		{
			nameof(LogDataSourceOptions.Category),
		}.AsReadOnly();
		public override UnderlyingLogDataSource UnderlyingSource => UnderlyingLogDataSource.WindowsEventLogs;
	}
}
