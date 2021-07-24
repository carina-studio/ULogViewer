using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles
{
	/// <summary>
	/// Class to manage <see cref="LogProfile"/>s.
	/// </summary>
	static class LogProfiles
	{
		// Constants.
		const int SaveProfilesDelay = 100;


		// Fields.
		static volatile IApplication? app;
		static readonly string[] builtInProfileIDs = new string[] {
			"AndroidDeviceLog",
			"AndroidFileLog",
#if DEBUG
			"DummyLog",
#endif
			"GitLog",
#if DEBUG
			"ULogViewerLog",
#endif
		};
		static volatile ILogger? logger;
		static readonly HashSet<LogProfile> pendingSavingProfiles = new HashSet<LogProfile>();
		static readonly ObservableList<LogProfile> pinnedProfiles = new ObservableList<LogProfile>();
		static readonly ObservableList<LogProfile> profiles = new ObservableList<LogProfile>();
		static string profilesDirectoryPath = "";
		static ScheduledAction? saveProfilesAction;


		// Static initializer.
		static LogProfiles()
		{
			All = profiles.AsReadOnly();
			Pinned = pinnedProfiles.AsReadOnly();
		}


		/// <summary>
		/// Get all <see cref="LogProfile"/>s.
		/// </summary>
		/// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
		public static IList<LogProfile> All { get; }


		/// <summary>
		/// Add new <see cref="LogProfile"/>.
		/// </summary>
		/// <param name="profile"><see cref="LogProfile"/>.</param>
		public static void Add(LogProfile profile)
		{
			app.AsNonNull().VerifyAccess();
			AttachToProfile(profile);
			pendingSavingProfiles.Add(profile);
			saveProfilesAction?.Schedule(SaveProfilesDelay);
		}


		// Attach to profile.
		static void AttachToProfile(LogProfile profile)
		{
			// check profile
			if (profiles.Contains(profile))
				throw new InvalidOperationException($"Profile '{profile.Name}' is already been attached.");

			// add handler
			profile.PropertyChanged += OnProfilePropertyChanged;

			// add to lists
			profiles.Add(profile);
			if (profile.IsPinned)
				pinnedProfiles.Add(profile);
		}


		// Detach from profile.
		static void DetachFromProfile(LogProfile profile)
		{
			// remove handler
			profile.PropertyChanged -= OnProfilePropertyChanged;

			// remove from lists
			pinnedProfiles.Remove(profile);
			profiles.Remove(profile);
		}


		/// <summary>
		/// Initialize <see cref="LogProfile"/>s asynchronously.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <returns>Task of initialization.</returns>
		public static async Task InitializeAsync(IApplication app)
		{
			// check state
			app.VerifyAccess();
			if (LogProfiles.app != null && LogProfiles.app != app)
				throw new InvalidOperationException();

			// attach to application
			LogProfiles.app = app;
			app.StringsUpdated += OnApplicationStringsUpdated;

			// create logger
			logger = app.LoggerFactory.CreateLogger(nameof(LogProfiles));

			// create scheduled action
			saveProfilesAction = new ScheduledAction(async () =>
			{
				if (pendingSavingProfiles.IsEmpty())
					return;
				var profiles = pendingSavingProfiles.ToArray().Also(_ => pendingSavingProfiles.Clear());
				logger?.LogDebug($"Start saving {profiles.Length} profile(s)");
				foreach (var profile in profiles)
				{
					var fileName = profile.FileName;
					try
					{
						if (fileName == null)
							fileName = await profile.FindValidFileNameAsync(profilesDirectoryPath);
						_ = profile.SaveAsync(fileName);
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Unable to save profile '{profile.Name}' to '{fileName}'");
					}
				}
				logger?.LogDebug($"Complete saving {profiles.Length} profile(s)");
			});

			// load build-in profiles
			logger.LogDebug("Start loading built-in profiles");
			var profileCount = 0;
			foreach (var id in builtInProfileIDs)
			{
				logger.LogDebug($"Load '{id}'");
				AttachToProfile(await LogProfile.LoadBuiltInProfileAsync(app, id));
				++profileCount;
			}
			logger.LogDebug($"Complete loading {profileCount} built-in profile(s)");

			// load profiles
			profilesDirectoryPath = Path.Combine(app.RootPrivateDirectoryPath, "Profiles");
			profileCount = 0;
			logger.LogDebug("Start loading profiles");
			var fileNames = await Task.Run(() =>
			{
				try
				{
					if (!Directory.Exists(profilesDirectoryPath))
						return new string[0];
					return Directory.GetFiles(profilesDirectoryPath, "*.json");
				}
				catch (Exception ex)
				{
					logger.LogError(ex, $"Unable to check profiles in directory '{profilesDirectoryPath}'");
					return new string[0];
				}
			});
			foreach (var fileName in fileNames)
			{
				try
				{
					AttachToProfile(await LogProfile.LoadProfileAsync(app, fileName));
					++profileCount;
				}
				catch (Exception ex)
				{
					logger.LogError(ex, $"Unable to load profile from '{fileName}'");
				}
			}
			logger.LogDebug($"Complete loading {profileCount} profile(s)");
		}


		// Called when application string resources updated.
		static void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			foreach (var profile in profiles)
				profile.OnApplicationStringsUpdated();
		}


		// Called when profile changed.
		static void OnProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not LogProfile profile)
				return;
			switch (e.PropertyName)
			{
				case nameof(LogProfile.IsPinned):
					if (profile.IsPinned)
						pinnedProfiles.Add(profile);
					else
						pinnedProfiles.Remove(profile);
					goto default;
				case nameof(LogProfile.Name):
					if (!profile.IsBuiltIn)
						_ = profile.DeleteFileAsync();
					goto default;
				default:
					if (!profile.IsBuiltIn)
					{
						pendingSavingProfiles.Add(profile);
						saveProfilesAction?.Schedule(SaveProfilesDelay);
					}
					break;
			}
		}


		/// <summary>
		/// Get list of pinned <see cref="LogProfile"/>s.
		/// </summary>
		/// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
		public static IList<LogProfile> Pinned { get; }


		/// <summary>
		/// Remove given <see cref="LogProfile"/>.
		/// </summary>
		/// <param name="profile"><see cref="LogProfile"/> to remove.</param>
		public static void Remove(LogProfile profile)
		{
			app.AsNonNull().VerifyAccess();
			if (profile.IsBuiltIn)
				throw new InvalidOperationException("Cannot remove built-in profile.");
			DetachFromProfile(profile);
			_ = profile.DeleteFileAsync();
		}


		/// <summary>
		/// Wait for completion of all IO tasks.
		/// </summary>
		/// <returns>Task of waiting.</returns>
		public static async Task WaitForIOCompletionAsync()
		{
			logger?.LogDebug("Wait for IO completion");
			saveProfilesAction?.ExecuteIfScheduled();
			await LogProfile.WaitForIOCompletionAsync();
		}
	}
}
