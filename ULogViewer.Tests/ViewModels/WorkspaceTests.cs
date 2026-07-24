using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using NUnit.Framework;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Tests of <see cref="Workspace"/>.
/// </summary>
[TestFixture]
class WorkspaceTests : ApplicationBasedTests
{
	/// <summary>
	/// Test for activating session in workspace.
	/// </summary>
	[Test]
	public void ActiveSessionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspaces and sessions
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace = new Workspace(null);
			using var otherWorkspace = new Workspace(null);
			var session1 = workspace.CreateAndAttachSession();
			var session2 = workspace.CreateAndAttachSession();
			var foreignSession = otherWorkspace.CreateAndAttachSession();
			Assert.That(workspace.ActiveSession, Is.Null);

			// activate first session
			workspace.ActiveSession = session1;
			await this.WaitForConditionAsync(() => session1.IsActivated, "Session was not activated.");

			// switch to second session
			workspace.ActiveSession = session2;
			await this.WaitForConditionAsync(() => session2.IsActivated, "Second session was not activated.");
			await this.WaitForConditionAsync(() => !session1.IsActivated, "First session was not deactivated.");

			// activating session of another workspace is not allowed
			Assert.Throws<ArgumentException>(() => workspace.ActiveSession = foreignSession);

			// detaching active session clears activation
			workspace.ActiveSession = session2;
			workspace.DetachSession(session2);
			Assert.That(workspace.ActiveSession, Is.Null);
			session2.Dispose();
		});
	}


	/// <summary>
	/// Test for attaching and detaching sessions.
	/// </summary>
	[Test]
	public void AttachDetachSessionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspaces
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace1 = new Workspace(null);
			using var workspace2 = new Workspace(null);

			// attach session to another workspace to transfer ownership
			var session = workspace1.CreateAndAttachSession();
			Assert.That(session.Owner, Is.SameAs(workspace1));
			workspace2.AttachSession(0, session);
			Assert.That(session.Owner, Is.SameAs(workspace2));
			Assert.That(workspace1.Sessions, Is.Empty);
			Assert.That(workspace2.Sessions.Count, Is.EqualTo(1));

			// attaching again is a no-op
			workspace2.AttachSession(0, session);
			Assert.That(workspace2.Sessions.Count, Is.EqualTo(1));

			// invalid position is not allowed
			var otherSession = workspace1.CreateAndAttachSession();
			Assert.Throws<ArgumentOutOfRangeException>(() => workspace1.AttachSession(-1, session));
			Assert.Throws<ArgumentOutOfRangeException>(() => workspace1.AttachSession(2, session));

			// detach session
			workspace2.DetachSession(session);
			Assert.That(session.Owner, Is.Null);
			Assert.That(workspace2.Sessions, Is.Empty);

			// detaching non-owned session is a no-op
			workspace2.DetachSession(otherSession);
			Assert.That(otherSession.Owner, Is.SameAs(workspace1));
			session.Dispose();
		});
	}


	/// <summary>
	/// Test for creating and attaching sessions.
	/// </summary>
	[Test]
	public void CreateAndAttachSessionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspace
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace = new Workspace(null);
			Assert.That(workspace.Sessions, Is.Empty);

			// create session at tail
			var session1 = workspace.CreateAndAttachSession();
			Assert.That(workspace.Sessions.Count, Is.EqualTo(1));
			Assert.That(session1.Owner, Is.SameAs(workspace));

			// create session at head
			var session2 = workspace.CreateAndAttachSession(0);
			Assert.That(workspace.Sessions.Count, Is.EqualTo(2));
			Assert.That(workspace.Sessions[0], Is.SameAs(session2));
			Assert.That(workspace.Sessions[1], Is.SameAs(session1));

			// invalid position is not allowed
			Assert.Throws<ArgumentOutOfRangeException>(() => workspace.CreateAndAttachSession(-1));
			Assert.Throws<ArgumentOutOfRangeException>(() => workspace.CreateAndAttachSession(3));
		});
	}


	/// <summary>
	/// Test for detaching and closing session.
	/// </summary>
	[Test]
	public void DetachAndCloseSessionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspaces and sessions
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace = new Workspace(null);
			using var otherWorkspace = new Workspace(null);
			var session = workspace.CreateAndAttachSession();
			var foreignSession = otherWorkspace.CreateAndAttachSession();

			// close session
			await workspace.DetachAndCloseSession(session);
			Assert.That(workspace.Sessions, Is.Empty);
			Assert.That(session.Owner, Is.Null);
			Assert.Throws<ObjectDisposedException>(() => session.Activate());

			// closing session of another workspace is a no-op
			await workspace.DetachAndCloseSession(foreignSession);
			Assert.That(foreignSession.Owner, Is.SameAs(otherWorkspace));
		});
	}


	/// <summary>
	/// Test for disposing workspace.
	/// </summary>
	[Test]
	public void DisposeTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspace and sessions
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var workspace = new Workspace(null);
			var session1 = workspace.CreateAndAttachSession();
			var session2 = workspace.CreateAndAttachSession();

			// dispose workspace to dispose all sessions
			workspace.Dispose();
			Assert.Throws<ObjectDisposedException>(() => session1.Activate());
			Assert.Throws<ObjectDisposedException>(() => session2.Activate());

			// no more session creation is allowed
			Assert.Throws<ObjectDisposedException>(() => workspace.CreateAndAttachSession());
		});
	}


	/// <summary>
	/// Test for moving session in workspace.
	/// </summary>
	[Test]
	public void MoveSessionTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspace and sessions
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace = new Workspace(null);
			var session1 = workspace.CreateAndAttachSession();
			var session2 = workspace.CreateAndAttachSession();
			var session3 = workspace.CreateAndAttachSession();

			// move session
			workspace.MoveSession(0, 2);
			Assert.That(workspace.Sessions[0], Is.SameAs(session2));
			Assert.That(workspace.Sessions[1], Is.SameAs(session3));
			Assert.That(workspace.Sessions[2], Is.SameAs(session1));

			// moving to same position is a no-op
			workspace.MoveSession(1, 1);
			Assert.That(workspace.Sessions[1], Is.SameAs(session3));

			// invalid position is not allowed
			Assert.Throws<ArgumentOutOfRangeException>(() => workspace.MoveSession(-1, 0));
			Assert.Throws<ArgumentOutOfRangeException>(() => workspace.MoveSession(0, 3));
		});
	}


	/// <summary>
	/// Test for saving and restoring state of workspace.
	/// </summary>
	[Test]
	public void SaveAndRestoreStateTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare workspace and sessions
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace = new Workspace(null);
			workspace.CreateAndAttachSession();
			var session2 = workspace.CreateAndAttachSession();
			session2.CustomTitle = "Second Session";
			workspace.ActiveSession = session2;

			// save state
			using var stream = new MemoryStream();
			await using (var jsonWriter = new Utf8JsonWriter(stream))
				Assert.That(workspace.SaveState(jsonWriter), Is.True);

			// restore into new workspace
			using var jsonState = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
			using var restoredWorkspace = new Workspace(jsonState.RootElement);
			Assert.That(restoredWorkspace.Sessions.Count, Is.EqualTo(2));
			Assert.That(restoredWorkspace.ActiveSession, Is.SameAs(restoredWorkspace.Sessions[1]));
			Assert.That(restoredWorkspace.Sessions[1].CustomTitle, Is.EqualTo("Second Session"));
		});
	}


	/// <summary>
	/// Test for creating session with log files.
	/// </summary>
	[Test]
	public void SessionCreationWithLogFilesTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare log file
			await SessionTestEnvironment.InitializeAsync(this.Application);
			var filePath = Path.GetTempFileName();
			await File.WriteAllTextAsync(filePath, "line 1\nline 2\nline 3");
			try
			{
				// create session with log file
				using var workspace = new Workspace(null);
				var profile = this.CreateFileLogProfile();
				var session = workspace.CreateAndAttachSessionWithLogFiles(profile, [ filePath ]);
				Assert.That(session.LogProfile, Is.SameAs(profile));
				await this.WaitForConditionAsync(() => session.LogFiles.Count == 1, "Log file was not added.");

				// wait for logs to be read
				await this.WaitForConditionAsync(() => session.AllLogCount == 3, $"Logs were not read, count: {session.AllLogCount}.");
			}
			finally
			{
				Global.RunWithoutError(() => File.Delete(filePath));
			}
		});
	}


	/// <summary>
	/// Test for creating session with working directory.
	/// </summary>
	[Test]
	public void SessionCreationWithWorkingDirectoryTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare profile which supports working directory
			await SessionTestEnvironment.InitializeAsync(this.Application);
			if (!LogDataSourceProviders.TryFindProviderByName("StandardOutput", out var provider))
				throw new AssertionException("Cannot find standard output log data source provider.");
			var profile = new LogProfile(this.Application).Also(it =>
			{
				it.DataSourceProvider = provider;
				it.DataSourceOptions = new LogDataSourceOptions { Command = "echo test" };
				it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
			});

			// create session with working directory
			using var workspace = new Workspace(null);
			var directory = Path.GetTempPath();
			var session = workspace.CreateAndAttachSessionWithWorkingDirectory(profile, directory);
			Assert.That(session.LogProfile, Is.SameAs(profile));
			await this.WaitForConditionAsync(() => session.WorkingDirectoryPath is not null, "Working directory was not set.");
			Assert.That(Path.TrimEndingDirectorySeparator(session.WorkingDirectoryPath!), Is.EqualTo(Path.TrimEndingDirectorySeparator(directory)));
		});
	}


	/// <summary>
	/// Test for title of workspace.
	/// </summary>
	[Test]
	public void TitleTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// title without active session
			await SessionTestEnvironment.InitializeAsync(this.Application);
			using var workspace = new Workspace(null);
			await this.WaitForConditionAsync(() => workspace.Title?.StartsWith("ULogViewer") == true, $"Unexpected title: {workspace.Title}.");

			// title follows active session
			var session = workspace.CreateAndAttachSession();
			session.CustomTitle = "Test Session";
			workspace.ActiveSession = session;
			await this.WaitForConditionAsync(() => workspace.Title?.EndsWith("- Test Session") == true, $"Unexpected title: {workspace.Title}.");

			// title follows renamed active session
			session.CustomTitle = "Renamed Session";
			await this.WaitForConditionAsync(() => workspace.Title?.EndsWith("- Renamed Session") == true, $"Unexpected title: {workspace.Title}.");

			// title restores after clearing active session
			workspace.ActiveSession = null;
			await this.WaitForConditionAsync(() => workspace.Title?.EndsWith("Session") != true, $"Unexpected title: {workspace.Title}.");
		});
	}


	// Create log profile for reading log files.
	LogProfile CreateFileLogProfile()
	{
		if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
			throw new AssertionException("Cannot find file log data source provider.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.DataSourceProvider = provider;
			it.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
		});
	}


	// Wait until given condition has been satisfied.
	async Task WaitForConditionAsync(Func<bool> condition, string message, int timeoutMillis = 10000)
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
