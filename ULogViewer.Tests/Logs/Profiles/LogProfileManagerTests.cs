using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using CarinaStudio.ULogViewer.Logs.DataSources;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
	/// Test for reading logs of Apache 2.2 and 2.4 by built-in log profile of Apache error log files.
	/// </summary>
	[Test]
	public void BuiltInApacheErrorLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// get log profile and data source provider
			var profile = LogProfileManager.Default.GetProfileOrDefault("ApacheErrorLogFile");
			Assert.That(profile, Is.Not.Null);
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
				throw new AssertionException("Cannot find file log data source provider.");

			// prepare log file
			var filePath = Path.GetTempFileName();
			try
			{
				File.WriteAllText(filePath, string.Join('\n', (string[])[
					"[Fri Dec 16 01:46:23 2005] [error] [client 1.2.3.4] Directory index forbidden by rule: /home/test/",
					"[Fri Sep 09 10:42:29.902022 2011] [core:error] [pid 35708:tid 4328636416] [client 72.15.99.187:52032] AH00128: File does not exist: /usr/local/apache2/htdocs/favicon.ico",
					"[Wed Oct 11 14:32:52.123456 2000] [mpm_prefork:notice] [pid 1234] AH00163: Apache/2.4.41 (Ubuntu) configured -- resuming normal operations",
					"[Sun Nov 17 10:00:00 2024] [php7:error] [pid 123] [client 1.2.3.4:5678] PHP Fatal error: oops, referer: http://example.com/",
					"Stack trace:",
					"#0 {main}",
					"[Mon Nov 18 09:00:00.000001 2024] [ssl:trace3] [pid 1:tid 2] ssl_engine_kernel.c(2283): [client ::1:58619] Handshake done",
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

				// check log of Apache 2.2
				var logs = logReader.Logs;
				Assert.That(logs.Count, Is.EqualTo(5));
				Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2005, 12, 16, 1, 46, 23)));
				Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Error));
				Assert.That(logs[0].Extra2, Is.Null);
				Assert.That(logs[0].Extra3?.ToString(), Is.EqualTo("1.2.3.4"));
				Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Directory index forbidden by rule: /home/test/"));

				// check log of Apache 2.4 with thread ID
				Assert.That(logs[1].Timestamp, Is.EqualTo(new DateTime(2011, 9, 9, 10, 42, 29).AddTicks(9020220)));
				Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Error));
				Assert.That(logs[1].Extra2?.ToString(), Is.EqualTo("core"));
				Assert.That(logs[1].ProcessId, Is.EqualTo(35708));
				Assert.That(logs[1].Extra6?.ToString(), Is.EqualTo("4328636416"));
				Assert.That(logs[1].Extra3?.ToString(), Is.EqualTo("72.15.99.187:52032"));
				Assert.That(logs[1].Extra4?.ToString(), Is.EqualTo("AH00128"));
				Assert.That(logs[1].Message?.ToString(), Is.EqualTo("AH00128: File does not exist: /usr/local/apache2/htdocs/favicon.ico"));

				// check log of Apache 2.4 without thread ID and client
				Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Info));
				Assert.That(logs[2].Extra2?.ToString(), Is.EqualTo("mpm_prefork"));
				Assert.That(logs[2].ProcessId, Is.EqualTo(1234));
				Assert.That(logs[2].Extra6, Is.Null);
				Assert.That(logs[2].Extra3, Is.Null);
				Assert.That(logs[2].Extra4?.ToString(), Is.EqualTo("AH00163"));

				// check log with referer and multiple lines
				Assert.That(logs[3].Timestamp, Is.EqualTo(new DateTime(2024, 11, 17, 10, 0, 0)));
				Assert.That(logs[3].Extra4, Is.Null);
				Assert.That(logs[3].Extra5?.ToString(), Is.EqualTo("http://example.com/"));
				Assert.That(logs[3].Message?.ToString(), Does.StartWith("PHP Fatal error: oops, referer: http://example.com/"));
				Assert.That(logs[3].Message?.ToString(), Does.EndWith("#0 {main}"));

				// check log of trace level with source file
				Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Trace));
				Assert.That(logs[4].Extra3?.ToString(), Is.EqualTo("::1:58619"));
				Assert.That(logs[4].Message?.ToString(), Is.EqualTo("Handshake done"));
			}
			finally
			{
				File.Delete(filePath);
			}
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
	/// Test for reading logs by built-in log profile of log files generated by glog or klog.
	/// </summary>
	[Test]
	public void BuiltInGlogKlogLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("GlogKlogLogFile", [
				"Log file created at: 2026/11/18 17:30:26",
				"Running on machine: my-host",
				"Running duration (h:mm:ss): 0:00:00",
				"Log line format: [IWEF]yyyymmdd hh:mm:ss.uuuuuu threadid file:line] msg",
				"I20261118 17:30:26.662066 0x208401740 server.cc:6] Server starting, port: 8080",
				"W20261118 17:30:26.663826 0x208401740 server.cc:8] Disk usage 91%",
				"E20261118 17:30:26.664182 0x208401740 server.cc:9] Failed to open config",
				"second line",
				"F20261118 17:30:26.664514 0x208401740 server.cc:10] Check failed: config != nullptr",
				"I1118 17:30:27.000001   12345 main.go:10] \"Starting server\" port=8080",
			]);

			// check logs of glog
			Assert.That(logs.Count, Is.EqualTo(5));
			Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 17, 30, 26).AddTicks(6620660)));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("0x208401740"));
			Assert.That(logs[0].SourceName?.ToString(), Is.EqualTo("server.cc:6"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Server starting, port: 8080"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("second line"));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Fatal));

			// check logs of klog without year
			Assert.That(logs[4].Timestamp, Is.EqualTo(new DateTime(DateTime.Now.Year, 11, 18, 17, 30, 27).AddTicks(10)));
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[4].Extra1?.ToString(), Is.EqualTo("12345"));
			Assert.That(logs[4].SourceName?.ToString(), Is.EqualTo("main.go:10"));
			Assert.That(logs[4].Message?.ToString(), Is.EqualTo("\"Starting server\" port=8080"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by slog or logrus of Go.
	/// </summary>
	[Test]
	public void BuiltInGoSlogLogrusLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("GoSlogLogrusLogFile", [
				"time=2026-11-18T15:30:18.019+08:00 level=DEBUG msg=\"loaded items from cart\" count=3",
				"time=2026-11-18T15:30:18.019+08:00 level=ERROR msg=\"failed to charge order\" order_id=1001 err=\"payment gateway timed out\"",
				"time=2026-11-18T07:30:18.019Z level=INFO+2 msg=\"custom level\"",
				"time=2026-11-18T15:30:18.019+08:00 level=INFO source=/src/app/main.go:22 msg=\"with source\" k=v",
				"time=\"2026-11-18T15:30:18+08:00\" level=warning msg=\"inventory is low\" sku=A-42",
				"time=\"2026-11-18T15:30:18+08:00\" level=info msg=simple",
				"panic: runtime error",
			]);

			// check logs of slog
			Assert.That(logs.Count, Is.EqualTo(6));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T15:30:18.019+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Debug));
			Assert.That(logs[0].Message?.ToString(), Does.StartWith("loaded items from cart"));
			Assert.That(logs[0].Message?.ToString(), Does.EndWith("count=3"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T07:30:18.019Z", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[2].Extra3?.ToString(), Is.EqualTo("INFO+2"));
			Assert.That(logs[3].SourceName?.ToString(), Is.EqualTo("/src/app/main.go:22"));

			// check logs of logrus
			Assert.That(logs[4].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T15:30:18+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[4].Message?.ToString(), Does.StartWith("inventory is low"));
			Assert.That(logs[5].Message?.ToString(), Does.StartWith("simple"));
			Assert.That(logs[5].Message?.ToString(), Does.EndWith("panic: runtime error"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by Hermes Agent.
	/// </summary>
	[Test]
	public void BuiltInHermesAgentLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("HermesAgentLogFile", [
				"2026-11-19 15:38:19,305 INFO agent.turn_context: Agent initialized, model: hermes-4-70b",
				"2026-11-19 15:38:19,305 INFO [local-62bd7f8c6a3e4ae7d3be74e4a81a8f73ef3f71958a89fd1e1a88c8b70902a53e] tools.web_tools: Tool call: web_search [ok], duration: 1.2s",
				"2026-11-19 15:38:19,305 WARNING [local-62bd7f8c6a3e4ae7d3be74e4a81a8f73ef3f71958a89fd1e1a88c8b70902a53e] gateway.platforms.telegram: Rate limited, retrying in 3s",
				"2026-11-19 15:38:19,305 ERROR [local-62bd7f8c6a3e4ae7d3be74e4a81a8f73ef3f71958a89fd1e1a88c8b70902a53e] tools.code_execution: Tool failed",
				"Traceback (most recent call last):",
				"  File \"/src/app/tools/code_execution.py\", line 12, in run",
				"    1/0",
				"    ~^~",
				"ZeroDivisionError: division by zero",
				"2026-11-19 15:38:19,305 CRITICAL cron.scheduler: Scheduler stopped",
				"2026-11-19 15:38:19,306 DEBUG run_agent: Debug message",
			], LogProfileIcon.Agent);

			// check logs without session
			Assert.That(logs.Count, Is.EqualTo(6));
			Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2026, 11, 19, 15, 38, 19, 305)));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("agent.turn_context"));
			Assert.That(logs[0].Extra3, Is.Null);
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Agent initialized, model: hermes-4-70b"));

			// check logs with session
			Assert.That(logs[1].Extra1?.ToString(), Is.EqualTo("tools.web_tools"));
			Assert.That(logs[1].Extra3?.ToString(), Is.EqualTo("local-62bd7f8c6a3e4ae7d3be74e4a81a8f73ef3f71958a89fd1e1a88c8b70902a53e"));
			Assert.That(logs[1].Message?.ToString(), Is.EqualTo("Tool call: web_search [ok], duration: 1.2s"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[2].Extra1?.ToString(), Is.EqualTo("gateway.platforms.telegram"));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[3].Message?.ToString(), Does.StartWith("Tool failed"));
			Assert.That(logs[3].Message?.ToString(), Does.EndWith("ZeroDivisionError: division by zero"));

			// check other levels
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Fatal));
			Assert.That(logs[4].Extra3, Is.Null);
			Assert.That(logs[5].Timestamp, Is.EqualTo(new DateTime(2026, 11, 19, 15, 38, 19, 306)));
			Assert.That(logs[5].Level, Is.EqualTo(LogLevel.Debug));
		});
	}


	/// <summary>
	/// Test for built-in log profile of real-time Hermes Agent log.
	/// </summary>
	[Test]
	public void BuiltInHermesAgentLogTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// get log profiles
			var manager = LogProfileManager.Default;
			var profile = manager.GetProfileOrDefault("HermesAgentLog");
			var fileProfile = manager.GetProfileOrDefault("HermesAgentLogFile");
			Assert.That(fileProfile, Is.Not.Null);
			if (Platform.IsWindows)
			{
				Assert.That(profile, Is.Null);
				return;
			}
			Assert.That(profile, Is.Not.Null);

			// check data source
			Assert.That(profile!.DataSourceProvider.Name, Is.EqualTo("StandardOutput"));
			Assert.That(profile.DataSourceOptions.Command, Is.EqualTo("tail -F -n 0 ~/.hermes/logs/agent.log"));
			Assert.That(profile.DataSourceOptions.UseTextShell);
			Assert.That(profile.IsContinuousReading);
			Assert.That(profile.Icon, Is.EqualTo(LogProfileIcon.Agent));

			// logs are parsed in the same way as log files
			Assert.That(profile.LogPatterns.Select(it => it.Regex.ToString()), Is.EqualTo(fileProfile!.LogPatterns.Select(it => it.Regex.ToString())));
			Assert.That(profile.LogLevelMapForReading, Is.EquivalentTo(fileProfile.LogLevelMapForReading));
			Assert.That(profile.RawLogLevelPropertyName, Is.EqualTo(fileProfile.RawLogLevelPropertyName));
			Assert.That(profile.TimestampFormatsForReading, Is.EqualTo(fileProfile.TimestampFormatsForReading));
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
	/// Test for reading logs by built-in log profile of log files generated by Log4j or Logback.
	/// </summary>
	[Test]
	public void BuiltInLog4jLogbackLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("Log4jLogbackLogFile", [
				"17:32:17.779 [main] DEBUG demo.App -- Debug message",
				"17:32:17.781 [main] INFO demo.App -user=\"alice\"- With key value",
				"17:34:35.269 [main] ERROR com.example.demo.App - Failed to parse quantity",
				"java.lang.NumberFormatException: For input string: \"x\"",
				"\tat demo.App.main(App.java:10)",
				"2026-11-18 17:34:35.270 [worker-1] FATAL demo.App - Fatal - from worker",
				"2026-11-18 17:32:17,779 [1] INFO  MyApp.Program - log4net message",
			]);

			// check logs of Logback
			Assert.That(logs.Count, Is.EqualTo(5));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTime.Today.Add(new TimeSpan(0, 17, 32, 17, 779))));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Debug));
			Assert.That(logs[0].ThreadName?.ToString(), Is.EqualTo("main"));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("demo.App"));
			Assert.That(logs[0].Extra3, Is.Null);
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Debug message"));
			Assert.That(logs[1].Extra3?.ToString(), Is.EqualTo("user=\"alice\""));
			Assert.That(logs[1].Message?.ToString(), Is.EqualTo("With key value"));

			// check logs of Log4j
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Extra1?.ToString(), Is.EqualTo("com.example.demo.App"));
			Assert.That(logs[2].Message?.ToString(), Does.StartWith("Failed to parse quantity"));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("(App.java:10)"));
			Assert.That(logs[3].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 17, 34, 35, 270)));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Fatal));
			Assert.That(logs[3].ThreadName?.ToString(), Is.EqualTo("worker-1"));
			Assert.That(logs[3].Message?.ToString(), Is.EqualTo("Fatal - from worker"));

			// check logs of log4net
			Assert.That(logs[4].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 17, 32, 17, 779)));
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[4].ThreadName?.ToString(), Is.EqualTo("1"));
			Assert.That(logs[4].Extra1?.ToString(), Is.EqualTo("MyApp.Program"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by Loguru.
	/// </summary>
	[Test]
	public void BuiltInLoguruLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("LoguruLogFile", [
				"2026-11-18 17:29:56.674 +08:00 | TRACE    | __main__:handle:7 - Entering handle, order: 3",
				"2026-11-18 17:29:56.675 +08:00 | SUCCESS  | __main__:handle:10 - Order 3 shipped",
				"2026-11-18 17:29:56.675 +08:00 | ERROR    | __main__:handle:16 - Failed to price order 3",
				"Traceback (most recent call last):",
				"",
				"> File \"/src/app/app.py\", line 14, in handle",
				"    1 / 0",
				"",
				"ZeroDivisionError: division by zero",
				"2026-11-18 17:29:56.675 | WARNING  | __main__:<module>:20 - Without time zone",
				"2026-11-18 17:29:56.676 +08:00 | CRITICAL | __main__:<module>:21 - Shutting down",
			]);

			// check logs
			Assert.That(logs.Count, Is.EqualTo(5));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T17:29:56.674+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Trace));
			Assert.That(logs[0].SourceName?.ToString(), Is.EqualTo("__main__:handle:7"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Entering handle, order: 3"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[1].Extra2?.ToString(), Is.EqualTo("SUCCESS"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Message?.ToString(), Does.StartWith("Failed to price order 3"));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("ZeroDivisionError: division by zero"));
			Assert.That(logs[3].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 17, 29, 56, 675)));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[3].SourceName?.ToString(), Is.EqualTo("__main__:<module>:20"));
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Fatal));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by Monolog of PHP.
	/// </summary>
	[Test]
	public void BuiltInMonologLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("MonologLogFile", [
				"[2026-11-18T07:30:37.558351+00:00] app.DEBUG: Loaded items from cart {\"count\":3} []",
				"[2026-11-18T07:30:37.559219+00:00] app.NOTICE: User logged in [] []",
				"[2026-11-18T07:30:37.559312+00:00] app.CRITICAL: Database unreachable [] []",
				"[2026-11-18T07:30:37.559485+00:00] my.channel.ALERT: Disk almost full [] []",
				"[2026-11-18 07:30:37] local.ERROR: Failed to charge order {\"exception\":\"[object] (RuntimeException(code: 0): Payment gateway timed out at /app/routes/web.php:28)",
				"[stacktrace]",
				"#0 {main}",
				"\"} ",
			]);

			// check logs of default format
			Assert.That(logs.Count, Is.EqualTo(5));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T07:30:37.558351+00:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Debug));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("app"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Loaded items from cart {\"count\":3} []"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Fatal));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[3].Extra1?.ToString(), Is.EqualTo("my.channel"));

			// check log of Laravel with stack trace
			Assert.That(logs[4].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 7, 30, 37)));
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[4].Extra1?.ToString(), Is.EqualTo("local"));
			Assert.That(logs[4].Message?.ToString(), Does.StartWith("Failed to charge order"));
			Assert.That(logs[4].Message?.ToString(), Does.Contain("#0 {main}"));
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
	/// Test for reading logs by built-in log profile of log files generated by NLog.
	/// </summary>
	[Test]
	public void BuiltInNLogLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("NLogLogFile", [
				"2026-11-18 15:29:04.0034|TRACE|MyApp.Services.OrderService|Entering PlaceOrder, order ID: 1001",
				"2026-11-18 15:29:04.0175|INFO|MyApp.Services.OrderService|Multi-line message:",
				"line 2",
				"2026-11-18 15:29:04.0184|ERROR|MyApp.Services.OrderService|Failed to charge order 1001|System.InvalidOperationException: Payment gateway timed out",
				"   at Program.<Main>$(String[] args) in /src/app/Program.cs:line 16",
				"2026-11-18 15:29:04.0225|FATAL|app.Program|Unrecoverable state, shutting down",
			]);

			// check logs
			Assert.That(logs.Count, Is.EqualTo(4));
			Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 15, 29, 4).AddTicks(34000)));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Trace));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("MyApp.Services.OrderService"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Entering PlaceOrder, order ID: 1001"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[1].Message?.ToString(), Does.EndWith("line 2"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Message?.ToString(), Does.StartWith("Failed to charge order 1001|System.InvalidOperationException: Payment gateway timed out"));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("in /src/app/Program.cs:line 16"));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Fatal));
			Assert.That(logs[3].Extra1?.ToString(), Is.EqualTo("app.Program"));
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
	/// Test for reading logs by built-in log profile of log files generated by the gateway of OpenClaw.
	/// </summary>
	[Test]
	public void BuiltInOpenClawLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("OpenClawLogFile", [
				"""{"0":"{\"subsystem\":\"gateway\"}","1":"loading configuration…","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:31:22.526Z","logLevelId":3,"logLevelName":"INFO","name":"{\"subsystem\":\"gateway\"}","parentNames":["openclaw"]},"time":"2026-11-19T16:31:22.526+08:00","hostname":"demo-mac.local","message":"loading configuration…"}""",
				"""{"0":"{\"module\":\"cron\",\"storeKey\":\"/Users/demo/.openclaw/cron/jobs.json\"}","1":{"jobId":"625fe4cf-0281-451d-b900-2636ba6bb443","jobName":"Memory Dreaming Promotion","nextRunAtMs":1795114800000,"cronEnabled":true},"2":"cron: job added","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:31:27.195Z","logLevelId":3,"logLevelName":"INFO","name":"{\"module\":\"cron\",\"storeKey\":\"/Users/demo/.openclaw/cron/jobs.json\"}","parentNames":["openclaw"],"path":{"fullFilePath":"file:///Users/demo/openclaw/node_modules/openclaw/dist/logger-dFyNvOA3.mjs:788:21","fileName":"logger-dFyNvOA3.mjs","fileNameWithLine":"logger-dFyNvOA3.mjs:788","fileColumn":"21","fileLine":"788","filePath":"node_modules/openclaw/dist/logger-dFyNvOA3.mjs","filePathWithLine":"node_modules/openclaw/dist/logger-dFyNvOA3.mjs:788","method":"Object.info"}},"time":"2026-11-19T16:31:27.195+08:00","hostname":"demo-mac.local","message":"cron: job added"}""",
				"""{"0":"{\"subsystem\":\"memory\"}","1":"memory_index_chunks_vec not updated — semantic vector embeddings unavailable — no vector dimensions resolved. Vector recall degraded. Further duplicate warnings suppressed.","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:32:14.573Z","logLevelId":4,"logLevelName":"WARN","name":"{\"subsystem\":\"memory\"}","parentNames":["openclaw"]},"time":"2026-11-19T16:32:14.573+08:00","hostname":"demo-mac.local","message":"memory_index_chunks_vec not updated — semantic vector embeddings unavailable — no vector dimensions resolved. Vector recall degraded. Further duplicate warnings suppressed."}""",
				"""{"0":"{\"subsystem\":\"gateway\"}","1":"native runtime: {\"pid\":41356,\"platform\":\"darwin\",\"arch\":\"arm64\",\"node\":\"26.3.1\",\"v8\":\"14.6.202.34-node.20\",\"uv\":\"1.52.1\",\"openssl\":\"3.6.3\",\"sqlite\":\"3.53.2\",\"sqliteClose\":{\"explicitSqliteCloseReleasesNativeResources\":true,\"decided\":true,\"reason\":\"Node runtime\"}}","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:31:26.567Z","logLevelId":3,"logLevelName":"INFO","name":"{\"subsystem\":\"gateway\"}","parentNames":["openclaw"]},"time":"2026-11-19T16:31:26.567+08:00","hostname":"demo-mac.local","message":"native runtime: {\"pid\":41356,\"platform\":\"darwin\",\"arch\":\"arm64\",\"node\":\"26.3.1\",\"v8\":\"14.6.202.34-node.20\",\"uv\":\"1.52.1\",\"openssl\":\"3.6.3\",\"sqlite\":\"3.53.2\",\"sqliteClose\":{\"explicitSqliteCloseReleasesNativeResources\":true,\"decided\":true,\"reason\":\"Node runtime\"}}"}""",
				"""{"0":"There are 2 fruits listed in `fruits.txt`: apple and banana.","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:32:47.599Z","logLevelId":3,"logLevelName":"INFO","name":"openclaw","path":{"fullFilePath":"file:///Users/demo/openclaw/node_modules/openclaw/dist/console-C4i9dif8.mjs:178:19","fileName":"console-C4i9dif8.mjs","fileNameWithLine":"console-C4i9dif8.mjs:178","fileColumn":"19","fileLine":"178","filePath":"/Users/demo/openclaw/node_modules/openclaw/dist/console-C4i9dif8.mjs","filePathWithLine":"/Users/demo/openclaw/node_modules/openclaw/dist/console-C4i9dif8.mjs:178","method":"console.forwardedConsoleCall [as log]"}},"time":"2026-11-19T16:32:47.600+08:00","hostname":"demo-mac.local","message":"There are 2 fruits listed in `fruits.txt`: apple and banana."}""",
				"""{"0":"{\"subsystem\":\"state/agent-db\"}","1":{"agentId":"main","path":"/Users/demo/.openclaw/agents/main/agent/openclaw-agent.sqlite","leaseId":"5b37bfa6-6787-4d04-83f1-261fb1955162","receipt":"written"},"2":"agent database clean-close receipt: written; agent=main","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:32:52.882Z","logLevelId":3,"logLevelName":"INFO","name":"{\"subsystem\":\"state/agent-db\"}","parentNames":["openclaw"]},"time":"2026-11-19T16:32:52.882+08:00","hostname":"demo-mac.local","message":"agent database clean-close receipt: written; agent=main","agent_id":"main"}""",
				"""{"0":"","_meta":{"runtime":"node","runtimeVersion":"26.3.1","hostname":"demo-mac.local","date":"2026-11-19T08:30:44.407Z","logLevelId":5,"logLevelName":"ERROR","name":"openclaw","path":{"fullFilePath":"file:///Users/demo/openclaw/node_modules/openclaw/dist/console-C4i9dif8.mjs:178:19","fileName":"console-C4i9dif8.mjs","fileNameWithLine":"console-C4i9dif8.mjs:178","fileColumn":"19","fileLine":"178","filePath":"node_modules/openclaw/dist/console-C4i9dif8.mjs","filePathWithLine":"node_modules/openclaw/dist/console-C4i9dif8.mjs:178","method":"console.forwardedConsoleCall [as error]"}},"time":"2026-11-19T16:30:44.407+08:00","hostname":"demo-mac.local"}""",
			], LogProfileIcon.Agent);

			// check logs of subsystems
			Assert.That(logs.Count, Is.EqualTo(7));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-19T16:31:22.526+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[0].SourceName?.ToString(), Is.EqualTo("gateway"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("loading configuration…"));
			Assert.That(logs[1].SourceName?.ToString(), Is.EqualTo("cron"));
			Assert.That(logs[1].Message?.ToString(), Is.EqualTo("cron: job added"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[2].SourceName?.ToString(), Is.EqualTo("memory"));

			// message is kept in JSON-escaped form
			Assert.That(logs[3].Message?.ToString(), Does.StartWith("native runtime: {\\\"pid\\\":41356,"));

			// check log without subsystem
			Assert.That(logs[4].SourceName, Is.Null);
			Assert.That(logs[4].Extra1, Is.Null);
			Assert.That(logs[4].Message?.ToString(), Is.EqualTo("There are 2 fruits listed in `fruits.txt`: apple and banana."));

			// check log with agent
			Assert.That(logs[5].SourceName?.ToString(), Is.EqualTo("state/agent-db"));
			Assert.That(logs[5].Extra1?.ToString(), Is.EqualTo("main"));
			Assert.That(logs[5].Extra3, Is.Null);

			// check log without message
			Assert.That(logs[6].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-19T16:30:44.407+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[6].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[6].SourceName, Is.Null);
			Assert.That(logs[6].Message, Is.Null);
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


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by Logger of Ruby.
	/// </summary>
	[Test]
	public void BuiltInRubyLoggerLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("RubyLoggerLogFile", [
				"# Logfile created on 2026-11-18 15:29:06 +0800 by logger.rb/66358",
				"D, [2026-11-18T15:29:06.505870 #45085] DEBUG -- : Loaded 3 items from cart",
				"I, [2026-11-18T15:29:06.505899 #45085]  INFO -- OrderService: Order 1001 placed by alice",
				"E, [2026-11-18T15:29:06.505919 #45085] ERROR -- OrderService: Failed to charge order 1001: Payment gateway timed out (ArgumentError)",
				"/app/order.rb:12:in `<main>\'",
				"A, [2026-11-18T15:29:06.505929 #45085]   ANY -- Admin::Console: Unknown severity message",
			]);

			// check logs
			Assert.That(logs.Count, Is.EqualTo(4));
			Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 15, 29, 6).AddTicks(5058700)));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Debug));
			Assert.That(logs[0].ProcessId, Is.EqualTo(45085));
			Assert.That(logs[0].Extra1?.ToString() ?? "", Is.Empty);
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Loaded 3 items from cart"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[1].Extra1?.ToString(), Is.EqualTo("OrderService"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("/app/order.rb:12:in `<main>'"));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Undefined));
			Assert.That(logs[3].Extra1?.ToString(), Is.EqualTo("Admin::Console"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by env_logger or tracing of Rust.
	/// </summary>
	[Test]
	public void BuiltInRustEnvLoggerTracingLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("RustEnvLoggerTracingLogFile", [
				"[2026-11-18T09:30:46Z TRACE rlog] trace message",
				"[2026-11-18T09:30:46Z INFO  my_app::server] server started on port 8080",
				"[2026-11-18T09:30:46Z ERROR my_app] failed",
				"    second line",
				"2026-11-18T09:30:46.590110Z DEBUG my_app: debug message user=\"alice\"",
				"2026-11-18T09:30:46.590254Z  INFO handle{order=7}: my_app::orders: accepted order=7",
				"2026-11-18T09:30:46.590283Z  WARN handle{order=7}:pricing{currency=\"USD\"}: my_app::orders: price missing",
				"2026-11-18T09:30:46.590332Z  INFO outer: my_app: inside span without fields",
				"2026-11-18T09:30:46.590400Z ERROR my_app::db: connection lost, retrying",
			]);

			// check logs of env_logger
			Assert.That(logs.Count, Is.EqualTo(8));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T09:30:46Z", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Trace));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("rlog"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("trace message"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Info));
			Assert.That(logs[1].Extra1?.ToString(), Is.EqualTo("my_app::server"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("second line"));

			// check logs of tracing
			Assert.That(logs[3].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T09:30:46.590110Z", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Debug));
			Assert.That(logs[3].Extra2, Is.Null);
			Assert.That(logs[3].Extra1?.ToString(), Is.EqualTo("my_app"));
			Assert.That(logs[3].Message?.ToString(), Is.EqualTo("debug message user=\"alice\""));
			Assert.That(logs[4].Extra2?.ToString(), Is.EqualTo("handle{order=7}"));
			Assert.That(logs[4].Extra1?.ToString(), Is.EqualTo("my_app::orders"));
			Assert.That(logs[4].Message?.ToString(), Is.EqualTo("accepted order=7"));
			Assert.That(logs[5].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[5].Extra2?.ToString(), Is.EqualTo("handle{order=7}:pricing{currency=\"USD\"}"));
			Assert.That(logs[6].Extra2?.ToString(), Is.EqualTo("outer"));
			Assert.That(logs[6].Extra1?.ToString(), Is.EqualTo("my_app"));
			Assert.That(logs[7].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[7].Extra1?.ToString(), Is.EqualTo("my_app::db"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by Serilog.
	/// </summary>
	[Test]
	public void BuiltInSerilogLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("SerilogLogFile", [
				"2026-11-18 15:29:04.892 +08:00 [VRB] Entering PlaceOrder, order ID: 1001",
				"2026-11-18 15:29:04.906 +08:00 [INF] Multi-line message:",
				"line 2",
				"2026-11-18 07:29:04.907 +00:00 [ERR] Failed to charge order 1001",
				"System.InvalidOperationException: Payment gateway timed out",
				"   at Program.<Main>$(String[] args) in /src/app/Program.cs:line 10",
				"2026-11-18 15:29:04.912 +08:00 [FTL] Unrecoverable state, shutting down",
			]);

			// check logs
			Assert.That(logs.Count, Is.EqualTo(4));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T15:29:04.892+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Verbose));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Entering PlaceOrder, order ID: 1001"));
			Assert.That(logs[1].Message?.ToString(), Does.EndWith("line 2"));
			Assert.That(logs[2].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T07:29:04.907+00:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].Message?.ToString(), Does.Contain("System.InvalidOperationException: Payment gateway timed out"));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Fatal));
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
	/// Test for reading logs by built-in log profile of log files generated by spdlog.
	/// </summary>
	[Test]
	public void BuiltInSpdlogLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("SpdlogLogFile", [
				"[2026-11-18 17:30:25.992] [trace] Default logger trace 1",
				"[2026-11-18 17:30:25.993] [network] [warning] Latency 230 ms",
				"[2026-11-18 17:30:25.993] [network] [critical] Giving up after 3 retries",
				"[2026-11-18 17:30:25.993] [network] [info] [net.cpp:17] With source location",
				"[2026-11-18 17:30:25.993] [info] [main.cpp:18] Default with source location",
				"[2026-11-18 17:30:25.993] [network] [error] Multi-line",
				"second line",
			]);

			// check logs
			Assert.That(logs.Count, Is.EqualTo(6));
			Assert.That(logs[0].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 17, 30, 25, 992)));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Trace));
			Assert.That(logs[0].Extra1, Is.Null);
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Default logger trace 1"));
			Assert.That(logs[1].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[1].Extra1?.ToString(), Is.EqualTo("network"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Fatal));
			Assert.That(logs[3].Extra1?.ToString(), Is.EqualTo("network"));
			Assert.That(logs[3].SourceName?.ToString(), Is.EqualTo("net.cpp:17"));
			Assert.That(logs[3].Message?.ToString(), Is.EqualTo("With source location"));
			Assert.That(logs[4].Extra1, Is.Null);
			Assert.That(logs[4].SourceName?.ToString(), Is.EqualTo("main.cpp:18"));
			Assert.That(logs[5].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[5].Message?.ToString(), Does.EndWith("second line"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of log files generated by Spring Boot.
	/// </summary>
	[Test]
	public void BuiltInSpringBootLogFileTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// read logs
			var logs = await ReadLogsAsync("SpringBootLogFile", [
				"2026-11-18T17:31:56.650+08:00 DEBUG 79786 --- [main] c.e.demo.DemoApplication : Running with Spring Boot v4.1.2",
				"2026-11-18T17:31:57.282+08:00  INFO 79787 --- [orders] [main] c.e.demo.DemoApplication : Processing order 42",
				"2026-11-18T17:31:57.282+08:00 ERROR 79787 --- [orders] [order-worker-1] c.e.demo.DemoApplication : Failed to parse quantity",
				"",
				"java.lang.NumberFormatException: For input string: \"x\"",
				"\tat java.base/java.lang.Integer.parseInt(Integer.java:662) ~[na:na]",
				"2026-11-18T17:31:57.284+08:00  WARN 79787 --- [orders] [           main] [0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331] c.e.demo.DemoApplication                 : Traced",
				"2026-11-18T17:31:57.285+08:00  INFO 79787 --- [           main] [                                                 ] o.s.b.w.e.tomcat.TomcatWebServer         : Not traced",
				"2026-11-18 17:31:57.715  INFO 79788 --- [           main] o.s.b.w.e.tomcat.TomcatWebServer         : Spring Boot 2",
			]);

			// check logs of Spring Boot 3 or later
			Assert.That(logs.Count, Is.EqualTo(6));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.Parse("2026-11-18T17:31:56.650+08:00", CultureInfo.InvariantCulture).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Debug));
			Assert.That(logs[0].ProcessId, Is.EqualTo(79786));
			Assert.That(logs[0].Extra2, Is.Null);
			Assert.That(logs[0].ThreadName?.ToString(), Is.EqualTo("main"));
			Assert.That(logs[0].Extra1?.ToString(), Is.EqualTo("c.e.demo.DemoApplication"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Running with Spring Boot v4.1.2"));
			Assert.That(logs[1].Extra2?.ToString(), Is.EqualTo("orders"));
			Assert.That(logs[1].ThreadName?.ToString(), Is.EqualTo("main"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[2].ThreadName?.ToString(), Is.EqualTo("order-worker-1"));
			Assert.That(logs[2].Message?.ToString(), Does.StartWith("Failed to parse quantity"));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("(Integer.java:662) ~[na:na]"));

			// check logs with correlation ID
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[3].Extra2?.ToString(), Is.EqualTo("orders"));
			Assert.That(logs[3].ThreadName?.ToString(), Is.EqualTo("main"));
			Assert.That(logs[3].Extra3?.ToString(), Is.EqualTo("0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331"));
			Assert.That(logs[3].Message?.ToString(), Is.EqualTo("Traced"));
			Assert.That(logs[4].Extra2, Is.Null);
			Assert.That(logs[4].Extra3, Is.Null);
			Assert.That(logs[4].Extra1?.ToString(), Is.EqualTo("o.s.b.w.e.tomcat.TomcatWebServer"));

			// check logs of Spring Boot 2
			Assert.That(logs[5].Timestamp, Is.EqualTo(new DateTime(2026, 11, 18, 17, 31, 57, 715)));
			Assert.That(logs[5].ProcessId, Is.EqualTo(79788));
			Assert.That(logs[5].Message?.ToString(), Is.EqualTo("Spring Boot 2"));
		});
	}


	/// <summary>
	/// Test for reading logs by built-in log profile of NLog through UDP.
	/// </summary>
	[Test]
	public void BuiltInUdpNLogTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// get log profiles
			var manager = LogProfileManager.Default;
			var profile = manager.GetProfileOrDefault("UdpNLog");
			var tcpProfile = manager.GetProfileOrDefault("TcpNLog");
			Assert.That(profile, Is.Not.Null);
			Assert.That(tcpProfile, Is.Not.Null);
			Assert.That(profile!.DataSourceProvider.Name, Is.EqualTo("UDP Server"));
			Assert.That(profile.DataSourceOptions.IsLineAcrossDatagramsAllowed, Is.False);
			Assert.That(profile.IsContinuousReading);
			Assert.That(profile.Icon, Is.EqualTo(LogProfileIcon.Log));

			// logs are parsed in the same way as NLog through TCP
			Assert.That(profile.LogPatterns.Select(it => it.Regex.ToString()), Is.EqualTo(tcpProfile!.LogPatterns.Select(it => it.Regex.ToString())));

			// find available port
			int port;
			using (var probingClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
				port = ((IPEndPoint)probingClient.Client.LocalEndPoint.AsNonNull()).Port;
			var endPoint = new IPEndPoint(IPAddress.Loopback, port);

			// start reading logs
			var options = profile.DataSourceOptions;
			options.IPEndPoint = endPoint;
			using var source = profile.DataSourceProvider.CreateSource(options);
			using var logReader = new LogReader(null, source).Setup(it =>
			{
				it.IsContinuousReading = true;
				it.LogLevelMap = profile.LogLevelMapForReading;
				it.LogPatterns = profile.LogPatterns;
				it.LogStringEncoding = profile.LogStringEncodingForReading;
				it.TimestampEncoding = profile.TimestampEncodingForReading;
			});
			logReader.Start();
			await logReader.WaitForPropertyChangeAsync(nameof(LogReader.State), it => it.State == LogReaderState.ReadingLogs, new CancellationTokenSource(10000).Token);

			// send each log in a datagram without trailing line break, as NLog does
			using (var client = new UdpClient())
			{
				foreach (var datagram in (string[])[
					"<log4j:event logger=\"MyApp.OrderService\" level=\"TRACE\" timestamp=\"1794994434798\" thread=\"1\"><log4j:message>Entering PlaceOrder</log4j:message><log4j:properties><log4j:data name=\"log4japp\" value=\"app(80279)\"/><log4j:data name=\"log4jmachinename\" value=\"my-host\"/></log4j:properties></log4j:event>",
					"<log4j:event logger=\"MyApp.OrderService\" level=\"INFO\" timestamp=\"1794994434807\" thread=\"1\"><log4j:message>Order 1001 placed</log4j:message><log4j:properties><log4j:data name=\"log4japp\" value=\"app(80279)\"/><log4j:data name=\"log4jmachinename\" value=\"my-host\"/></log4j:properties></log4j:event>",
					"<log4j:event logger=\"MyApp.OrderService\" level=\"WARN\" timestamp=\"1794994434808\" thread=\"1\"><log4j:message>Multi-line message:\nline 2</log4j:message><log4j:properties><log4j:data name=\"log4japp\" value=\"app(80279)\"/><log4j:data name=\"log4jmachinename\" value=\"my-host\"/></log4j:properties></log4j:event>",
					"<log4j:event logger=\"MyApp.OrderService\" level=\"ERROR\" timestamp=\"1794994434808\" thread=\"1\"><log4j:message>Failed to charge order</log4j:message><log4j:throwable>System.InvalidOperationException: Gateway timed out\n   at Program.&lt;Main&gt;$(String[] args) in /src/app/Program.cs:line 14</log4j:throwable><log4j:properties><log4j:data name=\"log4japp\" value=\"app(80279)\"/><log4j:data name=\"log4jmachinename\" value=\"my-host\"/></log4j:properties></log4j:event>",
					"<log4j:event logger=\"MyApp.OrderService\" level=\"FATAL\" timestamp=\"1794994434812\" thread=\"1\"><log4j:message>Shutting down &lt;now&gt; &amp; &quot;quoted&quot;</log4j:message><log4j:properties><log4j:data name=\"log4japp\" value=\"app(80279)\"/><log4j:data name=\"log4jmachinename\" value=\"my-host\"/></log4j:properties></log4j:event>",
					"<log4j:event logger=\"MyApp.Program\" level=\"INFO\" timestamp=\"1794994434813\" thread=\"1\"><log4j:message>Exited</log4j:message><log4j:properties><log4j:data name=\"log4japp\" value=\"app(80279)\"/><log4j:data name=\"log4jmachinename\" value=\"my-host\"/></log4j:properties></log4j:event>",
				])
				{
					await client.SendAsync(Encoding.UTF8.GetBytes(datagram), endPoint, CancellationToken.None);
				}
			}

			// wait for logs
			var deadline = DateTime.Now.AddSeconds(10);
			while (logReader.Logs.Count < 5 && DateTime.Now < deadline)
				await Task.Delay(100, CancellationToken.None);

			// check logs
			var logs = logReader.Logs;
			Assert.That(logs.Count, Is.GreaterThanOrEqualTo(5));
			Assert.That(logs[0].Timestamp, Is.EqualTo(DateTimeOffset.FromUnixTimeMilliseconds(1794994434798).LocalDateTime));
			Assert.That(logs[0].Level, Is.EqualTo(LogLevel.Trace));
			Assert.That(logs[0].SourceName?.ToString(), Is.EqualTo("MyApp.OrderService"));
			Assert.That(logs[0].ProcessId, Is.EqualTo(80279));
			Assert.That(logs[0].DeviceName?.ToString(), Is.EqualTo("my-host"));
			Assert.That(logs[0].Message?.ToString(), Is.EqualTo("Entering PlaceOrder"));
			Assert.That(logs[1].Message?.ToString(), Is.EqualTo("Order 1001 placed"));
			Assert.That(logs[2].Level, Is.EqualTo(LogLevel.Warn));
			Assert.That(logs[2].Message?.ToString(), Does.EndWith("line 2"));
			Assert.That(logs[3].Level, Is.EqualTo(LogLevel.Error));
			Assert.That(logs[3].Message?.ToString(), Does.Contain("Gateway timed out"));
			Assert.That(logs[3].Message?.ToString(), Does.EndWith("Program.cs:line 14"));
			Assert.That(logs[4].Level, Is.EqualTo(LogLevel.Fatal));
			Assert.That(logs[4].Message?.ToString(), Is.EqualTo("Shutting down <now> & \"quoted\""));
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


	// Read logs from given lines by built-in log profile.
	static async Task<IList<Log>> ReadLogsAsync(string profileId, string[] lines, LogProfileIcon icon = LogProfileIcon.Log)
	{
		// get log profile and data source provider
		var profile = LogProfileManager.Default.GetProfileOrDefault(profileId);
		Assert.That(profile, Is.Not.Null);
		Assert.That(profile!.Icon, Is.EqualTo(icon));
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");

		// prepare log file
		var filePath = Path.GetTempFileName();
		try
		{
			File.WriteAllText(filePath, string.Join('\n', lines));

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
			return logReader.Logs.ToArray();
		}
		finally
		{
			File.Delete(filePath);
		}
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
