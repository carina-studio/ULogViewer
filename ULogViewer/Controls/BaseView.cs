using Avalonia.Controls;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Base implementation of <see cref="UserControl"/> for views in ULogViewer.
	/// </summary>
	abstract class BaseView : UserControl, IApplicationObject
	{
		// Static fields.
		static int nextId = 1;


		/// <summary>
		/// Initialize new <see cref="BaseView"/> instance.
		/// </summary>
		protected BaseView()
		{
			this.Application = App.Current;
			this.Application.VerifyAccess();
			this.Id = nextId++;
			this.Logger = this.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
		}


		/// <summary>
		/// Get application instance.
		/// </summary>
		protected IApplication Application { get; }


		/// <summary>
		/// Get unique ID of <see cref="BaseView"/> instance.
		/// </summary>
		protected int Id { get; }


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
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";
	}
}
