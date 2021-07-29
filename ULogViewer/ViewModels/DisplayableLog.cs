﻿using Avalonia.Media;
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
		/// <summary>
		/// Maximum line count to be displayed on UI.
		/// </summary>
		public const int MaxDisplayableLineCount = 5;


		// Fields.
		IBrush? colorIndicatorBrush;
		bool isColorIndicatorBrushSet;
		bool isLevelBrushSet;
		bool isMarked;
		IBrush? levelBrush;
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
		/// Get timestamp of log in binary format.
		/// </summary>
		public long BinaryTimestamp { get; }


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
		/// Get event of log.
		/// </summary>
		public string? Event { get => this.Log.Event; }


		/// <summary>
		/// Get 1st extra data of log.
		/// </summary>
		public string? Extra1 { get => this.Log.Extra1; }


		/// <summary>
		/// Get 2nd extra data of log.
		/// </summary>
		public string? Extra2 { get => this.Log.Extra2; }


		/// <summary>
		/// Get name of file which read log from.
		/// </summary>
		public string? FileName { get => this.Log.FileName; }


		/// <summary>
		/// Get <see cref="DisplayableLogGroup"/> which the instance belongs to.
		/// </summary>
		public DisplayableLogGroup Group { get; }


		/// <summary>
		/// Check whether number of lines in <see cref="Message"/> is greater than <see cref="MaxDisplayableLineCount"/> or not.
		/// </summary>
		public bool HasExtraLinesOfMessage { get => this.Log.MessageLineCount > MaxDisplayableLineCount; }


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
		public int MessageLineCount { get => this.Log.MessageLineCount; }


		/// <summary>
		/// Called when application string resources updated.
		/// </summary>
		internal void OnApplicationStringsUpdated()
		{ }


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
				{
					var timestamp = this.Log.Timestamp;
					var format = this.Group.LogProfile.TimestampFormatForDisplaying;
					if (timestamp == null)
						this.timestampString = "";
					else if (format != null)
						this.timestampString = timestamp.Value.ToString(format);
					else
						this.timestampString = timestamp.Value.ToString();
				}
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
