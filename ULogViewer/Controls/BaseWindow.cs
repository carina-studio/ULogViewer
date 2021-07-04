using Avalonia;
using Avalonia.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Base implementation of <see cref="Window"/> for windows in ULogViewer.
	/// </summary>
	abstract class BaseWindow : Window, IApplicationObject
	{
		/// <summary>
		/// Property of <see cref="HasDialogs"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> HasDialogsProperty = AvaloniaProperty.Register<BaseWindow, bool>(nameof(HasDialogs), false);


		// Fields.
		readonly List<BaseDialog> dialogs = new List<BaseDialog>();


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
		/// Get whether at least one dialog owned by this window is shown or not.
		/// </summary>
		public bool HasDialogs { get => this.GetValue<bool>(HasDialogsProperty); }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Called when dialog closed.
		/// </summary>
		/// <param name="dialog">Closed dialog.</param>
		internal protected virtual void OnDialogClosed(BaseDialog dialog)
		{
			if (this.dialogs.Remove(dialog) && this.dialogs.IsEmpty())
				this.SetValue<bool>(HasDialogsProperty, false);
		}


		/// <summary>
		/// Called when dialog opened.
		/// </summary>
		/// <param name="dialog">Opened dialog.</param>
		internal protected virtual void OnDialogOpened(BaseDialog dialog)
		{
			this.dialogs.Add(dialog);
			if (this.dialogs.Count == 1)
				this.SetValue<bool>(HasDialogsProperty, true);
		}


		/// <summary>
		/// Get application settings.
		/// </summary>
		protected Settings Settings { get => (Settings)this.Application.Settings; }


		// Interface implementation.
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
