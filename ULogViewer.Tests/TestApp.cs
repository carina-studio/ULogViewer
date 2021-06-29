using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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

				// complete
				lock (typeof(TestApp))
				{
					Monitor.Pulse(typeof(TestApp));
				}
			});
			Monitor.Wait(typeof(TestApp));
		}


		// Interface implementations.
		public bool CheckAccess() => Thread.CurrentThread == syncContext?.ExecutionThread;
		public string? GetString(string key, string? defaultValue = null) => defaultValue;
		public bool IsShutdownStarted => false;
		public bool IsTesting => true;
		public ILoggerFactory LoggerFactory => new LoggerFactory(new ILoggerProvider[] { new NLogLoggerProvider() });
		public event PropertyChangedEventHandler? PropertyChanged;
		public string RootPrivateDirectoryPath => Global.Run(() =>
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName);
			return Path.GetTempPath();
		}) ?? throw new ArgumentException("Unable to get directory of application.");
		public BaseSettings Settings => new Settings();
		public event EventHandler? StringsUpdated;
		public SynchronizationContext SynchronizationContext => syncContext ?? throw new InternalStateCorruptedException();
	}
}
