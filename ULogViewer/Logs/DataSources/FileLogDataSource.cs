using CarinaStudio.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSource"/> for file.
	/// </summary>
	class FileLogDataSource : BaseLogDataSource
	{
		// Static fields.
		static readonly TaskFactory taskFactory = new TaskFactory(new FixedThreadsTaskScheduler(2));


		/// <summary>
		/// Initialize new <see cref="FileLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <param name="options">Options.</param>
		public FileLogDataSource(FileLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
		{
			if (options.FileName == null)
				throw new ArgumentException("No file name specified.");
		}


		// Open reader.
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			var options = this.CreationOptions;
			try
			{
				var fileName = options.FileName.AsNonNull();
				var encoding = options.Encoding ?? Encoding.UTF8;
				reader = new FileStream(fileName, FileMode.Open, FileAccess.Read).Let(stream =>
				{
					return Path.GetExtension(options.FileName)?.ToLower() switch
					{
						".gz" => new GZipStream(stream, CompressionMode.Decompress).Let(gzipStream =>
							new StreamReader(gzipStream, encoding)),
						_ => new StreamReader(stream, encoding),
					};
				});
				return LogDataSourceState.ReaderOpened;
			}
			catch (FileNotFoundException)
			{
				this.Logger.LogError($"File '{options.FileName}' not found");
				reader = null;
				return LogDataSourceState.SourceNotFound;
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Unable to open file '{options.FileName}'");
				reader = null;
				return LogDataSourceState.UnclassifiedError;
			}
		}


		// Prepare.
		protected override LogDataSourceState PrepareCore()
		{
			if (File.Exists(this.CreationOptions.FileName))
				return LogDataSourceState.ReadyToOpenReader;
			return LogDataSourceState.SourceNotFound;
		}
	}
}
