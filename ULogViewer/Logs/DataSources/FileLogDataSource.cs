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
		// Fields.
		volatile string? tempFilePath;


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


		// Delete temp file.
		void DeleteTempFile()
		{
			var filePath = Interlocked.Exchange(ref this.tempFilePath, null);
			if (filePath == null)
				return;
			this.TaskFactory.StartNew(() => 
			{
				this.Logger.LogDebug("Delete temp file '{fileName}'", filePath);
				try
				{
					File.Delete(filePath);
				}
				catch (Exception ex)
				{
					this.Logger.LogWarning(ex, "Failed to delete temp file '{fileName}'", filePath);
				}
			}, CancellationToken.None);
		}


		/// <inheritdoc/>
		protected override void OnReaderClosed()
		{
			this.DeleteTempFile();
			base.OnReaderClosed();
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
				var tempFilePath = (string?)null;
				try
				{
					while (true)
					{
						try
						{
							if (cancellationToken.IsCancellationRequested)
								return null;
							if (tempFilePath != null)
								this.Logger.LogWarning("Use temp file '{tempFilePath}'", tempFilePath);
							return new FileStream(tempFilePath ?? fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite).Let(stream =>
							{
								var reader = Path.GetExtension(fileName)?.ToLower() switch
								{
									".gz" => new GZipStream(stream, CompressionMode.Decompress).Let(gzipStream =>
										new StreamReader(gzipStream, encoding)),
									".json" => options.FormatJsonData
										? new FormattedJsonTextReader(new StreamReader(stream, encoding))
										: new StreamReader(stream, encoding),
									_ => (TextReader)new StreamReader(stream, encoding),
								};
								this.tempFilePath = tempFilePath;
								result = LogDataSourceState.ReaderOpened;
								return reader;
							});
						}
						catch (FileNotFoundException)
						{
							this.Logger.LogError("File '{fileName}' not found", fileName);
							result = LogDataSourceState.SourceNotFound;
							return null;
						}
						catch (Exception ex)
						{
							if (stopwatch.ElapsedMilliseconds < 5000)
							{
								this.Logger.LogWarning(ex, "Unable to open file '{fileName}', try again later", fileName);
								Thread.Sleep(300);
							}
							else if (tempFilePath == null)
							{
								// get file size
								var fileSize = Global.RunOrDefault(() => new FileInfo(fileName).Length, -1L);
								if (fileSize < 0)
								{
									this.Logger.LogError(ex, "Unable to open file '{fileName}' and failed to check size of file either", fileName);
									return null;
								}
								if (fileSize > (4L << 20))
								{
									this.Logger.LogError(ex, "Unable to open file '{fileName}' and size of file is too large: {size}", fileName, fileSize);
									return null;
								}
								try
								{
									tempFilePath = Path.GetTempFileName();
									this.Logger.LogWarning(ex, "Unable to open file '{fileName}', try copying to '{tempFilePath}'", fileName, tempFilePath);
									File.Copy(fileName, tempFilePath, true);
								}
								catch (Exception ex2)
								{
									this.Logger.LogError(ex2, "Failed to copy file '{fileName}' to '{tempFilePath}'", fileName, tempFilePath);
									if (tempFilePath != null)
										Global.RunWithoutError(() => File.Delete(tempFilePath));
									return null;
								}
							}
							else
							{
								this.Logger.LogError(ex, "Unable to open file '{fileName}'", fileName);
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
				this.DeleteTempFile();
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
