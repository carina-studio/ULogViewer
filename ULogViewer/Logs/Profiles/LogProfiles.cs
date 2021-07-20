using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.Profiles
{
	/// <summary>
	/// Class to manage <see cref="LogProfile"/>s.
	/// </summary>
	static class LogProfiles
	{
		// Fields.
		static volatile IApplication? app;
		static readonly IList<string> builtInProfileIDs = new List<string>(new string[] {
			"AndroidDeviceLog",
			"AndroidFileLog",
			"GitLog",
		});
		static volatile ILogger? logger;
		static readonly ObservableList<LogProfile> pinnedProfiles = new ObservableList<LogProfile>();
		static readonly ObservableList<LogProfile> profiles = new ObservableList<LogProfile>();
		static string profilesDirectoryPath = "";


		// Static initializer.
		static LogProfiles()
		{
			All = profiles.AsReadOnly();
			Pinned = pinnedProfiles.AsReadOnly();
#if DEBUG
			builtInProfileIDs.Add("ULogViewerLog");
#endif
		}


		/// <summary>
		/// Get all <see cref="LogProfile"/>s.
		/// </summary>
		/// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
		public static IList<LogProfile> All { get; }


		// Attach to profile.
		static void AttachToProfile(LogProfile profile)
		{
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

			// load build-in profiles
			logger.LogDebug("Start loading built-in profiles");
			var profileCount = 0;
			foreach (var id in builtInProfileIDs)
			{
				logger.LogDebug($"Load '{id}'");
				AttachToProfile(await LogProfile.LoadBuiltInProfileAsync(app, id));
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
					profiles.Add(await LogProfile.LoadProfileAsync(app, fileName));
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
					break;
			}
		}


		/// <summary>
		/// Get list of pinned <see cref="LogProfile"/>s.
		/// </summary>
		/// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
		public static IList<LogProfile> Pinned { get; }


		/// <summary>
		/// Wait for completion of all IO tasks.
		/// </summary>
		/// <returns>Task of waiting.</returns>
		public static async Task WaitForIOCompletionAsync()
		{
			logger?.LogDebug("Wait for IO completion");
			await LogProfile.WaitForIOCompletionAsync();
		}
	}
}
