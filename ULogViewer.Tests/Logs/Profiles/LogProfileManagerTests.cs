using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.Logs.DataSources;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
			foreach (var id in new[] { "AndroidFileLog", "RawFile", "RawHttp", "RawStandardOutput", "RawTcpServer" })
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
				await profile.SaveAsync(fileName, true, CancellationToken.None);
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
			Assert.That(manager.RecentlyUsedProfiles, Is.EqualTo(new[] { profiles[1], profiles[0] }), "Recently used log profile should be the first one.");

			// use log profile which was used before
			manager.SetAsRecentlyUsed(profiles[0]);
			Assert.That(manager.RecentlyUsedProfiles, Is.EqualTo(new[] { profiles[0], profiles[1] }), "Log profile should be moved to the first one without duplication.");

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
}
