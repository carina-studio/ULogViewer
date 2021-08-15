using Avalonia.Media;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Log which is suitable for displaying.
	/// </summary>
	class DisplayableLog : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Static fields.
		static readonly Func<Log, string?>[] extraGetters = new Func<Log, string?>[Log.ExtraCapacity].Also(it =>
		{
			for (var i = it.Length - 1; i >= 0; --i)
				it[i] = Log.CreatePropertyGetter<string?>($"Extra{i + 1}");
		});


		// Fields.
		string? beginningTimestampString;
		string? endingTimestampString;
		int[] extraLineCount = new int[10].Also(it =>
		{
			for (var i = it.Length - 1; i >= 0; --i)
				it[i] = -1;
		});
		IBrush? colorIndicatorBrush;
		bool?[] hasExtraLinesOfExtras = new bool?[10];
		bool? hasExtraLinesOfMessage;
		bool? hasExtraLinesOfSummary;
		bool isColorIndicatorBrushSet;
		bool isLevelBrushSet;
		bool isMarked;
		IBrush? levelBrush;
		int messageLineCount = -1;
		int summaryLineCount = -1;
		string? timestampString;


		/// <summary>
		/// Initialize new <see cref="DisplayableLog"/> instance.
		/// </summary>
		/// <param name="group">Group of <see cref="DisplayableLog"/>.</param>
		/// <param name="reader">Log reader which reads the log.</param>
		/// <param name="log">Log.</param>
		internal DisplayableLog(DisplayableLogGroup group, LogReader reader, Log log)
		{
			this.Application = group.Application;
			this.BinaryBeginningTimestamp = log.BeginningTimestamp?.ToBinary() ?? 0L;
			this.BinaryEndingTimestamp = log.EndingTimestamp?.ToBinary() ?? 0L;
			this.BinaryTimestamp = log.Timestamp?.ToBinary() ?? 0L;
			this.Group = group;
			this.Log = log;
			this.LogReader = reader;
			this.TrackingNode = new LinkedListNode<DisplayableLog>(this);
			group.OnDisplayableLogCreated(this);
		}


		/// <summary>
		/// Get <see cref="IApplication"/> instance.
		/// </summary>
		public IApplication Application { get; }


		/// <summary>
		/// Get beginning timestamp of log in string format.
		/// </summary>
		public string BeginningTimestampString
		{
			get
			{
				if (this.beginningTimestampString == null)
					this.beginningTimestampString = this.FormatTimestamp(this.Log.BeginningTimestamp);
				return this.beginningTimestampString;
			}
		}


		/// <summary>
		/// Get beginning timestamp of log in binary format.
		/// </summary>
		public long BinaryBeginningTimestamp { get; }


		/// <summary>
		/// Get ending timestamp of log in binary format.
		/// </summary>
		public long BinaryEndingTimestamp { get; }


		/// <summary>
		/// Get timestamp of log in binary format.
		/// </summary>
		public long BinaryTimestamp { get; }


		// Calculate line count.
		static int CalculateLineCount(string? text)
		{
			if (text == null)
				return 0;
			var lineCount = 1;
			for (var i = text.Length - 1; i >= 0; --i)
			{
				if (text[i] == '\n')
					++lineCount;
			}
			return lineCount;
		}


		/// <summary>
		/// Get category of log.
		/// </summary>
		public string? Category { get => this.Log.Category; }


		// Check whether extra line of ExtraX exist or not.
		bool CheckExtraLinesOfExtra(int index)
		{
			var hasExtraLines = this.hasExtraLinesOfExtras[index];
			if (hasExtraLines == null)
			{
				hasExtraLines = this.GetExtraLineCount(index) > this.Group.MaxDisplayLineCount;
				this.hasExtraLinesOfExtras[index] = hasExtraLines;
			}
			return hasExtraLines.Value;
		}


		/// <summary>
		/// Get <see cref="IBrush"/> of color indicator.
		/// </summary>
		public IBrush? ColorIndicatorBrush
		{
			get
			{
				if (!this.isColorIndicatorBrushSet)
				{
					this.colorIndicatorBrush = this.Group.GetColorIndicatorBrush(this);
					this.isColorIndicatorBrushSet = true;
				}
				return this.colorIndicatorBrush;
			}
		}


		/// <summary>
		/// Create <see cref="Func{T, TResult}"/> to get specific log property from <see cref="DisplayableLog"/>.
		/// </summary>
		/// <typeparam name="T">Type of property value.</typeparam>
		/// <param name="propertyName">Name of property.</param>
		/// <returns><see cref="Func{T, TResult}"/>.</returns>
		public static Func<DisplayableLog, T> CreateLogPropertyGetter<T>(string propertyName)
		{
			if (propertyName != nameof(LogId))
			{
				return Log.CreatePropertyGetter<T>(propertyName).Let(getter =>
				{
					return new Func<DisplayableLog, T>(it => getter(it.Log));
				});
			}
			return (it => (T)(object)it.LogId);
		}


		/// <summary>
		/// Get ID of device which generates log.
		/// </summary>
		public string? DeviceId { get => this.Log.DeviceId; }


		/// <summary>
		/// Get name of device which generates log.
		/// </summary>
		public string? DeviceName { get => this.Log.DeviceName; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// check thread
			this.VerifyAccess();

			// notify
			this.Group.OnDisplayableLogDisposed(this);

			// release resources
			this.colorIndicatorBrush = null;
			this.levelBrush = null;
		}


		/// <summary>
		/// Get ending timestamp of log in string format.
		/// </summary>
		public string EndingTimestampString
		{
			get
			{
				if (this.endingTimestampString == null)
					this.endingTimestampString = this.FormatTimestamp(this.Log.EndingTimestamp);
				return this.endingTimestampString;
			}
		}


		/// <summary>
		/// Get event of log.
		/// </summary>
		public string? Event { get => this.Log.Event; }


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public string? Extra1 { get => this.Log.Extra1; }


		/// <summary>
		/// Get line count of <see cref="Extra1"/>.
		/// </summary>
		public int Extra1LineCount { get => this.GetExtraLineCount(0); }


		/// <summary>
		/// Get 10th extra data of log.
		/// </summary>
		public string? Extra10 { get => this.Log.Extra10; }


		/// <summary>
		/// Get line count of <see cref="Extra10"/>.
		/// </summary>
		public int Extra10LineCount { get => this.GetExtraLineCount(9); }


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public string? Extra2 { get => this.Log.Extra2; }


		/// <summary>
		/// Get line count of <see cref="Extra2"/>.
		/// </summary>
		public int Extra2LineCount { get => this.GetExtraLineCount(1); }


		/// <summary>
		/// Get 3rd extra data of log.
		/// </summary>
		public string? Extra3 { get => this.Log.Extra3; }


		/// <summary>
		/// Get line count of <see cref="Extra3"/>.
		/// </summary>
		public int Extra3LineCount { get => this.GetExtraLineCount(2); }


		/// <summary>
		/// Get 4th extra data of log.
		/// </summary>
		public string? Extra4 { get => this.Log.Extra4; }


		/// <summary>
		/// Get line count of <see cref="Extra4"/>.
		/// </summary>
		public int Extra4LineCount { get => this.GetExtraLineCount(3); }


		/// <summary>
		/// Get 5th extra data of log.
		/// </summary>
		public string? Extra5 { get => this.Log.Extra5; }


		/// <summary>
		/// Get line count of <see cref="Extra5"/>.
		/// </summary>
		public int Extra5LineCount { get => this.GetExtraLineCount(4); }


		/// <summary>
		/// Get 6th extra data of log.
		/// </summary>
		public string? Extra6 { get => this.Log.Extra6; }


		/// <summary>
		/// Get line count of <see cref="Extra6"/>.
		/// </summary>
		public int Extra6LineCount { get => this.GetExtraLineCount(5); }


		/// <summary>
		/// Get 7th extra data of log.
		/// </summary>
		public string? Extra7 { get => this.Log.Extra7; }


		/// <summary>
		/// Get line count of <see cref="Extra7"/>.
		/// </summary>
		public int Extra7LineCount { get => this.GetExtraLineCount(6); }


		/// <summary>
		/// Get 8th extra data of log.
		/// </summary>
		public string? Extra8 { get => this.Log.Extra8; }


		/// <summary>
		/// Get line count of <see cref="Extra8"/>.
		/// </summary>
		public int Extra8LineCount { get => this.GetExtraLineCount(7); }


		/// <summary>
		/// Get 9th extra data of log.
		/// </summary>
		public string? Extra9 { get => this.Log.Extra9; }


		/// <summary>
		/// Get line count of <see cref="Extra9"/>.
		/// </summary>
		public int Extra9LineCount { get => this.GetExtraLineCount(8); }


		/// <summary>
		/// Get name of file which read log from.
		/// </summary>
		public string? FileName { get => this.Log.FileName; }


		// Format timestamp to string.
		string FormatTimestamp(DateTime? timestamp)
		{
			var format = this.Group.LogProfile.TimestampFormatForDisplaying;
			if (timestamp == null)
				return "";
			if (format != null)
				return timestamp.Value.ToString(format);
			return timestamp.Value.ToString();
		}


		// Get number of lines of ExtraX.
		int GetExtraLineCount(int index)
		{
			if (this.extraLineCount[index] < 0)
				this.extraLineCount[index] = CalculateLineCount(extraGetters[index](this.Log));
			return this.extraLineCount[index];
		}


		/// <summary>
		/// Get <see cref="DisplayableLogGroup"/> which the instance belongs to.
		/// </summary>
		public DisplayableLogGroup Group { get; }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra1"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra1 { get => this.CheckExtraLinesOfExtra(0); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra10"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra10 { get => this.CheckExtraLinesOfExtra(9); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra2"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra2 { get => this.CheckExtraLinesOfExtra(1); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra3"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra3 { get => this.CheckExtraLinesOfExtra(2); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra4"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra4 { get => this.CheckExtraLinesOfExtra(3); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra5"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra5 { get => this.CheckExtraLinesOfExtra(4); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra6"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra6 { get => this.CheckExtraLinesOfExtra(5); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra7"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra7 { get => this.CheckExtraLinesOfExtra(6); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra8"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra8 { get => this.CheckExtraLinesOfExtra(7); }


		/// <summary>
		/// Check whether number of lines in <see cref="Extra9"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra9 { get => this.CheckExtraLinesOfExtra(8); }


		/// <summary>
		/// Check whether number of lines in <see cref="Message"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfMessage
		{
			get
			{
				var hasExtraLines = this.hasExtraLinesOfMessage;
				if (hasExtraLines == null)
				{
					hasExtraLines = this.MessageLineCount > this.Group.MaxDisplayLineCount;
					this.hasExtraLinesOfMessage = hasExtraLines;
				}
				return hasExtraLines.Value;
			}
		}


		/// <summary>
		/// Check whether number of lines in <see cref="Summary"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfSummary
		{
			get
			{
				var hasExtraLines = this.hasExtraLinesOfSummary;
				if (hasExtraLines == null)
				{
					hasExtraLines = this.SummaryLineCount > this.Group.MaxDisplayLineCount;
					this.hasExtraLinesOfSummary = hasExtraLines;
				}
				return hasExtraLines.Value;
			}
		}


		/// <summary>
		/// Check whether given property of log is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasLogProperty(string propertyName)
		{
			if (propertyName != nameof(LogId))
				return Log.HasProperty(propertyName);
			return true;
		}


		/// <summary>
		/// Check whether given property of log with string value is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasStringLogProperty(string propertyName) => Log.HasStringProperty(propertyName);


		/// <summary>
		/// Get or set whether log has been marked or not.
		/// </summary>
		public bool IsMarked
		{
			get => this.isMarked;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.isMarked == value)
					return;
				this.isMarked = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarked)));
			}
		}


		/// <summary>
		/// Get level of log.
		/// </summary>
		public LogLevel Level { get => this.Log.Level; }


		/// <summary>
		/// Get <see cref="IBrush"/> according to level of log.
		/// </summary>
		public IBrush LevelBrush
		{
			get
			{
				if (!this.isLevelBrushSet)
				{
					this.levelBrush = this.Group.GetLevelBrush(this);
					this.isLevelBrushSet = true;
				}
				return this.levelBrush.AsNonNull();
			}
		}


		/// <summary>
		/// Get line number.
		/// </summary>
		public int? LineNumber { get => this.Log.LineNumber; }


		/// <summary>
		/// Get wrapped <see cref="Log"/>.
		/// </summary>
		public Log Log { get; }


		/// <summary>
		/// Get unique ID of log.
		/// </summary>
		public long LogId { get => this.Log.Id; }


		/// <summary>
		/// Get log reader which reads the log.
		/// </summary>
		public LogReader LogReader { get; }


		/// <summary>
		/// Get message of log.
		/// </summary>
		public string? Message { get => this.Log.Message; }


		/// <summary>
		/// Get line count of <see cref="Message"/>.
		/// </summary>
		public int MessageLineCount
		{
			get
			{
				if (this.messageLineCount < 0)
					this.messageLineCount = CalculateLineCount(this.Log.Message);
				return this.messageLineCount;
			}
		}


		/// <summary>
		/// Called when application string resources updated.
		/// </summary>
		internal void OnApplicationStringsUpdated()
		{ }


		/// <summary>
		/// Called when maximum display line count changed.
		/// </summary>
		internal void OnMaxDisplayLineCountChanged()
		{
			// check attached property changed handlers
			var propertyChangedHandlers = this.PropertyChanged;

			// check extra line count
			var hasExtraLines = (bool?)null;
			for (var i = Log.ExtraCapacity - 1; i >= 0; --i)
			{
				hasExtraLines = this.hasExtraLinesOfExtras[i];
				if (hasExtraLines.HasValue)
				{
					this.hasExtraLinesOfExtras[i] = null;
					if (propertyChangedHandlers != null && this.CheckExtraLinesOfExtra(i) != hasExtraLines)
						propertyChangedHandlers(this, new PropertyChangedEventArgs($"HasExtraLinesOfExtra{i + 1}"));
				}
			}

			// check message line count
			hasExtraLines = this.hasExtraLinesOfMessage;
			if (hasExtraLines.HasValue)
			{
				this.hasExtraLinesOfMessage = null;
				if (propertyChangedHandlers != null && this.HasExtraLinesOfMessage != hasExtraLines)
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(HasExtraLinesOfMessage)));
			}

			// check summary line count
			hasExtraLines = this.hasExtraLinesOfSummary;
			if (hasExtraLines.HasValue)
			{
				this.hasExtraLinesOfSummary = null;
				if (propertyChangedHandlers != null && this.HasExtraLinesOfSummary != hasExtraLines)
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(HasExtraLinesOfSummary)));
			}
		}


		/// <summary>
		/// Called when style related resources has been updated.
		/// </summary>
		internal void OnStyleResourcesUpdated()
		{
			if (this.isColorIndicatorBrushSet)
			{
				this.isColorIndicatorBrushSet = false;
				this.colorIndicatorBrush = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorIndicatorBrush)));
			}
			if (this.isLevelBrushSet)
			{
				this.isLevelBrushSet = false;
				this.levelBrush = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelBrush)));
			}
		}


		/// <summary>
		/// Called when format of displaying timestamp has been changed.
		/// </summary>
		internal void OnTimestampFormatChanged()
		{
			if (this.Log.BeginningTimestamp.HasValue)
			{
				this.beginningTimestampString = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BeginningTimestampString)));
			}
			if (this.Log.EndingTimestamp.HasValue)
			{
				this.endingTimestampString = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndingTimestampString)));
			}
			if (this.Log.Timestamp.HasValue)
			{
				this.timestampString = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimestampString)));
			}
		}


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId { get => this.Log.ProcessId; }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public string? ProcessName { get => this.Log.ProcessName; }


		/// <summary>
		/// Get name of source which generates log.
		/// </summary>
		public string? SourceName { get => this.Log.SourceName; }


		/// <summary>
		/// Get summary of log.
		/// </summary>
		public string? Summary { get => this.Log.Summary; }


		/// <summary>
		/// Get line count of <see cref="Summary"/>.
		/// </summary>
		public int SummaryLineCount
		{
			get
			{
				if (this.summaryLineCount < 0)
					this.summaryLineCount = CalculateLineCount(this.Log.Summary);
				return this.summaryLineCount;
			}
		}


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public string? Tags { get => this.Log.Tags; }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId { get => this.Log.ThreadId; }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public string? ThreadName { get => this.Log.ThreadName; }


		/// <summary>
		/// Get timestamp of log in string format.
		/// </summary>
		public string TimestampString
		{
			get
			{
				if (this.timestampString == null)
					this.timestampString = this.FormatTimestamp(this.Log.Timestamp);
				return this.timestampString;
			}
		}


		/// <summary>
		/// Get title of log.
		/// </summary>
		public string? Title { get => this.Log.Title; }


		/// <summary>
		/// Node for tracking instance.
		/// </summary>
		public LinkedListNode<DisplayableLog> TrackingNode { get; }


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public string? UserId { get => this.Log.UserId; }


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public string? UserName { get => this.Log.UserName; }


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
