using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Application update info.
	/// </summary>
	class AppUpdateInfo
	{
		/// <summary>
		/// Initialize <see cref="AppUpdateInfo"/> instance.
		/// </summary>
		/// <param name="version">Version of updated application.</param>
		/// <param name="releaseDate">Date of release of update.</param>
		/// <param name="releasePageUri">Uri of page of release.</param>
		/// <param name="packageUri">URI of update package.</param>
		public AppUpdateInfo(Version version, DateTime releaseDate, Uri releasePageUri, Uri? packageUri)
		{
			this.PackageUri = packageUri;
			this.ReleaseDate = releaseDate;
			this.ReleasePageUri = releasePageUri;
			this.Version = version;
		}


		// Check equality.
		public override bool Equals(object? obj)
		{
			if (!(obj is AppUpdateInfo updateInfo))
				return false;
			return this.Version == updateInfo.Version
				&& this.ReleaseDate == updateInfo.ReleaseDate
				&& this.ReleasePageUri == updateInfo.ReleasePageUri
				&& this.PackageUri == updateInfo.PackageUri;
		}


		// Get hash-code.
		public override int GetHashCode() => this.Version.GetHashCode();


		/// <summary>
		/// Equality operator.
		/// </summary>
		public static bool operator ==(AppUpdateInfo? x, AppUpdateInfo? y) => x?.Equals(y) ?? object.ReferenceEquals(y, null);


		/// <summary>
		/// Inequality operator.
		/// </summary>
		public static bool operator !=(AppUpdateInfo? x, AppUpdateInfo? y) => !(x?.Equals(y) ?? object.ReferenceEquals(y, null));


		/// <summary>
		/// URI of update package.
		/// </summary>
		public Uri? PackageUri { get; }


		/// <summary>
		/// Date of release of update.
		/// </summary>
		public DateTime ReleaseDate { get; }


		/// <summary>
		/// Uri of page of release.
		/// </summary>
		public Uri ReleasePageUri { get; }


		/// <summary>
		/// Version of updated application.
		/// </summary>
		public Version Version { get; }
	}
}
