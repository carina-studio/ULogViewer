using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// <see cref="IComparer{T}"/>s for <see cref="Log"/>.
	/// </summary>
	static class LogComparers
	{
		/// <summary>
		/// <see cref="IComparer{T}"/> which compares <see cref="Log"/> by <see cref="Log.Timestamp"/> (ascending).
		/// </summary>
		public static readonly IComparer<Log> TimestampAsc = Comparer<Log>.Create(CompareByTimestamp);
		/// <summary>
		/// <see cref="IComparer{T}"/> which compares <see cref="Log"/> by <see cref="Log.Timestamp"/> (descending).
		/// </summary>
		public static readonly IComparer<Log> TimestampDesc = new InverseComparer(CompareByTimestamp);


		// Comparer which inverts the comparison result.
		class InverseComparer : IComparer<Log>
		{
			// Fields.
			readonly Comparison<Log?> comparison;

			// Constructor.
			public InverseComparer(Comparison<Log?> comparison)
			{
				this.comparison = comparison;
			}

			// Compare.
			public int Compare(Log? x, Log? y) => -this.comparison(x, y);
		}


		// Compare by instance ID.
		static int CompareById(Log? x, Log? y)
		{
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;
			var result = (x.Id - y.Id);
			if (result > 0)
				return 1;
			if (result < 0)
				return -1;
			return 0;
		}


		// Compare by timestamp.
		static int CompareByTimestamp(Log? x, Log? y)
		{
			var timestampX = x?.Timestamp;
			var timestampY = y?.Timestamp;
			if (timestampX == null)
			{
				if (timestampY == null)
					return CompareById(x, y);
				return -1;
			}
			if (timestampY == null)
				return 1;
			var result = timestampX.Value.CompareTo(timestampY.Value);
			if (result != 0)
				return result;
			return CompareById(x, y);
		}
	}
}
