using CarinaStudio.AppSuite.Data;
using CarinaStudio.Collections;
using CarinaStudio.ComponentModel;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.ViewModels.Categorizing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles;

/// <summary>
/// Tests of <see cref="LogProfile"/>.
/// </summary>
[TestFixture]
class LogProfileTests : ApplicationBasedTests
{
	// Check whether all serialized properties of given log profiles are same or not.
	static void AssertProfilesEqual(LogProfile expected, LogProfile actual)
	{
		// basic properties
		Assert.That(actual.Name, Is.EqualTo(expected.Name));
		Assert.That(actual.AllowMultipleFiles, Is.EqualTo(expected.AllowMultipleFiles));
		Assert.That(actual.ColorIndicator, Is.EqualTo(expected.ColorIndicator));
		Assert.That(actual.DefaultLogLevel, Is.EqualTo(expected.DefaultLogLevel));
		Assert.That(actual.Description, Is.EqualTo(expected.Description));
		Assert.That(actual.HasDescription, Is.EqualTo(expected.HasDescription));
		Assert.That(actual.Icon, Is.EqualTo(expected.Icon));
		Assert.That(actual.IconColor, Is.EqualTo(expected.IconColor));
		Assert.That(actual.IsAdministratorNeeded, Is.EqualTo(expected.IsAdministratorNeeded));
		Assert.That(actual.IsContinuousReading, Is.EqualTo(expected.IsContinuousReading));
		Assert.That(actual.IsTemplate, Is.EqualTo(expected.IsTemplate));
		Assert.That(actual.MaxLogReadingCount, Is.EqualTo(expected.MaxLogReadingCount));
		Assert.That(actual.RawLogLevelPropertyName, Is.EqualTo(expected.RawLogLevelPropertyName));
		Assert.That(actual.RestartReadingDelay, Is.EqualTo(expected.RestartReadingDelay));
		Assert.That(actual.SortDirection, Is.EqualTo(expected.SortDirection));
		Assert.That(actual.SortKey, Is.EqualTo(expected.SortKey));
		Assert.That(actual.WorkingDirectoryRequirement, Is.EqualTo(expected.WorkingDirectoryRequirement));

		// data source
		Assert.That(actual.DataSourceProvider.Name, Is.EqualTo(expected.DataSourceProvider.Name));
		var expectedOptions = expected.DataSourceOptions;
		var actualOptions = actual.DataSourceOptions;
		Assert.That(actualOptions.Category, Is.EqualTo(expectedOptions.Category));
		Assert.That(actualOptions.Command, Is.EqualTo(expectedOptions.Command));
		Assert.That(actualOptions.Encoding?.WebName, Is.EqualTo(expectedOptions.Encoding?.WebName));
		Assert.That(actualOptions.FileName, Is.EqualTo(expectedOptions.FileName));
		Assert.That(actualOptions.Password, Is.EqualTo(expectedOptions.Password));
		Assert.That(actualOptions.QueryString, Is.EqualTo(expectedOptions.QueryString));
		Assert.That(actualOptions.SetupCommands, Is.EqualTo(expectedOptions.SetupCommands));
		Assert.That(actualOptions.TeardownCommands, Is.EqualTo(expectedOptions.TeardownCommands));
		Assert.That(actualOptions.Uri, Is.EqualTo(expectedOptions.Uri));
		Assert.That(actualOptions.UserName, Is.EqualTo(expectedOptions.UserName));
		Assert.That(actualOptions.UseTextShell, Is.EqualTo(expectedOptions.UseTextShell));
		Assert.That(actualOptions.WorkingDirectory, Is.EqualTo(expectedOptions.WorkingDirectory));

		// log reading and writing
		Assert.That(actual.LogPatternMatchingMode, Is.EqualTo(expected.LogPatternMatchingMode));
		Assert.That(actual.LogPatterns.Count, Is.EqualTo(expected.LogPatterns.Count));
		for (var i = expected.LogPatterns.Count - 1; i >= 0; --i)
		{
			var expectedPattern = expected.LogPatterns[i];
			var actualPattern = actual.LogPatterns[i];
			Assert.That(actualPattern.Regex.ToString(), Is.EqualTo(expectedPattern.Regex.ToString()));
			Assert.That(actualPattern.Regex.Options & RegexOptions.IgnoreCase, Is.EqualTo(expectedPattern.Regex.Options & RegexOptions.IgnoreCase));
			Assert.That(actualPattern.Description, Is.EqualTo(expectedPattern.Description));
			Assert.That(actualPattern.IsRepeatable, Is.EqualTo(expectedPattern.IsRepeatable));
			Assert.That(actualPattern.IsSkippable, Is.EqualTo(expectedPattern.IsSkippable));
		}
		Assert.That(actual.LogReadingWindow, Is.EqualTo(expected.LogReadingWindow));
		Assert.That(actual.LogStringEncodingForReading, Is.EqualTo(expected.LogStringEncodingForReading));
		Assert.That(actual.LogStringEncodingForWriting, Is.EqualTo(expected.LogStringEncodingForWriting));
		Assert.That(actual.LogWritingFormats, Is.EqualTo(expected.LogWritingFormats));
		Assert.That(actual.LogLevelMapForReading, Is.EqualTo(expected.LogLevelMapForReading));
		Assert.That(actual.LogLevelMapForWriting, Is.EqualTo(expected.LogLevelMapForWriting));

		// time span and timestamp
		Assert.That(actual.TimeSpanCultureInfoForReading, Is.EqualTo(expected.TimeSpanCultureInfoForReading));
		Assert.That(actual.TimeSpanCultureInfoForWriting, Is.EqualTo(expected.TimeSpanCultureInfoForWriting));
		Assert.That(actual.TimeSpanEncodingForReading, Is.EqualTo(expected.TimeSpanEncodingForReading));
		Assert.That(actual.TimeSpanFormatForDisplaying, Is.EqualTo(expected.TimeSpanFormatForDisplaying));
		Assert.That(actual.TimeSpanFormatForWriting, Is.EqualTo(expected.TimeSpanFormatForWriting));
		Assert.That(actual.TimeSpanFormatsForReading, Is.EqualTo(expected.TimeSpanFormatsForReading));
		Assert.That(actual.TimestampCategoryGranularity, Is.EqualTo(expected.TimestampCategoryGranularity));
		Assert.That(actual.TimestampCultureInfoForReading, Is.EqualTo(expected.TimestampCultureInfoForReading));
		Assert.That(actual.TimestampCultureInfoForWriting, Is.EqualTo(expected.TimestampCultureInfoForWriting));
		Assert.That(actual.TimestampEncodingForReading, Is.EqualTo(expected.TimestampEncodingForReading));
		Assert.That(actual.TimestampFormatForDisplaying, Is.EqualTo(expected.TimestampFormatForDisplaying));
		Assert.That(actual.TimestampFormatForWriting, Is.EqualTo(expected.TimestampFormatForWriting));
		Assert.That(actual.TimestampFormatsForReading, Is.EqualTo(expected.TimestampFormatsForReading));

		// displaying and charting
		Assert.That(actual.VisibleLogProperties, Is.EqualTo(expected.VisibleLogProperties));
		Assert.That(actual.LogChartSeriesSources, Is.EqualTo(expected.LogChartSeriesSources));
		Assert.That(actual.LogChartType, Is.EqualTo(expected.LogChartType));
		Assert.That(actual.LogChartValueGranularity, Is.EqualTo(expected.LogChartValueGranularity));
		Assert.That(actual.LogChartXAxisType, Is.EqualTo(expected.LogChartXAxisType));
	}


	/// <summary>
	/// Test for preventing modification of built-in log profile.
	/// </summary>
	[Test]
	public void BuiltInProfileImmutabilityTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// find built-in profile
			var profile = LogProfileManager.Default.GetProfileOrDefault("RawFile") ?? throw new AssertionException("Cannot find built-in log profile.");
			Assert.That(profile.IsBuiltIn);

			// modification is not allowed
			Assert.Throws<InvalidOperationException>(() => profile.ColorIndicator = LogColorIndicator.ProcessId);
			Assert.Throws<InvalidOperationException>(() => profile.Icon = LogProfileIcon.Database);
			Assert.Throws<InvalidOperationException>(() => profile.IsTemplate = true);
			Assert.Throws<InvalidOperationException>(() => profile.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ]);
			Assert.Throws<InvalidOperationException>(() => profile.Name = "New Name");

			// pinning is allowed
			var isPinned = profile.IsPinned;
			profile.IsPinned = !isPinned;
			Assert.That(profile.IsPinned, Is.EqualTo(!isPinned));
			profile.IsPinned = isPinned;

			// only width of visible log property can be changed
			var profileWithVisibleLogProperties = LogProfileManager.Default.Profiles.FirstOrDefault(it => it.IsBuiltIn && it.VisibleLogProperties.IsNotEmpty())
			                                      ?? throw new AssertionException("Cannot find built-in log profile with visible log properties.");
			var visibleLogProperties = profileWithVisibleLogProperties.VisibleLogProperties;
			var originalProperty = visibleLogProperties[0];
			Assert.Throws<ArgumentException>(() => profileWithVisibleLogProperties.VisibleLogProperties = []);
			Assert.Throws<ArgumentException>(() => profileWithVisibleLogProperties.VisibleLogProperties = new List<LogProperty>(visibleLogProperties).Also(it =>
			{
				it[0] = new LogProperty("Message", "Renamed", null, null, originalProperty.ForegroundColor, originalProperty.Width);
			}));
			profileWithVisibleLogProperties.VisibleLogProperties = new List<LogProperty>(visibleLogProperties).Also(it =>
			{
				it[0] = new LogProperty(originalProperty.Name, originalProperty.DisplayName, originalProperty.SecondaryDisplayName, originalProperty.Quantifier, originalProperty.ForegroundColor, 123);
			});
			Assert.That(profileWithVisibleLogProperties.VisibleLogProperties[0].Width, Is.EqualTo(123));
			profileWithVisibleLogProperties.VisibleLogProperties = visibleLogProperties;
		});
	}


	/// <summary>
	/// Test for built-in log profiles which are loaded from embedded resources.
	/// </summary>
	[Test]
	public void BuiltInProfilesTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// collect built-in profiles
			var profiles = LogProfileManager.Default.Profiles.Where(it => it.IsBuiltIn).ToArray();
			Assert.That(profiles, Is.Not.Empty, "No built-in log profile was loaded.");

			// check each profile
			foreach (var profile in profiles)
			{
				Assert.That(profile.Name, Is.Not.Empty, $"Name of built-in log profile '{profile.Id}' is empty.");
				Assert.That(profile.DataSourceProvider, Is.Not.InstanceOf<EmptyLogDataSourceProvider>(), $"No data source of built-in log profile '{profile.Id}'.");
				foreach (var logProperty in profile.VisibleLogProperties)
					Assert.That(Log.HasProperty(logProperty.Name), $"Unknown visible log property '{logProperty.Name}' in built-in log profile '{profile.Id}'.");
				foreach (var seriesSource in profile.LogChartSeriesSources)
					Assert.That(Log.HasProperty(seriesSource.PropertyName), $"Unknown log chart series source '{seriesSource.PropertyName}' in built-in log profile '{profile.Id}'.");
				if (profile.LogPatterns.IsNotEmpty())
					Assert.That(profile.IsValid, $"Built-in log profile '{profile.Id}' is invalid.");
			}
		});
	}


	/// <summary>
	/// Test for creating log profile based-on another log profile.
	/// </summary>
	[Test]
	public void CopyConstructorTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// copy custom log profile
			var template = this.CreateFullyPopulatedProfile();
			var copiedProfile = new LogProfile(template);
			Assert.That(copiedProfile.Id, Is.Not.EqualTo(template.Id), "Copied log profile should have its own ID.");
			Assert.That(copiedProfile.IsBuiltIn, Is.False);
			AssertProfilesEqual(template, copiedProfile);

			// copy built-in log profile
			var builtInProfile = LogProfileManager.Default.GetProfileOrDefault("RawFile") ?? throw new AssertionException("Cannot find built-in log profile.");
			var copiedBuiltInProfile = new LogProfile(builtInProfile);
			Assert.That(copiedBuiltInProfile.IsBuiltIn, Is.False, "Copy of built-in log profile should not be built-in.");
			Assert.That(copiedBuiltInProfile.SourceBuildInLogProfileId, Is.EqualTo(builtInProfile.Id));
			AssertProfilesEqual(builtInProfile, copiedBuiltInProfile);

			// copy of copy keeps the source of built-in log profile
			var copiedProfile2 = new LogProfile(copiedBuiltInProfile);
			Assert.That(copiedProfile2.SourceBuildInLogProfileId, Is.EqualTo(builtInProfile.Id));
		});
	}


	// Create log profile with all properties set to non-default values.
	LogProfile CreateFullyPopulatedProfile()
	{
		if (!LogDataSourceProviders.TryFindProviderByName("StandardOutput", out var provider))
			throw new AssertionException("Cannot find standard output log data source provider.");
		return new LogProfile(this.Application).Also(it =>
		{
			it.AllowMultipleFiles = false;
			it.ColorIndicator = LogColorIndicator.ProcessId;
			it.DataSourceProvider = provider;
			it.DataSourceOptions = new LogDataSourceOptions
			{
				Category = "TestCategory",
				Command = "echo test",
				Encoding = Encoding.UTF8,
				FileName = "/tmp/test.log",
				QueryString = "SELECT * FROM Logs",
				SetupCommands = [ "setup-1", "setup-2" ],
				TeardownCommands = [ "teardown-1" ],
				Uri = new Uri("https://localhost/logs"),
				UseTextShell = true,
				WorkingDirectory = "/tmp",
			};
			it.DefaultLogLevel = LogLevel.Info;
			it.Description = "Description of log profile.";
			it.Icon = LogProfileIcon.Database;
			it.IconColor = LogProfileIconColor.Red;
			it.IsAdministratorNeeded = true;
			it.IsContinuousReading = true;
			it.LogChartSeriesSources = [ new LogChartSeriesSource(nameof(Log.ProcessId), "PID", null, "count", 0, 2) ];
			it.LogChartType = LogChartType.ValueBars;
			it.LogChartValueGranularity = LogChartValueGranularity.Thousand;
			it.LogChartXAxisType = LogChartXAxisType.Timestamp;
			it.LogLevelMapForReading = new Dictionary<string, LogLevel>
			{
				["D"] = LogLevel.Debug,
				["E"] = LogLevel.Error,
			};
			it.LogLevelMapForWriting = new Dictionary<LogLevel, string>
			{
				[LogLevel.Debug] = "D",
				[LogLevel.Error] = "E",
			};
			it.LogPatternMatchingMode = LogPatternMatchingMode.Arbitrary;
			it.LogPatterns =
			[
				new LogPattern("^(?<Timestamp>[^\\s]+)\\s(?<Message>.*)$", false, false, "Header of log"),
				new LogPattern("^\\s+(?<Message>.*)$", true, true, null),
			];
			it.LogReadingWindow = LogReadingWindow.EndOfDataSource;
			it.LogStringEncodingForReading = LogStringEncoding.Json;
			it.LogStringEncodingForWriting = LogStringEncoding.Xml;
			it.LogWritingFormats = [ "{Timestamp} {Message}" ];
			it.MaxLogReadingCount = 1000;
			it.Name = "Fully Populated Profile";
			it.RawLogLevelPropertyName = nameof(Log.Category);
			it.RestartReadingDelay = 500;
			it.SortDirection = SortDirection.Descending;
			it.SortKey = LogSortKey.ReadTime;
			it.TimeSpanCultureInfoForReading = CultureInfo.GetCultureInfo("zh-TW");
			it.TimeSpanCultureInfoForWriting = CultureInfo.GetCultureInfo("ja-JP");
			it.TimeSpanEncodingForReading = LogTimeSpanEncoding.TotalSeconds;
			it.TimeSpanFormatForDisplaying = "hh\\:mm\\:ss";
			it.TimeSpanFormatForWriting = "hh\\:mm";
			it.TimeSpanFormatsForReading = [ "hh\\:mm\\:ss", "hh\\:mm" ];
			it.TimestampCategoryGranularity = TimestampDisplayableLogCategoryGranularity.Minute;
			it.TimestampCultureInfoForReading = CultureInfo.GetCultureInfo("de-DE");
			it.TimestampCultureInfoForWriting = CultureInfo.GetCultureInfo("fr-FR");
			it.TimestampEncodingForReading = LogTimestampEncoding.UnixMilliseconds;
			it.TimestampFormatForDisplaying = "yyyy/MM/dd HH:mm:ss";
			it.TimestampFormatForWriting = "yyyy-MM-dd HH:mm:ss";
			it.TimestampFormatsForReading = [ "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd" ];
			it.VisibleLogProperties =
			[
				new LogProperty(nameof(Log.Timestamp), "Time", null, null, LogPropertyForegroundColor.None, 100),
				new LogProperty(nameof(Log.Message), null, "Details", "lines", LogPropertyForegroundColor.Level, null),
			];
			it.WorkingDirectoryRequirement = LogProfilePropertyRequirement.Required;
		});
	}


	/// <summary>
	/// Test for equality of log profiles.
	/// </summary>
	[Test]
	public void EqualityTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// log profiles with different IDs are not equivalent
			var profile = new LogProfile(this.Application);
			var anotherProfile = new LogProfile(this.Application);
			Assert.That(profile.Id, Is.Not.EqualTo(anotherProfile.Id));
			Assert.That(profile.Equals(anotherProfile), Is.False);
			Assert.That(profile == anotherProfile, Is.False);
			Assert.That(profile != anotherProfile);

			// log profile is equivalent to itself
			Assert.That(profile.Equals(profile));
			Assert.That(profile.GetHashCode(), Is.EqualTo(profile.Id.GetHashCode()));

			// log profile is not equivalent to null
			Assert.That(profile.Equals(null), Is.False);
			Assert.That(profile == null, Is.False);
			Assert.That(profile != null);

			// copy of log profile is not equivalent to its template
			Assert.That(profile.Equals(new LogProfile(profile)), Is.False);
		});
	}


	/// <summary>
	/// Test for upgrading data of log profile which was saved by old version.
	/// </summary>
	[Test]
	public void LegacyDataUpgradeTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// save log profile in old format
			var fileName = Path.Combine(Path.GetTempPath(), $"ULogViewer.LogProfileTests.{Guid.NewGuid()}.json");
			await File.WriteAllTextAsync(fileName, """
			{
				"Name": "Legacy Log Profile",
				"DataSource": {
					"Name": "File",
					"FileName": "/tmp/legacy.log",
					"Encoding": "utf-8",
					"SetupCommands": [ "setup" ]
				},
				"LogPatterns": [ { "Regex": "^(?<Message>.*)$" } ],
				"LogWritingFormat": "{Message}",
				"TimestampFormatForReading": "yyyy-MM-dd HH:mm:ss",
				"IsWorkingDirectoryNeeded": true
			}
			""", CancellationToken.None);
			try
			{
				// load log profile
				var profile = await LogProfile.LoadAsync(this.Application, fileName);

				// check upgraded data
				Assert.That(profile.IsDataUpgraded, "Data of log profile should be upgraded.");
				Assert.That(profile.LogWritingFormats, Is.EqualTo(new[] { "{Message}" }));
				Assert.That(profile.TimestampFormatsForReading, Is.EqualTo(new[] { "yyyy-MM-dd HH:mm:ss" }));
				Assert.That(profile.WorkingDirectoryRequirement, Is.EqualTo(LogProfilePropertyRequirement.Required));
				Assert.That(profile.DataSourceProvider.Name, Is.EqualTo("File"));
				Assert.That(profile.DataSourceOptions.FileName, Is.EqualTo("/tmp/legacy.log"));
				Assert.That(profile.DataSourceOptions.Encoding?.WebName, Is.EqualTo(Encoding.UTF8.WebName));
				Assert.That(profile.DataSourceOptions.SetupCommands, Is.EqualTo(new[] { "setup" }));
			}
			finally
			{
				File.Delete(fileName);
			}
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
			// attach to log profile
			var profile = new LogProfile(this.Application);
			var changedPropertyNames = new List<string>();
			profile.PropertyChanged += (_, e) => changedPropertyNames.Add(e.PropertyName.AsNonNull());

			// change value of properties
			profile.ColorIndicator = LogColorIndicator.ProcessId;
			Assert.That(changedPropertyNames, Is.EqualTo(new[] { nameof(LogProfile.ColorIndicator) }));
			changedPropertyNames.Clear();
			profile.Icon = LogProfileIcon.Database;
			profile.MaxLogReadingCount = 100;
			profile.Name = "New Name";
			Assert.That(changedPropertyNames, Is.EquivalentTo(new[] { nameof(LogProfile.Icon), nameof(LogProfile.MaxLogReadingCount), nameof(LogProfile.Name) }));

			// set same value to properties
			changedPropertyNames.Clear();
			profile.ColorIndicator = LogColorIndicator.ProcessId;
			profile.Icon = LogProfileIcon.Database;
			profile.MaxLogReadingCount = 100;
			profile.Name = "New Name";
			Assert.That(changedPropertyNames, Is.Empty, "Setting same value to properties should not raise notification.");

			// change value of list properties
			profile.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
			profile.VisibleLogProperties = [ new LogProperty(nameof(Log.Message), null, null, null, LogPropertyForegroundColor.Level, null) ];
			Assert.That(changedPropertyNames, Does.Contain(nameof(LogProfile.LogPatterns)));
			Assert.That(changedPropertyNames, Does.Contain(nameof(LogProfile.VisibleLogProperties)));

			// set same value to list properties
			changedPropertyNames.Clear();
			profile.VisibleLogProperties = [ new LogProperty(nameof(Log.Message), null, null, null, LogPropertyForegroundColor.Level, null) ];
			Assert.That(changedPropertyNames, Is.Empty, "Setting same value to list properties should not raise notification.");

			// change value of map properties
			profile.LogLevelMapForReading = new Dictionary<string, LogLevel> { ["D"] = LogLevel.Debug };
			profile.LogLevelMapForWriting = new Dictionary<LogLevel, string> { [LogLevel.Debug] = "D" };
			Assert.That(changedPropertyNames, Is.EquivalentTo(new[] { nameof(LogProfile.LogLevelMapForReading), nameof(LogProfile.LogLevelMapForWriting) }));

			// set same value to map properties
			changedPropertyNames.Clear();
			profile.LogLevelMapForReading = new Dictionary<string, LogLevel> { ["D"] = LogLevel.Debug };
			profile.LogLevelMapForWriting = new Dictionary<LogLevel, string> { [LogLevel.Debug] = "D" };
			Assert.That(changedPropertyNames, Is.Empty, "Setting same value to map properties should not raise notification.");
		});
	}


	/// <summary>
	/// Test for saving and loading log profile with credentials.
	/// </summary>
	[Test]
	public void SaveAndLoadCredentialsTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare log profile with credentials
			var profile = this.CreateFullyPopulatedProfile();
			profile.DataSourceOptions = profile.DataSourceOptions.Let(it =>
			{
				it.Password = "P@ssw0rd";
				it.UserName = "user";
				return it;
			});

			// save and load log profile
			var fileName = Path.Combine(Path.GetTempPath(), $"ULogViewer.LogProfileTests.{Guid.NewGuid()}.json");
			try
			{
				await profile.SaveAsync(fileName, true, CancellationToken.None);
				var loadedProfile = await LogProfile.LoadAsync(this.Application, fileName);
				Assert.That(loadedProfile.DataSourceOptions.Password, Is.EqualTo("P@ssw0rd"));
				Assert.That(loadedProfile.DataSourceOptions.UserName, Is.EqualTo("user"));
			}
			finally
			{
				File.Delete(fileName);
			}
		});
	}


	/// <summary>
	/// Test for saving and loading log profile.
	/// </summary>
	[Test]
	public void SaveAndLoadTest()
	{
		this.TestOnApplicationThread(async () =>
		{
			// prepare log profile
			var profile = this.CreateFullyPopulatedProfile();

			// save and load log profile with ID
			var fileName = Path.Combine(Path.GetTempPath(), $"ULogViewer.LogProfileTests.{Guid.NewGuid()}.json");
			try
			{
				await profile.SaveAsync(fileName, true, CancellationToken.None);
				var loadedProfile = await LogProfile.LoadAsync(this.Application, fileName);
				Assert.That(loadedProfile.Id, Is.EqualTo(profile.Id));
				Assert.That(loadedProfile.IsDataUpgraded, Is.False, "Data of log profile saved by current version should not be upgraded.");
				AssertProfilesEqual(profile, loadedProfile);

				// save and load log profile without ID
				await profile.SaveAsync(fileName, false, CancellationToken.None);
				loadedProfile = await LogProfile.LoadAsync(this.Application, fileName);
				Assert.That(loadedProfile.Id, Is.Not.EqualTo(profile.Id), "New ID should be generated for log profile saved without ID.");
				AssertProfilesEqual(profile, loadedProfile);
			}
			finally
			{
				File.Delete(fileName);
			}
		});
	}


	/// <summary>
	/// Test for validation of log profile.
	/// </summary>
	[Test]
	public void ValidationTest()
	{
		this.TestOnApplicationThread(() =>
		{
			// log profile without data source is invalid
			if (!LogDataSourceProviders.TryFindProviderByName("File", out var provider))
				throw new AssertionException("Cannot find file log data source provider.");
			var profile = new LogProfile(this.Application);
			Assert.That(profile.IsValid, Is.False);

			// log profile without log pattern is invalid
			profile.DataSourceProvider = provider;
			Assert.That(profile.IsValid, Is.False);

			// log profile with data source and log pattern is valid
			profile.LogPatterns = [ new LogPattern("^(?<Message>.*)$", false, false, null) ];
			Assert.That(profile.IsValid);

			// log profile with non-positive maximum log reading count is invalid
			profile.MaxLogReadingCount = 0;
			Assert.That(profile.IsValid, Is.False);
			profile.MaxLogReadingCount = 100;
			Assert.That(profile.IsValid);

			// log profile with unknown visible log property is invalid
			profile.VisibleLogProperties = [ new LogProperty("NoSuchLogProperty", null, null, null, LogPropertyForegroundColor.Level, null) ];
			Assert.That(profile.IsValid, Is.False);
			profile.VisibleLogProperties = [ new LogProperty(nameof(Log.Message), null, null, null, LogPropertyForegroundColor.Level, null) ];
			Assert.That(profile.IsValid);

			// log profile with unknown log chart series source is invalid
			profile.LogChartSeriesSources = [ new LogChartSeriesSource("NoSuchLogProperty", null, null, null, null, 1) ];
			Assert.That(profile.IsValid, Is.False);
			profile.LogChartSeriesSources = [ new LogChartSeriesSource(nameof(Log.ProcessId), null, null, null, null, 1) ];
			Assert.That(profile.IsValid);

			// template only needs data source to be valid
			var templateProfile = new LogProfile(this.Application);
			templateProfile.IsTemplate = true;
			Assert.That(templateProfile.IsValid, Is.False);
			templateProfile.DataSourceProvider = provider;
			Assert.That(templateProfile.IsValid, "Template log profile without log pattern should be valid.");
		});
	}
}
