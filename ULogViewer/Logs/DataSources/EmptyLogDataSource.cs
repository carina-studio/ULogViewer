using System.IO;
using System.Threading;
using System.Threading.Tasks;

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
		protected override Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken) =>
			Task.FromResult<(LogDataSourceState, TextReader?)> ((LogDataSourceState.UnclassifiedError, null));


		// Prepare.
		protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken) => 
			Task.FromResult(LogDataSourceState.UnclassifiedError);
	}
}
