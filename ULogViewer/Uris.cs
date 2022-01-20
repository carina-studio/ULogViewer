﻿using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Predefined <see cref="Uri"/>.
	/// </summary>
	static class Uris
	{
		/// <summary>
		/// Application package manifest.
		/// </summary>
		public static readonly Uri AppPackageManifest = new Uri("https://raw.githubusercontent.com/carina-studio/ULogViewer/master/PackageManifest.json");
		/// <summary>
		/// Auto updater package manifest.
		/// </summary>
		public static readonly Uri AutoUpdaterPackageManifest = new Uri("https://raw.githubusercontent.com/carina-studio/AutoUpdater/master/PackageManifest-Avalonia.json");
		/// <summary>
		/// Reference of date time format.
		/// </summary>
		public static readonly Uri DateTimeFormatReference = new Uri("https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings");
		/// <summary>
		/// Application package manifest (Preview).
		/// </summary>
		public static readonly Uri PreviewAppPackageManifest = new Uri("https://raw.githubusercontent.com/carina-studio/ULogViewer/master/PackageManifest-Preview.json");
		/// <summary>
		/// Reference of regular expression.
		/// </summary>
		public static readonly Uri RegexReference = new Uri("https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference");
		/// <summary>
		/// Reference of string interpolation.
		/// </summary>
		public static readonly Uri StringInterpolationReference = new Uri("https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated#structure-of-an-interpolated-string");
		/// <summary>
		/// Reference of time span format.
		/// </summary>
		public static readonly Uri TimeSpanFormatReference = new Uri("https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-timespan-format-strings");
	}
}
