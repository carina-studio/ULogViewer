using CarinaStudio.Collections;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSourceProvider"/> for <see cref="MemoryLoggerLogDataSource"/>.
	/// </summary>
	class MemoryLoggerLogDataSourceProvider : BaseLogDataSourceProvider
	{
		// Constructor.
		public MemoryLoggerLogDataSourceProvider(IULogViewerApplication app) : base(app)
		{ }


		// Implementations.
		protected override ILogDataSource CreateSourceCore(LogDataSourceOptions options) => new MemoryLoggerLogDataSource(this);
		public override string Name => "MemoryLogger";
		public override ISet<string> RequiredSourceOptions => new HashSet<string>().AsReadOnly();
		public override ISet<string> SupportedSourceOptions => new HashSet<string>().AsReadOnly();
	}
}
