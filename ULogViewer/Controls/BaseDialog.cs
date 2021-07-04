using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Base class for dialog in ULogViewer.
	/// </summary>
	abstract class BaseDialog : BaseWindow
	{
		// Fields.
		BaseWindow? ownerWindow;


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			this.ownerWindow?.OnDialogClosed(this);
			this.ownerWindow = null;
			base.OnClosed(e);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			this.ownerWindow = (this.Owner as BaseWindow)?.Also(it => it.OnDialogOpened(this));
		}
	}
}
