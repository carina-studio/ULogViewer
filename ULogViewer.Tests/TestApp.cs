using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// <see cref="App"/> for testing.
	/// </summary>
	class TestApp : IApplication
	{
		// Fields.
		static volatile TestApp? current;
		static volatile SingleThreadSynchronizationContext? syncContext;


		/// <summary>
		/// Get <see cref="TestApp"/> instance.
		/// </summary>
		public static TestApp Current { get => current ?? throw new InvalidOperationException("Application is not ready."); }


		/// <summary>
		/// Entry.
		/// </summary>
		/// <param name="args">Arguments.</param>
		public static void Main(string[] args)
		{
			// build application
			Setup();
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


		/// <summary>
		/// Setup <see cref="TestApp"/> instance.
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void Setup()
		{
			if (current != null)
				return;
			syncContext = new SingleThreadSynchronizationContext();
			syncContext.Post(async () =>
			{
				// create application
				current = new TestApp();

				// initialize components
				LogDataSourceProviders.Initialize(current);
				await LogProfiles.InitializeAsync(current);
				await PredefinedLogTextFilters.InitializeAsync(current);

				// complete
				lock (typeof(TestApp))
				{
					Monitor.Pulse(typeof(TestApp));
				}
			});
			Monitor.Wait(typeof(TestApp));
		}


		// Interface implementations.
		public Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
		public bool CheckAccess() => Thread.CurrentThread == syncContext?.ExecutionThread;
		public CultureInfo CultureInfo { get; private set; } = CultureInfo.CurrentCulture;
		public string? GetString(string key, string? defaultValue = null) => defaultValue;
		public bool IsRunningAsAdministrator => false;
		public bool IsShutdownStarted => false;
		public bool IsTesting => true;
		public ILoggerFactory LoggerFactory => new LoggerFactory(new ILoggerProvider[] { new NLogLoggerProvider() });
		public ISettings PersistentState => new MemorySettings();
		public event PropertyChangedEventHandler? PropertyChanged;
		public bool Restart(string? args, bool asAdministrator) => false;
		public string RootPrivateDirectoryPath => Global.Run(() =>
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
			return Path.GetTempPath();
		}) ?? throw new ArgumentException("Unable to get directory of application.");
		public ISettings Settings => new Settings();
		public AppStartupParams StartupParams => new AppStartupParams();
		public event EventHandler? StringsUpdated;
		public SynchronizationContext SynchronizationContext => syncContext ?? throw new InternalStateCorruptedException();
		public AppUpdateInfo? UpdateInfo => null;
	}
}
