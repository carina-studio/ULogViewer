using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// <see cref="App"/> for testing.
	/// </summary>
	class TestApp : MockAppSuiteApplication, IULogViewerApplication
	{
		/// <summary>
		/// Get instance.
		/// </summary>
		public static new TestApp Current { get => (TestApp)MockAppSuiteApplication.Current; }


		/// <summary>
		/// Initialize instance.
		/// </summary>
		public static new void Initialize()
		{
			// create instance
			MockAppSuiteApplication.Initialize(() => new TestApp());
			var app = Current;

			// initialize
			var syncLock = new object();
			lock (syncLock)
			{
				Current.SynchronizationContext.Post(() =>
				{
					Logs.DataSources.LogDataSourceProviders.Initialize(app);
					lock (syncLock)
						Monitor.Pulse(syncLock);
				});
				Monitor.Wait(syncLock);
			}
		}


		/// <inheritdoc/>
		public bool IsTesting => true;


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
			var app = Current;

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
}
