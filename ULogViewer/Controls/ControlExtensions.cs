using Avalonia.Controls;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Extensions for <see cref="Control"/>.
	/// </summary>
	static class ControlExtensions
	{
		/// <summary>
		/// Find child control by name.
		/// </summary>
		/// <typeparam name="T">Type of child control.</typeparam>
		/// <param name="control">Parent control.</param>
		/// <param name="name">Name of child control.</param>
		/// <returns>Child control with specific name or null if no child control found.</returns>
		public static T? FindChildControl<T>(this Control control, string name) where T : Control
		{
			if (control is ContentControl contentControl)
			{
				if (contentControl.Content is not Control child)
					return null;
				if (child.Name == name)
					return (T)child;
				return child.FindChildControl<T>(name);
			}
			else if (control is Decorator decorator)
			{
				var child = decorator.Child;
				if (child?.Name == name)
					return (T)child;
				return child?.FindChildControl<T>(name);
			}
			else if (control is Panel panel)
			{
				foreach (var child in panel.Children)
				{
					if (child.Name == name)
						return (T)child;
				}
				foreach (var child in panel.Children)
				{
					var result = child.FindChildControl<T>(name);
					if (result != null)
						return result;
				}
			}
			return null;
		}
	}
}
