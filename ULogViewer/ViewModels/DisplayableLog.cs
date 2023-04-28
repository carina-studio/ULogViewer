using Avalonia.Media;
using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Data.Converters;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using CarinaStudio.ULogViewer.Text;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Log which is suitable for displaying.
	/// </summary>
	unsafe class DisplayableLog : IApplicationObject, IDisposable, INotifyPropertyChanged
	{
		// Constants (Data offset).
		const int ActiveAnalysisResultTypeDataOffset = 0; // byte
		const int LogReaderLocalIdDataOffset = 1; // byte
		const int GroupIdDataOffset = 2; // uint
		const int IsDisposedDataOffset = 6; // byte
		const int MarkedColorDataOffset = 7; // byte
		const int MemorySizeDataOffset = 8; // uint
		const int MessageLineCountDataOffset = 12; // byte
		const int SummaryLineCountDataOffset = 13; // byte
		const int ExtraLineCountsDataOffset = 14; // byte[], should be last one


		// Static fields.
		static readonly DisplayableLogAnalysisResult[] emptyAnalysisResults = Array.Empty<DisplayableLogAnalysisResult>();
		static readonly Func<Log, IStringSource?>[] extraGetters = new Func<Log, IStringSource?>[Log.ExtraCapacity].Also(it =>
		{
			for (var i = it.Length - 1; i >= 0; --i)
				it[i] = Log.CreatePropertyGetter<IStringSource?>($"Extra{i + 1}");
		});
		static readonly long instanceFieldMemorySize = Memory.EstimateInstanceSize<DisplayableLog>();
		static volatile bool isPropertyMapReady;
		static AppSuite.Converters.EnumConverter? levelConverter;
		static readonly Dictionary<string, PropertyInfo> propertyMap = new();
		static readonly delegate*<ReadOnlySpan<byte>, ushort> readUInt16Function;
		static readonly delegate*<ReadOnlySpan<byte>, uint> readUInt32Function;
		static readonly delegate*<Span<byte>, ushort, void> writeUInt16Function;
		static readonly delegate*<Span<byte>, uint, void> writeUInt32Function;


		// Fields.
		IList<DisplayableLogAnalysisResult> analysisResults = emptyAnalysisResults;
		readonly byte[] data;
		
		
		// Static initializer.
		static DisplayableLog()
		{
			var n = 1;
			if (*(byte*)&n == 1) // BE
			{
				readUInt16Function = &BinaryPrimitives.ReadUInt16BigEndian;
				readUInt32Function = &BinaryPrimitives.ReadUInt32BigEndian;
				writeUInt16Function = &BinaryPrimitives.WriteUInt16BigEndian;
				writeUInt32Function = &BinaryPrimitives.WriteUInt32BigEndian;
			}
			else // LE
			{
				readUInt16Function = &BinaryPrimitives.ReadUInt16LittleEndian;
				readUInt32Function = &BinaryPrimitives.ReadUInt32LittleEndian;
				writeUInt16Function = &BinaryPrimitives.WriteUInt16LittleEndian;
				writeUInt32Function = &BinaryPrimitives.WriteUInt32LittleEndian;
			}
		}


		/// <summary>
		/// Initialize new <see cref="DisplayableLog"/> instance.
		/// </summary>
		/// <param name="group">Group of <see cref="DisplayableLog"/>.</param>
		/// <param name="reader">Log reader which reads the log.</param>
		/// <param name="log">Log.</param>
		internal DisplayableLog(DisplayableLogGroup group, LogReader reader, Log log)
		{
			// allocate data buffer
			var extraCount = group.LogExtraNumberCount;
			var data = new byte[ExtraLineCountsDataOffset + extraCount];
			this.data = data;
			
			// setup properties
			writeUInt32Function(data.AsSpan(GroupIdDataOffset), group.Id);
			this.Log = log;

			// estimate memory usage
			var memorySize = log.MemorySize 
			                  + instanceFieldMemorySize 
			                  + Memory.EstimateArrayInstanceSize<byte>(data.Length);
			writeUInt32Function(data.AsSpan(MemorySizeDataOffset), (uint)memorySize);

			// notify group
			group.OnDisplayableLogCreated(this, reader, out var readerLocalId);
			data[LogReaderLocalIdDataOffset] = readerLocalId;
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
			if (this.data[IsDisposedDataOffset] != 0)
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
			var data = this.data;
			if (data[ActiveAnalysisResultTypeDataOffset] == 0 || data[ActiveAnalysisResultTypeDataOffset] > (byte)result.Type)
			{
				data[ActiveAnalysisResultTypeDataOffset] = (byte)result.Type;
				this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
			}
			if (currentResultCount == 0)
				this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			
			// update memory usage
			var memorySizeSpan = data.AsSpan(MemorySizeDataOffset);
			var memorySize = readUInt32Function(memorySizeSpan);
			writeUInt32Function(memorySizeSpan, (uint)Math.Min(uint.MaxValue, memorySize + memorySizeDiff));
			if (DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group))
			{
				group.OnAnalysisResultAdded(this);
				group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
			}
		}


		/// <summary>
		/// Get icon for analysis result indicator.
		/// </summary>
		public IImage? AnalysisResultIndicatorIcon => 
			this.GroupOrNull?.GetAnalysisResultIndicatorIcon((DisplayableLogAnalysisResultType)this.data[ActiveAnalysisResultTypeDataOffset]);


		/// <summary>
		/// Get all <see cref="DisplayableLogAnalysisResult"/>s which were added to this log.
		/// </summary>
		public IList<DisplayableLogAnalysisResult> AnalysisResults => this.analysisResults;


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application =>
			this.GroupOrNull?.Application
			?? AppSuiteApplication.CurrentOrNull as IULogViewerApplication
			?? throw new InvalidOperationException();


		/// <summary>
		/// Get beginning time span of log.
		/// </summary>
		public TimeSpan? BeginningTimeSpan => this.Log.BeginningTimeSpan;


		/// <summary>
		/// Get beginning time span of log in string format.
		/// </summary>
		public IStringSource? BeginningTimeSpanString => this.FormatTimeSpan(this.Log.BeginningTimeSpan);


		/// <summary>
		/// Get beginning timestamp of log.
		/// </summary>
		public DateTime? BeginningTimestamp => this.Log.BeginningTimestamp;


		/// <summary>
		/// Get beginning timestamp of log in string format.
		/// </summary>
		public IStringSource? BeginningTimestampString => this.FormatTimestamp(this.Log.BeginningTimestamp);


		// Calculate line count.
		static byte CalculateLineCount(IStringSource? text)
		{
			if (text == null)
				return 0;
			var textLength = text.Length;
			if (textLength == 0)
				return 1;
			var lineCount = (byte)1;
			var textBuffer = new char[textLength];
			if (!text.TryCopyTo(textBuffer.AsSpan()))
				return 1;
			fixed (char* p = textBuffer)
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
		public IStringSource? Category => this.Log.Category;


		// Check whether extra line of ExtraX exist or not.
		bool CheckExtraLinesOfExtra(int index) => this.GetExtraLineCount(index) > this.GroupOrNull?.MaxDisplayLineCount;


		/// <summary>
		/// Get <see cref="IBrush"/> of color indicator.
		/// </summary>
		public IBrush? ColorIndicatorBrush => this.GroupOrNull?.GetColorIndicatorBrush(this);


		/// <summary>
		/// Get tip text for color indicator.
		/// </summary>
		public string? ColorIndicatorTip => this.GroupOrNull?.GetColorIndicatorTip(this);


#pragma warning disable CS8600
#pragma warning disable CS8603
		/// <summary>
		/// Create <see cref="Func{T, TResult}"/> to get specific log property from <see cref="DisplayableLog"/>.
		/// </summary>
		/// <typeparam name="T">Type of property value.</typeparam>
		/// <param name="propertyName">Name of property.</param>
		/// <returns><see cref="Func{T, TResult}"/>.</returns>
		public static Func<DisplayableLog, T> CreateLogPropertyGetter<T>(string propertyName)
		{
			Func<DisplayableLog, object?> rawPropertyGetter = propertyName switch
			{
				nameof(BeginningTimeSpanString) => it => it.BeginningTimeSpanString,
				nameof(BeginningTimestampString) => it => it.BeginningTimestampString,
				nameof(EndingTimeSpanString) => it => it.EndingTimeSpanString,
				nameof(EndingTimestampString) => it => it.EndingTimestampString,
				nameof(LevelString) => it => it.LevelString,
				nameof(LogId) => it => it.LogId,
				nameof(ReadTimeString) => it => it.ReadTimeString,
				nameof(TimeSpanString) => it => it.TimeSpanString,
				nameof(TimestampString) => it => it.TimestampString,
				_ => Log.CreatePropertyGetter<object?>(propertyName).Let(getter =>
				{
					return new Func<DisplayableLog, object?>(it => getter(it.Log));
				}),
			};
			return log =>
			{
				var rawValue = rawPropertyGetter(log);
				if (rawValue is T valueT)
					return valueT;
				if (typeof(T) == typeof(string))
					return (T)(object?)rawValue?.ToString();
				return (T)rawValue;
			};
		}
#pragma warning restore CS8600
#pragma warning restore CS8603


		/// <summary>
		/// Get ID of device which generates log.
		/// </summary>
		public IStringSource? DeviceId => this.Log.DeviceId;


		/// <summary>
		/// Get name of device which generates log.
		/// </summary>
		public IStringSource? DeviceName => this.Log.DeviceName;


		public void Dispose()
		{
#if DEBUG
			this.VerifyAccess();
#endif
			if (this.data[IsDisposedDataOffset] != 0)
				return;
			this.data[IsDisposedDataOffset] = 1;
			if (DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group))
				group.OnDisplayableLogDisposed(this);
			else
			{
				if (this.Previous != null)
					this.Previous.Next = this.Next;
				if (this.Next != null)
					this.Next.Previous = this.Previous;
				this.Previous = null;
				this.Next = null;
			}
			this.data[LogReaderLocalIdDataOffset] = 0;
		}


		/// <summary>
		/// Get ending time span of log.
		/// </summary>
		public TimeSpan? EndingTimeSpan => this.Log.EndingTimeSpan;


		/// <summary>
		/// Get ending time span of log in string format.
		/// </summary>
		public IStringSource? EndingTimeSpanString => this.FormatTimeSpan(this.Log.EndingTimeSpan);


		/// <summary>
		/// Get ending timestamp.
		/// </summary>
		public DateTime? EndingTimestamp => this.Log.EndingTimestamp;


		/// <summary>
		/// Get ending timestamp of log in string format.
		/// </summary>
		public IStringSource? EndingTimestampString => this.FormatTimestamp(this.Log.EndingTimestamp);


		/// <summary>
		/// Get event of log.
		/// </summary>
		public IStringSource? Event => this.Log.Event;


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public IStringSource? Extra1 => this.Log.Extra1;


		/// <summary>
		/// Get line count of <see cref="Extra1"/>.
		/// </summary>
		public int Extra1LineCount => this.GetExtraLineCount(1);


		/// <summary>
		/// Get 10th extra data of log.
		/// </summary>
		public IStringSource? Extra10 => this.Log.Extra10;


		/// <summary>
		/// Get line count of <see cref="Extra10"/>.
		/// </summary>
		public int Extra10LineCount => this.GetExtraLineCount(10);
		
		
		/// <summary>
		/// Get 11st extra data of log.
		/// </summary>
		public IStringSource? Extra11 => this.Log.Extra11;


		/// <summary>
		/// Get line count of <see cref="Extra11"/>.
		/// </summary>
		public int Extra11LineCount => this.GetExtraLineCount(11);
		
		
		/// <summary>
		/// Get 12nd extra data of log.
		/// </summary>
		public IStringSource? Extra12 => this.Log.Extra12;


		/// <summary>
		/// Get line count of <see cref="Extra12"/>.
		/// </summary>
		public int Extra12LineCount => this.GetExtraLineCount(12);
		
		
		/// <summary>
		/// Get 13rd extra data of log.
		/// </summary>
		public IStringSource? Extra13 => this.Log.Extra13;


		/// <summary>
		/// Get line count of <see cref="Extra13"/>.
		/// </summary>
		public int Extra13LineCount => this.GetExtraLineCount(13);
		
		
		/// <summary>
		/// Get 14th extra data of log.
		/// </summary>
		public IStringSource? Extra14 => this.Log.Extra14;


		/// <summary>
		/// Get line count of <see cref="Extra14"/>.
		/// </summary>
		public int Extra14LineCount => this.GetExtraLineCount(14);
		
		
		/// <summary>
		/// Get 15th extra data of log.
		/// </summary>
		public IStringSource? Extra15 => this.Log.Extra15;


		/// <summary>
		/// Get line count of <see cref="Extra15"/>.
		/// </summary>
		public int Extra15LineCount => this.GetExtraLineCount(15);
		
		
		/// <summary>
		/// Get 16th extra data of log.
		/// </summary>
		public IStringSource? Extra16 => this.Log.Extra16;


		/// <summary>
		/// Get line count of <see cref="Extra16"/>.
		/// </summary>
		public int Extra16LineCount => this.GetExtraLineCount(16);
		
		
		/// <summary>
		/// Get 17th extra data of log.
		/// </summary>
		public IStringSource? Extra17 => this.Log.Extra17;


		/// <summary>
		/// Get line count of <see cref="Extra17"/>.
		/// </summary>
		public int Extra17LineCount => this.GetExtraLineCount(17);
		
		
		/// <summary>
		/// Get 18th extra data of log.
		/// </summary>
		public IStringSource? Extra18 => this.Log.Extra18;


		/// <summary>
		/// Get line count of <see cref="Extra18"/>.
		/// </summary>
		public int Extra18LineCount => this.GetExtraLineCount(18);
		
		
		/// <summary>
		/// Get 19th extra data of log.
		/// </summary>
		public IStringSource? Extra19 => this.Log.Extra19;


		/// <summary>
		/// Get line count of <see cref="Extra19"/>.
		/// </summary>
		public int Extra19LineCount => this.GetExtraLineCount(19);


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public IStringSource? Extra2 => this.Log.Extra2;


		/// <summary>
		/// Get line count of <see cref="Extra2"/>.
		/// </summary>
		public int Extra2LineCount => this.GetExtraLineCount(2);
		
		
		/// <summary>
		/// Get 20th extra data of log.
		/// </summary>
		public IStringSource? Extra20 => this.Log.Extra20;


		/// <summary>
		/// Get line count of <see cref="Extra20"/>.
		/// </summary>
		public int Extra20LineCount => this.GetExtraLineCount(20);


		/// <summary>
		/// Get 3rd extra data of log.
		/// </summary>
		public IStringSource? Extra3 => this.Log.Extra3;


		/// <summary>
		/// Get line count of <see cref="Extra3"/>.
		/// </summary>
		public int Extra3LineCount => this.GetExtraLineCount(3);


		/// <summary>
		/// Get 4th extra data of log.
		/// </summary>
		public IStringSource? Extra4 => this.Log.Extra4;


		/// <summary>
		/// Get line count of <see cref="Extra4"/>.
		/// </summary>
		public int Extra4LineCount => this.GetExtraLineCount(4);


		/// <summary>
		/// Get 5th extra data of log.
		/// </summary>
		public IStringSource? Extra5 => this.Log.Extra5;


		/// <summary>
		/// Get line count of <see cref="Extra5"/>.
		/// </summary>
		public int Extra5LineCount => this.GetExtraLineCount(5);


		/// <summary>
		/// Get 6th extra data of log.
		/// </summary>
		public IStringSource? Extra6 => this.Log.Extra6;


		/// <summary>
		/// Get line count of <see cref="Extra6"/>.
		/// </summary>
		public int Extra6LineCount => this.GetExtraLineCount(6);


		/// <summary>
		/// Get 7th extra data of log.
		/// </summary>
		public IStringSource? Extra7 => this.Log.Extra7;


		/// <summary>
		/// Get line count of <see cref="Extra7"/>.
		/// </summary>
		public int Extra7LineCount => this.GetExtraLineCount(7);


		/// <summary>
		/// Get 8th extra data of log.
		/// </summary>
		public IStringSource? Extra8 => this.Log.Extra8;


		/// <summary>
		/// Get line count of <see cref="Extra8"/>.
		/// </summary>
		public int Extra8LineCount => this.GetExtraLineCount(8);


		/// <summary>
		/// Get 9th extra data of log.
		/// </summary>
		public IStringSource? Extra9 => this.Log.Extra9;


		/// <summary>
		/// Get line count of <see cref="Extra9"/>.
		/// </summary>
		public int Extra9LineCount => this.GetExtraLineCount(9);


		/// <summary>
		/// Get name of file which read log from.
		/// </summary>
		public IStringSource? FileName => this.Log.FileName;


		// Format timestamp to string.
		IStringSource? FormatTimeSpan(TimeSpan? timeSpan)
		{
			if (timeSpan == null)
				return null;
			try
			{
				var format = this.GroupOrNull?.LogProfile.TimeSpanFormatForDisplaying;
				if (format != null)
					return new SimpleStringSource(timeSpan.Value.ToString(format));
				return new SimpleStringSource(timeSpan.Value.ToString());
			}
			catch
			{
				return null;
			}
		}


		// Format timestamp to string.
		IStringSource? FormatTimestamp(DateTime? timestamp)
		{
			if (timestamp == null)
				return null;
			try
			{
				var format = this.GroupOrNull?.LogProfile.TimestampFormatForDisplaying;
				if (format != null)
					return new SimpleStringSource(timestamp.Value.ToString(format));
				return new SimpleStringSource(timestamp.Value.ToString());
			}
			catch
			{
				return null;
			}
		}


		// Get number of lines of ExtraX.
		int GetExtraLineCount(int extraNumber)
		{
			if (extraNumber <= 0 || extraNumber > Log.ExtraCapacity)
				return 0;
			if (!DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group))
				return 0;
			var index = group.LogExtraNumbers.IndexOf((byte)extraNumber);
			if (index < 0)
				return 0;
			var data = this.data;
			index += ExtraLineCountsDataOffset;
			if (data[index] == 0)
				data[index] = CalculateLineCount(extraGetters[extraNumber - 1](this.Log));
			return data[index];
		}


		// ID of group which the instance belongs to.
		uint GroupId => readUInt32Function(this.data.AsSpan(GroupIdDataOffset));


		/// <summary>
		/// Get <see cref="DisplayableLogGroup"/> which the instance belongs to.
		/// </summary>
		public DisplayableLogGroup? GroupOrNull =>
			DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group)
				? group
				: null;


		/// <summary>
		/// Check whether at least one <see cref="DisplayableLogAnalysisResult"/> has been added to this log or not.
		/// </summary>
		public bool HasAnalysisResult => this.analysisResults.IsNotEmpty();


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
		public bool HasExtraLinesOfExtra1 => this.CheckExtraLinesOfExtra(1);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra10"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra10 => this.CheckExtraLinesOfExtra(10);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra11"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra11 => this.CheckExtraLinesOfExtra(11);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra12"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra12 => this.CheckExtraLinesOfExtra(12);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra13"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra13 => this.CheckExtraLinesOfExtra(13);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra14"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra14 => this.CheckExtraLinesOfExtra(14);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra15"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra15 => this.CheckExtraLinesOfExtra(15);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra16"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra16 => this.CheckExtraLinesOfExtra(16);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra17"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra17 => this.CheckExtraLinesOfExtra(17);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra18"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra18 => this.CheckExtraLinesOfExtra(18);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra19"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra19 => this.CheckExtraLinesOfExtra(19);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra2"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra2 => this.CheckExtraLinesOfExtra(2);
		
		
		/// <summary>
		/// Check whether number of lines in <see cref="Extra20"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra20 => this.CheckExtraLinesOfExtra(20);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra3"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra3 => this.CheckExtraLinesOfExtra(3);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra4"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra4 => this.CheckExtraLinesOfExtra(4);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra5"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra5 => this.CheckExtraLinesOfExtra(5);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra6"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra6 => this.CheckExtraLinesOfExtra(6);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra7"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra7 => this.CheckExtraLinesOfExtra(7);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra8"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra8 => this.CheckExtraLinesOfExtra(8);


		/// <summary>
		/// Check whether number of lines in <see cref="Extra9"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfExtra9 => this.CheckExtraLinesOfExtra(9);


		/// <summary>
		/// Check whether number of lines in <see cref="Message"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfMessage => this.MessageLineCount > this.GroupOrNull?.MaxDisplayLineCount;


		/// <summary>
		/// Check whether number of lines in <see cref="Summary"/> is greater than <see cref="DisplayableLogGroup.MaxDisplayLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfSummary => this.SummaryLineCount > this.GroupOrNull?.MaxDisplayLineCount;


		/// <summary>
		/// Check whether the value of given property of log is frozen or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if value of property of log is frozen.</returns>
		public static bool HasFrozenProperty(string propertyName) =>
			HasProperty(propertyName) && !propertyName.EndsWith("String");


		/// <summary>
		/// Check whether given property of log with <see cref="Int64"/> value is existing or not.
		/// </summary>
		/// <param name="propertyName">Name of property.</param>
		/// <returns>True if property of log is existing.</returns>
		public static bool HasInt64Property(string propertyName) => false;



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
		public bool IsMarked => this.data[MarkedColorDataOffset] != (byte)MarkColor.None;


		/// <summary>
		/// Check whether process ID of log has been selected by user or not.
		/// </summary>
		public bool IsProcessIdSelected =>
			this.GroupOrNull?.Let(group =>
			{
				return this.Log.ProcessId.Let(it =>
					it.HasValue && it.Value == group.SelectedProcessId);
			}) ?? false;
			


		/// <summary>
		/// Check whether thread ID of log has been selected by user or not.
		/// </summary>
		public bool IsThreadIdSelected =>
			this.GroupOrNull?.Let(group =>
			{
				return this.Log.ThreadId.Let(it =>
					it.HasValue && it.Value == group.SelectedThreadId);
			}) ?? false;


		/// <summary>
		/// Get level of log.
		/// </summary>
		public LogLevel Level => this.Log.Level;


		/// <summary>
		/// Get foreground <see cref="IBrush"/> according to level of log.
		/// </summary>
		public IBrush? LevelBackgroundBrush => this.GroupOrNull?.GetLevelBackgroundBrush(this);


		/// <summary>
		/// Get foreground <see cref="IBrush"/> according to level of log.
		/// </summary>
		public IBrush? LevelForegroundBrush => this.GroupOrNull?.GetLevelForegroundBrush(this);


		/// <summary>
		/// Get foreground <see cref="IBrush"/> for pointer-over according to level of log.
		/// </summary>
		public IBrush? LevelForegroundBrushForPointerOver => this.GroupOrNull?.GetLevelForegroundBrush(this, "PointerOver");


		/// <summary>
		/// Get string representation of <see cref="Level"/>.
		/// </summary>
		public string LevelString
		{
			get
			{
				var level = this.Log.Level;
				if (level == LogLevel.Undefined || !DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group))
					return "";
				if (group.LevelMapForDisplaying.TryGetValue(level, out var s))
					return s;
				var propertyName = group.LogProfile.RawLogLevelPropertyName;
				if (propertyName != nameof(Level) && this.TryGetProperty(propertyName, out s) && s != null)
					return s;
				levelConverter ??= new(this.Application, typeof(LogLevel));
				return levelConverter.Convert<string?>(level) ?? level.ToString();
			}
		}


		/// <summary>
		/// Get line number.
		/// </summary>
		public int? LineNumber => this.Log.LineNumber;


		/// <summary>
		/// Get wrapped <see cref="Log"/>.
		/// </summary>
		public Log Log { get; }


		/// <summary>
		/// Get unique ID of log.
		/// </summary>
		public long LogId => this.Log.Id;


		/// <summary>
		/// Get log reader which reads the log.
		/// </summary>
		public LogReader LogReader
		{
			get
			{
				var localId = this.data[LogReaderLocalIdDataOffset];
				if (localId != 0 && this.GroupOrNull?.TryGetLogReaderByLocalId(localId, out var reader) == true)
					return reader;
				throw new InvalidOperationException("No log reader which is related to the log.");
			}
		}


		/// <summary>
		/// Local ID of log reader which reads the log.
		/// </summary>
		internal byte LogReaderLocalId => this.data[LogReaderLocalIdDataOffset];


		/// <summary>
		/// Get or set color of marking.
		/// </summary>
		public MarkColor MarkedColor
		{
			get => (MarkColor)this.data[MarkedColorDataOffset];
			set
			{
#if DEBUG
				this.VerifyAccess();
#endif
				if (this.data[MarkedColorDataOffset] == (byte)value)
					return;
				var isPrevMarked = this.IsMarked;
				this.data[MarkedColorDataOffset] = (byte)value;
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkedColor)));
				if (this.IsMarked != isPrevMarked)
					this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMarked)));
			}
		}


		/// <summary>
		/// Get size of memory usage by the instance in bytes.
		/// </summary>
		public long MemorySize => readUInt32Function(this.data.AsSpan(MemorySizeDataOffset));


		/// <summary>
		/// Get message of log.
		/// </summary>
		public IStringSource? Message => this.Log.Message;


		/// <summary>
		/// Get line count of <see cref="Message"/>.
		/// </summary>
		public int MessageLineCount
		{
			get
			{
				if (this.data[MessageLineCountDataOffset] == 0)
					this.data[MessageLineCountDataOffset] = CalculateLineCount(this.Log.Message);
				return this.data[MessageLineCountDataOffset];
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
			var data = this.data;
			for (var i = this.data.Length - 1; i >= ExtraLineCountsDataOffset; --i)
			{
				if (data[i] > 0)
					propertyChangedHandlers(this, new PropertyChangedEventArgs($"HasExtraLinesOfExtra{i + 1}"));
			}

			// check message line count
			if (data[MessageLineCountDataOffset] > 0)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(HasExtraLinesOfMessage)));

			// check summary line count
			if (data[SummaryLineCountDataOffset] > 0)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(HasExtraLinesOfSummary)));
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
			if (this.analysisResults.IsNotEmpty())
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
			if (propertyChangedHandlers is null)
				return;
			if (this.Log.BeginningTimeSpan.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(BeginningTimeSpanString)));
			if (this.Log.EndingTimeSpan.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(EndingTimeSpanString)));
			if (this.Log.TimeSpan.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(TimeSpanString)));
		}


		/// <summary>
		/// Called when format of displaying timestamp has been changed.
		/// </summary>
		internal void OnTimestampFormatChanged()
		{
			var propertyChangedHandlers = this.PropertyChanged;
			if (propertyChangedHandlers is null)
				return;
			if (this.Log.BeginningTimestamp.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(BeginningTimestampString)));
			if (this.Log.EndingTimestamp.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(EndingTimestampString)));
			if (this.Log.Timestamp.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(ReadTimeString)));
			if (this.Log.Timestamp.HasValue)
				propertyChangedHandlers(this, new PropertyChangedEventArgs(nameof(TimestampString)));
		}


		/// <summary>
		/// Previous tracked instance.
		/// </summary>
		internal DisplayableLog? Previous;


		/// <summary>
		/// Get ID of process which generates log.
		/// </summary>
		public int? ProcessId => this.Log.ProcessId;


		/// <summary>
		/// Get name of process which generates log.
		/// </summary>
		public IStringSource? ProcessName => this.Log.ProcessName;


		/// <summary>
		/// Get the timestamp of this log was read.
		/// </summary>
		public DateTime ReadTime => this.Log.ReadTime;


		/// <summary>
		/// Get timestamp of this log was read in string format.
		/// </summary>
		public IStringSource? ReadTimeString => this.FormatTimestamp(this.Log.ReadTime);


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
			if (this.data[IsDisposedDataOffset] != 0)
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
			var data = this.data;
			if (data[ActiveAnalysisResultTypeDataOffset] >= (byte)result.Type)
			{
				data[ActiveAnalysisResultTypeDataOffset] = (byte)(currentResultCount > 1 ? this.analysisResults[0].Type : 0);
				for (var i = currentResultCount - 2; i >= 1; --i)
				{
					var type = this.analysisResults[i].Type;
					if (data[ActiveAnalysisResultTypeDataOffset] > (byte)type)
						data[ActiveAnalysisResultTypeDataOffset] = (byte)type;
				}
				if (data[ActiveAnalysisResultTypeDataOffset] > (byte)result.Type)
					this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
				if (currentResultCount == 1)
					this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			}
			
			// update memory usage
			var memorySizeSpan = this.data.AsSpan(MemorySizeDataOffset);
			var memorySize = readUInt16Function(memorySizeSpan);
			writeUInt16Function(memorySizeSpan, (ushort)Math.Max(0, memorySize + memorySizeDiff));
			if (DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group))
			{
				group.OnAnalysisResultRemoved(this);
				group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
			}
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
			if (this.data[IsDisposedDataOffset] != 0)
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
			var data = this.data;
			var currentActiveResultType = data[ActiveAnalysisResultTypeDataOffset];
			data[ActiveAnalysisResultTypeDataOffset] = (byte)(currentResultCount > 0 ? this.analysisResults[0].Type : 0);
			for (var i = currentResultCount - 1; i >= 1; --i)
			{
				var type = this.analysisResults[i].Type;
				if (data[ActiveAnalysisResultTypeDataOffset] > (byte)type)
					data[ActiveAnalysisResultTypeDataOffset] = (byte)type;
			}
			if (data[ActiveAnalysisResultTypeDataOffset] > currentActiveResultType)
				this.PropertyChanged?.Invoke(this, new(nameof(AnalysisResultIndicatorIcon)));
			if (currentResultCount == 0)
				this.PropertyChanged?.Invoke(this, new(nameof(HasAnalysisResult)));
			
			// update memory usage
			var memorySizeSpan = this.data.AsSpan(MemorySizeDataOffset);
			var memorySize = readUInt16Function(memorySizeSpan);
			writeUInt16Function(memorySizeSpan, (ushort)Math.Max(0, memorySize + memorySizeDiff));
			if (DisplayableLogGroup.TryGetInstanceById(this.GroupId, out var group))
			{
				group.OnAnalysisResultRemoved(this);
				group.OnDisplayableLogMemorySizeChanged(memorySizeDiff);
			}
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
							// ReSharper disable EmptyGeneralCatchClause
							catch
							{ }
							// ReSharper restore EmptyGeneralCatchClause
						}
						var specificPropertyNames = new[]
						{
							nameof(BeginningTimeSpanString),
							nameof(BeginningTimestampString),
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
		public IStringSource? SourceName => this.Log.SourceName;


		/// <summary>
		/// Get summary of log.
		/// </summary>
		public IStringSource? Summary => this.Log.Summary;


		/// <summary>
		/// Get line count of <see cref="Summary"/>.
		/// </summary>
		public int SummaryLineCount
		{
			get
			{
				if (this.data[SummaryLineCountDataOffset] == 0)
					this.data[SummaryLineCountDataOffset] = CalculateLineCount(this.Log.Summary);
				return this.data[SummaryLineCountDataOffset];
			}
		}


		/// <summary>
		/// Get tags of log.
		/// </summary>
		public IStringSource? Tags => this.Log.Tags;


		/// <summary>
		/// Get definition set of text highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet? TextHighlightingDefinitionSet => this.GroupOrNull?.TextHighlightingDefinitionSet;


		/// <summary>
		/// Get ID of thread which generates log.
		/// </summary>
		public int? ThreadId => this.Log.ThreadId;


		/// <summary>
		/// Get name of thread which generates log.
		/// </summary>
		public IStringSource? ThreadName => this.Log.ThreadName;


		/// <summary>
		/// Get time span of log.
		/// </summary>
		public TimeSpan? TimeSpan => this.Log.TimeSpan;


		/// <summary>
		/// Get time span of log in string format.
		/// </summary>
		public IStringSource? TimeSpanString => this.FormatTimeSpan(this.Log.TimeSpan);


		/// <summary>
		/// Get timestamp of log.
		/// </summary>
		public DateTime? Timestamp => this.Log.Timestamp;


		/// <summary>
		/// Get timestamp of log in string format.
		/// </summary>
		public IStringSource? TimestampString => this.FormatTimestamp(this.Log.Timestamp);


		/// <summary>
		/// Get title of log.
		/// </summary>
		public IStringSource? Title => this.Log.Title;


		/// <summary>
		/// Try getting the earliest/latest timestamp from <see cref="BeginningTimestamp"/>, <see cref="EndingTimestamp"/> and <see cref="Timestamp"/>.
		/// </summary>
		/// <param name="earliestTimestamp">The earliest timestamp.</param>
		/// <param name="latestTimestamp">The latest timestamp.</param>
		/// <returns>True if the earliest/latest timestamp are valid.</returns>
		public bool TryGetEarliestAndLatestTimestamp([NotNullWhen(true)] out DateTime? earliestTimestamp, [NotNullWhen(true)] out DateTime? latestTimestamp) =>
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
			if (propertyMap.TryGetValue(propertyName, out var propertyInfo))
			{
				var rawValue = propertyInfo.GetValue(this);
				if (rawValue is T valueT)
				{
					value = valueT;
					return true;
				}
				if (typeof(T) == typeof(string))
				{
					value = (T)(object?)rawValue?.ToString();
					return true;
				}
			}
			value = default;
			return false;
		}
#pragma warning restore CS8600
#pragma warning restore CS8601


		/// <summary>
		/// Try getting the smallest/largest time span from <see cref="BeginningTimeSpan"/>, <see cref="EndingTimeSpan"/> and <see cref="TimeSpan"/>.
		/// </summary>
		/// <param name="smallestTimeSpan">The smallest time span.</param>
		/// <param name="largestTimeSpan">The largest time span.</param>
		/// <returns>True if the smallest/largest time span are valid.</returns>
		public bool TryGetSmallestAndLargestTimeSpan([NotNullWhen(true)] out TimeSpan? smallestTimeSpan, [NotNullWhen(true)] out TimeSpan? largestTimeSpan) =>
			this.Log.TryGetSmallestAndLargestTimeSpan(out smallestTimeSpan, out largestTimeSpan);


		/// <summary>
		/// Get ID of user which generates log.
		/// </summary>
		public IStringSource? UserId => this.Log.UserId;


		/// <summary>
		/// Get name of user which generates log.
		/// </summary>
		public IStringSource? UserName => this.Log.UserName;


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		IApplication IApplicationObject.Application => this.Application;
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
	}


	/// <summary>
	/// Delegate of direct event handler for <see cref="DisplayableLog"/>.
	/// </summary>
	delegate void DirectDisplayableLogEventHandler(DisplayableLogGroup group, DisplayableLog log);


	/// <summary>
	/// Color of marking of <see cref="DisplayableLog"/>.
	/// </summary>
	enum MarkColor : byte
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
