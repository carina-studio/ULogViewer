using Avalonia.Controls;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Extensions for <see cref="ScrollViewer"/>.
	/// </summary>
	static class ScrollViewerExtensions
	{
		/// <summary>
		/// Scroll given control in <see cref="ScrollViewer"/> into view.
		/// </summary>
		/// <param name="scrollViewer"><see cref="ScrollViewer"/>.</param>
		/// <param name="control">Control inside <see cref="ScrollViewer"/>.</param>
		/// <returns>True if control has been scrolled into view.</returns>
		public static bool ScrollIntoView(this ScrollViewer scrollViewer, IControl control)
		{
			// check size
			if (scrollViewer == control)
				return false;
			var scrollViewerBounds = scrollViewer.Bounds;
			var controlBounds = control.Bounds;
			if (scrollViewerBounds.IsEmpty || controlBounds.IsEmpty)
				return false;

			// calculate offset in scroll viewer
			var offsetTop = controlBounds.Top;
			var offsetLeft = controlBounds.Left;
			var parent = control.Parent;
			while (parent != scrollViewer && parent != null)
			{
				offsetTop += parent.Bounds.Top;
				offsetLeft += parent.Bounds.Left;
				parent = parent.Parent;
			}
			if (parent == null)
				return false;
			var offsetBottom = offsetTop + controlBounds.Height;
			var offsetRight = offsetLeft + controlBounds.Width;

			// scroll
			var scrollOffset = scrollViewer.Offset;
			var scrollOffsetX = scrollOffset.X;
			var scrollOffsetY = scrollOffset.Y;
			var isScrollNeeded = false;
			if (offsetLeft < 0)
			{
				scrollOffsetX += offsetLeft;
				isScrollNeeded = true;
			}
			else if (offsetRight > scrollViewerBounds.Width)
			{
				scrollOffsetX += offsetRight - scrollViewerBounds.Width;
				isScrollNeeded = true;
			}
			if (offsetTop < 0)
			{
				scrollOffsetY += offsetTop;
				isScrollNeeded = true;
			}
			else if (offsetBottom > scrollViewerBounds.Height)
			{
				scrollOffsetY += offsetBottom - scrollViewerBounds.Height;
				isScrollNeeded = true;
			}
			if (isScrollNeeded)
				scrollViewer.Offset = new Avalonia.Vector(scrollOffsetX, scrollOffsetY);
			return true;
		}
	}
}
