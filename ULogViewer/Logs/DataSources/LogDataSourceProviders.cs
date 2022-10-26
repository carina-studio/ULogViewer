using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>p
	/// Manage all <see cref="ILogDataSourceProvider"/>s in application.
	/// </summary>
	static class LogDataSourceProviders
	{
		// Fields.
		static volatile IULogViewerApplication? app;
		static volatile EmptyLogDataSourceProvider? empty;
		static volatile ILogger? logger;
		static readonly SortedObservableList<ILogDataSourceProvider> providers = new((lhs, rhs) => 
		{
			if (lhs is ScriptLogDataSourceProvider)
			{
				if (rhs is ScriptLogDataSourceProvider)
					return string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture);
				return 1;
			}
			if (rhs is ScriptLogDataSourceProvider)
				return -1;
			return string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture);
		});
		static readonly SortedObservableList<ScriptLogDataSourceProvider> scriptProviders = new((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture));


		/// <summary>
		/// Add new <see cref="ScriptLogDataSourceProvider"/>.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <returns>True if provider has been added successfully.</returns>
		public static bool AddScriptProvider(ScriptLogDataSourceProvider provider)
		{
			return false;
		}


		/// <summary>
		/// Get all providers.
		/// </summary>
		public static IList<ILogDataSourceProvider> All { get; } = ListExtensions.AsReadOnly(providers);


		/// <summary>
		/// Check whether at least one script log data source provider can be added or not.
		/// </summary>
		public static bool CanAddScriptProvider { get; private set; }


		/// <summary>
		/// Get empty implementation of <see cref="ILogDataSourceProvider"/>.
		/// </summary>
		public static ILogDataSourceProvider Empty { get => empty ?? throw new InvalidOperationException($"{nameof(LogDataSourceProviders)} is not initialized yet."); }


		/// <summary>
		/// Initialize asynchronously.
		/// </summary>
		/// <param name="app">Application.</param>
		public static async Task InitializeAsync(IULogViewerApplication app)
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

			// create built-in providers
			logger.LogDebug("Initialize");
			empty = new EmptyLogDataSourceProvider(app);
#if DEBUG
			providers.Add(new DummyLogDataSourceProvider(app));
#endif
			providers.Add(new FileLogDataSourceProvider(app));
			providers.Add(new HttpLogDataSourceProvider(app));
			providers.Add(new MemoryLoggerLogDataSourceProvider(app));
			providers.Add(new SQLiteLogDataSourceProvider(app));
			providers.Add(new StandardOutputLogDataSourceProvider(app));
			providers.Add(new TcpServerLogDataSourceProvider(app));
			providers.Add(new UdpServerLogDataSourceProvider(app));
			if (Platform.IsWindows)
				providers.Add(new WindowsEventLogDataSourceProvider(app));

			// find script log data source providers
			//
			
			// load script log data source providers
			await Task.Yield();
		}


		// Called when property of provider changed.
		static void OnProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
		}


		/// <summary>
		/// Remove <see cref="ScriptLogDataSourceProvider"/>.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <param name="deleteFiles">True to delete related files.</param>
		/// <returns>True if provider has been removed successfully.</returns>
		public static bool RemoveScriptProvider(ScriptLogDataSourceProvider provider, bool deleteFiles = true)
		{
			return false;
		}


		/// <summary>
		/// Get all <see cref="ScriptLogDataSourceProvider"/>s.
		/// </summary>
		public static IList<ScriptLogDataSourceProvider> ScriptProviders { get; } = ListExtensions.AsReadOnly(scriptProviders);


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


		/// <summary>
		/// Wait for completion of I/O tasks.
		/// </summary>
		/// <returns>Task of waiting.</returns>
		public static Task WaitForIOTaskCompletion()
		{
			return Task.CompletedTask;
		}
	}
}
