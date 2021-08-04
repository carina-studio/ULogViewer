using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using System;
using System.Collections;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Extensions for <see cref="ListBox"/>.
	/// </summary>
	static class ListBoxExtensions
	{
		/// <summary>
		/// Find <see cref="ListBoxItem"/> of given item in <see cref="ListBox"/>.
		/// </summary>
		/// <param name="listBox"><see cref="ListBox"/>.</param>
		/// <param name="item">Item.</param>
		/// <returns><see cref="ListBoxItem"/> or null if not found.</returns>
		public static ListBoxItem? FindListBoxItem(this ListBox listBox, object item)
		{
			var presenter = listBox.FindDescendantOfType<ItemsPresenter>();
			if (presenter == null)
				return null;
			foreach (var child in presenter.GetVisualChildren())
			{
				if (child is Panel panel)
				{
					foreach (var panelChild in panel.Children)
					{
						if (panelChild is ListBoxItem listBoxItem && listBoxItem.DataContext == item)
							return listBoxItem;
					}
				}
			}
			return null;
		}


		/// <summary>
		/// Select first item in <see cref="ListBox"/>.
		/// </summary>
		/// <param name="listBox"><see cref="ListBox"/>.</param>
		/// <param name="scrollIntoView">True to scroll new selected item into view.</param>
		public static void SelectFirstItem(this ListBox listBox, bool scrollIntoView = true)
		{
			var itemCount = listBox.GetItemCount();
			if (itemCount > 0)
			{
				listBox.SelectedIndex = 0;
				if (scrollIntoView)
					listBox.ScrollIntoView(0);
			}
		}


		/// <summary>
		/// Select last item in <see cref="ListBox"/>.
		/// </summary>
		/// <param name="listBox"><see cref="ListBox"/>.</param>
		/// <param name="scrollIntoView">True to scroll new selected item into view.</param>
		public static void SelectLastItem(this ListBox listBox, bool scrollIntoView = true)
		{
			var itemCount = listBox.GetItemCount();
			if (itemCount > 0)
			{
				listBox.SelectedIndex = itemCount - 1;
				if (scrollIntoView)
					listBox.ScrollIntoView(itemCount - 1);
			}
		}


		/// <summary>
		/// Select next item in <see cref="ListBox"/>.
		/// </summary>
		/// <param name="listBox"><see cref="ListBox"/>.</param>
		/// <param name="scrollIntoView">True to scroll new selected item into view.</param>
		/// <returns>New index of selected item.</returns>
		public static int SelectNextItem(this ListBox listBox, bool scrollIntoView = true)
		{
			var itemCount = listBox.GetItemCount();
			if (itemCount > 0)
			{
				var newIndex = listBox.SelectedIndex;
				if (newIndex < itemCount - 1)
				{
					++newIndex;
					listBox.SelectedIndex = newIndex;
					if (scrollIntoView)
						listBox.ScrollIntoView(newIndex);
				}
				return newIndex;
			}
			return listBox.SelectedIndex;
		}


		/// <summary>
		/// Select previous item in <see cref="ListBox"/>.
		/// </summary>
		/// <param name="listBox"><see cref="ListBox"/>.</param>
		/// <param name="scrollIntoView">True to scroll new selected item into view.</param>
		/// <returns>New index of selected item.</returns>
		public static int SelectPreviousItem(this ListBox listBox, bool scrollIntoView = true)
		{
			var itemCount = listBox.GetItemCount();
			if (itemCount > 0)
			{
				var newIndex = listBox.SelectedIndex;
				if (newIndex < 0)
					newIndex = itemCount - 1;
				else if (newIndex > 0)
					--newIndex;
				else
					return newIndex;
				listBox.SelectedIndex = newIndex;
				if (scrollIntoView)
					listBox.ScrollIntoView(newIndex);
				return newIndex;
			}
			return listBox.SelectedIndex;
		}
	}
}
