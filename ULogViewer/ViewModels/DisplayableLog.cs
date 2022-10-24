using Avalonia.Media;
using CarinaStudio.Collections;
using CarinaStudio.Data.Converters;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
		static readonly DisplayableLogAnalysisResult[] emptyAnalysisResults = new DisplayableLogAnalysisResult[0];
		static readonly byte[] emptyByteArray = new byte[0];
		static readonly long estimatedTimeSpanSize = System.TimeSpan.FromDays(1.23456).ToString().Length << 1;
		static readonly long estimatedTimestampSize = DateTime.Now.ToString().Length << 1;
		static readonly Func<Log, string?>[] extraGetters = new Func<Log, string?>[Log.ExtraCapacity].Also(it =>
		{
			for (var i = it.Length - 1; i >= 0; --i)
				it[i] = Log.CreatePropertyGetter<string?>($"Extra{i + 1}");
		});
		static readonly long instanceFieldMemorySize = Memory.EstimateInstanceSize<DisplayableLog>();
		static volatile bool isPropertyMapReady;
		static Dictionary<string, PropertyInfo> propertyMap = new Dictionary<string, PropertyInfo>();


		// Fields.
		DisplayableLogAnalysisResultType activeAnalysisResultType;
		IImage? analysisResultIndicatorIcon;
		DisplayableLogAnalysisResult[] analysisResults = emptyAnalysisResults;
		CompressedString? beginningTimeSpanString;
		CompressedString? beginningTimestampString;
		CompressedString? endingTimeSpanString;
		CompressedString? endingTimestampString;
		readonly byte[] extraLineCount;
		MarkColor markedColor;
		long memorySize;
		int messageLineCount = -1;
		IList<DisplayableLogAnalysisResult> readOnlyAnalysisResults = emptyAnalysisResults;
		int summaryLineCount = -1;
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
			this.Application = group.Application;
			this.BinaryBeginningTimeSpan = (long)(log.BeginningTimeSpan?.TotalMilliseconds ?? 0);
			this.BinaryBeginningTimestamp = log.BeginningTimestamp?.ToBinary() ?? 0L;
			this.BinaryEndingTimeSpan = (long)(log.EndingTimeSpan?.TotalMilliseconds ?? 0);
			this.BinaryEndingTimestamp = log.EndingTimestamp?.ToBinary() ?? 0L;
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
			if (log.BeginningTimeSpan.HasValue)
				memorySize += estimatedTimeSpanSize;
			if (log.EndingTimeSpan.HasValue)
				memorySize += estimatedTimeSpanSize;
			if (log.TimeSpan.HasValue)
				memorySize += estimatedTimeSpanSize;
			if (log.BeginningTimestamp.HasValue)
				memorySize += estimatedTimestampSize;
			if (log.EndingTimestamp.HasValue)
				memorySize += estimatedTimestampSize;
			if (log.Timestamp.HasValue)
				memorySize += estimatedTimestampSize;
			this.memorySize = memorySize;

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
			var currentResultCount = this.analysisResults.Length;
			if (currentResultCount == 0)
			{
				this.analysisResults = new[] { result };
				memorySizeDiff = Memory.EstimateArrayInstanceSize(IntPtr.Size, 1);
			}
			else
			{
				var newList = new DisplayableLogAnalysisResult[currentResultCount + 1];
				Array.Copy(this.analysisResults, newList, currentResultCount);
				newList[currentResultCount] = result;
				this.analysisResults = newList;
				memorySizeDiff += IntPtr.Size;
			}
			this.readOnlyAnalysisResults = this.analysisResults.AsReadOnly();

			// select active type
			if (this.activeAnalysisResultType == 0 || this.activeAnalysisResultType > result.Type)
			{
				this.activeAnalysisResultType = result.Type;
				this.analysisResultIndicatorIcon = null;
				this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
			}
			if (currentResultCount == 0)
				this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			
			// update memory usage
			this.memorySize += memorySizeDiff;
			this.Group.OnAnalysisResultAdded(this);
			this.Group.OnDisplayableLogMemorySizeChanged(this, memorySizeDiff);
		}


		/// <summary>
		/// Get icon for analysis result indicator.
		/// </summary>
		public IImage? AnalysisResultIndicatorIcon 
		{ 
			get 
			{
				if (this.analysisResultIndicatorIcon != null || this.activeAnalysisResultType == 0)
					return this.analysisResultIndicatorIcon;
				this.analysisResultIndicatorIcon = DisplayableLogAnalysisResultIconConverter.Default.Convert<IImage?>(this.activeAnalysisResultType);
				return this.analysisResultIndicatorIcon;
			}
		}


		/// <summary>
		/// Get all <see cref="DisplayableLogAnalysisResult"/>s which were added to this log.
		/// </summary>
		public IList<DisplayableLogAnalysisResult> AnalysisResults { get => this.readOnlyAnalysisResults; }


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }


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
				if (this.beginningTimeSpanString == null)
					this.beginningTimeSpanString = this.FormatTimeSpan(this.Log.BeginningTimeSpan);
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
				if (this.beginningTimestampString == null)
					this.beginningTimestampString = this.FormatTimestamp(this.Log.BeginningTimestamp);
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
				nameof(BinaryTimeSpan) => (it => (T)(object)it.BinaryTimeSpan),
				nameof(BinaryTimestamp) => (it => (T)(object)it.BinaryTimestamp),
				nameof(EndingTimeSpanString) => (it => (T)(object)it.EndingTimeSpanString),
				nameof(EndingTimestampString) => (it => (T)(object)it.EndingTimestampString),
				nameof(LogId) => (it => (T)(object)it.LogId),
				nameof(TimeSpanString) => (it => (T)(object)it.TimeSpanString),
				nameof(TimestampString) => (it => (T)(object)it.TimestampString),
				_ => Log.CreatePropertyGetter<T>(propertyName).Let(getter =>
				{
					return new Func<DisplayableLog, T>(it => getter(it.Log));
				}),
			};
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
				if (this.endingTimeSpanString == null)
					this.endingTimeSpanString = this.FormatTimeSpan(this.Log.EndingTimeSpan);
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
				if (this.endingTimestampString == null)
					this.endingTimestampString = this.FormatTimestamp(this.Log.EndingTimestamp);
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
		CompressedString FormatTimeSpan(TimeSpan? timeSpan)
		{
			var level = this.Group.SaveMemoryAgressively ? CompressedString.Level.Fast : CompressedString.Level.None;
			var format = this.Group.LogProfile.TimeSpanFormatForDisplaying;
			if (timeSpan == null)
				return CompressedString.Empty;
			try
			{
				if (format != null)
					return CompressedString.Create(timeSpan.Value.ToString(format), level).AsNonNull();
				return CompressedString.Create(timeSpan.Value.ToString(), level).AsNonNull();
			}
			catch
			{
				return CompressedString.Empty;
			}
		}


		// Format timestamp to string.
		CompressedString FormatTimestamp(DateTime? timestamp)
		{
			var level = this.Group.SaveMemoryAgressively ? CompressedString.Level.Fast : CompressedString.Level.None;
			var format = this.Group.LogProfile.TimestampFormatForDisplaying;
			if (timestamp == null)
				return CompressedString.Empty;
			try
			{
				if (format != null)
					return CompressedString.Create(timestamp.Value.ToString(format), level).AsNonNull();
				return CompressedString.Create(timestamp.Value.ToString(), level).AsNonNull();
			}
			catch
			{
				return CompressedString.Empty;
			}
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
			or nameof(TimeSpanString)
			or nameof(TimestampString) => true,
			_ => Log.HasStringProperty(propertyName),
		};


		/// <summary>
		/// Check whether the value of <see cref="MarkedColor"/> is not <see cref="MarkColor.None"/> or not.
		/// </summary>
		public bool IsMarked { get => this.markedColor != MarkColor.None; }


		/// <summary>
		/// Get level of log.
		/// </summary>
		public LogLevel Level { get => this.Log.Level; }


		/// <summary>
		/// Get <see cref="IBrush"/> according to level of log.
		/// </summary>
		public IBrush LevelBrush { get => this.Group.GetLevelBrush(this); }


		/// <summary>
		/// Get <see cref="IBrush"/> for pointer-over according to level of log.
		/// </summary>
		public IBrush LevelBrushForPointerOver { get => this.Group.GetLevelBrush(this, "PointerOver"); }


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
				if (this.messageLineCount < 0)
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
		{ }


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
		/// Called when style related resources has been updated.
		/// </summary>
		internal void OnStyleResourcesUpdated()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers == null)
				return;
			propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(ColorIndicatorBrush)));
			propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelBrush)));
			propertyChangedHandlers?.Invoke(this, new PropertyChangedEventArgs(nameof(LevelBrushForPointerOver)));
		}


		/// <summary>
		/// Called when format of displaying time span has been changed.
		/// </summary>
		internal void OnTimeSpanFormatChanged()
		{
			if (this.Log.BeginningTimeSpan.HasValue)
			{
				this.beginningTimeSpanString = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BeginningTimeSpanString)));
			}
			if (this.Log.EndingTimeSpan.HasValue)
			{
				this.endingTimeSpanString = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EndingTimeSpanString)));
			}
			if (this.Log.TimeSpan.HasValue)
			{
				this.timeSpanString = null;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimeSpanString)));
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
			var currentResultCount = this.analysisResults.Length;
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
						this.readOnlyAnalysisResults = emptyAnalysisResults;
						memorySizeDiff -= Memory.EstimateArrayInstanceSize(IntPtr.Size, 1);
					}
					else if (i == 0)
					{
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						Array.Copy(this.analysisResults, 1, newList, 0, currentResultCount - 1);
						this.analysisResults = newList;
						this.readOnlyAnalysisResults = newList.AsReadOnly();
						memorySizeDiff -= IntPtr.Size;
					}
					else if (i == currentResultCount - 1)
					{
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						Array.Copy(this.analysisResults, 0, newList, 0, currentResultCount - 1);
						this.analysisResults = newList;
						this.readOnlyAnalysisResults = newList.AsReadOnly();
						memorySizeDiff -= IntPtr.Size;
					}
					else
					{
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						Array.Copy(this.analysisResults, 0, newList, 0, i);
						Array.Copy(this.analysisResults, i + 1, newList, i, currentResultCount - i - 1);
						this.analysisResults = newList;
						this.readOnlyAnalysisResults = newList.AsReadOnly();
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
				{
					this.analysisResultIndicatorIcon = null;
					this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
				}
				if (currentResultCount == 1)
					this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			}
			
			// update memory usage
			this.memorySize += memorySizeDiff;
			this.Group.OnAnalysisResultRemoved(this);
			this.Group.OnDisplayableLogMemorySizeChanged(this, memorySizeDiff);
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
			var currentResultCount = this.analysisResults.Length;
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
						this.readOnlyAnalysisResults = emptyAnalysisResults;
						memorySizeDiff -= Memory.EstimateArrayInstanceSize(IntPtr.Size, 1);
					}
					else if (i == 0)
					{
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						Array.Copy(this.analysisResults, 1, newList, 0, currentResultCount - 1);
						this.analysisResults = newList;
						memorySizeDiff -= IntPtr.Size;
					}
					else if (i == currentResultCount - 1)
					{
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						Array.Copy(this.analysisResults, 0, newList, 0, currentResultCount - 1);
						this.analysisResults = newList;
						memorySizeDiff -= IntPtr.Size;
					}
					else
					{
						var newList = new DisplayableLogAnalysisResult[currentResultCount - 1];
						Array.Copy(this.analysisResults, 0, newList, 0, i);
						Array.Copy(this.analysisResults, i + 1, newList, i, currentResultCount - i - 1);
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
			{
				this.analysisResultIndicatorIcon = null;
				this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
			}
			if (currentResultCount == 0)
				this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			
			// update memory usage
			this.memorySize += memorySizeDiff;
			this.Group.OnAnalysisResultRemoved(this);
			this.Group.OnDisplayableLogMemorySizeChanged(this, memorySizeDiff);
		}


		// Setup property map.
		static void SetupPropertyMap()
		{
			if (!isPropertyMapReady)
			{
				lock (typeof(DisplayableLog))
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
								typeof(DisplayableLog).GetProperty(convertedName)?.Let(it =>
								{
									propertyMap[convertedName] = it;
								});
							}
							catch
							{ }
						}
						typeof(DisplayableLog).Let(type =>
						{
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
								nameof(TimeSpanString),
								nameof(TimestampString),
							};
							foreach (var propertyName in specificPropertyNames)
							{
								type.GetProperty(propertyName)?.Let(it =>
									propertyMap[propertyName] = it);
							}
						});
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
				if (this.timeSpanString == null)
					this.timeSpanString = this.FormatTimeSpan(this.Log.TimeSpan);
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
				if (this.timestampString == null)
					this.timestampString = this.FormatTimestamp(this.Log.Timestamp);
				return this.timestampString.ToString();
			}
		}


		/// <summary>
		/// Get title of log.
		/// </summary>
		public string? Title { get => this.Log.Title; }


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
