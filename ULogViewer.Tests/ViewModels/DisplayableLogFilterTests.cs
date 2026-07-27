using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Tests of <see cref="DisplayableLogFilter"/>.
/// </summary>
[TestFixture]
class DisplayableLogFilterTests : ApplicationBasedTests
{
	// Static fields.
	static readonly IDisplayableLogComparer ascendingComparer = new DisplayableLogComparer((lhs, rhs) => lhs.LogId.CompareTo(rhs.LogId), SortDirection.Ascending);


	/// <summary>
	/// Test for filtering with regex which matches all text.
	/// </summary>
	[Test]
	public void AllMatchingRegexTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one"),
				CreateLog(context, "beta two"),
				CreateLog(context, "alpha three"),
			];

			// exclusive regex should still be applied when inclusive regex matches all text
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () =>
			{
				filter.InclusiveTextRegexList = [ new Regex(".*") ];
				filter.ExclusiveTextRegexList = [ new Regex("beta") ];
			}, 3000);
			Assert.That(filter.IsProcessingNeeded, "Filtering is needed when exclusive regex is set.");
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0], logs[2] ]), "Log which matches exclusive regex should be excluded.");

			// exclusive regex which matches all text should exclude all logs
			await FilterLogsAsync(filter, () =>
			{
				filter.InclusiveTextRegexList = [];
				filter.ExclusiveTextRegexList = [ new Regex(".*") ];
			}, 3000);
			Assert.That(filter.IsProcessingNeeded, "Filtering is needed when exclusive regex matches all text.");
			Assert.That(filter.FilteredLogs, Is.Empty, "All logs should be excluded by exclusive regex which matches all text.");
		});
	}


	/// <summary>
	/// Test for combining conditions of filtering.
	/// </summary>
	[Test]
	public void CombinationModeTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one", LogLevel.Error),
				CreateLog(context, "alpha two", LogLevel.Warn),
				CreateLog(context, "beta three", LogLevel.Error),
			];

			// intersection of text and level conditions
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () =>
			{
				filter.CombinationMode = FilterCombinationMode.Intersection;
				filter.InclusiveTextRegexList = [ new Regex("alpha") ];
				filter.Levels = [ LogLevel.Error ];
			});
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0] ]));

			// union of text and level conditions
			await FilterLogsAsync(filter, () => filter.CombinationMode = FilterCombinationMode.Union);
			Assert.That(filter.FilteredLogs, Is.EqualTo(logs));

			// automatic mode without process ID and thread ID is intersection
			await FilterLogsAsync(filter, () => filter.CombinationMode = FilterCombinationMode.Auto);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0] ]), "Automatic mode should be intersection without process ID and thread ID.");
		});
	}


	// Create filter with given properties to be considered into filtering.
	DisplayableLogFilter CreateFilter(IList<DisplayableLog> sourceLogs, params string[] filteringLogPropertyNames) =>
		new DisplayableLogFilter(this.Application, sourceLogs, ascendingComparer).Setup(it =>
		{
			it.FilteringLogProperties = [ ..filteringLogPropertyNames.Select(name => new DisplayableLogProperty(this.Application, name, null, null)) ];
		});


	// Create log with given message and level.
	static DisplayableLog CreateLog(DisplayableLogTestContext context, string message, LogLevel level = LogLevel.Undefined) =>
		context.CreateLog(builder =>
		{
			builder.Set(nameof(Log.Message), message);
			if (level != LogLevel.Undefined)
				builder.Set(nameof(Log.Level), level.ToString());
		});


	/// <summary>
	/// Test for filtering by exclusive text regex.
	/// </summary>
	[Test]
	public void ExclusiveTextFilteringTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one"),
				CreateLog(context, "beta two"),
				CreateLog(context, "alpha three"),
			];

			// exclude logs without inclusive regex
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () => filter.ExclusiveTextRegexList = [ new Regex("beta") ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0], logs[2] ]), "Logs which do not match exclusive regex should be kept.");

			// exclude logs which match inclusive regex
			await FilterLogsAsync(filter, () =>
			{
				filter.InclusiveTextRegexList = [ new Regex("alpha") ];
				filter.ExclusiveTextRegexList = [ new Regex("three") ];
			});
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0] ]));
		});
	}


	// Apply given changes to filter and wait for completion of filtering.
	static async Task FilterLogsAsync(DisplayableLogFilter filter, Action setup, int timeoutMillis = 10000)
	{
		// attach to filter
		var filteringCompletedSource = new TaskCompletionSource();
		var isFilteringStarted = false;
		var propertyChangedHandler = new PropertyChangedEventHandler((_, e) =>
		{
			if (e.PropertyName != nameof(DisplayableLogFilter.IsProcessing))
				return;
			if (filter.IsProcessing)
				isFilteringStarted = true;
			else if (isFilteringStarted)
				filteringCompletedSource.TrySetResult();
		});
		filter.PropertyChanged += propertyChangedHandler;

		// apply changes and wait for completion
		try
		{
			setup();
			try
			{
				await filteringCompletedSource.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMillis), CancellationToken.None);
			}
			catch (TimeoutException)
			{
				// filtering may be unnecessary so that it will not be started
			}
		}
		finally
		{
			filter.PropertyChanged -= propertyChangedHandler;
		}
	}


	/// <summary>
	/// Test for filtering by inclusive text regex.
	/// </summary>
	[Test]
	public void InclusiveTextFilteringTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one"),
				CreateLog(context, "beta two"),
				CreateLog(context, "gamma three"),
			];

			// filter by single regex
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () => filter.InclusiveTextRegexList = [ new Regex("alpha") ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0] ]));

			// filter by multiple regexes
			await FilterLogsAsync(filter, () => filter.InclusiveTextRegexList = [ new Regex("alpha"), new Regex("gamma") ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0], logs[2] ]), "Log which matches one of inclusive regexes should be kept.");

			// no log matches
			await FilterLogsAsync(filter, () => filter.InclusiveTextRegexList = [ new Regex("delta") ]);
			Assert.That(filter.FilteredLogs, Is.Empty);
		});
	}


	/// <summary>
	/// Test for filtering by level of log.
	/// </summary>
	[Test]
	public void LevelFilteringTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "log 1", LogLevel.Debug),
				CreateLog(context, "log 2", LogLevel.Error),
				CreateLog(context, "log 3", LogLevel.Warn),
			];

			// filter by single level
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () => filter.Levels = [ LogLevel.Error ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[1] ]));

			// filter by multiple levels
			await FilterLogsAsync(filter, () => filter.Levels = [ LogLevel.Error, LogLevel.Warn ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[1], logs[2] ]));

			// undefined level will be ignored
			await FilterLogsAsync(filter, () => filter.Levels = [ LogLevel.Error, LogLevel.Undefined ]);
			Assert.That(filter.Levels, Is.EquivalentTo([ LogLevel.Error ]), "Undefined level should be ignored.");
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[1] ]));

			// filtering by level is disabled if the first level is undefined
			await FilterLogsAsync(filter, () => filter.Levels = [ LogLevel.Undefined ], 1000);
			Assert.That(filter.Levels, Is.Empty);
			Assert.That(filter.IsProcessingNeeded, Is.False);
		});
	}


	/// <summary>
	/// Test for keeping marked logs.
	/// </summary>
	[Test]
	public void MarkedLogsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one"),
				CreateLog(context, "beta two"),
			];
			logs[1].MarkedColor = MarkColor.Red;

			// marked log is kept even if it does not match conditions
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () => filter.InclusiveTextRegexList = [ new Regex("alpha") ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo(logs), "Marked log should be kept.");

			// marked log is filtered as other logs
			await FilterLogsAsync(filter, () => filter.IncludeMarkedLogs = false);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0] ]));
		});
	}


	/// <summary>
	/// Test for skipping filtering when no condition was set.
	/// </summary>
	[Test]
	public void NoFilteringNeededTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one"),
				CreateLog(context, "beta two"),
			];

			// no condition was set
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			await FilterLogsAsync(filter, () => sourceLogs.AddRange(logs), 1000);
			Assert.That(filter.IsProcessingNeeded, Is.False);
			Assert.That(filter.FilteredLogs, Is.Empty, "No log should be filtered when no condition was set.");

			// inclusive regex which matches all text is not a condition
			await FilterLogsAsync(filter, () => filter.InclusiveTextRegexList = [ new Regex(".*") ], 1000);
			Assert.That(filter.IsProcessingNeeded, Is.False);
			Assert.That(filter.FilteredLogs, Is.Empty);

			// text regex without log property to filter is not a condition
			using var filterWithoutTextProperty = this.CreateFilter(sourceLogs, nameof(DisplayableLog.ProcessId));
			await FilterLogsAsync(filterWithoutTextProperty, () => filterWithoutTextProperty.InclusiveTextRegexList = [ new Regex("alpha") ], 1000);
			Assert.That(filterWithoutTextProperty.IsProcessingNeeded, Is.False);
			Assert.That(filterWithoutTextProperty.FilteredLogs, Is.Empty);
		});
	}


	/// <summary>
	/// Test for filtering by process ID and thread ID.
	/// </summary>
	[Test]
	public void ProcessIdThreadIdFilteringTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				context.CreateLog(builder =>
				{
					builder.Set(nameof(Log.Message), "log 1");
					builder.Set(nameof(Log.ProcessId), "1");
					builder.Set(nameof(Log.ThreadId), "11");
				}),
				context.CreateLog(builder =>
				{
					builder.Set(nameof(Log.Message), "log 2");
					builder.Set(nameof(Log.ProcessId), "2");
					builder.Set(nameof(Log.ThreadId), "22");
				}),
			];

			// filter by process ID and thread ID
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message), nameof(DisplayableLog.ProcessId), nameof(DisplayableLog.ThreadId));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () => filter.ProcessId = 1);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0] ]));
			await FilterLogsAsync(filter, () =>
			{
				filter.ProcessId = null;
				filter.ThreadId = 22;
			});
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[1] ]));

			// process ID is ignored if it is not one of properties to filter
			using var filterWithoutProcessId = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			await FilterLogsAsync(filterWithoutProcessId, () => filterWithoutProcessId.ProcessId = 1);
			Assert.That(filterWithoutProcessId.IsProcessingNeeded, "Filtering is needed when process ID was set.");
			Assert.That(filterWithoutProcessId.FilteredLogs, Is.EqualTo(logs), "Process ID should be ignored if it is not one of properties to filter.");
		});
	}


	/// <summary>
	/// Test for notification of property change.
	/// </summary>
	[Test]
	public void PropertyChangeNotificationTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// attach to filter
			ObservableList<DisplayableLog> sourceLogs = [];
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			HashSet<string> watchedPropertyNames =
			[
				nameof(DisplayableLogFilter.EndTimestamp),
				nameof(DisplayableLogFilter.IncludeMarkedLogs),
				nameof(DisplayableLogFilter.Levels),
				nameof(DisplayableLogFilter.ProcessId),
				nameof(DisplayableLogFilter.TimestampLogProperty),
			];
			List<string> changedPropertyNames = [];
			filter.PropertyChanged += (_, e) =>
			{
				var propertyName = e.PropertyName.AsNonNull();
				if (watchedPropertyNames.Contains(propertyName))
					changedPropertyNames.Add(propertyName);
			};

			// check notification of each property
			Assert.Multiple(() =>
			{
				// change value of properties
				filter.ProcessId = 1;
				filter.IncludeMarkedLogs = false;
				Assert.That(changedPropertyNames, Is.EqualTo([ nameof(DisplayableLogFilter.ProcessId), nameof(DisplayableLogFilter.IncludeMarkedLogs) ]));

				// set same value to properties
				changedPropertyNames.Clear();
				filter.ProcessId = 1;
				filter.IncludeMarkedLogs = false;
				Assert.That(changedPropertyNames, Is.Empty, "Setting same value to properties should not raise notification.");

				// change name of log property to get timestamp
				filter.TimestampLogProperty = nameof(DisplayableLog.Timestamp);
				Assert.That(changedPropertyNames, Is.EqualTo([ nameof(DisplayableLogFilter.TimestampLogProperty) ]));

				// change levels
				changedPropertyNames.Clear();
				filter.Levels = [ LogLevel.Error ];
				Assert.That(changedPropertyNames, Is.EqualTo([ nameof(DisplayableLogFilter.Levels) ]));

				// set same levels
				changedPropertyNames.Clear();
				filter.Levels = [ LogLevel.Error ];
				Assert.That(changedPropertyNames, Is.Empty, "Setting same levels should not raise notification.");
			});
		});
	}


	/// <summary>
	/// Test for changing source logs.
	/// </summary>
	[Test]
	public void SourceLogsChangeTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			DisplayableLog[] logs =
			[
				CreateLog(context, "alpha one"),
				CreateLog(context, "beta two"),
				CreateLog(context, "alpha three"),
			];

			// filter logs which were added before setting condition
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () => filter.InclusiveTextRegexList = [ new Regex("alpha") ]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0], logs[2] ]));

			// filter log which was added after setting condition
			var addedLog = CreateLog(context, "alpha four");
			await FilterLogsAsync(filter, () => sourceLogs.Add(addedLog));
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0], logs[2], addedLog ]));

			// remove filtered log from source logs
			sourceLogs.Remove(logs[0]);
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[2], addedLog ]), "Removed log should also be removed from filtered logs.");
		});
	}


	/// <summary>
	/// Test for filtering by timestamp.
	/// </summary>
	[Test]
	public void TimestampFilteringTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare logs
			using var context = new DisplayableLogTestContext(this.Application);
			ObservableList<DisplayableLog> sourceLogs = [];
			var baseTimestamp = new DateTime(2026, 7, 26, 13, 0, 0);
			DisplayableLog[] logs =
			[
				CreateLogWithTimestamp(context, "log 1", baseTimestamp),
				CreateLogWithTimestamp(context, "log 2", baseTimestamp.AddHours(1)),
				CreateLogWithTimestamp(context, "log 3", baseTimestamp.AddHours(2)),
				CreateLog(context, "log without timestamp"),
			];

			// filter by beginning timestamp
			using var filter = this.CreateFilter(sourceLogs, nameof(DisplayableLog.Message));
			sourceLogs.AddRange(logs);
			await FilterLogsAsync(filter, () =>
			{
				filter.TimestampLogProperty = nameof(DisplayableLog.Timestamp);
				filter.BeginningTimestamp = baseTimestamp.AddHours(1);
			});
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[1], logs[2], logs[3] ]), "Log without timestamp should be kept.");

			// filter by ending timestamp
			await FilterLogsAsync(filter, () =>
			{
				filter.BeginningTimestamp = null;
				filter.EndTimestamp = baseTimestamp.AddHours(1);
			});
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[0], logs[1], logs[3] ]));

			// filter by range of timestamp
			await FilterLogsAsync(filter, () => filter.BeginningTimestamp = baseTimestamp.AddHours(1));
			Assert.That(filter.FilteredLogs, Is.EqualTo([ logs[1], logs[3] ]));

			// timestamp is ignored without name of log property to get timestamp
			await FilterLogsAsync(filter, () => filter.TimestampLogProperty = null, 1000);
			Assert.That(filter.IsProcessingNeeded, Is.False);
		});
	}


	// Create log with given message and timestamp.
	static DisplayableLog CreateLogWithTimestamp(DisplayableLogTestContext context, string message, DateTime timestamp) =>
		context.CreateLog(builder =>
		{
			builder.Set(nameof(Log.Message), message);
			builder.Set(nameof(Log.Timestamp), timestamp.ToBinary().ToString());
		});
}
