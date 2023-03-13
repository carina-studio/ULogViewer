using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Data.Converters;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Log which is suitable for displaying.
	/// </summary>
	class DisplayableLog : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Static fields.
		static readonly DisplayableLogAnalysisResult[] emptyAnalysisResults = Array.Empty<DisplayableLogAnalysisResult>();
		static readonly byte[] emptyByteArray = Array.Empty<byte>();
		static readonly Func<Log, string?>[] extraGetters = new Func<Log, string?>[Log.ExtraCapacity].Also(it =>
		{
			for (var i = it.Length - 1; i >= 0; --i)
				it[i] = Log.CreatePropertyGetter<string?>($"Extra{i + 1}");
		});
		static readonly long instanceFieldMemorySize = Memory.EstimateInstanceSize<DisplayableLog>();
		static volatile bool isPropertyMapReady;
		static AppSuite.Converters.EnumConverter? levelConverter;
		[ThreadStatic]
		static Dictionary<string, DisplayableLogStringPropertyGetter>? logStringPropertyGetters;
		static readonly Dictionary<string, PropertyInfo> propertyMap = new();


		// Fields.
		DisplayableLogAnalysisResultType activeAnalysisResultType;
		IList<DisplayableLogAnalysisResult> analysisResults = emptyAnalysisResults;
		CompressedString? beginningTimeSpanString;
		CompressedString? beginningTimestampString;
		CompressedString? endingTimeSpanString;
		CompressedString? endingTimestampString;
		readonly byte[] extraLineCount;
		MarkColor markedColor;
		short memorySize;
		byte messageLineCount;
		CompressedString? readTimeString;
		byte summaryLineCount;
		CompressedString? timeSpanString;
		CompressedString? timestampString;


		/// <summary>
		/// Initialize new <see cref="DisplayableLog"/> instance.
		/// </summary>
		/// <param name="group">Group of <see cref="DisplayableLog"/>.</param>
		/// <param name="reader">Log reader which reads the log.</param>
		/// <param name="log">Log.</param>
		internal DisplayableLog(DisplayableLogGroup group, LogReader reader, Log log)
		{
			// setup properties
			this.BinaryBeginningTimeSpan = (long)(log.BeginningTimeSpan?.TotalMilliseconds ?? 0);
			this.BinaryBeginningTimestamp = log.BeginningTimestamp?.ToBinary() ?? 0L;
			this.BinaryEndingTimeSpan = (long)(log.EndingTimeSpan?.TotalMilliseconds ?? 0);
			this.BinaryEndingTimestamp = log.EndingTimestamp?.ToBinary() ?? 0L;
			this.BinaryReadTime = log.ReadTime.ToBinary();
			this.BinaryTimeSpan = (long)(log.TimeSpan?.TotalMilliseconds ?? 0);
			this.BinaryTimestamp = log.Timestamp?.ToBinary() ?? 0L;
			this.Group = group;
			this.Log = log;
			this.LogReader = reader;

			// check extras
			var extraCount = group.MaxLogExtraNumber;
			if (extraCount > 0)
				this.extraLineCount = new byte[extraCount];
			else
				this.extraLineCount = emptyByteArray;

			// estimate memory usage
			long memorySize = log.MemorySize + instanceFieldMemorySize;
			if (extraCount > 0)
				memorySize += Memory.EstimateArrayInstanceSize(sizeof(byte), extraCount);
			this.memorySize = (short)memorySize;

			// notify group
			group.OnDisplayableLogCreated(this);
		}


		/// <summary>
		/// Add <see cref="DisplayableLogAnalysisResult"/> to this log.
		/// </summary>
		/// <param name="result">Result to add.</param>
		public void AddAnalysisResult(DisplayableLogAnalysisResult result)
		{
			// check state
#if DEBUG
			this.VerifyAccess();
#endif
			if (this.IsDisposed)
				return;
			
			// add to list
			var memorySizeDiff = 0L;
			var currentResultCount = this.analysisResults.Count;
			if (currentResultCount == 0)
			{
				this.analysisResults = ListExtensions.AsReadOnly(new[] { result });
				memorySizeDiff = Memory.EstimateArrayInstanceSize(IntPtr.Size, 1) + Memory.EstimateCollectionInstanceSize(IntPtr.Size, 0);
			}
			else
			{
				var newList = new DisplayableLogAnalysisResult[currentResultCount + 1];
				this.analysisResults.CopyTo(newList, 0);
				newList[currentResultCount] = result;
				this.analysisResults = ListExtensions.AsReadOnly(newList);
				memorySizeDiff += IntPtr.Size;
			}

			// select active type
			if (this.activeAnalysisResultType == 0 || this.activeAnalysisResultType > result.Type)
			{
				this.activeAnalysisResultType = result.Type;
				this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
			}
			if (currentResultCount == 0)
				this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			
			// update memory usage
			this.memorySize += (short)memorySizeDiff;
			this.Group.OnAnalysisResultAdded(this);
			this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
		}


		/// <summary>
		/// Get icon for analysis result indicator.
		/// </summary>
		public IImage? AnalysisResultIndicatorIcon  { get => this.Group.GetAnalysisResultIndicatorIcon(this.activeAnalysisResultType); }


		/// <summary>
		/// Get all <see cref="DisplayableLogAnalysisResult"/>s which were added to this log.
		/// </summary>
		public IList<DisplayableLogAnalysisResult> AnalysisResults { get => this.analysisResults; }


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get => this.Group.Application; }


		/// <summary>
		/// Get beginning time span of log.
		/// </summary>
		public TimeSpan? BeginningTimeSpan { get => this.Log.BeginningTimeSpan; }


		/// <summary>
		/// Get beginning time span of log in string format.
		/// </summary>
		public string BeginningTimeSpanString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimeSpan(this.Log.BeginningTimeSpan);
				if (this.beginningTimeSpanString == null)
				{
					this.beginningTimeSpanString = this.FormatTimeSpanCompressed(this.Log.BeginningTimeSpan);
					if (this.beginningTimeSpanString != CompressedString.Empty)
					{
						var memorySizeDiff = this.beginningTimeSpanString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.beginningTimeSpanString.ToString();
			}
		}


		/// <summary>
		/// Get beginning timestamp of log.
		/// </summary>
		public DateTime? BeginningTimestamp { get => this.Log.BeginningTimestamp; }


		/// <summary>
		/// Get beginning timestamp of log in string format.
		/// </summary>
		public string BeginningTimestampString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimestamp(this.Log.BeginningTimestamp);
				if (this.beginningTimestampString == null)
				{
					this.beginningTimestampString = this.FormatTimestampCompressed(this.Log.BeginningTimestamp);
					if (this.beginningTimestampString != CompressedString.Empty)
					{
						var memorySizeDiff = this.beginningTimestampString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.beginningTimestampString.ToString();
			}
		}


		/// <summary>
		/// Get beginning time span of log in binary format.
		/// </summary>
		public long BinaryBeginningTimeSpan { get; }


		/// <summary>
		/// Get beginning timestamp of log in binary format.
		/// </summary>
		public long BinaryBeginningTimestamp { get; }


		/// <summary>
		/// Get ending time span of log in binary format.
		/// </summary>
		public long BinaryEndingTimeSpan { get; }


		/// <summary>
		/// Get ending timestamp of log in binary format.
		/// </summary>
		public long BinaryEndingTimestamp { get; }


		/// <summary>
		/// Get the timestamp of this log was read in binary format..
		/// </summary>
		public long BinaryReadTime { get; }


		/// <summary>
		/// Get time span of log in binary format.
		/// </summary>
		public long BinaryTimeSpan { get; }


		/// <summary>
		/// Get timestamp of log in binary format.
		/// </summary>
		public long BinaryTimestamp { get; }


		// Calculate line count.
		static unsafe byte CalculateLineCount(string? text)
		{
			if (text == null)
				return 0;
			var textLength = text.Length;
			if (textLength == 0)
				return 1;
			var lineCount = (byte)1;
			fixed (char* p = text.AsSpan())
			{
				char* charPtr = (p + textLength - 1);
				while (charPtr >= p)
				{
					if (*charPtr == '\n')
					{
						++lineCount;
						if (lineCount == byte.MaxValue)
							break;
					}
					--charPtr;
				}
			}
			return lineCount;
		}


		/// <summary>
		/// Get category of log.
		/// </summary>
		public string? Category { get => this.Log.Category; }


		// Check whether extra line of ExtraX exist or not.
		bool CheckExtraLinesOfExtra(int index) => this.GetExtraLineCount(index) > this.Group.MaxDisplayLineCount;


		/// <summary>
		/// Get <see cref="IBrush"/> of color indicator.
		/// </summary>
		public IBrush? ColorIndicatorBrush { get => this.Group.GetColorIndicatorBrush(this); }


		/// <summary>
		/// Get tip text for color indicator.
		/// </summary>
		public string? ColorIndicatorTip { get => this.Group.GetColorIndicatorTip(this); }


		/// <summary>
		/// Create <see cref="Func{T, TResult}"/> to get specific log property from <see cref="DisplayableLog"/>.
		/// </summary>
		/// <typeparam name="T">Type of property value.</typeparam>
		/// <param name="propertyName">Name of property.</param>
		/// <returns><see cref="Func{T, TResult}"/>.</returns>
		public static Func<DisplayableLog, T> CreateLogPropertyGetter<T>(string propertyName)
		{
			return propertyName switch
			{
				nameof(BeginningTimeSpanString) => (it => (T)(object)it.BeginningTimeSpanString),
				nameof(BeginningTimestampString) => (it => (T)(object)it.BeginningTimestampString),
				nameof(BinaryBeginningTimeSpan) => (it => (T)(object)it.BinaryBeginningTimeSpan),
				nameof(BinaryBeginningTimestamp) => (it => (T)(object)it.BinaryBeginningTimestamp),
				nameof(BinaryEndingTimeSpan) => (it => (T)(object)it.BinaryEndingTimeSpan),
				nameof(BinaryEndingTimestamp) => (it => (T)(object)it.BinaryEndingTimestamp),
				nameof(BinaryReadTime) => (it => (T)(object)it.BinaryReadTime),
				nameof(BinaryTimeSpan) => (it => (T)(object)it.BinaryTimeSpan),
				nameof(BinaryTimestamp) => (it => (T)(object)it.BinaryTimestamp),
				nameof(EndingTimeSpanString) => (it => (T)(object)it.EndingTimeSpanString),
				nameof(EndingTimestampString) => (it => (T)(object)it.EndingTimestampString),
				nameof(LevelString) => (it => (T)(object)it.LevelString),
				nameof(LogId) => (it => (T)(object)it.LogId),
				nameof(ReadTimeString) => (it => (T)(object)it.ReadTimeString),
				nameof(TimeSpanString) => (it => (T)(object)it.TimeSpanString),
				nameof(TimestampString) => (it => (T)(object)it.TimestampString),
				_ => Log.CreatePropertyGetter<T>(propertyName).Let(getter =>
				{
					return new Func<DisplayableLog, T>(it => getter(it.Log));
				}),
			};
		}


		/// <summary>
		/// Create delegate for getting specific string log property.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>Delegate for getting specific string log property.</returns>
		public static DisplayableLogStringPropertyGetter CreateLogStringPropertyGetter(string propertyName)
		{
			// use cached getter
			DisplayableLogStringPropertyGetter? getter;
			if (logStringPropertyGetters == null)
				logStringPropertyGetters = new();
			else if (logStringPropertyGetters.TryGetValue(propertyName, out getter))
				return getter;
			
			// create getter
			Func<DisplayableLog, CompressedString?> backedValueGetter;
			switch (propertyName)
			{
				case nameof(BeginningTimeSpanString):
					backedValueGetter = log => log.beginningTimeSpanString;
					break;
				case nameof(BeginningTimestampString):
					backedValueGetter = log => log.beginningTimestampString;
					break;
				case nameof(EndingTimeSpanString):
					backedValueGetter = log => log.endingTimestampString;
					break;
				case nameof(EndingTimestampString):
					backedValueGetter = log => log.endingTimestampString;
					break;
				case nameof(LevelString):
					getter = (log, buffer, offset) =>
					{
						if (offset < 0)
							throw new ArgumentOutOfRangeException(nameof(offset));
						var s = log.LevelString;
						if (offset + s.Length > buffer.Length)
							return ~s.Length;
						s.AsSpan().CopyTo(offset == 0 ? buffer : buffer[offset..^0]);
						return s.Length;
					};
					logStringPropertyGetters[propertyName] = getter;
					return getter;
				case nameof(ReadTimeString):
					backedValueGetter = log => log.readTimeString;
					break;
				case nameof(TimeSpanString):
					backedValueGetter = log => log.timeSpanString;
					break;
				case nameof(TimestampString):
					backedValueGetter = log => log.timestampString;
					break;
				default:
					getter = Log.CreateStringPropertyGetter(propertyName).Let(getter =>
					{
						return new DisplayableLogStringPropertyGetter((log, buffer, offset) => getter(log.Log, buffer, offset));
					});
					logStringPropertyGetters[propertyName] = getter;
					return getter;
			}
			getter = (log, buffer, offset) =>
			{
				var backedValue = backedValueGetter(log);
				if (backedValue != null)
					return backedValue.GetString(buffer, offset);
				if (log.TryGetProperty<string>(propertyName, out var s))
				{
					if (offset < 0)
						throw new ArgumentOutOfRangeException(nameof(offset));
					if (offset + s.Length > buffer.Length)
						return ~s.Length;
					s.AsSpan().CopyTo(offset == 0 ? buffer : buffer[offset..^0]);
					return s.Length;
				}
				return 0;
			};
			logStringPropertyGetters[propertyName] = getter;
			return getter;
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
#if DEBUG
			this.VerifyAccess();
#endif

			// notify
			this.Group.OnDisplayableLogDisposed(this);
		}


		/// <summary>
		/// Get ending time span of log.
		/// </summary>
		public TimeSpan? EndingTimeSpan { get => this.Log.EndingTimeSpan; }


		/// <summary>
		/// Get ending time span of log in string format.
		/// </summary>
		public string EndingTimeSpanString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimeSpan(this.Log.EndingTimeSpan);
				if (this.endingTimeSpanString == null)
				{
					this.endingTimeSpanString = this.FormatTimeSpanCompressed(this.Log.EndingTimeSpan);
					if (this.endingTimeSpanString != CompressedString.Empty)
					{
						var memorySizeDiff = this.endingTimeSpanString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.endingTimeSpanString.ToString();
			}
		}


		/// <summary>
		/// Get ending timestamp.
		/// </summary>
		public DateTime? EndingTimestamp { get => this.Log.EndingTimestamp; }


		/// <summary>
		/// Get ending timestamp of log in string format.
		/// </summary>
		public string EndingTimestampString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimestamp(this.Log.EndingTimestamp);
				if (this.endingTimestampString == null)
				{
					this.endingTimestampString = this.FormatTimestampCompressed(this.Log.EndingTimestamp);
					if (this.endingTimestampString != CompressedString.Empty)
					{
						var memorySizeDiff = this.endingTimestampString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.endingTimestampString.ToString();
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
		string FormatTimeSpan(TimeSpan? timeSpan)
		{
			if (timeSpan == null)
				return string.Empty;
			try
			{
				var format = this.Group.LogProfile.TimeSpanFormatForDisplaying;
				if (format != null)
					return timeSpan.Value.ToString(format);
				return timeSpan.Value.ToString();
			}
			catch
			{
				return string.Empty;
			}
		}


		// Format timestamp to compressed string.
		CompressedString FormatTimeSpanCompressed(TimeSpan? timeSpan)
		{
			var s = this.FormatTimeSpan(timeSpan);
			if (s.Length == 0)
				return CompressedString.Empty;
			var level = this.Group.MemoryUsagePolicy switch
			{
				MemoryUsagePolicy.Balance => CompressedString.Level.Fast,
				MemoryUsagePolicy.LessMemoryUsage => CompressedString.Level.Optimal,
				_ => CompressedString.Level.None,
			};
			return CompressedString.Create(s, level)!;
		}


		// Format timestamp to string.
		string FormatTimestamp(DateTime? timestamp)
		{
			if (timestamp == null)
				return string.Empty;
			try
			{
				var format = this.Group.LogProfile.TimestampFormatForDisplaying;
				if (format != null)
					return timestamp.Value.ToString(format);
				return timestamp.Value.ToString();
			}
			catch
			{
				return string.Empty;
			}
		}


		// Format timestamp to compressed string.
		CompressedString FormatTimestampCompressed(DateTime? timestamp)
		{
			var s = this.FormatTimestamp(timestamp);
			if (s.Length == 0)
				return CompressedString.Empty;
			var level = this.Group.MemoryUsagePolicy switch
			{
				MemoryUsagePolicy.Balance => CompressedString.Level.Fast,
				MemoryUsagePolicy.LessMemoryUsage => CompressedString.Level.Optimal,
				_ => CompressedString.Level.None,
			};
			return CompressedString.Create(s, level)!;
		}


		// Get number of lines of ExtraX.
		int GetExtraLineCount(int index)
		{
			if (index >= this.extraLineCount.Length)
				return 0;
			if (this.extraLineCount[index] == 0)
				this.extraLineCount[index] = CalculateLineCount(extraGetters[index](this.Log));
			return this.extraLineCount[index];
		}


		/// <summary>
		/// Get <see cref="DisplayableLogGroup"/> which the instance belongs to.
		/// </summary>
		public DisplayableLogGroup Group { get; }


		/// <summary>
		/// Check whether at least one <see cref="DisplayableLogAnalysisResult"/> has been added to this log or not.
		/// </summary>
		public bool HasAnalysisResult { get => this.analysisResults.IsNotEmpty(); }


		/// <summary>
		/// Check whether given property of log with <see cref="DateTime"/> value is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasDateTimeProperty(string propertyName) =>
			Log.HasDateTimeProperty(propertyName);
		

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
		public bool HasExtraLinesOfMessage { get=> this.MessageLineCount > this.Group.MaxDisplayLineCount; }


		/// <summary>
		/// Check whether number of lines in <see cref="Summary"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfSummary { get => this.SummaryLineCount > this.Group.MaxDisplayLineCount; }


		/// <summary>
		/// Check whether given property of log with <see cref="Int64"/> value is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasInt64Property(string propertyName) => propertyName switch
		{
			nameof(BinaryBeginningTimeSpan) 
			or nameof(BinaryBeginningTimestamp)
			or nameof(BinaryEndingTimeSpan)
			or nameof(BinaryEndingTimestamp)
			or nameof(BinaryReadTime)
			or nameof(BinaryTimeSpan)
			or nameof(BinaryTimestamp) => true,
			_ => false,
		};


		/// <summary>
		/// Check whether given log property is exported by <see cref="DisplayableLog"/> with multi-line <see cref="string"/> value or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if given log property is exported.</returns>
		public static bool HasMultiLineStringProperty(string propertyName) => Log.HasMultiLineStringProperty(propertyName);


		/// <summary>
		/// Check whether given property of log is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasProperty(string propertyName)
		{
			SetupPropertyMap();
			return propertyMap.ContainsKey(propertyName);
		}


		/// <summary>
		/// Check whether given property of log with string value is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasStringProperty(string propertyName) => propertyName switch
		{
			nameof(BeginningTimeSpanString) 
			or nameof(BeginningTimestampString)
			or nameof(EndingTimeSpanString)
			or nameof(EndingTimestampString)
			or nameof(LevelString)
			or nameof(ReadTimeString)
			or nameof(TimeSpanString)
			or nameof(TimestampString) => true,
			_ => Log.HasStringProperty(propertyName),
		};


		/// <summary>
		/// Check whether the value of <see cref="MarkedColor"/> is not <see cref="MarkColor.None"/> or not.
		/// </summary>
		public bool IsMarked { get => this.markedColor != MarkColor.None; }


		/// <summary>
		/// Check whether process ID of log has been selected by user or not.
		/// </summary>
		public bool IsProcessIdSelected 
		{ 
			get => this.Log.ProcessId.Let(it =>
				it.HasValue && it.Value == this.Group.SelectedProcessId); 
		}


		/// <summary>
		/// Check whether thread ID of log has been selected by user or not.
		/// </summary>
		public bool IsThreadIdSelected 
		{ 
			get => this.Log.ThreadId.Let(it =>
				it.HasValue && it.Value == this.Group.SelectedThreadId); 
		}


		/// <summary>
		/// Get level of log.
		/// </summary>
		public LogLevel Level { get => this.Log.Level; }


		/// <summary>
		/// Get foreground <see cref="IBrush"/> according to level of log.
		/// </summary>
		public IBrush LevelBackgroundBrush { get => this.Group.GetLevelBackgroundBrush(this); }


		/// <summary>
		/// Get foreground <see cref="IBrush"/> according to level of log.
		/// </summary>
		public IBrush LevelForegroundBrush { get => this.Group.GetLevelForegroundBrush(this); }


		/// <summary>
		/// Get foreground <see cref="IBrush"/> for pointer-over according to level of log.
		/// </summary>
		public IBrush LevelForegroundBrushForPointerOver { get => this.Group.GetLevelForegroundBrush(this, "PointerOver"); }


		/// <summary>
		/// Get string representation of <see cref="Level"/>.
		/// </summary>
		public string LevelString
		{
			get
			{
				var level = this.Log.Level;
				if (level == LogLevel.Undefined)
					return "";
				if (this.Group.LevelMapForDisplaying.TryGetValue(level, out var s))
					return s;
				var propertyName = this.Group.LogProfile.RawLogLevelPropertyName;
				if (propertyName != nameof(Level) && this.TryGetProperty(propertyName, out s) && s != null)
					return s;
				levelConverter ??= new(this.Application, typeof(LogLevel));
				return levelConverter.Convert<string?>(level) ?? level.ToString();
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
		/// Get or set color of marking.
		/// </summary>
		public MarkColor MarkedColor
		{
			get => this.markedColor;
			set
			{
#if DEBUG
				this.VerifyAccess();
#endif
				if (this.markedColor == value)
					return;
				var isPrevMarked = this.IsMarked;
				this.markedColor = value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkedColor)));
				if (this.IsMarked != isPrevMarked)
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarked)));
			}
		}


		/// <summary>
		/// Get size of memory usage by the instance in bytes.
		/// </summary>
		public long MemorySize { get => this.memorySize; }


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
				if (this.messageLineCount == 0)
					this.messageLineCount = CalculateLineCount(this.Log.Message);
				return this.messageLineCount;
			}
		}


		/// <summary>
		/// Next tracked instance.
		/// </summary>
		internal DisplayableLog? Next;


		/// <summary>
		/// Called when application string resources updated.
		/// </summary>
		internal void OnApplicationStringsUpdated()
		{
			this.OnTimeSpanFormatChanged();
			this.OnTimestampFormatChanged();
			this.PropertyChanged?.Invoke(this, new(nameof(LevelString)));
		}


		/// <summary>
		/// Called when log level map has been changed.
		/// </summary>
		internal void OnLevelMapForDisplayingChanged() =>
			this.PropertyChanged?.Invoke(this, new(nameof(LevelString)));


		/// <summary>
		/// Called when maximum display line count changed.
		/// </summary>
		internal void OnMaxDisplayLineCountChanged()
		{
			// check attached property changed handlers
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers == null)
				return;

			// check extra line count
			for (var i = this.extraLineCount.Length - 1; i >= 0; --i)
			{
				if (this.extraLineCount[i] > 0)
					propertyChangedHandlers(this, new PropertyChangedEventArgs($"HasExtraLinesOfExtra{i + 1}"));
			}

			// check message line count
			if (this.messageLineCount >= 0)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(HasExtraLinesOfMessage)));

			// check summary line count
			if (this.summaryLineCount >= 0)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(HasExtraLinesOfSummary)));
		}


		/// <summary>
		/// Called when policy of memory usage changed.
		/// </summary>
		/// <param name="policy">New policy.</param>
		internal void OnMemoryUsagePolicyChanged(MemoryUsagePolicy policy)
		{
			if (policy == MemoryUsagePolicy.LessMemoryUsage)
			{
				var memorySizeDiff = 0L;
				if (this.beginningTimeSpanString != null)
				{
					if (this.beginningTimeSpanString != CompressedString.Empty)
						memorySizeDiff -= this.beginningTimeSpanString.Size;
					this.beginningTimeSpanString = null;
				}
				if (this.beginningTimestampString != null)
				{
					if (this.beginningTimestampString != CompressedString.Empty)
						memorySizeDiff -= this.beginningTimestampString.Size;
					this.beginningTimestampString = null;
				}
				if (this.endingTimeSpanString != null)
				{
					if (this.endingTimeSpanString != CompressedString.Empty)
						memorySizeDiff -= this.endingTimeSpanString.Size;
					this.endingTimeSpanString = null;
				}
				if (this.endingTimestampString != null)
				{
					if (this.endingTimestampString != CompressedString.Empty)
						memorySizeDiff -= this.endingTimestampString.Size;
					this.endingTimestampString = null;
				}
				if (this.timeSpanString != null)
				{
					if (this.timeSpanString != CompressedString.Empty)
						memorySizeDiff -= this.timeSpanString.Size;
					this.timeSpanString = null;
				}
				if (this.timestampString != null)
				{
					if (this.timestampString != CompressedString.Empty)
						memorySizeDiff -= this.timestampString.Size;
					this.timestampString = null;
				}
				if (memorySizeDiff != 0)
				{
					this.memorySize += (short)memorySizeDiff;
					this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
				}
			}
		}


		// Called when user selected process ID changed.
		internal void OnSelectedProcessIdChanged()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers != null && this.Log.ProcessId.HasValue)
				propertyChangedHandlers(this, new(nameof(IsProcessIdSelected)));
		}


		// Called when user selected tread ID changed.
		internal void OnSelectedThreadIdChanged()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers != null && this.Log.ThreadId.HasValue)
				propertyChangedHandlers(this, new(nameof(IsThreadIdSelected)));
		}


		/// <summary>
		/// Called when style related resources has been updated.
		/// </summary>
		internal void OnStyleResourcesUpdated()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers == null)
				return;
			if (this.analysisResults != null)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(AnalysisResultIndicatorIcon)));
			propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(ColorIndicatorBrush)));
			propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(LevelBackgroundBrush)));
			propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(LevelForegroundBrush)));
			propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(LevelForegroundBrushForPointerOver)));
		}


		/// <summary>
		/// Called when format of displaying time span has been changed.
		/// </summary>
		internal void OnTimeSpanFormatChanged()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers == null)
			{
				this.beginningTimeSpanString = null;
				this.endingTimeSpanString = null;
				this.timeSpanString = null;
			}
			else
			{
				if (this.Log.BeginningTimeSpan.HasValue)
				{
					this.beginningTimeSpanString = null;
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(BeginningTimeSpanString)));
				}
				if (this.Log.EndingTimeSpan.HasValue)
				{
					this.endingTimeSpanString = null;
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(EndingTimeSpanString)));
				}
				if (this.Log.TimeSpan.HasValue)
				{
					this.timeSpanString = null;
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(TimeSpanString)));
				}
			}
		}


		/// <summary>
		/// Called when format of displaying timestamp has been changed.
		/// </summary>
		internal void OnTimestampFormatChanged()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers == null)
			{
				this.beginningTimestampString = null;
				this.endingTimestampString = null;
				this.timestampString = null;
			}
			else
			{
				if (this.Log.BeginningTimestamp.HasValue)
				{
					this.beginningTimestampString = null;
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(BeginningTimestampString)));
				}
				if (this.Log.EndingTimestamp.HasValue)
				{
					this.endingTimestampString = null;
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(EndingTimestampString)));
				}
				if (this.Log.Timestamp.HasValue)
				{
					this.timestampString = null;
					propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(TimestampString)));
				}
			}
		}


		/// <summary>
		/// Previous tracked instance.
		/// </summary>
		internal DisplayableLog? Previous;


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId { get => this.Log.ProcessId; }


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public string? ProcessName { get => this.Log.ProcessName; }


		/// <summary>
		/// Get the timestamp of this log was read.
		/// </summary>
		public DateTime ReadTime => this.Log.ReadTime;


		/// <summary>
		/// Get timestamp of this log was read in string format.
		/// </summary>
		public string ReadTimeString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimestamp(this.Log.ReadTime);
				if (this.readTimeString == null)
				{
					this.readTimeString = this.FormatTimestampCompressed(this.Log.ReadTime);
					if (this.readTimeString != CompressedString.Empty)
					{
						var memorySizeDiff = this.readTimeString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.readTimeString.ToString();
			}
		}


		/// <summary>
		/// Remove <see cref="DisplayableLogAnalysisResult"/> from this log.
		/// </summary>
		/// <param name="result">Result to remove.</param>
		public void RemoveAnalysisResult(DisplayableLogAnalysisResult result)
		{
			// check state
#if DEBUG
			this.VerifyAccess();
#endif
			if (this.IsDisposed)
				return;
			var currentResultCount = this.analysisResults.Count;
			if (currentResultCount == 0)
				return;
			
			// remove from list
			var memorySizeDiff = 0L;
			for (var i = currentResultCount - 1; i >= 0; --i)
			{
				if (this.analysisResults[i] == result)
				{
					if (currentResultCount == 1)
					{
						this.analysisResults = emptyAnalysisResults;
						memorySizeDiff -= Memory.EstimateArrayInstanceSize(IntPtr.Size, 1) + Memory.EstimateCollectionInstanceSize(IntPtr.Size, 0);
					}
					else
					{
						var oldList = this.analysisResults;
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						if (i == 0)
						{
							for (var j = currentResultCount - 1; j > 0; --j)
								newList[j - 1] = oldList[j];
						}
						else if (i == currentResultCount - 1)
						{
							for (var j = currentResultCount - 2; j >= 0; --j)
								newList[j] = oldList[j];
						}
						else
						{
							for (var j = 0; j < i; ++j)
								newList[j] = oldList[j];
							for (var j = currentResultCount - 1; j > i; --j)
								newList[j - 1] = oldList[j];
						}
						this.analysisResults = newList;
						memorySizeDiff -= IntPtr.Size;
					}
					break;
				}
			}
			if (memorySizeDiff == 0)
				return;
			
			// update active type
			if (this.activeAnalysisResultType >= result.Type)
			{
				this.activeAnalysisResultType = currentResultCount > 1 ? this.analysisResults[0].Type : 0;
				for (var i = currentResultCount - 2; i >= 1; --i)
				{
					var type = this.analysisResults[i].Type;
					if (this.activeAnalysisResultType > type)
						this.activeAnalysisResultType = type;
				}
				if (this.activeAnalysisResultType > result.Type)
					this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
				if (currentResultCount == 1)
					this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			}
			
			// update memory usage
			this.memorySize += (short)memorySizeDiff;
			this.Group.OnAnalysisResultRemoved(this);
			this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
		}


		/// <summary>
		/// Remove <see cref="DisplayableLogAnalysisResult"/>s from this log which were generated by given analyzer.
		/// </summary>
		/// <param name="analyzer"><see cref="DisplayableLogAnalysisResult"/> which generates results.</param>
		public void RemoveAnalysisResults(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
		{
			// check state
#if DEBUG
			this.VerifyAccess();
#endif
			if (this.IsDisposed)
				return;
			var currentResultCount = this.analysisResults.Count;
			if (currentResultCount == 0)
				return;
			
			// remove from list
			var memorySizeDiff = 0L;
			for (var i = currentResultCount - 1; i >= 0; --i)
			{
				if (this.analysisResults[i].Analyzer == analyzer)
				{
					if (currentResultCount == 1)
					{
						this.analysisResults = emptyAnalysisResults;
						memorySizeDiff -= Memory.EstimateArrayInstanceSize(IntPtr.Size, 1) + Memory.EstimateCollectionInstanceSize(IntPtr.Size, 0);
						currentResultCount = 0;
						break;
					}
					else
					{
						var oldList = this.analysisResults;
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						if (i == 0)
						{
							for (var j = currentResultCount - 1; j > 0; --j)
								newList[j - 1] = oldList[j];
						}
						else if (i == currentResultCount - 1)
						{
							for (var j = currentResultCount - 2; j >= 0; --j)
								newList[j] = oldList[j];
						}
						else
						{
							for (var j = 0; j < i; ++j)
								newList[j] = oldList[j];
							for (var j = currentResultCount - 1; j > i; --j)
								newList[j - 1] = oldList[j];
						}
						this.analysisResults = newList;
						memorySizeDiff -= IntPtr.Size;
					}
					--currentResultCount;
				}
			}
			if (memorySizeDiff == 0)
				return;
			
			// update active type
			var currentActiveResultType = this.activeAnalysisResultType;
			this.activeAnalysisResultType = currentResultCount > 0 ? this.analysisResults[0].Type : 0;
			for (var i = currentResultCount - 1; i >= 1; --i)
			{
				var type = this.analysisResults[i].Type;
				if (this.activeAnalysisResultType > type)
					this.activeAnalysisResultType = type;
			}
			if (this.activeAnalysisResultType > currentActiveResultType)
				this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
			if (currentResultCount == 0)
				this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			
			// update memory usage
			this.memorySize += (short)memorySizeDiff;
			this.Group.OnAnalysisResultRemoved(this);
			this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
		}


		// Setup property map.
		static void SetupPropertyMap()
		{
			if (!isPropertyMapReady)
			{
				var type = typeof(DisplayableLog);
				lock (type)
				{
					if (!isPropertyMapReady)
					{
						foreach (var propertyName in Log.PropertyNames)
						{
							var convertedName = (propertyName == nameof(Logs.Log.Id))
								? nameof(LogId)
								: propertyName;
							try
							{
								type.GetProperty(convertedName)?.Let(it =>
								{
									propertyMap[convertedName] = it;
								});
							}
							catch
							{ }
						}
						var specificPropertyNames = new string[]
						{
							nameof(BeginningTimeSpanString),
							nameof(BeginningTimestampString),
							nameof(BinaryBeginningTimeSpan),
							nameof(BinaryBeginningTimestamp),
							nameof(BinaryEndingTimeSpan),
							nameof(BinaryEndingTimestamp),
							nameof(BinaryTimeSpan),
							nameof(BinaryEndingTimestamp),
							nameof(EndingTimeSpanString),
							nameof(EndingTimestampString),
							nameof(LevelString),
							nameof(ReadTimeString),
							nameof(TimeSpanString),
							nameof(TimestampString),
						};
						foreach (var propertyName in specificPropertyNames)
						{
							type.GetProperty(propertyName)?.Let(it =>
								propertyMap[propertyName] = it);
						}
						isPropertyMapReady = true;
					}
				}
			}
		}


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
				if (this.summaryLineCount == 0)
					this.summaryLineCount = CalculateLineCount(this.Log.Summary);
				return this.summaryLineCount;
			}
		}


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public string? Tags { get => this.Log.Tags; }


		/// <summary>
		/// Get definition set of text highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet TextHighlightingDefinitionSet { get => this.Group.TextHighlightingDefinitionSet; }


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId { get => this.Log.ThreadId; }


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public string? ThreadName { get => this.Log.ThreadName; }


		/// <summary>
		/// Get time span of log.
		/// </summary>
		public TimeSpan? TimeSpan { get => this.Log.TimeSpan; }


		/// <summary>
		/// Get time span of log in string format.
		/// </summary>
		public string TimeSpanString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimeSpan(this.Log.TimeSpan);
				if (this.timeSpanString == null)
				{
					this.timeSpanString = this.FormatTimeSpanCompressed(this.Log.TimeSpan);
					if (this.timeSpanString != CompressedString.Empty)
					{
						var memorySizeDiff = this.timeSpanString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.timeSpanString.ToString();
			}
		}


		/// <summary>
		/// Get timestamp of log.
		/// </summary>
		public DateTime? Timestamp { get => this.Log.Timestamp; }


		/// <summary>
		/// Get timestamp of log in string format.
		/// </summary>
		public string TimestampString
		{
			get
			{
				if (this.Group.MemoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					return this.FormatTimestamp(this.Log.Timestamp);
				if (this.timestampString == null)
				{
					this.timestampString = this.FormatTimestampCompressed(this.Log.Timestamp);
					if (this.timestampString != CompressedString.Empty)
					{
						var memorySizeDiff = this.timestampString.Size;
						this.memorySize += (short)memorySizeDiff;
						this.Group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
					}
				}
				return this.timestampString.ToString();
			}
		}


		/// <summary>
		/// Get title of log.
		/// </summary>
		public string? Title { get => this.Log.Title; }


		/// Try getting the earliest/latest timestamp from <see cref="BeginningTimestamp"/>, <see cref="EndingTimestamp"/> and <see cref="Timestamp"/>.
		/// </summary>
		/// <param name="earliestTimestamp">The earliest timestamp.</param>
		/// <param name="latestTimestamp">The latest timestamp.</param>
		/// <returns>True if the earliest/latest timestamp are valid.</returns>
		public unsafe bool TryGetEarliestAndLatestTimestamp([NotNullWhen(true)] out DateTime? earliestTimestamp, [NotNullWhen(true)] out DateTime? latestTimestamp) =>
			this.Log.TryGetEarliestAndLatestTimestamp(out earliestTimestamp, out latestTimestamp);


#pragma warning disable CS8600
#pragma warning disable CS8601
		/// <summary>
		/// Get get property of log by name.
		/// </summary>
		/// <typeparam name="T">Type of property.</typeparam>
		/// <param name="propertyName">Name of property.</param>
		/// <param name="value">Property value.</param>
		/// <returns>True if value of property get successfully.</returns>
		public bool TryGetProperty<T>(string propertyName, out T value)
		{
			SetupPropertyMap();
			if (propertyMap.TryGetValue(propertyName, out var propertyInfo)
				&& propertyInfo != null
				&& typeof(T).IsAssignableFrom(propertyInfo.PropertyType))
			{
				value = (T)propertyInfo.GetValue(this);
				return true;
			}
			value = default;
			return false;
		}
#pragma warning restore CS8600
#pragma warning restore CS8601


		/// Try getting the smallest/largest time span from <see cref="BeginningTimeSpan"/>, <see cref="EndingTimeSpan"/> and <see cref="TimeSpan"/>.
		/// </summary>
		/// <param name="smallestTimeSpan">The smallest time span.</param>
		/// <param name="largestTimeSpan">The largest time span.</param>
		/// <returns>True if the smallest/largest time span are valid.</returns>
		public unsafe bool TryGetSmallestAndLargestTimeSpan([NotNullWhen(true)] out TimeSpan? smallestTimeSpan, [NotNullWhen(true)] out TimeSpan? largestTimeSpan) =>
			this.Log.TryGetSmallestAndLargestTimeSpan(out smallestTimeSpan, out largestTimeSpan);


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


	/// <summary>
	/// Delegate of direct event handler for <see cref="DisplayableLog"/>.
	/// </summary>
	delegate void DirectDisplayableLogEventHandler(DisplayableLogGroup group, DisplayableLog log);


	/// <summary>
	/// Delegate to get value of string property of log.
	/// </summary>
	/// <param name="log">Log.</param>
	/// <param name="buffer">Buffer.</param>
	/// <param name="offset">Offset in buffer to put first character.</param>
	/// <returns>Number of characters in original string, or 1's complement of number of characters if size of buffer is insufficient.</returns>
	delegate int DisplayableLogStringPropertyGetter(DisplayableLog log, Span<char> buffer, int offset = 0);


	/// <summary>
	/// Color of marking of <see cref="DisplayableLog"/>.
	/// </summary>
	enum MarkColor
    {
		/// <summary>
		/// Not marked.
		/// </summary>
		None,
		/// <summary>
		/// Default.
		/// </summary>
		Default,
		/// <summary>
		/// Red.
		/// </summary>
		Red,
		/// <summary>
		/// Orange.
		/// </summary>
		Orange,
		/// <summary>
		/// Yellow.
		/// </summary>
		Yellow,
		/// <summary>
		/// Green.
		/// </summary>
		Green,
		/// <summary>
		/// Blue.
		/// </summary>
		Blue,
		/// <summary>
		/// Indigo.
		/// </summary>
		Indigo,
		/// <summary>
		/// Purple.
		/// </summary>
		Purple,
		/// <summary>
		/// Magenta.
		/// </summary>
		Magenta,
	}
}
