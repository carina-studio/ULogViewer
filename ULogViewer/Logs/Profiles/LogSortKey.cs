﻿using System;

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
		/// Sort by instance ID.
		/// </summary>
		Id,
	}
}
