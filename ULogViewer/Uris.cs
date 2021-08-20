using System;

namespace CarinaStudio.ULogViewer
{
	/// <summary>
	/// Predefined <see cref="Uri"/>.
	/// </summary>
	static class Uris
	{
		/// <summary>
		/// Application package info.
		/// </summary>
		public static readonly Uri AppPackageInfo = new Uri("https://raw.githubusercontent.com/carina-studio/ULogViewer/master/PackageInfo.json");
		/// <summary>
		/// Reference of date time format.
		/// </summary>
		public static readonly Uri DateTimeFormatReference = new Uri("https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings");
		/// <summary>
		/// Reference of regular expression.
		/// </summary>
		public static readonly Uri RegexReference = new Uri("https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference");
		/// <summary>
		/// Reference of string interpolation.
		/// </summary>
		public static readonly Uri StringInterpolationReference = new Uri("https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated#structure-of-an-interpolated-string");
	}
}
