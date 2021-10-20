﻿using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Interface of ULogViewer application.
	/// </summary>
	interface IULogViewerApplication : AppSuite.IAppSuiteApplication
	{
		/// <summary>
		/// Is application running as administrator/superuser or not.
		/// </summary>
		bool IsRunningAsAdministrator { get; }


		/// <summary>
		/// Check whether application is running for testing purpose or not.
		/// </summary>
		bool IsTesting { get; }


		/// <summary>
		/// Try restarting application.
		/// </summary>
		/// <param name="args">Arguments to restart application.</param>
		/// <param name="asAdministrator">True to restart as administrator/superuser.</param>
		/// <returns>True if restarting has been scheduled.</returns>
		bool Restart(string? args = null, bool asAdministrator = false);
	}
}