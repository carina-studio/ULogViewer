using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.DataSources;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Tests of <see cref="LogProfileMatcher"/>.
/// </summary>
[TestFixture]
class LogProfileMatcherTests : ApplicationBasedTests
{
	// Static fields.
	static readonly string[] PlainTextBuiltInProfileNames =
	[
		"AndroidFileLog",
		"AndroidKernelLogFile",
		"AndroidTraceFile",
		"ApacheAccessLogFile",
		"ApacheErrorLogFile",
		"AzureWebappLogFile",
		"LinuxKernelLogFile",
		"LinuxSystemLogFile",
		"MacOSSystemLogFile",
		"ULogViewerLog",
	];


	static readonly string[] ApacheAccessLines =
	[
		"127.0.0.1 - frank [10/Oct/2000:13:55:36 -0700] \"GET /apache_pb.gif HTTP/1.0\" 200 2326 \"http://example.com/\" \"Mozilla/5.0\"",
		"127.0.0.1 - alice [10/Oct/2000:13:55:37 -0700] \"GET /index.html HTTP/1.0\" 200 2326 \"http://example.com/\" \"Mozilla/5.0\"",
		"127.0.0.1 - bob [10/Oct/2000:13:55:38 -0700] \"GET /a.html HTTP/1.0\" 200 2326 \"http://example.com/\" \"Mozilla/5.0\"",
		"127.0.0.1 - carol [10/Oct/2000:13:55:39 -0700] \"GET /b.html HTTP/1.0\" 200 2326 \"http://example.com/\" \"Mozilla/5.0\"",
		"127.0.0.1 - dave [10/Oct/2000:13:55:40 -0700] \"GET /c.html HTTP/1.0\" 200 2326 \"http://example.com/\" \"Mozilla/5.0\"",
		"127.0.0.1 - erin [10/Oct/2000:13:55:41 -0700] \"GET /d.html HTTP/1.0\" 200 2326 \"http://example.com/\" \"Mozilla/5.0\"",
	];
	static readonly string[] SyslogLines =
	[
		"Aug 26 01:02:03 localhost kernel: something happened",
		"Aug 26 01:02:04 localhost kernel: something else happened",
		"Aug 26 01:02:05 localhost systemd: started a unit",
		"Aug 26 01:02:06 localhost systemd: stopped a unit",
		"Aug 26 01:02:07 localhost cron: ran a job",
		"Aug 26 01:02:08 localhost cron: ran another job",
	];


	/// <summary>
	/// Test for ranking results of matching log profiles.
	/// </summary>
	[Test]
	public void CompareResultsTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// a log profile which matched more log files is ranked first
			var oneFile = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 1, 5, 5, true));
			var twoFiles = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a", "b" ], new(5, 20, 100, 100, true));
			Assert.That(LogProfileMatcher.CompareResults(twoFiles, oneFile), Is.LessThan(0));

			// among equally matched log files, the one which started matching earlier is ranked first
			var early = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 1, 20, 20, true));
			var late = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 10, 30, 30, true));
			Assert.That(LogProfileMatcher.CompareResults(early, late), Is.LessThan(0));

			// among equal line numbers, the tighter parse is ranked first
			var tight = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 1, 5, 5, true));
			var loose = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 1, 100, 100, true));
			Assert.That(LogProfileMatcher.CompareResults(tight, loose), Is.LessThan(0));

			// among equal scores, the log profile which defines more log patterns is ranked first
			var specific = CreateResult(this.CreateFileLogProfile(it => it.LogPatterns =
			[
				new LogPattern("^(?<Message>.*)$", false, false, null),
				new LogPattern("^(?<Message>.+)$", false, false, null),
			]), [ "a" ], new(5, 1, 5, 5, true));
			var loose2 = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 1, 5, 5, true));
			Assert.That(LogProfileMatcher.CompareResults(specific, loose2), Is.LessThan(0));

			// a log profile authored by user is ranked before a built-in one
			var builtIn = CreateResult(LogProfileManager.Default.Profiles.First(it => it.IsBuiltIn && it.Name == "ApacheAccessLogFile"), [ "a" ], new(5, 1, 5, 5, true));
			var userDefined = CreateResult(this.CreateFileLogProfile(_ => { }), [ "a" ], new(5, 1, 5, 5, true));
			Assert.That(LogProfileMatcher.CompareResults(userDefined, builtIn), Is.LessThan(0));
		});
	}


	// Fields.
	string? testDirectoryPath;


	/// <summary>
	/// Delete directory which contains files generated for testing.
	/// </summary>
	[OneTimeTearDown]
	public void DeleteTestDirectory()
	{
		if (this.testDirectoryPath is not null)
		{
			Global.RunWithoutError(() => Directory.Delete(this.testDirectoryPath, true));
			this.testDirectoryPath = null;
		}
	}


	// Create log profile which reads log files through the file data source.
	LogProfile CreateFileLogProfile(Action<LogProfile> setup)
	{
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.DataSourceProvider = provider;
			it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
			it.Name = $"Test Log Profile {Guid.NewGuid()}";
			setup(it);
		});
	}


	// Create result of matching log profile.
	static LogProfileMatchingResult CreateResult(LogProfile profile, string[] fileNames, LogProfileMatchingScore score) =>
		new(profile, fileNames, score);


	/// <summary>
	/// Test for checking whether the score of matching is good enough to be treated as a match or not.
	/// </summary>
	[Test]
	public void IsMatchedTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// the full quota of logs read tightly from the head of log file is a match
			var app = this.Application;
			Assert.That(LogProfileMatcher.IsMatched(app, new(5, 1, 12, 12, false)));

			// reading no log at all is never a match
			Assert.That(LogProfileMatcher.IsMatched(app, new(0, 0, 0, 1024, true)), Is.False);

			// scraping fewer logs than the quota out of a log file which has not been read through is not a match
			Assert.That(LogProfileMatcher.IsMatched(app, new(1, 1, 1, 1024, false)), Is.False);

			// the same score is a match when the whole log file has been read, the file is simply shorter than the quota
			Assert.That(LogProfileMatcher.IsMatched(app, new(1, 1, 1, 1, true)));

			// a format which starts after a page of noise is not a match
			Assert.That(LogProfileMatcher.IsMatched(app, new(5, 200, 212, 212, true)), Is.False);

			// logs scattered once every few hundred lines are noise instead of a parse
			Assert.That(LogProfileMatcher.IsMatched(app, new(5, 1, 900, 900, true)), Is.False);
		});
	}


	// Generate log file which contains given lines.
	string GenerateLogFile(params string[] lines)
	{
		this.testDirectoryPath ??= this.Application.CreatePrivateDirectory(this.GetType().Name + "_test").FullName;
		return Tests.Random.CreateFileWithRandomName(this.testDirectoryPath).Use(stream =>
		{
			using var writer = new StreamWriter(stream, Encoding.UTF8);
			foreach (var line in lines)
				writer.WriteLine(line);
			return stream.Name;
		});
	}


	/// <summary>
	/// Test for cancelling matching of log profiles.
	/// </summary>
	[Test]
	public void MatchCancellationTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// cancelling before matching starts is reported to the caller instead of being swallowed
			var filePath = this.GenerateLogFile(SyslogLines);
			using var cancellationTokenSource = new CancellationTokenSource();
			await cancellationTokenSource.CancelAsync();
			try
			{
				await LogProfileMatcher.MatchAsync(this.Application, [ filePath ], new(), cancellationTokenSource.Token);
				throw new AssertionException("Cancellation should be reported.");
			}
			catch (Exception ex)
			{
				if (ex is AssertionException)
					throw;
				Assert.That(ex, Is.InstanceOf<OperationCanceledException>());
			}
		});
	}


	/// <summary>
	/// Test for matching log profiles for log files which contain no matchable log.
	/// </summary>
	[Test]
	public void MatchUnmatchableFilesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// a log file which contains nothing matches nothing
			var emptyFilePath = this.GenerateLogFile();
			Assert.That(await MatchNamesAsync(this.Application, emptyFilePath), Is.Empty);

			// a format which starts after a page of noise is not a match
			var noiseLines = new List<string>();
			for (var i = 0; i < 200; ++i)
				noiseLines.Add($"(noise line {i})");
			noiseLines.AddRange(SyslogLines);
			var noisyFilePath = this.GenerateLogFile(noiseLines.ToArray());
			Assert.That(await MatchNamesAsync(this.Application, noisyFilePath), Does.Not.Contain("LinuxSystemLogFile"));
		});
	}


	/// <summary>
	/// Test for matching log profiles for a single log file.
	/// </summary>
	[Test]
	public void MatchSingleFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// a syslog file is read by the profile for Linux system log files, not by the one for Apache access logs
			var syslogFilePath = this.GenerateLogFile(SyslogLines);
			var syslogNames = await MatchNamesAsync(this.Application, syslogFilePath);
			Assert.That(syslogNames, Does.Contain("LinuxSystemLogFile"));
			Assert.That(syslogNames, Does.Not.Contain("ApacheAccessLogFile"));

			// an Apache access log file is read by the profile for Apache access logs
			var apacheFilePath = this.GenerateLogFile(ApacheAccessLines);
			var apacheNames = await MatchNamesAsync(this.Application, apacheFilePath);
			Assert.That(apacheNames, Does.Contain("ApacheAccessLogFile"));
			Assert.That(apacheNames, Does.Not.Contain("LinuxSystemLogFile"));
		});
	}


	/// <summary>
	/// Test for matching a single log profile for the log profile of current session.
	/// </summary>
	[Test]
	public void MatchSingleProfileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// matching is restricted to the given log profile
			var syslogFilePath = this.GenerateLogFile(SyslogLines);
			var syslogProfile = LogProfileManager.Default.Profiles.First(it => it.Name == "LinuxSystemLogFile");
			var results = await LogProfileMatcher.MatchAsync(this.Application, [ syslogFilePath ], new(syslogProfile), CancellationToken.None);
			Assert.That(results.Select(it => it.Profile.Name).ToArray(), Is.EqualTo(new[] { "LinuxSystemLogFile" }));

			// a log profile which cannot read the log file reports no match
			var apacheProfile = LogProfileManager.Default.Profiles.First(it => it.Name == "ApacheAccessLogFile");
			Assert.That(await LogProfileMatcher.MatchAsync(this.Application, [ syslogFilePath ], new(apacheProfile), CancellationToken.None), Is.Empty);

			// a log profile without log patterns reads every text file, it matches without reading anything
			var rawProfile = LogProfileManager.Default.Profiles.First(it => it.Name == "RawFile");
			var rawResults = await LogProfileMatcher.MatchAsync(this.Application, [ syslogFilePath ], new(rawProfile), CancellationToken.None);
			Assert.That(rawResults.Select(it => it.Profile.Name).ToArray(), Is.EqualTo(new[] { "RawFile" }));
		});
	}


	/// <summary>
	/// Test for matching log profiles for multiple log files of different formats.
	/// </summary>
	[Test]
	public void MatchMultipleFilesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// dropping a syslog file together with an Apache access log file yields both profiles
			var syslogFilePath = this.GenerateLogFile(SyslogLines);
			var apacheFilePath = this.GenerateLogFile(ApacheAccessLines);
			var results = await LogProfileMatcher.MatchAsync(this.Application, [ syslogFilePath, apacheFilePath ], new(), CancellationToken.None);
			var resultsByName = results.ToDictionary(it => it.Profile.Name);
			Assert.That(resultsByName.ContainsKey("LinuxSystemLogFile"));
			Assert.That(resultsByName.ContainsKey("ApacheAccessLogFile"));

			// each of them matched exactly one of the dropped log files
			Assert.That(resultsByName["LinuxSystemLogFile"].FileNames, Is.EqualTo(new[] { syslogFilePath }));
			Assert.That(resultsByName["ApacheAccessLogFile"].FileNames, Is.EqualTo(new[] { apacheFilePath }));
		});
	}


	// Match log profiles and report their names.
	static async Task<string[]> MatchNamesAsync(IULogViewerApplication app, params string[] fileNames)
	{
		var results = await LogProfileMatcher.MatchAsync(app, fileNames, new(), CancellationToken.None);
		return results.Select(it => it.Profile.Name).ToArray();
	}


	/// <summary>
	/// Test for selecting candidates for a log file which contains JSON data.
	/// </summary>
	[Test]
	public void SelectJsonCandidatesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// no built-in log profile formats JSON data, the group is empty until user defines one
			Assert.That(SelectNames(this.Application, LogFileFormat.Json, 1), Is.Empty);

			// a user-defined profile which formats JSON data joins the group
			var profile = this.CreateFileLogProfile(it => it.DataSourceOptions = new LogDataSourceOptions { FormatJsonData = true });
			LogProfileManager.Default.AddProfile(profile);
			try
			{
				Assert.That(SelectNames(this.Application, LogFileFormat.Json, 1), Is.EqualTo(new[] { profile.Name }));
			}
			finally
			{
				LogProfileManager.Default.RemoveProfile(profile);
			}
		});
	}


	// Select names of candidates of log profile.
	static string[] SelectNames(IULogViewerApplication app, LogFileFormat format, int fileCount) =>
		LogProfileMatcher.SelectCandidates(app, format, fileCount).Select(it => it.Profile.Name).OrderBy(it => it, StringComparer.Ordinal).ToArray();


	/// <summary>
	/// Test for excluding log profiles which have no log pattern from candidates.
	/// </summary>
	[Test]
	public void SelectPatternLessCandidatesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// the built-in profile which reads raw lines matches every text file, it never joins any group
			foreach (var format in new[] { LogFileFormat.PlainText, LogFileFormat.Json, LogFileFormat.WindowsEventLog })
				Assert.That(SelectNames(this.Application, format, 1), Does.Not.Contain("RawFile"));

			// a user-defined profile built the same way is excluded as well
			var profile = this.CreateFileLogProfile(it => it.LogPatterns = []);
			LogProfileManager.Default.AddProfile(profile);
			try
			{
				Assert.That(SelectNames(this.Application, LogFileFormat.PlainText, 1), Does.Not.Contain(profile.Name));
			}
			finally
			{
				LogProfileManager.Default.RemoveProfile(profile);
			}
		});
	}


	/// <summary>
	/// Test for selecting candidates for log files which contain plain text.
	/// </summary>
	[Test]
	public void SelectPlainTextCandidatesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// the plain text group contains every built-in profile which reads log files without formatting them
			var names = SelectNames(this.Application, LogFileFormat.PlainText, 1);
			foreach (var name in PlainTextBuiltInProfileNames)
				Assert.That(names, Does.Contain(name));

			// profiles which pin their own file name are driven by that file instead of the dropped one
			Assert.That(names, Does.Not.Contain("LinuxSystemLog"));
			Assert.That(names, Does.Not.Contain("LinuxKernelLog"));
			Assert.That(names, Does.Not.Contain("MacOSInstallationLog"));

			// a profile which reads a single file cannot take a drop of multiple files
			var multipleFileNames = SelectNames(this.Application, LogFileFormat.PlainText, 2);
			Assert.That(names, Does.Contain("AndroidTraceFile"));
			Assert.That(multipleFileNames, Does.Not.Contain("AndroidTraceFile"));
		});
	}


	/// <summary>
	/// Test for selecting candidates for a Windows event log file.
	/// </summary>
	[Test]
	public void SelectWindowsEventLogCandidatesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// the group contains exactly the built-in profile which reads Windows event log files
			var names = SelectNames(this.Application, LogFileFormat.WindowsEventLog, 1);
			Assert.That(names, Is.EqualTo(new[] { "WindowsEventLogFiles" }));

			// profiles which read text never join the group
			Assert.That(names, Does.Not.Contain("ApacheAccessLogFile"));
		});
	}
}
