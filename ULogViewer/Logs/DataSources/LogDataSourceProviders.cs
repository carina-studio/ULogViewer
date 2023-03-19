using CarinaStudio.AppSuite.Product;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
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
		static readonly Dictionary<string, ILogDataSourceProvider> providersByName = new();
		static ScheduledAction? saveScriptProvidersAction;
		static readonly SortedObservableList<ScriptLogDataSourceProvider> scriptProviders = new((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture));
		static string scriptProvidersDirectory = "";
		static Task? scriptProvidersSavingTask;
		static readonly HashSet<ScriptLogDataSourceProvider> scriptProvidersToSave = new();


		/// <summary>
		/// Add new <see cref="ScriptLogDataSourceProvider"/>.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <returns>True if provider has been added successfully.</returns>
		public static bool AddScriptProvider(ScriptLogDataSourceProvider provider) =>
			AddScriptProvider(provider, true);


		// Add script log data source.
		static bool AddScriptProvider(ScriptLogDataSourceProvider provider, bool saveToFile)
		{
			// check state
			if (app == null)
				throw new InvalidOperationException();
			app.VerifyAccess();
			if (providersByName.ContainsKey(provider.Name))
			{
				logger!.LogError("Script log data source provider '{providerDisplayName}' ({providerName}) is already been added", provider.DisplayName, provider.Name);
				return false;
			}
			var isProVersionActivated = app?.ProductManager?.IsProductActivated(Products.Professional) == true;
			if (!isProVersionActivated && scriptProviders.IsNotEmpty())
			{
				logger!.LogError("Cannot add script log data source provider '{providerDisplayName}' ({providerName}) in non-Pro version", provider.DisplayName, provider.Name);
				return false;
			}

			// add provider
			logger!.LogDebug("Add script log data source provider '{providerDisplayName}' ({providerName})", provider.DisplayName, provider.Name);
			providersByName[provider.Name] = provider;
			scriptProviders.Add(provider);
			providers.Add(provider);
			provider.PropertyChanged += OnProviderPropertyChanged;
			if (!isProVersionActivated)
				CanAddScriptProvider = false;

			// save to file
			if (saveToFile)
			{
				scriptProvidersToSave.Add(provider);
				saveScriptProvidersAction ??= new(SaveScriptProviders);
				saveScriptProvidersAction.Schedule();
			}

			// complete
			return true;
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

			// attach to product manager
			app.ProductManager.ProductActivationChanged += OnProductActivationChanged;

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
			foreach (var provider in providers)
				providersByName[provider.Name] = provider;

			// load script log data source providers
			scriptProvidersDirectory = Path.Combine(app.RootPrivateDirectoryPath, "ScriptLogDataSourceProviders");
			await LoadScriptLogDataSourceProvidersAsync();
			if (app.ProductManager.IsProductActivated(Products.Professional)
				|| scriptProviders.IsEmpty())
			{
				CanAddScriptProvider = true;
			}
		}


		// Load script log data sources from files.
		static async Task LoadScriptLogDataSourceProvidersAsync()
		{
			// check Pro version
			if (app?.ProductManager?.IsProductActivated(Products.Professional) != true)
			{
				logger?.LogWarning("Skip loading script log data source providers");
				return;
			}

			// find script log data source providers
			var scriptProviderFiles = await Task.Run(() =>
			{
				var fileNames = new List<string>();
				try
				{
					if (Directory.Exists(scriptProvidersDirectory))
						fileNames.AddAll(Directory.EnumerateFiles(scriptProvidersDirectory, "*.json"));
				}
				catch (Exception ex)
				{
					logger!.LogError(ex, "Error occurred while checking file in '{scriptProvidersDirectory}'", scriptProvidersDirectory);
				}
				return fileNames;
			});
			logger!.LogDebug("{scriptProviderFiles.Count} script log data source provider file(s) found", scriptProvidersDirectory);

			// load script log data source providers
			foreach (var fileName in scriptProviderFiles)
			{
				try
				{
					var provider = await ScriptLogDataSourceProvider.LoadAsync(app, fileName);
					if (providersByName.ContainsKey(provider.Name))
					{
						logger!.LogDebug("Skip adding loaded script log data source provuder '{providerDisplayName}' ({providerName})", provider.DisplayName, provider.Name);
						continue;
					}
					AddScriptProvider(provider, false);
				}
				catch (Exception ex)
				{
					logger!.LogError(ex, "Error occurred while loading script log data source provider from file '{fileName}'", fileName);
				}
			}
			logger!.LogDebug("{count} script log data source provider(s) loaded", scriptProviders.Count);
		}


		// Called when product state changed.
		static void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
		{
			if (productId != Products.Professional)
				return;
			if (isActivated)
				OnProVersionActivated();
			else
				OnProVersionDeactivated();
		}


		// Called when Pro version activated.
		static void OnProVersionActivated()
		{
			saveScriptProvidersAction ??= new(SaveScriptProviders);
			foreach (var provider in scriptProviders)
			{
				scriptProvidersToSave.Add(provider);
				saveScriptProvidersAction.Schedule();
			}
			_ = LoadScriptLogDataSourceProvidersAsync();
			CanAddScriptProvider = true;
		}


		// Called when Pro version deactivated or removed.
		static void OnProVersionDeactivated()
		{
			foreach (var provider in scriptProviders.ToArray())
				RemoveScriptProvider(provider, false);
			CanAddScriptProvider = true;
		}


		// Called when property of provider changed.
		static void OnProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is ScriptLogDataSourceProvider scriptProvider)
			{
				scriptProvidersToSave.Add(scriptProvider);
				saveScriptProvidersAction ??= new(SaveScriptProviders);
				saveScriptProvidersAction.Schedule();
			}
		}


		/// <summary>
		/// Remove <see cref="ScriptLogDataSourceProvider"/>.
		/// </summary>
		/// <param name="provider">Provider.</param>
		/// <param name="deleteFiles">True to delete related files.</param>
		/// <returns>True if provider has been removed successfully.</returns>
		public static bool RemoveScriptProvider(ScriptLogDataSourceProvider provider, bool deleteFiles = true)
		{
			// check state
			if (app == null)
				throw new InvalidOperationException();
			app.VerifyAccess();

			// try removing from map
			if (!providersByName.Remove(provider.Name))
				return false;
			logger!.LogDebug("Start removing script log data source provider '{providerDisplayName}' ({providerName})", provider.DisplayName, provider.Name);
			
			// find related log profiles
			var logProfiles = Profiles.LogProfileManager.Default.Profiles.Where(it => it.DataSourceProvider == provider).ToArray();
			if (logProfiles.IsNotEmpty())
			{
				logger!.LogWarning("Remove {logProfilesLength} related log profile(s) first", logProfiles.Length);
				foreach (var logProfile in logProfiles)
				{
					logger!.LogWarning("Remove log profile '{logProfileName}' ({logProfileId})", logProfile.Name, logProfile.Id);
					Profiles.LogProfileManager.Default.RemoveProfile(logProfile);
				}
			}
			
			// remove provider completely
			provider.PropertyChanged -= OnProviderPropertyChanged;
			scriptProviders.Remove(provider);
			providers.Remove(provider);
			scriptProvidersToSave.Remove(provider);
			logger!.LogDebug("Complete removing script log data source provider '{providerDisplayName}' ({providerName})", provider.DisplayName, provider.Name);

			// delete file
			if (deleteFiles)
			{
				var savingTask = scriptProvidersSavingTask;
				Task.Run(async () =>
				{
					var fileName = Path.Combine(scriptProvidersDirectory, $"{provider.Name}.json");
					try
					{
						if (savingTask != null)
							await savingTask;
						if (File.Exists(fileName))
						{
							logger!.LogTrace("Delete script log data source provider file '{fileName}'", fileName);
							File.Delete(fileName);
						}
					}
					catch (Exception ex)
					{
						logger!.LogError(ex, "Failed to delete script log data source provider file '{fileName}'", fileName);
					}
				});
			}

			// update state
			if (app?.ProductManager?.IsProductActivated(Products.Professional) != true
				&& scriptProviders.IsEmpty())
			{
				CanAddScriptProvider = true;
			}

			// complete
			return true;
		}


		// Save script providers.
		static async void SaveScriptProviders()
		{
			// check state
			if (scriptProvidersToSave.IsEmpty())
				return;
			var providers = scriptProvidersToSave.ToArray();
			scriptProvidersToSave.Clear();
			if (app?.ProductManager?.IsProductActivated(Products.Professional) != true)
			{
				logger!.LogDebug("Skip saving script log data source providers");
				return;
			}
			
			logger!.LogTrace("Start saving {providersLength} script log data source provider(s)", providers.Length);
			
			// wait for previous saving task
			var prevTask = scriptProvidersSavingTask;
			var taskCompletionSource = new TaskCompletionSource();
			scriptProvidersSavingTask = taskCompletionSource.Task;
			if (prevTask != null)
			{
				logger!.LogWarning($"Wait for previous saving");
				await prevTask;
			}

			// save providers
			await Task.Run(() =>
			{
				try
				{
					if (!Directory.Exists(scriptProvidersDirectory))
					{
						logger!.LogDebug("Create directory '{scriptProvidersDirectory}'", scriptProvidersDirectory);
						Directory.CreateDirectory(scriptProvidersDirectory);
					}
				}
				catch (Exception ex)
				{
					logger!.LogError(ex, "Error occurred while creating directory '{scriptProvidersDirectory}'", scriptProvidersDirectory);
				}
			});
			foreach (var provider in providers)
				await provider.SaveAsync(Path.Combine(scriptProvidersDirectory, $"{provider.Name}.json"));

			// complete
			logger!.LogTrace("Complete saving {providersLength} script log data source provider(s)", providers.Length);
			taskCompletionSource.SetResult();
			if (scriptProvidersSavingTask == taskCompletionSource.Task)
				scriptProvidersSavingTask = null;
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
		public static bool TryFindProviderByName(string name, [NotNullWhen(true)] out ILogDataSourceProvider? provider) =>
			providersByName.TryGetValue(name, out provider);


		/// <summary>
		/// Wait for completion of I/O tasks.
		/// </summary>
		/// <returns>Task of waiting.</returns>
		public static Task WaitForIOTaskCompletion()
		{
			saveScriptProvidersAction?.ExecuteIfScheduled();
			return scriptProvidersSavingTask ?? Task.CompletedTask;
		}
	}
}
