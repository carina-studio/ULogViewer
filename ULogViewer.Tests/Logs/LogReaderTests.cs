using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Tests;
using CarinaStudio.ULogViewer.Logs.DataSources;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Tests of <see cref="LogReader"/>.
	/// </summary>
	[TestFixture]
	class LogReaderTests : ApplicationBasedTests
	{
		// Constants.
		const string TimestampFormat = "yyyy-MM-dd HH-mm-ss.SSS";


		// Static fields.
		static readonly Regex LogHeaderRegex = new Regex("^(?<Timestamp>[^\\s]+[\\s]+[^\\s]+)[\\s]+(?<Level>[^\\s]+)[\\s]+(?<SourceName>[^\\s]+)\\:[\\s]*(?<Message>.*)$");
		static readonly Dictionary<string, LogLevel> LogLevelMap = new Dictionary<string, LogLevel>()
		{
			{ "D", LogLevel.Debug },
			{ "E", LogLevel.Error },
			{ "F", LogLevel.Fatal },
			{ "I", LogLevel.Info },
			{ "V", LogLevel.Verbose },
			{ "W", LogLevel.Warn },
		};
		static readonly Regex LogMessageRegex = new Regex("^[\\s]{2}(?<Message>[^\\[]*)$");
		static readonly LogPattern[] LogPatterns;
		static readonly Regex LogTailRegex = new Regex("^[\\s]{2}\\[TAIL\\]$");


		// Fields.
		string? testDirectoryPath;


		// Static initializer.
		static LogReaderTests()
		{
			LogPatterns = new LogPattern[] {
				new LogPattern(LogHeaderRegex, false, false),
				new LogPattern(LogMessageRegex, true, true),
				new LogPattern(LogTailRegex, false, false),
			};
		}


		/// <summary>
		/// Test for reading logs continuously from file.
		/// </summary>
		[Test]
		public void ContinuousReadingLogsFromFileTest()
		{
			this.TestOnApplicationThread(async () =>
			{
				if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider) || provider == null)
					throw new AssertionException("Cannot find file log data source provider.");
				for (var i = 0; i < 10; ++i)
				{
					// prepare source
					var logCount = 256;
					var filePath = this.GenerateLogFile(logCount);
					var options = new LogDataSourceOptions() { FileName = filePath };

					// create source
					using var source = provider.CreateSource(options);

					// create log reader
					using var logReader = new LogReader(source, Task.Factory)
					{
						IsContinuousReading = true,
						LogLevelMap = LogLevelMap,
						LogPatterns = LogPatterns,
						TimestampFormat = TimestampFormat,
					};

					// read logs 10 times
					logReader.Start();
					for (var j = 0; j < 50; ++j)
					{
						Assert.IsTrue(await logReader.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.Starting, 5000));
						Assert.IsTrue(await logReader.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.ReadingLogs, 5000));
						Assert.IsTrue(await logReader.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.Starting, -1));
					}
					Assert.LessOrEqual(logCount * 30, logReader.Logs.Count);
				}
			});
		}


		/// <summary>
		/// Delete generated test directory.
		/// </summary>
		[OneTimeTearDown]
		[MethodImpl(MethodImplOptions.Synchronized)]
		public void DeleteTestDirectory()
		{
			if (this.testDirectoryPath != null)
			{
				Directory.Delete(this.testDirectoryPath, true);
				this.testDirectoryPath = null;
			}
		}


		// Generate header line of log.
		string GenerateLogHeaderLine()
		{
			var timestampString = DateTime.Now.ToString(TimestampFormat);
			var levelString = ((LogLevel[])Enum.GetValues(typeof(LogLevel))).SelectRandomElement() switch
			{
				LogLevel.Debug => "D",
				LogLevel.Error => "E",
				LogLevel.Fatal => "F",
				LogLevel.Info => "I",
				LogLevel.Verbose => "V",
				LogLevel.Warn => "W",
				_ => "U",
			};
			var sourceName = Tests.Random.GenerateRandomString(4);
			var message = Tests.Random.GenerateRandomString(8);
			return $"{timestampString} {levelString} {sourceName}: {message}";
		}


		// Generate file with random logs.
		[MethodImpl(MethodImplOptions.Synchronized)]
		string GenerateLogFile(int logCount)
		{
			if (this.testDirectoryPath == null)
				this.testDirectoryPath = this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
			return Tests.Random.CreateFileWithRandomName(this.testDirectoryPath).Use(stream =>
			{
				var isFirstLine = true;
				using var writer = new StreamWriter(stream, Encoding.UTF8);
				foreach (var logLine in this.GenerateLogLines(logCount))
				{
					if (!isFirstLine)
						writer.WriteLine();
					else
						isFirstLine = false;
					writer.Write(logLine);
				}
				return stream.Name;
			});
		}


		// Generate random log lines.
		string[] GenerateLogLines(int logCount) => new List<string>().Also(it =>
		{
			if (Tests.Random.Next(2) == 0)
				it.Add("(Invalid)");
			for (var i = 0; i < logCount; ++i)
			{
				var messageLineCount = Tests.Random.Next(1, 5);
				it.Add(this.GenerateLogHeaderLine());
				for (var j = 1; j <= messageLineCount; ++j)
					it.Add(this.GenerateLogMessageLine());
				it.Add(this.GenerateLogTailLine());
				if (Tests.Random.Next(2) == 0)
					it.Add("(Invalid)");
			}
		}).ToArray();


		// Generate message line of log.
		string GenerateLogMessageLine() => $"  {Tests.Random.GenerateRandomString(8)}";


		// Generate tail line of log.
		string GenerateLogTailLine() => "  [TAIL]";


		/// <summary>
		/// Test for setting max/drop log count.
		/// </summary>
		[Test]
		public void MaxLogCountTest()
		{
			this.TestOnApplicationThread(async () =>
			{
				// prepare source
				var logCount = 256;
				var filePath = this.GenerateLogFile(logCount);
				var options = new LogDataSourceOptions() { FileName = filePath };

				// create source
				if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider) || provider == null)
					throw new AssertionException("Cannot find file log data source provider.");
				using var source = provider.CreateSource(options);

				// create log reader
				using var logReader1 = new LogReader(source, Task.Factory)
				{
					DropLogCount = 1024,
					IsContinuousReading = true,
					LogLevelMap = LogLevelMap,
					LogPatterns = LogPatterns,
					MaxLogCount = 2048,
					TimestampFormat = TimestampFormat,
				};

				// start reading logs and check log count
				logReader1.Start();
				for (var j = 0; j < 100; ++j)
				{
					await Task.Delay(200);
					Assert.LessOrEqual(logReader1.Logs.Count, logReader1.MaxLogCount);
				}
				logReader1.Dispose();

				// create log reader
				using var logReader2 = new LogReader(source, Task.Factory)
				{
					IsContinuousReading = true,
					LogLevelMap = LogLevelMap,
					LogPatterns = LogPatterns,
					MaxLogCount = 2048,
					TimestampFormat = TimestampFormat,
				};

				// start reading logs and check log count
				logReader2.Start();
				for (var j = 0; j < 100; ++j)
				{
					await Task.Delay(200);
					Assert.LessOrEqual(logReader2.Logs.Count, logReader2.MaxLogCount);
				}
				logReader2.Dispose();

				// create log reader
				using var logReader3 = new LogReader(source, Task.Factory)
				{
					DropLogCount = 4096,
					IsContinuousReading = true,
					LogLevelMap = LogLevelMap,
					LogPatterns = LogPatterns,
					MaxLogCount = 2048,
					TimestampFormat = TimestampFormat,
				};

				// start reading logs and check log count
				logReader3.Start();
				for (var j = 0; j < 100; ++j)
				{
					await Task.Delay(200);
					Assert.LessOrEqual(logReader3.Logs.Count, logReader3.MaxLogCount);
				}
				logReader3.Dispose();
			});
		}


		/// <summary>
		/// Test for pausing reading logs.
		/// </summary>
		[Test]
		public void PauseReadingLogsTest()
		{
			this.TestOnApplicationThread(async () =>
			{
				// prepare source
				var logCount = 256;
				var filePath = this.GenerateLogFile(logCount);
				var options = new LogDataSourceOptions() { FileName = filePath };

				// create source
				if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider) || provider == null)
					throw new AssertionException("Cannot find file log data source provider.");
				using var source = provider.CreateSource(options);

				// create log reader
				using var logReader = new LogReader(source, Task.Factory)
				{
					IsContinuousReading = true,
					LogLevelMap = LogLevelMap,
					LogPatterns = LogPatterns,
					TimestampFormat = TimestampFormat,
				};

				// start reading logs and check pause/resume
				logReader.Start();
				var prevReadLogCount = 0;
				for (var j = 0; j < 10; ++j)
				{
					// read logs
					await Task.Delay(1000);

					// pause
					Assert.IsTrue(logReader.Pause());
					var readLogCount = logReader.Logs.Count;
					if (logReader.State == LogReaderState.Paused)
						Assert.IsTrue(await logReader.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.StartingWhenPaused, -1));
					else
						Assert.IsTrue(await logReader.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.Paused, -1));
					await Task.Delay(1000);
					Assert.AreEqual(readLogCount, logReader.Logs.Count);
					Assert.Greater(readLogCount, prevReadLogCount);
					prevReadLogCount = readLogCount;

					// resume
					Assert.IsTrue(logReader.Resume());
					Assert.IsTrue(logReader.State == LogReaderState.Starting || logReader.State == LogReaderState.ReadingLogs);
				}
			});
		}


		/// <summary>
		/// Test for reading logs from file.
		/// </summary>
		[Test]
		public void ReadingLogsFromFileTest()
		{
			this.TestOnApplicationThread(async () =>
			{
				if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider) || provider == null)
					throw new AssertionException("Cannot find file log data source provider.");
				for (var i = 0; i < 10; ++i)
				{
					// prepare source
					var logCount = 256;
					var filePath = this.GenerateLogFile(logCount);
					var options = new LogDataSourceOptions() { FileName = filePath };

					// create source
					using var source = provider.CreateSource(options);

					// create log reader
					using var logReader1 = new LogReader(source, Task.Factory)
					{
						LogLevelMap = LogLevelMap,
						LogPatterns = LogPatterns,
						TimestampFormat = TimestampFormat,
					};

					// read logs
					logReader1.Start();
					Assert.AreEqual(LogReaderState.Starting, logReader1.State);
					Assert.IsTrue(await logReader1.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.ReadingLogs, 5000));
					Assert.IsTrue(await logReader1.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.Stopped, -1));
					Assert.AreEqual(logCount, logReader1.Logs.Count);

					// try reading logs again
					try
					{
						logReader1.Start();
						throw new AssertionException("Should not allow restart reading logs.");
					}
					catch (Exception ex)
					{
						if (ex is AssertionException)
							throw;
					}

					// dispose log reader
					logReader1.Dispose();
					Assert.AreEqual(LogReaderState.Disposed, logReader1.State);

					// create log reader
					using var logReader2 = new LogReader(source, Task.Factory)
					{
						LogLevelMap = LogLevelMap,
						LogPatterns = new LogPattern[] { 
							new LogPattern(LogHeaderRegex, false, false),
						},
						TimestampFormat = TimestampFormat,
					};

					// read logs
					logReader2.Start();
					Assert.AreEqual(LogReaderState.Starting, logReader2.State);
					Assert.IsTrue(await logReader2.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.ReadingLogs, 5000));
					Assert.IsTrue(await logReader2.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.Stopped, -1));
					Assert.AreEqual(logCount, logReader2.Logs.Count);
				}
			});
		}


		/// <summary>
		/// Test for reading logs from invalid file.
		/// </summary>
		[Test]
		public void ReadingLogsFromInvalidFileTest()
		{
			this.TestOnApplicationThread(async () =>
			{
				// prepare source
				var options = new LogDataSourceOptions() { FileName = "Invalid" };

				// create source
				if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider) || provider == null)
					throw new AssertionException("Cannot find file log data source provider.");
				using var source = provider.CreateSource(options);

				// create log reader
				using var logReader1 = new LogReader(source, Task.Factory)
				{
					LogLevelMap = LogLevelMap,
					LogPatterns = LogPatterns,
					TimestampFormat = TimestampFormat,
				};

				// read logs
				logReader1.Start();
				Assert.AreEqual(LogReaderState.Starting, logReader1.State);
				Assert.IsTrue(await logReader1.WaitForPropertyAsync(nameof(LogReader.State), LogReaderState.DataSourceError, 5000));
				Assert.AreEqual(0, logReader1.Logs.Count);
				logReader1.Dispose();

				// create another log reader
				using var logReader2 = new LogReader(source, Task.Factory)
				{
					LogLevelMap = LogLevelMap,
					LogPatterns = LogPatterns,
					TimestampFormat = TimestampFormat,
				};
				logReader2.Start();
				Assert.AreEqual(LogReaderState.DataSourceError, logReader2.State);
			});
		}
	}
}
