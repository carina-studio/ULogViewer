﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// <see cref="ILogDataSource"/> for reading logs from <see cref="MemoryLogger"/>.
	/// </summary>
	class MemoryLoggerLogDataSource : BaseLogDataSource
	{
		// Reader.
		class ReaderImpl : TextReader
		{
			// Fields.
			readonly IEnumerator<string> enumerator;

			// Constructor.
			public ReaderImpl()
			{
				this.enumerator = MemoryLogger.EnumerateLogs().GetEnumerator();
			}

			// Dispose.
			protected override void Dispose(bool disposing)
			{
				this.enumerator.Dispose();
				base.Dispose(disposing);
			}

			// Read line.
			public override string? ReadLine()
			{
				if (!this.enumerator.MoveNext())
					return null;
				return this.enumerator.Current;
			}
		}


		// Constructor.
		public MemoryLoggerLogDataSource(MemoryLoggerLogDataSourceProvider provider) : base(provider, new LogDataSourceOptions())
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
