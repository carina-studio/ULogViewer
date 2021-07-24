using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Extensions for <see cref="Window"/>.
	/// </summary>
	static class WindowExtensions
	{
		/// <summary>
		/// Activate window and bring window to foreground.
		/// </summary>
		/// <param name="window"><see cref="Window"/>.</param>
		public static void ActivateAndBringToFront(this Window window)
		{
			window.VerifyAccess();
			window.Activate();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				SetForegroundWindow(window.PlatformImpl.Handle.Handle);
		}


		// Bring window to foreground.
		[DllImport("User32")]
		static extern bool SetForegroundWindow(IntPtr hWnd);
	}
}
