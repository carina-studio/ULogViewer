using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
		/// <summary>
		/// Property of <see cref="IsClosed"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsClosedProperty = AvaloniaProperty.Register<BaseWindow, bool>(nameof(IsClosed), false);
		/// <summary>
		/// Property of <see cref="IsOpened"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsOpenedProperty = AvaloniaProperty.Register<BaseWindow, bool>(nameof(IsOpened), false);


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
			this.AddHandler(PointerWheelChangedEvent, (_, e) =>
			{
				if (this.HasDialogs)
					e.Handled = true;
			}, Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
		/// Check whether window is closed or not.
		/// </summary>
		public bool IsClosed { get => this.GetValue<bool>(IsClosedProperty); }


		/// <summary>
		/// Check whether window is opened or not.
		/// </summary>
		public bool IsOpened { get => this.GetValue<bool>(IsOpenedProperty); }


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			this.SetValue<bool>(IsOpenedProperty, false);
			this.SetValue<bool>(IsClosedProperty, true);
			base.OnClosed(e);
		}


		/// <summary>
		/// Called when dialog closed.
		/// </summary>
		/// <param name="dialog">Closed dialog.</param>
		internal protected virtual void OnDialogClosed(BaseDialog dialog)
		{
			if (this.dialogs.Remove(dialog) && this.dialogs.IsEmpty())
			{
				(this.Content as Control)?.Let(it => it.Opacity = 1);
				this.SetValue<bool>(HasDialogsProperty, false);
			}
		}


		/// <summary>
		/// Called when dialog opened.
		/// </summary>
		/// <param name="dialog">Opened dialog.</param>
		internal protected virtual void OnDialogOpened(BaseDialog dialog)
		{
			this.dialogs.Add(dialog);
			if (this.dialogs.Count == 1)
			{
				(this.Content as Control)?.Let(it => it.Opacity = 0.2);
				this.SetValue<bool>(HasDialogsProperty, true);
			}
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			this.SetValue<bool>(IsOpenedProperty, true);
			base.OnOpened(e);
		}


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == ContentProperty && change.NewValue.Value is Control control)
			{
				var transitions = control.Transitions ?? new Transitions().Also(it => control.Transitions = it);
				transitions.Add(new DoubleTransition()
				{
					Duration = TimeSpan.FromMilliseconds(500),
					Property = OpacityProperty
				});
			}
		}


		/// <summary>
		/// Open given URI by default browser.
		/// </summary>
		/// <param name="uri">URI to open.</param>
		protected void OpenLink(string uri) => this.OpenLink(new Uri(uri));


		/// <summary>
		/// Open given <see cref="Uri"/> by default browser.
		/// </summary>
		/// <param name="uri"><see cref="Uri"/> to open.</param>
		protected void OpenLink(Uri uri)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					Process.Start(new ProcessStartInfo("cmd", $"/c start {uri}")
					{
						CreateNoWindow = true
					});
				}
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
					Process.Start("xdg-open", uri.ToString());
				else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
					Process.Start("open", uri.ToString());
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Unable to open link: {uri}");
			}
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
