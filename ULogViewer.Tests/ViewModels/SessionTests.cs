using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.Windows.Input;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Tests of <see cref="Session"/>.
/// </summary>
[TestFixture]
class SessionTests : ApplicationBasedTests
{
	// Constants.
	const int MaxContinuousLogCountForTesting = 1000;
	const string MarkedLogsFileExtension = ".ulvmark";


	// Fields.
	readonly List<string> logFilePaths = [];


	// Create log file with given number of logs.
	async Task<string> CreateLogFileAsync(int logCount)
	{
		var filePath = Path.GetTempFileName();
		this.logFilePaths.Add(filePath);
		await File.WriteAllTextAsync(filePath, string.Join('\n', Enumerable.Range(1, logCount).Select(it => $"log {it}")));
		return filePath;
	}


	// Create session which has read given number of logs from a log file.
	async Task<Session> CreateSessionWithLogsAsync(int logCount)
	{
		await SessionTestEnvironment.InitializeAsync(this.Application);
		var filePath = await this.CreateLogFileAsync(logCount);
		var session = new Session(this.Application, this.CreateFileLogProfile());
		try
		{
			session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath });
			await WaitForConditionAsync(() => session.AllLogCount == logCount, "Logs were not read.");
			return session;
		}
		catch
		{
			session.Dispose();
			throw;
		}
	}


	// Create log profile which reads logs continuously from dummy log data source.
	LogProfile CreateContinuousLogProfile()
	{
		if (!LogDataSourceProviders.TryFindProviderByName("Dummy", out var provider))
			Assert.Ignore("Dummy log data source provider is available in debug build only.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.DataSourceProvider = provider.AsNonNull();
			it.IsContinuousReading = true;
			it.LogPatterns = [ new LogPattern("^(?<Timestamp>[^\\s]+\\s[^\\s]+)\\s(?<Level>[A-Z])\\s(?<Message>.*)$", false, false, null) ];
			it.Name = "Test Continuous Log Profile";
			it.SortKey = LogSortKey.Id;
			it.VisibleLogProperties = [ new LogProperty(nameof(Log.Message), null, null, null, LogPropertyForegroundColor.Level, null) ];
		});
	}


	// Create log file with given number of logs which contain timestamp.
	async Task<string> CreateLogFileWithTimestampsAsync(int logCount)
	{
		var filePath = Path.GetTempFileName();
		this.logFilePaths.Add(filePath);
		var baseTimestamp = new DateTime(2026, 7, 26, 13, 0, 0);
		await File.WriteAllTextAsync(filePath, string.Join('\n', Enumerable.Range(0, logCount).Select(it =>
			$"{baseTimestamp.AddHours(it):yyyy-MM-dd HH:mm:ss} log {it + 1}")));
		return filePath;
	}


	// Create log profile which reads logs with timestamp from files.
	LogProfile CreateTimestampLogProfile()
	{
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.DataSourceProvider = provider;
			it.LogPatterns = [ new LogPattern("^(?<Timestamp>[^\\s]+\\s[^\\s]+)\\s(?<Message>.*)$", false, false, null) ];
			it.Name = "Test Timestamp Log Profile";
			it.SortKey = LogSortKey.Timestamp;
			it.TimestampFormatsForReading = [ "yyyy-MM-dd HH:mm:ss" ];
			it.VisibleLogProperties =
			[
				new LogProperty(nameof(Log.Timestamp), null, null, null, LogPropertyForegroundColor.Level, 100),
				new LogProperty(nameof(Log.Message), null, null, null, LogPropertyForegroundColor.Level, null),
			];
		});
	}


	// Create log profile which reads logs from files.
	LogProfile CreateFileLogProfile(bool allowMultipleFiles = true)
	{
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.AllowMultipleFiles = allowMultipleFiles;
			it.DataSourceProvider = provider;
			it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
			it.Name = "Test Log Profile";
			it.SortKey = LogSortKey.Timestamp;
			it.VisibleLogProperties = [ new LogProperty(nameof(Log.Message), null, null, null, LogPropertyForegroundColor.Level, null) ];
		});
	}


	// Delete log files which were created by test.
	[TearDown]
	public void DeleteLogFiles()
	{
		foreach (var filePath in this.logFilePaths)
		{
			Global.RunWithoutError(() => File.Delete(filePath));
			Global.RunWithoutError(() => File.Delete(filePath + MarkedLogsFileExtension));
		}
		this.logFilePaths.Clear();
	}


	/// <summary>
	/// Test for activating session.
	/// </summary>
	[Test]
	public void ActivationTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// activate session
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var session = new Session(this.Application);
			Assert.That(session.IsActivated, Is.False);
			var token = session.Activate();
			Assert.That(session.IsActivated);

			// activate session by another token
			var anotherToken = session.Activate();
			Assert.That(session.IsActivated);

			// session is still activated until all tokens are disposed
			token.Dispose();
			Assert.That(session.IsActivated, "Session should be activated until all activation tokens are disposed.");
			anotherToken.Dispose();
			Assert.That(session.IsActivated, Is.False);

			// disposing token again is a no-op
			anotherToken.Dispose();
			Assert.That(session.IsActivated, Is.False);
		});
	}


	/// <summary>
	/// Test for reading logs continuously.
	/// </summary>
	[Test]
	public void ContinuousReadingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session which reads logs continuously
			await SessionTestEnvironment.InitializeAsync(this.Application);
			this.Application.Settings.SetValue(SettingKeys.MaxContinuousLogCount, MaxContinuousLogCountForTesting);
			try
			{
				using var session = new Session(this.Application, this.CreateContinuousLogProfile());
				Assert.That(session.IsReadingLogsContinuously);
				await WaitForConditionAsync(() => session.AllLogCount > 0, "No log was read.");
				Assert.That(session.HasLogReaders);
				Assert.That(session.IsReadingLogs);
				Assert.That(session.PauseResumeLogsReadingCommand.CanExecute(null));

				// pause reading logs
				Assert.That(session.PauseResumeLogsReadingCommand.TryExecute());
				Assert.That(session.IsLogsReadingPaused);
				await Task.Delay(500);
				var lastLogId = GetLastLogId(session);
				await Task.Delay(500);
				Assert.That(GetLastLogId(session), Is.EqualTo(lastLogId), "Logs should not be read while reading logs is paused.");

				// resume reading logs
				Assert.That(session.PauseResumeLogsReadingCommand.TryExecute());
				Assert.That(session.IsLogsReadingPaused, Is.False);
				await WaitForConditionAsync(() => GetLastLogId(session) != lastLogId, "Logs should be read after resuming reading logs.");
			}
			finally
			{
				this.Application.Settings.ResetValue(SettingKeys.MaxContinuousLogCount);
			}
		});
	}


	// Get ID of the latest read log, or null if no log was read.
	static long? GetLastLogId(Session session)
	{
		var logs = session.AllLogs;
		return logs.IsNotEmpty() ? logs[^1].LogId : null;
	}


	/// <summary>
	/// Test for adding and removing log files.
	/// </summary>
	[Test]
	public void LogFileManagementTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var filePath1 = await this.CreateLogFileAsync(3);
			var filePath2 = await this.CreateLogFileAsync(2);
			using var session = new Session(this.Application, this.CreateFileLogProfile());
			Assert.That(session.CanAddLogFile);
			Assert.That(session.HasLogFiles, Is.False);
			Assert.That(session.LogFiles, Is.Empty);
			Assert.That(session.MaxLogFileCount, Is.GreaterThan(1));

			// add log file
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath1 }));
			Assert.That(session.LogFiles.Count, Is.EqualTo(1));
			Assert.That(session.HasLogFiles);
			Assert.That(session.IsLogFileAdded(filePath1));

			// add same log file again
			session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath1 });
			Assert.That(session.LogFiles.Count, Is.EqualTo(1), "Log file which has been added should be ignored.");

			// add another log file
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath2 }));
			Assert.That(session.LogFiles.Count, Is.EqualTo(2));
			await WaitForConditionAsync(() => session.AllLogCount == 5, "Logs were not read from all log files.");

			// remove log file
			Assert.That(session.RemoveLogFileCommand.TryExecute(filePath1));
			await WaitForConditionAsync(() => session.LogFiles.Count == 1, "Log file was not removed.");
			Assert.That(session.IsLogFileAdded(filePath1), Is.False);
			await WaitForConditionAsync(() => session.AllLogCount == 2, "Logs of removed log file were not dropped.");

			// clear log files
			Assert.That(session.ClearLogFilesCommand.TryExecute());
			await WaitForConditionAsync(() => session.LogFiles.IsEmpty(), "Log files were not cleared.");
			Assert.That(session.HasLogFiles, Is.False);
			Assert.That(session.AllLogCount, Is.Zero);
		});
	}


	/// <summary>
	/// Test for adding log files to log profile which does not allow multiple files.
	/// </summary>
	[Test]
	public void SingleLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var filePath1 = await this.CreateLogFileAsync(3);
			var filePath2 = await this.CreateLogFileAsync(2);
			using var session = new Session(this.Application, this.CreateFileLogProfile(false));
			Assert.That(session.MaxLogFileCount, Is.EqualTo(1));
			Assert.That(session.CanAddLogFile);

			// add log file
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath1 }));
			Assert.That(session.LogFiles.Count, Is.EqualTo(1));
			Assert.That(session.CanAddLogFile, Is.False, "Adding more log files should not be allowed.");

			// add another log file
			session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath2 });
			Assert.That(session.LogFiles.Count, Is.EqualTo(1), "Only one log file should be added.");
			Assert.That(session.IsLogFileAdded(filePath2), Is.False);
		});
	}


	/// <summary>
	/// Test for reading logs from log file.
	/// </summary>
	[Test]
	public void LogsReadingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session without log file
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var filePath = await this.CreateLogFileAsync(3);
			using var session = new Session(this.Application, this.CreateFileLogProfile());
			Assert.That(session.HasLogReaders, Is.False);
			Assert.That(session.HasLogs, Is.False);
			Assert.That(session.IsReadingLogsContinuously, Is.False);
			Assert.That(session.ReloadLogsCommand.CanExecute(null), Is.False);
			Assert.That(session.CanStopReadingLogs, Is.False);

			// read logs from log file
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath }));
			Assert.That(session.HasLogReaders);
			await WaitForConditionAsync(() => session.AllLogCount == 3, "Logs were not read.");
			await WaitForConditionAsync(() => !session.IsReadingLogs, "Reading logs was not completed.");
			Assert.That(session.HasLogs);
			Assert.That(session.AllLogs.Count, Is.EqualTo(3));
			Assert.That(session.HasLastLogsReadingDuration);
			Assert.That(session.LastLogsReadingDuration, Is.Not.Null);
			Assert.That(session.CanStopReadingLogs, Is.False, "Reading logs has been completed.");

			// reload logs after updating log file
			await File.AppendAllTextAsync(filePath, "\nlog 4\nlog 5");
			Assert.That(session.ReloadLogsCommand.TryExecute());
			await WaitForConditionAsync(() => session.AllLogCount == 5, "Logs were not reloaded.");
			Assert.That(session.LogFiles.Count, Is.EqualTo(1), "Log file should be kept after reloading logs.");
		});
	}


	/// <summary>
	/// Test for marking and unmarking logs.
	/// </summary>
	[Test]
	public void MarkingTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session with logs
			var session = await this.CreateSessionWithLogsAsync(3);
			using var sessionHolder = session;
			var logs = session.AllLogs.ToArray();
			Assert.That(session.HasMarkedLogs, Is.False);
			Assert.That(session.MarkedLogs, Is.Empty);

			// mark logs
			Assert.That(session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams { Color = MarkColor.Red, Logs = [ logs[0], logs[2] ] }));
			Assert.That(session.MarkedLogs, Is.EquivalentTo([ logs[0], logs[2] ]));
			Assert.That(session.HasMarkedLogs);
			Assert.That(logs[0].MarkedColor, Is.EqualTo(MarkColor.Red));
			Assert.That(logs[1].MarkedColor, Is.EqualTo(MarkColor.None));

			// change color of marked log
			session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams { Color = MarkColor.Blue, Logs = [ logs[0] ] });
			Assert.That(logs[0].MarkedColor, Is.EqualTo(MarkColor.Blue));
			Assert.That(session.MarkedLogs.Count, Is.EqualTo(2), "Number of marked logs should be kept after changing color.");

			// unmark log
			Assert.That(session.UnmarkLogsCommand.TryExecute(new[] { logs[0] }));
			Assert.That(session.MarkedLogs, Is.EquivalentTo([ logs[2] ]));
			Assert.That(logs[0].MarkedColor, Is.EqualTo(MarkColor.None));

			// mark log by toggling
			Assert.That(session.MarkUnmarkLogsCommand.TryExecute(new[] { logs[1] }));
			Assert.That(logs[1].MarkedColor, Is.EqualTo(MarkColor.Default));
			Assert.That(session.MarkedLogs, Is.EquivalentTo([ logs[1], logs[2] ]));

			// unmark log by toggling
			Assert.That(session.MarkUnmarkLogsCommand.TryExecute(new[] { logs[1] }));
			Assert.That(logs[1].MarkedColor, Is.EqualTo(MarkColor.None));

			// unmark all logs
			Assert.That(session.UnmarkLogsCommand.TryExecute(session.MarkedLogs.ToArray()));
			Assert.That(session.MarkedLogs, Is.Empty);
			Assert.That(session.HasMarkedLogs, Is.False);
		});
	}


	/// <summary>
	/// Test for changing properties of log to be displayed.
	/// </summary>
	[Test]
	public void DisplayLogPropertiesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var profile = this.CreateTimestampLogProfile();
			using var session = new Session(this.Application, profile);
			Assert.That(session.DisplayLogProperties.Select(it => it.Name), Is.EqualTo([ nameof(DisplayableLog.TimestampString), nameof(DisplayableLog.Message) ]));
			Assert.That(session.HasTimestampDisplayableLogProperty);

			// set width of displayed log property
			Assert.That(session.SetDisplayLogPropertyWidth(0, 200));
			Assert.That(profile.VisibleLogProperties[0].Width, Is.EqualTo(200));

			// width of auto-sized log property cannot be set
			Assert.That(session.SetDisplayLogPropertyWidth(1, 200), Is.False, "Width of auto-sized log property should not be able to be set.");

			// invalid index or width is not allowed
			Assert.That(session.SetDisplayLogPropertyWidth(-1, 200), Is.False);
			Assert.That(session.SetDisplayLogPropertyWidth(2, 200), Is.False);
			Assert.That(session.SetDisplayLogPropertyWidth(0, 0), Is.False);

			// replace displayed log property
			var newLogProperty = new LogProperty(nameof(Log.Timestamp), "Time", null, null, LogPropertyForegroundColor.None, 200);
			Assert.That(session.ReplaceVisibleLogProperty(0, newLogProperty));
			Assert.That(profile.VisibleLogProperties[0].DisplayName, Is.EqualTo("Time"));
			Assert.That(session.DisplayLogProperties[0].DisplayName, Is.EqualTo("Time"));

			// replace displayed log property by property with another name
			Assert.That(session.ReplaceVisibleLogProperty(1, new LogProperty(nameof(Log.Summary), null, null, null, LogPropertyForegroundColor.Level, null)));
			Assert.That(profile.VisibleLogProperties[1].Name, Is.EqualTo(nameof(Log.Summary)));
			await WaitForConditionAsync(() => session.DisplayLogProperties.Count == 2 && session.DisplayLogProperties[1].Name == nameof(DisplayableLog.Summary), "Displayed log properties were not updated.");

			// invalid index is not allowed
			Assert.That(session.ReplaceVisibleLogProperty(-1, newLogProperty), Is.False);
			Assert.That(session.ReplaceVisibleLogProperty(2, newLogProperty), Is.False);
		});
	}


	/// <summary>
	/// Test for saving logs to file.
	/// </summary>
	[Test]
	public void SaveLogsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session with logs
			var session = await this.CreateSessionWithLogsAsync(3);
			using var sessionHolder = session;
			session.LogProfile.AsNonNull().LogWritingFormats = [ "{Message}" ];
			Assert.That(session.IsSavingLogs, Is.False);

			// save all logs as raw text
			var fileName = Path.Combine(Path.GetTempPath(), $"ULogViewer.SessionTests.{Guid.NewGuid()}.log");
			this.logFilePaths.Add(fileName);
			var savingCompletedFileName = default(string);
			var isSavingSucceeded = false;
			session.LogsSavingCompleted += (_, savedFileName, isSucceeded) =>
			{
				savingCompletedFileName = savedFileName;
				isSavingSucceeded = isSucceeded;
			};
			Assert.That(session.SaveLogsCommand.TryExecute(new LogsSavingOptions(session.AllLogs)
			{
				FileName = fileName,
			}));
			await WaitForConditionAsync(() => savingCompletedFileName is not null, "Saving logs was not completed.");
			Assert.That(isSavingSucceeded);
			Assert.That(savingCompletedFileName, Is.EqualTo(fileName));
			Assert.That(session.IsSavingLogs, Is.False);
			Assert.That(await File.ReadAllLinesAsync(fileName), Is.EqualTo([ "log 1", "log 2", "log 3" ]));

			// save selected logs as JSON
			var jsonFileName = Path.Combine(Path.GetTempPath(), $"ULogViewer.SessionTests.{Guid.NewGuid()}.json");
			this.logFilePaths.Add(jsonFileName);
			savingCompletedFileName = null;
			Assert.That(session.SaveLogsCommand.TryExecute(new JsonLogsSavingOptions(new[] { session.AllLogs[0] })
			{
				FileName = jsonFileName,
				LogPropertyMap = new Dictionary<string, string> { [nameof(Log.Message)] = "message" },
			}));
			await WaitForConditionAsync(() => savingCompletedFileName is not null, "Saving logs as JSON was not completed.");
			Assert.That(isSavingSucceeded);
			using var jsonDocument = JsonDocument.Parse(await File.ReadAllTextAsync(jsonFileName));
			var jsonLogs = jsonDocument.RootElement.EnumerateArray().ToArray();
			Assert.That(jsonLogs.Length, Is.EqualTo(1));
			Assert.That(jsonLogs[0].GetProperty("message").GetString(), Is.EqualTo("log 1"));
		});
	}


	/// <summary>
	/// Test for saving and restoring state of session which reads logs by command.
	/// </summary>
	[Test]
	public void SaveAndRestoreCommandStateTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session which reads logs by command
			await SessionTestEnvironment.InitializeAsync(this.Application);
			if (!LogDataSourceProviders.TryFindProviderByName("StandardOutput", out var provider))
				throw new AssertionException("Cannot find standard output log data source provider.");
			var profile = new LogProfile(this.Application).Also(it =>
			{
				it.DataSourceProvider = provider;
				it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
				it.Name = "Test Command Log Profile";
			});
			LogProfileManager.Default.AddProfile(profile);
			try
			{
				// set command
				using var session = new Session(this.Application, profile);
				Assert.That(session.SetCommandCommand.TryExecute(new Session.CommandParams { Command = "echo test", UseTextShell = true }));
				Assert.That(session.Command, Is.EqualTo("echo test"));
				Assert.That(session.UseTextShellToExecuteCommand, Is.True);

				// save and restore state
				using var restoredSession = SaveAndRestoreState(session, this.Application);
				Assert.That(restoredSession.LogProfile, Is.SameAs(profile));
				Assert.That(restoredSession.Command, Is.EqualTo("echo test"));
				Assert.That(restoredSession.UseTextShellToExecuteCommand, Is.True, "Using text shell to execute command should be restored.");
			}
			finally
			{
				LogProfileManager.Default.RemoveProfile(profile);
			}
		});
	}


	/// <summary>
	/// Test for saving and restoring state of session.
	/// </summary>
	[Test]
	public void SaveAndRestoreStateTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session with log file
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var filePath = await this.CreateLogFileAsync(3);
			var profile = this.CreateFileLogProfile();
			LogProfileManager.Default.AddProfile(profile);
			try
			{
				using var session = new Session(this.Application, profile);
				session.CustomTitle = "Custom Title";
				Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams
				{
					FileName = filePath,
					ReadingWindow = LogReadingWindow.EndOfDataSource,
				}));
				await WaitForConditionAsync(() => session.AllLogCount == 3, "Logs were not read.");

				// save and restore state
				using var restoredSession = SaveAndRestoreState(session, this.Application);
				Assert.That(restoredSession.LogProfile, Is.SameAs(profile));
				Assert.That(restoredSession.CustomTitle, Is.EqualTo("Custom Title"));
				Assert.That(restoredSession.HasCustomTitle);
				Assert.That(restoredSession.LogFiles.Count, Is.EqualTo(1));
				Assert.That(restoredSession.LogFiles[0].FileName, Is.EqualTo(filePath));
				Assert.That(restoredSession.LogFiles[0].LogReadingWindow, Is.EqualTo(LogReadingWindow.EndOfDataSource), "Window of reading logs should be restored.");
				await WaitForConditionAsync(() => restoredSession.AllLogCount == 3, "Logs were not read after restoring state.");
			}
			finally
			{
				LogProfileManager.Default.RemoveProfile(profile);
			}
		});
	}


	// Save state of given session and restore it into a new session.
	static Session SaveAndRestoreState(Session session, IULogViewerApplication app)
	{
		using var stream = new MemoryStream();
		using (var jsonWriter = new Utf8JsonWriter(stream))
			session.SaveState(jsonWriter);
		using var jsonDocument = JsonDocument.Parse(stream.ToArray());
		var restoredSession = new Session(app);
		try
		{
			restoredSession.RestoreState(jsonDocument.RootElement);
			return restoredSession;
		}
		catch
		{
			restoredSession.Dispose();
			throw;
		}
	}


	/// <summary>
	/// Test for setting and resetting log profile.
	/// </summary>
	[Test]
	public void LogProfileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// check state without log profile
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var session = new Session(this.Application);
			Assert.That(session.LogProfile, Is.Null);
			Assert.That(session.HasLogProfile, Is.False);
			Assert.That(session.LogProfileName, Is.Null);
			Assert.That(session.ResetLogProfileCommand.CanExecute(null), Is.False);

			// set log profile
			var profile = this.CreateFileLogProfile();
			Assert.That(session.SetLogProfileCommand.TryExecute(profile));
			Assert.That(session.LogProfile, Is.SameAs(profile));
			Assert.That(session.HasLogProfile);
			Assert.That(session.LogProfileName, Is.EqualTo(profile.Name));
			Assert.That(session.IsBuiltInLogProfile, Is.False);
			Assert.That(session.HasLogPatterns);
			Assert.That(session.AreLogsSortedByTimestamp);
			Assert.That(session.AreDisplayLogPropertiesDefinedByLogProfile);
			Assert.That(session.DisplayLogProperties.Select(it => it.Name), Is.EqualTo([ nameof(DisplayableLog.Message) ]));
			Assert.That(session.DefinedLogPropertyNames, Is.EquivalentTo([ nameof(Log.Message) ]));

			// set log profile again
			Assert.That(session.SetLogProfileCommand.CanExecute(profile), Is.False, "Log profile should be reset before setting another one.");

			// reset log profile
			Assert.That(session.ResetLogProfileCommand.TryExecute());
			Assert.That(session.LogProfile, Is.Null);
			Assert.That(session.HasLogProfile, Is.False);
			Assert.That(session.LogProfileName, Is.Null);
			Assert.That(session.AreDisplayLogPropertiesDefinedByLogProfile, Is.False);
			Assert.That(session.DisplayLogProperties, Is.Empty);
			Assert.That(session.SetLogProfileCommand.CanExecute(profile), "Another log profile should be able to be set after resetting.");

			// set built-in log profile
			var builtInProfile = LogProfileManager.Default.GetProfileOrDefault("RawFile") ?? throw new AssertionException("Cannot find built-in log profile.");
			Assert.That(session.SetLogProfileCommand.TryExecute(builtInProfile));
			Assert.That(session.IsBuiltInLogProfile);
			Assert.That(session.LogProfileName, Is.EqualTo(builtInProfile.Name));
		});
	}


	/// <summary>
	/// Test for stopping reading logs.
	/// </summary>
	[Test]
	public void StopReadingLogsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session which reads logs continuously
			await SessionTestEnvironment.InitializeAsync(this.Application);
			this.Application.Settings.SetValue(SettingKeys.MaxContinuousLogCount, MaxContinuousLogCountForTesting);
			try
			{
				using var session = new Session(this.Application, this.CreateContinuousLogProfile());
				await WaitForConditionAsync(() => session.AllLogCount > 0, "No log was read.");
				Assert.That(session.CanStopReadingLogs);

				// stop reading logs
				Assert.That(session.StopReadingLogsCommand.TryExecute());
				await WaitForConditionAsync(() => !session.IsReadingLogs, "Reading logs was not stopped.");
				Assert.That(session.CanStopReadingLogs, Is.False);
				Assert.That(session.IsLogsReadingPaused, Is.False);
				Assert.That(session.HasLogReaders, "Log readers should be kept after stopping reading logs.");

				// check that no more log is read
				var lastLogId = GetLastLogId(session);
				await Task.Delay(500);
				Assert.That(GetLastLogId(session), Is.EqualTo(lastLogId), "Logs should not be read after stopping reading logs.");
				Assert.That(session.HasLogs, "Logs which have been read should be kept.");
			}
			finally
			{
				this.Application.Settings.ResetValue(SettingKeys.MaxContinuousLogCount);
			}
		});
	}


	/// <summary>
	/// Test for showing logs temporarily.
	/// </summary>
	[Test]
	public void TemporaryViewsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session with filtered logs
			var session = await this.CreateSessionWithLogsAsync(3);
			using var sessionHolder = session;
			var logs = session.AllLogs.ToArray();
			session.LogFiltering.TextFilter = new Regex("log 1");
			await WaitForConditionAsync(() => session.Logs.Count == 1, "Logs were not filtered.");
			Assert.That(session.IsShowingAllLogsTemporarily, Is.False);

			// show all logs temporarily
			Assert.That(session.ShowAllLogsTemporarilyCommand.TryExecute());
			Assert.That(session.IsShowingAllLogsTemporarily);
			Assert.That(session.Logs.Count, Is.EqualTo(3), "All logs should be shown temporarily.");

			// reset temporarily shown logs
			Assert.That(session.ResetTemporarilyShownLogsCommand.TryExecute());
			Assert.That(session.IsShowingAllLogsTemporarily, Is.False);
			await WaitForConditionAsync(() => session.Logs.Count == 1, "Logs should be filtered after resetting temporarily shown logs.");

			// show marked logs temporarily
			Assert.That(session.ShowMarkedLogsTemporarilyCommand.CanExecute(null), Is.False, "There is no marked log to be shown.");
			session.MarkLogsCommand.TryExecute(new Session.MarkingLogsParams { Color = MarkColor.Red, Logs = [ logs[2] ] });
			Assert.That(session.ShowMarkedLogsTemporarilyCommand.TryExecute());
			Assert.That(session.IsShowingMarkedLogsTemporarily);
			await WaitForConditionAsync(() => session.Logs.Count == 1 && session.Logs[0] == logs[2], "Marked logs should be shown temporarily.");

			// showing all logs temporarily cancels showing marked logs temporarily
			Assert.That(session.ShowAllLogsTemporarilyCommand.TryExecute());
			Assert.That(session.IsShowingMarkedLogsTemporarily, Is.False);
			Assert.That(session.IsShowingAllLogsTemporarily);
			session.ResetTemporarilyShownLogsCommand.TryExecute();

			// show raw log lines temporarily
			Assert.That(session.ToggleShowingRawLogLinesTemporarilyCommand.TryExecute());
			Assert.That(session.IsShowingRawLogLinesTemporarily);
			Assert.That(session.MarkLogsCommand.CanExecute(new Session.MarkingLogsParams { Color = MarkColor.Red, Logs = [] }), Is.False, "Marking logs should not be allowed while showing raw log lines.");
			await WaitForConditionAsync(() => session.AllLogCount == 3, "Raw log lines were not read.");

			// stop showing raw log lines temporarily
			Assert.That(session.ToggleShowingRawLogLinesTemporarilyCommand.TryExecute());
			Assert.That(session.IsShowingRawLogLinesTemporarily, Is.False);
			await WaitForConditionAsync(() => session.AllLogCount == 3, "Logs were not read.");
		});
	}


	/// <summary>
	/// Test for setting parameters of reading logs.
	/// </summary>
	[Test]
	public void SourceParametersTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// parameters are not supported by log profile which reads logs from files
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var fileSession = new Session(this.Application, this.CreateFileLogProfile());
			Assert.That(fileSession.CanSetCommand, Is.False);
			Assert.That(fileSession.CanSetUri, Is.False);
			Assert.That(fileSession.CanSetIPEndPoint, Is.False);
			Assert.That(fileSession.IsCommandSupported, Is.False);
			Assert.That(fileSession.IsUriSupported, Is.False);

			// set command and working directory
			if (!LogDataSourceProviders.TryFindProviderByName("StandardOutput", out var stdOutProvider))
				throw new AssertionException("Cannot find standard output log data source provider.");
			var commandProfile = new LogProfile(this.Application).Also(it =>
			{
				it.DataSourceProvider = stdOutProvider;
				it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
				it.Name = "Test Command Log Profile";
				it.WorkingDirectoryRequirement = LogProfilePropertyRequirement.Required;
			});
			using var commandSession = new Session(this.Application, commandProfile);
			Assert.That(commandSession.IsCommandSupported);
			Assert.That(commandSession.IsCommandNeeded);
			Assert.That(commandSession.CanSetCommand);
			Assert.That(commandSession.CanSetWorkingDirectory);
			Assert.That(commandSession.HasWorkingDirectory, Is.False);
			Assert.That(commandSession.SetCommandCommand.TryExecute(new Session.CommandParams { Command = "echo test" }));
			Assert.That(commandSession.Command, Is.EqualTo("echo test"));
			Assert.That(commandSession.IsCommandNeeded, Is.False);
			var workingDirectory = Path.TrimEndingDirectorySeparator(Path.GetTempPath());
			Assert.That(commandSession.SetWorkingDirectoryCommand.TryExecute(workingDirectory));
			Assert.That(commandSession.WorkingDirectoryPath, Is.EqualTo(workingDirectory));
			Assert.That(commandSession.WorkingDirectoryName, Is.EqualTo(Path.GetFileName(workingDirectory)));
			Assert.That(commandSession.HasWorkingDirectory);

			// relative working directory is not allowed
			Assert.Throws<ArgumentException>(() => commandSession.SetWorkingDirectoryCommand.Execute("relative/directory"));

			// set URI
			if (!LogDataSourceProviders.TryFindProviderByName("Http", out var httpProvider))
				throw new AssertionException("Cannot find HTTP log data source provider.");
			var uriProfile = new LogProfile(this.Application).Also(it =>
			{
				it.DataSourceProvider = httpProvider;
				it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
				it.Name = "Test URI Log Profile";
			});
			using var uriSession = new Session(this.Application, uriProfile);
			Assert.That(uriSession.IsUriSupported);
			Assert.That(uriSession.IsUriNeeded);
			Assert.That(uriSession.CanSetUri);
			Assert.That(uriSession.HasUri, Is.False);
			Assert.That(uriSession.SetUriCommand.TryExecute(new Uri("https://localhost/logs")));
			Assert.That(uriSession.Uri, Is.EqualTo(new Uri("https://localhost/logs")));
			Assert.That(uriSession.HasUri);
			Assert.That(uriSession.IsUriNeeded, Is.False);

			// set IP end point
			if (!LogDataSourceProviders.TryFindProviderByName("TCP Server", out var tcpProvider))
				throw new AssertionException("Cannot find TCP server log data source provider.");
			var ipEndPointProfile = new LogProfile(this.Application).Also(it =>
			{
				it.DataSourceProvider = tcpProvider;
				it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
				it.Name = "Test IP End Point Log Profile";
			});
			using var ipEndPointSession = new Session(this.Application, ipEndPointProfile);
			Assert.That(ipEndPointSession.IsIPEndPointSupported);
			Assert.That(ipEndPointSession.CanSetIPEndPoint);
			Assert.That(ipEndPointSession.HasIPEndPoint, Is.False);
			var ipEndPoint = new IPEndPoint(IPAddress.Loopback, 5566);
			Assert.That(ipEndPointSession.SetIPEndPointCommand.TryExecute(ipEndPoint));
			Assert.That(ipEndPointSession.IPEndPoint, Is.EqualTo(ipEndPoint));
			Assert.That(ipEndPointSession.HasIPEndPoint);
		});
	}


	/// <summary>
	/// Test for setting template log profile.
	/// </summary>
	[Test]
	public void TemplateLogProfileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare template log profile
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var profile = this.CreateFileLogProfile().Also(it => it.IsTemplate = true);

			// template log profile cannot be used
			using var session = new Session(this.Application);
			session.SetLogProfileCommand.TryExecute(profile);
			Assert.That(session.LogProfile, Is.Null, "Template log profile should not be used by session.");
		});
	}


	/// <summary>
	/// Test for timestamps and durations of logs.
	/// </summary>
	[Test]
	public void TimestampAndDurationTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare session with logs which contain timestamp
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var filePath = await this.CreateLogFileWithTimestampsAsync(3);
			using var session = new Session(this.Application, this.CreateTimestampLogProfile());
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath }));
			await WaitForConditionAsync(() => session.AllLogCount == 3, "Logs were not read.");
			var baseTimestamp = new DateTime(2026, 7, 26, 13, 0, 0);
			var logs = session.AllLogs.ToArray();
			Assert.That(logs[0].Timestamp, Is.EqualTo(baseTimestamp));

			// check timestamps and duration of logs
			await WaitForConditionAsync(() => session.HasLogsDuration, "Duration of logs was not reported.");
			Assert.That(session.EarliestLogTimestamp, Is.EqualTo(baseTimestamp));
			Assert.That(session.LatestLogTimestamp, Is.EqualTo(baseTimestamp.AddHours(2)));
			Assert.That(session.LogsDuration, Is.EqualTo(TimeSpan.FromHours(2)));
			Assert.That(session.HasAllLogsDuration);
			Assert.That(session.EarliestAllLogTimestamp, Is.EqualTo(baseTimestamp));
			Assert.That(session.LatestAllLogTimestamp, Is.EqualTo(baseTimestamp.AddHours(2)));
			Assert.That(session.AllLogsDuration, Is.EqualTo(TimeSpan.FromHours(2)));

			// calculate duration between logs
			var duration = Session.CalculateDurationBetweenLogs(logs[0], logs[2], out var minTimeSpan, out var maxTimeSpan, out var earliestTimestamp, out var latestTimestamp);
			Assert.That(duration, Is.EqualTo(TimeSpan.FromHours(2)));
			Assert.That(earliestTimestamp, Is.EqualTo(baseTimestamp));
			Assert.That(latestTimestamp, Is.EqualTo(baseTimestamp.AddHours(2)));
			Assert.That(minTimeSpan, Is.Null);
			Assert.That(maxTimeSpan, Is.Null);
			Assert.That(Session.CalculateDurationBetweenLogs(logs[2], logs[0], out _, out _, out _, out _), Is.EqualTo(TimeSpan.FromHours(2)), "Order of logs should not affect duration.");

			// find first and last log
			session.FindFirstAndLastLog([ logs[2], logs[0], logs[1] ], out var firstLog, out var lastLog);
			Assert.That(firstLog, Is.SameAs(logs[0]));
			Assert.That(lastLog, Is.SameAs(logs[2]));

			// find nearest log
			Assert.That(session.FindNearestLog(baseTimestamp.AddHours(1)), Is.SameAs(logs[1]), "Log with same timestamp should be found.");
			Assert.That(session.FindNearestLog(baseTimestamp.AddMinutes(90)), Is.SameAs(logs[2]), "The first log after given timestamp should be found.");
			Assert.That(session.FindNearestLog(baseTimestamp.AddDays(-1)), Is.SameAs(logs[0]));
			Assert.That(session.FindNearestLog(baseTimestamp.AddDays(1)), Is.SameAs(logs[2]));
		});
	}


	/// <summary>
	/// Test for title of session.
	/// </summary>
	[Test]
	public void TitleTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// use custom title without log profile
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var session = new Session(this.Application);
			Assert.That(session.HasCustomTitle, Is.False);
			session.CustomTitle = "Custom Title";
			Assert.That(session.HasCustomTitle);
			await WaitForConditionAsync(() => session.Title == "Custom Title", $"Unexpected title: {session.Title}.");

			// white space custom title is treated as no custom title
			session.CustomTitle = "   ";
			Assert.That(session.CustomTitle, Is.Null);
			Assert.That(session.HasCustomTitle, Is.False);

			// use name of log profile as title
			var profile = this.CreateFileLogProfile();
			Assert.That(session.SetLogProfileCommand.TryExecute(profile));
			await WaitForConditionAsync(() => session.Title == profile.Name, $"Unexpected title: {session.Title}.");

			// custom title overrides name of log profile
			session.CustomTitle = "Custom Title";
			await WaitForConditionAsync(() => session.Title == "Custom Title", $"Unexpected title: {session.Title}.");

			// custom title is kept after adding single log file
			var filePath = await this.CreateLogFileAsync(1);
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = filePath }));
			await Task.Delay(500);
			Assert.That(session.Title, Is.EqualTo("Custom Title"), "Custom title should be kept after adding log file.");

			// number of log files is shown with custom title
			var anotherFilePath = await this.CreateLogFileAsync(1);
			Assert.That(session.AddLogFileCommand.TryExecute(new Session.LogFileParams { FileName = anotherFilePath }));
			await WaitForConditionAsync(() => session.Title == "Custom Title (2)", $"Unexpected title: {session.Title}.");
		});
	}


	// Wait until given condition has been satisfied.
	static async Task WaitForConditionAsync(Func<bool> condition, string message, int timeoutMillis = 10000)
	{
		var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMillis);
		while (!condition())
		{
			if (DateTime.UtcNow >= deadline)
				throw new AssertionException(message);
			await Task.Delay(50);
		}
	}
}
