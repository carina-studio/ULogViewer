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
	/// Test for reading logs by built-in log profile of Nginx error log files.
	/// </summary>
	[Test]
	public void BuiltInNginxErrorLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// get log profile and data source provider
			var profile = LogProfileManager.Default.GetProfileOrDefault("NginxErrorLogFile");
			Assert.That(profile, Is.Not.Null);
			Assert.That(profile!.Icon, Is.EqualTo(LogProfileIcon.Server));
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
				throw new AssertionException("Cannot find file log data source provider.");

			// prepare log file
			var filePath = Path.GetTempFileName();
			try
			{
				File.WriteAllText(filePath, string.Join('\n', (string[])[
					"2016/10/25 14:49:34 [error] 54053#0: *1 open() \"/usr/local/Cellar/nginx/1.10.2_1/html/favicon.ico\" failed (2: No such file or directory), client: 127.0.0.1, server: localhost, request: \"GET /favicon.ico HTTP/1.1\", host: \"localhost:8080\", referrer: \"http://localhost:8080/\"",
					"2026/11/18 10:00:00 [notice] 1234#1234: signal process started",
					"2026/11/18 10:01:02 [error] 1234#0: *5 connect() failed (111: Connection refused) while connecting to upstream, client: 192.168.0.10, server: example.com, request: \"POST /api/login HTTP/1.1\", upstream: \"http://127.0.0.1:8080/api/login\", host: \"example.com\"",
					"2019/10/30 23:26:34 [error] 205860#205860: *180289 FastCGI sent in stderr: \"PHP message: PHP Warning:  Declaration of A::wrap() should be compatible",
					"PHP message: PHP Warning:  Declaration of B::wrap() should be compatible",
					"",
					"2026/11/18 10:02:00 [warn] 1234#0: *7 a client request body is buffered to a temporary file /var/cache/nginx/client_temp/0000000001, client: 10.0.0.1, server: example.com, request: \"hello\"",
					"2026/11/18 10:03:00 [emerg] 1234#0: unknown directive \"foo\" in /etc/nginx/nginx.conf:12",
					"2026/11/18 10:04:00 [crit] 1234#0: *9 SSL_do_handshake() failed (SSL: error:0A00006C:SSL routines::bad key share) while SSL handshaking, client: 10.0.0.2, server: 0.0.0.0:443",
				]));

				// read logs
				using var source = provider.CreateSource(new LogDataSourceOptions { FileName = filePath });
				using var logReader = new LogReader(null, source).Setup(it =>
				{
					it.LogLevelMap = profile.LogLevelMapForReading;
					it.LogPatterns = profile.LogPatterns;
					it.RawLogLevelPropertyName = profile.RawLogLevelPropertyName;
					it.TimestampCultureInfo = profile.TimestampCultureInfoForReading;
					it.TimestampEncoding = profile.TimestampEncodingForReading;
					it.TimestampFormats = profile.TimestampFormatsForReading;
				});
				logReader.Start();
				await logReader.WaitForPropertyChangeAsync(nameof(LogReader.State), it => it.State == LogReaderState.Stopped, new CancellationTokenSource(10000).Token);

				// check log with request context
				var logs = logReader.Logs;
				Assert.That(logs.Count, Is.EqualTo(7));
				Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2016, 10, 25, 14, 49, 34)));
				Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Error));
				Assert.That(logs[0].ProcessId, Is.EqualTo(54053));
				Assert.That(logs[0].ThreadId, Is.EqualTo(0));
				Assert.That(logs[0].Extra2?.ToString(), Is.EqualTo("1"));
				Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("127.0.0.1"));
				Assert.That(logs[0].Extra6?.ToString(), Is.EqualTo("localhost"));
				Assert.That(logs[0].Extra3?.ToString(), Is.EqualTo("GET"));
				Assert.That(logs[0].Extra4?.ToString(), Is.EqualTo("/favicon.ico"));
				Assert.That(logs[0].Message?.ToString(), Does.StartWith("open() \"/usr/local/Cellar/nginx/1.10.2_1/html/favicon.ico\" failed (2: No such file or directory)"));
				Assert.That(logs[0].Message?.ToString(), Does.Contain("client: 127.0.0.1, server: localhost"));
				Assert.That(logs[0].Message?.ToString(), Does.EndWith("referrer: \"http://localhost:8080/\""));

				// check log without connection and context
				Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Info));
				Assert.That(logs[1].ThreadId, Is.EqualTo(1234));
				Assert.That(logs[1].Extra1, Is.Null);
				Assert.That(logs[1].Extra2, Is.Null);
				Assert.That(logs[1].Message?.ToString(), Is.EqualTo("signal process started"));

				// check log of upstream error
				Assert.That(logs[2].Extra2?.ToString(), Is.EqualTo("5"));
				Assert.That(logs[2].Extra3?.ToString(), Is.EqualTo("POST"));
				Assert.That(logs[2].Extra4?.ToString(), Is.EqualTo("/api/login"));
				Assert.That(logs[2].Message?.ToString(), Does.StartWith("connect() failed (111: Connection refused) while connecting to upstream"));
				Assert.That(logs[2].Message?.ToString(), Does.Contain("upstream: \"http://127.0.0.1:8080/api/login\""));

				// check log with multiple lines
				Assert.That(logs[3].Extra2?.ToString(), Is.EqualTo("180289"));
				Assert.That(logs[3].Message?.ToString(), Does.StartWith("FastCGI sent in stderr:"));
				Assert.That(logs[3].Message?.ToString(), Does.EndWith("Declaration of B::wrap() should be compatible"));

				// check log of malformed request
				Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Warn));
				Assert.That(logs[4].Extra1?.ToString(), Is.EqualTo("10.0.0.1"));
				Assert.That(logs[4].Extra3, Is.Null);
				Assert.That(logs[4].Extra4, Is.Null);

				// check logs of fatal levels
				Assert.That(logs[5].Level, Is.EqualTo(LogLevel.Fatal));
				Assert.That(logs[5].Message?.ToString(), Is.EqualTo("unknown directive \"foo\" in /etc/nginx/nginx.conf:12"));
				Assert.That(logs[6].Level, Is.EqualTo(LogLevel.Fatal));
				Assert.That(logs[6].Extra6?.ToString(), Is.EqualTo("0.0.0.0:443"));
				Assert.That(logs[6].Extra3, Is.Null);
			}
			finally
			{
				File.Delete(filePath);
			}
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of Ollama server log files.
	/// </summary>
	[Test]
	public void BuiltInOllamaServerLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// get log profile and data source provider
			var profile = LogProfileManager.Default.GetProfileOrDefault("OllamaServerLogFile");
			Assert.That(profile, Is.Not.Null);
			Assert.That(profile!.Icon, Is.EqualTo(LogProfileIcon.Intelligence));
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
				throw new AssertionException("Cannot find file log data source provider.");

			// prepare log file
			var filePath = Path.GetTempFileName();
			try
			{
				File.WriteAllText(filePath, string.Join('\n', (string[])[
					"Couldn't find '/Users/user/.ollama/id_ed25519'. Generating new private key.",
					"",
					"time=2026-11-18T10:59:26.790+08:00 level=INFO source=routes.go:2476 msg=\"Ollama cloud disabled: false\"",
					"time=2026-11-18T10:59:27.728+08:00 level=INFO source=model_recommendations.go:177 msg=\"model recommendations cache sleep scheduled\" wait=3h23m40.931989267s consecutive_failures=0",
					"time=2026-11-18T03:01:03.385Z level=WARN source=server.go:260 msg=starting path=/models",
					"time=2026-11-18T11:01:03.388+08:00 level=INFO source=llama_server.go:1406 msg=\"waiting for llama-server to become available\" status=\"llm server not responding\"",
					"srv  llama_server: initializing ...",
					"slot launch_slot_: id  0 | task -1 | sampler params: ",
					"\trepeat_last_n = 64, repeat_penalty = 1.000",
					"time=2026-11-18T11:01:04.146+08:00 level=DEBUG source=sched.go:775 msg=\"escaped \\\"quote\\\"\"",
					"[GIN] 2026/11/18 - 11:01:04 | 200 | 961.81ms |       127.0.0.1 | POST     \"/api/generate\"",
					"[GIN] 2026/11/18 - 11:01:04 | 404 | 271.791µs |       127.0.0.1 | POST     \"/api/chat\"",
					"[GIN] 2026/11/18 - 11:01:04 | 501 |   1.25ms |       127.0.0.1 | GET      \"/api/embed\"",
				]));

				// read logs
				using var source = provider.CreateSource(new LogDataSourceOptions { FileName = filePath });
				using var logReader = new LogReader(null, source).Setup(it =>
				{
					it.LogLevelMap = profile.LogLevelMapForReading;
					it.LogPatterns = profile.LogPatterns;
					it.TimestampCultureInfo = profile.TimestampCultureInfoForReading;
					it.TimestampEncoding = profile.TimestampEncodingForReading;
					it.TimestampFormats = profile.TimestampFormatsForReading;
				});
				logReader.Start();
				await logReader.WaitForPropertyChangeAsync(nameof(LogReader.State), it => it.State == LogReaderState.Stopped, new CancellationTokenSource(10000).Token);

				// check slog logs
				var logs = logReader.Logs;
				Assert.That(logs.Count, Is.EqualTo(8));
				Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T10:59:26.790+08:00", CultureInfo.InvariantCulture).LocalDateTime));
				Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Info));
				Assert.That(logs[0].SourceName?.ToString(), Is.EqualTo("routes.go:2476"));
				Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Ollama cloud disabled: false"));
				Assert.That(logs[1].Message?.ToString(), Does.StartWith("model recommendations cache sleep scheduled"));
				Assert.That(logs[1].Message?.ToString(), Does.EndWith("wait=3h23m40.931989267s consecutive_failures=0"));
				Assert.That(logs[2].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T03:01:03.385Z", CultureInfo.InvariantCulture).LocalDateTime));
				Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Warn));
				Assert.That(logs[2].Message?.ToString(), Does.StartWith("starting"));
				Assert.That(logs[2].Message?.ToString(), Does.EndWith("path=/models"));
				Assert.That(logs[3].Message?.ToString(), Does.StartWith("waiting for llama-server to become available"));
				Assert.That(logs[3].Message?.ToString(), Does.Contain("srv  llama_server: initializing ..."));
				Assert.That(logs[3].Message?.ToString(), Does.EndWith("repeat_last_n = 64, repeat_penalty = 1.000"));
				Assert.That(logs[3].Extra5, Is.Null);
				Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Debug));
				Assert.That(logs[4].Message?.ToString(), Is.EqualTo("escaped \\\"quote\\\""));

				// check HTTP request logs
				Assert.That(logs[5].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 11, 1, 4)));
				Assert.That(logs[5].Level, Is.EqualTo(LogLevel.Info));
				Assert.That(logs[5].SourceName, Is.Null);
				Assert.That(logs[5].Extra1?.ToString(), Is.EqualTo("127.0.0.1"));
				Assert.That(logs[5].Extra3?.ToString(), Is.EqualTo("POST"));
				Assert.That(logs[5].Extra4?.ToString(), Is.EqualTo("/api/generate"));
				Assert.That(logs[5].Extra5?.ToString(), Is.EqualTo("200"));
				Assert.That(logs[5].Extra6?.ToString(), Is.EqualTo("961.81ms"));
				Assert.That(logs[5].Message?.ToString(), Does.StartWith("POST"));
				Assert.That(logs[6].Level, Is.EqualTo(LogLevel.Warn));
				Assert.That(logs[6].Extra5?.ToString(), Is.EqualTo("404"));
				Assert.That(logs[6].Extra6?.ToString(), Is.EqualTo("271.791µs"));
				Assert.That(logs[7].Level, Is.EqualTo(LogLevel.Error));
				Assert.That(logs[7].Extra3?.ToString(), Is.EqualTo("GET"));
				Assert.That(logs[7].Extra4?.ToString(), Is.EqualTo("/api/embed"));
			}
			finally
			{
				File.Delete(filePath);
			}
		});
	}


	/// <summary>
	/// Test for built-in log profiles of real-time Ollama server log.
	/// </summary>
	[Test]
	public void BuiltInOllamaServerLogTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// get log profiles
			var manager = LogProfileManager.Default;
			var serverProfile = manager.GetProfileOrDefault("OllamaServerLog");
			var serviceProfile = manager.GetProfileOrDefault("OllamaServiceLog");
			var fileProfile = manager.GetProfileOrDefault("OllamaServerLogFile");
			Assert.That(fileProfile, Is.Not.Null);
			if (Platform.IsMacOS)
			{
				Assert.That(serverProfile, Is.Not.Null);
				Assert.That(serviceProfile, Is.Null);
			}
			else if (Platform.IsLinux)
			{
				Assert.That(serverProfile, Is.Null);
				Assert.That(serviceProfile, Is.Not.Null);
			}
			else
			{
				Assert.That(serverProfile, Is.Null);
				Assert.That(serviceProfile, Is.Null);
				return;
			}
			var profile = (serverProfile ?? serviceProfile).AsNonNull();

			// check data source
			Assert.That(profile.DataSourceProvider.Name, Is.EqualTo("StandardOutput"));
			if (Platform.IsMacOS)
			{
				Assert.That(profile.DataSourceOptions.Command, Is.EqualTo("tail -F -n 0 ~/.ollama/logs/server.log"));
				Assert.That(profile.DataSourceOptions.UseTextShell);
			}
			else
			{
				Assert.That(profile.DataSourceOptions.Command, Is.EqualTo("journalctl -u ollama -f -n 0 -o cat"));
				Assert.That(profile.DataSourceOptions.UseTextShell, Is.False);
			}
			Assert.That(profile.IsContinuousReading);
			Assert.That(profile.Icon, Is.EqualTo(LogProfileIcon.Intelligence));

			// logs are parsed in the same way as log files
			Assert.That(profile.LogPatterns.Select(it => it.Regex.ToString()), Is.EqualTo(fileProfile!.LogPatterns.Select(it => it.Regex.ToString())));
			Assert.That(profile.LogLevelMapForReading, Is.EquivalentTo(fileProfile.LogLevelMapForReading));
			Assert.That(profile.TimestampFormatsForReading, Is.EqualTo(fileProfile.TimestampFormatsForReading));
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
