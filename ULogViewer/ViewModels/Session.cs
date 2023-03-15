using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.AppSuite.Product;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Data.Converters;
using CarinaStudio.Diagnostics;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using CarinaStudio.ULogViewer.Collections;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Session to view logs.
	/// </summary>
	class Session : ViewModel<IULogViewerApplication>
	{
		/// <summary>
		/// Maximum size of side panel.
		/// </summary>
		public const double MaxSidePanelSize = 400;
		/// <summary>
		/// Minimum size of side panel.
		/// </summary>
		public const double MinSidePanelSize = 100;


		/// <summary>
		/// Property of <see cref="AllLogCount"/>.
		/// </summary>
		public static readonly ObservableProperty<int> AllLogCountProperty = ObservableProperty.Register<Session, int>(nameof(AllLogCount));
		/// <summary>
		/// Property of <see cref="AreFileBasedLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> AreFileBasedLogsProperty = ObservableProperty.Register<Session, bool>(nameof(AreFileBasedLogs));
		/// <summary>
		/// Property of <see cref="AreLogsSortedByTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> AreLogsSortedByTimestampProperty = ObservableProperty.Register<Session, bool>(nameof(AreLogsSortedByTimestamp));
		/// <summary>
		/// Property of <see cref="CustomTitle"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> CustomTitleProperty = ObservableProperty.Register<Session, string?>(nameof(CustomTitle), coerce: (_, it) => string.IsNullOrWhiteSpace(it) ? null : it);
		/// <summary>
		/// Property of <see cref="DisplayLogProperties"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<DisplayableLogProperty>> DisplayLogPropertiesProperty = ObservableProperty.Register<Session, IList<DisplayableLogProperty>>(nameof(DisplayLogProperties), Array.Empty<DisplayableLogProperty>());
		/// <summary>
		/// Property of <see cref="EarliestLogTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<DateTime?> EarliestLogTimestampProperty = ObservableProperty.Register<Session, DateTime?>(nameof(EarliestLogTimestamp));
		/// <summary>
		/// Property of <see cref="FilteredLogCount"/>.
		/// </summary>
		public static readonly ObservableProperty<int> FilteredLogCountProperty = ObservableProperty.Register<Session, int>(nameof(FilteredLogCount));
		/// <summary>
		/// Property of <see cref="HasAllDataSourceErrors"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasAllDataSourceErrorsProperty = ObservableProperty.Register<Session, bool>(nameof(HasAllDataSourceErrors));
		/// <summary>
		/// Property of <see cref="HasCustomTitle"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasCustomTitleProperty = ObservableProperty.Register<Session, bool>(nameof(HasCustomTitle));
		/// <summary>
		/// Property of <see cref="HasIPEndPoint"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasIPEndPointProperty = ObservableProperty.Register<Session, bool>(nameof(HasIPEndPoint));
		/// <summary>
		/// Property of <see cref="HasLastLogsReadingDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLastLogsReadingDurationProperty = ObservableProperty.Register<Session, bool>(nameof(HasLastLogsReadingDuration));
		/// <summary>
		/// Property of <see cref="HasLogColorIndicator"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogColorIndicatorProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogColorIndicator));
		/// <summary>
		/// Property of <see cref="HasLogColorIndicatorByFileName"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogColorIndicatorByFileNameProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogColorIndicatorByFileName));
		/// <summary>
		/// Property of <see cref="HasLogFiles"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogFilesProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogFiles));
		/// <summary>
		/// Property of <see cref="HasLogProfile"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogProfileProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogProfile));
		/// <summary>
		/// Property of <see cref="HasLogReaders"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogReadersProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogReaders));
		/// <summary>
		/// Property of <see cref="HasLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogsProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogs));
		/// <summary>
		/// Property of <see cref="HasLogsDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogsDurationProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogsDuration));
		/// <summary>
		/// Property of <see cref="HasMarkedLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasMarkedLogsProperty = ObservableProperty.Register<Session, bool>(nameof(HasMarkedLogs));
		/// <summary>
		/// Property of <see cref="HasPartialDataSourceErrors"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasPartialDataSourceErrorsProperty = ObservableProperty.Register<Session, bool>(nameof(HasPartialDataSourceErrors));
		/// <summary>
		/// Property of <see cref="HasUri"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasUriProperty = ObservableProperty.Register<Session, bool>(nameof(HasUri));
		/// <summary>
		/// Property of <see cref="HasTimestampDisplayableLogProperty"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasTimestampDisplayableLogPropertyProperty = ObservableProperty.Register<Session, bool>(nameof(HasTimestampDisplayableLogProperty));
		/// <summary>
		/// Property of <see cref="HasWorkingDirectory"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasWorkingDirectoryProperty = ObservableProperty.Register<Session, bool>(nameof(HasWorkingDirectory));
		/// <summary>
		/// Property of <see cref="Icon"/>.
		/// </summary>
		public static readonly ObservableProperty<IImage?> IconProperty = ObservableProperty.Register<Session, IImage?>(nameof(Icon));
		/// <summary>
		/// Property of <see cref="IPEndPoint"/>.
		/// </summary>
		public static readonly ObservableProperty<IPEndPoint?> IPEndPointProperty = ObservableProperty.Register<Session, IPEndPoint?>(nameof(IPEndPoint));
		/// <summary>
		/// Property of <see cref="IsActivated"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsActivatedProperty = ObservableProperty.Register<Session, bool>(nameof(IsActivated));
		/// <summary>
		/// Property of <see cref="IsAnalyzingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsAnalyzingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsAnalyzingLogs));
		/// <summary>
		/// Property of <see cref="IsBuiltInLogProfile"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsBuiltInLogProfileProperty = ObservableProperty.Register<Session, bool>(nameof(IsBuiltInLogProfile));
		/// <summary>
		/// Property of <see cref="IsCopyingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsCopyingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsCopyingLogs));
		/// <summary>
		/// Property of <see cref="IsHibernated"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsHibernatedProperty = ObservableProperty.Register<Session, bool>(nameof(IsHibernated));
		/// <summary>
		/// Property of <see cref="IsIPEndPointNeeded"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsIPEndPointNeededProperty = ObservableProperty.Register<Session, bool>(nameof(IsIPEndPointNeeded));
		/// <summary>
		/// Property of <see cref="IsLogFileNeeded"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsLogFileNeededProperty = ObservableProperty.Register<Session, bool>(nameof(IsLogFileNeeded));
		/// <summary>
		/// Property of <see cref="IsLogFilesPanelVisible"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsLogFilesPanelVisibleProperty = ObservableProperty.Register<Session, bool>(nameof(IsLogFilesPanelVisible), false);
		/// <summary>
		/// Property of <see cref="IsLogFileSupported"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsLogFileSupportedProperty = ObservableProperty.Register<Session, bool>(nameof(IsLogFileSupported), false);
		/// <summary>
		/// Property of <see cref="IsLogsReadingPaused"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsLogsReadingPausedProperty = ObservableProperty.Register<Session, bool>(nameof(IsLogsReadingPaused));
		/// <summary>
		/// Property of <see cref="IsMarkedLogsPanelVisible"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsMarkedLogsPanelVisibleProperty = ObservableProperty.Register<Session, bool>(nameof(IsMarkedLogsPanelVisible), true);
		/// <summary>
		/// Property of <see cref="IsProcessingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsProcessingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsProcessingLogs));
		/// <summary>
		/// Property of <see cref="IsProVersionActivated"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsProVersionActivatedProperty = ObservableProperty.Register<Session, bool>(nameof(IsProVersionActivated));
		/// <summary>
		/// Property of <see cref="IsReadingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsReadingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsReadingLogs));
		/// <summary>
		/// Property of <see cref="IsReadingLogsContinuously"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsReadingLogsContinuouslyProperty = ObservableProperty.Register<Session, bool>(nameof(IsReadingLogsContinuously));
		/// <summary>
		/// Property of <see cref="IsRemovingLogFiles"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsRemovingLogFilesProperty = ObservableProperty.Register<Session, bool>(nameof(IsRemovingLogFiles));
		/// <summary>
		/// Property of <see cref="IsSavingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsSavingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsSavingLogs));
		/// <summary>
		/// Property of <see cref="IsShowingAllLogsTemporarily"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsShowingAllLogsTemporarilyProperty = ObservableProperty.Register<Session, bool>(nameof(IsShowingAllLogsTemporarily));
		/// <summary>
		/// Property of <see cref="IsShowingMarkedLogsTemporarily"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsShowingMarkedLogsTemporarilyProperty = ObservableProperty.Register<Session, bool>(nameof(IsShowingMarkedLogsTemporarily));
		/// <summary>
		/// Property of <see cref="IsUriNeeded"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsUriNeededProperty = ObservableProperty.Register<Session, bool>(nameof(IsUriNeeded));
		/// <summary>
		/// Property of <see cref="IsWaitingForDataSources"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsWaitingForDataSourcesProperty = ObservableProperty.Register<Session, bool>(nameof(IsWaitingForDataSources));
		/// <summary>
		/// Property of <see cref="IsWorkingDirectoryNeeded"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsWorkingDirectoryNeededProperty = ObservableProperty.Register<Session, bool>(nameof(IsWorkingDirectoryNeeded));
		/// <summary>
		/// Property of <see cref="LastLogReadingPrecondition"/>.
		/// </summary>
		public static readonly ObservableProperty<LogReadingPrecondition> LastLogReadingPreconditionProperty = ObservableProperty.Register<Session, LogReadingPrecondition>(nameof(LastLogReadingPrecondition));
		/// <summary>
		/// Property of <see cref="LastLogsReadingDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> LastLogsReadingDurationProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(LastLogsReadingDuration));
		/// <summary>
		/// Property of <see cref="LatestLogTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<DateTime?> LatestLogTimestampProperty = ObservableProperty.Register<Session, DateTime?>(nameof(LatestLogTimestamp));
		/// <summary>
		/// Property of <see cref="LogFilesPanelSize"/>.
		/// </summary>
		public static readonly ObservableProperty<double> LogFilesPanelSizeProperty = ObservableProperty.Register<Session, double>(nameof(LogFilesPanelSize), (MinSidePanelSize + MaxSidePanelSize) / 2, 
			coerce: (_, it) =>
			{
				if (it >= MaxSidePanelSize)
					return MaxSidePanelSize;
				if (it < MinSidePanelSize)
					return MinSidePanelSize;
				return it;
			}, 
			validate: it => double.IsFinite(it));
		/// <summary>
		/// Property of <see cref="LogProfile"/>.
		/// </summary>
		public static readonly ObservableProperty<LogProfile?> LogProfileProperty = ObservableProperty.Register<Session, LogProfile?>(nameof(LogProfile));
		/// <summary>
		/// Property of <see cref="LogsMemoryUsage"/>.
		/// </summary>
		public static readonly ObservableProperty<long> LogsMemoryUsageProperty = ObservableProperty.Register<Session, long>(nameof(LogsMemoryUsage));
		/// <summary>
		/// Property of <see cref="Logs"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<DisplayableLog>> LogsProperty = ObservableProperty.Register<Session, IList<DisplayableLog>>(nameof(Logs), Array.Empty<DisplayableLog>());
		/// <summary>
		/// Property of <see cref="LogsDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> LogsDurationProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(LogsDuration));
		/// <summary>
		/// Property of <see cref="LogsDurationEndingString"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> LogsDurationEndingStringProperty = ObservableProperty.Register<Session, string?>(nameof(LogsDurationEndingString));
		/// <summary>
		/// Property of <see cref="LogsDurationStartingString"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> LogsDurationStartingStringProperty = ObservableProperty.Register<Session, string?>(nameof(LogsDurationStartingString));
		/// <summary>
		/// Property of <see cref="MarkedLogsPanelSize"/>.
		/// </summary>
		public static readonly ObservableProperty<double> MarkedLogsPanelSizeProperty = ObservableProperty.Register<Session, double>(nameof(MarkedLogsPanelSize), (MinSidePanelSize + MaxSidePanelSize) / 2, 
			coerce: (_, it) =>
			{
				if (it >= MaxSidePanelSize)
					return MaxSidePanelSize;
				if (it < MinSidePanelSize)
					return MinSidePanelSize;
				return it;
			}, 
			validate: it => double.IsFinite(it));
		/// <summary>
		/// Property of <see cref="MaxLogTimeSpan"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> MaxLogTimeSpanProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(MaxLogTimeSpan));
		/// <summary>
		/// Property of <see cref="MinLogTimeSpan"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> MinLogTimeSpanProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(MinLogTimeSpan));
		/// <summary>
		/// Property of <see cref="Title"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> TitleProperty = ObservableProperty.Register<Session, string?>(nameof(Title));
		/// <summary>
		/// Property of <see cref="TotalLogsMemoryUsage"/>.
		/// </summary>
		public static readonly ObservableProperty<long> TotalLogsMemoryUsageProperty = ObservableProperty.Register<Session, long>(nameof(TotalLogsMemoryUsage));
		/// <summary>
		/// Property of <see cref="Uri"/>.
		/// </summary>
		public static readonly ObservableProperty<Uri?> UriProperty = ObservableProperty.Register<Session, Uri?>(nameof(Uri));
		/// <summary>
		/// Property of <see cref="ValidLogLevels"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<Logs.LogLevel>> ValidLogLevelsProperty = ObservableProperty.Register<Session, IList<Logs.LogLevel>>(nameof(ValidLogLevels), Array.Empty<Logs.LogLevel>());
		/// <summary>
		/// Property of <see cref="WorkingDirectoryName"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> WorkingDirectoryNameProperty = ObservableProperty.Register<Session, string?>(nameof(WorkingDirectoryName));
		/// <summary>
		/// Property of <see cref="WorkingDirectoryPath"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> WorkingDirectoryPathProperty = ObservableProperty.Register<Session, string?>(nameof(WorkingDirectoryPath));


		/// <summary>
		/// <see cref="IValueConverter"/> to convert from logs operation progress to readable string.
		/// </summary>
		public static readonly IValueConverter LogsOperationProgressConverter = new AppSuite.Converters.RatioToPercentageConverter(1);


		/// <summary>
		/// Parameters of log data source.
		/// </summary>
		/// <typeparam name="TSource">Type of source of log data.</typeparam>
		public class LogDataSourceParams<TSource>
		{
			/// <summary>
			/// Precondition of log reading.
			/// </summary>
			public LogReadingPrecondition Precondition { get; set; }


			/// <summary>
			/// Source of log data.
			/// </summary>
			[System.Diagnostics.CodeAnalysis.AllowNull]
			public TSource Source { get; set; }
		}


		/// <summary>
		/// Information of log file.
		/// </summary>
		public abstract class LogFileInfo : INotifyPropertyChanged
		{
			/// <summary>
			/// Initialize new <see cref="LogFileInfo"/> instance.
			/// </summary>
			/// <param name="fileName">Name of log file.</param>
			protected LogFileInfo(string fileName)
			{
				this.FileName = fileName;
			}

			/// <summary>
			/// Get brush of color indicator.
			/// </summary>
			public virtual IBrush? ColorIndicatorBrush => null;

			/// <summary>
			/// Get name of log file.
			/// </summary>
			public string FileName { get; }

			/// <summary>
			/// Check whether error was occurred while reading logs or not.
			/// </summary>
			public abstract bool HasError { get; }

			/// <summary>
			/// Check whether all logs are read from log file or not.
			/// </summary>
			public abstract bool IsLogsReadingCompleted { get; }

			/// <summary>
			/// Check whether the log file is predefined by log profile or not.
			/// </summary>
			public abstract bool IsPredefined { get; }

			/// <summary>
			/// Check whether logs are being read from this file or not.
			/// </summary>
			public abstract bool IsReadingLogs { get; }

			/// <summary>
			/// Check whether the log file is being removed or not.
			/// </summary>
			public abstract bool IsRemoving { get; }

			/// <summary>
			/// Get number of logs read from this file.
			/// </summary>
			public abstract int LogCount { get; }

			/// <summary>
			/// Raise <see cref="OnPropertyChanged"/> event.
			/// </summary>
			/// <param name="propertyName">Property name.</param>
			protected void OnPropertyChanged(string propertyName) =>
				this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			
			/// <summary>
			/// Get precondition of reading logs from this file.
			/// </summary>
			public abstract LogReadingPrecondition LogReadingPrecondition { get; }

			/// <inheritdoc/>
			public event PropertyChangedEventHandler? PropertyChanged;
		}


		/// <summary>
		/// Parameters of marking logs.
		/// </summary>
		public class MarkingLogsParams
        {
			/// <summary>
			/// Color to mark logs.
			/// </summary>
			public MarkColor Color { get; set; } = MarkColor.Default;

			/// <summary>
			/// Get or set logs to be marked.
			/// </summary>
			public IEnumerable<DisplayableLog> Logs { get; set; } = Array.Empty<DisplayableLog>();
        }


		// Constants.
		const string MarkedFileExtension = ".ulvmark";
		const int DisplayableLogDisposingChunkSize = 65536;
		const int DelaySaveMarkedLogs = 1000;
		const int DisposeDisplayableLogsInterval = 100;
		const int FileLogsReadingConcurrencyLevel = 1;
		const int LogsTimeInfoReportingInterval = 500;


		// Static fields.
		static readonly LinkedList<Session> activationHistoryList = new();
		static readonly TaskFactory defaultLogsReadingTaskFactory = new(TaskScheduler.Default);
		static readonly List<DisplayableLog> displayableLogsToDispose = new();
		static readonly ScheduledAction disposeDisplayableLogsAction = new(App.Current, () =>
		{
			if (displayableLogsToDispose.IsEmpty())
				return;
			var logCount = displayableLogsToDispose.Count;
			if (logCount <= DisplayableLogDisposingChunkSize)
			{
				foreach (var log in displayableLogsToDispose)
					log.Dispose();
				displayableLogsToDispose.Clear();
				if (App.Current.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy) != MemoryUsagePolicy.BetterPerformance)
					displayableLogsToDispose.TrimExcess();
				staticLogger?.LogTrace("Disposed {disposed} displayable logs", logCount);
				staticLogger?.LogTrace($"All displayable logs were disposed, trigger GC");
				TriggerGC();
			}
			else
			{
				for (var i = logCount - DisplayableLogDisposingChunkSize; i < logCount; ++i)
					displayableLogsToDispose[i].Dispose();
				displayableLogsToDispose.RemoveRange(logCount - DisplayableLogDisposingChunkSize, DisplayableLogDisposingChunkSize);
#pragma warning disable CS8602
				disposeDisplayableLogsAction.Schedule(DisposeDisplayableLogsInterval);
#pragma warning restore CS8602
				staticLogger?.LogTrace("Disposed {disposed} displayable logs, {remaining} remains", DisplayableLogDisposingChunkSize, displayableLogsToDispose.Count);
			}
		});
		static readonly ScheduledAction hibernateSessionsAction = new(() =>
		{
			// setup threshold
			if (memoryThresholdToStartHibernation <= 0)
			{
				memoryThresholdToStartHibernation = App.Current.HardwareInfo.TotalPhysicalMemory.GetValueOrDefault() >> 2;
				if (memoryThresholdToStartHibernation <= 0)
				{
					staticLogger?.LogWarning("Unable to get total physical memory to setup threshold for hibernation");
					return;
				}
			}

			// check logs memory usage
			var logsMemoryUsage = totalLogsMemoryUsage;
			staticLogger?.LogTrace("Total logs memory usage: {memoryUsage}, threshold: {threshold}", 
				AppSuite.Converters.FileSizeConverter.Default.Convert<string>(logsMemoryUsage),
				AppSuite.Converters.FileSizeConverter.Default.Convert<string>(memoryThresholdToStartHibernation));
			if (logsMemoryUsage <= memoryThresholdToStartHibernation)
				return;

			// hibernate sessions
			var releasedMemory = 0L;
			var hibernatedSessionCount = 0;
			var node = activationHistoryList.Last;
			while (node != null && logsMemoryUsage > memoryThresholdToStartHibernation)
			{
				var session = node.Value;
				var sessionLogsMemoryUsage = session.LogsMemoryUsage;
				if (session.Hibernate())
				{
					releasedMemory += sessionLogsMemoryUsage;
					logsMemoryUsage -= sessionLogsMemoryUsage;
					++hibernatedSessionCount;
				}
				node = node.Previous;
			}
			staticLogger?.LogWarning("Hibernate {hibernatedSessionCount} session(s) to release {releasedMemory} memory", hibernatedSessionCount, AppSuite.Converters.FileSizeConverter.Default.Convert<string>(releasedMemory));
		});
		static readonly TaskFactory ioTaskFactory = new(new FixedThreadsTaskScheduler(1));
		static readonly SettingKey<double> latestLogFilesPanelSizeKey = new("Session.LatestLogFilesPanelSize", LogFilesPanelSizeProperty.DefaultValue);
		static readonly SettingKey<double> latestMarkedLogsPanelSizeKey = new("Session.LatestMarkedLogsPanelSize", MarkedLogsPanelSizeProperty.DefaultValue);
		[Obsolete]
		static readonly SettingKey<double> latestSidePanelSizeKey = new("Session.LatestSidePanelSize", MarkedLogsPanelSizeProperty.DefaultValue);
		static long memoryThresholdToStartHibernation;
		static ILogger? staticLogger;
		static long totalLogsMemoryUsage;


		// Activation token.
		class ActivationToken : IDisposable
		{
			readonly Session session;
			public ActivationToken(Session session) => this.session = session;
			public void Dispose() => this.session.Deactivate(this);
		}


		// Implementation of ISessionInternalAccessor.
		class InternalAccessorImpl : ISessionInternalAccessor
		{
			// Fields.
			readonly Session session;

			// Constructor.
			public InternalAccessorImpl(Session session) =>
				this.session = session;
			
			/// <inheritdoc/>
			public DisplayableLogGroup? DisplayableLogGroup =>
				this.session.displayableLogGroup;
			
			/// <inheritdoc/>
			public event EventHandler? DisplayableLogGroupCreated
			{
				add => this.session.DisplayableLogGroupCreated += value;
				remove => this.session.DisplayableLogGroupCreated -= value;
			}
			
			/// <inheritdoc/>
			public MemoryUsagePolicy MemoryUsagePolicy =>
				this.session.memoryUsagePolicy;
		}


		// Implementation of LogFileInfo.
		class LogFileInfoImpl : LogFileInfo
		{
			// Fields.
			bool hasError;
			bool isLogsReadingCompleted;
			bool isReadingLogs = true;
			bool isRemoving;
			int logCount;
			LogReadingPrecondition readingPrecondition;
			readonly Session session;

			// Constructor.
			public LogFileInfoImpl(Session session, string fileName, LogReadingPrecondition precondition, bool isPredefined = false) : base(fileName)
			{ 
				this.IsPredefined = isPredefined;
				this.readingPrecondition = precondition;
				this.session = session;
			}

			// Color indicator brush.
			public override IBrush? ColorIndicatorBrush => this.session.displayableLogGroup?.GetColorIndicatorBrush(this.FileName);

			// Has error.
			public override bool HasError => this.hasError;

			// Whether all logs are read or not.
			public override bool IsLogsReadingCompleted => this.isLogsReadingCompleted;

			// Is predefined.
			public override bool IsPredefined { get; }

			// Is reading logs.
			public override bool IsReadingLogs => this.isReadingLogs;

			// Whether file is being removed or not.
			public override bool IsRemoving => this.isRemoving;

			// Log count.
			public override int LogCount => this.logCount;

			// Log reading precondition.
			public override LogReadingPrecondition LogReadingPrecondition => this.readingPrecondition;

			// Update color indicator brush.
			public void UpdateColorIndicatorBrush() =>
				this.OnPropertyChanged(nameof(ColorIndicatorBrush));

			// Update log count.
			public void UpdateLogCount(int logCount)
			{
				var prevLogCount = this.logCount;
				if (prevLogCount == logCount)
					return;
				this.logCount = logCount;
				this.OnPropertyChanged(nameof(LogCount));
				if (prevLogCount == 0)
					this.OnPropertyChanged(nameof(ColorIndicatorBrush)); // to prevent getting brush too earlier by view
			}

			// Update log reader state.
			public void UpdateLogReaderState(LogReaderState state)
			{
				var hasError = state == LogReaderState.DataSourceError 
					|| state == LogReaderState.UnclassifiedError;
				var isLogsReadingCompleted = false;
				var isReadingLogs = false;
				var isRemoving = false;
				switch (state)
				{
					case LogReaderState.ClearingLogs:
						isRemoving = true;
						break;
					case LogReaderState.DataSourceError:
					case LogReaderState.UnclassifiedError:
						break;
					case LogReaderState.Stopped:
						isLogsReadingCompleted = true;
						break;
					default:
						isReadingLogs = true;
						break;
				}
				if (this.hasError != hasError)
				{
					this.hasError = hasError;
					this.OnPropertyChanged(nameof(HasError));
				}
				if (this.isLogsReadingCompleted != isLogsReadingCompleted)
				{
					this.isLogsReadingCompleted = isLogsReadingCompleted;
					this.OnPropertyChanged(nameof(IsLogsReadingCompleted));
				}
				if (this.isReadingLogs != isReadingLogs)
				{
					this.isReadingLogs = isReadingLogs;
					this.OnPropertyChanged(nameof(IsReadingLogs));
				}
				if (this.isRemoving != isRemoving)
				{
					this.isRemoving = isRemoving;
					this.OnPropertyChanged(nameof(IsRemoving));
				}
			}

			// Update log reading precondition.
			public void UpdateLogReadingPrecondition(LogReadingPrecondition precondition)
			{
				if (this.readingPrecondition == precondition)
					return;
				this.readingPrecondition = precondition;
				this.OnPropertyChanged(nameof(LogReadingPrecondition));
			}
		}


		// Options of log reader.
		class LogReaderOptions
		{
			public readonly LogDataSourceOptions DataSourceOptions;
			public LogReadingPrecondition Precondition;

			public LogReaderOptions(LogDataSourceOptions dataSourceOptions) =>
				this.DataSourceOptions = dataSourceOptions;
		}


		// Class for marked log info.
		class MarkedLogInfo
		{
			public readonly MarkColor Color;
			public readonly string FileName;
			public readonly int LineNumber;
			public readonly DateTime? Timestamp;
			public MarkedLogInfo(string fileName, int lineNumber, DateTime? timestamp, MarkColor color)
			{
				this.Color = color;
				this.FileName = fileName;
				this.LineNumber = lineNumber;
				this.Timestamp = timestamp;
			}
		}


		// Constants.
		const int DefaultFileOpeningTimeout = 10000;
		const int LogsMemoryUsageCheckInterval = 1000;


		// Fields.
		readonly LinkedListNode<Session> activationHistoryListNode;
		readonly List<IDisposable> activationTokens = new ();
		readonly SortedObservableList<DisplayableLog> allLogs;
		readonly Dictionary<string, List<DisplayableLog>> allLogsByLogFilePath = new(PathEqualityComparer.Default);
		readonly HashSet<SessionComponent> attachedComponents = new();
		ILogDataSourceProvider? attachedLogDataSourceProvider;
		readonly MutableObservableBoolean canClearLogFiles = new();
		readonly MutableObservableBoolean canCopyLogs = new();
		readonly MutableObservableBoolean canCopyLogsWithFileNames = new();
		readonly MutableObservableBoolean canMarkUnmarkLogs = new();
		readonly MutableObservableBoolean canPauseResumeLogsReading = new();
		readonly MutableObservableBoolean canReloadLogs = new();
		readonly MutableObservableBoolean canResetLogProfile = new();
		readonly MutableObservableBoolean canSaveLogs = new();
		readonly MutableObservableBoolean canSearchLogPropertyOnInternet = new();
		readonly MutableObservableBoolean canSetLogProfile = new();
		readonly MutableObservableBoolean canShowAllLogsTemporarily = new();
		readonly ScheduledAction checkDataSourceErrorsAction;
		readonly ScheduledAction checkIsWaitingForDataSourcesAction;
		readonly ScheduledAction checkLogsMemoryUsageAction;
		Comparison<DisplayableLog?> compareDisplayableLogsDelegate;
		DisplayableLogGroup? displayableLogGroup;
		TaskFactory? fileLogsReadingTaskFactory;
		bool hasLogDataSourceCreationFailure;
		bool isInitLogProfile;
		bool isRestoringState;
		readonly Dictionary<LogReader, LogFileInfoImpl> logFileInfoMapByLogReader = new();
		readonly SortedObservableList<LogFileInfo> logFileInfoList = new((lhs, rhs) =>
			PathComparer.Default.Compare(lhs.FileName, rhs.FileName));
		readonly List<LogReader> logReaders = new();
		readonly Stopwatch logsReadingWatch = new();
		readonly SortedObservableList<DisplayableLog> markedLogs;
		readonly HashSet<string> markedLogsChangedFilePaths = new(PathEqualityComparer.Default);
		MemoryUsagePolicy memoryUsagePolicy;
		readonly ScheduledAction reloadLogsAction;
		readonly ScheduledAction reloadLogsFullyAction;
		readonly ScheduledAction reloadLogsWithRecreatingLogReadersAction;
		readonly ScheduledAction reloadLogsWithUpdatingVisPropAction;
		readonly ScheduledAction reportLogsTimeInfoAction;
		readonly List<LogReaderOptions> savedLogReaderOptions = new();
		readonly ScheduledAction saveMarkedLogsAction;
		readonly ScheduledAction selectLogsToReportActions;
		readonly List<MarkedLogInfo> unmatchedMarkedLogInfos = new();
		readonly ScheduledAction updateIsReadingLogsAction;
		readonly ScheduledAction updateIsRemovingLogFilesAction;
		readonly ScheduledAction updateIsProcessingLogsAction;
		readonly ScheduledAction updateTitleAndIconAction;


		/// <summary>
		/// Initialize new <see cref="Session"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public Session(IULogViewerApplication app) : this(app, null)
		{ }


		/// <summary>
		/// Initialize new <see cref="Session"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="initLogProfile">Initial log profile.</param>
		public Session(IULogViewerApplication app, LogProfile? initLogProfile) : base(app)
		{
			// create static logger
			staticLogger ??= app.LoggerFactory.CreateLogger(nameof(Session));

			// create node for activation history
			this.activationHistoryListNode = new LinkedListNode<Session>(this);

			// create commands
			this.AddLogFileCommand = new Command<LogDataSourceParams<string>?>(this.AddLogFile, this.GetValueAsObservable(IsLogFileNeededProperty));
			this.ClearLogFilesCommand = new Command(this.ClearLogFiles, this.canClearLogFiles);
			this.CopyLogsCommand = new Command<IList<DisplayableLog>>(it => this.CopyLogs(it, false), this.canCopyLogs);
			this.CopyLogsWithFileNamesCommand = new Command<IList<DisplayableLog>>(it => this.CopyLogs(it, true), this.canCopyLogsWithFileNames);
			this.MarkLogsCommand = new Command<MarkingLogsParams>(this.MarkLogs, this.canMarkUnmarkLogs);
			this.MarkUnmarkLogsCommand = new Command<IEnumerable<DisplayableLog>>(this.MarkUnmarkLogs, this.canMarkUnmarkLogs);
			this.PauseResumeLogsReadingCommand = new Command(this.PauseResumeLogsReading, this.canPauseResumeLogsReading);
			this.ReloadLogFileCommand = new Command<LogDataSourceParams<string>?>(this.ReloadLogFile, this.canClearLogFiles);
			this.ReloadLogsCommand = new Command(() => 
			{
				if (this.canReloadLogs.Value)
					this.ScheduleReloadingLogs(false, false);
			}, this.canReloadLogs);
			this.RemoveLogFileCommand = new Command<string?>(this.RemoveLogFile, this.canClearLogFiles);
			this.ResetLogProfileCommand = new Command(this.ResetLogProfile, this.canResetLogProfile);
			this.SaveLogsCommand = new Command<LogsSavingOptions>(this.SaveLogs, this.canSaveLogs);
			this.SearchLogPropertyOnInternetCommand = new Command<Net.SearchProvider>(this.SearchLogPropertyOnInternet, this.canSearchLogPropertyOnInternet);
			this.SetIPEndPointCommand = new Command<IPEndPoint?>(this.SetIPEndPoint, this.GetValueAsObservable(IsIPEndPointNeededProperty));
			this.SetLogProfileCommand = new Command<LogProfile?>(this.SetLogProfile, this.canSetLogProfile);
			this.SetUriCommand = new Command<Uri?>(this.SetUri, this.GetValueAsObservable(IsUriNeededProperty));
			this.SetWorkingDirectoryCommand = new Command<string?>(this.SetWorkingDirectory, this.GetValueAsObservable(IsWorkingDirectoryNeededProperty));
			this.ToggleShowingAllLogsTemporarilyCommand = new Command(this.ToggleShowingAllLogsTemporarily, this.canShowAllLogsTemporarily);
			this.ToggleShowingMarkedLogsTemporarilyCommand = new Command(this.ToggleShowingMarkedLogsTemporarily, this.GetValueAsObservable(HasMarkedLogsProperty));
			this.UnmarkLogsCommand = new Command<IEnumerable<DisplayableLog>>(this.UnmarkLogs, this.canMarkUnmarkLogs);
			this.canSetLogProfile.Update(true);

			// get settings
			this.memoryUsagePolicy = this.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy);

			// create collections
			this.allLogs = new SortedObservableList<DisplayableLog>(this.CompareDisplayableLogs).Also(it =>
			{
				it.CollectionChanged += this.OnAllLogsChanged;
			});
			this.markedLogs = new SortedObservableList<DisplayableLog>(this.CompareDisplayableLogs).Also(it =>
			{
				it.CollectionChanged += this.OnMarkedLogsChanged;
			});

			// setup properties
			this.AllLogs = new SafeReadOnlyList<DisplayableLog>(this.allLogs);
			this.SetValue(LogsProperty, this.AllLogs);
			this.MarkedLogs = new SafeReadOnlyList<DisplayableLog>(this.markedLogs);

			// setup delegates
			this.compareDisplayableLogsDelegate = CompareDisplayableLogsById;

			// create components
			var internalAccessor = new InternalAccessorImpl(this);
			this.LogCategorizing = new LogCategorizingViewModel(this, internalAccessor).Also(it =>
			{
				this.AttachToComponent(it);
			});
			this.LogFiltering = new LogFilteringViewModel(this, internalAccessor).Also(it =>
			{
				this.AttachToComponent(it);
				(it.FilteredLogs as INotifyCollectionChanged)?.Let(it =>
					it.CollectionChanged += this.OnFilteredLogsChanged);
				it.GetValueAsObservable(LogFilteringViewModel.IsFilteringProperty).Subscribe(_ =>
					this.updateIsProcessingLogsAction?.Schedule());
				it.GetValueAsObservable(LogFilteringViewModel.IsFilteringNeededProperty).Subscribe(_ =>
				{
					this.canShowAllLogsTemporarily.Update(this.LogProfile != null && it.IsFilteringNeeded);
					this.selectLogsToReportActions?.Schedule();
				});
			});
			this.LogAnalysis = new LogAnalysisViewModel(this, internalAccessor).Also(it =>
				this.AttachToComponent(it));
			this.LogSelection = new LogSelectionViewModel(this, internalAccessor).Also(it =>
			{
				this.AttachToComponent(it);
				it.GetValueAsObservable(LogSelectionViewModel.SelectedLogStringPropertyValueProperty).Subscribe(value =>
					this.canSearchLogPropertyOnInternet.Update(!string.IsNullOrWhiteSpace(value)));
			});
			this.AllComponentsCreated?.Invoke();

			// create scheduled actions
			this.checkDataSourceErrorsAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				var dataSourceCount = this.logReaders.Count;
				var errorCount = 0;
				if (dataSourceCount == 0)
				{
					this.SetValue(HasAllDataSourceErrorsProperty, this.hasLogDataSourceCreationFailure);
					this.SetValue(HasPartialDataSourceErrorsProperty, false);
					this.checkIsWaitingForDataSourcesAction?.Schedule();
					return;
				}
				foreach (var logReader in this.logReaders)
				{
					if (logReader.DataSource.IsErrorState())
						++errorCount;
				}
				if (errorCount == 0)
				{
					this.SetValue(HasAllDataSourceErrorsProperty, false);
					this.SetValue(HasPartialDataSourceErrorsProperty, false);
				}
				else
				{
					this.SetValue(HasAllDataSourceErrorsProperty, errorCount >= dataSourceCount);
					this.SetValue(HasPartialDataSourceErrorsProperty, errorCount < dataSourceCount);
				}
				this.checkIsWaitingForDataSourcesAction?.Schedule();
			});
			this.checkIsWaitingForDataSourcesAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				var profile = this.LogProfile;
				if (profile == null || this.logReaders.IsEmpty() || this.HasAllDataSourceErrors)
					this.SetValue(IsWaitingForDataSourcesProperty, false);
				else
				{
					var isWaiting = false;
					foreach (var logReader in this.logReaders)
					{
						if (logReader.IsWaitingForDataSource && !logReader.DataSource.CreationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
						{
							isWaiting = true;
							break;
						}
					}
					this.SetValue(IsWaitingForDataSourcesProperty, isWaiting);
				}
			});
			this.checkLogsMemoryUsageAction = new ScheduledAction(() =>
			{
				// check state
				if (this.IsDisposed)
					return;

				// report memory usage
				var prevLogsMemoryUsage = this.LogsMemoryUsage;
				var logsMemoryUsage = (this.displayableLogGroup?.MemorySize).GetValueOrDefault()
					+ Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.allLogs.Count + this.markedLogs.Count);
				foreach (var component in this.attachedComponents)
					logsMemoryUsage += component.MemorySize;
				this.SetValue(LogsMemoryUsageProperty, logsMemoryUsage);
				totalLogsMemoryUsage += (logsMemoryUsage - prevLogsMemoryUsage);
				this.SetValue(TotalLogsMemoryUsageProperty, totalLogsMemoryUsage);

				// hibernate sessions if needed
				hibernateSessionsAction.Schedule();

				// schedule next checking
				this.checkLogsMemoryUsageAction?.Schedule(LogsMemoryUsageCheckInterval);
			});
			this.reloadLogsAction = new(() => this.ReloadLogs(false, false));
			this.reloadLogsFullyAction= new(() => this.ReloadLogs(true, true));
			this.reloadLogsWithRecreatingLogReadersAction = new(() => this.ReloadLogs(true, false));
			this.reloadLogsWithUpdatingVisPropAction = new(() => this.ReloadLogs(false, true));
			this.reportLogsTimeInfoAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				var logs = this.Logs;
				var profile = this.LogProfile;
				if (logs.IsNotEmpty() && profile != null && this.logReaders.IsNotEmpty())
				{
					var firstLog = logs[0];
					var lastLog = logs.Last();
					var duration = (TimeSpan?)null;
					var earliestTimestamp = (DateTime?)null;
					var latestTimestamp = (DateTime?)null;
					var minTimeSpan = (TimeSpan?)null;
					var maxTimeSpan = (TimeSpan?)null;
					if (profile.SortDirection == SortDirection.Ascending)
						duration = CalculateDurationBetweenLogs(firstLog, lastLog, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);
					else
						duration = CalculateDurationBetweenLogs(lastLog, firstLog, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);
					this.SetValue(LogsDurationProperty, duration);
					this.SetValue(MinLogTimeSpanProperty, minTimeSpan);
					this.SetValue(MaxLogTimeSpanProperty, maxTimeSpan);
					this.SetValue(EarliestLogTimestampProperty, earliestTimestamp);
					this.SetValue(LatestLogTimestampProperty, latestTimestamp);
					try
					{
						if (earliestTimestamp != null && latestTimestamp != null)
						{
							var format = profile.TimestampFormatForDisplaying;
							if (format != null)
							{
								this.SetValue(LogsDurationStartingStringProperty, earliestTimestamp.Value.ToString(format));
								this.SetValue(LogsDurationEndingStringProperty, latestTimestamp.Value.ToString(format));
							}
							else
							{
								this.SetValue(LogsDurationStartingStringProperty, earliestTimestamp.Value.ToString());
								this.SetValue(LogsDurationEndingStringProperty, latestTimestamp.Value.ToString());
							}
						}
						else if (minTimeSpan != null && maxTimeSpan != null)
						{
							var format = profile.TimeSpanFormatForDisplaying;
							if (format != null)
							{
								this.SetValue(LogsDurationStartingStringProperty, minTimeSpan.Value.ToString(format));
								this.SetValue(LogsDurationEndingStringProperty, maxTimeSpan.Value.ToString(format));
							}
							else
							{
								this.SetValue(LogsDurationStartingStringProperty, minTimeSpan.Value.ToString());
								this.SetValue(LogsDurationEndingStringProperty, maxTimeSpan.Value.ToString());
							}
						}
						else
						{
							this.SetValue(LogsDurationStartingStringProperty, null);
							this.SetValue(LogsDurationEndingStringProperty, null);
						}
					}
					catch
					{
						this.SetValue(LogsDurationStartingStringProperty, null);
						this.SetValue(LogsDurationEndingStringProperty, null);
					}
				}
				else
				{
					this.SetValue(LogsDurationProperty, null);
					this.SetValue(EarliestLogTimestampProperty, null);
					this.SetValue(LatestLogTimestampProperty, null);
					this.SetValue(LogsDurationStartingStringProperty, null);
					this.SetValue(LogsDurationEndingStringProperty, null);
				}
			});
			this.saveMarkedLogsAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				this.SaveMarkedLogs();
			});
			this.selectLogsToReportActions = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				if (!this.IsShowingAllLogsTemporarily)
				{
					if (this.IsShowingMarkedLogsTemporarily)
					{
						if (this.Application.IsDebugMode)
							this.Logger.LogTrace("Show marked logs");
						this.SetValue(LogsProperty, this.MarkedLogs);
						this.SetValue(HasLogsProperty, this.markedLogs.IsNotEmpty());
						return;
					}
					if (this.LogFiltering.IsFilteringNeeded)
					{
						if (this.Application.IsDebugMode)
							this.Logger.LogTrace("Show filtered logs");
						this.SetValue(LogsProperty, this.LogFiltering.FilteredLogs);
						this.SetValue(HasLogsProperty, this.LogFiltering.FilteredLogs.IsNotEmpty());
						return;
					}
				}
				if (this.Application.IsDebugMode)
					this.Logger.LogTrace("Show all logs");
				this.SetValue(LogsProperty, this.AllLogs);
				this.SetValue(HasLogsProperty, this.allLogs.IsNotEmpty());
				if (!this.LogFiltering.IsFilteringNeeded)
				{
					this.Logger.LogDebug("Trigger GC after clearing log filters");
					TriggerGC();
				}
			});
			this.updateIsReadingLogsAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				if (this.logReaders.IsEmpty())
				{
					this.SetValue(IsReadingLogsProperty, false);
					this.SetValue(LastLogsReadingDurationProperty, null);
					this.logsReadingWatch.Reset();
				}
				else
				{
					foreach (var logReader in this.logReaders)
					{
						switch (logReader.State)
						{
							case LogReaderState.Starting:
							case LogReaderState.ReadingLogs:
								if (!this.logsReadingWatch.IsRunning)
									this.logsReadingWatch.Restart();
								this.SetValue(IsReadingLogsProperty, true);
								return;
						}
					}
					bool wasReadingLogs = this.IsReadingLogs;
					this.SetValue(IsReadingLogsProperty, false);
					if (this.logsReadingWatch.IsRunning)
					{
						this.logsReadingWatch.Stop();
						if (this.LogProfile?.IsContinuousReading != true)
							this.SetValue(LastLogsReadingDurationProperty, TimeSpan.FromMilliseconds(this.logsReadingWatch.ElapsedMilliseconds));
					}
					if (wasReadingLogs)
					{
						this.Logger.LogDebug("Trigger GC after reading logs");
						TriggerGC();
					}
				}
			});
			this.updateIsRemovingLogFilesAction = new(() =>
			{
				if (this.IsDisposed)
					return;
				if (this.logReaders.IsEmpty())
					this.ResetValue(IsRemovingLogFilesProperty);
				else
				{
					var isRemovingLogFiles = false;
					foreach (var logReader in this.logReaders)
					{
						if (!logReader.DataSource.CreationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
							continue;
						if (logReader.State == LogReaderState.ClearingLogs)
						{
							isRemovingLogFiles = true;
							break;
						}
					}
					this.SetValue(IsRemovingLogFilesProperty, isRemovingLogFiles);
				}
			});
			this.updateIsProcessingLogsAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				if (this.IsSavingLogs)
				{
					this.SetValue(IsProcessingLogsProperty, true);
					return;
				}
				var logProfile = this.LogProfile;
				if (logProfile == null || logProfile.IsContinuousReading)
					this.SetValue(IsProcessingLogsProperty, false);
				else if (this.LogFiltering.IsFiltering || this.IsReadingLogs || this.IsRemovingLogFiles)
					this.SetValue(IsProcessingLogsProperty, true);
				else
					this.SetValue(IsProcessingLogsProperty, false);
			});
			this.updateTitleAndIconAction = new ScheduledAction(() =>
			{
				// check state
				if (this.IsDisposed)
					return;

				// select icon
				var app = this.Application as App;
				var logProfile = this.LogProfile;
				var icon = Global.Run(() =>
				{
					if (logProfile == null)
					{
						var res = (object?)null;
						app?.Resources.TryGetResource("Image/Icon.Tab", out res);
						return res as IImage;
					}
					if (app != null)
						return LogProfileIconConverter.Default.Convert(logProfile, typeof(IImage), null, app.CultureInfo) as IImage;
					return null;
				});

				// select title
				var title = Global.Run(() =>
				{
					var customTitle = this.CustomTitle;
					if (logProfile == null)
						return customTitle ?? app?.GetString("Session.Empty");
					if (this.logFileInfoList.IsEmpty() || !logProfile.AllowMultipleFiles)
						return customTitle ?? logProfile.Name;
					return $"{customTitle ?? logProfile.Name} ({this.logFileInfoList.Count})";
				});

				// update properties
				this.SetValue(IconProperty, icon);
				this.SetValue(TitleProperty, title);
			});
			this.checkLogsMemoryUsageAction.Schedule();
			this.updateTitleAndIconAction.Execute();
		
			// attach to log profile manager
			(LogProfileManager.Default.Profiles as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged += this.OnLogProfilesChanged);
			
			// attach to ptoduct manager
			this.SetValue(IsProVersionActivatedProperty, app.ProductManager.IsProductActivated(Products.Professional));
			app.ProductManager.ProductActivationChanged += this.OnProductActivationChanged;

			// restore state
#pragma warning disable CS0612
			if (this.PersistentState.GetRawValue(latestSidePanelSizeKey) is double sidePanelSize)
			{
				this.PersistentState.ResetValue(latestSidePanelSizeKey);
				this.SetValue(LogFilesPanelSizeProperty, sidePanelSize);
				this.SetValue(MarkedLogsPanelSizeProperty, sidePanelSize);
			}
			else
			{
				this.SetValue(LogFilesPanelSizeProperty, this.PersistentState.GetValueOrDefault(latestLogFilesPanelSizeKey));
				this.SetValue(MarkedLogsPanelSizeProperty, this.PersistentState.GetValueOrDefault(latestMarkedLogsPanelSizeKey));
			}
#pragma warning restore CS0612

			// set initial log profile
			if (initLogProfile != null)
			{
				this.Logger.LogWarning("Initial lop profile: '{name}' [{id}]", initLogProfile.Name, initLogProfile.Id);
				this.SetLogProfile(initLogProfile, true, true);
			}
		}


		/// <summary>
		/// Activate session.
		/// </summary>
		/// <returns>Token of activation.</returns>
		public IDisposable Activate()
		{
			this.VerifyAccess();
			return new ActivationToken(this).Also(it =>
			{
				// update activation list
				if (this.activationHistoryListNode.List != null)
					activationHistoryList.Remove(this.activationHistoryListNode);
				activationHistoryList.AddFirst(this.activationHistoryListNode);

				// update state
				this.activationTokens.Add(it);
				if (this.activationTokens.Count > 1)
					return;
				this.Logger.LogWarning("Activate");
				this.SetValue(IsActivatedProperty, true);

				// update log updating interval
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().UpdateInterval = this.ContinuousLogReadingUpdateInterval;

				// check logs memory usage
				this.checkLogsMemoryUsageAction.ExecuteIfScheduled();
				hibernateSessionsAction.Schedule();

				// restore from hibernation
				if (this.IsHibernated)
				{
					this.Logger.LogWarning("Leave hibernation with {savedLogReaderOptionsCount} log reader(s)", this.savedLogReaderOptions.Count);

					// restore log readers
					this.RestoreLogReaders();

					// update state
					this.SetValue(IsHibernatedProperty, false);
				}
			});
		}


		// Add file.
		void AddLogFile(LogDataSourceParams<string>? param)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsLogFileNeeded)
				return;
			if (param == null)
				throw new ArgumentNullException(nameof(param));
			var fileName = param.Source;
			if (fileName == null)
				throw new ArgumentException("No file name specified.");
			if (PathEqualityComparer.Default.Equals(Path.GetExtension(fileName), MarkedFileExtension))
			{
				this.Logger.LogWarning("Ignore adding marked logs info file '{fileName}'", fileName);
				return;
			}
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to add log file.");
			var dataSourceOptions = profile.DataSourceOptions;
			if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				throw new InternalStateCorruptedException($"Cannot add log file because file name is already specified.");
			if (this.logFileInfoList.BinarySearch<LogFileInfo, string>(fileName, it => it.FileName, PathComparer.Default.Compare) >= 0)
			{
				this.Logger.LogWarning("File '{fileName}' is already added", fileName);
				return;
			}
			this.logFileInfoList.Add(new LogFileInfoImpl(this, fileName, param.Precondition));

			this.Logger.LogDebug("Add log file '{fileName}'", fileName);

			// create data source
			dataSourceOptions.FileName = fileName;
			var dataSource = this.CreateLogDataSourceOrNull(profile.DataSourceProvider, dataSourceOptions);
			if (dataSource == null)
				return;
			
			// update state
			this.SetValue(LastLogReadingPreconditionProperty, param.Precondition);

			// create log reader
			this.CreateLogReader(dataSource, param.Precondition);
		}


		/// <summary>
		/// Command to add log file.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="LogDataSourceParams{String}"/>.</remarks>
		public ICommand AddLogFileCommand { get; }


		/// <summary>
		/// Raised when all instances of components are created.
		/// </summary>
		public event Action? AllComponentsCreated;


		/// <summary>
		/// Get number of all read logs.
		/// </summary>
		public int AllLogCount => 
			this.GetValue(AllLogCountProperty);


		/// <summary>
		/// Raised when all log readers have been disposed.
		/// </summary>
		public event Action? AllLogReadersDisposed;


		/// <summary>
		/// Get all logs without filtering.
		/// </summary>
		public IList<DisplayableLog> AllLogs { get; }


		/// <summary>
		/// Check whether logs are based-on one or more files or not.
		/// </summary>
		public bool AreFileBasedLogs => 
			this.GetValue(AreFileBasedLogsProperty);


		/// <summary>
		/// Check whether logs are sorted by one of <see cref="Log.BeginningTimestamp"/>, <see cref="Log.EndingTimestamp"/>, <see cref="Log.Timestamp"/> or not.
		/// </summary>
		public bool AreLogsSortedByTimestamp => 
			this.GetValue(AreLogsSortedByTimestampProperty);


		// Attach to given component.
		void AttachToComponent(SessionComponent component)
		{
			if (this.IsDisposed || !this.attachedComponents.Add(component))
				return;
			component.ErrorMessageGenerated += this.OnComponentErrorMessageGenerated;
		}


		/// <summary>
		/// Calculate duration between given logs.
		/// </summary>
		/// <param name="x">First log.</param>
		/// <param name="y">Second log.</param>
		/// <param name="minTimeSpan">Minimum time span of <paramref name="x"/> and <paramref name="y"/>.</param>
		/// <param name="maxTimeSpan">Maximum time span of <paramref name="x"/> and <paramref name="y"/>.</param>
		/// <param name="earliestTimestamp">Earliest timestamp of <paramref name="x"/> and <paramref name="y"/>.</param>
		/// <param name="latestTimestamp">Latest timestamp of <paramref name="x"/> and <paramref name="y"/>.</param>
		/// <returns>Duration between these logs.</returns>
		public static TimeSpan? CalculateDurationBetweenLogs(DisplayableLog x, DisplayableLog y, out TimeSpan? minTimeSpan, out TimeSpan? maxTimeSpan, out DateTime? earliestTimestamp, out DateTime? latestTimestamp)
		{
			// calculate duration by timestamps
			if (x.TryGetEarliestAndLatestTimestamp(out var eTimestampX, out var lTimestampX)
				&& y.TryGetEarliestAndLatestTimestamp(out var eTimestampY, out var lTimestampY))
			{
				minTimeSpan = null;
				maxTimeSpan = null;
				if (eTimestampX <= lTimestampY)
				{
					earliestTimestamp = eTimestampX;
					latestTimestamp = lTimestampY;
					return lTimestampY.Value - eTimestampX.Value;
				}
				earliestTimestamp = eTimestampY;
				latestTimestamp = lTimestampX;
				return lTimestampX.Value - eTimestampY.Value;
			}
			
			// calculate by time spans
			if (x.TryGetSmallestAndLargestTimeSpan(out var sTimeSpanX, out var lTimeSpanX)
				&& y.TryGetSmallestAndLargestTimeSpan(out var sTimeSpanY, out var lTimeSpanY))
			{
				earliestTimestamp = null;
				latestTimestamp = null;
				if (sTimeSpanX <= lTimeSpanY)
				{
					minTimeSpan = sTimeSpanX;
					maxTimeSpan = lTimeSpanY;
					return lTimeSpanY.Value - sTimeSpanX.Value;
				}
				minTimeSpan = sTimeSpanY;
				maxTimeSpan = lTimeSpanX;
				return lTimeSpanX.Value - sTimeSpanY.Value;
			}

			// no duration available
			earliestTimestamp = null;
			latestTimestamp = null;
			minTimeSpan = null;
			maxTimeSpan = null;
			return null;
		}


		// Clear all log files.
		void ClearLogFiles()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canClearLogFiles.Value)
				return;

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// dispose log readers
			this.DisposeLogReaders();

			// clear file name table
			this.logFileInfoList.Clear();

			// update title
			this.updateTitleAndIconAction.Schedule();
		}


		/// <summary>
		/// Command to clear all added log files.
		/// </summary>
		public ICommand ClearLogFilesCommand { get; }


		// Compare displayable logs.
		internal int CompareDisplayableLogs(DisplayableLog? x, DisplayableLog? y) => this.compareDisplayableLogsDelegate(x, y);
		static int CompareDisplayableLogsByBeginningTimeSpan(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by time span
			var diff = x.BinaryBeginningTimeSpan - y.BinaryBeginningTimeSpan;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsByBeginningTimestamp(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by timestamp
			var diff = x.BinaryBeginningTimestamp - y.BinaryBeginningTimestamp;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsByEndingTimeSpan(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by time span
			var diff = x.BinaryEndingTimeSpan - y.BinaryEndingTimeSpan;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsByEndingTimestamp(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by timestamp
			var diff = x.BinaryEndingTimestamp - y.BinaryEndingTimestamp;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsById(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsByIdNonNull(DisplayableLog x, DisplayableLog y)
		{
			var diff = x.LogId - y.LogId;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;
			return 0;
		}
		static int CompareDisplayableLogsByReadTime(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by timestamp
			var diff = x.BinaryReadTime - y.BinaryReadTime;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsByTimeSpan(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by time span
			var diff = x.BinaryTimeSpan - y.BinaryTimeSpan;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}
		static int CompareDisplayableLogsByTimestamp(DisplayableLog? x, DisplayableLog? y)
		{
			// compare by reference
			if (x == null)
			{
				if (y == null)
					return 0;
				return -1;
			}
			if (y == null)
				return 1;

			// compare by timestamp
			var diff = x.BinaryTimestamp - y.BinaryTimestamp;
			if (diff < 0)
				return -1;
			if (diff > 0)
				return 1;

			// compare by ID
			return CompareDisplayableLogsByIdNonNull(x, y);
		}


		// Interval of updating continuous log reading.
		int ContinuousLogReadingUpdateInterval
		{
			get
			{
				if (this.IsActivated)
					return Math.Max(Math.Min(this.Settings.GetValueOrDefault(SettingKeys.ContinuousLogReadingUpdateInterval), SettingKeys.MaxContinuousLogReadingUpdateInterval), SettingKeys.MinContinuousLogReadingUpdateInterval);
				return 1000;
			}
		}


		// Copy logs.
		async void CopyLogs(IList<DisplayableLog> logs, bool withFileNames)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canCopyLogs.Value || logs.IsEmpty())
				return;
			var app = this.Application as App ?? throw new InternalStateCorruptedException("No application.");

			// sort logs
			if (!logs.IsSorted(this.compareDisplayableLogsDelegate))
				logs = logs.ToArray().Also(it => Array.Sort(it, this.compareDisplayableLogsDelegate));

			// prepare log writer
			using var dataOutput = new StringLogDataOutput(app);
			using var logWriter = this.CreateRawLogWriter(dataOutput);
			var syncLock = new object();
			var isCopyingCompleted = false;
			logWriter.Logs = new Log[logs.Count].Also(it =>
			{
				for (var i = it.Length - 1; i >= 0; --i)
					it[i] = logs[i].Log;
			});
			logWriter.WriteFileNames = withFileNames;
			logWriter.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(RawLogWriter.State))
				{
					switch (logWriter.State)
					{
						case LogWriterState.DataOutputError:
						case LogWriterState.Stopped:
						case LogWriterState.UnclassifiedError:
							lock (syncLock)
							{
								isCopyingCompleted = true;
								Monitor.Pulse(syncLock);
							}
							break;
					}
				}
			};

			// start copying
			this.Logger.LogDebug("Start copying logs");
			this.SetValue(IsCopyingLogsProperty, true);
			logWriter.Start();
			await this.WaitForNecessaryTaskAsync(Task.Run(() =>
			{
				lock (syncLock)
				{
					while (!isCopyingCompleted)
					{
						if (Monitor.Wait(syncLock, 500))
							break;
						Task.Yield();
					}
				}
			}));

			// complete
			if (logWriter.State == LogWriterState.Stopped)
			{
				this.Logger.LogDebug("Logs writing completed, start setting to clipboard");
				var clipboard = app.Clipboard;
				if (clipboard != null)
				{
					await clipboard.SetTextAsync(dataOutput.String ?? "");
					this.Logger.LogDebug("Logs copying completed");
				}
				else
					this.Logger.LogError("Unable to get clipboard");
			}
			else
				this.Logger.LogError("Logs copying failed");
			if (!this.IsDisposed)
				this.SetValue(IsCopyingLogsProperty, false);
		}


		/// <summary>
		/// Command to copy logs.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="IList{DisplayableLog}"/>.</remarks>
		public ICommand CopyLogsCommand { get; }


		/// <summary>
		/// Command to copy logs with file names.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="IList{DisplayableLog}"/>.</remarks>
		public ICommand CopyLogsWithFileNamesCommand { get; }


		// Try creating log data source.
		ILogDataSource? CreateLogDataSourceOrNull(ILogDataSourceProvider provider, LogDataSourceOptions options)
		{
			try
			{
				return provider.CreateSource(options);
			}
			catch (Exception ex)
			{
				this.Logger.LogError(ex, $"Unable to create data source");
				if (!this.hasLogDataSourceCreationFailure)
				{
					this.hasLogDataSourceCreationFailure = true;
					this.checkDataSourceErrorsAction.Schedule();
				}
				return null;
			}
		}


		// Create log reader.
		void CreateLogReader(ILogDataSource dataSource, LogReadingPrecondition precondition)
		{
			// prepare displayable log group
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to create log reader.");
			if (this.displayableLogGroup == null)
			{
				this.displayableLogGroup = new DisplayableLogGroup(profile).Also(it =>
				{
					this.Logger.LogDebug("Create displayable log group '{group}'", it);
					it.ColorIndicatorBrushesUpdated += (_, _) =>
					{
						foreach (var fileInfo in this.logFileInfoMapByLogReader.Values)
							fileInfo.UpdateColorIndicatorBrush();
					};
				});
				this.DisplayableLogGroupCreated?.Invoke(this, EventArgs.Empty);
			}

			// select logs reading task factory
			var readingTaskFactory = dataSource.CreationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName))
				? (fileLogsReadingTaskFactory ?? new TaskFactory(new FixedThreadsTaskScheduler(FileLogsReadingConcurrencyLevel)).Also(it => this.fileLogsReadingTaskFactory = it))
				: defaultLogsReadingTaskFactory;

			// create log reader
			var logReader = new LogReader(dataSource, readingTaskFactory).Also(it =>
			{
				if (profile.IsContinuousReading)
					it.UpdateInterval = this.ContinuousLogReadingUpdateInterval;
				it.IsContinuousReading = profile.IsContinuousReading;
				it.LogLevelMap = profile.LogLevelMapForReading;
				if (profile.LogPatterns.IsNotEmpty())
					it.LogPatterns = profile.LogPatterns;
				else
					it.LogPatterns = new[] { new LogPattern("^(?<Message>.*)", false, false) };
				it.LogStringEncoding = profile.LogStringEncodingForReading;
				if (profile.IsContinuousReading)
				{
					it.MaxLogCount = this.Settings.GetValueOrDefault(SettingKeys.MaxContinuousLogCount);
					it.RestartReadingDelay = TimeSpan.FromMilliseconds(profile.RestartReadingDelay);
				}
				it.Precondition = precondition;
				it.RawLogLevelPropertyName = profile.RawLogLevelPropertyName;
				it.TimeSpanCultureInfo = profile.TimeSpanCultureInfoForReading;
				it.TimeSpanEncoding = profile.TimeSpanEncodingForReading;
				it.TimeSpanFormats = profile.TimeSpanFormatsForReading;
				it.TimestampCultureInfo = profile.TimestampCultureInfoForReading;
				it.TimestampEncoding = profile.TimestampEncodingForReading;
				it.TimestampFormats = profile.TimestampFormatsForReading;
			});
			this.logReaders.Add(logReader);
			this.Logger.LogDebug("Log reader '{logReaderId} created", logReader.Id);

			// add event handlers
			dataSource.MessageGenerated += this.OnLogDataSourceMessageGenerated;
			dataSource.PropertyChanged += this.OnLogDataSourcePropertyChanged;
			if (dataSource is IScriptRunningHost scriptRunningHost)
				scriptRunningHost.ScriptRuntimeErrorOccurred += this.OnLogDataSourceScriptRuntimeErrorOccurred;
			logReader.LogsChanged += this.OnLogReaderLogsChanged;
			logReader.PropertyChanged += this.OnLogReaderPropertyChanged;

			// start reading logs
			logReader.Start();

			// add logs
			var displayableLogGroup = this.displayableLogGroup ?? throw new InternalStateCorruptedException("No displayable log group.");
			logReader.Logs.Let(logs =>
			{
				var displayableLogs = new DisplayableLog[logs.Count].Also(array =>
				{
					for (var i = array.Length - 1; i >= 0; --i)
						array[i] = displayableLogGroup.CreateDisplayableLog(logReader, logs[i]);
				});
				this.allLogs.AddAll(displayableLogs);
				logReader.DataSource.CreationOptions.FileName?.Let(fileName =>
				{
					if (!this.allLogsByLogFilePath.TryGetValue(fileName, out var localLogs))
					{
						localLogs = new List<DisplayableLog>();
						this.allLogsByLogFilePath.Add(fileName, localLogs);
					}
					localLogs.AddRange(displayableLogs);
					this.MatchMarkedLogs();
				});
			});

			// check data source error
			this.checkDataSourceErrorsAction.Schedule();

			// check data source waiting state
			this.checkIsWaitingForDataSourcesAction.Schedule();

			// update title
			this.updateTitleAndIconAction.Schedule();

			// bind to log file info
			var dataSourceProvider = dataSource.Provider;
			var creationOptions = dataSource.CreationOptions;
			var hasFileName = dataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FileName)) 
				&& creationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName));
			if (hasFileName)
			{
				var fileName = creationOptions.FileName.AsNonNull();
				var logFileInfoIndex = this.logFileInfoList.BinarySearch<LogFileInfo, string>(fileName, it => it.FileName, PathComparer.Default.Compare);
				if (logFileInfoIndex >= 0)
					this.logFileInfoMapByLogReader[logReader] = (LogFileInfoImpl)this.logFileInfoList[logFileInfoIndex];
			}

			// update state
			if (this.logFileInfoList.IsNotEmpty() 
				&& !profile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
			{
				this.canClearLogFiles.Update(true);
			}
			this.canMarkUnmarkLogs.Update(true);
			this.canPauseResumeLogsReading.Update(profile.IsContinuousReading);
			this.canReloadLogs.Update(true);
			this.SetValue(UriProperty, creationOptions.Uri);
			if (hasFileName)
			{
				this.SetValue(HasLogFilesProperty, true);
				if (!profile.AllowMultipleFiles)
					this.SetValue(IsLogFileNeededProperty, false);
			}
			if (dataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IPEndPoint)) 
				&& creationOptions.IsOptionSet(nameof(LogDataSourceOptions.IPEndPoint)))
			{
				this.SetValue(IPEndPointProperty, creationOptions.IPEndPoint);
			}
			if (dataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory))
				&& creationOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory)))
			{
				var directory = creationOptions.WorkingDirectory;
				this.SetValue(WorkingDirectoryNameProperty, Path.GetFileName(directory));
				this.SetValue(WorkingDirectoryPathProperty, directory);
				this.SetValue(HasWorkingDirectoryProperty, true);
			}
			this.SetValue(HasLogReadersProperty, true);
		}


		// Create raw log writer for current log profile.
		RawLogWriter CreateRawLogWriter(ILogDataOutput dataOutput)
		{
			// check state
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile.");
			var writingFormats = profile.LogWritingFormats;

			// prepare log writer
			var logWriter = new RawLogWriter(dataOutput)
			{
				LogFormats = writingFormats.IsEmpty()
					? new StringBuilder().Let(it =>
					{
						foreach (var property in this.DisplayLogProperties)
						{
							if (it.Length > 0)
								it.Append(' ');
							var propertyName = property.Name;
							switch (propertyName)
							{
								case nameof(DisplayableLog.BeginningTimeSpanString):
								case nameof(DisplayableLog.EndingTimeSpanString):
								case nameof(DisplayableLog.TimeSpanString):
									it.Append($"{{{propertyName.AsSpan(0, propertyName.Length - 6)}}}");
									break;
								case nameof(DisplayableLog.BeginningTimestampString):
								case nameof(DisplayableLog.EndingTimestampString):
								case nameof(DisplayableLog.ReadTimeString):
								case nameof(DisplayableLog.TimestampString):
									it.Append($"{{{propertyName.AsSpan(0, propertyName.Length - 6)}}}");
									break;
								case nameof(DisplayableLog.LineNumber):
								case nameof(DisplayableLog.ProcessId):
								case nameof(DisplayableLog.ThreadId):
									it.Append($"{{{propertyName},-5}}");
									break;
								default:
									it.Append($"{{{propertyName}}}");
									break;
							}
						}
						return new[] { it.ToString() };
					})
					: writingFormats,
				LogLevelMap = this.displayableLogGroup?.LevelMapForDisplaying ?? profile.LogLevelMapForWriting,
				LogStringEncoding = profile.LogStringEncodingForWriting,
				TimeSpanCultureInfo = profile.TimeSpanCultureInfoForWriting,
				TimeSpanFormat = string.IsNullOrEmpty(profile.TimeSpanFormatForWriting)
					? profile.TimeSpanFormatsForReading.IsEmpty() ? profile.TimeSpanFormatForDisplaying : profile.TimeSpanFormatsForReading[0]
					: profile.TimeSpanFormatForWriting,
				TimestampCultureInfo = profile.TimestampCultureInfoForWriting,
				TimestampFormat = string.IsNullOrEmpty(profile.TimestampFormatForWriting)
					? profile.TimestampFormatsForReading.IsEmpty() ? profile.TimestampFormatForDisplaying : profile.TimestampFormatsForReading[0]
					: profile.TimestampFormatForWriting,
			};
			return logWriter;
		}


		/// <summary>
		/// Get or set custom title.
		/// </summary>
		public string? CustomTitle
        {
			get => this.GetValue(CustomTitleProperty);
			set => this.SetValue(CustomTitleProperty, value);
        }


		// Deactivate.
		void Deactivate(IDisposable token)
		{
			this.VerifyAccess();
			this.activationTokens.Remove(token);
			if (activationTokens.IsEmpty())
			{
				// update state
				this.Logger.LogWarning("Deactivate");
				this.SetValue(IsActivatedProperty, false);

				// update log updating interval
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().UpdateInterval = this.ContinuousLogReadingUpdateInterval;

				// hibernate
				if (this.memoryUsagePolicy == MemoryUsagePolicy.LessMemoryUsage)
					this.Hibernate();
			}
		}


		// Raised when group of displayable log created.
		event EventHandler? DisplayableLogGroupCreated;


		/// <summary>
		/// Get list of property of <see cref="DisplayableLog"/> needed to be shown on UI.
		/// </summary>
		public IList<DisplayableLogProperty> DisplayLogProperties => 
			this.GetValue(DisplayLogPropertiesProperty);


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// ignore managed resources
			if (!disposing)
			{
				base.Dispose(disposing);
				return;
			}

			// check thread
			this.VerifyAccess();

			// dispose components
			foreach (var component in this.attachedComponents.ToArray())
				this.DisposeComponent(component);

			// cancel scheduled reloading logs
			this.reloadLogsAction.Cancel();
			this.reloadLogsFullyAction.Cancel();
			this.reloadLogsWithRecreatingLogReadersAction.Cancel();
			this.reloadLogsWithUpdatingVisPropAction.Cancel();

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// dispose log readers
			this.DisposeLogReaders();

			// release log group
			totalLogsMemoryUsage -= this.displayableLogGroup?.MemorySize ?? 0L;
			this.Logger.LogDebug("Dispose displayable log group '{group}'", this.displayableLogGroup);
			this.displayableLogGroup = this.displayableLogGroup.DisposeAndReturnNull();
			this.checkLogsMemoryUsageAction.Cancel();

			// detach from log profile
			this.LogProfile?.Let(it => it.PropertyChanged -= this.OnLogProfilePropertyChanged);

			// detach from log data source provider
			if (this.attachedLogDataSourceProvider != null)
			{
				this.attachedLogDataSourceProvider.PropertyChanged -= this.OnLogDataSourceProviderPropertyChanged;
				this.attachedLogDataSourceProvider = null;
			}

			// detach from log profile manager
			(LogProfileManager.Default.Profiles as INotifyCollectionChanged)?.Let(it =>
				it.CollectionChanged -= this.OnLogProfilesChanged);
			
			// detach from ptoduct manager
			this.Application.ProductManager.ProductActivationChanged -= this.OnProductActivationChanged;

			// stop watches
			this.logsReadingWatch.Stop();

			// dispose task factories
			(this.fileLogsReadingTaskFactory?.Scheduler as IDisposable)?.Dispose();

			// remove from activation history
			if (this.activationHistoryListNode.List != null)
				activationHistoryList.Remove(this.activationHistoryListNode);

			// call base
			base.Dispose(disposing);
		}


		// Detach from and dispose given component.
		void DisposeComponent(SessionComponent component)
		{
			if (!this.attachedComponents.Remove(component))
				return;
			component.ErrorMessageGenerated -= this.OnComponentErrorMessageGenerated;
			component.Dispose();
		}


		// Dispose displayable logs.
		static void DisposeDisplayableLogs(IEnumerable<DisplayableLog> logs)
		{
			if (logs is ICollection<DisplayableLog> collection && collection.IsEmpty())
				return;
			displayableLogsToDispose.AddRange(logs);
			disposeDisplayableLogsAction.Schedule(DisposeDisplayableLogsInterval);
		}


		// Dispose log reader.
		void DisposeLogReader(LogReader logReader, bool removeLogs)
		{
			// remove from set
			if (!this.logReaders.Remove(logReader))
				return;

			// remove event handlers
			var dataSource = logReader.DataSource;
			dataSource.MessageGenerated -= this.OnLogDataSourceMessageGenerated;
			dataSource.PropertyChanged -= this.OnLogDataSourcePropertyChanged;
			if (dataSource is IScriptRunningHost scriptRunningHost)
				scriptRunningHost.ScriptRuntimeErrorOccurred -= this.OnLogDataSourceScriptRuntimeErrorOccurred;
			logReader.LogsChanged -= this.OnLogReaderLogsChanged;
			logReader.PropertyChanged -= this.OnLogReaderPropertyChanged;

			// remove logs
			if (removeLogs)
			{
				this.allLogs.RemoveAll(it => it.LogReader == logReader);
				logReader.DataSource.CreationOptions.FileName?.Let(fileName => this.allLogsByLogFilePath.Remove(fileName));
			}

			// dispose data source and log reader
			logReader.Dispose();
			dataSource.Dispose();
			this.Logger.LogDebug("Log reader '{logReaderId}' disposed", logReader.Id);

			// check data source error
			this.checkDataSourceErrorsAction.Schedule();

			// check data source waiting state
			this.checkIsWaitingForDataSourcesAction.Schedule();

			// unbind from log file info
			this.logFileInfoMapByLogReader.Remove(logReader);

			// update state
			this.updateIsReadingLogsAction.Execute();
			this.updateIsProcessingLogsAction.Execute();
			if (this.logReaders.IsEmpty())
			{
				this.Logger.LogDebug($"The last log reader disposed");
				if (!this.IsDisposed)
				{
					var profile = this.LogProfile;
					this.reportLogsTimeInfoAction.Execute();
					this.canClearLogFiles.Update(false);
					this.canMarkUnmarkLogs.Update(false);
					this.canPauseResumeLogsReading.Update(false);
					this.canReloadLogs.Update(false);
					this.SetValue(HasLogFilesProperty, false);
					this.SetValue(HasLogReadersProperty, false);
					this.SetValue(HasWorkingDirectoryProperty, false);
					this.SetValue(IsLogFileNeededProperty, profile != null 
						&& profile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName))
						&& !profile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)));
					this.SetValue(IPEndPointProperty, null);
					this.SetValue(IsLogsReadingPausedProperty, false);
					this.SetValue(UriProperty, null);
					this.SetValue(WorkingDirectoryNameProperty, null);
					this.SetValue(WorkingDirectoryPathProperty, null);
					this.AllLogReadersDisposed?.Invoke();
				}
			}
		}


		// Dispose all log readers.
		void DisposeLogReaders()
		{
			// dispose log readers
			foreach (var logReader in this.logReaders.ToArray())
				this.DisposeLogReader(logReader, false);

			// clear data source error
			this.hasLogDataSourceCreationFailure = false;
			this.checkDataSourceErrorsAction.Execute();

			// clear logs
			DisposeDisplayableLogs(this.allLogs);
			this.allLogs.Clear();
			this.allLogsByLogFilePath.Clear();

			// release log group
			if (this.IsDisposed)
			{
				var logsMemoryUsage = this.LogsMemoryUsage;
				totalLogsMemoryUsage -= logsMemoryUsage;
			}
			else
				this.checkLogsMemoryUsageAction.Execute();
			this.displayableLogGroup = this.displayableLogGroup.DisposeAndReturnNull();
		}


		/// <summary>
		/// Get earliest timestamp of log in <see cref="Logs"/>.
		/// </summary>
		public DateTime? EarliestLogTimestamp => 
			this.GetValue(EarliestLogTimestampProperty);


		/// <summary>
		/// Raised when error message generated.
		/// </summary>
		public event EventHandler<MessageEventArgs>? ErrorMessageGenerated;


		/// <summary>
		/// Raised when one or more external dependencies not found.
		/// </summary>
		public event EventHandler? ExternalDependencyNotFound;


		/// <summary>
		/// Get number of filtered logs.
		/// </summary>
		public int FilteredLogCount => 
			this.GetValue(FilteredLogCountProperty);


		/// <summary>
		/// Find first and last log from given logs according to sorting parameters defined in <see cref="LogProfile"/>.
		/// </summary>
		/// <param name="logs">Logs to find from.</param>
		/// <param name="firstLog">Found first log.</param>
		/// <param name="lastLog">Found last log.</param>
		public void FindFirstAndLastLog(IEnumerable<DisplayableLog> logs, out DisplayableLog? firstLog, out DisplayableLog? lastLog)
		{
			firstLog = null;
			lastLog = null;
			var profile = this.LogProfile;
			if (profile == null)
				return;
			var compare = this.compareDisplayableLogsDelegate;
			if (profile.SortDirection == SortDirection.Ascending)
			{
				foreach (var log in logs)
				{
					if (firstLog == null || compare(log, firstLog) < 0)
						firstLog = log;
					if (lastLog == null || compare(log, lastLog) > 0)
						lastLog = log;
				}
			}
			else
			{
				foreach (var log in logs)
				{
					if (firstLog == null || compare(log, firstLog) > 0)
						firstLog = log;
					if (lastLog == null || compare(log, lastLog) < 0)
						lastLog = log;
				}
			}
		}


		/// <summary>
		/// Find log which is the nearest one to the given timestamp.
		/// </summary>
		/// <param name="timestamp">Timestamp.</param>
		/// <returns>Found log.</returns>
		public DisplayableLog? FindNearestLog(DateTime timestamp)
        {
			// check state
			this.VerifyAccess();
			var profile = this.LogProfile;
			if (profile == null)
				return null;
			var logs = this.Logs;
			if (logs.IsEmpty())
				return null;

			// prepare comparison
			var timestampGetter = profile.SortKey switch
			{
				LogSortKey.BeginningTimestamp => log => log.BinaryBeginningTimestamp,
				LogSortKey.EndingTimestamp => log => log.BinaryEndingTimestamp,
				LogSortKey.Timestamp => log => log.BinaryTimestamp,
				_ => default(Func<DisplayableLog, long>?),
			};
			if (timestampGetter == null)
				return null;
			var comparison = new Comparison<long>((x, y) => x.CompareTo(y));
			if (profile.SortDirection == SortDirection.Descending)
				comparison = comparison.Invert();

			// find log
			var index = logs.BinarySearch(timestamp.ToBinary(), timestampGetter, comparison);
			if (index < 0)
				index = Math.Min(logs.Count - 1, ~index);
			return logs[index];
		}


		/// <summary>
		/// Check whether errors are found in all data sources or not.
		/// </summary>
		public bool HasAllDataSourceErrors => 
			this.GetValue(HasAllDataSourceErrorsProperty);


		/// <summary>
		/// Check whether <see cref="CustomTitle"/> has been set or not.
		/// </summary>
		public bool HasCustomTitle => 
			this.GetValue(HasCustomTitleProperty);


		/// <summary>
		/// Check whether <see cref="IPEndPoint"/> is non-null or not.
		/// </summary>
		public bool HasIPEndPoint => 
			this.GetValue(HasIPEndPointProperty);


		/// <summary>
		/// Check whether <see cref="LastLogsReadingDuration"/> is valid or not.
		/// </summary>
		public bool HasLastLogsReadingDuration => 
			this.GetValue(HasLastLogsReadingDurationProperty);


		/// <summary>
		/// Check whether color indicator of log is needed or not.
		/// </summary>
		public bool HasLogColorIndicator => 
			this.GetValue(HasLogColorIndicatorProperty);


		/// <summary>
		/// Check whether color indicator of log by file name is needed or not.
		/// </summary>
		public bool HasLogColorIndicatorByFileName => 
			this.GetValue(HasLogColorIndicatorByFileNameProperty);


		/// <summary>
		/// Check whether at least one log file was added to session or not.
		/// </summary>
		public bool HasLogFiles => 
			this.GetValue(HasLogFilesProperty);


		/// <summary>
		/// Check whether <see cref="LogProfile"/> is valid or not.
		/// </summary>
		public bool HasLogProfile => 
			this.GetValue(HasLogProfileProperty);


		/// <summary>
		/// Check whether at least one log reader created or not.
		/// </summary>
		public bool HasLogReaders => 
			this.GetValue(HasLogReadersProperty);


		/// <summary>
		/// Check whether at least one log is read or not.
		/// </summary>
		public bool HasLogs => 
			this.GetValue(HasLogsProperty);


		/// <summary>
		/// Check whether <see cref="LogsDuration"/> is valid or not.
		/// </summary>
		public bool HasLogsDuration => 
			this.GetValue(HasLogsDurationProperty);


		/// <summary>
		/// Check whether at least one log has been marked or not.
		/// </summary>
		public bool HasMarkedLogs => 
			this.GetValue(HasMarkedLogsProperty);


		/// <summary>
		/// Check whether errors are found in some of data sources or not.
		/// </summary>
		public bool HasPartialDataSourceErrors =>
			this.GetValue(HasPartialDataSourceErrorsProperty);


		/// <summary>
		/// Check whether URI has been set or not.
		/// </summary>
		public bool HasUri =>
			this.GetValue(HasUriProperty);


		/// <summary>
		/// Check whether at least one property of <see cref="DisplayableLog"/> which represents timestamp will be shown in UI or not.
		/// </summary>
		public bool HasTimestampDisplayableLogProperty => 
			this.GetValue(HasTimestampDisplayableLogPropertyProperty);


		/// <summary>
		/// Check whether working directory has been set or not.
		/// </summary>
		public bool HasWorkingDirectory => 
			this.GetValue(HasWorkingDirectoryProperty);


		// Hibernate the session.
		bool Hibernate()
        {
			// check state
			if (this.IsActivated)
				return false;
			if (this.IsHibernated)
				return true;

			// check log profile
			var profile = this.LogProfile;
			if (profile == null || profile.IsContinuousReading)
				return false;

			this.Logger.LogWarning("Hibernate with {logReaderCount} log reader(s) and {allLogCount} log(s)", this.logReaders.Count, this.AllLogCount);

			// update state
			this.SetValue(IsHibernatedProperty, true);

			// save log readers
			this.SaveLogReaders();

			// dispose log readers
			this.DisposeLogReaders();

			// complete
			return true;
        }


		/// <summary>
		/// Get icon of session.
		/// </summary>
		public IImage? Icon => 
			this.GetValue(IconProperty);


		/// <summary>
		/// Get current <see cref="IPEndPoint"/> to read logs from.
		/// </summary>
		public IPEndPoint? IPEndPoint => 
			this.GetValue(IPEndPointProperty);


		/// <summary>
		/// Check whether session has been activated or not.
		/// </summary>
		public bool IsActivated => 
			this.GetValue(IsActivatedProperty);


		/// <summary>
		/// Check whether logs are being analyzed or not.
		/// </summary>
		public bool IsAnalyzingLogs => 
			this.GetValue(IsAnalyzingLogsProperty);


		/// <summary>
		/// Check whether current log profile is a built-in log profile or not.
		/// </summary>
		public bool IsBuiltInLogProfile => 
			this.GetValue(IsBuiltInLogProfileProperty);


		/// <summary>
		/// Check whether logs copying is on-going or not.
		/// </summary>
		public bool IsCopyingLogs => 
			this.GetValue(IsCopyingLogsProperty);


		/// <summary>
		/// Check whether session is hibernated or not.
		/// </summary>
		public bool IsHibernated => 
			this.GetValue(IsHibernatedProperty);


		/// <summary>
		/// Check whether IP endpoint is needed or not.
		/// </summary>
		public bool IsIPEndPointNeeded => 
			this.GetValue(IsIPEndPointNeededProperty);


		/// <summary>
		/// Check whether given log file has been added or not.
		/// </summary>
		/// <param name="filePath">Path of log file.</param>
		/// <returns>True if log file has been added.</returns>
		public bool IsLogFileAdded(string? filePath) =>
			filePath != null && this.logFileInfoList.BinarySearch<LogFileInfo, string>(filePath, it => it.FileName, PathComparer.Default.Compare) >= 0;


		/// <summary>
		/// Check whether logs file is needed or not.
		/// </summary>
		public bool IsLogFileNeeded => 
			this.GetValue(IsLogFileNeededProperty);


		/// <summary>
		/// Get or set whether panel of added log files is visible or not.
		/// </summary>
		public bool IsLogFilesPanelVisible 
		{
			 get => this.GetValue(IsLogFilesPanelVisibleProperty);
			 set => this.SetValue(IsLogFilesPanelVisibleProperty, value);
		}


		/// <summary>
		/// Check whether logs file is supported or not.
		/// </summary>
		public bool IsLogFileSupported => 
			this.GetValue(IsLogFileSupportedProperty);


		/// <summary>
		/// Check whether logs reading has been paused or not.
		/// </summary>
		public bool IsLogsReadingPaused => 
			this.GetValue(IsLogsReadingPausedProperty);


		/// <summary>
		/// Get or set whether panel of marked logs is visible or not.
		/// </summary>
		public bool IsMarkedLogsPanelVisible 
		{
			 get => this.GetValue(IsMarkedLogsPanelVisibleProperty);
			 set => this.SetValue(IsMarkedLogsPanelVisibleProperty, value);
		}


		/// <summary>
		/// Check whether logs are being processed or not.
		/// </summary>
		public bool IsProcessingLogs => 
			this.GetValue(IsProcessingLogsProperty);
		

		/// <summary>
		/// Check whether Pro-version is activated or not.
		/// </summary>
		public bool IsProVersionActivated => 
			this.GetValue(IsProVersionActivatedProperty);


		/// <summary>
		/// Check whether logs are being read or not.
		/// </summary>
		public bool IsReadingLogs => 
			this.GetValue(IsReadingLogsProperty);


		/// <summary>
		/// Check whether logs are being read continuously or not.
		/// </summary>
		public bool IsReadingLogsContinuously =>
			this.GetValue(IsReadingLogsContinuouslyProperty);


		/// <summary>
		/// Check whether one or more log files are being removed or not.
		/// </summary>
		public bool IsRemovingLogFiles =>
			this.GetValue(IsRemovingLogFilesProperty);


		/// <summary>
		/// Check whether logs saving is on-going or not.
		/// </summary>
		public bool IsSavingLogs => 
			this.GetValue(IsSavingLogsProperty);


		/// <summary>
		/// Check whether all logs are shown temporarily.
		/// </summary>
		public bool IsShowingAllLogsTemporarily => 
			this.GetValue(IsShowingAllLogsTemporarilyProperty);


		/// <summary>
		/// Check whether showing marked logs temporarily or not.
		/// </summary>
		public bool IsShowingMarkedLogsTemporarily =>
			this.GetValue(IsShowingMarkedLogsTemporarilyProperty);


		/// <summary>
		/// Check whether URI is needed or not.
		/// </summary>
		public bool IsUriNeeded => 
			this.GetValue(IsUriNeededProperty);


		/// <summary>
		/// Check data sources are not ready for reading logs.
		/// </summary>
		public bool IsWaitingForDataSources =>
			this.GetValue(IsWaitingForDataSourcesProperty);


		/// <summary>
		/// Check whether working directory is needed or not.
		/// </summary>
		public bool IsWorkingDirectoryNeeded => 
			this.GetValue(IsWorkingDirectoryNeededProperty);


		/// <summary>
		/// Get the last precondition of log reading.
		/// </summary>
		public LogReadingPrecondition LastLogReadingPrecondition => 
			this.GetValue(LastLogReadingPreconditionProperty);


		/// <summary>
		/// Get the duration of last logs reading.
		/// </summary>
		public TimeSpan? LastLogsReadingDuration =>
			this.GetValue(LastLogsReadingDurationProperty);


		/// <summary>
		/// Get latest timestamp of log in <see cref="Logs"/>.
		/// </summary>
		public DateTime? LatestLogTimestamp => 
			this.GetValue(LatestLogTimestampProperty);


		// Load marked logs from file.
		async void LoadMarkedLogs(string fileName)
		{
			this.Logger.LogTrace("Request loading marked log(s) of '{fileName}'", fileName);

			// load marked logs from file
			var markedLogInfos = await ioTaskFactory.StartNew(() =>
			{
				var markedLogInfos = new List<MarkedLogInfo>();
				var markedFileName = fileName + MarkedFileExtension;
				if (!System.IO.File.Exists(markedFileName))
				{
					this.Logger.LogTrace("Marked log file '{markedFileName}' not found", markedFileName);
					return Array.Empty<MarkedLogInfo>();
				}
				try
				{
					this.Logger.LogTrace("Start loading marked log file '{markedFileName}'", markedFileName);
					if (!CarinaStudio.IO.File.TryOpenRead(markedFileName, DefaultFileOpeningTimeout, out var stream) || stream == null)
					{
						this.Logger.LogError("Unable to open marked file to load: {markedFileName}", markedFileName);
						return Array.Empty<MarkedLogInfo>();
					}
					using (stream)
					{
						JsonDocument.Parse(stream).Use(jsonDocument =>
						{
							foreach (var jsonProperty in jsonDocument.RootElement.EnumerateObject())
							{
								switch (jsonProperty.Name)
								{
									case "MarkedLogInfos":
										{
											foreach (var jsonObject in jsonProperty.Value.EnumerateArray())
											{
												var color = MarkColor.Default;
												if (jsonObject.TryGetProperty("MarkedColor", out var colorProperty) 
													&& colorProperty.ValueKind == JsonValueKind.String
													&& Enum.TryParse<MarkColor>(colorProperty.GetString(), out var parsedColor))
												{
													color = parsedColor;
												}
												var lineNumber = jsonObject.GetProperty("MarkedLineNumber").GetInt32();
												var timestamp = (DateTime?)null;
												if (jsonObject.TryGetProperty("MarkedTimestamp", out var timestampElement))
													timestamp = DateTime.Parse(timestampElement.GetString().AsNonNull());
												markedLogInfos.Add(new MarkedLogInfo(fileName, lineNumber, timestamp, color));
											}
											break;
										}
								}
							}
							return 0;
						});
					}
					this.Logger.LogTrace("Complete loading marked log file '{markedFileName}', {markedLogInfoCount} marked log(s) found", markedFileName, markedLogInfos.Count);
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, "Unable to load marked log file: {markedFileName}", markedFileName);
					return Array.Empty<MarkedLogInfo>();
				}
				return markedLogInfos.ToArray();
			});

			// check state
			if (markedLogInfos.IsEmpty())
				return;
			if (this.logFileInfoList.BinarySearch<LogFileInfo, string>(fileName, it => it.FileName, PathComparer.Default.Compare) < 0)
				return;

			// add as unmatched 
			this.unmatchedMarkedLogInfos.RemoveAll(it => PathEqualityComparer.Default.Equals(it.FileName, fileName));
			this.unmatchedMarkedLogInfos.AddRange(markedLogInfos);

			// match
			this.MatchMarkedLogs();
		}


		/// <summary>
		/// Get view-model of log analysis.
		/// </summary>
		public LogAnalysisViewModel LogAnalysis { get; }


		/// <summary>
		/// Get view-model of log categorizing.
		/// </summary>
		/// <value></value>
		public LogCategorizingViewModel LogCategorizing { get; }


		/// <summary>
		/// Raised when runtime error occurred by script of log data source.
		/// </summary>
		public event EventHandler<ScriptRuntimeErrorEventArgs>? LogDataSourceScriptRuntimeErrorOccurred;


		/// <summary>
		/// Get or set size of panel of added log files.
		/// </summary>
		public double LogFilesPanelSize
		{
			 get => this.GetValue(LogFilesPanelSizeProperty);
			 set => this.SetValue(LogFilesPanelSizeProperty, value);
		}


		/// <summary>
		/// Get view-model of log filtering.
		/// </summary>
		public LogFilteringViewModel LogFiltering { get; }


		/// <summary>
		/// Get current log profile.
		/// </summary>
		public LogProfile? LogProfile => 
			this.GetValue(LogProfileProperty);


		/// <summary>
		/// Get list of <see cref="DisplayableLog"/>s to display.
		/// </summary>
		public IList<DisplayableLog> Logs => 
			this.GetValue(LogsProperty);


		/// <summary>
		/// Get view-model of log selection.
		/// </summary>
		public LogSelectionViewModel LogSelection { get; }


		/// <summary>
		/// Get duration of <see cref="Logs"/>.
		/// </summary>
		public TimeSpan? LogsDuration => 
			this.GetValue(LogsDurationProperty);


		/// <summary>
		/// Get string to describe ending point of <see cref="LogsDuration"/>.
		/// </summary>
		public string? LogsDurationEndingString => 
			this.GetValue(LogsDurationEndingStringProperty);


		/// <summary>
		/// Get string to describe starting point of <see cref="LogsDuration"/>.
		/// </summary>
		public string? LogsDurationStartingString => 
			this.GetValue(LogsDurationStartingStringProperty);


		/// <summary>
		/// Get size of memory usage of logs by the <see cref="Session"/> instance in bytes.
		/// </summary>
		public long LogsMemoryUsage => 
			this.GetValue(LogsMemoryUsageProperty);


		/// <summary>
		/// Get list of marked <see cref="DisplayableLog"/>s .
		/// </summary>
		public IList<DisplayableLog> MarkedLogs { get; }


		/// <summary>
		/// Get or set size of panel of marked logs.
		/// </summary>
		public double MarkedLogsPanelSize
		{
			 get => this.GetValue(MarkedLogsPanelSizeProperty);
			 set => this.SetValue(MarkedLogsPanelSizeProperty, value);
		}


		// Mark logs.
		void MarkLogs(MarkingLogsParams parameters)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canMarkUnmarkLogs.Value)
				return;
			var color = parameters.Color;
			if (color == MarkColor.None)
				color = MarkColor.Default;
			foreach (var log in parameters.Logs)
			{
				if (log.MarkedColor != color)
				{
					if (log.MarkedColor == MarkColor.None)
					{
						log.MarkedColor = color;
						this.markedLogs.Add(log);
						this.LogFiltering.InvalidateLog(log);
					}
					else
						log.MarkedColor = color;
					log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
				}
			}

			// schedule save to file action
			this.saveMarkedLogsAction.Schedule(DelaySaveMarkedLogs);
		}


		/// <summary>
		/// Command to mark logs.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="MarkingLogsParams"/>.</remarks>
		public ICommand MarkLogsCommand { get; }


		// Mark or unmark logs.
		void MarkUnmarkLogs(IEnumerable<DisplayableLog> logs)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canMarkUnmarkLogs.Value)
				return;
			var allLogsAreMarked = true;
			var isShowingAllLogsTemporarily = this.IsShowingAllLogsTemporarily;
			foreach (var log in logs)
			{
				if (log.MarkedColor == MarkColor.None)
				{
					allLogsAreMarked = false;
					break;
				}
			}
			if (allLogsAreMarked)
			{
				this.Logger.LogTrace("Unmark log(s)");

				foreach (var log in logs)
				{
					if (log.MarkedColor != MarkColor.None)
					{
						log.MarkedColor = MarkColor.None;
						this.markedLogs.Remove(log);
						if (isShowingAllLogsTemporarily)
							this.LogFiltering.InvalidateLog(log);
						log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
					}
				}
			}
			else
			{
				this.Logger.LogTrace("Mark log(s)");
				foreach (var log in logs)
				{
					if(log.MarkedColor == MarkColor.None)
					{
						log.MarkedColor = MarkColor.Default;
						this.markedLogs.Add(log);
						this.LogFiltering.InvalidateLog(log);
						log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
					}
				}
			}

			// schedule save to file action
			this.saveMarkedLogsAction.Schedule(DelaySaveMarkedLogs);
		}


		/// <summary>
		/// Command to mark or unmark logs.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="IEnumerable{DisplayableLog}"/></remarks>
		public ICommand MarkUnmarkLogsCommand { get; }


		// Match marked logs.
		void MatchMarkedLogs()
		{
			// match
			if (this.unmatchedMarkedLogInfos.IsEmpty())
			{
				this.Logger.LogTrace("All marked logs were matched");
				return;
			}
			var logList = (List<DisplayableLog>?)null;
			var logListFileName = "";
			for (var i = this.unmatchedMarkedLogInfos.Count - 1 ; i >= 0 ; i--)
			{
				var markedLogInfo = this.unmatchedMarkedLogInfos[i];
				if (!PathEqualityComparer.Default.Equals(markedLogInfo.FileName, logListFileName))
				{
					logListFileName = markedLogInfo.FileName;
					this.allLogsByLogFilePath.TryGetValue(logListFileName, out logList);
				}
				if (logList == null)
					continue;
				var index = logList.BinarySearch(markedLogInfo.LineNumber, it => it.LineNumber.GetValueOrDefault(), (x, y) => x - y);
				if (index >= 0)
				{
					var log = logList[index];
					if (log.MarkedColor == MarkColor.None)
					{
						log.MarkedColor = markedLogInfo.Color;
						this.markedLogs.Add(log);
						this.LogFiltering.InvalidateLog(log);
					}
					this.unmatchedMarkedLogInfos.RemoveAt(i);
				}
			}
			if (this.unmatchedMarkedLogInfos.IsEmpty())
				this.Logger.LogTrace("All marked logs are matched");
			else
				this.Logger.LogTrace("{unmatchedMarkedLogInfoCount} marked log(s) are unmatched", this.unmatchedMarkedLogInfos.Count);
		}


		/// <summary>
		/// Get maximum time span of log in <see cref="Logs"/>.
		/// </summary>
		public TimeSpan? MaxLogTimeSpan => 
			this.GetValue(MaxLogTimeSpanProperty);


		/// <summary>
		/// Get minimum time span of log in <see cref="Logs"/>.
		/// </summary>
		public TimeSpan? MinLogTimeSpan => 
			this.GetValue(MinLogTimeSpanProperty);


		// Called when logs in allLogs has been changed.
		void OnAllLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Remove:
					this.markedLogs.RemoveAll(e.OldItems.AsNonNull().Cast<DisplayableLog>(), true);
					DisposeDisplayableLogs(e.OldItems.AsNonNull().Cast<DisplayableLog>());
					if (this.allLogs.IsEmpty() && this.memoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
						this.allLogs.TrimExcess();
					break;
				case NotifyCollectionChangedAction.Reset:
					this.markedLogs.Clear();
					if (this.allLogs.IsNotEmpty())
						this.markedLogs.AddAll(this.allLogs.TakeWhile(it => it.MarkedColor != MarkColor.None));
					else if (this.memoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
						this.allLogs.TrimExcess();
					break;
			}
			if (!this.IsDisposed)
			{
				if ((!this.LogFiltering.IsFilteringNeeded && !this.IsShowingMarkedLogsTemporarily) || this.IsShowingAllLogsTemporarily)
				{
					this.SetValue(HasLogsProperty, this.allLogs.IsNotEmpty());
					this.reportLogsTimeInfoAction.Schedule(LogsTimeInfoReportingInterval);
				}
				this.SetValue(AllLogCountProperty, this.allLogs.Count);
			}
		}


		// Called when string resources updated.
		protected override void OnApplicationStringsUpdated()
		{
			base.OnApplicationStringsUpdated();
			this.updateTitleAndIconAction.Schedule();
		}


		// Called when error message generated by component.
		void OnComponentErrorMessageGenerated(object? sender, MessageEventArgs e) =>
			this.ErrorMessageGenerated?.Invoke(this, e);


		// Called when filtered logs has been changed.
		void OnFilteredLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!this.IsDisposed)
			{
				if (this.LogFiltering.IsFilteringNeeded && !this.IsShowingAllLogsTemporarily && !this.IsShowingMarkedLogsTemporarily)
				{
					this.SetValue(HasLogsProperty, this.LogFiltering.FilteredLogs.IsNotEmpty());
					this.reportLogsTimeInfoAction.Schedule(LogsTimeInfoReportingInterval);
				}
				this.SetValue(FilteredLogCountProperty, this.LogFiltering.FilteredLogs.Count);
			}
		}


		// Called when message generated by log data source.
		void OnLogDataSourceMessageGenerated(ILogDataSource source, LogDataSourceMessage message)
		{
			if (message.Type != LogDataSourceMessageType.Error)
				return;
			foreach (var logReader in this.logReaders)
			{
				if (logReader.DataSource == source)
				{
					this.ErrorMessageGenerated?.Invoke(this, new MessageEventArgs(message.Message));
					break;
				}
			}
		}


		// Called when property of log data source changed.
		void OnLogDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataSource.State))
				this.checkDataSourceErrorsAction.Schedule();
		}


		// Called when property of log data source provider changed.
		void OnLogDataSourceProviderPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is ScriptLogDataSourceProvider)
			{
				switch (e.PropertyName)
				{
					case nameof(ScriptLogDataSourceProvider.ClosingReaderScript):
					case nameof(ScriptLogDataSourceProvider.OpeningReaderScript):
					case nameof(ScriptLogDataSourceProvider.ReadingLineScript):
						if (!this.reloadLogsFullyAction.IsScheduled)
						{
							this.reloadLogsAction.Cancel();
							if (this.reloadLogsWithUpdatingVisPropAction.IsScheduled)
							{
								this.reloadLogsWithUpdatingVisPropAction.Cancel();
								this.reloadLogsFullyAction.Schedule();
							}
							else
								this.reloadLogsWithRecreatingLogReadersAction.Schedule();
						}
						break;
					case nameof(ILogDataSourceProvider.RequiredSourceOptions):
					case nameof(ILogDataSourceProvider.SupportedSourceOptions):
						this.DisposeLogReaders();
						this.UpdateDataSourceRelatedStates();
						this.StartReadingLogs();
						break;
				}
			}
		}


		// Called when runtime error occurred by script pf log data source.
		void OnLogDataSourceScriptRuntimeErrorOccurred(object? sender, ScriptRuntimeErrorEventArgs e) =>
			this.LogDataSourceScriptRuntimeErrorOccurred?.Invoke(this, e);


		// Called when collection of log profiles changed.
		void OnLogProfilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Remove:
				case NotifyCollectionChangedAction.Replace:
				case NotifyCollectionChangedAction.Reset:
					this.LogProfile?.Let(profile =>
					{
						if (!LogProfileManager.Default.Profiles.Contains(profile))
						{
							this.Logger.LogWarning("Log profile '{profileName}' ({profileId}) has been removed, reset log profile", profile.Name, profile.Id);
							this.ResetLogProfile();
						}
					});
					break;
			}
		}


		// Called when property of log profile changed.
		void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender != this.LogProfile)
				return;
			switch (e.PropertyName)
			{
				case nameof(LogProfile.AllowMultipleFiles):
					(sender as LogProfile)?.Let(profile =>
					{
						if (this.AreFileBasedLogs)
						{
							if (profile.AllowMultipleFiles)
								this.ScheduleReloadingLogs(true, false);
							else
							{
								if (this.logFileInfoList.Count > 1)
									this.ClearLogFiles();
								else
									this.ScheduleReloadingLogs(true, false);
							}
						}
					});
					break;
				case nameof(LogProfile.ColorIndicator):
					this.SetValue(HasLogColorIndicatorProperty, this.LogProfile?.ColorIndicator != LogColorIndicator.None);
					this.SetValue(HasLogColorIndicatorByFileNameProperty, this.LogProfile?.ColorIndicator == LogColorIndicator.FileName);
					this.ScheduleReloadingLogs(false, true);
					break;
				case nameof(LogProfile.DataSourceOptions):
					this.ScheduleReloadingLogs(true, false);
					break;
				case nameof(LogProfile.DataSourceProvider):
					if (this.attachedLogDataSourceProvider != null)
						this.attachedLogDataSourceProvider.PropertyChanged -= this.OnLogDataSourceProviderPropertyChanged;
					this.attachedLogDataSourceProvider = (sender as LogProfile)?.DataSourceProvider;
					if (this.attachedLogDataSourceProvider != null)
						this.attachedLogDataSourceProvider.PropertyChanged += this.OnLogDataSourceProviderPropertyChanged;
					this.DisposeLogReaders();
					this.UpdateDataSourceRelatedStates();
					this.StartReadingLogs();
					break;
				case nameof(LogProfile.Icon):
				case nameof(LogProfile.IconColor):
				case nameof(LogProfile.Name):
					this.updateTitleAndIconAction.Schedule();
					break;
				case nameof(LogProfile.IsContinuousReading):
					this.SetValue(IsReadingLogsContinuouslyProperty, this.LogProfile.AsNonNull().IsContinuousReading);
					goto case nameof(LogProfile.LogPatterns);
				case nameof(LogProfile.IsTemplate):
					(sender as LogProfile)?.Let(it =>
					{
						if (it.IsTemplate)
						{
							this.Logger.LogWarning("Log profile '{profileName}' has been set as template, reset log profile", it.Name);
							this.ResetLogProfile();
						}
					});
					break;
				case nameof(LogProfile.LogLevelMapForReading):
					this.UpdateValidLogLevels();
					goto case nameof(LogProfile.LogPatterns);
				case nameof(LogProfile.LogWritingFormats):
					this.UpdateIsLogsWritingAvailable(this.LogProfile);
					break;
				case nameof(LogProfile.LogPatterns):
				case nameof(LogProfile.LogStringEncodingForReading):
				case nameof(LogProfile.RawLogLevelPropertyName):
				case nameof(LogProfile.SortDirection):
				case nameof(LogProfile.SortKey):
				case nameof(LogProfile.TimeSpanCultureInfoForReading):
				case nameof(LogProfile.TimeSpanEncodingForReading):
				case nameof(LogProfile.TimeSpanFormatForDisplaying):
				case nameof(LogProfile.TimeSpanFormatsForReading):
				case nameof(LogProfile.TimestampCultureInfoForReading):
				case nameof(LogProfile.TimestampFormatForDisplaying):
				case nameof(LogProfile.TimestampEncodingForReading):
				case nameof(LogProfile.TimestampFormatsForReading):
					this.ScheduleReloadingLogs(true, false);
					break;
				case nameof(LogProfile.RestartReadingDelay):
					(sender as LogProfile)?.Let(profile =>
					{
						if (profile.IsContinuousReading && this.logReaders.IsNotEmpty())
							this.logReaders[0].RestartReadingDelay = TimeSpan.FromMilliseconds(profile.RestartReadingDelay);
					});
					break;
				case nameof(LogProfile.VisibleLogProperties):
					this.ScheduleReloadingLogs(false, true);
					break;
			}
		}


		// Called when logs of log reader has been changed.
		void OnLogReaderLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			var logReader = (LogReader)sender.AsNonNull();
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					e.NewItems.AsNonNull().Let(logs =>
					{
						var displayableLogGroup = this.displayableLogGroup ?? throw new InternalStateCorruptedException("No displayable log group.");
						var displayableLogs = new DisplayableLog[logs.Count].Also(array =>
						{
							for (var i = array.Length - 1; i >= 0; --i)
								array[i] = displayableLogGroup.CreateDisplayableLog(logReader, (Log)logs[i].AsNonNull());
						});
						this.allLogs.AddAll(displayableLogs);
						logReader.DataSource.CreationOptions.FileName?.Let(fileName =>
						{
							if (!this.allLogsByLogFilePath.TryGetValue(fileName, out var localLogs))
							{
								localLogs = new List<DisplayableLog>();
								this.allLogsByLogFilePath.Add(fileName, localLogs);
							}
							localLogs.InsertRange(e.NewStartingIndex, displayableLogs);
							this.MatchMarkedLogs();
						});
					});
					break;
				case NotifyCollectionChangedAction.Remove:
					e.OldItems.AsNonNull().Let(oldItems =>
					{
						if (oldItems.Count == 1)
						{
							var removedLog = (Log)oldItems[0].AsNonNull();
							this.allLogs.RemoveAll(it => it.Log == removedLog);
						}
						else
						{
							var profile = this.LogProfile;
							if (profile != null 
								&& profile.SortKey == LogSortKey.Id 
								&& this.logReaders.Count == 1 
								&& this.logReaders[0] == logReader)
							{
								this.allLogs.RemoveRange(e.OldStartingIndex, oldItems.Count);
							}
							else
							{
								var removedLogs = new HashSet<Log>(oldItems.Cast<Log>());
								this.allLogs.RemoveAll(it => it.LogReader == logReader && removedLogs.Contains(it.Log));
							}
						}
						logReader.DataSource.CreationOptions.FileName?.Let(fileName =>
						{
							if (this.allLogsByLogFilePath.TryGetValue(fileName, out var localLogs))
								localLogs.RemoveRange(e.OldStartingIndex, oldItems.Count);
						});
					});
					break;
				case NotifyCollectionChangedAction.Reset:
					if (this.logReaders.Count == 1 && this.logReaders.First() == logReader)
					{
						DisposeDisplayableLogs(this.allLogs);
						this.allLogs.Clear();
					}
					else
						this.allLogs.RemoveAll(it => it.LogReader == logReader);
					logReader.DataSource.CreationOptions.FileName?.Let(fileName => this.allLogsByLogFilePath.Remove(fileName));
					break;
				default:
					throw new InvalidOperationException($"Unsupported logs change action: {e.Action}.");
			}
			if (this.logFileInfoMapByLogReader.TryGetValue(logReader, out var logFileInfo))
				logFileInfo.UpdateLogCount(logReader.Logs.Count);
		}


		// Called when property of log reader changed.
		void OnLogReaderPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(LogReader.IsWaitingForDataSource):
					this.checkIsWaitingForDataSourcesAction.Schedule();
					break;
				case nameof(LogReader.State):
					(sender as LogReader)?.Let(reader =>
					{
						this.logFileInfoMapByLogReader.TryGetValue(reader, out var logFileInfo);
						if (reader.State == LogReaderState.DataSourceError)
						{
							var source = reader.DataSource;
							if (source.State == LogDataSourceState.ExternalDependencyNotFound)
								this.ExternalDependencyNotFound?.Invoke(this, EventArgs.Empty);
						}
						else if (reader.State == LogReaderState.ReadingLogs)
						{
							if (reader.DataSource.CreationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
								this.LoadMarkedLogs(reader.DataSource.CreationOptions.FileName.AsNonNull());
						}
						else if (reader.State == LogReaderState.Stopped)
						{
							if (logFileInfo?.IsRemoving == true)
							{
								this.Logger.LogDebug("All logs cleared, remove log file '{logFileInfoFileName}'", logFileInfo.FileName);
								this.logFileInfoList.Remove(logFileInfo);
								this.DisposeLogReader(reader, false);
							}
						}
						logFileInfo?.UpdateLogReaderState(reader.State);
					});
					this.updateIsReadingLogsAction.Schedule();
					this.updateIsRemovingLogFilesAction.Schedule();
					break;
			}
		}


		// Called when marked logs has been changed.
		void OnMarkedLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!this.IsDisposed)
			{
				var hasLogs = this.markedLogs.IsNotEmpty();
				this.SetValue(HasMarkedLogsProperty, hasLogs);
				if (this.IsShowingMarkedLogsTemporarily)
				{
					this.SetValue(HasLogsProperty, hasLogs);
					if (!hasLogs)
						this.SetValue(IsShowingMarkedLogsTemporarilyProperty, false);
				}
			}
		}


		// Called when product state changed.
		void OnProductActivationChanged(IProductManager productManager, string productId, bool isActivated)
		{
			if (productId == Products.Professional)
				this.SetValue(IsProVersionActivatedProperty, isActivated);
		}


		// Called when property changed.
		protected override void OnPropertyChanged(ObservableProperty property, object? oldValue, object? newValue)
		{
			base.OnPropertyChanged(property, oldValue, newValue);
			if (property == AllLogCountProperty)
				this.UpdateIsLogsWritingAvailable(this.LogProfile);
			else if (property == CustomTitleProperty)
			{
				this.SetValue(HasCustomTitleProperty, newValue != null);
				this.updateTitleAndIconAction.Schedule();
			}
			else if (property == HasLogsProperty
				|| property == IsCopyingLogsProperty)
			{
				this.UpdateIsLogsWritingAvailable(this.LogProfile);
			}
			else if (property == IPEndPointProperty)
				this.SetValue(HasIPEndPointProperty, newValue != null);
			else if (property == IsReadingLogsProperty
				|| property == IsRemovingLogFilesProperty)
			{
				this.updateIsProcessingLogsAction.Schedule();
			}
			else if (property == IsSavingLogsProperty)
			{
				this.UpdateIsLogsWritingAvailable(this.LogProfile);
				this.updateIsProcessingLogsAction.Schedule();
			}
			else if (property == IsShowingAllLogsTemporarilyProperty
				|| property == IsShowingMarkedLogsTemporarilyProperty)
			{
				this.selectLogsToReportActions.Schedule();
			}
			else if (property == LastLogsReadingDurationProperty)
				this.SetValue(HasLastLogsReadingDurationProperty, this.LastLogsReadingDuration != null);
			else if (property == LogFilesPanelSizeProperty)
			{
				if (!this.isRestoringState)
					this.PersistentState.SetValue<double>(latestLogFilesPanelSizeKey, (double)newValue.AsNonNull());
			}
			else if (property == LogsDurationProperty)
				this.SetValue(HasLogsDurationProperty, newValue != null);
			else if (property == LogProfileProperty)
			{
				this.SetValue(HasLogProfileProperty, newValue != null);
				this.SetValue(IsBuiltInLogProfileProperty, (newValue as LogProfile)?.IsBuiltIn == true);
			}
			else if (property == LogsProperty)
				this.reportLogsTimeInfoAction?.Reschedule();
			else if (property == MarkedLogsPanelSizeProperty)
			{
				if (!this.isRestoringState)
					this.PersistentState.SetValue<double>(latestMarkedLogsPanelSizeKey, (double)newValue.AsNonNull());
			}
			else if (property == UriProperty)
				this.SetValue(HasUriProperty, newValue != null);
		}


		// Called when setting changed.
		protected override void OnSettingChanged(SettingChangedEventArgs e)
		{
			base.OnSettingChanged(e);
			if (e.Key == SettingKeys.ContinuousLogReadingUpdateInterval)
			{
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().UpdateInterval = this.ContinuousLogReadingUpdateInterval;
			}
			else if (e.Key == SettingKeys.DefaultTextShell)
			{
				this.LogProfile?.Let(profile =>
				{
					if (profile.DataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UseTextShell))
						&& profile.DataSourceOptions.UseTextShell)
					{
						this.ReloadLogs(true, false);
					}
				});
			}
			else if (e.Key == SettingKeys.MaxContinuousLogCount)
			{
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().MaxLogCount = (int)e.Value;
			}
			else if (e.Key == SettingKeys.MemoryUsagePolicy)
				this.memoryUsagePolicy = (MemoryUsagePolicy)e.Value;
		}


		// Pause or resume logs reading.
		void PauseResumeLogsReading()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canPauseResumeLogsReading.Value)
				return;
			if (this.logReaders.IsEmpty())
				throw new InternalStateCorruptedException("No log reader to pause/resume logs reading.");

			// pause or resume
			if (this.IsLogsReadingPaused)
			{
				foreach (var logReader in this.logReaders)
					logReader.Resume();
				this.SetValue(IsLogsReadingPausedProperty, false);
			}
			else
			{
				foreach (var logReader in this.logReaders)
					logReader.Pause();
				this.SetValue(IsLogsReadingPausedProperty, true);
			}
		}


		/// <summary>
		/// Command to pause or resume logs reading.
		/// </summary>
		public ICommand PauseResumeLogsReadingCommand { get; }


		// Reload log file.
		void ReloadLogFile(LogDataSourceParams<string>? param)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			var fileName = param?.Source;
			if (param == null || fileName == null)
				return;
			
			// find log reader
			var logReader = this.logReaders.FirstOrDefault(it => it.DataSource.CreationOptions.FileName == fileName);
			if (logReader == null)
				return;
			
			// check state
			if (this.logFileInfoMapByLogReader.TryGetValue(logReader, out var logFileInfo) && logFileInfo.IsPredefined)
				return;
			if (logReader.Precondition == param.Precondition)
				return;

			this.Logger.LogDebug("Request refreshing log file '{fileName}'", fileName);

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// reload logs
			logFileInfo?.UpdateLogReadingPrecondition(param.Precondition);
			logReader.Precondition = param.Precondition;
			logReader.Restart();
		}


		/// <summary>
		/// Command to reload added log file.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="LogDataSourceParams{String}"/>.</remarks>
		public ICommand ReloadLogFileCommand { get; }


		// Reload logs.
		void ReloadLogs(bool recreateLogReaders, bool updateDisplayLogProperties)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			var profile = this.LogProfile;
			if (profile == null)
				throw new InternalStateCorruptedException("No log profile to reload logs.");
			
			// cancel scheduled reloading logs
			this.reloadLogsAction.Cancel();
			this.reloadLogsFullyAction.Cancel();
			this.reloadLogsWithRecreatingLogReadersAction.Cancel();
			this.reloadLogsWithUpdatingVisPropAction.Cancel();

			// clear logs
			var isContinuousReading = profile.IsContinuousReading;
			if (isContinuousReading 
				&& !recreateLogReaders
				&& !this.GetValue(HasAllDataSourceErrorsProperty)
				&& !this.GetValue(HasPartialDataSourceErrorsProperty))
			{
				foreach (var logReader in this.logReaders)
					logReader.ClearLogs();
				this.Logger.LogWarning("Clear logs in {logReaderCount} log reader(s)", this.logReaders.Count);
			}
			else
			{
				// save log readers
				this.SaveLogReaders();

				this.Logger.LogWarning("Reload logs with {savedLogReaderOptionCount} log reader(s)", this.savedLogReaderOptions.Count);

				// dispose log readers
				this.DisposeLogReaders();

				// reset log file info
#pragma warning disable IDE0220
				foreach (LogFileInfoImpl logFileInfo in this.logFileInfoList)
				{
					logFileInfo.UpdateLogCount(0);
					logFileInfo.UpdateLogReaderState(LogReaderState.Preparing);
				}
#pragma warning restore IDE0220
			}

			// update display log properties
			if (updateDisplayLogProperties)
				this.UpdateDisplayLogProperties();
			
			// setup log comparer
			this.UpdateDisplayableLogComparison();

			// recreate log readers
			if (this.logReaders.IsEmpty())
				this.RestoreLogReaders();
		}


		/// <summary>
		/// Command to reload logs.
		/// </summary>
		public ICommand ReloadLogsCommand { get; }


		// Remove given log file.
		void RemoveLogFile(string? fileName)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (fileName == null)
				return;
			
			// find log reader
			var logReader = this.logReaders.FirstOrDefault(it => it.DataSource.CreationOptions.FileName == fileName);
			if (logReader == null)
				return;
			
			// check state
			if (this.logFileInfoMapByLogReader.TryGetValue(logReader, out var logFileInfo) && logFileInfo.IsPredefined)
				return;

			this.Logger.LogDebug("Request removing log file '{fileName}'", fileName);
			
			// clear logs directly
			if (this.logReaders.Count == 1)
			{
				this.ClearLogFiles();
				return;
			}

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// clear logs and remove later
			logReader.ClearLogs(true);
		}


		/// <summary>
		/// Command ro remove log file.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="string"/>.</remarks>
		public ICommand RemoveLogFileCommand { get; }


		// Reset log profile.
		void ResetLogProfile()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canResetLogProfile.Value)
				return;
			var profile = this.LogProfile;
			if (profile == null)
				throw new InternalStateCorruptedException("No log profile to reset.");
			
			// cancel scheduled reloading logs
			this.reloadLogsAction.Cancel();
			this.reloadLogsFullyAction.Cancel();
			this.reloadLogsWithRecreatingLogReadersAction.Cancel();
			this.reloadLogsWithUpdatingVisPropAction.Cancel();

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// detach from log profile
			profile.PropertyChanged -= this.OnLogProfilePropertyChanged;

			// detach from log data source provider
			if (this.attachedLogDataSourceProvider != null)
			{
				this.attachedLogDataSourceProvider.PropertyChanged -= this.OnLogDataSourceProviderPropertyChanged;
				this.attachedLogDataSourceProvider = null;
			}

			// update state
			this.SetValue(AreFileBasedLogsProperty, false);
			this.SetValue(AreLogsSortedByTimestampProperty, false);
			this.canResetLogProfile.Update(false);
			this.canShowAllLogsTemporarily.Update(false);
			this.ResetValue(HasLogColorIndicatorProperty);
			this.ResetValue(HasLogColorIndicatorByFileNameProperty);
			this.SetValue(IsIPEndPointNeededProperty, false);
			this.SetValue(IsLogFileNeededProperty, false);
			this.ResetValue(IsLogFileSupportedProperty);
			this.SetValue(IsReadingLogsContinuouslyProperty, false);
			this.SetValue(IsUriNeededProperty, false);
			this.SetValue(IsWorkingDirectoryNeededProperty, false);
			this.ResetValue(LastLogReadingPreconditionProperty);
			this.UpdateIsLogsWritingAvailable(null);
			this.UpdateValidLogLevels();

			// clear profile
			this.Logger.LogWarning("Reset log profile '{profileName}'", profile.Name);
			this.SetValue(LogProfileProperty, null);

			// dispose log readers
			this.DisposeLogReaders();

			// clear file name table
			this.logFileInfoList.Clear();

			// clear display log properties
			this.UpdateDisplayLogProperties();

			// update title
			this.updateTitleAndIconAction.Schedule();

			// update state
			this.canSetLogProfile.Update(true);
		}


		/// <summary>
		/// Command to reset log profile.
		/// </summary>
		public ICommand ResetLogProfileCommand { get; }


		// Restore log readers from saved state.
		void RestoreLogReaders()
		{
			// check profile
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.Logger.LogError("No log profile to restore {savedLogReaderOptionCount} log reader(s)", this.savedLogReaderOptions.Count);
				this.savedLogReaderOptions.Clear();
				return;
			}

			this.Logger.LogWarning("Start restoring {savedLogReaderOptionCount} log reader(s)", this.savedLogReaderOptions.Count);

			// dispose current log readers
			this.DisposeLogReaders();

			// restore to default source options
			var dataSourceProvider = profile.DataSourceProvider;
			var defaultDataSourceOptions = profile.DataSourceOptions;
			var useDefaultDataSourceOptions = false;
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)))
			{
				this.SetValue(AreFileBasedLogsProperty, true);
				this.SetValue(IsLogFileSupportedProperty, true);
				if (defaultDataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				{
					useDefaultDataSourceOptions = true;
					this.logFileInfoList.Clear();
					this.logFileInfoList.Add(new LogFileInfoImpl(this, defaultDataSourceOptions.FileName.AsNonNull(), new(), true));
					this.canClearLogFiles.Update(false);
					this.SetValue(IsLogFileNeededProperty, false);
				}
				else
					this.SetValue(IsLogFileNeededProperty, true);
			}
			else
			{
				this.canClearLogFiles.Update(false);
				this.SetValue(AreFileBasedLogsProperty, false);
				this.SetValue(IsLogFileNeededProperty, false);
				this.SetValue(IsLogFileSupportedProperty, dataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FileName)));
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.IPEndPoint)))
			{
				if (defaultDataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.IPEndPoint)))
				{
					useDefaultDataSourceOptions = true;
					this.SetValue(IsIPEndPointNeededProperty, false);
				}
				else
					this.SetValue(IsIPEndPointNeededProperty, true);
			}
			else
				this.SetValue(IsIPEndPointNeededProperty, false);
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Uri)))
			{
				if (defaultDataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.Uri)))
				{
					useDefaultDataSourceOptions = true;
					this.SetValue(IsUriNeededProperty, false);
				}
				else
					this.SetValue(IsUriNeededProperty, true);
			}
			else
				this.SetValue(IsUriNeededProperty, false);
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory))
				|| profile.IsWorkingDirectoryNeeded)
			{
				if (defaultDataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory)))
				{
					useDefaultDataSourceOptions = true;
					this.SetValue(IsWorkingDirectoryNeededProperty, false);
				}
				else
					this.SetValue(IsWorkingDirectoryNeededProperty, true);
			}
			else
				this.SetValue(IsWorkingDirectoryNeededProperty, false);

			// restore
			if (useDefaultDataSourceOptions)
			{
				this.Logger.LogWarning("Restore log reader(s) using default data source options");
				var precondition = this.savedLogReaderOptions.FirstOrDefault()?.Precondition ?? new();
				this.savedLogReaderOptions.Clear();
				this.savedLogReaderOptions.Add(new LogReaderOptions(defaultDataSourceOptions)
				{
					Precondition = precondition,
				});
			}
			foreach (var logReaderOption in this.savedLogReaderOptions)
			{
				// apply default options
				var newDataSourceOptions = logReaderOption.DataSourceOptions;
				newDataSourceOptions.Command = defaultDataSourceOptions.Command;
				newDataSourceOptions.ConnectionString = defaultDataSourceOptions.ConnectionString;
				newDataSourceOptions.FormatJsonData = defaultDataSourceOptions.FormatJsonData;
				newDataSourceOptions.FormatXmlData = defaultDataSourceOptions.FormatXmlData;
				newDataSourceOptions.Encoding = defaultDataSourceOptions.Encoding;
				newDataSourceOptions.QueryString = defaultDataSourceOptions.QueryString;
				newDataSourceOptions.SetupCommands = defaultDataSourceOptions.SetupCommands;
				newDataSourceOptions.UseTextShell = defaultDataSourceOptions.UseTextShell;
				newDataSourceOptions.TeardownCommands = defaultDataSourceOptions.TeardownCommands;

				// create data source and reader
				var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, newDataSourceOptions);
				if (dataSource != null)
					this.CreateLogReader(dataSource, logReaderOption.Precondition);
				else
				{
					this.hasLogDataSourceCreationFailure = true;
					this.checkDataSourceErrorsAction.Schedule();
				}
			}
			this.savedLogReaderOptions.Clear();

			this.Logger.LogWarning("Complete restoring {logReaderCount} log reader(s)", this.logReaders.Count);
		}


		/// <summary>
		/// Restore state from JSON value.
		/// </summary>
		/// <param name="jsonState">State in JSON format.</param>
		public void RestoreState(JsonElement jsonState)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (this.isRestoringState)
				throw new InvalidOperationException();

			this.Logger.LogTrace("Start restoring state");

			// restore
			this.isRestoringState = true;
			try
			{
				// reset log profile
				this.ResetLogProfile();

				// restore hibernation state
				if (jsonState.TryGetProperty(nameof(IsHibernated), out var jsonValue) && jsonValue.ValueKind == JsonValueKind.True)
					this.SetValue(IsHibernatedProperty, true);

				// set log profile
				if (!jsonState.TryGetProperty(nameof(LogProfile), out jsonValue) || jsonValue.ValueKind != JsonValueKind.String)
				{
					this.Logger.LogTrace("No state to restore");
					return;
				}
				var profile = LogProfileManager.Default.GetProfileOrDefault(jsonValue.GetString()!);
				if (profile == null)
				{
					this.Logger.LogWarning("Unable to find log profile '{jsonValueString}' to restore state", jsonValue.GetString());
					return;
				}
				var isInitLogProfile = jsonState.TryGetProperty("IsInitLogProfile", out jsonValue)
					&& jsonValue.ValueKind == JsonValueKind.True;
				this.SetLogProfile(profile, isInitLogProfile, false);

				// restore log reading precondition
				if (jsonState.TryGetProperty(nameof(LastLogReadingPrecondition), out jsonValue))
					this.SetValue(LastLogReadingPreconditionProperty, LogReadingPrecondition.Load(jsonValue));

				// create log readers
				if (jsonState.TryGetProperty("LogReaders", out jsonValue) && jsonValue.ValueKind == JsonValueKind.Array)
				{
					// restore data source options
					this.savedLogReaderOptions.Clear();
					foreach (var jsonLogReader in jsonValue.EnumerateArray())
					{
						// get data source options
						if (!jsonLogReader.TryGetProperty("Options", out jsonValue) || jsonValue.ValueKind != JsonValueKind.Object)
							continue;
						var dataSourceOptions = new LogDataSourceOptions();
						try
						{
							dataSourceOptions = LogDataSourceOptions.Load(jsonValue);
						}
						catch (Exception ex)
						{
							this.Logger.LogError(ex, "Failed to restore data source options");
							continue;
						}
						var logReaderOptions = new LogReaderOptions(dataSourceOptions);

						// get precondition
						if (jsonLogReader.TryGetProperty("Precondition", out jsonValue))
							logReaderOptions.Precondition = LogReadingPrecondition.Load(jsonValue);

						// add options to list
						this.savedLogReaderOptions.Add(logReaderOptions);

						// check file paths
						if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
							this.logFileInfoList.Add(new LogFileInfoImpl(this, dataSourceOptions.FileName.AsNonNull(), logReaderOptions.Precondition));
					}
					this.Logger.LogTrace("{savedLogReaderOptionCount} log reader(s) can be restored", this.savedLogReaderOptions.Count);

					// restore log readers
					if (!this.IsHibernated)
						this.RestoreLogReaders();
					else
						this.Logger.LogWarning("No need to restore log reader(s) because session is hibernated");
				}
				else
					this.Logger.LogTrace("No log readers to restore");

				// restore panel state
				if (jsonState.TryGetProperty(nameof(IsLogFilesPanelVisible), out jsonValue))
					this.SetValue(IsLogFilesPanelVisibleProperty, jsonValue.ValueKind != JsonValueKind.False);
				if (jsonState.TryGetProperty(nameof(IsMarkedLogsPanelVisible), out jsonValue))
					this.SetValue(IsMarkedLogsPanelVisibleProperty, jsonValue.ValueKind != JsonValueKind.False);
				if (jsonState.TryGetProperty(nameof(LogFilesPanelSize), out jsonValue) 
					&& jsonValue.TryGetDouble(out var doubleValue)
					&& LogFilesPanelSizeProperty.ValidationFunction(doubleValue))
				{
					this.SetValue(LogFilesPanelSizeProperty, doubleValue);
				}
				if (jsonState.TryGetProperty(nameof(MarkedLogsPanelSize), out jsonValue) 
					&& jsonValue.TryGetDouble(out doubleValue)
					&& MarkedLogsPanelSizeProperty.ValidationFunction(doubleValue))
				{
					this.SetValue(MarkedLogsPanelSizeProperty, doubleValue);
				}

				// raise event
				this.RestoringState?.Invoke(jsonState);

				this.Logger.LogTrace("Complete restoring state");
			}
			finally
			{
				this.isRestoringState = false;
			}
		}


		/// <summary>
		/// Raised when restoring state from JSON data.
		/// </summary>
		public event Action<JsonElement>? RestoringState;


		// Save state of log readers in memory.
		void SaveLogReaders()
        {
			// prepare
			this.savedLogReaderOptions.Clear();

			// check log reader
			if (this.logReaders.IsEmpty())
			{
				this.Logger.LogDebug("No log reader to save");
				return;
			}

			this.Logger.LogWarning("Start saving {logReaderCount} log reader(s)", this.logReaders.Count);

			// save marked logs
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// save data source options
			foreach (var logReader in this.logReaders)
			{
				if (logReader.State == LogReaderState.ClearingLogs && !logReader.IsRestarting)
					continue;
				this.savedLogReaderOptions.Add(new(logReader.DataSource.CreationOptions)
				{
					Precondition = logReader.Precondition,
				});
			}

			this.Logger.LogWarning("Complete saving {savedLogReaderOptionCount} log reader(s)", this.savedLogReaderOptions.Count);
		}


		// Save logs.
		async void SaveLogs(LogsSavingOptions? options)
		{
			// check state
			if (options == null)
				throw new ArgumentNullException(nameof(options));
			if (!options.HasFileName)
				return;
			if (!this.canSaveLogs.Value)
				return;

			// get log profile
			var profile = this.LogProfile;
			if (profile == null)
				return;

			// create log writer
			var logs = options.Logs;
			var markedColorMap = new Dictionary<Log, MarkColor>();
			using var dataOutput = new FileLogDataOutput(this.Application, options.FileName.AsNonNull());
			using var logWriter = options switch
			{
				JsonLogsSavingOptions jsonSavingOptions => new JsonLogWriter(dataOutput).Also(it =>
				{
					it.LogLevelMap = this.displayableLogGroup?.LevelMapForDisplaying ?? profile.LogLevelMapForWriting;
					it.LogPropertyMap = jsonSavingOptions.LogPropertyMap;
					it.TimeSpanCultureInfo = profile.TimeSpanCultureInfoForWriting;
					it.TimeSpanFormat = string.IsNullOrEmpty(profile.TimeSpanFormatForWriting)
						? profile.TimeSpanFormatsForReading.IsEmpty() ? profile.TimeSpanFormatForDisplaying : profile.TimeSpanFormatsForReading[0]
						: profile.TimeSpanFormatForWriting;
					it.TimestampCultureInfo = profile.TimestampCultureInfoForWriting;
					it.TimestampFormat = string.IsNullOrEmpty(profile.TimestampFormatForWriting) 
						? profile.TimestampFormatsForReading.IsEmpty() ? profile.TimestampFormatForDisplaying : profile.TimestampFormatsForReading[0]
						: profile.TimestampFormatForWriting;
				}),
				_ => (ILogWriter)this.CreateRawLogWriter(dataOutput).Also(it =>
				{
					var markedLogs = this.markedLogs;
					it.LogsToGetLineNumber = new HashSet<Log>().Also(it =>
					{
						for (var i = markedLogs.Count - 1; i >= 0; --i)
						{
							var markedLog = markedLogs[i];
							it.Add(markedLog.Log);
							markedColorMap[markedLog.Log] = markedLog.MarkedColor;
						}
					});
					it.WriteFileNames = false;
				}),
			};
			logWriter.Logs = new Log[logs.Count].Also(it =>
			{
				for (var i = it.Length - 1; i >= 0; --i)
					it[i] = logs[i].Log;
			});

			// prepare saving
			var syncLock = new object();
			var isCompleted = false;
			logWriter.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(RawLogWriter.State))
				{
					switch (logWriter.State)
					{
						case LogWriterState.DataOutputError:
						case LogWriterState.Stopped:
						case LogWriterState.UnclassifiedError:
							lock (syncLock)
							{
								isCompleted = true;
								Monitor.Pulse(syncLock);
							}
							break;
					}
				}
			};

			// start saving
			this.Logger.LogDebug($"Start saving logs");
			this.SetValue(IsSavingLogsProperty, true);
			logWriter.Start();
			await this.WaitForNecessaryTaskAsync(Task.Run(() =>
			{
				lock (syncLock)
				{
					while (!isCompleted)
					{
						if (Monitor.Wait(syncLock, 500))
							break;
						Task.Yield();
					}
				}
			}));

			// complete
			if (this.IsDisposed)
				return;
			this.SetValue(IsSavingLogsProperty, false);
			if (logWriter.State == LogWriterState.Stopped)
			{
				if (logWriter is RawLogWriter rawLogWriter && options.HasFileName)
				{
					this.Logger.LogDebug("Logs saving completed, save marked logs");
					var fileName = options.FileName.AsNonNull();
					var markedLogInfos = new List<MarkedLogInfo>().Also(markedLogInfos =>
					{
						foreach (var pair in rawLogWriter.LineNumbers)
						{
							if (!markedColorMap.TryGetValue(pair.Key, out var color))
								color = MarkColor.None;
							markedLogInfos.Add(new MarkedLogInfo(fileName, pair.Value, pair.Key.Timestamp, color));
						}
					});
					this.SaveMarkedLogs(fileName, markedLogInfos);
				}
				else
					this.Logger.LogDebug("Logs saving completed");
			}
			else
				this.Logger.LogError("Logs saving failed");
		}


		/// <summary>
		/// Command to save <see cref="Logs"/> to file.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="LogsSavingOptions"/>.</remarks>
		public ICommand SaveLogsCommand { get; }


		// Save marked logs.
		void SaveMarkedLogs()
		{
			foreach (var fileName in this.markedLogsChangedFilePaths)
				this.SaveMarkedLogs(fileName);
			this.markedLogsChangedFilePaths.Clear();
		}


		// Save marked logs.
		void SaveMarkedLogs(string logFileName)
		{
			var markedLogs = this.markedLogs.Where(it => it.FileName == logFileName);
			var markedLogInfos = new List<MarkedLogInfo>().Also(markedLogInfos =>
			{
				foreach (var markedLog in markedLogs)
					markedLog.LineNumber?.Let(lineNumber => markedLogInfos.Add(new MarkedLogInfo(logFileName, lineNumber, markedLog.Log.Timestamp, markedLog.MarkedColor)));
				var unmatchedMarkedLogInfos = this.unmatchedMarkedLogInfos;
				var unmatchedInfoIndex = -1;
				var pathComparer = PathEqualityComparer.Default;
				for (var i = unmatchedMarkedLogInfos.Count - 1; i >= 0; --i)
				{
					if (pathComparer.Equals(unmatchedMarkedLogInfos[i].FileName, logFileName))
					{
						unmatchedInfoIndex = i;
						break;
					}
				}
				if (unmatchedInfoIndex >= 0)
				{
					var count = 0;
					do
					{
						++count;
						markedLogInfos.Add(unmatchedMarkedLogInfos[unmatchedInfoIndex--]);
					} while (unmatchedInfoIndex >= 0 && pathComparer.Equals(unmatchedMarkedLogInfos[unmatchedInfoIndex].FileName, logFileName));
					this.Logger.LogTrace("Include {count} unmatched marked info of '{logFileName}'", count,  logFileName);
				}
			});
			this.SaveMarkedLogs(logFileName, markedLogInfos);
		}
		void SaveMarkedLogs(string logFileName, IList<MarkedLogInfo> markedLogInfos)
		{
			this.Logger.LogTrace("Request saving {markedLogInfoCount} marked log(s) of '{logFileName}'", markedLogInfos.Count, logFileName);

			// save or delete marked file
			var task = ioTaskFactory.StartNew(() =>
			{
				var markedFileName = logFileName + MarkedFileExtension;
				if (markedLogInfos.IsEmpty())
				{
					this.Logger.LogTrace("Delete marked log file '{markedFileName}'", markedFileName);
					Global.RunWithoutError(() => System.IO.File.Delete(markedFileName));
				}
				else
				{
					this.Logger.LogTrace("Start saving {markedLogInfoCount} marked log(s) to '{markedFileName}'", markedLogInfos.Count, markedFileName);
					try
					{
						if (!CarinaStudio.IO.File.TryOpenReadWrite(markedFileName, DefaultFileOpeningTimeout, out var stream) || stream == null)
						{
							this.Logger.LogError("Unable to open marked file to save: {markedFileName}", markedFileName);
							return;
						}
						using (stream)
						{
							using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions() { Indented = true });
							writer.WriteStartObject();
							writer.WritePropertyName("MarkedLogInfos");
							writer.WriteStartArray();
							foreach (var markedLog in markedLogInfos)
							{
								writer.WriteStartObject();
								if (markedLog.Color != MarkColor.None && markedLog.Color != MarkColor.Default)
									writer.WriteString("MarkedColor", markedLog.Color.ToString());
								writer.WriteNumber("MarkedLineNumber", markedLog.LineNumber);
								markedLog.Timestamp?.Let(it => writer.WriteString("MarkedTimestamp", it));
								writer.WriteEndObject();
							}
							writer.WriteEndArray();
							writer.WriteEndObject();
						}
					}
					catch (Exception ex)
					{
						this.Logger.LogError(ex, "Unable to save marked file: {markedFileName}", markedFileName);
					}
					this.Logger.LogTrace("Complete saving {markedLogInfoCount} marked log(s) to '{markedFileName}'", markedLogInfos.Count, markedFileName);
				}
			});
			_ = this.WaitForNecessaryTaskAsync(task);
		}


		// Search value of log property on the Internet.
		void SearchLogPropertyOnInternet(Net.SearchProvider searchProvider)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();

			// get property value
			var propertyValue = this.LogSelection.SelectedLogStringPropertyValue;
			if (string.IsNullOrWhiteSpace(propertyValue))
				return;
			
			// parse template value
			var tokens = new List<(Text.TokenType, string)>();
			while (true)
			{
				var actualTokenCount = 0;
				foreach (var token in new Text.Tokenizer(propertyValue))
				{
					++actualTokenCount;
					switch (token.Type)
					{
						case Text.TokenType.CjkPhrese:
						case Text.TokenType.Phrase:
						case Text.TokenType.VaryingString:
							tokens.Add((token.Type, new string(token.Value)));
							break;
					}
				}
				if (tokens.IsEmpty())
				{
					if (actualTokenCount == 0)
						return;
					foreach (var token in new Text.Tokenizer(propertyValue))
					{
						++actualTokenCount;
						switch (token.Type)
						{
							case Text.TokenType.DecimalNumber:
							case Text.TokenType.HexNumber:
								tokens.Add((token.Type, new string(token.Value)));
								break;
						}
					}
				}
				else if (tokens.Count == 1 && tokens[0].Item1 == Text.TokenType.VaryingString)
				{
					propertyValue = propertyValue.Trim().Let(it => it[1..^1]);
					tokens.Clear();
					continue;
				}
				break;
			}

			// search
			if (searchProvider.TryCreateSearchUri(tokens.Select(it => it.Item2).ToArray(), out var uri))
				Platform.OpenLink(uri);
		}


		/// <summary>
		/// Command to search value of log property on the Internet.
		/// </summary>
		/// <remarks>The type of parameter is <see cref="Net.SearchProvider"/>.</remarks>
		public ICommand SearchLogPropertyOnInternetCommand { get; }


		// Set IP endpoint.
		void SetIPEndPoint(IPEndPoint? endPoint)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsIPEndPointNeeded)
				return;
			if (endPoint == null)
				throw new ArgumentNullException(nameof(endPoint));
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to set IP endpoint.");
			var dataSourceOptions = profile.DataSourceOptions;
			if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.IPEndPoint)))
				throw new InternalStateCorruptedException($"Cannot set IP endpoint because URI is already specified.");

			// check current IP endpoint
			if (this.logReaders.IsNotEmpty())
			{
				if (object.Equals(this.logReaders.First().DataSource.CreationOptions.IPEndPoint, endPoint))
				{
					this.Logger.LogDebug("Set to same IP endpoint");
					return;
				}
			}

			// dispose current log readers
			this.DisposeLogReaders();

			this.Logger.LogDebug("Set IP endpoint to {address} ({port})", endPoint.Address, endPoint.Port);

			// create data source
			dataSourceOptions.IPEndPoint = endPoint;
			var dataSource = this.CreateLogDataSourceOrNull(profile.DataSourceProvider, dataSourceOptions);
			if (dataSource == null)
			{
				this.hasLogDataSourceCreationFailure = true;
				this.checkDataSourceErrorsAction.Schedule();
				return;
			}

			// create log reader
			this.CreateLogReader(dataSource, new());
		}


		/// <summary>
		/// Command to set IP endpoint.
		/// </summary>
		public ICommand SetIPEndPointCommand { get; }


		// Set log profile.
		void SetLogProfile(LogProfile? profile) => 
			this.SetLogProfile(profile, false, true);
		void SetLogProfile(LogProfile? profile, bool isInit, bool startReadingLogs)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canSetLogProfile.Value)
				return;
			if (profile == null)
				throw new ArgumentNullException(nameof(profile));
			if (this.LogProfile != null)
				throw new InternalStateCorruptedException("Already set another log profile.");
			if (profile.IsTemplate)
			{
				this.Logger.LogError("Cannot use template log profile '{profileName}'", profile.Name);
				return;
			}
			if (profile.DataSourceProvider.IsProVersionOnly && !this.Application.ProductManager.IsProductActivated(Products.Professional))
			{
				this.Logger.LogError("Cannot use log profile '{profileName}' with Pro-version only data source '{profileDataSourceProviderName}'", profile.Name, profile.DataSourceProvider.Name);
				return;
			}

			// set profile
			this.Logger.LogWarning("Set profile '{profileName}'", profile.Name);
			this.canSetLogProfile.Update(false);
			this.SetValue(IsReadingLogsContinuouslyProperty, profile.IsContinuousReading);
			this.SetValue(LogProfileProperty, profile);
			this.isInitLogProfile = isInit;
			if (!isInit)
				LogProfileManager.Default.SetAsRecentlyUsed(profile);

			// attach to log profile
			profile.PropertyChanged += this.OnLogProfilePropertyChanged;

			// attach to log data source provider
			this.attachedLogDataSourceProvider = profile.DataSourceProvider;
			this.attachedLogDataSourceProvider.PropertyChanged += this.OnLogDataSourceProviderPropertyChanged;

			// update display log properties
			this.UpdateDisplayLogProperties();

			// setup log comparer
			this.UpdateDisplayableLogComparison();

			// update valid log levels
			this.UpdateValidLogLevels();

			// update state
			this.SetValue(HasLogColorIndicatorProperty, profile.ColorIndicator != LogColorIndicator.None);
			this.SetValue(HasLogColorIndicatorByFileNameProperty, profile.ColorIndicator == LogColorIndicator.FileName);
			this.ResetValue(LastLogReadingPreconditionProperty);

			// update state
			this.UpdateDataSourceRelatedStates();

			// start reading logs
			if (startReadingLogs)
				this.StartReadingLogs();

			// update title
			this.updateTitleAndIconAction.Schedule();

			// update state
			this.UpdateIsLogsWritingAvailable(profile);
			this.canResetLogProfile.Update(true);
			this.canShowAllLogsTemporarily.Update(this.LogFiltering.IsFilteringNeeded);
		}


		/// <summary>
		/// Save current state to JSON data.
		/// </summary>
		/// <param name="jsonWriter"><see cref="Utf8JsonWriter"/> to write JSON data.</param>
		public void SaveState(Utf8JsonWriter jsonWriter)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();

			// save state
			jsonWriter.WriteStartObject();
			this.LogProfile?.Let(profile =>
			{
				// save hibernation state
				if (this.IsHibernated)
					jsonWriter.WriteBoolean(nameof(IsHibernated), true);

				// save log profile
				jsonWriter.WriteString(nameof(LogProfile), profile.Id);
				if (this.isInitLogProfile)
					jsonWriter.WriteBoolean("IsInitLogProfile", true);

				// save log readers
				if (this.savedLogReaderOptions.IsEmpty())
					this.SaveLogReaders();
				jsonWriter.WritePropertyName("LogReaders");
				jsonWriter.WriteStartArray();
				foreach (var logReaderOptions in this.savedLogReaderOptions)
				{
					jsonWriter.WriteStartObject();
					jsonWriter.WritePropertyName("Options");
					logReaderOptions.DataSourceOptions.Save(jsonWriter);
					jsonWriter.WritePropertyName("Precondition");
					logReaderOptions.Precondition.Save(jsonWriter);
					jsonWriter.WriteEndObject();
				}
				jsonWriter.WriteEndArray();

				// save log reading precondition
				jsonWriter.WritePropertyName(nameof(LastLogReadingPrecondition));
				this.GetValue(LastLogReadingPreconditionProperty).Save(jsonWriter);

				// save side panel state
				jsonWriter.WriteBoolean(nameof(IsLogFilesPanelVisible), this.IsLogFilesPanelVisible);
				jsonWriter.WriteBoolean(nameof(IsMarkedLogsPanelVisible), this.IsMarkedLogsPanelVisible);
				jsonWriter.WriteNumber(nameof(LogFilesPanelSize), this.LogFilesPanelSize);
				jsonWriter.WriteNumber(nameof(MarkedLogsPanelSize), this.MarkedLogsPanelSize);

				// raise event
				this.SavingState?.Invoke(jsonWriter);
			});
			jsonWriter.WriteEndObject();
		}


		/// <summary>
		/// Raised when saving state in JSON data.
		/// </summary>
		public event Action<Utf8JsonWriter>? SavingState;


		// Schedule reloading logs.
		void ScheduleReloadingLogs(bool recreateLogReaders, bool updateVisibleProperties)
		{
			if (this.reloadLogsFullyAction.IsScheduled)
				return;
			if (recreateLogReaders)
			{
				if (updateVisibleProperties || this.reloadLogsWithUpdatingVisPropAction.IsScheduled)
				{
					this.reloadLogsAction.Cancel();
					this.reloadLogsWithRecreatingLogReadersAction.Cancel();
					this.reloadLogsWithUpdatingVisPropAction.Cancel();
					this.reloadLogsFullyAction.Schedule();
				}
				else
				{
					this.reloadLogsAction.Cancel();
					this.reloadLogsWithRecreatingLogReadersAction.Schedule();
				}
			}
			else if (updateVisibleProperties)
			{
				if (this.reloadLogsWithRecreatingLogReadersAction.IsScheduled)
				{
					this.reloadLogsAction.Cancel();
					this.reloadLogsWithRecreatingLogReadersAction.Cancel();
					this.reloadLogsWithUpdatingVisPropAction.Cancel();
					this.reloadLogsFullyAction.Schedule();
				}
				else
				{
					this.reloadLogsAction.Cancel();
					this.reloadLogsWithUpdatingVisPropAction.Schedule();
				}
			}
			else
				this.reloadLogsAction.Schedule();
		}


		/// <summary>
		/// Command to set specific log profile.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="LogProfile"/>.</remarks>
		public ICommand SetLogProfileCommand { get; }


		// Set URI.
		void SetUri(Uri? uri)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsUriNeeded)
				return;
			if (uri == null)
				throw new ArgumentNullException(nameof(uri));
			if (!uri.IsAbsoluteUri)
				throw new ArgumentException("Cannot set relative URI.");
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to set URI.");
			var dataSourceOptions = profile.DataSourceOptions;
			if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.Uri)))
				throw new InternalStateCorruptedException($"Cannot set URI because URI is already specified.");

			// check current URI
			if (this.logReaders.IsNotEmpty())
			{
				if (this.logReaders.First().DataSource.CreationOptions.Uri == uri)
				{
					this.Logger.LogDebug("Set to same URI");
					return;
				}
			}

			// dispose current log readers
			this.DisposeLogReaders();

			this.Logger.LogDebug("Set URI to '{uri}'", uri);

			// create data source
			dataSourceOptions.Uri = uri;
			var dataSource = this.CreateLogDataSourceOrNull(profile.DataSourceProvider, dataSourceOptions);
			if (dataSource == null)
			{
				this.hasLogDataSourceCreationFailure = true;
				this.checkDataSourceErrorsAction.Schedule();
				return;
			}

			// create log reader
			this.CreateLogReader(dataSource, new());
		}


		/// <summary>
		/// Command to set URI.
		/// </summary>
		public ICommand SetUriCommand { get; }


		// Set working directory.
		void SetWorkingDirectory(string? directory)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsWorkingDirectoryNeeded)
				return;
			if (directory == null)
				throw new ArgumentNullException(nameof(directory));
			if (!Path.IsPathRooted(directory))
				throw new ArgumentException("Cannot set working directory by relative path.");
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to set working directory.");
			var dataSourceOptions = profile.DataSourceOptions;
			if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory)))
				throw new InternalStateCorruptedException($"Cannot set working directory because working directory is already specified.");

			// check current working directory
			if (this.logReaders.IsNotEmpty())
			{
				if (PathEqualityComparer.Default.Equals(this.logReaders.First().DataSource.CreationOptions.WorkingDirectory, directory))
				{
					this.Logger.LogDebug("Set to same working directory");
					return;
				}
			}

			// dispose current log readers
			this.DisposeLogReaders();

			this.Logger.LogDebug("Set working directory to '{directory}'", directory);

			// create data source
			dataSourceOptions.WorkingDirectory = directory;
			var dataSource = this.CreateLogDataSourceOrNull(profile.DataSourceProvider, dataSourceOptions);
			if (dataSource == null)
			{
				this.hasLogDataSourceCreationFailure = true;
				this.checkDataSourceErrorsAction.Schedule();
				return;
			}

			// create log reader
			this.CreateLogReader(dataSource, new());
		}


		/// <summary>
		/// Command to set working directory.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="string"/>.</remarks>
		public ICommand SetWorkingDirectoryCommand { get; }


		// Start reading logs if available.
		void StartReadingLogs()
		{
			// check state
			var profile = this.LogProfile;
			if (profile == null)
				return;
			if (this.logReaders.IsNotEmpty())
				return;

			// start reading or wait for more actions
			var canStartReading = true;
			var dataSourceOptions = profile.DataSourceOptions;
			var dataSourceProvider = profile.DataSourceProvider;
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)))
			{
				if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				{
					this.ResetValue(IsLogFileNeededProperty);
					this.logFileInfoList.Add(new LogFileInfoImpl(this, dataSourceOptions.FileName.AsNonNull(), new(), true));
				}
				else
				{
					this.Logger.LogDebug("No file name specified, waiting for adding file");
					this.SetValue(IsLogFileNeededProperty, true);
					canStartReading = false;
				}
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.IPEndPoint)))
			{
				if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.IPEndPoint)))
					this.ResetValue(IsIPEndPointNeededProperty);
				else
				{
					this.Logger.LogDebug("No IP endpoint specified, waiting for setting IP endpoint");
					this.SetValue(IsIPEndPointNeededProperty, true);
					canStartReading = false;
				}
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Uri)))
			{
				if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.Uri)))
					this.ResetValue(IsUriNeededProperty);
				else
				{
					this.Logger.LogDebug("Need URI, waiting for setting URI");
					this.SetValue(IsUriNeededProperty, true);
					canStartReading = false;
				}
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory))
				|| profile.IsWorkingDirectoryNeeded)
			{
				if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory)))
					this.ResetValue(IsWorkingDirectoryNeededProperty);
				else
				{
					this.Logger.LogDebug("Need working directory, waiting for setting working directory");
					this.SetValue(IsWorkingDirectoryNeededProperty, true);
					canStartReading = false;
				}
			}
			if (canStartReading)
			{
				this.Logger.LogDebug("Start reading logs for source '{dataSourceProviderName}'", dataSourceProvider.Name);
				var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, dataSourceOptions);
				if (dataSource != null)
					this.CreateLogReader(dataSource, new());
				else
				{
					this.hasLogDataSourceCreationFailure = true;
					this.checkDataSourceErrorsAction.Schedule();
				}
			}
		}


		/// <summary>
		/// Get title of session.
		/// </summary>
		public string? Title => 
			this.GetValue(TitleProperty);


		// Enable or disable showing all logs temporarily.
		void ToggleShowingAllLogsTemporarily()
        {
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canShowAllLogsTemporarily.Value)
				return;

			// toggle
			this.SetValue(IsShowingAllLogsTemporarilyProperty, !this.GetValue(IsShowingAllLogsTemporarilyProperty));
			if (this.IsShowingAllLogsTemporarily)
				this.SetValue(IsShowingMarkedLogsTemporarilyProperty, false);
        }


		/// <summary>
		/// Command to enable or disable showing all logs temporarily.
		/// </summary>
		public ICommand ToggleShowingAllLogsTemporarilyCommand { get; }


		// Enable or disable showing all logs temporarily.
		void ToggleShowingMarkedLogsTemporarily()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.HasMarkedLogs)
				return;

			// toggle
			this.SetValue(IsShowingMarkedLogsTemporarilyProperty, !this.GetValue(IsShowingMarkedLogsTemporarilyProperty));
			if (this.IsShowingMarkedLogsTemporarily)
				this.SetValue(IsShowingAllLogsTemporarilyProperty, false);
		}


		/// <summary>
		/// Command to toggle <see cref="IsShowingMarkedLogsTemporarily"/>.
		/// </summary>
		public ICommand ToggleShowingMarkedLogsTemporarilyCommand { get; }


		/// <summary>
		/// Get size of total memory usage of logs by all <see cref="Session"/> instances in bytes.
		/// </summary>
		public long TotalLogsMemoryUsage => 
			this.GetValue(TotalLogsMemoryUsageProperty);


		// Trigger GC if needed.
		static void TriggerGC()
		{
			var app = App.CurrentOrNull;
			if (app == null)
				return;
			switch (app.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy))
			{
				case MemoryUsagePolicy.Balance:
					app.PerformGC(GCCollectionMode.Optimized);
					break;
				case MemoryUsagePolicy.LessMemoryUsage:
					app.PerformGC(GCCollectionMode.Forced);
					break;
				default:
					staticLogger?.LogDebug("Skip triggering GC");
					return;
			}
		}


		// Unmark logs.
		void UnmarkLogs(IEnumerable<DisplayableLog> logs)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canMarkUnmarkLogs.Value)
				return;
			var isShowingAllLogsTemporarily = this.IsShowingAllLogsTemporarily;
			foreach (var log in logs)
			{
				if (log.MarkedColor != MarkColor.None)
				{
					log.MarkedColor = MarkColor.None;
					this.markedLogs.Remove(log);
					if (isShowingAllLogsTemporarily)
						this.LogFiltering.InvalidateLog(log);
					log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
				}
			}

			// schedule save to file action
			this.saveMarkedLogsAction.Schedule(DelaySaveMarkedLogs);
		}


		/// <summary>
		/// Command to unmark logs.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="IEnumerable{DisplayableLog}"/>.</remarks>
		public ICommand UnmarkLogsCommand { get; }


		// Update log data source related states.
		void UpdateDataSourceRelatedStates()
		{
			var dataSourceProvider = this.LogProfile?.DataSourceProvider;
			if (dataSourceProvider == null)
				return;
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)))
			{
				this.SetValue(AreFileBasedLogsProperty, true);
				this.SetValue(IsLogFileSupportedProperty, true);
			}
			else
			{
				this.ResetValue(AreFileBasedLogsProperty);
				this.SetValue(IsLogFileSupportedProperty, dataSourceProvider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FileName)));
			}
		}


		// Update comparison for displayable logs.
		void UpdateDisplayableLogComparison()
		{
			var profile = this.LogProfile.AsNonNull();
			var sortedByTimestamp = true;
			this.compareDisplayableLogsDelegate = profile.SortKey switch
			{
				LogSortKey.BeginningTimeSpan => ((Comparison<DisplayableLog?>)CompareDisplayableLogsByBeginningTimeSpan).Also(_ => sortedByTimestamp = false),
				LogSortKey.BeginningTimestamp => CompareDisplayableLogsByBeginningTimestamp,
				LogSortKey.EndingTimeSpan => ((Comparison<DisplayableLog?>)CompareDisplayableLogsByEndingTimeSpan).Also(_ => sortedByTimestamp = false),
				LogSortKey.EndingTimestamp => CompareDisplayableLogsByEndingTimestamp,
				LogSortKey.ReadTime => CompareDisplayableLogsByReadTime,
				LogSortKey.TimeSpan => ((Comparison<DisplayableLog?>)CompareDisplayableLogsByTimeSpan).Also(_ => sortedByTimestamp = false),
				LogSortKey.Timestamp => CompareDisplayableLogsByTimestamp,
				_ => ((Comparison<DisplayableLog?>)CompareDisplayableLogsById).Also(_ => sortedByTimestamp = false),
			};
			if (profile.SortDirection == SortDirection.Descending)
				this.compareDisplayableLogsDelegate = this.compareDisplayableLogsDelegate.Invert();
			this.SetValue(AreLogsSortedByTimestampProperty, sortedByTimestamp);
		}


		// Update list of display log properties according to profile.
		void UpdateDisplayLogProperties()
		{
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.ResetValue(HasTimestampDisplayableLogPropertyProperty);
				this.SetValue(DisplayLogPropertiesProperty, DisplayLogPropertiesProperty.DefaultValue);
			}
			else
			{
				var app = this.Application;
				var visibleLogProperties = profile.VisibleLogProperties;
				var displayLogProperties = new List<DisplayableLogProperty>();
				var hasTimestamp = false;
				foreach (var logProperty in visibleLogProperties)
				{
					var displayableLogProperty = new DisplayableLogProperty(app, logProperty);
					displayLogProperties.Add(displayableLogProperty);
					if (!hasTimestamp)
						hasTimestamp = DisplayableLog.HasDateTimeProperty(logProperty.Name);
				}
				if (displayLogProperties.IsEmpty())
					displayLogProperties.Add(new DisplayableLogProperty(app, nameof(DisplayableLog.Message), "RawData", null));
				this.SetValue(DisplayLogPropertiesProperty, new SafeReadOnlyList<DisplayableLogProperty>(displayLogProperties));
				this.SetValue(HasTimestampDisplayableLogPropertyProperty, hasTimestamp);
			}
		}


		// Update logs writing related states.
		void UpdateIsLogsWritingAvailable(LogProfile? profile)
		{
			if (profile == null)
			{
				this.canCopyLogs.Update(false);
				this.canCopyLogsWithFileNames.Update(false);
				this.canSaveLogs.Update(false);
			}
			else
			{
				this.canCopyLogs.Update(this.Application is App && !this.IsCopyingLogs);
				this.canCopyLogsWithFileNames.Update(this.canCopyLogs.Value && profile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)));
				this.canSaveLogs.Update(this.Application is App && !this.IsSavingLogs && this.AllLogCount > 0);
			}
		}


		// Update valid log levels defined by log profile.
		void UpdateValidLogLevels()
		{
			var profile = this.LogProfile;
			if (profile == null)
				this.SetValue(ValidLogLevelsProperty, Array.Empty<Logs.LogLevel>());
			else
			{
				var logLevels = new HashSet<Logs.LogLevel>(profile.LogLevelMapForReading.Values).Also(it => it.Add(ULogViewer.Logs.LogLevel.Undefined));
				this.SetValue(ValidLogLevelsProperty, new SafeReadOnlyList<Logs.LogLevel>(logLevels.ToList()));
			}
		}


		/// <summary>
		/// Get current URI to read logs from.
		/// </summary>
		public Uri? Uri =>
			this.GetValue(UriProperty);


		/// <summary>
		/// Get list of valid <see cref="ULogViewer.Logs.LogLevel"/> defined by log profile including <see cref="ULogViewer.Logs.LogLevel.Undefined"/>.
		/// </summary>
		public IList<Logs.LogLevel> ValidLogLevels =>
			this.GetValue(ValidLogLevelsProperty);


		// Wait for all necessary tasks.
		public override Task WaitForNecessaryTasksAsync()
		{
			this.saveMarkedLogsAction.ExecuteIfScheduled();
			return base.WaitForNecessaryTasksAsync();
		}


		/// <summary>
		/// Get name of current working directory.
		/// </summary>
		public string? WorkingDirectoryName =>
			this.GetValue(WorkingDirectoryNameProperty);


		/// <summary>
		/// Get path of current working directory.
		/// </summary>
		public string? WorkingDirectoryPath =>
			this.GetValue(WorkingDirectoryPathProperty);
	}
}
