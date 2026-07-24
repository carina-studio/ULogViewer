using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Tests of <see cref="DisplayableLogGroup"/>.
/// </summary>
[TestFixture]
class DisplayableLogGroupTests : ApplicationBasedTests
{
	// Static fields.
	static readonly IDisplayableLogComparer comparer = new DisplayableLogComparer((lhs, rhs) => lhs.LogId.CompareTo(rhs.LogId), SortDirection.Ascending);


	/// <summary>
	/// Test for brushes and tips of color indicator.
	/// </summary>
	[Test]
	public void ColorIndicatorTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check without color indicator
			using var context = new DisplayableLogTestContext(this.Application);
			var log = context.CreateLog(it => it.Set(nameof(Log.ProcessId), "123"));
			Assert.That(log.ColorIndicatorBrush, Is.Null);
			Assert.That(log.ColorIndicatorTip, Is.Null);

			// change to process ID indicator
			var eventCount = 0;
			context.Group.ColorIndicatorBrushesUpdated += (_, _) => ++eventCount;
			context.Profile.ColorIndicator = LogColorIndicator.ProcessId;
			Assert.That(eventCount, Is.EqualTo(1));
			var brush = log.ColorIndicatorBrush;
			Assert.That(brush, Is.Not.Null);
			Assert.That(log.ColorIndicatorTip, Is.EqualTo("123"));

			// check brush caching by key
			var sameKeyLog = context.CreateLog(it => it.Set(nameof(Log.ProcessId), "123"));
			Assert.That(sameKeyLog.ColorIndicatorBrush, Is.SameAs(brush));
			var otherKeyLog = context.CreateLog(it => it.Set(nameof(Log.ProcessId), "456"));
			Assert.That(otherKeyLog.ColorIndicatorBrush, Is.Not.SameAs(brush));

			// check overload for file name
			Assert.That(context.Group.GetColorIndicatorBrush("file.log"), Is.Null);
			context.Profile.ColorIndicator = LogColorIndicator.FileName;
			Assert.That(eventCount, Is.EqualTo(2));
			Assert.That(context.Group.GetColorIndicatorBrush("file.log"), Is.Not.Null);

			// check disposed group
			var disposedContext = new DisplayableLogTestContext(this.Application, profile => profile.ColorIndicator = LogColorIndicator.FileName);
			var disposedGroup = disposedContext.Group;
			Assert.That(disposedGroup.GetColorIndicatorBrush("file.log"), Is.Not.Null);
			disposedContext.Dispose();
			Assert.That(disposedGroup.GetColorIndicatorBrush("file.log"), Is.Null);
		});
	}


	/// <summary>
	/// Test for identity and lifecycle of group.
	/// </summary>
	[Test]
	public void IdentityAndLifecycleTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check unique IDs
			using var context1 = new DisplayableLogTestContext(this.Application);
			using var context2 = new DisplayableLogTestContext(this.Application);
			Assert.That(context1.Group.Id, Is.Not.EqualTo(context2.Group.Id));

			// find instance by ID
			Assert.That(DisplayableLogGroup.TryGetInstanceById(context1.Group.Id, out var found), Is.True);
			Assert.That(found, Is.SameAs(context1.Group));

			// dispose group to unregister
			var context3 = new DisplayableLogTestContext(this.Application);
			var group3 = context3.Group;
			var reader3 = context3.DefaultLogReader;
			var rawLog = new LogBuilder().Let(it =>
			{
				it.Set(nameof(Log.Message), "Test");
				return it.BuildAndReset();
			});
			context3.Dispose();
			Assert.That(DisplayableLogGroup.TryGetInstanceById(group3.Id, out _), Is.False);
			Assert.Throws<ObjectDisposedException>(() => _ = group3.CreateDisplayableLog(reader3, rawLog));
		});
	}


	/// <summary>
	/// Test for showing raw log lines temporarily.
	/// </summary>
	[Test]
	public void IsShowingRawLogsTemporarilyTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log with color indicator
			using var context = new DisplayableLogTestContext(this.Application, profile => profile.ColorIndicator = LogColorIndicator.ProcessId);
			var log = context.CreateLog(it => it.Set(nameof(Log.ProcessId), "123"));
			Assert.That(log.ColorIndicatorBrush, Is.Not.Null);

			// enter raw log lines mode
			context.Group.IsShowingRawLogsTemporarily = true;
			Assert.That(log.ColorIndicatorBrush, Is.Null);

			// leave raw log lines mode
			context.Group.IsShowingRawLogsTemporarily = false;
			Assert.That(log.ColorIndicatorBrush, Is.Not.Null, "Color indicator should be restored after leaving raw log lines mode.");
		});
	}


	/// <summary>
	/// Test for map of converting log level to string for displaying.
	/// </summary>
	[Test]
	public void LevelMapForDisplayingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// check initial map merged from reading and writing maps
			using var context = new DisplayableLogTestContext(this.Application, profile =>
			{
				profile.LogLevelMapForReading = new Dictionary<string, LogLevel> { { "E", LogLevel.Error }, { "W", LogLevel.Warn } };
				profile.LogLevelMapForWriting = new Dictionary<LogLevel, string> { { LogLevel.Error, "ERROR!" } };
			});
			var map = context.Group.LevelMapForDisplaying;
			Assert.That(map[LogLevel.Error], Is.EqualTo("ERROR!"), "Writing map should override reading map.");
			Assert.That(map[LogLevel.Warn], Is.EqualTo("W"), "Reading map should be inverted into displaying map.");

			// change writing map to update displaying map
			var log = context.CreateLog(it => it.Set(nameof(Log.Level), nameof(LogLevel.Warn)));
			List<string> notified = [];
			log.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");
			context.Profile.LogLevelMapForWriting = new Dictionary<LogLevel, string> { { LogLevel.Warn, "WARNING!" } };
			await WaitForConditionAsync(() => context.Group.LevelMapForDisplaying.TryGetValue(LogLevel.Warn, out var s) && s == "WARNING!", "level map updated");
			Assert.That(notified, Does.Contain(nameof(DisplayableLog.LevelString)));
			Assert.That(log.LevelString, Is.EqualTo("WARNING!"));
		});
	}


	/// <summary>
	/// Test for detection of Extra* log properties from log patterns.
	/// </summary>
	[Test]
	public void LogExtrasDetectionTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check profile without patterns
			using var context = new DisplayableLogTestContext(this.Application);
			Assert.That(context.Group.LogExtraNumbers, Is.Empty);
			Assert.That(context.Group.LogExtraNumberCount, Is.Zero);

			// change patterns with duplicate and out-of-range extras
			context.Profile.LogPatterns =
			[
				new LogPattern("(?<Extra5>.*) (?<Extra1>.*)", false, false, null),
				new LogPattern("(?<Extra5>.*) (?<Extra21>.*) (?<Extra0>.*)", false, false, null),
			];
			Assert.That(context.Group.LogExtraNumbers, Is.EqualTo([ 1, 5 ]));
			Assert.That(context.Group.LogExtraNumberCount, Is.EqualTo(2));
		});
	}


	/// <summary>
	/// Test for selecting task factory for logs reading.
	/// </summary>
	[Test]
	public void LogsReadingTaskFactoryTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare sources
			using var context = new DisplayableLogTestContext(this.Application);
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var fileProvider))
				throw new AssertionException("Cannot find file log data source provider.");
			if (!LogDataSourceProviders.TryFindProviderByName("MemoryLogger", out var memoryLoggerProvider))
				throw new AssertionException("Cannot find memory logger log data source provider.");
			var filePath = Path.GetTempFileName();
			try
			{
				// select factory for file source
				using var fileSource = fileProvider.CreateSource(new LogDataSourceOptions { FileName = filePath });
				var fileFactory = context.Group.SelectLogsReadingTaskFactory(fileSource);
				Assert.That(context.Group.SelectLogsReadingTaskFactory(fileSource), Is.SameAs(fileFactory));

				// select factory for non-file source
				using var memoryLoggerSource = memoryLoggerProvider.CreateSource(new LogDataSourceOptions());
				var defaultFactory = context.Group.SelectLogsReadingTaskFactory(memoryLoggerSource);
				Assert.That(defaultFactory, Is.Not.SameAs(fileFactory));
				Assert.That(context.Group.SelectLogsReadingTaskFactory(memoryLoggerSource), Is.SameAs(defaultFactory));
			}
			finally
			{
				Global.RunWithoutError(() => File.Delete(filePath));
			}
		});
	}


	/// <summary>
	/// Test for scheduling progressive logs removing.
	/// </summary>
	[Test]
	public void ProgressiveLogsRemovingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// schedule two removings
			using var context = new DisplayableLogTestContext(this.Application);
			var group = (ILogGroup)context.Group;
			var triggeredCount1 = 0;
			var triggeredCount2 = 0;
			var token1 = group.ScheduleProgressiveLogsRemoving(() =>
			{
				++triggeredCount1;
				return true;
			});
			var token2 = group.ScheduleProgressiveLogsRemoving(() =>
			{
				++triggeredCount2;
				return true;
			});

			// check first removing triggered only
			await WaitForConditionAsync(() => triggeredCount1 == 1, "first removing triggered");
			Assert.That(triggeredCount2, Is.Zero);

			// complete first removing to trigger second one
			token1.Dispose();
			await WaitForConditionAsync(() => triggeredCount2 == 1, "second removing triggered");
			token2.Dispose();

			// schedule removing which cannot be triggered
			var triggeredCount3 = 0;
			var triggeredCount4 = 0;
			var token3 = group.ScheduleProgressiveLogsRemoving(() =>
			{
				++triggeredCount3;
				return false;
			});
			var token4 = group.ScheduleProgressiveLogsRemoving(() =>
			{
				++triggeredCount4;
				return true;
			});

			// check untriggerable removing dropped
			await WaitForConditionAsync(() => triggeredCount4 == 1, "fourth removing triggered");
			Assert.That(triggeredCount3, Is.EqualTo(1));
			token4.Dispose();
			token3.Dispose();
		});
	}


	/// <summary>
	/// Test for management of log readers and memory usage.
	/// </summary>
	[Test]
	public void ReaderManagementAndMemoryTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare readers
			using var context = new DisplayableLogTestContext(this.Application);
			var group = context.Group;
			var reader1 = context.DefaultLogReader;
			var reader2 = context.CreateLogReader();
			var baselineMemorySize = group.MemorySize;

			// create logs through two readers
			var log1 = context.CreateLog(it => it.Set(nameof(Log.Message), "1"));
			var log2 = context.CreateLog(reader2, it => it.Set(nameof(Log.Message), "2"));
			Assert.That(log1.LogReader, Is.SameAs(reader1));
			Assert.That(log2.LogReader, Is.SameAs(reader2));
			Assert.That(log1.LogReaderLocalId, Is.Not.EqualTo(log2.LogReaderLocalId));

			// look up reader by local ID
			Assert.That(group.TryGetLogReaderByLocalId(log1.LogReaderLocalId, out var foundReader), Is.True);
			Assert.That(foundReader, Is.SameAs(reader1));

			// check memory usage growth
			Assert.That(group.MemorySize, Is.GreaterThan(baselineMemorySize));

			// dispose last log of reader to release reader
			var localId2 = log2.LogReaderLocalId;
			log2.Dispose();
			Assert.That(group.TryGetLogReaderByLocalId(localId2, out _), Is.False);

			// dispose remaining log to restore memory usage
			log1.Dispose();
			Assert.That(group.MemorySize, Is.EqualTo(baselineMemorySize));
		});
	}


	/// <summary>
	/// Test for removing analysis results from all logs in group.
	/// </summary>
	[Test]
	public void RemoveAnalysisResultsTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare logs with results from two analyzers
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			using var analyzerA = new DummyDisplayableLogAnalyzer(this.Application, sourceLogs, comparer);
			using var analyzerB = new DummyDisplayableLogAnalyzer(this.Application, sourceLogs, comparer);
			var log1 = context.CreateLog();
			var log2 = context.CreateLog();
			var resultA1 = new DisplayableLogAnalysisResult(analyzerA, DisplayableLogAnalysisResultType.Warning, log1);
			var resultA2 = new DisplayableLogAnalysisResult(analyzerA, DisplayableLogAnalysisResultType.Warning, log2);
			var resultB1 = new DisplayableLogAnalysisResult(analyzerB, DisplayableLogAnalysisResultType.Information, log1);
			log1.AddAnalysisResult(resultA1);
			log1.AddAnalysisResult(resultB1);
			log2.AddAnalysisResult(resultA2);

			// remove results of first analyzer from all logs
			context.Group.RemoveAnalysisResults(analyzerA);
			Assert.That(log1.AnalysisResults, Is.EqualTo([ resultB1 ]));
			Assert.That(log2.AnalysisResults, Is.Empty);
			Assert.That(log2.HasAnalysisResult, Is.False);
		});
	}


	/// <summary>
	/// Test for building text highlighting definitions from text filters.
	/// </summary>
	[Test]
	public void TextHighlightingDefinitionsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// check initial state
			using var context = new DisplayableLogTestContext(this.Application);
			var definitions = context.Group.TextHighlightingDefinitionSet.TokenDefinitions;
			Assert.That(definitions, Is.Empty);

			// apply single filter
			context.Group.ActiveInclusiveTextFilters = [ new Regex("abc") ];
			await WaitForConditionAsync(definitions.IsNotEmpty, "definitions built for single filter");
			Assert.That(definitions.Count, Is.EqualTo(1));
			Assert.That(definitions[0].Pattern?.ToString(), Is.EqualTo("abc"));

			// apply cross-property filter
			context.Group.ActiveInclusiveTextFilters = [ new Regex(@"carina\$\$.+hello") ];
			await WaitForConditionAsync(() => definitions.Count > 1, "definitions built for cross-property filter");
			var patterns = definitions.Select(it => it.Pattern?.ToString()).ToList();
			Assert.That(patterns, Does.Contain("carina"));
			Assert.That(patterns, Does.Contain("hello"));

			// apply all-matching filter
			context.Group.ActiveInclusiveTextFilters = [ new Regex(".*") ];
			await WaitForConditionAsync(definitions.IsEmpty, "definitions cleared for all-matching filter");

			// clear filters
			context.Group.ActiveInclusiveTextFilters = [ new Regex("abc") ];
			await WaitForConditionAsync(definitions.IsNotEmpty, "definitions built again");
			context.Group.ActiveInclusiveTextFilters = [];
			await WaitForConditionAsync(definitions.IsEmpty, "definitions cleared");
		});
	}


	// Wait for given condition to be satisfied.
	static async Task WaitForConditionAsync(Func<bool> condition, string description, int timeoutMillis = 10000)
	{
		var stopwatch = Stopwatch.StartNew();
		while (stopwatch.ElapsedMilliseconds < timeoutMillis)
		{
			if (condition())
				return;
			await Task.Delay(50, CancellationToken.None);
		}
		throw new AssertionException($"Timeout waiting for {description}.");
	}
}
