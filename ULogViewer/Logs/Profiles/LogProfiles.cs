using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
		static readonly string[] builtInProfileIDs = new string[] {
			"GitLog",
		};
		static volatile ILogger? logger;
		static readonly ObservableList<LogProfile> profiles = new ObservableList<LogProfile>();


		// Static initializer.
		static LogProfiles()
		{
			All = profiles.AsReadOnly();
		}


		/// <summary>
		/// Get all <see cref="LogProfile"/>s.
		/// </summary>
		/// <remarks>The list will implement <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> interface.</remarks>
		public static IList<LogProfile> All { get; }


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

			// create logger
			logger = app.LoggerFactory.CreateLogger(nameof(LogProfiles));

			// load build-in profiles
			logger.LogDebug("Start loading built-in profiles");
			foreach (var id in builtInProfileIDs)
			{
				logger.LogDebug($"Load '{id}'");
				profiles.Add(await LogProfile.LoadBuiltInProfileAsync(app, id));
			}
			logger.LogDebug("Complete loading built-in profiles");
		}
	}
}
