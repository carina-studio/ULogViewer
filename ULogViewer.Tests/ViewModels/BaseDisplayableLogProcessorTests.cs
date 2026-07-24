using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Tests of <see cref="BaseDisplayableLogProcessor{TProcessingToken, TProcessingResult}"/>.
/// </summary>
[TestFixture]
class BaseDisplayableLogProcessorTests : ApplicationBasedTests
{
	// Context to create displayable logs for testing.
	class LogSourceContext : IDisposable
	{
		// Fields.
		readonly string filePath;
		readonly DisplayableLogGroup group;
		readonly LogBuilder logBuilder = new();
		readonly ILogDataSource logDataSource;
		readonly LogReader logReader;

		// Constructor.
		public LogSourceContext(IULogViewerApplication app)
		{
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
				throw new AssertionException("Cannot find file log data source provider.");
			this.filePath = Path.GetTempFileName();
			this.group = new DisplayableLogGroup(new LogProfile(app));
			this.logDataSource = provider.CreateSource(new LogDataSourceOptions { FileName = this.filePath });
			this.logReader = new LogReader(this.group, this.logDataSource);
		}

		// Create given number of displayable logs.
		public IList<DisplayableLog> CreateLogs(int count)
		{
			var logs = new List<DisplayableLog>(count);
			for (var i = 0; i < count; ++i)
			{
				this.logBuilder.Set(nameof(Log.Message), $"Log {i}");
				logs.Add(new DisplayableLog(this.group, this.logReader, this.logBuilder.BuildAndReset()));
			}
			return logs;
		}

		// Dispose context.
		public void Dispose()
		{
			this.logReader.Dispose();
			this.logDataSource.Dispose();
			this.group.Dispose();
			Global.RunWithoutError(() => File.Delete(this.filePath));
		}

		// Group of created displayable logs.
		public DisplayableLogGroup Group => this.group;
	}


	// Token of test processing.
	class TestProcessingToken;


	// Implementation of processor for testing.
	class TestProcessor(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, IDisplayableLogComparer comparer, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Realtime, int maxConcurrencyLevel = 1) : BaseDisplayableLogProcessor<TestProcessingToken, long>(app, sourceLogs, comparer, priority)
	{
		/// <summary>
		/// Size of processing chunk.
		/// </summary>
		public const int TestChunkSize = 64;
		
		// Fields.
		int processLogCallCount;
		
		// All attached log groups in order of receiving by OnAttachToLogGroup().
		public List<DisplayableLogGroup> AttachedLogGroups { get; } = [];

		// All attached log profiles in order of receiving by OnAttachToLogProfile().
		public List<LogProfile> AttachedLogProfiles { get; } = [];

		// Records of processing cancellation in order of receiving by OnProcessingCancelled().
		public List<(TestProcessingToken Token, bool WillStartProcessing)> CancellationRecords { get; } = [];

		// Chunk size for testing.
		protected override int ChunkSize => TestChunkSize;

		// All created processing tokens in order of creation.
		public List<TestProcessingToken> CreatedTokens { get; } = [];

		// Create processing token.
		protected override TestProcessingToken CreateProcessingToken(out bool isProcessingNeeded)
		{
			isProcessingNeeded = this.IsProcessingNeededWhenCreatingToken;
			return new TestProcessingToken().Also(it => this.CreatedTokens.Add(it));
		}

		// All detached log groups in order of receiving by OnDetachFromLogGroup().
		public List<DisplayableLogGroup> DetachedLogGroups { get; } = [];

		// All detached log profiles in order of receiving by OnDetachFromLogProfile().
		public List<LogProfile> DetachedLogProfiles { get; } = [];

		// Records of chunk processing failure in order of receiving by OnChunkProcessingFailed().
		public List<(TestProcessingToken Token, Exception Exception)> FailureRecords { get; } = [];

		// All invalidated logs in order of receiving by OnLogInvalidated().
		public List<DisplayableLog> InvalidatedLogs { get; } = [];

		// Invalidate current processing and start new processing later.
		public new void InvalidateProcessing() =>
			base.InvalidateProcessing();

		// Whether processing is needed when creating token or not.
		public bool IsProcessingNeededWhenCreatingToken { get; set; } = true;

		// Handler to handle invalidation of log. Returns whether invalidation is handled directly or not.
		public Func<DisplayableLog, bool>? LogInvalidatedHandler { get; set; }

		// Max concurrency level for testing.
		protected override int MaxConcurrencyLevel => maxConcurrencyLevel;
		
		// Handle attaching to log group.
		protected override void OnAttachToLogGroup(DisplayableLogGroup group)
		{
			base.OnAttachToLogGroup(group);
			this.AttachedLogGroups.Add(group);
		}

		// Handle attaching to log profile.
		protected override void OnAttachToLogProfile(LogProfile profile)
		{
			base.OnAttachToLogProfile(profile);
			this.AttachedLogProfiles.Add(profile);
		}

		// Handle chunk of processed logs.
		protected override void OnChunkProcessed(TestProcessingToken token, List<DisplayableLog> logs, List<long> results)
		{
			this.ProcessedChunks.Add([ ..logs ]);
			this.ProcessedLogs.AddRange(logs);
			this.ProcessingResults.AddRange(results);
		}
		
		// Handle failure of chunk processing.
		protected override void OnChunkProcessingFailed(TestProcessingToken token, Exception exception)
		{
			this.FailureRecords.Add((token, exception));
			base.OnChunkProcessingFailed(token, exception);
		}

		// Handle detaching from log group.
		protected override void OnDetachFromLogGroup(DisplayableLogGroup group)
		{
			this.DetachedLogGroups.Add(group);
			base.OnDetachFromLogGroup(group);
		}

		// Handle detaching from log profile.
		protected override void OnDetachFromLogProfile(LogProfile profile)
		{
			this.DetachedLogProfiles.Add(profile);
			base.OnDetachFromLogProfile(profile);
		}

		// Handle invalidation of log.
		protected override bool OnLogInvalidated(DisplayableLog log)
		{
			this.InvalidatedLogs.Add(log);
			return this.LogInvalidatedHandler?.Invoke(log) ?? false;
		}

		// Handle cancellation of processing.
		protected override void OnProcessingCancelled(TestProcessingToken token, bool willStartProcessing) =>
			this.CancellationRecords.Add((token, willStartProcessing));
		
		// Process single log.
		protected override bool OnProcessLog(TestProcessingToken token, DisplayableLog log, out long result)
		{
			Interlocked.Increment(ref this.processLogCallCount);
			var handler = this.ProcessLogHandler;
			if (handler is null)
			{
				result = log.LogId;
				return true;
			}
			var (isAccepted, handledResult) = handler(log);
			result = handledResult;
			return isAccepted;
		}
		
		// Logs of each processed chunk in order of receiving by OnChunkProcessed().
		public List<List<DisplayableLog>> ProcessedChunks { get; } = [];
		
		// All processed logs in order of receiving by OnChunkProcessed().
		public List<DisplayableLog> ProcessedLogs { get; } = [];
		
		// All collected results in order of receiving by OnChunkProcessed().
		public List<long> ProcessingResults { get; } = [];
		
		// Number of calls to OnProcessLog().
		public int ProcessLogCallCount => this.processLogCallCount;
		
		// Handler to process single log. Returns whether result should be collected and the result.
		public Func<DisplayableLog, (bool, long)>? ProcessLogHandler { get; set; }
	}


	// Static fields.
	static readonly IDisplayableLogComparer ascendingComparer = new DisplayableLogComparer((lhs, rhs) => lhs.LogId.CompareTo(rhs.LogId), SortDirection.Ascending);
	static readonly IDisplayableLogComparer descendingComparer = new DisplayableLogComparer((lhs, rhs) => rhs.LogId.CompareTo(lhs.LogId), SortDirection.Descending);


	/// <summary>
	/// Test for processing logs in chunks with concurrency.
	/// </summary>
	[Test]
	public void ChunkedProcessingWithConcurrencyTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 10);

			// create processor which slows down processing to encourage overlapping chunks
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer, DisplayableLogProcessingPriority.Realtime, BaseDisplayableLogProcessors.MaxProcessingConcurrencyLevelRealtime).Setup(it =>
			{
				it.ProcessLogHandler = log =>
				{
					Thread.Sleep(1);
					return (true, log.LogId);
				};
			});

			// add logs and wait for completion
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs", 30000);

			// verify that logs were processed in multiple chunks
			Assert.That(processor.ProcessedChunks.Count, Is.GreaterThanOrEqualTo(logs.Count / TestProcessor.TestChunkSize));
			foreach (var chunk in processor.ProcessedChunks)
				Assert.That(chunk.Count, Is.LessThanOrEqualTo(TestProcessor.TestChunkSize));

			// verify that chunks were committed in order of source logs
			Assert.That(processor.ProcessedLogs, Is.EqualTo(logs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(logs.Select(it => it.LogId)));
		});
	}


	/// <summary>
	/// Test for disposing processor while processing logs.
	/// </summary>
	[Test]
	public void DisposalTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor which blocks processing on first log
			var firstLog = logs[0];
			using var blockEvent = new ManualResetEventSlim();
			using var reachedEvent = new ManualResetEventSlim();
			ObservableList<DisplayableLog> sourceLogs = [];
			var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.ProcessLogHandler = log =>
				{
					if (log == firstLog)
					{
						reachedEvent.Set();
						blockEvent.Wait(10000);
					}
					return (true, log.LogId);
				};
			});
			try
			{
				// add logs and wait until first chunk is being processed
				sourceLogs.AddRange(logs);
				await WaitForConditionAsync(() => reachedEvent.IsSet, "processing first chunk");
			}
			finally
			{
				processor.Dispose();
			}

			// verify that processing was cancelled
			Assert.That(processor.IsProcessing, Is.False);
			Assert.That(processor.CancellationRecords, Is.EqualTo([ (processor.CreatedTokens[0], false) ]));

			// continue processing and wait for a while
			blockEvent.Set();
			await Task.Delay(500);

			// verify that no chunk was committed and no more logs were processed
			Assert.That(processor.ProcessedLogs, Is.Empty);
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(1));

			// verify that invalidation is not allowed after disposal
			Assert.Throws<ObjectDisposedException>(() => processor.InvalidateLog(firstLog));
		});
	}


	/// <summary>
	/// Test for reporting fault of processing.
	/// </summary>
	[Test]
	public void FaultReportingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor which throws exception on specific log
			var error = new Exception("Test error.");
			var errorLog = logs[10];
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.ProcessLogHandler = log =>
				{
					if (log == errorLog)
						throw error;
					return (true, log.LogId);
				};
			});

			// add logs and wait for fault
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => processor.IsFaulted, "reporting fault");
			await WaitForConditionAsync(() => !processor.IsProcessing, "completing processing");

			// verify that fault was reported with thrown exception
			Assert.That(processor.Exception, Is.SameAs(error));
			Assert.That(processor.FailureRecords, Is.EqualTo([ (processor.CreatedTokens[0], error) ]));

			// restart processing without error and wait for completion
			var processedLogCount = processor.ProcessedLogs.Count;
			processor.ProcessLogHandler = null;
			processor.InvalidateProcessing();
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= processedLogCount + logs.Count, "re-processing all logs");

			// verify that fault was cleared
			Assert.That(processor.IsFaulted, Is.False);
			Assert.That(processor.Exception, Is.Null);
		});
	}


	/// <summary>
	/// Test for processing logs which are added to source list incrementally.
	/// </summary>
	[Test]
	public void IncrementalSourceLogAdditionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 5);

			// create processor
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);

			// add logs in batches and wait for completion
			var batchSize = TestProcessor.TestChunkSize / 2;
			for (var i = 0; i < logs.Count; i += batchSize)
			{
				sourceLogs.AddRange(logs.Skip(i).Take(batchSize));
				await Task.Delay(10);
			}
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// verify
			Assert.That(processor.Progress, Is.EqualTo(1));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count));
			Assert.That(processor.ProcessedLogs, Is.EqualTo(logs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(logs.Select(it => it.LogId)));
		});
	}


	/// <summary>
	/// Test for handling invalidation of log directly without re-processing.
	/// </summary>
	[Test]
	public void InvalidateLogHandledDirectlyTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize);

			// create processor which handles invalidation directly and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.LogInvalidatedHandler = _ => true;
			});
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// invalidate single log and wait for a while
			var invalidatedLog = logs[10];
			processor.InvalidateLog(invalidatedLog);
			await Task.Delay(500);

			// verify that invalidation was received but log was not processed again
			Assert.That(processor.InvalidatedLogs, Is.EqualTo([ invalidatedLog ]));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count));
			Assert.That(processor.ProcessedLogs.Count, Is.EqualTo(logs.Count));
		});
	}


	/// <summary>
	/// Test for invalidating multiple logs.
	/// </summary>
	[Test]
	public void InvalidateLogsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize);

			// create processor and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// invalidate multiple logs and wait for re-processing
			var invalidatedLogs = logs.Skip(8).Take(16).ToList();
			processor.InvalidateLogs(invalidatedLogs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count + invalidatedLogs.Count, "re-processing invalidated logs");

			// verify that only the invalidated logs were processed again
			Assert.That(processor.InvalidatedLogs, Is.EqualTo(invalidatedLogs));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count + invalidatedLogs.Count));
			Assert.That(processor.ProcessedLogs.Skip(logs.Count), Is.EqualTo(invalidatedLogs));
		});
	}


	/// <summary>
	/// Test for invalidating single log.
	/// </summary>
	[Test]
	public void InvalidateLogTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize);

			// create processor and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// invalidate single log and wait for re-processing
			var invalidatedLog = logs[10];
			processor.InvalidateLog(invalidatedLog);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count + 1, "re-processing invalidated log");

			// verify that only the invalidated log was processed again
			Assert.That(processor.InvalidatedLogs, Is.EqualTo([ invalidatedLog ]));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count + 1));
			Assert.That(processor.ProcessedLogs[^1], Is.SameAs(invalidatedLog));

			// invalidate log which is not contained in source list and wait for a while
			var foreignLog = context.CreateLogs(1)[0];
			processor.InvalidateLog(foreignLog);
			await Task.Delay(500);

			// verify that nothing was processed
			Assert.That(processor.InvalidatedLogs.Count, Is.EqualTo(1));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count + 1));
		});
	}


	/// <summary>
	/// Test for invalidating whole processing.
	/// </summary>
	[Test]
	public void InvalidateProcessingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");
			Assert.That(processor.CreatedTokens.Count, Is.EqualTo(1));

			// invalidate processing and wait for re-processing all logs
			processor.InvalidateProcessing();
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count * 2, "re-processing all logs");

			// verify that current processing was cancelled and all logs were processed again with new token
			Assert.That(processor.CreatedTokens.Count, Is.EqualTo(2));
			Assert.That(processor.CancellationRecords, Is.EqualTo([ (processor.CreatedTokens[0], true) ]));
			Assert.That(processor.ProcessedLogs.Skip(logs.Count), Is.EqualTo(logs));
		});
	}


	/// <summary>
	/// Test for attaching to and detaching from log group and profile.
	/// </summary>
	[Test]
	public void LogGroupAndProfileAttachmentTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize);

			// create processor and wait for a while
			ObservableList<DisplayableLog> sourceLogs = [];
			var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			try
			{
				await Task.Delay(500);

				// verify that nothing was attached before processing logs
				Assert.That(processor.AttachedLogGroups, Is.Empty);
				Assert.That(processor.AttachedLogProfiles, Is.Empty);

				// add logs and wait for completion
				sourceLogs.AddRange(logs);
				await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

				// verify that group and profile of logs were attached
				Assert.That(processor.AttachedLogGroups, Is.EqualTo([ context.Group ]));
				Assert.That(processor.AttachedLogProfiles, Is.EqualTo([ context.Group.LogProfile ]));
				Assert.That(processor.DetachedLogGroups, Is.Empty);
				Assert.That(processor.DetachedLogProfiles, Is.Empty);
			}
			finally
			{
				processor.Dispose();
			}

			// verify that group and profile were detached after disposal
			Assert.That(processor.DetachedLogGroups, Is.EqualTo([ context.Group ]));
			Assert.That(processor.DetachedLogProfiles, Is.EqualTo([ context.Group.LogProfile ]));
		});
	}


	/// <summary>
	/// Test for tolerating error occurred while processing log.
	/// </summary>
	[Test]
	public void ProcessingErrorToleranceTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor which throws exception on specific log
			var errorLog = logs[10];
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.ProcessLogHandler = log =>
				{
					if (log == errorLog)
						throw new Exception("Test error.");
					return (true, log.LogId);
				};
			});

			// add logs and wait for completion
			sourceLogs.AddRange(logs);
			var expectedLogs = logs.Take(10).Concat(logs.Skip(TestProcessor.TestChunkSize)).ToList();
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= expectedLogs.Count, "processing all logs");

			// verify that remaining logs in the chunk were dropped and other chunks were processed normally
			Assert.That(processor.ProcessedLogs, Is.EqualTo(expectedLogs));

			// invalidate log and wait for re-processing
			processor.InvalidateLog(logs[0]);
			await WaitForConditionAsync(() => processor.ProcessedLogs.Count >= expectedLogs.Count + 1, "re-processing invalidated log");

			// verify that processor is still functional
			Assert.That(processor.ProcessedLogs[^1], Is.SameAs(logs[0]));
		});
	}


	/// <summary>
	/// Test for skipping processing when processing is not needed.
	/// </summary>
	[Test]
	public void ProcessingNotNeededTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize);

			// create processor which reports that processing is not needed
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.IsProcessingNeededWhenCreatingToken = false;
			});

			// add logs and wait for a while
			sourceLogs.AddRange(logs);
			await Task.Delay(500);

			// verify that no logs were processed
			Assert.That(processor.IsProcessingNeeded, Is.False);
			Assert.That(processor.IsProcessing, Is.False);
			Assert.That(processor.Progress, Is.Zero);
			Assert.That(processor.ProcessLogCallCount, Is.Zero);
			Assert.That(processor.ProcessedLogs, Is.Empty);
		});
	}


	/// <summary>
	/// Test for processing logs which are added to source list after construction.
	/// </summary>
	[Test]
	public void ProcessLogsAddedAfterConstructionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 5);

			// create processor
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);

			// add logs and wait for completion
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// verify
			Assert.That(processor.IsProcessingNeeded, Is.True);
			Assert.That(processor.Progress, Is.EqualTo(1));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count));
			Assert.That(processor.ProcessedLogs, Is.EqualTo(logs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(logs.Select(it => it.LogId)));
		});
	}


	/// <summary>
	/// Test for processing logs which are already contained in source list when constructing processor.
	/// </summary>
	[Test]
	public void ProcessLogsProvidedAtConstructionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 5);

			// create processor with non-empty source list and wait for completion
			using var processor = new TestProcessor(this.Application, logs, ascendingComparer);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// verify
			Assert.That(processor.IsProcessingNeeded, Is.True);
			Assert.That(processor.Progress, Is.EqualTo(1));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count));
			Assert.That(processor.ProcessedLogs, Is.EqualTo(logs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(logs.Select(it => it.LogId)));
		});
	}


	/// <summary>
	/// Test for processing logs with descending comparer.
	/// </summary>
	[Test]
	public void ProcessLogsWithDescendingComparerTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs in descending order
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 5).Reverse().ToList();

			// create processor
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, descendingComparer);

			// add logs and wait for completion
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// verify that chunks were committed from tail of source list to keep processing in chronological order
			Assert.That(processor.Progress, Is.EqualTo(1));
			Assert.That(Enumerable.Reverse(processor.ProcessedChunks).SelectMany(it => it), Is.EqualTo(logs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(processor.ProcessedLogs.Select(it => it.LogId)));
		});
	}


	/// <summary>
	/// Test for resetting source list.
	/// </summary>
	[Test]
	public void ResetSourceLogsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");
			Assert.That(processor.AttachedLogGroups, Is.EqualTo([ context.Group ]));
			Assert.That(processor.AttachedLogProfiles, Is.EqualTo([ context.Group.LogProfile ]));

			// clear source list and wait for restarting processing
			sourceLogs.Clear();
			await WaitForConditionAsync(() => processor.CreatedTokens.Count >= 2, "restarting processing");

			// verify that processing was cancelled and detached from log group and profile
			Assert.That(processor.CancellationRecords, Is.EqualTo([ (processor.CreatedTokens[0], true) ]));
			Assert.That(processor.DetachedLogGroups, Is.EqualTo([ context.Group ]));
			Assert.That(processor.DetachedLogProfiles, Is.EqualTo([ context.Group.LogProfile ]));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count));
			Assert.That(processor.ProcessedLogs.Count, Is.EqualTo(logs.Count));
		});
	}


	/// <summary>
	/// Test for collecting results of accepted logs only.
	/// </summary>
	[Test]
	public void SelectiveResultCollectionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 5);
			var expectedLogs = logs.Where(it => (it.LogId & 1) == 0).ToList();

			// create processor which accepts logs with even ID only
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.ProcessLogHandler = log => ((log.LogId & 1) == 0, log.LogId);
			});

			// add logs and wait for completion
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= expectedLogs.Count, "processing all logs");

			// verify that results of accepted logs were collected only
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count));
			Assert.That(processor.ProcessedLogs, Is.EqualTo(expectedLogs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(expectedLogs.Select(it => it.LogId)));
		});
	}


	/// <summary>
	/// Test for changing comparer of source logs.
	/// </summary>
	[Test]
	public void SourceLogComparerChangeTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// change comparer with descending sorted logs and wait for re-processing
			var descendingLogs = logs.Reverse().ToList();
			var processedChunkCount = processor.ProcessedChunks.Count;
			sourceLogs.Clear();
			processor.SourceLogComparer = descendingComparer;
			sourceLogs.AddRange(descendingLogs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count * 2, "re-processing all logs");

			// verify that logs were processed with new sorting direction
			var chunks = processor.ProcessedChunks.Skip(processedChunkCount).ToList();
			Assert.That(Enumerable.Reverse(chunks).SelectMany(it => it), Is.EqualTo(descendingLogs));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count * 2));
		});
	}


	/// <summary>
	/// Test for removing logs from source list which are not yet processed.
	/// </summary>
	[Test]
	public void SourceLogRemovalTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor which blocks processing on first log
			var firstLog = logs[0];
			using var blockEvent = new ManualResetEventSlim();
			using var reachedEvent = new ManualResetEventSlim();
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.ProcessLogHandler = log =>
				{
					if (log == firstLog)
					{
						reachedEvent.Set();
						blockEvent.Wait(10000);
					}
					return (true, log.LogId);
				};
			});

			// add logs and wait until first chunk is being processed
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => reachedEvent.IsSet, "processing first chunk");

			// remove logs which are not yet processed then continue processing
			sourceLogs.RemoveRange(TestProcessor.TestChunkSize, TestProcessor.TestChunkSize);
			blockEvent.Set();
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= TestProcessor.TestChunkSize, "processing remaining logs");

			// verify that removed logs were not processed
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(TestProcessor.TestChunkSize));
			Assert.That(processor.ProcessedLogs, Is.EqualTo(logs.Take(TestProcessor.TestChunkSize)));
		});
	}


	/// <summary>
	/// Test for replacing source list.
	/// </summary>
	[Test]
	public void SourceLogsReplacementTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize);

			// create processor and wait for completing initial processing
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer);
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// replace source list and wait for processing new logs
			var newLogs = context.CreateLogs(TestProcessor.TestChunkSize);
			ObservableList<DisplayableLog> newSourceLogs = [ ..newLogs ];
			processor.SourceLogs = newSourceLogs;
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count + newLogs.Count, "processing new logs");

			// verify that new logs were processed
			Assert.That(processor.CancellationRecords, Is.EqualTo([ (processor.CreatedTokens[0], true) ]));
			Assert.That(processor.ProcessedLogs.Skip(logs.Count), Is.EqualTo(newLogs));

			// add logs to old source list and wait for a while
			sourceLogs.AddRange(context.CreateLogs(TestProcessor.TestChunkSize));
			await Task.Delay(500);

			// verify that logs in old source list were not processed
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count + newLogs.Count));
		});
	}


	/// <summary>
	/// Test for dropping stale result of log which was invalidated while its chunk is being processed.
	/// </summary>
	[Test]
	public void StaleResultDroppingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new LogSourceContext(this.Application);
			var logs = context.CreateLogs(TestProcessor.TestChunkSize * 2);

			// create processor which blocks processing on first log
			var targetLog = logs[0];
			using var blockEvent = new ManualResetEventSlim();
			using var reachedEvent = new ManualResetEventSlim();
			var isBlocked = 0;
			ObservableList<DisplayableLog> sourceLogs = [];
			using var processor = new TestProcessor(this.Application, sourceLogs, ascendingComparer).Setup(it =>
			{
				it.ProcessLogHandler = log =>
				{
					if (log == targetLog && Interlocked.Exchange(ref isBlocked, 1) == 0)
					{
						reachedEvent.Set();
						blockEvent.Wait(10000);
					}
					return (true, log.LogId);
				};
			});

			// add logs and wait until first chunk is being processed
			sourceLogs.AddRange(logs);
			await WaitForConditionAsync(() => reachedEvent.IsSet, "processing first chunk");

			// invalidate log which is being processed then continue processing
			processor.InvalidateLog(targetLog);
			blockEvent.Set();
			await WaitForConditionAsync(() => !processor.IsProcessing && processor.ProcessedLogs.Count >= logs.Count, "processing all logs");

			// verify that stale result was dropped and log was processed again exactly once
			Assert.That(processor.ProcessedChunks[0], Does.Not.Contain(targetLog));
			Assert.That(processor.ProcessLogCallCount, Is.EqualTo(logs.Count + 1));
			Assert.That(processor.ProcessedLogs, Is.EquivalentTo(logs));
			Assert.That(processor.ProcessingResults, Is.EqualTo(processor.ProcessedLogs.Select(it => it.LogId)));
		});
	}


	// Wait for given condition asynchronously.
	static async Task WaitForConditionAsync(Func<bool> condition, string description, int timeoutMillis = 10000)
	{
		var stopwatch = Stopwatch.StartNew();
		while (!condition())
		{
			if (stopwatch.ElapsedMilliseconds >= timeoutMillis)
				throw new AssertionException($"Timeout before {description}.");
			await Task.Delay(50);
		}
	}
}
