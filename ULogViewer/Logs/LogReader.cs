﻿using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Json;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Text;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CarinaStudio.Diagnostics;

namespace CarinaStudio.ULogViewer.Logs
{
	/// <summary>
	/// Log reader.
	/// </summary>
	class LogReader : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Static fields.
		static readonly long baseMemorySize = Memory.EstimateInstanceSize<LogReader>();
		static readonly CultureInfo defaultTimestampCultureInfo = CultureInfo.GetCultureInfo("en-US");
		static long nextId = 1;


		// Fields.
		readonly ScheduledAction clearLogChunkAction;
		int dropLogCount = -1;
		readonly ScheduledAction flushPendingLogsAction;
		bool isContinuousReading;
		bool isRestarting;
		bool isWaitingForDataSource;
		readonly Dictionary<string, LogLevel> logLevelMap = new();
		LogPatternMatchingMode logPatternMatchingMode = LogPatternMatchingMode.Sequential;
		IList<LogPattern> logPatterns = Array.Empty<LogPattern>();
		readonly ObservableList<Log> logs = new();
		long logsReadingCacheMemorySize;
		CancellationTokenSource? logsReadingCancellationTokenSource;
		object logsReadingToken = new();
		LogStringEncoding logStringEncoding = LogStringEncoding.Plane;
		int maxLogCount = -1;
		CancellationTokenSource? openingReaderCancellationSource;
		readonly List<Log> pendingLogs = new ();
		object? pendingLogsReadingToken;
		readonly SingleThreadSynchronizationContext pendingLogsSyncContext = new();
		LogReadingPrecondition precondition;
		bool printTraceLogs;
		IDisposable? progressiveLogsClearingToken;
		string? rawLogLevelPropertyName;
		readonly IDictionary<string, LogLevel> readOnlyLogLevelMap;
		readonly TaskFactory readingTaskFactory;
		LogReadingWindow readingWindow = LogReadingWindow.StartOfDataSource;
		TimeSpan restartReadingDelay;
		readonly ScheduledAction startReadingLogsAction;
		LogReaderState state = LogReaderState.Preparing;
		LogReaderState stateBeforeClearingLogs;
		CultureInfo timeSpanCultureInfo = defaultTimestampCultureInfo;
		LogTimeSpanEncoding timeSpanEncoding = LogTimeSpanEncoding.Custom;
		IList<string> timeSpanFormats = Array.Empty<string>();
		CultureInfo timestampCultureInfo = defaultTimestampCultureInfo;
		LogTimestampEncoding timestampEncoding = LogTimestampEncoding.Custom;
		IList<string> timestampFormats = Array.Empty<string>();
		int? updateInterval;


		/// <summary>
		/// Initialize new <see cref="LogReader"/> instance.
		/// </summary>
		/// <param name="group"><see cref="ILogGroup"/> the reader belongs to.</param>
		/// <param name="dataSource"><see cref="ILogDataSource"/> to read log data from.</param>
		/// <param name="readingTaskFactory"><see cref="TaskFactory"/> to perform logs reading tasks.</param>
		public LogReader(ILogGroup? group, ILogDataSource dataSource, TaskFactory readingTaskFactory)
		{
			// check thread
			dataSource.VerifyAccess();

			// setup properties
			this.Application = (IULogViewerApplication)dataSource.Application;
			this.DataSource = dataSource;
			this.Id = nextId++;
			this.Logger = dataSource.Application.LoggerFactory.CreateLogger($"{this.GetType().Name}-{this.Id}");
			this.LogGroup = group;
			this.Logs = new ReadOnlyObservableList<Log>(this.logs);
			this.printTraceLogs = this.Application.IsDebugMode;
			this.readingTaskFactory = readingTaskFactory;
			this.readOnlyLogLevelMap = new ReadOnlyDictionary<string, LogLevel>(this.logLevelMap);

			// create scheduled actions
			this.clearLogChunkAction = new(() =>
			{
				if (this.state != LogReaderState.ClearingLogs)
					return;
				var chunkSize = Math.Max(128, this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.ProgressiveLogClearingChunkSize));
				if (this.logs.Count <= chunkSize)
				{
					this.logs.RemoveRange(0, this.logs.Count);
					if (this.Application.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy) != MemoryUsagePolicy.BetterPerformance)
						this.logs.TrimExcess();
					this.OnLogsClearingCompleted();
				}
				else
				{
					this.logs.RemoveRange(this.logs.Count - chunkSize, chunkSize);
					this.clearLogChunkAction?.Schedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.ProgressiveLogClearingInterval));
				}
			});
			this.flushPendingLogsAction = new ScheduledAction(this.pendingLogsSyncContext, () =>
			{
				if (this.pendingLogs.IsEmpty())
					return;
				var token = this.pendingLogsReadingToken;
				if (token == null)
				{
					this.pendingLogs.Clear();
					return;
				}
				var logs = this.pendingLogs.ToArray();
				this.pendingLogs.Clear();
				this.SynchronizationContext.Post(() => this.OnLogsRead(token, logs, this.logsReadingCacheMemorySize));
			});
			this.startReadingLogsAction = new ScheduledAction(this.StartReadingLogs);

			// attach to data source
			dataSource.PropertyChanged += this.OnDataSourcePropertyChanged;

			// attach to log list
			this.logs.CollectionChanged += (_, e) => this.LogsChanged?.Invoke(this, e);

			this.Logger.LogDebug("Create with data source: {dataSource}", dataSource);
		}


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }


		// Whether read logs can be added or not.
		bool CanAddLogs => this.state switch
		{
			LogReaderState.Starting => true,
			LogReaderState.ReadingLogs => true,
			_ => false,
		};


		// Change state.
		bool ChangeState(LogReaderState state)
		{
			// check state
			var prevState = this.state;
			if (prevState == state)
				return true;

			// change state
			this.state = state;
			this.Logger.LogDebug("Change state from {prevState} to {state}", prevState, state);
			this.OnPropertyChanged(nameof(State));

			// update data source waiting state
			switch (this.state)
			{
				case LogReaderState.Starting:
				case LogReaderState.StartingWhenPaused:
					this.IsWaitingForDataSource = true;
					break;
				case LogReaderState.DataSourceError:
				case LogReaderState.UnclassifiedError:
					this.IsWaitingForDataSource = false;
					break;
			}

			// check final state
			return (this.state == state);
		}


		/// <summary>
		/// Clear all read logs.
		/// </summary>
		/// <param name="progressive">True to clear logs progressively.</param>
		public void ClearLogs(bool progressive = false)
		{
			// check state
			this.VerifyAccess();
			if (this.IsDisposed)
				return;
			if (this.state == LogReaderState.Preparing)
				return;
			if (this.state == LogReaderState.ClearingLogs)
			{
				if (this.clearLogChunkAction.IsScheduled == progressive)
					return;
			}

			// change state
			if (this.state != LogReaderState.ClearingLogs)
				this.stateBeforeClearingLogs = this.state;
			this.ChangeState(LogReaderState.ClearingLogs);

			// cancel reading logs
			if (!this.isContinuousReading)
			{
				this.logsReadingCancellationTokenSource?.Let(it =>
				{
					it.Cancel();
					it.Dispose();
					this.logsReadingCancellationTokenSource = null;
				});
			}

			// drop pending logs
			this.pendingLogsSyncContext.Post(() =>
			{
				this.pendingLogs.Clear();
				this.flushPendingLogsAction.Cancel();
			});

			// start clearing logs
			if (progressive)
			{
				if (this.LogGroup != null)
				{
					this.Logger.LogDebug("Schedule progressive logs clearing");
					this.progressiveLogsClearingToken = this.LogGroup.ScheduleProgressiveLogsRemoving(() =>
					{
						if (this.state == LogReaderState.ClearingLogs)
						{
							this.Logger.LogDebug("Start progressive logs clearing");
							this.clearLogChunkAction.Schedule();
							return true;
						}
						return false;
					});
				}
				else
				{
					this.Logger.LogDebug("Start progressive logs clearing");
					this.clearLogChunkAction.Schedule();
				}
			}
			else
			{
				this.clearLogChunkAction.Cancel();
				this.progressiveLogsClearingToken = this.progressiveLogsClearingToken.DisposeAndReturnNull();
				this.logs.Clear();
				if (this.Application.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy) != MemoryUsagePolicy.BetterPerformance)
					this.logs.TrimExcess();
				this.OnLogsClearingCompleted();
			}
		}


		// Create date time from unix timestamp.
		static DateTime CreateDateTimeFromUnixTimestamp(double timestamp) =>
			new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timestamp).ToLocalTime();
		static DateTime CreateDateTimeFromUnixTimestampMillis(double timestampMillis) =>
			new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(timestampMillis).ToLocalTime();


		/// <summary>
		/// Get <see cref="ILogDataSource"/> to read log data from.
		/// </summary>
		public ILogDataSource DataSource { get; }


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// check state.
			if (disposing)
				this.VerifyAccess();
			else
			{
				this.pendingLogsSyncContext.Dispose();
				return; // ignore releasing managed resources
			}

			// cancel clearing logs
			this.clearLogChunkAction.Cancel();

			// cancel restarting
			this.isRestarting = false;

			// change state
			this.ChangeState(LogReaderState.Disposed);

			// detach from data source
			this.DataSource.PropertyChanged -= this.OnDataSourcePropertyChanged;

			// cancel opening reader
			this.openingReaderCancellationSource?.Let(it =>
			{
				this.Logger.LogWarning("Cancel opening reader because of disposing");
				it.Cancel();
				it.Dispose();
				this.openingReaderCancellationSource = null;
			});

			// cancel reading logs
			this.logsReadingCancellationTokenSource?.Let(it =>
			{
				this.Logger.LogWarning("Cancel reading logs because of disposing");
				it.Cancel();
				it.Dispose();
				this.logsReadingCancellationTokenSource = null;
			});
			this.pendingLogsSyncContext.Send(() =>
			{
				this.pendingLogs.Clear();
				this.flushPendingLogsAction.Cancel();
			});

			// clear logs
			this.logs.Clear();
			this.logs.TrimExcess();

			// dispose pending logs waiting thread
			this.pendingLogsSyncContext.Dispose();
		}


		/// <summary>
		/// Get or set number of logs to drop when number of logs reaches <see cref="MaxLogCount"/>.
		/// </summary>
		/// <remarks>Negative value means dropping 10% of read logs.</remarks>
		public int DropLogCount
		{
			get => this.dropLogCount;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (value == 0)
					throw new ArgumentOutOfRangeException(nameof(value));
				if (this.dropLogCount == value)
					return;
				this.dropLogCount = value;
				this.OnPropertyChanged(nameof(DropLogCount));
			}
		}


		// Drop exceeded logs.
		void DropLogs(int addingLogCount)
		{
			// prepare dropping information
			if (this.maxLogCount < 0)
				return;
			var logs = this.logs;
			var logCount = logs.Count + addingLogCount;
			var droppingLogCount = (logCount - this.maxLogCount);
			if (droppingLogCount <= 0)
				return;
			if (this.dropLogCount < 0)
				droppingLogCount += (this.maxLogCount / 10);
			else
				droppingLogCount += this.dropLogCount;

			// drop logs
			if (droppingLogCount < logs.Count)
				logs.RemoveRange(0, droppingLogCount);
			else
				logs.Clear();
		}


		/// <summary>
		/// Get unique ID of this <see cref="LogReader"/> instance.
		/// </summary>
		public long Id { get; }


		/// <summary>
		/// Get or set whether restart reading is needed when reaching end of log data.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public bool IsContinuousReading
		{
			get => this.isContinuousReading;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(IsContinuousReading)} when state is {this.state}.");
				if (this.isContinuousReading == value)
					return;
				this.isContinuousReading = value;
				this.OnPropertyChanged(nameof(IsContinuousReading));
			}
		}


		/// <summary>
		/// Check whether instance is restarting reading logs or not.
		/// </summary>
		public bool IsRestarting 
		{
			get => this.isRestarting;
			private set
			{
				if (this.isRestarting == value)
					return;
				this.isRestarting = value;
				this.OnPropertyChanged(nameof(IsRestarting));
			}
		}


		/// <summary>
		/// Check whether instance is waiting for <see cref="DataSource"/> to get first log data or not.
		/// </summary>
		public bool IsWaitingForDataSource 
		{
			get => this.isWaitingForDataSource;
			private set
			{
				if (this.isWaitingForDataSource == value)
					return;
				this.isWaitingForDataSource = value;
				this.OnPropertyChanged(nameof(IsWaitingForDataSource));
			}
		}
		
		
		/// <summary>
		/// Get <see cref="ILogGroup"/> the reader belongs to.
		/// </summary>
		public ILogGroup? LogGroup { get; }
		
		
		/// <summary>
		/// Get or set mode of matching raw log lines by patterns.
		/// </summary>
		public LogPatternMatchingMode LogPatternMatchingMode
		{
			get => this.logPatternMatchingMode;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogPatternMatchingMode)} when state is {this.state}.");
				if (this.logPatternMatchingMode == value)
					return;
				this.logPatternMatchingMode = value;
				this.OnPropertyChanged(nameof(LogPatternMatchingMode));
			}
		}


		/// <summary>
		/// Get or set log patterns to parse log data.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public IList<LogPattern> LogPatterns 
		{
			get => this.logPatterns;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogPatterns)} when state is {this.state}.");
				if (this.logPatterns.SequenceEqual(value))
					return;
				this.logPatterns = value.ToList().AsReadOnly();
				this.OnPropertyChanged(nameof(LogPatterns));
			}
		}


		/// <summary>
		/// Get list of read <see cref="Log"/>s.
		/// </summary>
		/// <remarks>The list implements <see cref="INotifyCollectionChanged"/> interface.</remarks>
		public IList<Log> Logs { get; }


		/// <summary>
		/// Raised when elements in <see cref="Logs"/> has been changed.
		/// </summary>
		public event NotifyCollectionChangedEventHandler? LogsChanged;


		/// <summary>
		/// Get or set string encoding of log.
		/// </summary>
		/// <remarks>The property can be set ONLY when state is <see cref="LogReaderState.Preparing"/>.</remarks>
		public LogStringEncoding LogStringEncoding
		{
			get => this.logStringEncoding;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogStringEncoding)} when state is {this.state}.");
				if (this.logStringEncoding == value)
					return;
				this.logStringEncoding = value;
				this.OnPropertyChanged(nameof(LogStringEncoding));
			}
		}


		/// <summary>
		/// Get logger.
		/// </summary>
		protected ILogger Logger { get; }


		/// <summary>
		/// Get or set <see cref="IDictionary{TKey, TValue}"/> to map from string to <see cref="LogLevel"/>.
		/// </summary>
		public IDictionary<string, LogLevel> LogLevelMap
		{
			get => this.readOnlyLogLevelMap;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(LogLevelMap)} when state is {this.state}.");
				if (this.logLevelMap.Equals(value))
					return;
				this.logLevelMap.Clear();
				foreach (var pair in value)
					this.logLevelMap.Add(pair.Key, pair.Value);
				this.OnPropertyChanged(nameof(LogLevelMap));
			}
		}


		/// <summary>
		/// Get or set maximum number of logs to read. Logs read earlier will be dropped if number of logs reaches <see cref="MaxLogCount"/>.
		/// </summary>
		/// <remarks>Negative number means unlimited number of logs.</remarks>
		public int MaxLogCount
		{
			get => this.maxLogCount;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (value == 0)
					throw new ArgumentOutOfRangeException(nameof(value));
				if (this.maxLogCount == value)
					return;
				this.maxLogCount = value;
				this.DropLogs(0);
				this.OnPropertyChanged(nameof(MaxLogCount));
			}
		}


		/// <summary>
		/// Get memory size used by the instance.
		/// </summary>
		public long MemorySize => baseMemorySize
		                          + Memory.EstimateCollectionInstanceSize<Log>(this.logs.Capacity)
		                          + Memory.EstimateCollectionInstanceSize<Log>(this.pendingLogs.Capacity)
		                          + this.logsReadingCacheMemorySize;


		// Called when property of data source has been changed.
		void OnDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e) => this.OnDataSourcePropertyChanged(e);


		/// <summary>
		/// Called when property of <see cref="DataSource"/> has been changed.
		/// </summary>
		/// <param name="e">Event data.</param>
		protected virtual void OnDataSourcePropertyChanged(PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataSource.State))
				this.OnDataSourceStateChanged(this.DataSource.State);
		}


		/// <summary>
		/// Called when state of <see cref="DataSource"/> has been changed.
		/// </summary>
		/// <param name="state">New state.</param>
		protected virtual void OnDataSourceStateChanged(LogDataSourceState state)
		{
			switch(state)
			{
				case LogDataSourceState.ReadyToOpenReader:
					switch (this.state)
					{
						case LogReaderState.Starting:
						case LogReaderState.StartingWhenPaused:
							this.Logger.LogWarning("Data source is ready to open reader, start reading logs");
							this.startReadingLogsAction.Schedule();
							break;
					}
					break;
				case LogDataSourceState.OpeningReader:
				case LogDataSourceState.ReaderOpened:
				case LogDataSourceState.ClosingReader:
					break;
				default:
					switch (this.state)
					{
						case LogReaderState.Starting:
						case LogReaderState.StartingWhenPaused:
						case LogReaderState.ReadingLogs:
						case LogReaderState.Paused:
						case LogReaderState.Stopping:
							if (this.DataSource.IsErrorState())
							{
								this.Logger.LogWarning("Data source state changed to {state}, cancel reading logs", state);
								this.OnLogsReadingCompleted(null, this.logsReadingCacheMemorySize);
							}
							break;
					}
					break;
			}
		}


		// Called when first raw log data read.
		void OnFirstRawLogDataRead(object readingToken)
		{
			if (this.logsReadingToken != readingToken)
				return;
			this.IsWaitingForDataSource = false;
		}


		// Called when clearing logs completed.
		void OnLogsClearingCompleted()
		{
			if (this.state != LogReaderState.ClearingLogs)
				return;
			this.logsReadingCacheMemorySize = 0;
			this.progressiveLogsClearingToken = this.progressiveLogsClearingToken.DisposeAndReturnNull();
			if (this.isRestarting)
			{
				this.Logger.LogWarning("Logs cleared, start reading logs");
				this.Start(false);
			}
			else if (this.isContinuousReading)
				this.ChangeState(this.stateBeforeClearingLogs);
			else
			{
				this.IsWaitingForDataSource = false;
				this.ChangeState(LogReaderState.Stopped);
			}
		}


		// Called when logs read.
		void OnLogsRead(object readingToken, ICollection<Log> readLogs, long cacheByteCount)
		{
			if (!this.CanAddLogs || this.logsReadingToken != readingToken)
				return;
			this.DropLogs(readLogs.Count);
			this.logs.AddRange(readLogs);
			this.DropLogs(0);
			this.logsReadingCacheMemorySize = cacheByteCount;
		}


		// Called when all logs read.
		void OnLogsReadingCompleted(Exception? ex, long cacheByteCount)
		{
			// check state
			switch(this.state)
			{
				case LogReaderState.Starting:
				case LogReaderState.StartingWhenPaused:
				case LogReaderState.ReadingLogs:
				case LogReaderState.Paused:
				case LogReaderState.Stopping:
					break;
				default:
					return;
			}

			this.Logger.LogWarning("Complete reading logs");
			
			// update cache state
			this.logsReadingCacheMemorySize = cacheByteCount;

			// update data source waiting state
			this.IsWaitingForDataSource = false;

			// release cancellation token
			this.logsReadingCancellationTokenSource?.Let(it =>
			{
				it.Cancel();
				it.Dispose();
				this.logsReadingCancellationTokenSource = null;
			});

			// flush pending logs
			if (this.IsContinuousReading)
			{
				var readingToken = (object?)null;
				var logs = Array.Empty<Log>();
				this.pendingLogsSyncContext.Send(() =>
				{
					this.flushPendingLogsAction.Cancel();
					if (this.pendingLogs.IsEmpty())
						return;
					readingToken = this.pendingLogsReadingToken;
					logs = this.pendingLogs.ToArray();
					this.pendingLogs.Clear();
				});
				if (readingToken != null && logs.IsNotEmpty())
					this.OnLogsRead(readingToken, logs, this.logsReadingCacheMemorySize);
			}

			// change state
			if (!this.isContinuousReading || this.state == LogReaderState.Stopping)
			{
				this.ChangeState(LogReaderState.Stopping);
				if (this.DataSource.IsErrorState())
				{
					this.Logger.LogError("Data source state is {state} when completing reading logs", this.DataSource.State);
					this.ChangeState(LogReaderState.DataSourceError);
				}
				else if (ex is null)
					this.ChangeState(LogReaderState.Stopped);
				else
					this.ChangeState(LogReaderState.UnclassifiedError);
				return;
			}

			// restart reading
			var delayMillis = (long)this.restartReadingDelay.TotalMilliseconds;
			if (this.isContinuousReading && this.logs.IsEmpty())
				delayMillis = Math.Max(delayMillis, this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.RestartContinuousLogReadingWhenNoLogReadDelay));
			if (delayMillis > 0)
			{
				this.Logger.LogWarning("Restart reading logs {ms} ms later", delayMillis);
				this.startReadingLogsAction.Reschedule((int)delayMillis);
			}
			else
			{
				this.Logger.LogWarning("Restart reading logs");
				this.startReadingLogsAction.Reschedule();
			}
		}


		/// <summary>
		/// Raise <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of changed property.</param>
		protected virtual void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		/// <summary>
		/// Pause reading logs.
		/// </summary>
		/// <returns>True if logs reading has been paused successfully.</returns>
		public bool Pause()
		{
			this.VerifyAccess();
			switch (this.state)
			{
				case LogReaderState.StartingWhenPaused:
				case LogReaderState.Paused:
					return true;
				case LogReaderState.Starting:
					if (!this.isContinuousReading)
					{
						this.Logger.LogWarning($"Cannot pause logs reading when {nameof(IsContinuousReading)} is false");
						return false;
					}
					return this.ChangeState(LogReaderState.StartingWhenPaused);
				case LogReaderState.ReadingLogs:
					if (!this.isContinuousReading)
					{
						this.Logger.LogWarning($"Cannot pause logs reading when {nameof(IsContinuousReading)} is false");
						return false;
					}
					return this.ChangeState(LogReaderState.Paused);
				default:
					this.Logger.LogWarning("Cannot pause logs reading when state is {state}", this.state);
					return false;
			}
		}


		/// <summary>
		/// Get or set precondition of reading logs.
		/// </summary>
		public LogReadingPrecondition Precondition
		{
			get => this.precondition;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.precondition == value)
					return;
				this.precondition = value;
				this.OnPropertyChanged(nameof(Precondition));
			}
		}


		/// <summary>
		/// Get or set name of property which represents raw (unmapped) level of log.
		/// </summary>
		public string? RawLogLevelPropertyName
		{
			get => this.rawLogLevelPropertyName;
			set
			{
				this.VerifyAccess();
				if (this.state != LogReaderState.Preparing)
					throw new InvalidOperationException($"Cannot change {nameof(RawLogLevelPropertyName)} when state is {this.state}.");
				if (this.rawLogLevelPropertyName == value)
					return;
				this.rawLogLevelPropertyName = value;
				this.OnPropertyChanged(nameof(RawLogLevelPropertyName));
			}
		}


		/// <summary>
		/// Get or set window of reading logs from data source.
		/// </summary>
		public LogReadingWindow ReadingWindow
		{
			get => this.readingWindow;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.readingWindow == value)
					return;
				this.readingWindow = value;
				this.OnPropertyChanged(nameof(ReadingWindow));
			}
		}


		// Read single line of log.
		unsafe void ReadLog(LogBuilder logBuilder, Match match, string[]? timeSpanFormats, string[]? timestampFormats)
		{
#pragma warning disable IDE0220
			foreach (Group group in match.Groups)
#pragma warning restore IDE0220
			{
				if (!group.Success)
					continue;
				var name = group.Name;
				var isAnonymousGroup = name.Pin(p =>
				{
					if (p is null)
						return true;
					var cPtr = p + name.Length - 1;
					while (cPtr >= p)
					{
						if (!char.IsDigit(*cPtr))
							return false;
						--cPtr;
					}
					return true;
				});
				if (isAnonymousGroup)
					continue;
				var value = group.Value.Let(it =>
				{
					if (it.Length == 0)
						return it;
					return this.logStringEncoding switch
					{
						LogStringEncoding.Json => JsonUtility.DecodeFromJsonString(it.TrimEnd()),
						LogStringEncoding.Xml => WebUtility.HtmlDecode(it.TrimEnd()),
						_ => it.TrimEnd(),
					};
				});
				if (Log.HasMultiLineStringProperty(name))
				{
					foreach (Capture capture in group.Captures)
						logBuilder.AppendToNextLine(name, capture.Value);
				}
				else if (Log.HasStringProperty(name))
					logBuilder.Set(name, value);
				else if (Log.HasDateTimeProperty(name))
				{
					switch (this.timestampEncoding)
					{
						case LogTimestampEncoding.Custom:
							if (timestampFormats != null)
							{
								var parsed = false;
								for (var i = timestampFormats.Length - 1; i >= 0; --i)
								{
									if (DateTime.TryParseExact(value, timestampFormats[i], this.timestampCultureInfo, DateTimeStyles.None, out var timestamp))
									{
										parsed = true;
										logBuilder.Set(name, timestamp.ToBinary().ToString());
										break;
									}
								}
								if (!parsed && this.Application.IsDebugMode)
									this.Logger.LogWarning("Unable to parse '{value}' as date time with custom formats: {timestampFormats}", value, timestampFormats.ContentToString());
							}
							else
								logBuilder.Set(name, value);
							break;
						case LogTimestampEncoding.Unix:
							if (double.TryParse(value, out var sec))
								logBuilder.Set(name, CreateDateTimeFromUnixTimestamp(sec).ToBinary().ToString());
							break;
						case LogTimestampEncoding.UnixMicroseconds:
							if (double.TryParse(value, out var us))
								logBuilder.Set(name, CreateDateTimeFromUnixTimestampMillis(us / 1000).ToBinary().ToString());
							break;
						case LogTimestampEncoding.UnixMilliseconds:
							if (double.TryParse(value, out var ms))
								logBuilder.Set(name, CreateDateTimeFromUnixTimestampMillis(ms).ToBinary().ToString());
							break;
					}
				}
				else if (Log.HasTimeSpanProperty(name))
				{
					switch (this.timeSpanEncoding)
					{
						case LogTimeSpanEncoding.Custom:
							{
								if (timeSpanFormats != null)
								{
									for (var i = timeSpanFormats.Length - 1; i >= 0; --i)
									{
										if (TimeSpan.TryParseExact(value, timeSpanFormats[i], this.timeSpanCultureInfo, TimeSpanStyles.None, out var timeSpan))
										{
											logBuilder.Set(name, timeSpan.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
											break;
										}
									}
								}
								else if (TimeSpan.TryParse(value, out var timeSpan))
									logBuilder.Set(name, timeSpan.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
							}
							break;
						case LogTimeSpanEncoding.TotalDays:
							{
								if (double.TryParse(value, out var days) && double.IsFinite(days))
									logBuilder.Set(name, TimeSpan.FromDays(days).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
								break;
							}
						case LogTimeSpanEncoding.TotalHours:
							{
								if (double.TryParse(value, out var hours) && double.IsFinite(hours))
									logBuilder.Set(name, TimeSpan.FromHours(hours).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
								break;
							}
						case LogTimeSpanEncoding.TotalMicroseconds:
							{
								if (double.TryParse(value, out var us))
									logBuilder.Set(name, TimeSpan.FromMilliseconds(us / 1000).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
								break;
							}
						case LogTimeSpanEncoding.TotalMilliseconds:
							{
								if (double.TryParse(value, out var ms))
									logBuilder.Set(name, TimeSpan.FromMilliseconds(ms).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
								break;
							}
						case LogTimeSpanEncoding.TotalMinutes:
							{
								if (double.TryParse(value, out var mins) && double.IsFinite(mins))
									logBuilder.Set(name, TimeSpan.FromMinutes(mins).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
								break;
							}
						case LogTimeSpanEncoding.TotalSeconds:
							{
								if (double.TryParse(value, out var sec) && double.IsFinite(sec))
									logBuilder.Set(name, TimeSpan.FromSeconds(sec).TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
								break;
							}
						default:
							{
								if (TimeSpan.TryParse(value, out var timeSpan))
									logBuilder.Set(name, timeSpan.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
							}
							break;
					}
				}
				else
					logBuilder.Set(name, value);
				if ((this.rawLogLevelPropertyName == null && name == nameof(Log.Level))
					|| this.rawLogLevelPropertyName == name)
				{
					if (this.logLevelMap.TryGetValue(value, out var level))
						logBuilder.Set(nameof(Log.Level), level.ToString());
				}
			}
		}


		// Read logs.
		void ReadLogs(object readingToken, TextReader reader, CancellationToken cancellationToken)
		{
			// check state
			if (cancellationToken.IsCancellationRequested)
			{
				this.Logger.LogWarning("Logs reading was cancelled before starting reading");
				Global.RunWithoutError(reader.Close);
				this.SynchronizationContext.Post(() => this.OnLogsReadingCompleted(null, 0L));
				return;
			}
			
			this.Logger.LogDebug("Start reading logs in background");
			
			// get state
			var configuration = this.Application.Configuration;
			var readLogs = new List<Log>();
			var readLog = (Log?)null;
			var precondition = this.precondition;
			var hasPrecondition = !precondition.IsEmpty;
			var isContinuousReading = this.isContinuousReading;
			var maxLogCount = isContinuousReading ? -1 : this.maxLogCount;
			var hasMaxLogCount = maxLogCount >= 0;
			var logCount = 0;
			var accumulatedLogCount = 0;
			var readingWindow = this.readingWindow;
			var logPatternMatchingMode = this.logPatternMatchingMode;
			var logPatterns = this.logPatterns.ToArray().Also(it =>
			{
				if (this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.UseCompiledRegex))
				{
					for (var i = it.Length - 1; i >= 0; --i)
					{
						var logPattern = it[i];
						if ((logPattern.Regex.Options & RegexOptions.Compiled) == 0)
						{
							var regex = new Regex(logPattern.Regex.ToString(), logPattern.Regex.Options | RegexOptions.Compiled);
							it[i] = new(regex, logPattern.IsRepeatable, logPattern.IsSkippable, null);
						}
					}
				}
			});
			var syncContext = this.SynchronizationContext;
			var stringSourceCache = new StringSourceCache
			{
				MaxByteCount = Math.Max(1, this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogReadingStringCacheSizeInMB)) << 20,
			};
			var dataSourceOptions = this.DataSource.CreationOptions;
			var isReadingFromFile = dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName));
			var fileName = dataSourceOptions.FileName.Let(it =>
			{
				if (it is null)
					return IStringSource.Empty;
				return new CompressedStringSource(it);
			});
			var logBuilder = new LogBuilder()
			{
				MemoryUsagePolicy = this.Application.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy),
				StringCache = stringSourceCache,
			};
			var timeSpanFormats = this.timeSpanFormats.IsNotEmpty() ? this.timeSpanFormats.ToArray() : null;
			var timestampFormats = this.timestampFormats.IsNotEmpty() ? this.timestampFormats.ToArray() : null;
			var exception = (Exception?)null;
			var defaultNonContinuousUpdateInterval = configuration.GetValueOrDefault(ConfigurationKeys.NonContinuousLogsReadingUpdateInterval);
			var nonContinuousChunkSize = configuration.GetValueOrDefault(ConfigurationKeys.NonContinuousLogsReadingUpdateChunkSize);
			var nonContinuousPaddingInterval = configuration.GetValueOrDefault(ConfigurationKeys.NonContinuousLogsReadingPaddingInterval);
			var printTraceLogs = this.printTraceLogs;

			// read logs
			var stopWatch = new Stopwatch().Also(it => it.Start());
			var readLineSyncLock = new object();
			try
			{
				// prepare
				if (this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.ReadRawLogLinesConcurrently))
				{
					this.Logger.LogDebug("Use concurrent reader");
					reader = new IO.ConcurrentTextReader(this.Application, reader);
				}
				var prevLogPattern = (LogPattern?)null;
				var logPatternIndex = 0;
				var logPattern = logPatterns[0];
				var lastLogPatternIndex = (logPatterns.Length - 1);
				var lineNumber = 0;
				var startReadingTime = stopWatch.ElapsedMilliseconds;
				int updateInterval;
				string? logLine;
				
				// prepare local functions
				void FlushContinuousReadingLog(int updateInterval)
				{
					var log = readLog;
					if (log != null)
					{
						readLog = null;
						var stringSourceCacheByteCount = stringSourceCache.ByteCount;
						this.pendingLogsSyncContext.Post(() =>
						{
							if (this.pendingLogsReadingToken != readingToken)
							{
								this.pendingLogsReadingToken = readingToken;
								this.pendingLogs.Clear();
							}
							this.logsReadingCacheMemorySize = stringSourceCacheByteCount;
							this.pendingLogs.Add(log);
							this.flushPendingLogsAction.Schedule(updateInterval);
						});
					}
				}
				void ReadNextLineSkippable()
				{
					if (logPatterns[logPatternIndex].IsSkippable)
					{
						lock (readLineSyncLock)
						{
							ThreadPool.QueueUserWorkItem(_ =>
							{
								try
								{
									logLine = reader.ReadLine();
								}
								// ReSharper disable EmptyGeneralCatchClause
								catch
								{ }
								// ReSharper restore EmptyGeneralCatchClause
								lock (readLineSyncLock)
								{
									Monitor.PulseAll(readLineSyncLock);
								}
							});
							if (Monitor.Wait(readLineSyncLock, 5000))
								++lineNumber;
							else
							{
								this.Logger.LogTrace("No line read, skip pattern {index}/{t}: '{pattern}'", logPatternIndex, lastLogPatternIndex, logPatterns[logPatternIndex].Regex);

								// skip all skippable patterns because of no line read
								while (logPatternIndex <= lastLogPatternIndex)
								{
									++logPatternIndex;
									if (logPatternIndex > lastLogPatternIndex)
										break;
									if (printTraceLogs)
										this.Logger.LogTrace("Move to pattern {index}/{total}: '{pattern}'", logPatternIndex, lastLogPatternIndex, logPatterns[logPatternIndex].Regex);
									if (!logPatterns[logPatternIndex].IsSkippable)
										break;
								}

								// create log
								if (logPatternIndex >= lastLogPatternIndex)
								{
									if (logBuilder.IsNotEmpty())
									{
										readLog = logBuilder.BuildAndReset();
										if (!hasPrecondition || precondition.Matches(readLog))
										{
											if (isContinuousReading)
												FlushContinuousReadingLog(updateInterval);
											else
											{
												readLogs.Add(readLog);
												++logCount;
												++accumulatedLogCount;
											}
										}
									}
									logPatternIndex = 0;
									if (printTraceLogs)
										this.Logger.LogTrace("Reset to pattern 0/{total}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
								}

								// wait for reading line
								Monitor.Wait(readLineSyncLock);
								++lineNumber;
							}
						}
					}
					else
						logLine = this.ReadNextLine(reader, ref lineNumber);
				}
				
				// read first line
				logLine = ReadNextLine(reader, ref lineNumber);
				if (cancellationToken.IsCancellationRequested)
				{
					this.Logger.LogWarning("Logs reading has been cancelled");
					return;
				}
				if (logLine == null)
				{
					this.Logger.LogWarning("No raw log line read");
					return;
				}
				if (printTraceLogs)
					this.Logger.LogTrace("Start with matching mode {mode}, pattern 0/{t}: '{pattern}'", logPatternMatchingMode, lastLogPatternIndex, logPatterns[0].Regex);
				this.SynchronizationContext.Post(() => this.OnFirstRawLogDataRead(readingToken));
				
				// read logs
				if (logPatterns.Length == 1 && !logPattern.IsRepeatable && !logPattern.IsSkippable) // special case for single pattern
				{
					var logPatternRegex = logPattern.Regex;
					do
					{
						if (hasMaxLogCount && readingWindow == LogReadingWindow.StartOfDataSource && logCount >= maxLogCount)
							break;
						updateInterval = this.updateInterval ?? (isContinuousReading ? configuration.GetValueOrDefault(ConfigurationKeys.ContinuousLogsReadingUpdateInterval) : defaultNonContinuousUpdateInterval);
						try
						{
							var match = logPatternRegex.Match(logLine);
							if (match.Success)
							{
								// read log
								this.ReadLog(logBuilder, match, timeSpanFormats, timestampFormats);

								// set file name and line number
								if (isReadingFromFile)
								{
									logBuilder.Set(nameof(Log.LineNumber), lineNumber.ToString());
									logBuilder.Set(nameof(Log.FileName), fileName);
								}

								// create log
								if (logBuilder.IsNotEmpty())
								{
									readLog = logBuilder.BuildAndReset();
									if (!hasPrecondition || precondition.Matches(readLog))
									{
										if (isContinuousReading)
											FlushContinuousReadingLog(updateInterval);
										else
										{
											readLogs.Add(readLog);
											++logCount;
											++accumulatedLogCount;
										}
									}
								}
							}
							else if (printTraceLogs)
								this.Logger.LogTrace("'{logLine}' Cannot be matched by pattern {index}/{t}: '{logPattern}'", logLine, logPatternIndex, lastLogPatternIndex, logPattern.Regex);
							logLine = ReadNextLine(reader, ref lineNumber);
						}
						finally
						{
							if (isContinuousReading)
								FlushContinuousReadingLog(updateInterval);
							else if (accumulatedLogCount >= nonContinuousChunkSize || (stopWatch.ElapsedMilliseconds - startReadingTime) >= updateInterval)
							{
								if (!hasMaxLogCount || readingWindow == LogReadingWindow.StartOfDataSource)
								{
									if (nonContinuousPaddingInterval > 0)
										Thread.Sleep(nonContinuousPaddingInterval);
									var logArray = readLogs.ToArray();
									readLogs.Clear();
									syncContext.Post(() => this.OnLogsRead(readingToken, logArray, stringSourceCache.ByteCount));
								}
								else if (readLogs.Count > maxLogCount)
									readLogs.RemoveRange(0, readLogs.Count - maxLogCount);
								accumulatedLogCount = 0;
								startReadingTime = stopWatch.ElapsedMilliseconds;
							}
						}
					} while (logLine != null && !cancellationToken.IsCancellationRequested);
				}
				else if (logPatternMatchingMode == LogPatternMatchingMode.Sequential) // general case (use 1 or more patterns sequentially)
				{
					// read logs
					var numberOfSkippedLogPatterns = 0;
					do
					{
						if (hasMaxLogCount && readingWindow == LogReadingWindow.StartOfDataSource && logCount >= maxLogCount)
							break;
						updateInterval = this.updateInterval ?? (isContinuousReading ? configuration.GetValueOrDefault(ConfigurationKeys.ContinuousLogsReadingUpdateInterval) : defaultNonContinuousUpdateInterval);
						try
						{
							var match = logPattern.Regex.Match(logLine);
							if (match.Success)
							{
								// read log
								numberOfSkippedLogPatterns = 0;
								this.ReadLog(logBuilder, match, timeSpanFormats, timestampFormats);

								// set file name and line number
								if (logPatternIndex == 0 && isReadingFromFile)
								{
									logBuilder.Set(nameof(Log.LineNumber), lineNumber.ToString());
									logBuilder.Set(nameof(Log.FileName), fileName);
								}

								// create log and move to next pattern
								if (!logPattern.IsRepeatable)
								{
									if (logPatternIndex == lastLogPatternIndex)
									{
										if (logBuilder.IsNotEmpty())
										{
											readLog = logBuilder.BuildAndReset();
											if (!hasPrecondition || precondition.Matches(readLog))
											{
												if (isContinuousReading)
													FlushContinuousReadingLog(updateInterval);
												else
												{
													readLogs.Add(readLog);
													++logCount;
													++accumulatedLogCount;
												}
											}
										}
										logPatternIndex = 0;
										if (this.printTraceLogs)
											this.Logger.LogTrace("Reset to pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
									}
									else
									{
										++logPatternIndex;
										if (this.printTraceLogs)
											this.Logger.LogTrace("Move to pattern {index}/{t}: '{pattern}'", logPatternIndex, lastLogPatternIndex, logPatterns[logPatternIndex].Regex);
									}
								}

								// read next line or skip patterns
								ReadNextLineSkippable();
							}
							else if (logPattern.IsSkippable)
							{
								// read next line if all patterns were skipped
								++numberOfSkippedLogPatterns;
								if (numberOfSkippedLogPatterns > lastLogPatternIndex)
								{
									// log
									if (this.printTraceLogs)
										this.Logger.LogTrace("All patterns were skipped");
									
									// read next log line
									numberOfSkippedLogPatterns = 0;
									ReadNextLineSkippable();
									
									// move to first pattern
									logPatternIndex = 0;
									if (this.printTraceLogs)
										this.Logger.LogTrace("Reset to pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
									continue;
								}
								
								// move to next pattern
								if (logPatternIndex < lastLogPatternIndex)
								{
									++logPatternIndex;
									if (this.printTraceLogs)
										this.Logger.LogTrace("Move to pattern {index}/{t}: '{pattern}'", logPatternIndex, lastLogPatternIndex, logPatterns[logPatternIndex].Regex);
									continue;
								}

								// build log if this is the last pattern
								if (logBuilder.IsNotEmpty())
								{
									readLog = logBuilder.BuildAndReset();
									if (!hasPrecondition || precondition.Matches(readLog))
									{
										if (isContinuousReading)
											FlushContinuousReadingLog(updateInterval);
										else
										{
											readLogs.Add(readLog);
											++logCount;
											++accumulatedLogCount;
										}
									}
								}

								// need to move to next line if there is only one pattern
								if (lastLogPatternIndex == 0)
									ReadNextLineSkippable();

								// move to first pattern
								logPatternIndex = 0;
								if (this.printTraceLogs)
									this.Logger.LogTrace("Reset to pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
							}
							else if (logPattern.IsRepeatable)
							{
								// drop this log if this pattern never be matched
								if (prevLogPattern != logPattern)
								{
									// print log
									if (printTraceLogs)
										this.Logger.LogTrace("'{logLine}' Cannot be matched as 1st line of repeatable pattern {index}/{t}: '{logPattern}'", logLine, logPatternIndex, lastLogPatternIndex, logPattern.Regex);

									// drop log
									logBuilder.Reset();

									// need to move to next line if there is only one pattern or this is the first pattern
									if (logPatternIndex == 0 || lastLogPatternIndex == 0)
										logLine = ReadNextLine(reader, ref lineNumber);

									// move to first pattern
									logPatternIndex = 0;
									if (this.printTraceLogs)
										this.Logger.LogTrace("Reset to pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
									continue;
								}

								// move to next pattern
								if (logPatternIndex != lastLogPatternIndex)
								{
									++logPatternIndex;
									if (this.printTraceLogs)
										this.Logger.LogTrace("Move to pattern {index}/{t}: '{pattern}'", logPatternIndex, lastLogPatternIndex, logPatterns[logPatternIndex].Regex);
									continue;
								}

								// build log if this is the last pattern
								if (logBuilder.IsNotEmpty())
								{
									readLog = logBuilder.BuildAndReset();
									if (!hasPrecondition || precondition.Matches(readLog))
									{
										if (isContinuousReading)
											FlushContinuousReadingLog(updateInterval);
										else
										{
											readLogs.Add(readLog);
											++logCount;
											++accumulatedLogCount;
										}
									}
								}

								// need to move to next line if there is only one pattern or this is the first pattern
								if (logPatternIndex == 0 || lastLogPatternIndex == 0)
									logLine = ReadNextLine(reader, ref lineNumber);

								// move to first pattern
								logPatternIndex = 0;
								if (this.printTraceLogs)
									this.Logger.LogTrace("Reset to pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
							}
							else
							{
								// print log
								if (printTraceLogs)
									this.Logger.LogTrace("'{logLine}' cannot be matched by pattern {index}/{t}: '{logPattern}'", logLine, logPatternIndex, lastLogPatternIndex, logPattern.Regex);

								// drop log
								logBuilder.Reset();

								// need to move to next line if there is only one pattern or this is the first pattern
								if (logPatternIndex == 0 || lastLogPatternIndex == 0)
									logLine = ReadNextLine(reader, ref lineNumber);

								// move to first pattern
								logPatternIndex = 0;
								if (this.printTraceLogs)
									this.Logger.LogTrace("Reset to pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
							}
						}
						finally
						{
							if (isContinuousReading)
								FlushContinuousReadingLog(updateInterval);
							else if (accumulatedLogCount >= nonContinuousChunkSize || (stopWatch.ElapsedMilliseconds - startReadingTime) >= updateInterval)
							{
								if (!hasMaxLogCount || readingWindow == LogReadingWindow.StartOfDataSource)
								{
									if (readLogs.IsNotEmpty())
									{
										if (nonContinuousPaddingInterval > 0)
											Thread.Sleep(nonContinuousPaddingInterval);
										var logArray = readLogs.ToArray();
										readLogs.Clear();
										syncContext.Post(() => this.OnLogsRead(readingToken, logArray, stringSourceCache.ByteCount));
									}
								}
								else if (readLogs.Count > maxLogCount)
									readLogs.RemoveRange(0, readLogs.Count - maxLogCount);
								accumulatedLogCount = 0;
								startReadingTime = stopWatch.ElapsedMilliseconds;
							}
							prevLogPattern = logPattern;
							logPattern = logPatterns[logPatternIndex];
						}
					} while (logLine != null && !cancellationToken.IsCancellationRequested);
					
					// build the last log
					if (!cancellationToken.IsCancellationRequested && logBuilder.IsNotEmpty())
					{
						bool canBuildLog;
						if ((logPattern.IsRepeatable && prevLogPattern == logPattern /* matched at least one time */) || logPattern.IsSkippable)
						{
							canBuildLog = true;
							for (++logPatternIndex; logPatternIndex <= lastLogPatternIndex; ++logPatternIndex)
							{
								logPattern = logPatterns[logPatternIndex];
								if (!logPattern.IsSkippable)
								{
									canBuildLog = false;
									break;
								}
							}
						}
						else
							canBuildLog = false;
						if (canBuildLog)
						{
							readLog = logBuilder.BuildAndReset();
							if (!hasPrecondition || precondition.Matches(readLog))
							{
								readLogs.Add(readLog);
								++logCount;
								++accumulatedLogCount;
							}
						}
					}
				}
				else if (logPatternMatchingMode == LogPatternMatchingMode.Arbitrary
				         || logPatternMatchingMode == LogPatternMatchingMode.ArbitraryAfterFirstMatch) // general case (use 1 or more patterns in arbitrary order)
				{
					// get state
					var isFirstPatternMatchNeeded = logPatternMatchingMode == LogPatternMatchingMode.ArbitraryAfterFirstMatch
						&& !logPatterns[0].IsSkippable;
					if (isFirstPatternMatchNeeded && printTraceLogs)
						this.Logger.LogTrace("Need to match first log");

					// prepare local functions
					var unmatchedPatterns = new HashSet<LogPattern>();
					void ResetUnmatchedLogPatterns()
					{
						foreach (var logPattern in logPatterns)
						{
							if (!logPattern.IsSkippable)
								unmatchedPatterns.Add(logPattern);
						}
					}
					ResetUnmatchedLogPatterns();
					
					// read logs
					var isLineNumberSet = false;
					var isFirstPatternMatched = false;
					do
					{
						if (hasMaxLogCount && readingWindow == LogReadingWindow.StartOfDataSource && logCount >= maxLogCount)
							break;
						updateInterval = this.updateInterval ?? (isContinuousReading ? configuration.GetValueOrDefault(ConfigurationKeys.ContinuousLogsReadingUpdateInterval) : defaultNonContinuousUpdateInterval);
						try
						{
							// match pattern
							logPatternIndex = 0;
							if (!isFirstPatternMatchNeeded || isFirstPatternMatched)
							{
								while (logPatternIndex <= lastLogPatternIndex)
								{
									logPattern = logPatterns[logPatternIndex];
									var match = logPattern.Regex.Match(logLine);
									if (match.Success)
									{
										// read log
										this.ReadLog(logBuilder, match, timeSpanFormats, timestampFormats);
										unmatchedPatterns.Remove(logPattern);

										// set file name and line number
										if (!isLineNumberSet && isReadingFromFile)
										{
											logBuilder.Set(nameof(Log.LineNumber), lineNumber.ToString());
											logBuilder.Set(nameof(Log.FileName), fileName);
											isLineNumberSet = true;
										}

										// stop matching
										if (printTraceLogs)
											this.Logger.LogTrace("Matched by pattern {index}/{t}: '{pattern}'", logPatternIndex, lastLogPatternIndex, logPattern.Regex);
										break;
									}
									++logPatternIndex;
								}
							}
							else
							{
								var match = logPatterns[0].Regex.Match(logLine);
								if (match.Success)
								{
									// read log
									this.ReadLog(logBuilder, match, timeSpanFormats, timestampFormats);
									unmatchedPatterns.Remove(logPattern);
									isFirstPatternMatched = true;
									
									// set file name and line number
									if (!isLineNumberSet && isReadingFromFile)
									{
										logBuilder.Set(nameof(Log.LineNumber), lineNumber.ToString());
										logBuilder.Set(nameof(Log.FileName), fileName);
										isLineNumberSet = true;
									}
									
									// print log
									if (printTraceLogs)
										this.Logger.LogTrace("First log matched by pattern 0/{t}: '{pattern}'", lastLogPatternIndex, logPatterns[0].Regex);
								}
							}

							// build log
							if (unmatchedPatterns.IsEmpty())
							{
								if (logBuilder.IsNotEmpty())
								{
									readLog = logBuilder.BuildAndReset();
									isLineNumberSet = false;
									if (!hasPrecondition || precondition.Matches(readLog))
									{
										if (isContinuousReading)
											FlushContinuousReadingLog(updateInterval);
										else
										{
											readLogs.Add(readLog);
											++logCount;
											++accumulatedLogCount;
										}
									}
								}
								ResetUnmatchedLogPatterns();
							}
							
							// move to next line
							logLine = this.ReadNextLine(reader, ref lineNumber);
						}
						finally
						{
							if (isContinuousReading)
								FlushContinuousReadingLog(updateInterval);
							else if (accumulatedLogCount >= nonContinuousChunkSize || (stopWatch.ElapsedMilliseconds - startReadingTime) >= updateInterval)
							{
								if (!hasMaxLogCount || readingWindow == LogReadingWindow.StartOfDataSource)
								{
									if (readLogs.IsNotEmpty())
									{
										if (nonContinuousPaddingInterval > 0)
											Thread.Sleep(nonContinuousPaddingInterval);
										var logArray = readLogs.ToArray();
										readLogs.Clear();
										syncContext.Post(() => this.OnLogsRead(readingToken, logArray, stringSourceCache.ByteCount));
									}
								}
								else if (readLogs.Count > maxLogCount)
									readLogs.RemoveRange(0, readLogs.Count - maxLogCount);
								accumulatedLogCount = 0;
								startReadingTime = stopWatch.ElapsedMilliseconds;
							}
						}
					} while (logLine != null && !cancellationToken.IsCancellationRequested);
					
					// build the last log
					if (!cancellationToken.IsCancellationRequested && logBuilder.IsNotEmpty())
					{
						var canBuildLog = true;
						foreach (var unmatchedPattern in unmatchedPatterns)
						{
							if (!unmatchedPattern.IsSkippable)
							{
								canBuildLog = false;
								break;
							}
						}
						if (canBuildLog)
						{
							readLog = logBuilder.BuildAndReset();
							if (!hasPrecondition || precondition.Matches(readLog))
							{
								readLogs.Add(readLog);
								++logCount;
								++accumulatedLogCount;
							}
						}
					}
				}
				else
					throw new NotImplementedException();
				
				// cancellation check
				if (logLine != null && cancellationToken.IsCancellationRequested)
					this.Logger.LogWarning("Logs reading has been cancelled");
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Error occurred while reading logs");
				exception = ex;
			}
			finally
			{
				this.Logger.LogDebug("Complete reading logs in background");

				// send last chunk of logs
				if (readLogs.IsNotEmpty())
				{
					if (hasMaxLogCount && readingWindow == LogReadingWindow.EndOfDataSource && readLogs.Count > maxLogCount)
						readLogs.RemoveRange(0, readLogs.Count - maxLogCount);
					syncContext.Post(() => this.OnLogsRead(readingToken, readLogs, stringSourceCache.ByteCount));
				}
				
				// clear cache
				var finalStringSourceCacheByteCount = stringSourceCache.CachedStringSourcesByteCount;
				stringSourceCache.Clear();

				// close reader
				Global.RunWithoutError(reader.Close);

				// complete reading
				syncContext.Post(() => this.OnLogsReadingCompleted(exception, finalStringSourceCacheByteCount));
			}
		}
		
		
		// Read next raw log line.
		string? ReadNextLine(TextReader reader, ref int lineNumber)
		{
			++lineNumber;
			return reader.ReadLine();
		}


		/// <summary>
		/// Clear logs and restart reading logs.
		/// </summary>
		public void Restart()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (this.isRestarting)
				return;

			// start directly
			if (this.state == LogReaderState.Preparing)
			{
				this.Start(false);
				return;
			}

			this.Logger.LogWarning("Restart");

			// update state
			this.IsRestarting = true;

			// clear logs
			if (this.isContinuousReading || this.state != LogReaderState.ClearingLogs)
				this.ClearLogs(!this.isContinuousReading);
			if (this.state == LogReaderState.ClearingLogs)
			{
				this.Logger.LogWarning("Restart after clearing logs");
				return;
			}

			// start reading logs
			this.Start(false);
		}


		/// <summary>
		/// Get or set delay before restarting logs reading when <see cref="IsContinuousReading"/> is True.
		/// </summary>
		public TimeSpan RestartReadingDelay
		{
			get => this.restartReadingDelay;
			set
			{
				this.VerifyAccess();
				if (this.restartReadingDelay == value)
					return;
				if (value.TotalMilliseconds < 0)
					throw new ArgumentOutOfRangeException(nameof(value));
				this.restartReadingDelay = value;
				if (this.startReadingLogsAction.IsScheduled)
					this.startReadingLogsAction.Reschedule(value);
				this.OnPropertyChanged(nameof(RestartReadingDelay));
			}
		}


		/// <summary>
		/// Resume reading logs.
		/// </summary>
		/// <returns>True if logs reading has been resumed successfully.</returns>
		public bool Resume()
		{
			this.VerifyAccess();
			switch (this.state)
			{
				case LogReaderState.Starting:
				case LogReaderState.ReadingLogs:
					return true;
				case LogReaderState.StartingWhenPaused:
					return this.ChangeState(LogReaderState.Starting);
				case LogReaderState.Paused:
					return this.ChangeState(LogReaderState.ReadingLogs);
				default:
					this.Logger.LogWarning("Cannot resume logs reading when state is {state}", this.state);
					return false;
			}
		}


		/// <summary>
		/// Start reading logs.
		/// </summary>
		public void Start() =>
			this.Start(true);


		// Start reading logs.
		void Start(bool checkState)
		{
			// check state
			this.VerifyAccess();
			if (checkState && state != LogReaderState.Preparing)
				throw new InvalidOperationException();
			if (this.logPatterns.IsEmpty())
				throw new InvalidOperationException("No log pattern specified.");
			
			// change state
			this.ChangeState(LogReaderState.Starting);
			this.IsRestarting = false;

			// start
			this.startReadingLogsAction.Reschedule();
		}


		// Start reading logs.
		async void StartReadingLogs()
		{
			// change state
			switch (this.state)
			{
				case LogReaderState.Preparing:
				case LogReaderState.ReadingLogs:
					if (!this.ChangeState(LogReaderState.Starting))
						return;
					break;
				case LogReaderState.Paused:
					if (!this.ChangeState(LogReaderState.StartingWhenPaused))
						return;
					break;
				case LogReaderState.Starting:
				case LogReaderState.StartingWhenPaused:
					break;
				default:
					this.Logger.LogError("Cannot start reading logs when state is {state}", this.state);
					return;
			}

			// check source state
			if (this.DataSource.State != LogDataSourceState.ReadyToOpenReader)
			{
				if (this.DataSource.IsErrorState())
				{
					this.Logger.LogError("Data source state is {state} when starting reading logs", this.DataSource.State);
					this.ChangeState(LogReaderState.DataSourceError);
				}
				else
					this.Logger.LogWarning("Wait for data source ready to open reader");
				return;
			}

			// open reader
			this.openingReaderCancellationSource = new CancellationTokenSource();
			var reader = (TextReader?)null;
			try
			{
				this.Logger.LogDebug("Start opening reader");
				reader = await this.DataSource.OpenReaderAsync(this.openingReaderCancellationSource.Token);
				this.Logger.LogDebug("Reader opened");
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, "Unable to open reader");
			}
			this.openingReaderCancellationSource = this.openingReaderCancellationSource.DisposeAndReturnNull();

			// change state
			switch(this.state)
			{
				case LogReaderState.Starting:
					if (reader == null)
					{
						this.ChangeState(LogReaderState.DataSourceError);
						return;
					}
					if (!this.ChangeState(LogReaderState.ReadingLogs))
					{
						Global.RunWithoutErrorAsync(() => reader.Close());
						return;
					}
					break;
				case LogReaderState.StartingWhenPaused:
					if (reader == null)
					{
						this.ChangeState(LogReaderState.DataSourceError);
						return;
					}
					if (!this.ChangeState(LogReaderState.Paused))
					{
						Global.RunWithoutErrorAsync(() => reader.Close());
						return;
					}
					break;
				default:
					this.Logger.LogWarning("State has been changed to {state} when opening reader", this.state);
					Global.RunWithoutErrorAsync(() => reader?.Close());
					return;
			}

			// read logs
			this.logsReadingToken = new object();
			this.logsReadingCancellationTokenSource = new CancellationTokenSource();
			var readingToken = this.logsReadingToken;
			var cancellationToken = this.logsReadingCancellationTokenSource.Token;
			_ = this.readingTaskFactory.StartNew(() => this.ReadLogs(readingToken, reader, cancellationToken), CancellationToken.None);
		}


		/// <summary>
		/// Get current state of <see cref="LogReader"/>.
		/// </summary>
		public LogReaderState State => this.state;
		
		
		/// <summary>
		/// Stop reading logs.
		/// </summary>
		/// <returns>True if logs reading has been stopped successfully.</returns>
		public bool Stop()
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			switch (this.state)
			{
				case LogReaderState.Starting:
				case LogReaderState.StartingWhenPaused:
					this.Logger.LogWarning("Stop when starting");
					if (!this.ChangeState(LogReaderState.Stopping))
					{
						this.Logger.LogWarning("Stopping flow was interrupted");
						return false;
					}
					if (this.openingReaderCancellationSource is not null)
					{
						this.openingReaderCancellationSource.Cancel();
						this.openingReaderCancellationSource = this.openingReaderCancellationSource.DisposeAndReturnNull();
					}
					if (this.logsReadingCancellationTokenSource is not null)
					{
						this.logsReadingCancellationTokenSource.Cancel();
						this.logsReadingCancellationTokenSource = this.logsReadingCancellationTokenSource.DisposeAndReturnNull();
					}
					if (!this.ChangeState(LogReaderState.Stopped))
					{
						this.Logger.LogWarning("Stopping flow was interrupted");
						return false;
					}
					return true;
				case LogReaderState.ReadingLogs:
				case LogReaderState.Paused:
					this.Logger.LogWarning("Stop");
					if (!this.ChangeState(LogReaderState.Stopping))
					{
						this.Logger.LogWarning("Stopping flow was interrupted");
						return false;
					}
					break;
				case LogReaderState.Stopping:
					return true;
				default:
					this.Logger.LogWarning("Cannot stop when state is {state}", this.state);
					return false;
			}
			if (this.logsReadingCancellationTokenSource is not null)
			{
				this.logsReadingCancellationTokenSource.Cancel();
				this.logsReadingCancellationTokenSource = null;
			}
			return true;
		}


		/// <summary>
		/// Get or set <see cref="CultureInfo"/> to parse time span of log.
		/// </summary>
		public CultureInfo TimeSpanCultureInfo
		{
			get => this.timeSpanCultureInfo;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.timeSpanCultureInfo.Equals(value))
					return;
				this.timeSpanCultureInfo = value;
				this.OnPropertyChanged(nameof(TimeSpanCultureInfo));
			}
		}


		/// <summary>
		/// Get or set encoding to parse time span of log.
		/// </summary>
		public LogTimeSpanEncoding TimeSpanEncoding
		{
			get => this.timeSpanEncoding;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.timeSpanEncoding == value)
					return;
				this.timeSpanEncoding = value;
				this.OnPropertyChanged(nameof(TimeSpanEncoding));
			}
		}


		/// <summary>
		/// Get or set list of format to parse time span of log when <see cref="TimeSpanEncoding"/> is <see cref="LogTimeSpanEncoding.Custom"/>.
		/// </summary>
		public IList<string> TimeSpanFormats
		{
			get => this.timeSpanFormats;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.timeSpanFormats.SequenceEqual(value))
					return;
				this.timeSpanFormats = ListExtensions.AsReadOnly(value.ToArray());
				this.OnPropertyChanged(nameof(TimeSpanFormats));
			}
		}


		/// <summary>
		/// Get or set <see cref="CultureInfo"/> to parse timestamp of log.
		/// </summary>
		public CultureInfo TimestampCultureInfo
		{
			get => this.timestampCultureInfo;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.timestampCultureInfo.Equals(value))
					return;
				this.timestampCultureInfo = value;
				this.OnPropertyChanged(nameof(TimestampCultureInfo));
			}
		}


		/// <summary>
		/// Get or set encoding to parse timestamp of log.
		/// </summary>
		public LogTimestampEncoding TimestampEncoding
		{
			get => this.timestampEncoding;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.timestampEncoding == value)
					return;
				this.timestampEncoding = value;
				this.OnPropertyChanged(nameof(TimestampEncoding));
			}
		}


		/// <summary>
		/// Get or set list of format to parse timestamp of log when <see cref="TimestampEncoding"/> is <see cref="LogTimestampEncoding.Custom"/>.
		/// </summary>
		public IList<string> TimestampFormats
		{
			get => this.timestampFormats;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.timestampFormats.SequenceEqual(value))
					return;
				this.timestampFormats = ListExtensions.AsReadOnly(value.ToArray());
				this.OnPropertyChanged(nameof(TimestampFormats));
			}
		}


		/// <summary>
		/// Get or set interval of updating read log in milliseconds.
		/// </summary>
		public int? UpdateInterval
		{
			get => this.updateInterval;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (value.GetValueOrDefault() < 0)
					throw new ArgumentOutOfRangeException(nameof(value));
				if (this.updateInterval == value)
					return;
				this.updateInterval = value;
				this.OnPropertyChanged(nameof(UpdateInterval));
			}
		}


		// Implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		IApplication IApplicationObject.Application => this.Application;
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
		public override string ToString() => $"{this.GetType().Name}-{this.Id}";
	}


	/// <summary>
	/// State of <see cref="LogReader"/>.
	/// </summary>
	enum LogReaderState
	{
		/// <summary>
		/// Preparing.
		/// </summary>
		Preparing,
		/// <summary>
		/// Starting to read logs.
		/// </summary>
		Starting,
		/// <summary>
		/// Starting to read logs but pausing logs reading has been requested.
		/// </summary>
		StartingWhenPaused,
		/// <summary>
		/// Reading logs.
		/// </summary>
		ReadingLogs,
		/// <summary>
		/// Pause reading logs.
		/// </summary>
		Paused,
		/// <summary>
		/// Stopping.
		/// </summary>
		Stopping,
		/// <summary>
		/// Stopped.
		/// </summary>
		Stopped,
		/// <summary>
		/// Clearing logs.
		/// </summary>
		ClearingLogs,
		/// <summary>
		/// Error caused by <see cref="LogReader.DataSource"/>.
		/// </summary>
		DataSourceError,
		/// <summary>
		/// Unclassified error.
		/// </summary>
		UnclassifiedError,
		/// <summary>
		/// Disposed.
		/// </summary>
		Disposed,
	}


	/// <summary>
	/// Window of reading logs from data source.
	/// </summary>
	enum LogReadingWindow
	{
		/// <summary>
		/// Start of data source.
		/// </summary>
		StartOfDataSource,
		/// <summary>
		/// End of data source.
		/// </summary>
		EndOfDataSource,
	}
}
