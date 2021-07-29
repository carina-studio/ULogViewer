using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using System;

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
	}
}
