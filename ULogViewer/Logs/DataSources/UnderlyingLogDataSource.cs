using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources
{
	/// <summary>
	/// Low-level source of log data.
	/// </summary>
	enum UnderlyingLogDataSource
	{
		/// <summary>
		/// Undefined.
		/// </summary>
		Undefined,
		/// <summary>
		/// Database.
		/// </summary>
		Database,
		/// <summary>
		/// File.
		/// </summary>
		File,
		/// <summary>
		/// Standard output of process.
		/// </summary>
		StandardOutput,
		/// <summary>
		/// Web request.
		/// </summary>
		WebRequest,
		/// <summary>
		/// Windows event logs.
		/// </summary>
		WindowsEventLogs,
	}
}
