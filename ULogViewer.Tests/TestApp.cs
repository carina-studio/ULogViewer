using Avalonia;
using Avalonia.ReactiveUI;
using CarinaStudio.Threading;
using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// <see cref="App"/> for testing.
	/// </summary>
	class TestApp : App
	{
		// Fields.
		static volatile bool isSetupCompleted;
		static volatile SingleThreadSynchronizationContext? syncContext;


		// Constructor.
		public TestApp() : base(true)
		{ }


		// Avalonia configuration, don't remove; also used by visual designer.
		static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
			.UsePlatformDetect()
			.UseReactiveUI()
			.LogToTrace();


		// Called when Avalonia initialization completed.
		public override void OnFrameworkInitializationCompleted()
		{
			base.OnFrameworkInitializationCompleted();
			lock (typeof(TestApp))
			{
				Monitor.Pulse(typeof(TestApp));
			}
		}


		/// <summary>
		/// Setup <see cref="TestApp"/> instance.
		/// </summary>
		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void Setup()
		{
			if (isSetupCompleted)
				return;
			isSetupCompleted = true;
			syncContext = new SingleThreadSynchronizationContext();
			syncContext.Post(() =>
			{
				BuildAvaloniaApp().StartWithClassicDesktopLifetime(new string[0]);
			});
			Monitor.Wait(typeof(TestApp));
		}
	}
}
