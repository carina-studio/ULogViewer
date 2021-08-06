using CarinaStudio.Collections;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Dummy implementation of <see cref="ILogDataSource"/> to generate log data continuously.
	/// </summary>
	class DummyLogDataSource : BaseLogDataSource
	{
		// Implementation of log data reader.
		class ReaderImpl : TextReader
		{
			int nextMessageId = 1;
			readonly Random random = new Random();
			public override string? ReadLine()
			{
				// simulate end of data
				if (this.random.Next(10001) == 1)
					return null;

				// prepare
				var log = new StringBuilder();

				// timestamp
				log.Append(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"));

				// level
				log.Append(Enum.GetValues<LogLevel>().SelectRandomElement() switch
				{
					LogLevel.Debug => " D",
					LogLevel.Error => " E",
					LogLevel.Fatal => " F",
					LogLevel.Info => " I",
					LogLevel.Verbose => " V",
					LogLevel.Warn => " W",
					_ => " U",
				});

				// message
				log.Append($" Message#{this.nextMessageId++} &lt;XML encoded string&gt; \\u003CJSON encoded string\\u003E");

				// complete
				return log.ToString();
			}
		}


		/// <summary>
		/// Initialize new <see cref="DummyLogDataSource"/> instance.
		/// </summary>
		/// <param name="provider">Provider.</param>
		public DummyLogDataSource(DummyLogDataSourceProvider provider) : base(provider, new LogDataSourceOptions())
		{ }


		// Open reader.
		protected override LogDataSourceState OpenReaderCore(CancellationToken cancellationToken, out TextReader? reader)
		{
			reader = new ReaderImpl();
			return LogDataSourceState.ReaderOpened;
		}


		// Prepare.
		protected override LogDataSourceState PrepareCore() => LogDataSourceState.ReadyToOpenReader;
	}
}
