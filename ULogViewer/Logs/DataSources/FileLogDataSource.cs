using CarinaStudio.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
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
		protected override async Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
		{
			var options = this.CreationOptions;
			var fileName = options.FileName.AsNonNull();
			var encoding = options.Encoding ?? Encoding.UTF8;
			var result = LogDataSourceState.UnclassifiedError;
			var reader = await this.TaskFactory.StartNew(() =>
			{
				var stopwatch = new Stopwatch().Also(it => it.Start());
				try
				{
					while (true)
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return null;
							return new FileStream(fileName, FileMode.Open, FileAccess.Read).Let(stream =>
							{
								var reader = Path.GetExtension(options.FileName)?.ToLower() switch
								{
									".gz" => new GZipStream(stream, CompressionMode.Decompress).Let(gzipStream =>
										new StreamReader(gzipStream, encoding)),
									".json" => options.FormatJsonData
										? new FormattedJsonTextReader(new StreamReader(stream, encoding))
										: new StreamReader(stream, encoding),
									_ => (TextReader)new StreamReader(stream, encoding),
								};
								if (this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.ReadRawLogLinesConcurrently))
									reader = new IO.ConcurrentTextReader(reader);
								result = LogDataSourceState.ReaderOpened;
								return reader;
							});
						}
						catch (FileNotFoundException)
						{
							this.Logger.LogError("File '{fileName}' not found", options.FileName);
							result = LogDataSourceState.SourceNotFound;
							return null;
						}
						catch (Exception ex)
						{
							if (stopwatch.ElapsedMilliseconds < 5000)
							{
								this.Logger.LogWarning(ex, "Unable to open file '{fileName}', try again later", options.FileName);
								Thread.Sleep(100);
							}
							else
							{
								this.Logger.LogError(ex, "Unable to open file '{fileName}'", options.FileName);
								return null;
							}
						}
					}
				}
				finally 
				{
					stopwatch.Stop();
				}
			});
			if (cancellationToken.IsCancellationRequested)
			{
				if (reader != null)
					_ = this.TaskFactory.StartNew(reader.Close, CancellationToken.None);
				throw new TaskCanceledException();
			}
			return (result, reader);
		}


		// Prepare.
		protected override async Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken)
		{
			if (await this.TaskFactory.StartNew(() => File.Exists(this.CreationOptions.FileName)))
				return LogDataSourceState.ReadyToOpenReader;
			return LogDataSourceState.SourceNotFound;
		}
	}
}
