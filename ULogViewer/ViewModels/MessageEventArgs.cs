using System;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Data for message related events.
	/// </summary>
	class MessageEventArgs : EventArgs
	{
		/// <summary>
		/// Initialize new <see cref="MessageEventArgs"/> instance.
		/// </summary>
		/// <param name="message">Message.</param>
		public MessageEventArgs(string message) => this.Message = message;


		/// <summary>
		/// Get message.
		/// </summary>
		public string Message { get; }
	}
}
