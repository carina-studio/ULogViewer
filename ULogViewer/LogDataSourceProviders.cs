using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Manage all <see cref="ILogDataSourceProvider"/>s in application.
	/// </summary>
	static class LogDataSourceProviders
	{
		// Fields.
		static volatile App? app;
		static volatile ILogger? logger;
		static readonly List<ILogDataSourceProvider> providers = new List<ILogDataSourceProvider>();


		/// <summary>
		/// Get all providers.
		/// </summary>
		public static IList<ILogDataSourceProvider> All { get; } = providers.AsReadOnly();


		/// <summary>
		/// Initialize <see cref="ILogDataSourceProvider"/>s.
		/// </summary>
		/// <param name="app">Application.</param>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void Initialize(App app)
		{
			// check state
			app.VerifyAccess();
			if (LogDataSourceProviders.app != null)
			{
				if (LogDataSourceProviders.app != app)
					throw new InvalidOperationException("Initialize by different application instances.");
				return;
			}

			// attach to application
			LogDataSourceProviders.app = app;

			// create logger
			logger = app.LoggerFactory.CreateLogger(typeof(LogDataSourceProviders).Name);

			// create providers
			logger.LogDebug("Initialize");
		}


		/// <summary>
		/// Try finding <see cref="ILogDataSourceProvider"/> by name of provider.
		/// </summary>
		/// <param name="name">Name of provider.</param>
		/// <param name="provider">Found provider.</param>
		/// <returns>True if provider found.</returns>
		public static bool TryFindProviderByName(string name, out ILogDataSourceProvider? provider)
		{
			foreach (var candidate in providers)
			{
				if (candidate.Name == name)
				{
					provider = candidate;
					return true;
				}
			}
			provider = null;
			return false;
		}
	}
}
