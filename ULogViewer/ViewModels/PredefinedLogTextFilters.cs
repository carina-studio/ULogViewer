using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Manager of <see cref="PredefinedLogTextFilter"/>.
	/// </summary>
	static class PredefinedLogTextFilters
	{
		// Fields.
		static IApplication? app;
		static string directoryPath = "";
		static ILogger? logger;
		static readonly ObservableList<PredefinedLogTextFilter> filters = new ObservableList<PredefinedLogTextFilter>();


		/// <summary>
		/// Get all <see cref="PredefinedLogTextFilter"/>s.
		/// </summary>
		public static IList<PredefinedLogTextFilter> All { get; } = filters.AsReadOnly();


		/// <summary>
		/// Add <see cref="PredefinedLogTextFilter"/>.
		/// </summary>
		/// <param name="filter"><see cref="PredefinedLogTextFilter"/> to add.</param>
		public static void Add(PredefinedLogTextFilter filter)
		{
			// check state
			var app = PredefinedLogTextFilters.app ?? throw new InvalidOperationException();
			app.VerifyAccess();
			if (filters.Contains(filter))
				return;

			// add filter
			filters.Add(filter);
		}


		/// <summary>
		/// Initialize asynchronously.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <returns>Task of initialization.</returns>
		public static async Task InitializeAsync(IApplication app)
		{
			// check state
			app.VerifyAccess();
			if (PredefinedLogTextFilters.app != null)
			{
				if (PredefinedLogTextFilters.app == app)
					return;
				throw new InvalidOperationException("Already initialized by another application instance.");
			}

			// keep application
			PredefinedLogTextFilters.app = app;

			// create logger
			logger = app.LoggerFactory.CreateLogger(typeof(PredefinedLogTextFilters).Name);
			logger.LogDebug("Initialize");

			// find filter files
			directoryPath = Path.Combine(app.RootPrivateDirectoryPath, "TextFilters");
			var filterFileNames = await Task.Run(() =>
			{
				try
				{
					if (Directory.Exists(directoryPath))
						return Directory.GetFiles(directoryPath, "*.json");
					return new string[0];
				}
				catch(Exception ex)
				{
					logger.LogError(ex, $"Unable to find filter files in '{directoryPath}'");
					return new string[0];
				}
			});
			logger.LogDebug($"Found {filterFileNames.Length} filter file(s)");
			if (filterFileNames.IsEmpty())
				return;

			// load filters
			foreach (var fileName in filterFileNames)
			{
				try
				{
					filters.Add(await PredefinedLogTextFilter.LoadAsync(app, fileName));
				}
				catch (Exception ex)
				{
					logger.LogError(ex, $"Unable to load filter from file '{fileName}'");
				}
			}
			logger.LogDebug($"{filters.Count} filter(s) loaded");
		}


		/// <summary>
		/// Remove <see cref="PredefinedLogTextFilter"/>.
		/// </summary>
		/// <param name="filter"><see cref="PredefinedLogTextFilter"/> to remove.</param>
		public static void Remove(PredefinedLogTextFilter filter)
		{
			// check state
			var app = PredefinedLogTextFilters.app ?? throw new InvalidOperationException();
			app.VerifyAccess();

			// remove
			if (!filters.Remove(filter))
				return;

			// delete file
			filter.FileName?.Let(fileName =>
			{
				Task.Run(() => Global.RunWithoutError(() => File.Delete(fileName)));
			});
		}


		/// <summary>
		/// Save all filters to file asynchronously.
		/// </summary>
		/// <returns></returns>
		public static async Task SaveAllAsync()
		{
			// check state
			var app = PredefinedLogTextFilters.app ?? throw new InvalidOperationException();
			app.VerifyAccess();

			// save
			var savedCount = 0;
			logger?.LogDebug("Start saving all filters");
			foreach (var filter in filters)
			{
				var fileName = filter.FileName;
				if (fileName == null)
				{
					try
					{
						fileName = await filter.FindValidFileNameAsync(directoryPath);
					}
					catch (Exception ex)
					{
						logger?.LogError(ex, $"Unable to find file name for filter '{filter.Name}'");
						continue;
					}
				}
				try
				{
					await filter.SaveAsync(fileName);
					++savedCount;
				}
				catch (Exception ex)
				{
					logger?.LogError(ex, $"Unable to save filter '{filter.Name}' to file '{fileName}'");
				}
			}
			logger?.LogDebug($"{savedCount} filter(s) saved");
		}
	}
}
