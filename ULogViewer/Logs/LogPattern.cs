using System.Text;
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
		/// <param name="isSkippable">Whether the pattern can be skipped or not when parsing log data.</param>
		public LogPattern(string regex, bool isRepeatable, bool isSkippable)
		{
			this.IsRepeatable = isRepeatable;
			this.IsSkippable = isSkippable;
			this.Regex = new Regex(regex);
		}


		/// <summary>
		/// Initialize new <see cref="LogPattern"/> instance.
		/// </summary>
		/// <param name="regex">Regular expression for parsing log data.</param>
		/// <param name="isRepeatable">Whether the pattern is repeatable or not when parsing log data.</param>
		/// <param name="isSkippable">Whether the pattern can be skipped or not when parsing log data.</param>
		public LogPattern(Regex regex, bool isRepeatable, bool isSkippable)
		{
			this.IsRepeatable = isRepeatable;
			this.IsSkippable = isSkippable;
			this.Regex = regex;
		}


		/// <summary>
		/// Get whether the pattern is repeatable or not when parsing log data.
		/// </summary>
		public bool IsRepeatable { get; }


		/// <summary>
		/// Get whether the pattern can be skipped or not when parsing log data.
		/// </summary>
		public bool IsSkippable { get; }


		/// <summary>
		/// Get regular expression for parsing log data.
		/// </summary>
		public Regex Regex { get; }


		// Get readable string.
		public override string ToString()
		{
			var stringBuilder = new StringBuilder(this.Regex.ToString());
			if (this.IsRepeatable)
				stringBuilder.Append(" (R)");
			if (this.IsSkippable)
				stringBuilder.Append(" (S)");
			return stringBuilder.ToString();
		}
	}
}
