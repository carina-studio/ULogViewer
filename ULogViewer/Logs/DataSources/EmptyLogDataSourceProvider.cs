using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="EmptyLogDataSource"/>.
	/// </summary>
	class EmptyLogDataSourceProvider : BaseLogDataSourceProvider
	{
		/// <summary>
		/// Initialize new <see cref="EmptyLogDataSourceProvider"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public EmptyLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new EmptyLogDataSource(this, options);
		public override string Name => "Empty";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>().AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>().AsReadOnly();
	}
}
