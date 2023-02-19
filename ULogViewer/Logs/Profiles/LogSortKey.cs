using System;

namespace CarinaStudio.ULogViewer.Logs.Profiles
{
	/// <summary>
	/// Key of log sorting.
	/// </summary>
	enum LogSortKey
	{
		/// <summary>
		/// Sort by beginning timestamp.
		/// </summary>
		BeginningTimestamp,
		/// <summary>
		/// Sort by ending timestamp.
		/// </summary>
		EndingTimestamp,
		/// <summary>
		/// Sort by timestamp.
		/// </summary>
		Timestamp,
		/// <summary>
		/// Sort by beginning time span.
		/// </summary>
		BeginningTimeSpan,
		/// <summary>
		/// Sort by ending time span.
		/// </summary>
		EndingTimeSpan,
		/// <summary>
		/// Sort by time span.
		/// </summary>
		TimeSpan,
		/// <summary>
		/// Sort by instance ID.
		/// </summary>
		Id,
		/// <summary>
		/// Sort by timestamp of reading log.
		/// </summary>
		ReadTime,
	}
}
