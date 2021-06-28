using System;
using System.IO;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Empty implementation of <see cref="ILogDataSource"/>.
	/// </summary>
	class EmptyLogDataSource : BaseLogDataSource
	{
		/// <summary>
		/// Initialize new <see cref="EmptyLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <param name="options">Options.</param>
		public EmptyLogDataSource(EmptyLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{ }


		// Open reader.
		protected override LogDataSourceState OpenReaderCore(out TextReader? reader)
		{
			reader = null;
			return LogDataSourceState.UnclassifiedError;
		}


		// Prepare.
		protected override LogDataSourceState PrepareCore() => LogDataSourceState.UnclassifiedError;
	}
}
