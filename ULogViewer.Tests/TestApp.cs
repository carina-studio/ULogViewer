using CarinaStudio.AppSuite;
using Microsoft.Extensions.Logging;
using System;
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
        public static new void Initialize() => MockAppSuiteApplication.Initialize(() => new TestApp());


		/// <inheritdoc/>
		public bool IsTesting => true;


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
			var logger = app.LoggerFactory.CreateLogger(nameof(TestApp));
			for (var i = 1; i < int.MaxValue; ++i)
			{
				logger.LogTrace($"Log #{i}");
				Thread.Sleep(1000);
			}
		}
	}
}
