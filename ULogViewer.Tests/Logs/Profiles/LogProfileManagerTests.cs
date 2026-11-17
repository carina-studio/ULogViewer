using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using CarinaStudio.ULogViewer.Logs.DataSources;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Tests of <see cref="LogProfileManager"/>.
/// </summary>
[TestFixture]
class LogProfileManagerTests : ApplicationBasedTests
{
	// Fields.
	readonly List<LogProfile> addedProfiles = [];


	/// <summary>
	/// Test for adding and removing log profiles.
	/// </summary>
	[Test]
	public void AddAndRemoveProfileTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// add log profile
			var manager = LogProfileManager.Default;
			var profile = this.CreateProfile();
			manager.AddProfile(profile);
			this.addedProfiles.Add(profile);
			Assert.That(manager.Profiles, Does.Contain(profile));
			Assert.That(profile.Manager, Is.SameAs(manager));
			Assert.That(manager.GetProfileOrDefault(profile.Id), Is.SameAs(profile));

			// adding log profile which has been added is not allowed
			Assert.Throws<InvalidOperationException>(() => manager.AddProfile(profile));

			// remove log profile
			Assert.That(manager.RemoveProfile(profile));
			this.addedProfiles.Remove(profile);
			Assert.That(manager.Profiles, Does.Not.Contain(profile));
			Assert.That(profile.Manager, Is.Null);
			Assert.That(manager.GetProfileOrDefault(profile.Id), Is.Null);

			// removing log profile which has been removed is a no-op
			Assert.That(manager.RemoveProfile(profile), Is.False);
		});
	}


	/// <summary>
	/// Test for parsing timestamps of logs read by built-in log profile of Apple devices.
	/// </summary>
	[Test]
	public void BuiltInAppleDevicesLogTimestampTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// get formats of timestamp
			var profile = LogProfileManager.Default.GetProfileOrDefault("AppleDevicesLog");
			Assert.That(profile, Is.Not.Null);
			var formats = profile!.TimestampFormatsForReading.ToArray();
			var cultureInfo = profile.TimestampCultureInfoForReading;

			// parse timestamps with and without fractional seconds, and with day padded by space
			foreach (var (value, expected) in ((string, DateTime)[])[
				("Nov 17 16:14:11.122288", new DateTime(DateTime.Now.Year, 11, 17, 16, 14, 11).AddTicks(1222880)),
				("Nov 17 16:14:11", new DateTime(DateTime.Now.Year, 11, 17, 16, 14, 11)),
				("Nov  5 08:01:02.000001", new DateTime(DateTime.Now.Year, 11, 5, 8, 1, 2).AddTicks(10)),
				("Nov  5 08:01:02", new DateTime(DateTime.Now.Year, 11, 5, 8, 1, 2)),
			])
			{
				Assert.That(DateTime.TryParseExact(value, formats, cultureInfo, DateTimeStyles.None, out var timestamp), $"Unable to parse '{value}'.");
				Assert.That(timestamp, Is.EqualTo(expected));
			}
		});
	}


	/// <summary>
	/// Test for built-in log profile of real-time LM Studio server log.
	/// </summary>
	[Test]
	public void BuiltInLMStudioServerLogTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// get log profiles
			var manager = LogProfileManager.Default;
			var profile = manager.GetProfileOrDefault("LMStudioServerLog");
			var fileProfile = manager.GetProfileOrDefault("LMStudioServerLogFile");
			Assert.That(profile, Is.Not.Null);
			Assert.That(fileProfile, Is.Not.Null);

			// check data source
			Assert.That(profile!.DataSourceProvider.Name, Is.EqualTo("StandardOutput"));
			Assert.That(profile.DataSourceOptions.Command, Is.EqualTo("lms log stream --source server"));
			Assert.That(profile.IsContinuousReading);
			Assert.That(profile.Icon, Is.EqualTo(LogProfileIcon.Intelligence));

			// logs are parsed in the same way as log files
			Assert.That(profile.LogPatterns.Select(it => it.Regex.ToString()), Is.EqualTo(fileProfile!.LogPatterns.Select(it => it.Regex.ToString())));
			Assert.That(profile.LogLevelMapForReading, Is.EquivalentTo(fileProfile.LogLevelMapForReading));
			Assert.That(profile.TimestampFormatsForReading, Is.EqualTo(fileProfile.TimestampFormatsForReading));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of LM Studio server log files.
	/// </summary>
	[Test]
	public void BuiltInLMStudioServerLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// get log profile and data source provider
			var profile = LogProfileManager.Default.GetProfileOrDefault("LMStudioServerLogFile");
			Assert.That(profile, Is.Not.Null);
			Assert.That(profile!.Icon, Is.EqualTo(LogProfileIcon.Intelligence));
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
				throw new AssertionException("Cannot find file log data source provider.");

			// prepare log file
			var filePath = Path.GetTempFileName();
			try
			{
				File.WriteAllText(filePath, string.Join('\n', (string[])[
					"[2026-05-25 15:19:39][DEBUG][LMSAuthenticator][Client=plugin:installed:mcp/filesystem] Client created.",
					"[2026-05-25 15:19:40][ERROR][Plugin(mcp/filesystem)] stderr: Secure MCP Filesystem Server running on stdio",
					"",
					"[2026-05-25 15:19:40][DEBUG][LMSAuthenticator][Client=plugin:installed:mcp/filesystem][Endpoint=setToolsProvider] Registering tools provider.",
					"[2026-05-25 15:21:00][INFO] Server stopped.",
					"[2026-05-25 15:21:42][DEBUG] LlamaV4::load called with model path: /models/gemma.gguf",
					"LlamaV4::load config: n_parallel=4 n_ctx=4096 kv_unified=true",
					"[2025-09-15 15:07:55][INFO][LM STUDIO SERVER] Success! HTTP server listening on port 1234",
					"[2025-09-15 15:07:55][WARN] Just-in-time model loading active.",
					"[2026-11-17 18:12:58][DEBUG] Received request: POST to /v1/chat/completions with body {",
					"  \"model\": \"none\"",
					"}",
				]));

				// read logs
				using var source = provider.CreateSource(new LogDataSourceOptions { FileName = filePath });
				using var logReader = new LogReader(null, source).Setup(it =>
				{
					it.LogLevelMap = profile.LogLevelMapForReading;
					it.LogPatterns = profile.LogPatterns;
					it.TimestampEncoding = profile.TimestampEncodingForReading;
					it.TimestampFormats = profile.TimestampFormatsForReading;
				});
				logReader.Start();
				await logReader.WaitForPropertyChangeAsync(nameof(LogReader.State), it => it.State == LogReaderState.Stopped, new CancellationTokenSource(10000).Token);

				// check logs
				var logs = logReader.Logs;
				Assert.That(logs.Count, Is.EqualTo(8));
				Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2026, 5, 25, 15, 19, 39)));
				Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Debug));
				Assert.That(logs[0].SourceName?.ToString(), Is.EqualTo("LMSAuthenticator"));
				Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("plugin:installed:mcp/filesystem"));
				Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Client created."));
				Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Error));
				Assert.That(logs[1].SourceName?.ToString(), Is.EqualTo("Plugin(mcp/filesystem)"));
				Assert.That(logs[1].Message?.ToString(), Is.EqualTo("stderr: Secure MCP Filesystem Server running on stdio"));
				Assert.That(logs[2].Extra2?.ToString(), Is.EqualTo("setToolsProvider"));
				Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Info));
				Assert.That(logs[3].SourceName, Is.Null);
				Assert.That(logs[3].Message?.ToString(), Is.EqualTo("Server stopped."));
				Assert.That(logs[4].Message?.ToString(), Does.StartWith("LlamaV4::load called with model path: /models/gemma.gguf"));
				Assert.That(logs[4].Message?.ToString(), Does.EndWith("LlamaV4::load config: n_parallel=4 n_ctx=4096 kv_unified=true"));
				Assert.That(logs[5].SourceName?.ToString(), Is.EqualTo("LM STUDIO SERVER"));
				Assert.That(logs[6].Level, Is.EqualTo(LogLevel.Warn));
				Assert.That(logs[6].Extra3, Is.Null);
				Assert.That(logs[7].Extra3?.ToString(), Is.EqualTo("POST"));
				Assert.That(logs[7].Extra4?.ToString(), Is.EqualTo("/v1/chat/completions"));
				Assert.That(logs[7].Message?.ToString(), Does.StartWith("Received request: POST to /v1/chat/completions with body {"));
				Assert.That(logs[7].Message?.ToString(), Does.EndWith("}"));
			}
			finally
			{
				File.Delete(filePath);
			}
		});
	}


	/// <summary>
	/// Test for built-in log profiles.
	/// </summary>
	[Test]
	public void BuiltInProfilesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// built-in log profiles are loaded
			var manager = LogProfileManager.Default;
			var builtInProfiles = manager.Profiles.Where(it => it.IsBuiltIn).ToArray();
			Assert.That(builtInProfiles, Is.Not.Empty);
			foreach (var id in (string[])[ "AndroidFileLog", "RawFile", "RawHttp", "RawStandardOutput", "RawTcpServer" ])
			{
				var profile = manager.GetProfileOrDefault(id);
				Assert.That(profile, Is.Not.Null, $"Built-in log profile '{id}' was not loaded.");
				Assert.That(profile!.IsBuiltIn);
			}

			// empty log profile is available but not managed
			var emptyProfile = manager.EmptyProfile;
			Assert.That(emptyProfile.IsBuiltIn);
			Assert.That(emptyProfile.DataSourceProvider, Is.InstanceOf<EmptyLogDataSourceProvider>());
		});
	}


	// Create log profile for testing.
	LogProfile CreateProfile()
	{
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.DataSourceProvider = provider;
			it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
			it.Name = $"Test Log Profile {Guid.NewGuid()}";
		});
	}


	/// <summary>
	/// Test for adding log profile with duplicate ID.
	/// </summary>
	[Test]
	public void DuplicateIdTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// add log profile
			var manager = LogProfileManager.Default;
			var profile = this.CreateProfile();
			manager.AddProfile(profile);
			this.addedProfiles.Add(profile);

			// load another log profile with same ID
			var fileName = Path.Combine(Path.GetTempPath(), $"ULogViewer.LogProfileManagerTests.{Guid.NewGuid()}.json");
			LogProfile profileWithDuplicateId;
			try
			{
				await using (var stream = new FileStream(fileName, FileMode.Create, FileAccess.Write))
					await profile.SaveAsync(stream, true, CancellationToken.None);
				profileWithDuplicateId = await LogProfile.LoadAsync(this.Application, fileName);
			}
			finally
			{
				File.Delete(fileName);
			}
			Assert.That(profileWithDuplicateId.Id, Is.EqualTo(profile.Id));

			// ID should be changed after adding to manager
			manager.AddProfile(profileWithDuplicateId);
			this.addedProfiles.Add(profileWithDuplicateId);
			Assert.That(profileWithDuplicateId.Id, Is.Not.EqualTo(profile.Id), "ID of log profile should be changed to prevent duplication.");
			Assert.That(manager.GetProfileOrDefault(profile.Id), Is.SameAs(profile));
			Assert.That(manager.GetProfileOrDefault(profileWithDuplicateId.Id), Is.SameAs(profileWithDuplicateId));
		});
	}


	/// <summary>
	/// Test for getting log profile by ID.
	/// </summary>
	[Test]
	public void GetProfileOrDefaultTest()
	{
		this.TestOnApplicationThread(() =>
		{
			var manager = LogProfileManager.Default;
			Assert.That(manager.GetProfileOrDefault("RawFile")?.Id, Is.EqualTo("RawFile"));
			Assert.That(manager.GetProfileOrDefault("NoSuchLogProfile"), Is.Null);
		});
	}


	/// <summary>
	/// Test for pinned log profiles.
	/// </summary>
	[Test]
	public void PinnedProfilesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// add log profile
			var manager = LogProfileManager.Default;
			var profile = this.CreateProfile();
			manager.AddProfile(profile);
			this.addedProfiles.Add(profile);
			Assert.That(manager.PinnedProfiles, Does.Not.Contain(profile));

			// pin log profile
			profile.IsPinned = true;
			Assert.That(manager.PinnedProfiles, Does.Contain(profile));

			// unpin log profile
			profile.IsPinned = false;
			Assert.That(manager.PinnedProfiles, Does.Not.Contain(profile));

			// removing pinned log profile also removes it from list of pinned log profiles
			profile.IsPinned = true;
			Assert.That(manager.PinnedProfiles, Does.Contain(profile));
			manager.RemoveProfile(profile);
			this.addedProfiles.Remove(profile);
			Assert.That(manager.PinnedProfiles, Does.Not.Contain(profile));
		});
	}


	/// <summary>
	/// Test for recently used log profiles.
	/// </summary>
	[Test]
	public void RecentlyUsedProfilesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// prepare log profiles
			var manager = LogProfileManager.Default;
			manager.ResetRecentlyUsedProfiles();
			Assert.That(manager.RecentlyUsedProfiles, Is.Empty);
			var profiles = manager.Profiles.Where(it => it.IsBuiltIn).Take(10).ToArray();
			Assert.That(profiles.Length, Is.EqualTo(10), "Not enough log profiles for testing.");

			// use log profiles
			manager.SetAsRecentlyUsed(profiles[0]);
			manager.SetAsRecentlyUsed(profiles[1]);
			Assert.That(manager.RecentlyUsedProfiles, Is.EqualTo([ profiles[1], profiles[0] ]), "Recently used log profile should be the first one.");

			// use log profile which was used before
			manager.SetAsRecentlyUsed(profiles[0]);
			Assert.That(manager.RecentlyUsedProfiles, Is.EqualTo([ profiles[0], profiles[1] ]), "Log profile should be moved to the first one without duplication.");

			// number of recently used log profiles is limited
			foreach (var profile in profiles)
				manager.SetAsRecentlyUsed(profile);
			Assert.That(manager.RecentlyUsedProfiles.Count, Is.EqualTo(8));
			Assert.That(manager.RecentlyUsedProfiles[0], Is.SameAs(profiles[9]));

			// log profile which is not managed will be ignored
			var unmanagedProfile = this.CreateProfile();
			manager.SetAsRecentlyUsed(unmanagedProfile);
			Assert.That(manager.RecentlyUsedProfiles, Does.Not.Contain(unmanagedProfile));

			// removed log profile will also be removed from list of recently used log profiles
			var removedProfile = this.CreateProfile();
			manager.AddProfile(removedProfile);
			this.addedProfiles.Add(removedProfile);
			manager.SetAsRecentlyUsed(removedProfile);
			Assert.That(manager.RecentlyUsedProfiles, Does.Contain(removedProfile));
			manager.RemoveProfile(removedProfile);
			this.addedProfiles.Remove(removedProfile);
			Assert.That(manager.RecentlyUsedProfiles, Does.Not.Contain(removedProfile));

			// reset list of recently used log profiles
			manager.ResetRecentlyUsedProfiles();
			Assert.That(manager.RecentlyUsedProfiles, Is.Empty);
		});
	}


	// Remove log profiles which were added by test.
	[TearDown]
	public void RemoveAddedProfiles()
	{
		if (this.addedProfiles.IsEmpty())
			return;
		this.TestOnApplicationThread(() =>
		{
			var manager = LogProfileManager.Default;
			foreach (var profile in this.addedProfiles)
				manager.RemoveProfile(profile);
			this.addedProfiles.Clear();
			manager.ResetRecentlyUsedProfiles();
		});
	}


	/// <summary>
	/// Test for suggested log profiles.
	/// </summary>
	[Test]
	public void SuggestedProfilesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// default log profiles are suggested without pinned and recently used log profiles
			var manager = LogProfileManager.Default;
			manager.ResetRecentlyUsedProfiles();
			Assert.That(manager.PinnedProfiles, Is.Empty, "Pinned log profiles were left by other tests.");
			string[] defaultIds = Platform.IsWindows ? [ "AndroidDeviceLog", "AndroidFileLog", "RawFile" ]
				: Platform.IsMacOS ? [ "AndroidDeviceLog", "RawFile", "AndroidFileLog" ]
				: [ "LinuxSystemLogFile", "AndroidDeviceLog", "AndroidFileLog" ];
			var defaultProfiles = defaultIds.Select(id => manager.GetProfileOrDefault(id).AsNonNull()).ToArray();
			Assert.That(manager.SuggestedProfiles, Is.EqualTo(defaultProfiles));

			// prepare log profiles
			var profiles = new LogProfile[6];
			for (var i = 0; i < profiles.Length; ++i)
			{
				profiles[i] = this.CreateProfile();
				manager.AddProfile(profiles[i]);
				this.addedProfiles.Add(profiles[i]);
			}

			// recently used log profiles are suggested before default ones
			manager.SetAsRecentlyUsed(profiles[0]);
			manager.SetAsRecentlyUsed(profiles[1]);
			Assert.That(manager.SuggestedProfiles, Is.EqualTo([ profiles[1], profiles[0], defaultProfiles[0], defaultProfiles[1] ]));

			// pinned log profiles which are not recently used are suggested after recently used ones
			profiles[2].IsPinned = true;
			Assert.That(manager.SuggestedProfiles, Is.EqualTo([ profiles[1], profiles[0], profiles[2], defaultProfiles[0] ]));

			// pinned and recently used log profiles are suggested first
			profiles[0].IsPinned = true;
			Assert.That(manager.SuggestedProfiles, Is.EqualTo([ profiles[0], profiles[1], profiles[2], defaultProfiles[0] ]));

			// number of suggested log profiles is limited
			foreach (var profile in profiles)
				manager.SetAsRecentlyUsed(profile);
			Assert.That(manager.SuggestedProfiles, Is.EqualTo([ profiles[2], profiles[0], profiles[5], profiles[4] ]));

			// template log profiles are not suggested
			profiles[5].IsTemplate = true;
			Assert.That(manager.SuggestedProfiles, Is.EqualTo([ profiles[2], profiles[0], profiles[4], profiles[3] ]));

			// removed log profiles are not suggested
			manager.RemoveProfile(profiles[2]);
			this.addedProfiles.Remove(profiles[2]);
			Assert.That(manager.SuggestedProfiles, Is.EqualTo([ profiles[0], profiles[4], profiles[3], profiles[1] ]));

			// default log profiles are suggested again after resetting
			profiles[0].IsPinned = false;
			manager.ResetRecentlyUsedProfiles();
			Assert.That(manager.SuggestedProfiles, Is.EqualTo(defaultProfiles));
		});
	}
}
