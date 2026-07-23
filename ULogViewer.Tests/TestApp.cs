using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// <see cref="App"/> for testing.
/// </summary>
class TestApp : MockAppSuiteApplication, IULogViewerApplication
{
	/// <summary>
	/// Initialize instance.
	/// </summary>
	/// <returns><see cref="TestApp"/> instance.</returns>
	public static new TestApp Initialize()
	{
		// create instance
		var app = (TestApp)MockAppSuiteApplication.Initialize(() => new TestApp());

		// initialize
		Task? initTask = null;
		app.SynchronizationContext.Send(() => initTask = Logs.DataSources.LogDataSourceProviders.InitializeAsync(app));
		initTask.AsNonNull().GetAwaiter().GetResult();
		return app;
	}


	/// <inheritdoc/>
	public override ILoggerFactory LoggerFactory { get; } = new NLogLoggerFactory();


    /// <summary>
    /// Entry.
    /// </summary>
    /// <param name="args">Arguments.</param>
    public static void Main(string[] args)
	{
		// build application
		Initialize();

		// print logs
		Console.WriteLine("Start writing logs...");
		var logLevels = NLog.LogLevel.AllLoggingLevels.ToArray();
		var logger = NLog.LogManager.GetLogger(nameof(TestApp));
		for (var i = 1; i < int.MaxValue; ++i)
		{
			logger.Log(logLevels.SelectRandomElement(), $"Log #{i}");
			Thread.Sleep(1000);
		}
	}
}