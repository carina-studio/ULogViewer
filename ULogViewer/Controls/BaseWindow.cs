using Avalonia.Controls;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Base implementation of <see cref="Window"/> for windows in ULogViewer.
	/// </summary>
	abstract class BaseWindow : Window, IApplicationObject
	{
		/// <summary>
		/// Initialize new <see cref="BaseWindow"/> instance.
		/// </summary>
		protected BaseWindow()
		{
			this.Application = App.Current;
			this.Application.VerifyAccess();
			this.Logger = this.Application.LoggerFactory.CreateLogger(this.GetType().Name);
		}


		/// <summary>
		/// Get application instance.
		/// </summary>
		protected IApplication Application { get; }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Get application settings.
		/// </summary>
		protected Settings Settings { get => (Settings)this.Application.Settings; }


		// Interface implementation.
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
