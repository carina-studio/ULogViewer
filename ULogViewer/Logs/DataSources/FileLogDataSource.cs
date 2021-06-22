using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

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
		protected override LogDataSourceState OpenReaderCore(out TextReader? reader)
		{
			var options = this.CreationOptions;
			try
			{
				reader = new StreamReader(options.FileName.AsNonNull(), options.Encoding ?? Encoding.UTF8);
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
