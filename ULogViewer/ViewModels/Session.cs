using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.AppSuite;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Data.Converters;
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Session to view logs.
	/// </summary>
	class Session : ViewModel
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
		/// Property of <see cref="BeginningPreconditionTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<DateTime?> BeginningPreconditionTimestampProperty = ObservableProperty.Register<Session, DateTime?>(nameof(BeginningPreconditionTimestamp));
		/// <summary>
		/// Property of <see cref="CustomTitle"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> CustomTitleProperty = ObservableProperty.Register<Session, string?>(nameof(CustomTitle), coerce: (_, it) => string.IsNullOrWhiteSpace(it) ? null : it);
		/// <summary>
		/// Property of <see cref="DisplayLogProperties"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<DisplayableLogProperty>> DisplayLogPropertiesProperty = ObservableProperty.Register<Session, IList<DisplayableLogProperty>>(nameof(DisplayLogProperties), new DisplayableLogProperty[0]);
		/// <summary>
		/// Property of <see cref="EarliestLogTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<DateTime?> EarliestLogTimestampProperty = ObservableProperty.Register<Session, DateTime?>(nameof(EarliestLogTimestamp));
		/// <summary>
		/// Property of <see cref="EndingPreconditionTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<DateTime?> EndingPreconditionTimestampProperty = ObservableProperty.Register<Session, DateTime?>(nameof(EndingPreconditionTimestamp));
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
		/// Property of <see cref="HasLastLogsFilteringDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLastLogsFilteringDurationProperty = ObservableProperty.Register<Session, bool>(nameof(HasLastLogsFilteringDuration));
		/// <summary>
		/// Property of <see cref="HasLastLogsReadingDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLastLogsReadingDurationProperty = ObservableProperty.Register<Session, bool>(nameof(HasLastLogsReadingDuration));
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
		/// Property of <see cref="HasPredefinedLogTextFilters"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasPredefinedLogTextFiltersProperty = ObservableProperty.Register<Session, bool>(nameof(HasPredefinedLogTextFilters));
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
		/// Property of <see cref="IsCopyingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsCopyingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsCopyingLogs));
		/// <summary>
		/// Property of <see cref="IsFilteringLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsFilteringLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsFilteringLogs));
		/// <summary>
		/// Property of <see cref="IsFilteringLogsNeeded"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsFilteringLogsNeededProperty = ObservableProperty.Register<Session, bool>(nameof(IsFilteringLogsNeeded));
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
		/// Property of <see cref="IsLogsReadingPaused"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsLogsReadingPausedProperty = ObservableProperty.Register<Session, bool>(nameof(IsLogsReadingPaused));
		/// <summary>
		/// Property of <see cref="IsProcessingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsProcessingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsProcessingLogs));
		/// <summary>
		/// Property of <see cref="IsReadingLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsReadingLogsProperty = ObservableProperty.Register<Session, bool>(nameof(IsReadingLogs));
		/// <summary>
		/// Property of <see cref="IsReadingLogsContinuously"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsReadingLogsContinuouslyProperty = ObservableProperty.Register<Session, bool>(nameof(IsReadingLogsContinuously));
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
		/// Property of <see cref="IsSidePanelVisible"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsSidePanelVisibleProperty = ObservableProperty.Register<Session, bool>(nameof(IsSidePanelVisible), true);
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
		/// Property of <see cref="LastLogsFilteringDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> LastLogsFilteringDurationProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(LastLogsFilteringDuration));
		/// <summary>
		/// Property of <see cref="LastLogsReadingDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> LastLogsReadingDurationProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(LastLogsReadingDuration));
		/// <summary>
		/// Property of <see cref="LatestLogTimestamp"/>.
		/// </summary>
		public static readonly ObservableProperty<DateTime?> LatestLogTimestampProperty = ObservableProperty.Register<Session, DateTime?>(nameof(LatestLogTimestamp));
		/// <summary>
		/// Property of <see cref="LogFiltersCombinationMode"/>.
		/// </summary>
		public static readonly ObservableProperty<FilterCombinationMode> LogFiltersCombinationModeProperty = ObservableProperty.Register<Session, FilterCombinationMode>(nameof(LogFiltersCombinationMode), FilterCombinationMode.Intersection);
		/// <summary>
		/// Property of <see cref="LogLevelFilter"/>.
		/// </summary>
		public static readonly ObservableProperty<Logs.LogLevel> LogLevelFilterProperty = ObservableProperty.Register<Session, Logs.LogLevel>(nameof(LogLevelFilter), ULogViewer.Logs.LogLevel.Undefined);
		/// <summary>
		/// Property of <see cref="LogProcessIdFilter"/>.
		/// </summary>
		public static readonly ObservableProperty<int?> LogProcessIdFilterProperty = ObservableProperty.Register<Session, int?>(nameof(LogProcessIdFilter));
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
		public static readonly ObservableProperty<IList<DisplayableLog>> LogsProperty = ObservableProperty.Register<Session, IList<DisplayableLog>>(nameof(Logs), new DisplayableLog[0]);
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
		/// Property of <see cref="LogsFilteringProgress"/>.
		/// </summary>
		public static readonly ObservableProperty<double> LogsFilteringProgressProperty = ObservableProperty.Register<Session, double>(nameof(LogsFilteringProgress));
		/// <summary>
		/// Property of <see cref="LogTextFilter"/>.
		/// </summary>
		public static readonly ObservableProperty<Regex?> LogTextFilterProperty = ObservableProperty.Register<Session, Regex?>(nameof(LogTextFilter));
		/// <summary>
		/// Property of <see cref="LogThreadIdFilter"/>.
		/// </summary>
		public static readonly ObservableProperty<int?> LogThreadIdFilterProperty = ObservableProperty.Register<Session, int?>(nameof(LogThreadIdFilter));
		/// <summary>
		/// Property of <see cref="SidePanelSize"/>.
		/// </summary>
		public static readonly ObservableProperty<double> SidePanelSizeProperty = ObservableProperty.Register<Session, double>(nameof(SidePanelSize), (MinSidePanelSize + MaxSidePanelSize) / 2, 
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
		public static readonly ObservableProperty<IList<Logs.LogLevel>> ValidLogLevelsProperty = ObservableProperty.Register<Session, IList<Logs.LogLevel>>(nameof(ValidLogLevels), new Logs.LogLevel[0]);
		/// <summary>
		/// Property of <see cref="WorkingDirectoryName"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> WorkingDirectoryNameProperty = ObservableProperty.Register<Session, string?>(nameof(WorkingDirectoryName));
		/// <summary>
		/// Property of <see cref="WorkingDirectoryPath"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> WorkingDirectoryPathProperty = ObservableProperty.Register<Session, string?>(nameof(WorkingDirectoryPath));


		/// <summary>
		/// <see cref="IValueConverter"/> to convert from logs filtering progress to readable string.
		/// </summary>
		public static readonly IValueConverter LogsFilteringProgressConverter = new AppSuite.Converters.RatioToPercentageConverter(1);


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
			public IEnumerable<DisplayableLog> Logs { get; set; } = new DisplayableLog[0];
        }


		// Constants.
		const string MarkedFileExtension = ".ulvmark";
		const int DisplayableLogDisposingChunkSize = 65536;
		const int DelaySaveMarkedLogs = 1000;
		const int DisposeDisplayableLogsInterval = 100;
		const int FileLogsReadingConcurrencyLevel = 1;
		const int LogsTimeInfoReportingInterval = 500;


		// Static fields.
		static readonly LinkedList<Session> activationHistoryList = new LinkedList<Session>();
		static readonly TaskFactory defaultLogsReadingTaskFactory = new TaskFactory(TaskScheduler.Default);
		static readonly List<DisplayableLog> displayableLogsToDispose = new List<DisplayableLog>();
		static readonly ScheduledAction disposeDisplayableLogsAction = new ScheduledAction(App.Current, () =>
		{
			if (displayableLogsToDispose.IsEmpty())
				return;
			var logCount = displayableLogsToDispose.Count;
			if (logCount <= DisplayableLogDisposingChunkSize)
			{
				foreach (var log in displayableLogsToDispose)
					log.Dispose();
				displayableLogsToDispose.Clear();
				staticLogger?.LogTrace($"Disposed {logCount} displayable logs");
				staticLogger?.LogTrace($"All displayable logs were disposed, trigger GC");
				if (App.Current.Settings.GetValueOrDefault(SettingKeys.SaveMemoryAggressively))
					GC.Collect();
				else
					GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, false);
			}
			else
			{
				for (var i = logCount - DisplayableLogDisposingChunkSize; i < logCount; ++i)
					displayableLogsToDispose[i].Dispose();
				displayableLogsToDispose.RemoveRange(logCount - DisplayableLogDisposingChunkSize, DisplayableLogDisposingChunkSize);
				disposeDisplayableLogsAction?.Schedule(DisposeDisplayableLogsInterval);
				staticLogger?.LogTrace($"Disposed {DisplayableLogDisposingChunkSize} displayable logs, {displayableLogsToDispose.Count} remains");
			}
		});
		static readonly ScheduledAction hibernateSessionsAction = new ScheduledAction(() =>
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
			staticLogger.LogTrace($"Total logs memory usage: {AppSuite.Converters.FileSizeConverter.Default.Convert<string>(logsMemoryUsage)}, threshold: {AppSuite.Converters.FileSizeConverter.Default.Convert<string>(memoryThresholdToStartHibernation)}");
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
			staticLogger?.LogWarning($"Hibernate {hibernatedSessionCount} session(s) to release {AppSuite.Converters.FileSizeConverter.Default.Convert<string>(releasedMemory)} memory");
		});
		static readonly TaskFactory ioTaskFactory = new TaskFactory(new FixedThreadsTaskScheduler(1));
		static readonly SettingKey<double> latestSidePanelSizeKey = new SettingKey<double>("Session.LatestSidePanelSize", SidePanelSizeProperty.DefaultValue);
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


		// Class for marked log info.
		class MarkedLogInfo
		{
			public MarkColor Color = MarkColor.None;
			public string FileName;
			public int LineNumber;
			public DateTime? Timestamp;
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
		readonly List<IDisposable> activationTokens = new List<IDisposable>();
		readonly HashSet<string> addedLogFilePaths = new HashSet<string>(PathEqualityComparer.Default);
		readonly SortedObservableList<DisplayableLog> allLogs;
		readonly Dictionary<string, List<DisplayableLog>> allLogsByLogFilePath = new Dictionary<string, List<DisplayableLog>>(PathEqualityComparer.Default);
		readonly MutableObservableBoolean canClearLogFiles = new MutableObservableBoolean();
		readonly MutableObservableBoolean canCopyLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canCopyLogsWithFileNames = new MutableObservableBoolean();
		readonly MutableObservableBoolean canMarkUnmarkLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canPauseResumeLogsReading = new MutableObservableBoolean();
		readonly MutableObservableBoolean canReloadLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canResetLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSaveLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSetLogProfile = new MutableObservableBoolean();
		readonly MutableObservableBoolean canShowAllLogsTemporarily = new MutableObservableBoolean();
		readonly ScheduledAction checkDataSourceErrorsAction;
		readonly ScheduledAction checkIsWaitingForDataSourcesAction;
		readonly ScheduledAction checkLogsMemoryUsageAction;
		Comparison<DisplayableLog?> compareDisplayableLogsDelegate;
		DisplayableLogGroup? displayableLogGroup;
		TaskFactory? fileLogsReadingTaskFactory;
		bool hasLogDataSourceCreationFailure;
		bool isRestoringState;
		readonly DisplayableLogFilter logFilter;
		readonly List<LogReader> logReaders = new List<LogReader>();
		readonly Stopwatch logsFilteringWatch = new Stopwatch();
		readonly Stopwatch logsReadingWatch = new Stopwatch();
		readonly SortedObservableList<DisplayableLog> markedLogs;
		readonly HashSet<string> markedLogsChangedFilePaths = new HashSet<string>(PathEqualityComparer.Default);
		readonly ObservableList<PredefinedLogTextFilter> predefinedLogTextFilters;
		readonly ScheduledAction reportLogsTimeInfoAction;
		readonly List<LogDataSourceOptions> savedDataSourceOptions = new List<LogDataSourceOptions>();
		readonly ScheduledAction saveMarkedLogsAction;
		readonly ScheduledAction selectLogsToReportActions;
		readonly List<MarkedLogInfo> unmatchedMarkedLogInfos = new List<MarkedLogInfo>();
		readonly ScheduledAction updateIsReadingLogsAction;
		readonly ScheduledAction updateIsProcessingLogsAction;
		readonly ScheduledAction updateLogFilterAction;
		readonly ScheduledAction updateTitleAndIconAction;


		/// <summary>
		/// Initialize new <see cref="Session"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public Session(IAppSuiteApplication app) : base(app)
		{
			// create static logger
			if (staticLogger == null)
				staticLogger = app.LoggerFactory.CreateLogger(nameof(Session));

			// create node for activation history
			this.activationHistoryListNode = new LinkedListNode<Session>(this);

			// create commands
			this.AddLogFileCommand = new Command<string?>(this.AddLogFile, this.GetValueAsObservable(IsLogFileNeededProperty));
			this.ClearLogFilesCommand = new Command(this.ClearLogFiles, this.canClearLogFiles);
			this.CopyLogsCommand = new Command<IList<DisplayableLog>>(it => this.CopyLogs(it, false), this.canCopyLogs);
			this.CopyLogsWithFileNamesCommand = new Command<IList<DisplayableLog>>(it => this.CopyLogs(it, true), this.canCopyLogsWithFileNames);
			this.MarkLogsCommand = new Command<MarkingLogsParams>(this.MarkLogs, this.canMarkUnmarkLogs);
			this.MarkUnmarkLogsCommand = new Command<IEnumerable<DisplayableLog>>(this.MarkUnmarkLogs, this.canMarkUnmarkLogs);
			this.PauseResumeLogsReadingCommand = new Command(this.PauseResumeLogsReading, this.canPauseResumeLogsReading);
			this.ReloadLogsCommand = new Command(() => 
			{
				if (this.canReloadLogs.Value)
					this.ReloadLogs(false, false);
			}, this.canReloadLogs);
			this.ResetLogProfileCommand = new Command(this.ResetLogProfile, this.canResetLogProfile);
			this.SaveLogsCommand = new Command<LogsSavingOptions>(this.SaveLogs, this.canSaveLogs);
			this.SetIPEndPointCommand = new Command<IPEndPoint?>(this.SetIPEndPoint, this.GetValueAsObservable(IsIPEndPointNeededProperty));
			this.SetLogProfileCommand = new Command<LogProfile?>(this.SetLogProfile, this.canSetLogProfile);
			this.SetUriCommand = new Command<Uri?>(this.SetUri, this.GetValueAsObservable(IsUriNeededProperty));
			this.SetWorkingDirectoryCommand = new Command<string?>(this.SetWorkingDirectory, this.GetValueAsObservable(IsWorkingDirectoryNeededProperty));
			this.ToggleShowingAllLogsTemporarilyCommand = new Command(this.ToggleShowingAllLogsTemporarily, this.canShowAllLogsTemporarily);
			this.ToggleShowingMarkedLogsTemporarilyCommand = new Command(this.ToggleShowingMarkedLogsTemporarily, this.GetValueAsObservable(HasMarkedLogsProperty));
			this.UnmarkLogsCommand = new Command<IEnumerable<DisplayableLog>>(this.UnmarkLogs, this.canMarkUnmarkLogs);
			this.canSetLogProfile.Update(true);

			// create collections
			this.allLogs = new SortedObservableList<DisplayableLog>(this.CompareDisplayableLogs).Also(it =>
			{
				it.CollectionChanged += this.OnAllLogsChanged;
			});
			this.markedLogs = new SortedObservableList<DisplayableLog>(this.CompareDisplayableLogs).Also(it =>
			{
				it.CollectionChanged += this.OnMarkedLogsChanged;
			});
			this.predefinedLogTextFilters = new ObservableList<PredefinedLogTextFilter>().Also(it =>
			{
				it.CollectionChanged += (_, e) =>
				{
					if (this.IsDisposed)
						return;
					this.SetValue(HasPredefinedLogTextFiltersProperty, it.IsNotEmpty());
					this.updateLogFilterAction?.Reschedule();
				};
			});

			// create log filter
			this.logFilter = new DisplayableLogFilter((IULogViewerApplication)this.Application, this.allLogs, this.CompareDisplayableLogs).Also(it =>
			{
				((INotifyCollectionChanged)it.FilteredLogs).CollectionChanged += this.OnFilteredLogsChanged;
				it.PropertyChanged += this.OnLogFilterPropertyChanged;
			});

			// setup properties
			this.AllLogs = new SafeReadOnlyList<DisplayableLog>(this.allLogs);
			this.SetValue(LogsProperty, this.AllLogs);
			this.MarkedLogs = new SafeReadOnlyList<DisplayableLog>(this.markedLogs);

			// setup delegates
			this.compareDisplayableLogsDelegate = CompareDisplayableLogsById;

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
				var logsMemoryUsage = (this.displayableLogGroup?.MemorySize ?? 0L) + ((this.allLogs.Count + this.markedLogs.Count) * IntPtr.Size + this.logFilter.MemorySize);
				this.SetValue(LogsMemoryUsageProperty, logsMemoryUsage);
				totalLogsMemoryUsage += (logsMemoryUsage - prevLogsMemoryUsage);
				this.SetValue(TotalLogsMemoryUsageProperty, totalLogsMemoryUsage);

				// hibernate sessions if needed
				hibernateSessionsAction.Schedule();

				// schedule next checking

				this.checkLogsMemoryUsageAction?.Schedule(LogsMemoryUsageCheckInterval);
			});
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
						duration = this.CalculateDurationBetweenLogs(firstLog, lastLog, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);
					else
						duration = this.CalculateDurationBetweenLogs(lastLog, firstLog, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);
					this.SetValue(LogsDurationProperty, duration);
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
						this.SetValue(LogsProperty, this.MarkedLogs);
						this.SetValue(HasLogsProperty, this.markedLogs.IsNotEmpty());
						return;
					}
					if (this.logFilter.IsProcessingNeeded)
					{
						this.SetValue(LogsProperty, logFilter.FilteredLogs);
						this.SetValue(HasLogsProperty, logFilter.FilteredLogs.IsNotEmpty());
						return;
					}
				}
				this.SetValue(LogsProperty, this.AllLogs);
				this.SetValue(HasLogsProperty, this.allLogs.IsNotEmpty());
				this.SetValue(LastLogsFilteringDurationProperty, null);
				if (!this.logFilter.IsProcessingNeeded && this.Settings.GetValueOrDefault(SettingKeys.SaveMemoryAggressively))
				{
					this.Logger.LogDebug("Trigger GC after clearing log filters");
					GC.Collect();
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
					if (wasReadingLogs && this.Settings.GetValueOrDefault(SettingKeys.SaveMemoryAggressively))
					{
						this.Logger.LogDebug("Trigger GC after reading logs");
						GC.Collect();
					}
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
				else if (this.IsFilteringLogs || this.IsReadingLogs)
					this.SetValue(IsProcessingLogsProperty, true);
				else
					this.SetValue(IsProcessingLogsProperty, false);
			});
			this.updateLogFilterAction = new ScheduledAction(() =>
			{
				// check state
				if (this.IsDisposed || this.LogProfile == null)
					return;

				// setup level
				this.logFilter.Level = this.LogLevelFilter;

				// setup PID and TID
				this.logFilter.ProcessId = this.LogProcessIdFilter;
				this.logFilter.ThreadId = this.LogThreadIdFilter;

				// setup combination mode
				this.logFilter.CombinationMode = this.LogFiltersCombinationMode;

				// setup text regex
				List<Regex> textRegexList = new List<Regex>();
				this.LogTextFilter?.Let(it => textRegexList.Add(it));
				foreach (var filter in this.predefinedLogTextFilters)
					textRegexList.Add(filter.Regex);
				this.logFilter.TextRegexList = textRegexList;

				// cancel showing all.marked logs
				this.SetValue(IsShowingAllLogsTemporarilyProperty, false);
				this.SetValue(IsShowingMarkedLogsTemporarilyProperty, false);
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
						app?.Resources?.TryGetResource("Image/Icon.Tab", out res);
						return res as IImage;
					}
					else if (app != null)
						return LogProfileIconConverter.Default.Convert(logProfile.Icon, typeof(IImage), null, app.CultureInfo) as IImage;
					return null;
				});

				// select title
				var title = Global.Run(() =>
				{
					var customTitle = this.CustomTitle;
					if (logProfile == null)
						return customTitle ?? app?.GetString("Session.Empty");
					if (this.addedLogFilePaths.IsEmpty() || !logProfile.AllowMultipleFiles)
						return customTitle ?? logProfile.Name;
					return $"{customTitle ?? logProfile.Name} ({this.addedLogFilePaths.Count})";
				});

				// update properties
				this.SetValue(IconProperty, icon);
				this.SetValue(TitleProperty, title);
			});
			this.checkLogsMemoryUsageAction.Schedule();
			this.updateTitleAndIconAction.Execute();

			// restore state
			this.SetValue(SidePanelSizeProperty, this.PersistentState.GetValueOrDefault(latestSidePanelSizeKey));
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
					this.Logger.LogWarning($"Leave hibernation with {this.savedDataSourceOptions.Count} log reader(s)");

					// restore log readers
					this.RestoreLogReaders();

					// update state
					this.SetValue(IsHibernatedProperty, false);
				}
			});
		}


		// Add file.
		void AddLogFile(string? fileName)
		{
			// check parameter and state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.IsLogFileNeeded)
				return;
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));
			if (PathEqualityComparer.Default.Equals(Path.GetExtension(fileName), MarkedFileExtension))
			{
				this.Logger.LogWarning($"Ignore adding marked logs info file '{fileName}'");
				return;
			}
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to add log file.");
			var dataSourceOptions = profile.DataSourceOptions;
			if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				throw new InternalStateCorruptedException($"Cannot add log file because file name is already specified.");
			if (!this.addedLogFilePaths.Add(fileName))
			{
				this.Logger.LogWarning($"File '{fileName}' is already added");
				return;
			}

			this.Logger.LogDebug($"Add log file '{fileName}'");

			// create data source
			dataSourceOptions.FileName = fileName;
			var dataSource = this.CreateLogDataSourceOrNull(profile.DataSourceProvider, dataSourceOptions);
			if (dataSource == null)
				return;

			// create log reader
			this.CreateLogReader(dataSource);
		}


		/// <summary>
		/// Command to add log file.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="string"/>.</remarks>
		public ICommand AddLogFileCommand { get; }


		/// <summary>
		/// Get number of all read logs.
		/// </summary>
		public int AllLogCount { get => this.GetValue(AllLogCountProperty); }


		/// <summary>
		/// Get all logs without filtering.
		/// </summary>
		public IList<DisplayableLog> AllLogs { get; }


		/// <summary>
		/// Check whether logs are based-on one or more files or not.
		/// </summary>
		public bool AreFileBasedLogs { get => this.GetValue(AreFileBasedLogsProperty); }


		/// <summary>
		/// Check whether logs are sorted by one of <see cref="Log.BeginningTimestamp"/>, <see cref="Log.EndingTimestamp"/>, <see cref="Log.Timestamp"/> or not.
		/// </summary>
		public bool AreLogsSortedByTimestamp { get => this.GetValue(AreLogsSortedByTimestampProperty); }


		/// <summary>
		/// Get or set beginning timestamp of log reading precondition.
		/// </summary>
		public DateTime? BeginningPreconditionTimestamp
		{ 
			get => this.GetValue(BeginningPreconditionTimestampProperty);
			set => this.SetValue(BeginningPreconditionTimestampProperty, value); 
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
		public TimeSpan? CalculateDurationBetweenLogs(DisplayableLog x, DisplayableLog y, out TimeSpan? minTimeSpan, out TimeSpan? maxTimeSpan, out DateTime? earliestTimestamp, out DateTime? latestTimestamp) =>
			this.CalculateDurationBetweenLogs(x.Log, y.Log, out minTimeSpan, out maxTimeSpan, out earliestTimestamp, out latestTimestamp);


		// Calculate duration between two logs.
		TimeSpan? CalculateDurationBetweenLogs(Log x, Log y, out TimeSpan? minTimeSpan, out TimeSpan? maxTimeSpan, out DateTime? earliestTimestamp, out DateTime? latestTimestamp)
		{
			// get timestamps
			earliestTimestamp = x.SelectEarliestTimestamp();
			latestTimestamp = y.SelectLatestTimestamp();

			// get time spans
			minTimeSpan = x.SelectMinTimeSpan();
			maxTimeSpan = y.SelectMaxTimeSpan();

			// calculate duration
			if (earliestTimestamp != null && latestTimestamp != null
				&& earliestTimestamp.Value <= latestTimestamp.Value)
			{
				return latestTimestamp.Value - earliestTimestamp.Value;
			}
			if (minTimeSpan != null && maxTimeSpan != null
				&& minTimeSpan.Value <= maxTimeSpan.Value)
			{
				return (maxTimeSpan.Value - minTimeSpan.Value);
			}

			// get inverse timestamps
			earliestTimestamp = y.SelectEarliestTimestamp();
			latestTimestamp = x.SelectLatestTimestamp();

			// get inverse time spans
			minTimeSpan = y.SelectMinTimeSpan();
			maxTimeSpan = x.SelectMaxTimeSpan();

			// calculate duration
			if (earliestTimestamp != null && latestTimestamp != null
				&& earliestTimestamp.Value <= latestTimestamp.Value)
			{
				return latestTimestamp.Value - earliestTimestamp.Value;
			}
			if (minTimeSpan != null && maxTimeSpan != null
				&& minTimeSpan.Value <= maxTimeSpan.Value)
			{
				return (maxTimeSpan.Value - minTimeSpan.Value);
			}
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
			this.addedLogFilePaths.Clear();

			// update title
			this.updateTitleAndIconAction.Schedule();

			// reset log reading precondition
			this.ResetValue(BeginningPreconditionTimestampProperty);
			this.ResetValue(EndingPreconditionTimestampProperty);
		}


		/// <summary>
		/// Command to clear all added log files.
		/// </summary>
		public ICommand ClearLogFilesCommand { get; }


		// Compare displayable logs.
		int CompareDisplayableLogs(DisplayableLog? x, DisplayableLog? y) => this.compareDisplayableLogsDelegate(x, y);
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
		void CreateLogReader(ILogDataSource dataSource)
		{
			// prepare displayable log group
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to create log reader.");
			if (this.displayableLogGroup == null)
				this.displayableLogGroup = new DisplayableLogGroup(profile);

			// select logs reading task factory
			var readingTaskFactory = dataSource.CreationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName))
				? (fileLogsReadingTaskFactory ?? new TaskFactory(new FixedThreadsTaskScheduler(FileLogsReadingConcurrencyLevel)).Also(it => this.fileLogsReadingTaskFactory = it))
				: defaultLogsReadingTaskFactory;

			// create log reader
			var logReader = new LogReader(dataSource, readingTaskFactory).Also(it =>
			{
				if (profile.IsContinuousReading)
					it.UpdateInterval = this.ContinuousLogReadingUpdateInterval;
				if (this.GetValue(HasTimestampDisplayableLogPropertyProperty))
				{
					it.Precondition = new LogReadingPrecondition()
					{
						TimestampRange = (this.GetValue(BeginningPreconditionTimestampProperty), this.GetValue(EndingPreconditionTimestampProperty)),
					};
				}
				it.IsContinuousReading = profile.IsContinuousReading;
				it.LogLevelMap = profile.LogLevelMapForReading;
				if (profile.LogPatterns.IsNotEmpty())
					it.LogPatterns = profile.LogPatterns;
				else
					it.LogPatterns = new LogPattern[] { new LogPattern("^(?<Message>.*)", false, false) };
				it.LogStringEncoding = profile.LogStringEncodingForReading;
				if (profile.IsContinuousReading)
				{
					it.MaxLogCount = this.Settings.GetValueOrDefault(SettingKeys.MaxContinuousLogCount);
					it.RestartReadingDelay = TimeSpan.FromMilliseconds(profile.RestartReadingDelay);
				}
				it.TimeSpanCultureInfo = profile.TimeSpanCultureInfoForReading;
				it.TimeSpanEncoding = profile.TimeSpanEncodingForReading;
				it.TimeSpanFormats = profile.TimeSpanFormatsForReading;
				it.TimestampCultureInfo = profile.TimestampCultureInfoForReading;
				it.TimestampEncoding = profile.TimestampEncodingForReading;
				it.TimestampFormats = profile.TimestampFormatsForReading;
			});
			this.logReaders.Add(logReader);
			this.Logger.LogDebug($"Log reader '{logReader.Id} created");

			// add event handlers
			dataSource.PropertyChanged += this.OnLogDataSourcePropertyChanged;
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

			// load marked logs
			var creationOptions = dataSource.CreationOptions;
			if (creationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				this.LoadMarkedLogs(creationOptions.FileName.AsNonNull());

			// update state
			if (this.addedLogFilePaths.IsNotEmpty() 
				&& !profile.DataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
			{
				this.canClearLogFiles.Update(true);
			}
			this.canMarkUnmarkLogs.Update(true);
			this.canPauseResumeLogsReading.Update(profile.IsContinuousReading);
			this.canReloadLogs.Update(true);
			this.SetValue(UriProperty, creationOptions.Uri);
			if (creationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
			{
				this.SetValue(HasLogFilesProperty, true);
				if (!profile.AllowMultipleFiles)
					this.SetValue(IsLogFileNeededProperty, false);
			}
			if (creationOptions.IsOptionSet(nameof(LogDataSourceOptions.IPEndPoint)))
				this.SetValue(IPEndPointProperty, creationOptions.IPEndPoint);
			if (creationOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory)))
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
			var app = this.Application as App ?? throw new InternalStateCorruptedException("No application.");
			var writingFormat = profile.LogWritingFormat ?? "";

			// prepare log writer
			var logWriter = new RawLogWriter(dataOutput);
			logWriter.LogFormat = string.IsNullOrWhiteSpace(writingFormat)
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
								it.Append($"{{{propertyName.Substring(0, propertyName.Length - 6)}}}");
								break;
							case nameof(DisplayableLog.BeginningTimestampString):
							case nameof(DisplayableLog.EndingTimestampString):
							case nameof(DisplayableLog.TimestampString):
								it.Append($"{{{propertyName.Substring(0, propertyName.Length - 6)}}}");
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
					return it.ToString();
				})
				: writingFormat;
			logWriter.LogLevelMap = profile.LogLevelMapForWriting;
			logWriter.LogStringEncoding = profile.LogStringEncodingForWriting;
			logWriter.TimeSpanCultureInfo = profile.TimeSpanCultureInfoForWriting;
			logWriter.TimeSpanFormat = string.IsNullOrEmpty(profile.TimeSpanFormatForWriting)
				? profile.TimeSpanFormatsForReading.IsEmpty() ? profile.TimeSpanFormatForDisplaying : profile.TimeSpanFormatsForReading[0]
				: profile.TimeSpanFormatForWriting;
			logWriter.TimestampCultureInfo = profile.TimestampCultureInfoForWriting;
			logWriter.TimestampFormat = string.IsNullOrEmpty(profile.TimestampFormatForWriting)
				? profile.TimestampFormatsForReading.IsEmpty() ? profile.TimestampFormatForDisplaying : profile.TimestampFormatsForReading[0]
				: profile.TimestampFormatForWriting;
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
				if (this.Settings.GetValueOrDefault(SettingKeys.SaveMemoryAggressively))
					this.Hibernate();
			}
		}


		/// <summary>
		/// Get list of property of <see cref="DisplayableLog"/> needed to be shown on UI.
		/// </summary>
		public IList<DisplayableLogProperty> DisplayLogProperties { get => this.GetValue(DisplayLogPropertiesProperty); }


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

			// cancel filtering
			((INotifyCollectionChanged)this.logFilter.FilteredLogs).CollectionChanged -= this.OnFilteredLogsChanged;
			this.logFilter.PropertyChanged -= this.OnLogFilterPropertyChanged;
			this.logFilter.Dispose();

			// dispose log readers
			this.DisposeLogReaders();

			// release log group
			totalLogsMemoryUsage -= this.displayableLogGroup?.MemorySize ?? 0L;
			this.displayableLogGroup = this.displayableLogGroup.DisposeAndReturnNull();
			this.checkLogsMemoryUsageAction.Cancel();

			// detach from log profile
			this.LogProfile?.Let(it => it.PropertyChanged -= this.OnLogProfilePropertyChanged);

			// stop watches
			this.logsFilteringWatch.Stop();
			this.logsReadingWatch.Stop();

			// dispose task factories
			(this.fileLogsReadingTaskFactory?.Scheduler as IDisposable)?.Dispose();

			// remove from activation history
			if (this.activationHistoryListNode.List != null)
				activationHistoryList.Remove(this.activationHistoryListNode);

			// call base
			base.Dispose(disposing);
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
			dataSource.PropertyChanged -= this.OnLogDataSourcePropertyChanged;
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
			this.Logger.LogDebug($"Log reader '{logReader.Id} disposed");

			// check data source error
			this.checkDataSourceErrorsAction.Schedule();

			// check data source waiting state
			this.checkIsWaitingForDataSourcesAction.Schedule();

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
					this.SetValue(LastLogsFilteringDurationProperty, null);
					this.SetValue(UriProperty, null);
					this.SetValue(WorkingDirectoryNameProperty, null);
					this.SetValue(WorkingDirectoryPathProperty, null);
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
		public DateTime? EarliestLogTimestamp { get => this.GetValue(EarliestLogTimestampProperty); }


		/// <summary>
		/// Get or set ending timestamp of log reading precondition.
		/// </summary>
		public DateTime? EndingPreconditionTimestamp
		{ 
			get => this.GetValue(EndingPreconditionTimestampProperty);
			set => this.SetValue(EndingPreconditionTimestampProperty, value); 
		}


		/// <summary>
		/// Raised when error message generated.
		/// </summary>
		public event EventHandler<MessageEventArgs>? ErrorMessageGenerated;


		/// <summary>
		/// Get number of filtered logs.
		/// </summary>
		public int FilteredLogCount { get => this.GetValue(FilteredLogCountProperty); }


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
				LogSortKey.BeginningTimestamp => 
					new Func<DisplayableLog, long>(log => log.BinaryBeginningTimestamp),
				LogSortKey.EndingTimestamp =>
					new Func<DisplayableLog, long>(log => log.BinaryEndingTimestamp),
				LogSortKey.Timestamp =>
					new Func<DisplayableLog, long>(log => log.BinaryTimestamp),
				_ => null,
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
		public bool HasAllDataSourceErrors { get => this.GetValue(HasAllDataSourceErrorsProperty); }


		/// <summary>
		/// Check whether <see cref="CustomTitle"/> has been set or not.
		/// </summary>
		public bool HasCustomTitle { get => this.GetValue(HasCustomTitleProperty); }


		/// <summary>
		/// Check whether <see cref="IPEndPoint"/> is non-null or not.
		/// </summary>
		public bool HasIPEndPoint { get => this.GetValue(HasIPEndPointProperty); }


		/// <summary>
		/// Check whether <see cref="LastLogsFilteringDuration"/> is valid or not.
		/// </summary>
		public bool HasLastLogsFilteringDuration { get => this.GetValue(HasLastLogsFilteringDurationProperty); }


		/// <summary>
		/// Check whether <see cref="LastLogsReadingDuration"/> is valid or not.
		/// </summary>
		public bool HasLastLogsReadingDuration { get => this.GetValue(HasLastLogsReadingDurationProperty); }


		/// <summary>
		/// Check whether at least one log file was added to session or not.
		/// </summary>
		public bool HasLogFiles { get => this.GetValue(HasLogFilesProperty); }


		/// <summary>
		/// Check whether <see cref="LogProfile"/> is valid or not.
		/// </summary>
		public bool HasLogProfile { get => this.GetValue(HasLogProfileProperty); }


		/// <summary>
		/// Check whether at least one log reader created or not.
		/// </summary>
		public bool HasLogReaders { get => this.GetValue(HasLogReadersProperty); }


		/// <summary>
		/// Check whether at least one log is read or not.
		/// </summary>
		public bool HasLogs { get => this.GetValue(HasLogsProperty); }


		/// <summary>
		/// Check whether <see cref="LogsDuration"/> is valid or not.
		/// </summary>
		public bool HasLogsDuration { get => this.GetValue(HasLogsDurationProperty); }


		/// <summary>
		/// Check whether at least one log has been marked or not.
		/// </summary>
		public bool HasMarkedLogs { get => this.GetValue(HasMarkedLogsProperty); }


		/// <summary>
		/// Check whether errors are found in some of data sources or not.
		/// </summary>
		public bool HasPartialDataSourceErrors { get => this.GetValue(HasPartialDataSourceErrorsProperty); }


		/// <summary>
		/// Check whether at least one <see cref="PredefinedLogTextFilters"/> has been added to <see cref="PredefinedLogTextFilters"/> or not.
		/// </summary>
		public bool HasPredefinedLogTextFilters { get => this.GetValue(HasPredefinedLogTextFiltersProperty); }


		/// <summary>
		/// Check whether URI has been set or not.
		/// </summary>
		public bool HasUri { get => this.GetValue(HasUriProperty); }


		/// <summary>
		/// Check whether at least one property of <see cref="DisplayableLog"/> which represents timestamp will be shown in UI or not.
		/// </summary>
		public bool HasTimestampDisplayableLogProperty { get => this.GetValue(HasTimestampDisplayableLogPropertyProperty); }


		/// <summary>
		/// Check whether working directory has been set or not.
		/// </summary>
		public bool HasWorkingDirectory { get => this.GetValue(HasWorkingDirectoryProperty); }


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

			this.Logger.LogWarning($"Hibernate with {this.logReaders.Count} log reader(s) and {this.AllLogCount} log(s)");

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
		public IImage? Icon { get => this.GetValue(IconProperty); }


		/// <summary>
		/// Get current <see cref="IPEndPoint"/> to read logs from.
		/// </summary>
		public IPEndPoint? IPEndPoint { get => this.GetValue(IPEndPointProperty); }


		/// <summary>
		/// Check whether session has been activated or not.
		/// </summary>
		public bool IsActivated { get => this.GetValue(IsActivatedProperty); }


		/// <summary>
		/// Check whether logs copying is on-going or not.
		/// </summary>
		public bool IsCopyingLogs { get => this.GetValue(IsCopyingLogsProperty); }


		/// <summary>
		/// Check whether logs are being filtered or not.
		/// </summary>
		public bool IsFilteringLogs { get => this.GetValue(IsFilteringLogsProperty); }


		/// <summary>
		/// Check whether logs filtering is needed or not.
		/// </summary>
		public bool IsFilteringLogsNeeded { get => this.GetValue(IsFilteringLogsNeededProperty); }


		/// <summary>
		/// Check whether session is hibernated or not.
		/// </summary>
		public bool IsHibernated { get => this.GetValue(IsHibernatedProperty); }


		/// <summary>
		/// Check whether IP endpoint is needed or not.
		/// </summary>
		public bool IsIPEndPointNeeded { get => this.GetValue(IsIPEndPointNeededProperty); }


		/// <summary>
		/// Check whether logs file is needed or not.
		/// </summary>
		public bool IsLogFileNeeded { get => this.GetValue(IsLogFileNeededProperty); }


		/// <summary>
		/// Check whether logs reading has been paused or not.
		/// </summary>
		public bool IsLogsReadingPaused { get => this.GetValue(IsLogsReadingPausedProperty); }


		/// <summary>
		/// Check whether logs are being processed or not.
		/// </summary>
		public bool IsProcessingLogs { get => this.GetValue(IsProcessingLogsProperty); }


		/// <summary>
		/// Check whether logs are being read or not.
		/// </summary>
		public bool IsReadingLogs { get => this.GetValue(IsReadingLogsProperty); }


		/// <summary>
		/// Check whether logs are being read continuously or not.
		/// </summary>
		public bool IsReadingLogsContinuously { get => this.GetValue(IsReadingLogsContinuouslyProperty); }


		/// <summary>
		/// Check whether logs saving is on-going or not.
		/// </summary>
		public bool IsSavingLogs { get => this.GetValue(IsSavingLogsProperty); }


		/// <summary>
		/// Check whether all logs are shown temporarily.
		/// </summary>
		public bool IsShowingAllLogsTemporarily { get => this.GetValue(IsShowingAllLogsTemporarilyProperty); }


		/// <summary>
		/// Check whether showing marked logs temporarily or not.
		/// </summary>
		public bool IsShowingMarkedLogsTemporarily { get => this.GetValue(IsShowingMarkedLogsTemporarilyProperty); }


		/// <summary>
		/// Get or set whether side panel is visible or not.
		/// </summary>
		public bool IsSidePanelVisible 
		{
			 get => this.GetValue(IsSidePanelVisibleProperty);
			 set => this.SetValue(IsSidePanelVisibleProperty, value);
		}


		/// <summary>
		/// Check whether URI is needed or not.
		/// </summary>
		public bool IsUriNeeded { get => this.GetValue(IsUriNeededProperty); }


		/// <summary>
		/// Check data sources are not ready for reading logs.
		/// </summary>
		public bool IsWaitingForDataSources { get => this.GetValue(IsWaitingForDataSourcesProperty); }


		/// <summary>
		/// Check whether working directory is needed or not.
		/// </summary>
		public bool IsWorkingDirectoryNeeded { get => this.GetValue(IsWorkingDirectoryNeededProperty); }


		/// <summary>
		/// Get the duration of last logs filtering.
		/// </summary>
		public TimeSpan? LastLogsFilteringDuration { get => this.GetValue(LastLogsFilteringDurationProperty); }


		/// <summary>
		/// Get the duration of last logs reading.
		/// </summary>
		public TimeSpan? LastLogsReadingDuration { get => this.GetValue(LastLogsReadingDurationProperty); }


		/// <summary>
		/// Get latest timestamp of log in <see cref="Logs"/>.
		/// </summary>
		public DateTime? LatestLogTimestamp { get => this.GetValue(LatestLogTimestampProperty); }


		// Load marked logs from file.
		async void LoadMarkedLogs(string fileName)
		{
			this.Logger.LogTrace($"Request loading marked log(s) of '{fileName}'");

			// load marked logs from file
			var markedLogInfos = await ioTaskFactory.StartNew(() =>
			{
				var markedLogInfos = new List<MarkedLogInfo>();
				var markedFileName = fileName + MarkedFileExtension;
				if (!System.IO.File.Exists(markedFileName))
				{
					this.Logger.LogTrace($"Marked log file '{markedFileName}' not found");
					return Array.Empty<MarkedLogInfo>();
				}
				try
				{
					this.Logger.LogTrace($"Start loading marked log file '{markedFileName}'");
					if (!IO.File.TryOpenRead(markedFileName, DefaultFileOpeningTimeout, out var stream) || stream == null)
					{
						this.Logger.LogError($"Unable to open marked file to load: {markedFileName}");
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
												if (jsonObject.TryGetProperty("MarkedColor", out var colorProperty) && colorProperty.ValueKind == JsonValueKind.String)
													Enum.TryParse(colorProperty.GetString(), out color);
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
					this.Logger.LogTrace($"Complete loading marked log file '{markedFileName}', {markedLogInfos.Count} marked log(s) found");
				}
				catch (Exception ex)
				{
					this.Logger.LogError(ex, $"Unable to load marked log file: {markedFileName}");
					return Array.Empty<MarkedLogInfo>();
				}
				return markedLogInfos.ToArray();
			});

			// check state
			if (markedLogInfos.IsEmpty())
				return;
			if (!this.addedLogFilePaths.Contains(fileName))
				return;

			// add as unmatched 
			this.unmatchedMarkedLogInfos.AddRange(markedLogInfos);

			// match
			this.MatchMarkedLogs();
		}


		/// <summary>
		/// Get or set mode to combine condition of <see cref="LogTextFilter"/> and other conditions for logs filtering.
		/// </summary>
		public FilterCombinationMode LogFiltersCombinationMode 
		{
			get => this.GetValue(LogFiltersCombinationModeProperty);
			set => this.SetValue(LogFiltersCombinationModeProperty, value);
		}


		/// <summary>
		/// Get or set level to filter logs.
		/// </summary>
		public Logs.LogLevel LogLevelFilter 
		{ 
			get => this.GetValue(LogLevelFilterProperty);
			set => this.SetValue(LogLevelFilterProperty, value);
		}


		/// <summary>
		/// Get or set process ID to filter logs.
		/// </summary>
		public int? LogProcessIdFilter
		{
			get => this.GetValue(LogProcessIdFilterProperty);
			set => this.SetValue(LogProcessIdFilterProperty, value);
		}


		/// <summary>
		/// Get current log profile.
		/// </summary>
		public LogProfile? LogProfile { get => this.GetValue(LogProfileProperty); }


		/// <summary>
		/// Get list of <see cref="DisplayableLog"/>s to display.
		/// </summary>
		public IList<DisplayableLog> Logs { get => this.GetValue(LogsProperty); }


		/// <summary>
		/// Get duration of <see cref="Logs"/>.
		/// </summary>
		public TimeSpan? LogsDuration { get => this.GetValue(LogsDurationProperty); }


		/// <summary>
		/// Get string to describe ending point of <see cref="LogsDuration"/>.
		/// </summary>
		public string? LogsDurationEndingString { get => this.GetValue(LogsDurationEndingStringProperty); }


		/// <summary>
		/// Get string to describe starting point of <see cref="LogsDuration"/>.
		/// </summary>
		public string? LogsDurationStartingString { get => this.GetValue(LogsDurationStartingStringProperty); }


		/// <summary>
		/// Get progress of logs filtering.
		/// </summary>
		public double LogsFilteringProgress { get => this.GetValue(LogsFilteringProgressProperty); }


		/// <summary>
		/// Get size of memory usage of logs by the <see cref="Session"/> instance in bytes.
		/// </summary>
		public long LogsMemoryUsage { get => this.GetValue(LogsMemoryUsageProperty); }


		/// <summary>
		/// Get or set <see cref="Regex"/> for log text filtering.
		/// </summary>
		public Regex? LogTextFilter
		{
			get => this.GetValue(LogTextFilterProperty);
			set => this.SetValue(LogTextFilterProperty, value);
		}


		/// <summary>
		/// Get or set thread ID to filter logs.
		/// </summary>
		public int? LogThreadIdFilter
		{
			get => this.GetValue(LogThreadIdFilterProperty);
			set => this.SetValue(LogThreadIdFilterProperty, value);
		}


		/// <summary>
		/// Get list of marked <see cref="DisplayableLog"/>s .
		/// </summary>
		public IList<DisplayableLog> MarkedLogs { get; }


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
						this.logFilter.InvalidateLog(log);
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
		/// <remarks>Type of parmeter is <see cref="MarkingLogsParams"/>.</remarks>
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
							this.logFilter.InvalidateLog(log);
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
						this.logFilter.InvalidateLog(log);
						log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
					}
				}
			}

			// schedule save to file action
			this.saveMarkedLogsAction.Schedule(DelaySaveMarkedLogs);
		}


		/// <summary>
		/// Command to mark or ummark logs.
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
						this.logFilter.InvalidateLog(log);
					}
					this.unmatchedMarkedLogInfos.RemoveAt(i);
				}
			}
			if (this.unmatchedMarkedLogInfos.IsEmpty())
				this.Logger.LogTrace("All marked logs are matched");
			else
				this.Logger.LogTrace($"{this.unmatchedMarkedLogInfos.Count} marked log(s) are unmatched");
		}


		// Called when logs in allLogs has been changed.
		void OnAllLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Remove:
					this.markedLogs.RemoveAll(e.OldItems.AsNonNull().Cast<DisplayableLog>(), true);
					DisposeDisplayableLogs(e.OldItems.AsNonNull().Cast<DisplayableLog>());
					break;
				case NotifyCollectionChangedAction.Reset:
					this.markedLogs.Clear();
					this.markedLogs.AddAll(this.allLogs.TakeWhile(it => it.MarkedColor != MarkColor.None));
					break;
			}
			if (!this.IsDisposed)
			{
				if ((!this.logFilter.IsProcessingNeeded && !this.IsShowingMarkedLogsTemporarily) || this.IsShowingAllLogsTemporarily)
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


		// Called when filtered logs has been changed.
		void OnFilteredLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (!this.IsDisposed)
			{
				if (this.logFilter.IsProcessingNeeded && !this.IsShowingAllLogsTemporarily && !this.IsShowingMarkedLogsTemporarily)
				{
					this.SetValue(HasLogsProperty, this.logFilter.FilteredLogs.IsNotEmpty());
					this.reportLogsTimeInfoAction.Schedule(LogsTimeInfoReportingInterval);
				}
				this.SetValue(FilteredLogCountProperty, this.logFilter.FilteredLogs.Count);
			}
		}


		// Called when property of log data source changed.
		void OnLogDataSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ILogDataSource.State))
				this.checkDataSourceErrorsAction.Schedule();
		}


		// Called when property of log filter changed.
		void OnLogFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(DisplayableLogFilter.IsProcessing):
					if(this.logFilter.IsProcessing)
					{
						if (!this.logsFilteringWatch.IsRunning)
							this.logsFilteringWatch.Restart();
						this.SetValue(IsFilteringLogsProperty, true);
					}
					else
					{
						this.logsFilteringWatch.Stop();
						this.SetValue(IsFilteringLogsProperty, false);
						this.SetValue(LastLogsFilteringDurationProperty, TimeSpan.FromMilliseconds(this.logsFilteringWatch.ElapsedMilliseconds));
						if (this.Settings.GetValueOrDefault(SettingKeys.SaveMemoryAggressively))
						{
							this.Logger.LogDebug("Trigger GC after filtering logs");
							GC.Collect();
						}
					}
					break;
				case nameof(DisplayableLogFilter.IsProcessingNeeded):
					this.canShowAllLogsTemporarily.Update(this.LogProfile != null && this.logFilter.IsProcessingNeeded);
					this.selectLogsToReportActions.Schedule();
					this.SetValue(IsFilteringLogsNeededProperty, this.logFilter.IsProcessingNeeded);
					break;
				case nameof(DisplayableLogFilter.Progress):
					this.SetValue(LogsFilteringProgressProperty, this.logFilter.Progress);
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
								this.ReloadLogs(true, false);
							else
							{
								if (this.addedLogFilePaths.Count > 1)
									this.ClearLogFiles();
								else
									this.ReloadLogs(true, false);
							}
						}
					});
					break;
				case nameof(LogProfile.ColorIndicator):
					this.SynchronizationContext.Post(() => this.ReloadLogs(false, true));
					break;
				case nameof(LogProfile.DataSourceOptions):
					this.SynchronizationContext.Post(() => this.ReloadLogs(true, false));
					break;
				case nameof(LogProfile.DataSourceProvider):
					this.SynchronizationContext.Post(() => this.ReloadLogs(true, true));
					break;
				case nameof(LogProfile.Icon):
				case nameof(LogProfile.Name):
					this.updateTitleAndIconAction.Schedule();
					break;
				case nameof(LogProfile.IsContinuousReading):
					this.SetValue(IsReadingLogsContinuouslyProperty, this.LogProfile.AsNonNull().IsContinuousReading);
					goto case nameof(LogProfile.LogPatterns);
				case nameof(LogProfile.LogLevelMapForReading):
					this.UpdateValidLogLevels();
					goto case nameof(LogProfile.LogPatterns);
				case nameof(LogProfile.LogWritingFormat):
					this.UpdateIsLogsWritingAvailable(this.LogProfile);
					break;
				case nameof(LogProfile.LogPatterns):
				case nameof(LogProfile.LogStringEncodingForReading):
				case nameof(LogProfile.SortDirection):
				case nameof(LogProfile.SortKey):
				case nameof(LogProfile.TimestampCultureInfoForReading):
				case nameof(LogProfile.TimestampFormatForDisplaying):
				case nameof(LogProfile.TimestampEncodingForReading):
				case nameof(LogProfile.TimestampFormatsForReading):
					this.SynchronizationContext.Post(() => this.ReloadLogs(true, false));
					break;
				case nameof(LogProfile.RestartReadingDelay):
					(sender as LogProfile)?.Let(profile =>
					{
						if (profile.IsContinuousReading && this.logReaders.IsNotEmpty())
							this.logReaders[0].RestartReadingDelay = TimeSpan.FromMilliseconds(profile.RestartReadingDelay);
					});
					break;
				case nameof(LogProfile.VisibleLogProperties):
					this.SynchronizationContext.Post(() => this.ReloadLogs(false, true));
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
								this.allLogs.RemoveAll(it => removedLogs.Contains(it.Log));
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
						if (reader.State == LogReaderState.DataSourceError)
						{
							var source = reader.DataSource;
							if (source.State == LogDataSourceState.SourceNotFound
								&& source is StandardOutputLogDataSource)
							{
								var match = new Regex("^((?<Command>[^\"\\s]+)|\"(?<Command>[^\"]+)\"|'(?<Command>[^']+)')").Match(source.CreationOptions.Command ?? "");
								if (match.Success)
								{
									var message = this.Application.GetFormattedString("Session.Message.SourceNotFound.StandardOutput", match.Groups["Command"].Value);
									this.ErrorMessageGenerated?.Invoke(this, new MessageEventArgs(message));
								}
							}
						}
					});
					this.updateIsReadingLogsAction.Schedule();
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
			else if (property == IsFilteringLogsProperty
				|| property == IsReadingLogsProperty)
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
			else if (property == LastLogsFilteringDurationProperty)
				this.SetValue(HasLastLogsFilteringDurationProperty, this.LastLogsFilteringDuration != null);
			else if (property == LogFiltersCombinationModeProperty
				|| property == LogLevelFilterProperty
				|| property == LogProcessIdFilterProperty
				|| property == LogTextFilterProperty
				|| property == LogThreadIdFilterProperty)
			{
				this.updateLogFilterAction.Schedule();
			}
			else if (property == LogsDurationProperty)
				this.SetValue(HasLogsDurationProperty, newValue != null);
			else if (property == LogProfileProperty)
				this.SetValue(HasLogProfileProperty, newValue != null);
			else if (property == LogsProperty)
				this.reportLogsTimeInfoAction?.Reschedule();
			else if (property == SidePanelSizeProperty)
			{
				if (!this.isRestoringState)
					this.PersistentState.SetValue<double>(latestSidePanelSizeKey, (double)newValue.AsNonNull());
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
			else if (e.Key == SettingKeys.MaxContinuousLogCount)
			{
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().MaxLogCount = (int)e.Value;
			}
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


		/// <summary>
		/// Get list of <see cref="PredefinedLogTextFilters"/>s to filter logs.
		/// </summary>
		public IList<PredefinedLogTextFilter> PredefinedLogTextFilters { get => this.predefinedLogTextFilters; }


		// Reload logs.
		void ReloadLogs(bool recreateLogReaders, bool updateDisplayLogProperties)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			var profile = this.LogProfile;
			if (profile == null)
				throw new InternalStateCorruptedException("No log profile to reload logs.");

			// clear logs
			var isContinuousReading = profile.IsContinuousReading;
			if (isContinuousReading && !recreateLogReaders)
			{
				foreach (var logReader in this.logReaders)
					logReader.ClearLogs();
				this.Logger.LogWarning($"Clear logs in {this.logReaders.Count} log reader(s)");
			}
			else
			{
				// save log readers
				this.SaveLogReaders();

				this.Logger.LogWarning($"Reload logs with {this.savedDataSourceOptions.Count} log reader(s)");

				// dispose log readers
				this.DisposeLogReaders();
			}

			// setup log comparer
			this.UpdateDisplayableLogComparison();

			// update display log properties
			if (updateDisplayLogProperties)
				this.UpdateDisplayLogProperties();

			// recreate log readers
			if (this.logReaders.IsEmpty())
				this.RestoreLogReaders();
		}


		/// <summary>
		/// Command to reload logs.
		/// </summary>
		public ICommand ReloadLogsCommand { get; }


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

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// detach from log profile
			profile.PropertyChanged -= this.OnLogProfilePropertyChanged;

			// update state
			this.SetValue(AreFileBasedLogsProperty, false);
			this.SetValue(AreLogsSortedByTimestampProperty, false);
			this.canResetLogProfile.Update(false);
			this.canShowAllLogsTemporarily.Update(false);
			this.SetValue(IsIPEndPointNeededProperty, false);
			this.SetValue(IsLogFileNeededProperty, false);
			this.SetValue(IsReadingLogsContinuouslyProperty, false);
			this.SetValue(IsUriNeededProperty, false);
			this.SetValue(IsWorkingDirectoryNeededProperty, false);
			this.UpdateIsLogsWritingAvailable(null);
			this.UpdateValidLogLevels();

			// clear profile
			this.Logger.LogWarning($"Reset log profile '{profile.Name}'");
			this.SetValue(LogProfileProperty, null);

			// cancel filtering
			this.logFilter.FilteringLogProperties = DisplayLogPropertiesProperty.DefaultValue;
			this.updateLogFilterAction.Cancel();

			// dispose log readers
			this.DisposeLogReaders();

			// clear file name table
			this.addedLogFilePaths.Clear();

			// clear log reading precondition
			this.ResetValue(BeginningPreconditionTimestampProperty);
			this.ResetValue(EndingPreconditionTimestampProperty);

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
				this.Logger.LogError($"No log profile to restore {this.savedDataSourceOptions.Count} log reader(s)");
				this.savedDataSourceOptions.Clear();
				return;
			}

			this.Logger.LogWarning($"Start restoring {this.savedDataSourceOptions.Count} log reader(s)");

			// dispose current log readers
			this.DisposeLogReaders();

			// restore to default source options
			var dataSourceProvider = profile.DataSourceProvider;
			var defaultDataSourceOptions = profile.DataSourceOptions;
			var useDefaultDataSourceOptions = false;
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)))
			{
				this.SetValue(AreFileBasedLogsProperty, true);
				if (defaultDataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				{
					useDefaultDataSourceOptions = true;
					this.addedLogFilePaths.Clear();
					this.addedLogFilePaths.Add(defaultDataSourceOptions.FileName.AsNonNull());
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
				this.savedDataSourceOptions.Clear();
				this.savedDataSourceOptions.Add(defaultDataSourceOptions);
			}
			foreach (var dataSourceOption in this.savedDataSourceOptions)
			{
				// apply default options
				var newDataSourceOptions = dataSourceOption;
				newDataSourceOptions.Command = defaultDataSourceOptions.Command;
				newDataSourceOptions.Encoding = defaultDataSourceOptions.Encoding;
				newDataSourceOptions.SetupCommands = defaultDataSourceOptions.SetupCommands;
				newDataSourceOptions.TeardownCommands = defaultDataSourceOptions.TeardownCommands;

				// create data source and reader
				var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, newDataSourceOptions);
				if (dataSource != null)
					this.CreateLogReader(dataSource);
				else
				{
					this.hasLogDataSourceCreationFailure = true;
					this.checkDataSourceErrorsAction.Schedule();
				}
			}
			this.savedDataSourceOptions.Clear();

			this.Logger.LogWarning($"Complete restoring {this.logReaders.Count} log reader(s)");
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
				if (!LogProfiles.TryFindProfileById(jsonValue.GetString().AsNonNull(), out var profile) || profile == null)
				{
					this.Logger.LogWarning($"Unable to find log profile '{jsonValue.GetString()}' to restore state");
					return;
				}
				this.SetLogProfile(profile, false);

				// setup log reading precondition
				if (jsonState.TryGetProperty(nameof(BeginningPreconditionTimestamp), out jsonValue) && jsonValue.TryGetInt64(out var longValue))
					this.SetValue(BeginningPreconditionTimestampProperty, DateTime.FromBinary(longValue));
				if (jsonState.TryGetProperty(nameof(EndingPreconditionTimestamp), out jsonValue) && jsonValue.TryGetInt64(out longValue))
					this.SetValue(EndingPreconditionTimestampProperty, DateTime.FromBinary(longValue));

				// create log readers
				if (jsonState.TryGetProperty("LogReaders", out jsonValue) && jsonValue.ValueKind == JsonValueKind.Array)
				{
					// restore data source options
					this.savedDataSourceOptions.Clear();
					foreach (var jsonLogReader in jsonValue.EnumerateArray())
					{
						// get data source options
						if (!jsonLogReader.TryGetProperty("Options", out jsonValue) || jsonValue.ValueKind != JsonValueKind.Object)
							continue;
						var options = new LogDataSourceOptions();
						try
						{
							options = LogDataSourceOptions.Load(jsonValue);
							this.savedDataSourceOptions.Add(options);
						}
						catch (Exception ex)
						{
							this.Logger.LogError(ex, "Failed to restore data source options");
							continue;
						}

						// check file paths
						if (options.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
							this.addedLogFilePaths.Add(options.FileName.AsNonNull());
					}
					this.Logger.LogTrace($"{this.savedDataSourceOptions.Count} log reader(s) can be restored");

					// restore log readers
					if (!this.IsHibernated)
						this.RestoreLogReaders();
					else
						this.Logger.LogWarning("No need to restore log reader(s) because session is hibernated");
				}
				else
					this.Logger.LogTrace("No log readers to restore");

				// restore filtering parameters
				if (jsonState.TryGetProperty(nameof(LogFiltersCombinationMode), out jsonValue)
					&& jsonValue.ValueKind == JsonValueKind.String
					&& Enum.TryParse<FilterCombinationMode>(jsonValue.GetString(), out var combinationMode))
				{
					this.LogFiltersCombinationMode = combinationMode;
				}
				if (jsonState.TryGetProperty(nameof(LogLevelFilter), out jsonValue)
					&& jsonValue.ValueKind == JsonValueKind.String
					&& Enum.TryParse<Logs.LogLevel>(jsonValue.GetString(), out var level))
				{
					this.LogLevelFilter = level;
				}
				if (jsonState.TryGetProperty(nameof(LogProcessIdFilter), out jsonValue)
					&& jsonValue.TryGetInt32(out var pid))
				{
					this.LogProcessIdFilter = pid;
				}
				if (jsonState.TryGetProperty(nameof(LogTextFilter), out jsonValue)
					&& jsonValue.ValueKind == JsonValueKind.Object
					&& jsonValue.TryGetProperty("Pattern", out var jsonPattern)
					&& jsonPattern.ValueKind == JsonValueKind.String)
				{
					var options = RegexOptions.None;
					jsonValue.TryGetProperty("Options", out var jsonOptions);
					if (jsonOptions.TryGetInt32(out var optionsValue))
						options = (RegexOptions)optionsValue;
					try
					{
						this.LogTextFilter = new Regex(jsonPattern.GetString().AsNonNull(), options);
					}
					catch (Exception ex)
					{
						this.Logger.LogError(ex, "Unable to restore log text filter");
					}
				}
				if (jsonState.TryGetProperty(nameof(LogThreadIdFilter), out jsonValue)
					&& jsonValue.TryGetInt32(out var tid))
				{
					this.LogThreadIdFilter = tid;
				}

				// restore side panel state
				if (jsonState.TryGetProperty(nameof(IsSidePanelVisible), out jsonValue))
					this.SetValue(IsSidePanelVisibleProperty, jsonValue.ValueKind != JsonValueKind.False);
				if (jsonState.TryGetProperty(nameof(SidePanelSize), out jsonValue) 
					&& jsonValue.TryGetDouble(out var doubleValue)
					&& SidePanelSizeProperty.ValidationFunction?.Invoke(doubleValue) == true)
				{
					this.SetValue(SidePanelSizeProperty, doubleValue);
				}

				this.Logger.LogTrace("Complete restoring state");
			}
			finally
			{
				this.isRestoringState = false;
			}
		}


		// Save state of log readers in memory.
		void SaveLogReaders()
        {
			// prepare
			this.savedDataSourceOptions.Clear();

			// check log reader
			if (this.logReaders.IsEmpty())
			{
				this.Logger.LogDebug("No log reader to save");
				return;
			}

			this.Logger.LogWarning($"Start saving {this.logReaders.Count} log reader(s)");

			// save marked logs
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// save data source options
			foreach (var logReader in this.logReaders)
				this.savedDataSourceOptions.Add(logReader.DataSource.CreationOptions);

			this.Logger.LogWarning($"Complete saving {this.savedDataSourceOptions.Count} log reader(s)");
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
			using var dataOutput = new FileLogDataOutput((IULogViewerApplication)this.Application, options.FileName.AsNonNull());
			using var logWriter = options switch
			{
				JsonLogsSavingOptions jsonSavingOptions => new JsonLogWriter(dataOutput).Also(it =>
				{
					it.LogLevelMap = profile.LogLevelMapForWriting;
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
				_ => (ILogWriter)(this.CreateRawLogWriter(dataOutput).Also(it =>
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
				})),
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
							var color = MarkColor.None;
							markedColorMap.TryGetValue(pair.Key, out color);
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
					this.Logger.LogTrace($"Include {count} unmatched marked info of '{logFileName}'");
				}
			});
			this.SaveMarkedLogs(logFileName, markedLogInfos);
		}
		void SaveMarkedLogs(string logFileName, IList<MarkedLogInfo> markedLogInfos)
		{
			this.Logger.LogTrace($"Request saving {markedLogInfos.Count} marked log(s) of '{logFileName}'");

			// save or delete marked file
			var task = ioTaskFactory.StartNew(() =>
			{
				var markedFileName = logFileName + MarkedFileExtension;
				if (markedLogInfos.IsEmpty())
				{
					this.Logger.LogTrace($"Delete marked log file '{markedFileName}'");
					Global.RunWithoutError(() => System.IO.File.Delete(markedFileName));
				}
				else
				{
					this.Logger.LogTrace($"Start saving {markedLogInfos.Count} marked log(s) to '{markedFileName}'");
					try
					{
						if (!IO.File.TryOpenReadWrite(markedFileName, DefaultFileOpeningTimeout, out var stream) || stream == null)
						{
							this.Logger.LogError($"Unable to open marked file to save: {markedFileName}");
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
						this.Logger.LogError(ex, $"Unable to save marked file: {markedFileName}");
					}
					this.Logger.LogTrace($"Complete saving {markedLogInfos.Count} marked log(s) to '{markedFileName}'");
				}
			});
			_ = this.WaitForNecessaryTaskAsync(task);
		}


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

			this.Logger.LogDebug($"Set IP endpoint to {endPoint.Address} ({endPoint.Port})");

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
			this.CreateLogReader(dataSource);
		}


		/// <summary>
		/// Command to set IP endpoint.
		/// </summary>
		public ICommand SetIPEndPointCommand { get; }


		// Set log profile.
		void SetLogProfile(LogProfile? profile) => this.SetLogProfile(profile, true);
		void SetLogProfile(LogProfile? profile, bool startReadingLogs)
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

			// set profile
			this.Logger.LogWarning($"Set profile '{profile.Name}'");
			this.canSetLogProfile.Update(false);
			this.SetValue(IsReadingLogsContinuouslyProperty, profile.IsContinuousReading);
			this.SetValue(LogProfileProperty, profile);

			// attach to log profile
			profile.PropertyChanged += this.OnLogProfilePropertyChanged;

			// setup log comparer
			this.UpdateDisplayableLogComparison();

			// update valid log levels
			this.UpdateValidLogLevels();

			// reset log reading precondition
			this.ResetValue(BeginningPreconditionTimestampProperty);
			this.ResetValue(EndingPreconditionTimestampProperty);

			// read logs or wait for more actions
			var dataSourceOptions = profile.DataSourceOptions;
			var dataSourceProvider = profile.DataSourceProvider;
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)))
			{
				this.SetValue(AreFileBasedLogsProperty, true);
				if (dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
					this.addedLogFilePaths.Add(dataSourceOptions.FileName.AsNonNull());
				else
				{
					this.Logger.LogDebug("No file name specified, waiting for adding file");
					this.SetValue(IsLogFileNeededProperty, true);
					startReadingLogs = false;
				}
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.IPEndPoint)))
			{
				if (!dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.IPEndPoint)))
				{
					this.Logger.LogDebug("No IP endpoint specified, waiting for setting IP endpoint");
					this.SetValue(IsIPEndPointNeededProperty, true);
					startReadingLogs = false;
				}
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Uri)))
			{
				if (!dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.Uri)))
				{
					this.Logger.LogDebug("Need URI, waiting for setting URI");
					this.SetValue(IsUriNeededProperty, true);
					startReadingLogs = false;
				}
			}
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.WorkingDirectory))
				|| profile.IsWorkingDirectoryNeeded)
			{
				if (!dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.WorkingDirectory)))
				{
					this.Logger.LogDebug("Need working directory, waiting for setting working directory");
					this.SetValue(IsWorkingDirectoryNeededProperty, true);
					startReadingLogs = false;
				}
			}
			if (startReadingLogs)
			{
				this.Logger.LogDebug($"Start reading logs for source '{dataSourceProvider.Name}'");
				var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, dataSourceOptions);
				if (dataSource != null)
					this.CreateLogReader(dataSource);
				else
				{
					this.hasLogDataSourceCreationFailure = true;
					this.checkDataSourceErrorsAction.Schedule();
				}
			}

			// update display log properties
			this.UpdateDisplayLogProperties();

			// update title
			this.updateTitleAndIconAction.Schedule();

			// update log filter
			this.updateLogFilterAction.Reschedule();

			// update state
			this.UpdateIsLogsWritingAvailable(profile);
			this.canResetLogProfile.Update(true);
			this.canShowAllLogsTemporarily.Update(this.logFilter.IsProcessingNeeded);
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

				// save log readers
				if (this.savedDataSourceOptions.IsEmpty())
					this.SaveLogReaders();
				jsonWriter.WritePropertyName("LogReaders");
				jsonWriter.WriteStartArray();
				foreach (var dataSourceOptions in this.savedDataSourceOptions)
				{
					jsonWriter.WriteStartObject();
					jsonWriter.WritePropertyName("Options");
					dataSourceOptions.Save(jsonWriter);
					jsonWriter.WriteEndObject();
				}
				jsonWriter.WriteEndArray();

				// save log reading precondition
				this.GetValue(BeginningPreconditionTimestampProperty)?.Let(it =>
					jsonWriter.WriteNumber(nameof(BeginningPreconditionTimestamp), it.ToBinary()));
				this.GetValue(EndingPreconditionTimestampProperty)?.Let(it =>
					jsonWriter.WriteNumber(nameof(EndingPreconditionTimestamp), it.ToBinary()));

				// save filtering parameters
				jsonWriter.WriteString(nameof(LogFiltersCombinationMode), this.LogFiltersCombinationMode.ToString());
				jsonWriter.WriteString(nameof(LogLevelFilter), this.LogLevelFilter.ToString());
				this.LogProcessIdFilter?.Let(it => jsonWriter.WriteNumber(nameof(LogProcessIdFilter), it));
				this.LogTextFilter?.Let(it =>
				{
					jsonWriter.WritePropertyName(nameof(LogTextFilter));
					jsonWriter.WriteStartObject();
					jsonWriter.WriteString("Pattern", it.ToString());
					jsonWriter.WriteNumber("Options", (int)it.Options);
					jsonWriter.WriteEndObject();
				});
				this.LogThreadIdFilter?.Let(it => jsonWriter.WriteNumber(nameof(LogThreadIdFilter), it));
				if (this.predefinedLogTextFilters.IsNotEmpty())
				{
					jsonWriter.WritePropertyName(nameof(PredefinedLogTextFilters));
					jsonWriter.WriteStartArray();
					foreach (var filter in this.predefinedLogTextFilters)
						jsonWriter.WriteStringValue(filter.Id);
					jsonWriter.WriteEndArray();
				}

				// save side panel state
				jsonWriter.WriteBoolean(nameof(IsSidePanelVisible), this.IsSidePanelVisible);
				jsonWriter.WriteNumber(nameof(SidePanelSize), this.SidePanelSize);
			});
			jsonWriter.WriteEndObject();
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

			this.Logger.LogDebug($"Set URI to '{uri}'");

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
			this.CreateLogReader(dataSource);
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

			this.Logger.LogDebug($"Set working directory to '{directory}'");

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
			this.CreateLogReader(dataSource);
		}


		/// <summary>
		/// Command to set working directory.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="string"/>.</remarks>
		public ICommand SetWorkingDirectoryCommand { get; }


		/// <summary>
		/// Get or set size of side panel.
		/// </summary>
		public double SidePanelSize
		{
			 get => this.GetValue(SidePanelSizeProperty);
			 set => this.SetValue(SidePanelSizeProperty, value);
		}


		/// <summary>
		/// Get title of session.
		/// </summary>
		public string? Title { get => this.GetValue(TitleProperty); }


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
		public long TotalLogsMemoryUsage { get => this.GetValue(TotalLogsMemoryUsageProperty); }


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
						this.logFilter.InvalidateLog(log);
					log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
				}
			}

			// schedule save to file action
			this.saveMarkedLogsAction.Schedule(DelaySaveMarkedLogs);
		}


		/// <summary>
		/// Command to unmark logs.
		/// </summary>
		/// <remarks>Type of parmeter is <see cref="IEnumerable{DisplayableLog}"/>.</remarks>
		public ICommand UnmarkLogsCommand { get; }


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
				this.logFilter.FilteringLogProperties = DisplayLogPropertiesProperty.DefaultValue;
			}
			else
			{
				var app = (IULogViewerApplication)this.Application;
				var visibleLogProperties = profile.VisibleLogProperties;
				var displayLogProperties = new List<DisplayableLogProperty>();
				var hasTimestamp = false;
				foreach (var logProperty in visibleLogProperties)
				{
					displayLogProperties.Add(new DisplayableLogProperty(app, logProperty));
					if (!hasTimestamp)
						hasTimestamp = DisplayableLog.HasDateTimeProperty(logProperty.Name);
				}
				if (displayLogProperties.IsEmpty())
					displayLogProperties.Add(new DisplayableLogProperty(app, nameof(DisplayableLog.Message), "RawData", null));
				this.SetValue(DisplayLogPropertiesProperty, new SafeReadOnlyList<DisplayableLogProperty>(displayLogProperties));
				this.SetValue(HasTimestampDisplayableLogPropertyProperty, hasTimestamp);
				this.logFilter.FilteringLogProperties = displayLogProperties;
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
				this.SetValue(ValidLogLevelsProperty, new Logs.LogLevel[0]);
			else
			{
				var logLevels = new HashSet<Logs.LogLevel>(profile.LogLevelMapForReading.Values).Also(it => it.Add(ULogViewer.Logs.LogLevel.Undefined));
				this.SetValue(ValidLogLevelsProperty, new SafeReadOnlyList<Logs.LogLevel>(logLevels.ToList()));
			}
		}


		/// <summary>
		/// Get current URI to read logs from.
		/// </summary>
		public Uri? Uri { get => this.GetValue(UriProperty); }


		/// <summary>
		/// Get list of valid <see cref="Logs.LogLevel"/> defined by log profile including <see cref="Logs.LogLevel.Undefined"/>.
		/// </summary>
		public IList<Logs.LogLevel> ValidLogLevels { get => this.GetValue(ValidLogLevelsProperty); }


		// Wait for all necessary tasks.
		public override Task WaitForNecessaryTasksAsync()
		{
			this.saveMarkedLogsAction.ExecuteIfScheduled();
			return base.WaitForNecessaryTasksAsync();
		}


		/// <summary>
		/// Get name of current working directory.
		/// </summary>
		public string? WorkingDirectoryName { get => this.GetValue(WorkingDirectoryNameProperty); }


		/// <summary>
		/// Get path of current working directory.
		/// </summary>
		public string? WorkingDirectoryPath { get => this.GetValue(WorkingDirectoryPathProperty); }
	}
}
