using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

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
		public DummyLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Update display name.
		protected override string OnUpdateDisplayName() => "Dummy";


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new DummyLogDataSource(this);
		public override string Name => "Dummy";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>().AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>().AsReadOnly();
		
	}
}
