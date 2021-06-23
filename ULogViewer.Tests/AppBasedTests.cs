using CarinaStudio.Threading;
using NUnit.Framework;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Base implementations of <see cref="App"/> based tests.
	/// </summary>
	abstract class AppBasedTests
	{
		// Fields.
		volatile App? app;


		/// <summary>
		/// Get <see cref="App"/> instance.
		/// </summary>
		protected App Application { get => this.app ?? throw new InvalidOperationException("Application is not ready."); }


		/// <summary>
		/// Run asynchronous testing on thread of <see cref="App"/>.
		/// </summary>
		/// <param name="test">Asynchronous test action.</param>
		protected void AsyncTestOnApplicationThread(Func<Task> asyncTest)
		{
			var app = this.Application;
			if (app.CheckAccess())
				asyncTest();
			else
			{
				var syncLock = new object();
				var awaiter = new TaskAwaiter();
				lock (syncLock)
				{
					app.SynchronizationContext.Post(() =>
					{
						awaiter = asyncTest().GetAwaiter();
						awaiter.OnCompleted(() =>
						{
							lock (syncLock)
							{
								Monitor.Pulse(syncLock);
							}
						});
					});
					Monitor.Wait(syncLock);
					awaiter.GetResult();
				}
			}
		}


		/// <summary>
		/// Setup <see cref="App"/> for testing.
		/// </summary>
		[OneTimeSetUp]
		public void SetupApp()
		{
			TestApp.Setup();
			this.app = App.Current;
		}


		/// <summary>
		/// Get <see cref="SynchronizationContext"/> provided by <see cref="App"/>.
		/// </summary>
		protected SynchronizationContext SynchronizationContext { get => this.app?.SynchronizationContext ?? throw new InvalidOperationException("Application is not ready."); }


		/// <summary>
		/// Run testing on thread of <see cref="App"/>.
		/// </summary>
		/// <param name="test">Test action.</param>
		protected void TestOnApplicationThread(Action test)
		{
			var app = this.Application;
			if (app.CheckAccess())
				test();
			else
				app.SynchronizationContext.Send(test);
		}
	}
}
