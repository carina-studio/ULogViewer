using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Text;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Tests of <see cref="DisplayableLog"/>.
/// </summary>
[TestFixture]
class DisplayableLogTests : ApplicationBasedTests
{
	// Static fields.
	static readonly IDisplayableLogComparer comparer = new DisplayableLogComparer((lhs, rhs) => lhs.LogId.CompareTo(rhs.LogId), SortDirection.Ascending);


	/// <summary>
	/// Test for managing analysis results of log.
	/// </summary>
	[Test]
	public void AnalysisResultManagementTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log and analyzer
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			using var analyzer = new DummyDisplayableLogAnalyzer(this.Application, sourceLogs, comparer);
			var log = context.CreateLog();
			var result1 = new DisplayableLogAnalysisResult(analyzer, DisplayableLogAnalysisResultType.Warning, log);
			var result2 = new DisplayableLogAnalysisResult(analyzer, DisplayableLogAnalysisResultType.Information, log);
			var result3 = new DisplayableLogAnalysisResult(analyzer, DisplayableLogAnalysisResultType.Error, log);

			// check initial state
			var baselineMemorySize = log.MemorySize;
			Assert.That(baselineMemorySize, Is.GreaterThan(log.Log.MemorySize));
			Assert.That(log.HasAnalysisResult, Is.False);
			Assert.That(log.AnalysisResults, Is.Empty);

			// collect change notifications and group events
			List<string> notified = [];
			log.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");
			var addedEventCount = 0;
			var removedEventCount = 0;
			context.Group.AnalysisResultAdded += (_, it) =>
			{
				if (it == log)
					++addedEventCount;
			};
			context.Group.AnalysisResultRemoved += (_, it) =>
			{
				if (it == log)
					++removedEventCount;
			};

			// add first result
			log.AddAnalysisResult(result1);
			Assert.That(log.HasAnalysisResult, Is.True);
			Assert.That(log.AnalysisResults, Is.EqualTo([ result1 ]));
			Assert.That(notified, Is.EqualTo([ nameof(DisplayableLog.AnalysisResultIndicatorIcon), nameof(DisplayableLog.HasAnalysisResult) ]));
			Assert.That(addedEventCount, Is.EqualTo(1));
			Assert.That(log.MemorySize, Is.GreaterThan(baselineMemorySize));

			// add result with lower priority type without affecting indicator
			notified.Clear();
			log.AddAnalysisResult(result2);
			Assert.That(log.AnalysisResults, Is.EqualTo([ result1, result2 ]));
			Assert.That(notified, Is.Empty);
			Assert.That(addedEventCount, Is.EqualTo(2));

			// add result with higher priority type
			notified.Clear();
			log.AddAnalysisResult(result3);
			Assert.That(log.AnalysisResults, Is.EqualTo([ result1, result2, result3 ]));
			Assert.That(notified, Is.EqualTo([ nameof(DisplayableLog.AnalysisResultIndicatorIcon) ]));
			Assert.That(addedEventCount, Is.EqualTo(3));

			// remove result with highest priority type
			notified.Clear();
			log.RemoveAnalysisResult(result3);
			Assert.That(log.AnalysisResults, Is.EqualTo([ result1, result2 ]));
			Assert.That(notified, Does.Contain(nameof(DisplayableLog.AnalysisResultIndicatorIcon)));
			Assert.That(removedEventCount, Is.EqualTo(1));

			// remove result which was not added
			var unknownResult = new DisplayableLogAnalysisResult(analyzer, DisplayableLogAnalysisResultType.Warning, log);
			notified.Clear();
			log.RemoveAnalysisResult(unknownResult);
			Assert.That(log.AnalysisResults, Is.EqualTo([ result1, result2 ]));
			Assert.That(notified, Is.Empty);
			Assert.That(removedEventCount, Is.EqualTo(1));

			// remove remaining results
			log.RemoveAnalysisResult(result1);
			notified.Clear();
			log.RemoveAnalysisResult(result2);
			Assert.That(log.HasAnalysisResult, Is.False);
			Assert.That(log.AnalysisResults, Is.Empty);
			Assert.That(notified, Does.Contain(nameof(DisplayableLog.HasAnalysisResult)));
			Assert.That(removedEventCount, Is.EqualTo(3));
			Assert.That(log.MemorySize, Is.EqualTo(baselineMemorySize));
		});
	}


	/// <summary>
	/// Test for removing analysis results of log by analyzer.
	/// </summary>
	[Test]
	public void AnalysisResultRemovalByAnalyzerTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log and results from two analyzers
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			using var analyzerA = new DummyDisplayableLogAnalyzer(this.Application, sourceLogs, comparer);
			using var analyzerB = new DummyDisplayableLogAnalyzer(this.Application, sourceLogs, comparer);
			var log = context.CreateLog();
			var resultA1 = new DisplayableLogAnalysisResult(analyzerA, DisplayableLogAnalysisResultType.Warning, log);
			var resultA2 = new DisplayableLogAnalysisResult(analyzerA, DisplayableLogAnalysisResultType.Information, log);
			var resultB1 = new DisplayableLogAnalysisResult(analyzerB, DisplayableLogAnalysisResultType.Checkpoint, log);
			log.AddAnalysisResult(resultA1);
			log.AddAnalysisResult(resultA2);
			log.AddAnalysisResult(resultB1);

			// remove all results of first analyzer
			var removedEventCount = 0;
			context.Group.AnalysisResultRemoved += (_, it) =>
			{
				if (it == log)
					++removedEventCount;
			};
			log.RemoveAnalysisResults(analyzerA);
			Assert.That(log.AnalysisResults, Is.EqualTo([ resultB1 ]));
			Assert.That(log.HasAnalysisResult, Is.True);
			Assert.That(removedEventCount, Is.EqualTo(1));

			// remove results of analyzer without remaining results
			log.RemoveAnalysisResults(analyzerA);
			Assert.That(log.AnalysisResults, Is.EqualTo([ resultB1 ]));
			Assert.That(removedEventCount, Is.EqualTo(1));

			// remove results of second analyzer
			log.RemoveAnalysisResults(analyzerB);
			Assert.That(log.HasAnalysisResult, Is.False);
			Assert.That(log.AnalysisResults, Is.Empty);
			Assert.That(removedEventCount, Is.EqualTo(2));
		});
	}


	/// <summary>
	/// Test for creating getters of log properties.
	/// </summary>
	[Test]
	public void CreateLogPropertyGetterTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log
			using var context = new DisplayableLogTestContext(this.Application);
			var log = CreateLogWithAllProperties(context);

			// get property with native type
			var messageGetter = DisplayableLog.CreateLogPropertyGetter<IStringSource?>(nameof(DisplayableLog.Message));
			Assert.That(messageGetter(log)?.ToString(), Is.EqualTo("Test message"));
			var idGetter = DisplayableLog.CreateLogPropertyGetter<long>(nameof(DisplayableLog.LogId));
			Assert.That(idGetter(log), Is.EqualTo(log.LogId));
			var pidGetter = DisplayableLog.CreateLogPropertyGetter<int?>(nameof(DisplayableLog.ProcessId));
			Assert.That(pidGetter(log), Is.EqualTo(123));

			// get property with conversion to string
			var messageStringGetter = DisplayableLog.CreateLogPropertyGetter<string?>(nameof(DisplayableLog.Message));
			Assert.That(messageStringGetter(log), Is.EqualTo("Test message"));

			// get property with conversion from string to string source
			var levelStringGetter = DisplayableLog.CreateLogPropertyGetter<IStringSource?>(nameof(DisplayableLog.LevelString));
			Assert.That(levelStringGetter(log)?.ToString(), Is.EqualTo(log.LevelString));
		});
	}


	// Create displayable log with all common properties.
	static DisplayableLog CreateLogWithAllProperties(DisplayableLogTestContext context) => context.CreateLog(it =>
	{
		it.Set(nameof(Log.BeginningTimeSpan), "00:00:01");
		it.Set(nameof(Log.BeginningTimestamp), "2026-07-24T10:30:00");
		it.Set(nameof(Log.Category), "TestCategory");
		it.Set(nameof(Log.DeviceId), "Device-1");
		it.Set(nameof(Log.DeviceName), "Test Device");
		it.Set(nameof(Log.EndingTimeSpan), "00:00:05");
		it.Set(nameof(Log.EndingTimestamp), "2026-07-24T10:30:05");
		it.Set(nameof(Log.Error), "Error message");
		it.Set(nameof(Log.Event), "TestEvent");
		it.Set(nameof(Log.Exception), "Exception message");
		it.Set(nameof(Log.Extra1), "Extra data 1");
		it.Set(nameof(Log.Extra20), "Extra data 20");
		it.Set(nameof(Log.FileName), "test.log");
		it.Set(nameof(Log.Level), nameof(LogLevel.Error));
		it.Set(nameof(Log.LineNumber), "42");
		it.Set(nameof(Log.Message), "Test message");
		it.Set(nameof(Log.ProcessId), "123");
		it.Set(nameof(Log.ProcessName), "TestProcess");
		it.Set(nameof(Log.SourceName), "TestSource");
		it.Set(nameof(Log.Summary), "Test summary");
		it.Set(nameof(Log.Tags), "tag1,tag2");
		it.Set(nameof(Log.ThreadId), "456");
		it.Set(nameof(Log.ThreadName), "TestThread");
		it.Set(nameof(Log.TimeSpan), "00:00:03");
		it.Set(nameof(Log.Timestamp), "2026-07-24T10:30:01");
		it.Set(nameof(Log.Title), "Test title");
		it.Set(nameof(Log.UserId), "user-1");
		it.Set(nameof(Log.UserName), "Test User");
	});


	/// <summary>
	/// Test for change notifications of formatted strings when display formats changed.
	/// </summary>
	[Test]
	public void FormatChangeNotificationTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			var logWithValues = context.CreateLog(it =>
			{
				it.Set(nameof(Log.TimeSpan), "00:00:03");
				it.Set(nameof(Log.Timestamp), "2026-07-24T10:30:01");
			});
			var logWithoutValues = context.CreateLog();

			// collect change notifications
			List<string> notifiedWithValues = [];
			List<string> notifiedWithoutValues = [];
			logWithValues.PropertyChanged += (_, e) => notifiedWithValues.Add(e.PropertyName ?? "");
			logWithoutValues.PropertyChanged += (_, e) => notifiedWithoutValues.Add(e.PropertyName ?? "");

			// change time span format
			context.Profile.TimeSpanFormatForDisplaying = @"hh\:mm\:ss";
			Assert.That(notifiedWithValues, Does.Contain(nameof(DisplayableLog.TimeSpanString)));
			Assert.That(notifiedWithoutValues, Does.Not.Contain(nameof(DisplayableLog.TimeSpanString)));

			// change timestamp format
			notifiedWithValues.Clear();
			notifiedWithoutValues.Clear();
			context.Profile.TimestampFormatForDisplaying = "yyyy/MM/dd HH:mm:ss";
			Assert.That(notifiedWithValues, Does.Contain(nameof(DisplayableLog.TimestampString)));
			Assert.That(notifiedWithValues, Does.Contain(nameof(DisplayableLog.ReadTimeString)));
			Assert.That(notifiedWithoutValues, Does.Not.Contain(nameof(DisplayableLog.TimestampString)));
			Assert.That(notifiedWithoutValues, Does.Contain(nameof(DisplayableLog.ReadTimeString)), "ReadTimeString should be notified even when log has no timestamp.");
		});
	}


	/// <summary>
	/// Test for time spans of log in string format.
	/// </summary>
	[Test]
	public void FormattedTimeSpanStringTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check formatting with custom format
			using var context = new DisplayableLogTestContext(this.Application, profile => profile.TimeSpanFormatForDisplaying = @"hh\:mm\:ss");
			var log = CreateLogWithAllProperties(context);
			Assert.That(log.BeginningTimeSpanString?.ToString(), Is.EqualTo("00:00:01"));
			Assert.That(log.EndingTimeSpanString?.ToString(), Is.EqualTo("00:00:05"));
			Assert.That(log.TimeSpanString?.ToString(), Is.EqualTo("00:00:03"));

			// check log without time spans
			var emptyLog = context.CreateLog();
			Assert.That(emptyLog.BeginningTimeSpanString, Is.Null);
			Assert.That(emptyLog.EndingTimeSpanString, Is.Null);
			Assert.That(emptyLog.TimeSpanString, Is.Null);

			// check formatting without custom format
			using var defaultContext = new DisplayableLogTestContext(this.Application);
			var defaultLog = CreateLogWithAllProperties(defaultContext);
			Assert.That(defaultLog.TimeSpanString?.ToString(), Is.EqualTo(TimeSpan.FromSeconds(3).ToString()));

			// check formatting with invalid format
			using var invalidContext = new DisplayableLogTestContext(this.Application, profile => profile.TimeSpanFormatForDisplaying = "q");
			var invalidLog = CreateLogWithAllProperties(invalidContext);
			Assert.That(invalidLog.TimeSpanString, Is.Null);
		});
	}


	/// <summary>
	/// Test for timestamps of log in string format.
	/// </summary>
	[Test]
	public void FormattedTimestampStringTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check formatting with custom format
			const string format = "yyyy/MM/dd HH:mm:ss";
			using var context = new DisplayableLogTestContext(this.Application, profile => profile.TimestampFormatForDisplaying = format);
			var log = CreateLogWithAllProperties(context);
			Assert.That(log.BeginningTimestampString?.ToString(), Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 0).ToString(format)));
			Assert.That(log.EndingTimestampString?.ToString(), Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 5).ToString(format)));
			Assert.That(log.TimestampString?.ToString(), Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 1).ToString(format)));
			Assert.That(log.ReadTimeString?.ToString(), Is.EqualTo(log.ReadTime.ToString(format)));

			// check log without timestamps
			var emptyLog = context.CreateLog();
			Assert.That(emptyLog.BeginningTimestampString, Is.Null);
			Assert.That(emptyLog.EndingTimestampString, Is.Null);
			Assert.That(emptyLog.TimestampString, Is.Null);
			Assert.That(emptyLog.ReadTimeString, Is.Not.Null);

			// check formatting without custom format
			using var defaultContext = new DisplayableLogTestContext(this.Application);
			var defaultLog = CreateLogWithAllProperties(defaultContext);
			Assert.That(defaultLog.TimestampString?.ToString(), Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
		});
	}


	/// <summary>
	/// Test for disposing log.
	/// </summary>
	[Test]
	public void DisposalTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log and analyzer
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			using var analyzer = new DummyDisplayableLogAnalyzer(this.Application, sourceLogs, comparer);
			var log = context.CreateLog(it => it.Set(nameof(Log.Message), "Test message"));

			// check log reader before disposal
			Assert.That(log.LogReader, Is.Not.Null);

			// dispose log
			log.Dispose();

			// adding analysis result becomes no-op
			var result = new DisplayableLogAnalysisResult(analyzer, DisplayableLogAnalysisResultType.Warning, log);
			log.AddAnalysisResult(result);
			Assert.That(log.HasAnalysisResult, Is.False);
			Assert.That(log.AnalysisResults, Is.Empty);

			// log reader is no longer available
			Assert.Throws<InvalidOperationException>(() => _ = log.LogReader);

			// wrapped log is still accessible
			Assert.That(log.Message?.ToString(), Is.EqualTo("Test message"));
			Assert.That(log.LogId, Is.EqualTo(log.Log.Id));

			// dispose again without error
			log.Dispose();
		});
	}


	/// <summary>
	/// Test for line counts of extra data of log.
	/// </summary>
	[Test]
	public void ExtraLineCountTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log with extras captured by profile
			using var context = new DisplayableLogTestContext(this.Application, profile => profile.LogPatterns = [ new LogPattern("(?<Extra1>.*) (?<Extra5>.*)", false, false, null) ]);
			var log = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Extra1), "A\nB");
				it.Set(nameof(Log.Extra2), "F\nG");
				it.Set(nameof(Log.Extra5), "C\nD\nE");
			});

			// check line counts of captured extras
			Assert.That(log.Extra1LineCount, Is.EqualTo(2));
			Assert.That(log.Extra5LineCount, Is.EqualTo(3));

			// check line counts of extras not captured by profile
			Assert.That(log.Extra2LineCount, Is.Zero);
			Assert.That(log.Extra20LineCount, Is.Zero);

			// check extra lines against maximum display line count
			Assert.That(log.HasExtraLinesOfExtra1, Is.True);
			Assert.That(log.HasExtraLinesOfExtra2, Is.False);
			var singleLineLog = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Extra1), "Single line");
			});
			Assert.That(singleLineLog.HasExtraLinesOfExtra1, Is.False);
		});
	}


	/// <summary>
	/// Test for extra lines of log properties against maximum display line count.
	/// </summary>
	[Test]
	public void HasExtraLinesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check maximum display line count
			using var context = new DisplayableLogTestContext(this.Application);
			Assert.That(context.Group.MaxDisplayLineCount, Is.EqualTo(1));

			// check multi-line properties
			var multiLineLog = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Error), "E1\nE2");
				it.Set(nameof(Log.Exception), "X1\nX2");
				it.Set(nameof(Log.Message), "M1\nM2");
				it.Set(nameof(Log.Summary), "S1\nS2");
				it.Set(nameof(Log.Warning), "W1\nW2");
			});
			Assert.That(multiLineLog.HasExtraLinesOfError, Is.True);
			Assert.That(multiLineLog.HasExtraLinesOfException, Is.True);
			Assert.That(multiLineLog.HasExtraLinesOfMessage, Is.True);
			Assert.That(multiLineLog.HasExtraLinesOfSummary, Is.True);
			Assert.That(multiLineLog.HasExtraLinesOfWarning, Is.True);

			// check single-line properties
			var singleLineLog = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Error), "E1");
				it.Set(nameof(Log.Exception), "X1");
				it.Set(nameof(Log.Message), "M1");
				it.Set(nameof(Log.Summary), "S1");
				it.Set(nameof(Log.Warning), "W1");
			});
			Assert.That(singleLineLog.HasExtraLinesOfError, Is.False);
			Assert.That(singleLineLog.HasExtraLinesOfException, Is.False);
			Assert.That(singleLineLog.HasExtraLinesOfMessage, Is.False);
			Assert.That(singleLineLog.HasExtraLinesOfSummary, Is.False);
			Assert.That(singleLineLog.HasExtraLinesOfWarning, Is.False);

			// check properties without values
			var emptyLog = context.CreateLog();
			Assert.That(emptyLog.HasExtraLinesOfMessage, Is.False);
		});
	}


	/// <summary>
	/// Test for string representation of level of log.
	/// </summary>
	[Test]
	public void LevelStringTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check level with mapping in profile
			using var context = new DisplayableLogTestContext(this.Application, profile => profile.LogLevelMapForWriting = new Dictionary<LogLevel, string> { { LogLevel.Error, "E!" } });
			var log = CreateLogWithAllProperties(context);
			Assert.That(log.LevelString, Is.EqualTo("E!"));

			// check level without mapping
			var infoLog = context.CreateLog(it => it.Set(nameof(Log.Level), nameof(LogLevel.Info)));
			Assert.That(infoLog.LevelString, Is.Not.Empty);

			// check undefined level
			var undefinedLog = context.CreateLog();
			Assert.That(undefinedLog.LevelString, Is.Empty);
		});
	}


	/// <summary>
	/// Test for line counts of multi-line properties of log.
	/// </summary>
	[Test]
	public void LineCountTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check line counts of properties with values
			using var context = new DisplayableLogTestContext(this.Application);
			var log = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Error), "Line 1\nLine 2\nLine 3");
				it.Set(nameof(Log.Exception), "Line 1\nLine 2\nLine 3\nLine 4\nLine 5");
				it.Set(nameof(Log.Message), "Single line");
				it.Set(nameof(Log.Summary), "Line 1\nLine 2");
				it.Set(nameof(Log.Warning), "Line 1\nLine 2\nLine 3\nLine 4");
			});
			Assert.That(log.ErrorLineCount, Is.EqualTo(3));
			Assert.That(log.ExceptionLineCount, Is.EqualTo(5));
			Assert.That(log.MessageLineCount, Is.EqualTo(1));
			Assert.That(log.SummaryLineCount, Is.EqualTo(2));
			Assert.That(log.WarningLineCount, Is.EqualTo(4));

			// check line counts of properties without values
			var emptyLog = context.CreateLog();
			Assert.That(emptyLog.ErrorLineCount, Is.Zero);
			Assert.That(emptyLog.ExceptionLineCount, Is.Zero);
			Assert.That(emptyLog.MessageLineCount, Is.Zero);
			Assert.That(emptyLog.SummaryLineCount, Is.Zero);
			Assert.That(emptyLog.WarningLineCount, Is.Zero);

			// check line count cap
			var bigLog = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Message), new string('\n', 300));
			});
			Assert.That(bigLog.MessageLineCount, Is.EqualTo(255));
		});
	}


	/// <summary>
	/// Test for marking log with color.
	/// </summary>
	[Test]
	public void MarkedColorTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check initial state
			using var context = new DisplayableLogTestContext(this.Application);
			var log = context.CreateLog();
			Assert.That(log.MarkedColor, Is.EqualTo(MarkColor.None));
			Assert.That(log.IsMarked, Is.False);

			// collect change notifications
			List<string> notified = [];
			log.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");

			// mark log
			log.MarkedColor = MarkColor.Red;
			Assert.That(log.MarkedColor, Is.EqualTo(MarkColor.Red));
			Assert.That(log.IsMarked, Is.True);
			Assert.That(notified, Is.EqualTo([ nameof(DisplayableLog.MarkedColor), nameof(DisplayableLog.IsMarked) ]));

			// change color of marked log
			notified.Clear();
			log.MarkedColor = MarkColor.Blue;
			Assert.That(log.IsMarked, Is.True);
			Assert.That(notified, Is.EqualTo([ nameof(DisplayableLog.MarkedColor) ]));

			// set same color
			notified.Clear();
			log.MarkedColor = MarkColor.Blue;
			Assert.That(notified, Is.Empty);

			// unmark log
			log.MarkedColor = MarkColor.None;
			Assert.That(log.IsMarked, Is.False);
			Assert.That(notified, Is.EqualTo([ nameof(DisplayableLog.MarkedColor), nameof(DisplayableLog.IsMarked) ]));
		});
	}


	/// <summary>
	/// Test for change notifications when maximum display line count changed.
	/// </summary>
	[Test]
	public void MaxDisplayLineCountChangeNotificationTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log with cached line counts
			using var context = new DisplayableLogTestContext(this.Application, profile => profile.LogPatterns = [ new LogPattern("(?<Extra1>.*) (?<Extra5>.*)", false, false, null) ]);
			var log = context.CreateLog(it =>
			{
				it.Set(nameof(Log.Error), "E1\nE2");
				it.Set(nameof(Log.Exception), "X1\nX2");
				it.Set(nameof(Log.Extra1), "A1\nA2");
				it.Set(nameof(Log.Message), "M1\nM2");
				it.Set(nameof(Log.Summary), "S1\nS2");
				it.Set(nameof(Log.Warning), "W1\nW2");
			});
			_ = log.ErrorLineCount;
			_ = log.ExceptionLineCount;
			_ = log.Extra1LineCount;
			_ = log.MessageLineCount;
			_ = log.SummaryLineCount;
			_ = log.WarningLineCount;

			// collect change notifications
			List<string> notified = [];
			log.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");

			// notify maximum display line count changed
			log.OnMaxDisplayLineCountChanged();

			// check notifications
			Assert.That(notified, Is.EquivalentTo([
				nameof(DisplayableLog.HasExtraLinesOfError),
				nameof(DisplayableLog.HasExtraLinesOfException),
				nameof(DisplayableLog.HasExtraLinesOfExtra1),
				nameof(DisplayableLog.HasExtraLinesOfMessage),
				nameof(DisplayableLog.HasExtraLinesOfSummary),
				nameof(DisplayableLog.HasExtraLinesOfWarning),
			]));

			// check log without cached line counts
			var freshLog = context.CreateLog(it => it.Set(nameof(Log.Message), "M1\nM2"));
			List<string> freshNotified = [];
			freshLog.PropertyChanged += (_, e) => freshNotified.Add(e.PropertyName ?? "");
			freshLog.OnMaxDisplayLineCountChanged();
			Assert.That(freshNotified, Is.Empty);
		});
	}


	/// <summary>
	/// Test for properties which pass through values of wrapped log.
	/// </summary>
	[Test]
	public void PassThroughPropertiesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log
			using var context = new DisplayableLogTestContext(this.Application);
			var log = CreateLogWithAllProperties(context);

			// check string properties
			Assert.That(log.Category?.ToString(), Is.EqualTo("TestCategory"));
			Assert.That(log.DeviceId?.ToString(), Is.EqualTo("Device-1"));
			Assert.That(log.DeviceName?.ToString(), Is.EqualTo("Test Device"));
			Assert.That(log.Error?.ToString(), Is.EqualTo("Error message"));
			Assert.That(log.Event?.ToString(), Is.EqualTo("TestEvent"));
			Assert.That(log.Exception?.ToString(), Is.EqualTo("Exception message"));
			Assert.That(log.Extra1?.ToString(), Is.EqualTo("Extra data 1"));
			Assert.That(log.Extra20?.ToString(), Is.EqualTo("Extra data 20"));
			Assert.That(log.FileName?.ToString(), Is.EqualTo("test.log"));
			Assert.That(log.Message?.ToString(), Is.EqualTo("Test message"));
			Assert.That(log.ProcessName?.ToString(), Is.EqualTo("TestProcess"));
			Assert.That(log.SourceName?.ToString(), Is.EqualTo("TestSource"));
			Assert.That(log.Summary?.ToString(), Is.EqualTo("Test summary"));
			Assert.That(log.Tags?.ToString(), Is.EqualTo("tag1,tag2"));
			Assert.That(log.ThreadName?.ToString(), Is.EqualTo("TestThread"));
			Assert.That(log.Title?.ToString(), Is.EqualTo("Test title"));
			Assert.That(log.UserId?.ToString(), Is.EqualTo("user-1"));
			Assert.That(log.UserName?.ToString(), Is.EqualTo("Test User"));

			// check value properties
			Assert.That(log.Level, Is.EqualTo(LogLevel.Error));
			Assert.That(log.LineNumber, Is.EqualTo(42));
			Assert.That(log.ProcessId, Is.EqualTo(123));
			Assert.That(log.ThreadId, Is.EqualTo(456));

			// check timestamps and time spans
			Assert.That(log.BeginningTimeSpan, Is.EqualTo(TimeSpan.FromSeconds(1)));
			Assert.That(log.BeginningTimestamp, Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 0)));
			Assert.That(log.EndingTimeSpan, Is.EqualTo(TimeSpan.FromSeconds(5)));
			Assert.That(log.EndingTimestamp, Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 5)));
			Assert.That(log.TimeSpan, Is.EqualTo(TimeSpan.FromSeconds(3)));
			Assert.That(log.Timestamp, Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 1)));

			// check identity properties
			Assert.That(log.LogId, Is.EqualTo(log.Log.Id));
			Assert.That(log.ReadTime, Is.EqualTo(log.Log.ReadTime));

			// check properties without values
			var emptyLog = context.CreateLog();
			Assert.That(emptyLog.Category, Is.Null);
			Assert.That(emptyLog.LineNumber, Is.Null);
			Assert.That(emptyLog.Message, Is.Null);
			Assert.That(emptyLog.ProcessId, Is.Null);
			Assert.That(emptyLog.Timestamp, Is.Null);
			Assert.That(emptyLog.TimeSpan, Is.Null);
		});
	}


	/// <summary>
	/// Test for static reflection of log properties.
	/// </summary>
	[Test]
	public void PropertyReflectionTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check existence of properties
			Assert.That(DisplayableLog.HasProperty(nameof(DisplayableLog.Message)), Is.True);
			Assert.That(DisplayableLog.HasProperty(nameof(DisplayableLog.Level)), Is.True);
			Assert.That(DisplayableLog.HasProperty(nameof(DisplayableLog.ReadTime)), Is.True);
			Assert.That(DisplayableLog.HasProperty(nameof(DisplayableLog.TimestampString)), Is.True);
			Assert.That(DisplayableLog.HasProperty(nameof(DisplayableLog.LogId)), Is.True);
			Assert.That(DisplayableLog.HasProperty(nameof(Log.Id)), Is.False);
			Assert.That(DisplayableLog.HasProperty("NotExisting"), Is.False);

			// check existence of string properties
			Assert.That(DisplayableLog.HasStringProperty(nameof(DisplayableLog.Message)), Is.True);
			Assert.That(DisplayableLog.HasStringProperty(nameof(DisplayableLog.LevelString)), Is.True);
			Assert.That(DisplayableLog.HasStringProperty(nameof(DisplayableLog.TimestampString)), Is.True);
			Assert.That(DisplayableLog.HasStringProperty(nameof(DisplayableLog.ProcessId)), Is.False);

			// check existence of date time properties
			Assert.That(DisplayableLog.HasDateTimeProperty(nameof(DisplayableLog.Timestamp)), Is.True);
			Assert.That(DisplayableLog.HasDateTimeProperty(nameof(DisplayableLog.ReadTime)), Is.True);
			Assert.That(DisplayableLog.HasDateTimeProperty(nameof(DisplayableLog.Message)), Is.False);

			// check existence of integer properties
			Assert.That(DisplayableLog.HasInt32Property(nameof(DisplayableLog.ProcessId)), Is.True);
			Assert.That(DisplayableLog.HasInt32Property(nameof(DisplayableLog.ThreadId)), Is.True);
			Assert.That(DisplayableLog.HasInt32Property(nameof(DisplayableLog.LineNumber)), Is.True);
			Assert.That(DisplayableLog.HasInt32Property(nameof(DisplayableLog.Message)), Is.False);
			Assert.That(DisplayableLog.HasInt64Property(nameof(DisplayableLog.LogId)), Is.False);

			// check frozen properties
			Assert.That(DisplayableLog.HasFrozenProperty(nameof(DisplayableLog.Message)), Is.True);
			Assert.That(DisplayableLog.HasFrozenProperty(nameof(DisplayableLog.TimestampString)), Is.False);
			Assert.That(DisplayableLog.HasFrozenProperty(nameof(DisplayableLog.LevelString)), Is.False);
			Assert.That(DisplayableLog.HasFrozenProperty("NotExisting"), Is.False);

			// check multi-line properties
			Assert.That(DisplayableLog.HasMultiLineStringProperty(nameof(DisplayableLog.Message)), Is.True);
			Assert.That(DisplayableLog.HasMultiLineStringProperty(nameof(DisplayableLog.Error)), Is.True);
			Assert.That(DisplayableLog.HasMultiLineStringProperty(nameof(DisplayableLog.Extra1)), Is.True);
			Assert.That(DisplayableLog.HasMultiLineStringProperty(nameof(DisplayableLog.Category)), Is.False);
		});
	}


	/// <summary>
	/// Test for selection of process ID and thread ID of log.
	/// </summary>
	[Test]
	public void SelectedProcessIdAndThreadIdTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			var log = context.CreateLog(it =>
			{
				it.Set(nameof(Log.ProcessId), "123");
				it.Set(nameof(Log.ThreadId), "456");
			});
			var otherLog = context.CreateLog(it => it.Set(nameof(Log.ProcessId), "789"));
			var noIdLog = context.CreateLog();
			Assert.That(log.IsProcessIdSelected, Is.False);
			Assert.That(log.IsThreadIdSelected, Is.False);

			// collect change notifications
			List<string> notified = [];
			List<string> notifiedWithoutIds = [];
			log.PropertyChanged += (_, e) => notified.Add(e.PropertyName ?? "");
			noIdLog.PropertyChanged += (_, e) => notifiedWithoutIds.Add(e.PropertyName ?? "");

			// select process ID
			context.Group.SelectedProcessId = 123;
			Assert.That(log.IsProcessIdSelected, Is.True);
			Assert.That(otherLog.IsProcessIdSelected, Is.False);
			Assert.That(noIdLog.IsProcessIdSelected, Is.False);
			Assert.That(notified, Does.Contain(nameof(DisplayableLog.IsProcessIdSelected)));
			Assert.That(notifiedWithoutIds, Is.Empty);

			// select thread ID
			notified.Clear();
			context.Group.SelectedThreadId = 456;
			Assert.That(log.IsThreadIdSelected, Is.True);
			Assert.That(notified, Does.Contain(nameof(DisplayableLog.IsThreadIdSelected)));
			Assert.That(notifiedWithoutIds, Is.Empty);

			// clear selection
			context.Group.SelectedProcessId = null;
			context.Group.SelectedThreadId = null;
			Assert.That(log.IsProcessIdSelected, Is.False);
			Assert.That(log.IsThreadIdSelected, Is.False);
		});
	}


	/// <summary>
	/// Test for getting range of timestamps and time spans of log.
	/// </summary>
	[Test]
	public void TimestampAndTimeSpanRangeTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// check log with timestamps and time spans
			using var context = new DisplayableLogTestContext(this.Application);
			var log = CreateLogWithAllProperties(context);
			Assert.That(log.TryGetEarliestAndLatestTimestamp(out var earliest, out var latest), Is.True);
			Assert.That(earliest, Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 0)));
			Assert.That(latest, Is.EqualTo(new DateTime(2026, 7, 24, 10, 30, 5)));
			Assert.That(log.TryGetSmallestAndLargestTimeSpan(out var smallest, out var largest), Is.True);
			Assert.That(smallest, Is.EqualTo(TimeSpan.FromSeconds(1)));
			Assert.That(largest, Is.EqualTo(TimeSpan.FromSeconds(5)));

			// check log without timestamps and time spans
			var emptyLog = context.CreateLog();
			Assert.That(emptyLog.TryGetEarliestAndLatestTimestamp(out earliest, out latest), Is.False);
			Assert.That(earliest, Is.Null);
			Assert.That(latest, Is.Null);
			Assert.That(emptyLog.TryGetSmallestAndLargestTimeSpan(out smallest, out largest), Is.False);
			Assert.That(smallest, Is.Null);
			Assert.That(largest, Is.Null);
		});
	}


	/// <summary>
	/// Test for getting log properties by name.
	/// </summary>
	[Test]
	public void TryGetPropertyTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log
			using var context = new DisplayableLogTestContext(this.Application);
			var log = CreateLogWithAllProperties(context);

			// get property with native type
			Assert.That(log.TryGetProperty<IStringSource>(nameof(DisplayableLog.Message), out var messageSource), Is.True);
			Assert.That(messageSource.ToString(), Is.EqualTo("Test message"));
			Assert.That(log.TryGetProperty<int?>(nameof(DisplayableLog.ProcessId), out var pid), Is.True);
			Assert.That(pid, Is.EqualTo(123));
			Assert.That(log.TryGetProperty<LogLevel>(nameof(DisplayableLog.Level), out var level), Is.True);
			Assert.That(level, Is.EqualTo(LogLevel.Error));
			Assert.That(log.TryGetProperty<long>(nameof(DisplayableLog.LogId), out var id), Is.True);
			Assert.That(id, Is.EqualTo(log.LogId));

			// get property with conversion to string
			Assert.That(log.TryGetProperty<string>(nameof(DisplayableLog.Message), out var messageString), Is.True);
			Assert.That(messageString, Is.EqualTo("Test message"));

			// get property with unknown name or incompatible type
			Assert.That(log.TryGetProperty<string>("NotExisting", out _), Is.False);
			Assert.That(log.TryGetProperty<int>(nameof(DisplayableLog.Message), out _), Is.False);
		});
	}
}
