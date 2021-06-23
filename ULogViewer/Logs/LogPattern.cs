using System;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Pattern to parse log data.
	/// </summary>
	public class LogPattern
	{
		/// <summary>
		/// Initialize new <see cref="LogPattern"/> instance.
		/// </summary>
		/// <param name="regex">Regular expression for parsing log data.</param>
		/// <param name="isRepeatable">Whether the pattern is repeatable or not when parsing log data.</param>
		public LogPattern(string regex, bool isRepeatable)
		{
			this.IsRepeatable = isRepeatable;
			this.Regex = new Regex(regex);
		}


		/// <summary>
		/// Get whether the pattern is repeatable or not when parsing log data.
		/// </summary>
		public bool IsRepeatable { get; }


		/// <summary>
		/// Get regular expression for parsing log data.
		/// </summary>
		public Regex Regex { get; }


		// Get readable string.
		public override string ToString()
		{
			if (!this.IsRepeatable)
				return this.Regex.ToString();
			return $"{this.Regex} (Repeatable)";
		}
	}
}
