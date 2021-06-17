using Avalonia;
using CarinaStudio;
using CarinaStudio.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application.
	/// </summary>
	class App : Application, IApplication
	{
		// Fields.
		volatile Settings? settings;
		volatile SynchronizationContext? synchronizationContext;


		/// <summary>
		/// Get <see cref="App"/> instance for current process.
		/// </summary>
		public static new App Current
		{
			get => (App)Application.Current;
		}


		// Get string.
		public string? GetString(string key, string? defaultValue = null) => defaultValue;


		// Program entry.
		static void Main(string[] args)
		{ }


		// Interface implementations.
		public bool IsShutdownStarted { get; private set; }
		public ILoggerFactory LoggerFactory => new LoggerFactory(new ILoggerProvider[] { new NLogLoggerProvider() });
		public string RootPrivateDirectoryPath => Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) ?? throw new ArgumentException("Unable to get directory of application.");
		public BaseSettings Settings { get => this.settings ?? throw new InvalidOperationException("Application is not ready."); }
		public SynchronizationContext SynchronizationContext { get => this.synchronizationContext ?? throw new InvalidOperationException("Application is not ready."); }
	}
}
