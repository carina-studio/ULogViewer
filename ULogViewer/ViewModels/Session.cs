using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataOutputs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
		// Constants.
		const string MarkedFileExtension = ".ulvmark";
		const int DelaySaveMarkedLogs = 1000;


		/// <summary>
		/// Property of <see cref="AllLogCount"/>.
		/// </summary>
		public static readonly ObservableProperty<int> AllLogCountProperty = ObservableProperty.Register<Session, int>(nameof(AllLogCount));
		/// <summary>
		/// Property of <see cref="DisplayLogProperties"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<DisplayableLogProperty>> DisplayLogPropertiesProperty = ObservableProperty.Register<Session, IList<DisplayableLogProperty>>(nameof(DisplayLogProperties), new DisplayableLogProperty[0]);
		/// <summary>
		/// Property of <see cref="FilteredLogCount"/>.
		/// </summary>
		public static readonly ObservableProperty<int> FilteredLogCountProperty = ObservableProperty.Register<Session, int>(nameof(FilteredLogCount));
		/// <summary>
		/// Property of <see cref="HasAllDataSourceErrors"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasAllDataSourceErrorsProperty = ObservableProperty.Register<Session, bool>(nameof(HasAllDataSourceErrors));
		/// <summary>
		/// Property of <see cref="HasLastLogsReadingDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLastLogsReadingDurationProperty = ObservableProperty.Register<Session, bool>(nameof(HasLastLogsReadingDuration));
		/// <summary>
		/// Property of <see cref="HasLogReaders"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogReadersProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogReaders));
		/// <summary>
		/// Property of <see cref="HasLogs"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasLogsProperty = ObservableProperty.Register<Session, bool>(nameof(HasLogs));
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
		/// Property of <see cref="HasWorkingDirectory"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> HasWorkingDirectoryProperty = ObservableProperty.Register<Session, bool>(nameof(HasWorkingDirectory));
		/// <summary>
		/// Property of <see cref="Icon"/>.
		/// </summary>
		public static readonly ObservableProperty<Drawing?> IconProperty = ObservableProperty.Register<Session, Drawing?>(nameof(Icon));
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
		/// Property of <see cref="IsWaitingForDataSources"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsWaitingForDataSourcesProperty = ObservableProperty.Register<Session, bool>(nameof(IsWaitingForDataSources));
		/// <summary>
		/// Property of <see cref="IsWorkingDirectoryNeeded"/>.
		/// </summary>
		public static readonly ObservableProperty<bool> IsWorkingDirectoryNeededProperty = ObservableProperty.Register<Session, bool>(nameof(IsWorkingDirectoryNeeded));
		/// <summary>
		/// Property of <see cref="LastLogsReadingDuration"/>.
		/// </summary>
		public static readonly ObservableProperty<TimeSpan?> LastLogsReadingDurationProperty = ObservableProperty.Register<Session, TimeSpan?>(nameof(LastLogsReadingDuration));
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
		/// Property of <see cref="Logs"/>.
		/// </summary>
		public static readonly ObservableProperty<IList<DisplayableLog>> LogsProperty = ObservableProperty.Register<Session, IList<DisplayableLog>>(nameof(Logs), new DisplayableLog[0]);
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
		/// Property of <see cref="Title"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> TitleProperty = ObservableProperty.Register<Session, string?>(nameof(Title));
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
		public static readonly IValueConverter LogsFilteringProgressConverter = new RatioToPercentageConverter(1);


		// Static fields.
		static readonly TaskFactory defaultLogsReadingTaskFactory = new TaskFactory(TaskScheduler.Default);
		static readonly TaskFactory ioTaskFactory = new TaskFactory(new FixedThreadsTaskScheduler(1));


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
			public string FileName;
			public int LineNumber;
			public DateTime? Timestamp;
			public MarkedLogInfo(string fileName, int lineNumber, DateTime? timestamp)
			{
				this.FileName = fileName;
				this.LineNumber = lineNumber;
				this.Timestamp = timestamp;
			}
		}


		// Constants.
		const int DefaultFileOpeningTimeout = 10000;


		// Fields.
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
		readonly MutableObservableBoolean canSaveAllLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSaveLogs = new MutableObservableBoolean();
		readonly MutableObservableBoolean canSetLogProfile = new MutableObservableBoolean();
		readonly ScheduledAction checkDataSourceErrorsAction;
		readonly ScheduledAction checkIsWaitingForDataSourcesAction;
		Comparison<DisplayableLog?> compareDisplayableLogsDelegate;
		DisplayableLogGroup? displayableLogGroup;
		TaskFactory? fileLogsReadingTaskFactory;
		bool hasLogDataSourceCreationFailure;
		readonly DisplayableLogFilter logFilter;
		readonly Stopwatch logsReadingWatch = new Stopwatch();
		readonly HashSet<LogReader> logReaders = new HashSet<LogReader>();
		readonly SortedObservableList<DisplayableLog> markedLogs;
		readonly HashSet<string> markedLogsChangedFilePaths = new HashSet<string>(PathEqualityComparer.Default);
		readonly ObservableList<PredefinedLogTextFilter> predefinedLogTextFilters;
		readonly ScheduledAction saveMarkedLogsAction;
		readonly List<MarkedLogInfo> unmatchedMarkedLogInfos = new List<MarkedLogInfo>();
		readonly ScheduledAction updateIsReadingLogsAction;
		readonly ScheduledAction updateIsProcessingLogsAction;
		readonly ScheduledAction updateLogFilterAction;
		readonly ScheduledAction updateTitleAndIconAction;


		/// <summary>
		/// Initialize new <see cref="Session"/> instance.
		/// </summary>
		/// <param name="workspace"><see cref="Workspace"/>.</param>
		public Session(Workspace workspace) : base(workspace)
		{
			// create commands
			this.AddLogFileCommand = ReactiveCommand.Create<string?>(this.AddLogFile, this.GetValueAsObservable(IsLogFileNeededProperty));
			this.ClearLogFilesCommand = ReactiveCommand.Create(this.ClearLogFiles, this.canClearLogFiles);
			this.CopyLogsCommand = ReactiveCommand.Create<IList<DisplayableLog>>(it => this.CopyLogs(it, false), this.canCopyLogs);
			this.CopyLogsWithFileNamesCommand = ReactiveCommand.Create<IList<DisplayableLog>>(it => this.CopyLogs(it, true), this.canCopyLogsWithFileNames);
			this.MarkUnmarkLogsCommand = ReactiveCommand.Create<IEnumerable<DisplayableLog>>(this.MarkUnmarkLogs, this.canMarkUnmarkLogs);
			this.PauseResumeLogsReadingCommand = ReactiveCommand.Create(this.PauseResumeLogsReading, this.canPauseResumeLogsReading);
			this.ReloadLogsCommand = ReactiveCommand.Create(() => this.ReloadLogs(false, false), this.canReloadLogs);
			this.ResetLogProfileCommand = ReactiveCommand.Create(this.ResetLogProfile, this.canResetLogProfile);
			this.SaveAllLogsCommand = ReactiveCommand.Create<string>(this.SaveAllLogs, this.canSaveAllLogs);
			this.SaveLogsCommand = ReactiveCommand.Create<string>(this.SaveLogs, this.canSaveLogs);
			this.SetLogProfileCommand = ReactiveCommand.Create<LogProfile?>(this.SetLogProfile, this.canSetLogProfile);
			this.SetWorkingDirectoryCommand = ReactiveCommand.Create<string?>(this.SetWorkingDirectory, this.GetValueAsObservable(IsWorkingDirectoryNeededProperty));
			this.canSetLogProfile.Update(true);

			// create collections
			this.allLogs = new SortedObservableList<DisplayableLog>(this.CompareDisplayableLogs).Also(it =>
			{
				it.CollectionChanged += this.OnAllLogsChanged;
			});
			this.markedLogs = new SortedObservableList<DisplayableLog>(this.CompareDisplayableLogs).Also(it =>
			{
				it.CollectionChanged += (_, e) =>
				{
					if (!this.IsDisposed)
						this.SetValue(HasMarkedLogsProperty, it.IsNotEmpty());
				};
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
			this.logFilter = new DisplayableLogFilter((IApplication)this.Application, this.allLogs, this.CompareDisplayableLogs).Also(it =>
			{
				((INotifyCollectionChanged)it.FilteredLogs).CollectionChanged += this.OnFilteredLogsChanged;
				it.PropertyChanged += this.OnLogFilterPropertyChanged;
			});

			// setup properties
			this.SetValue(LogsProperty, this.allLogs.AsReadOnly());
			this.MarkedLogs = this.markedLogs.AsReadOnly();
			this.Workspace = workspace;

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
						if (logReader.IsWaitingForDataSource)
						{
							isWaiting = true;
							break;
						}
					}
					this.SetValue(IsWaitingForDataSourcesProperty, isWaiting);
				}
			});
			this.saveMarkedLogsAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				this.SaveMarkedLogs();
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
					this.SetValue(IsReadingLogsProperty, false);
					if(this.logsReadingWatch.IsRunning)
					{
						this.logsReadingWatch.Stop();
						this.SetValue(LastLogsReadingDurationProperty, TimeSpan.FromMilliseconds(this.logsReadingWatch.ElapsedMilliseconds));
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
						app?.Resources?.TryGetResource("Drawing.EmptySession", out res);
						return res as Drawing;
					}
					else if (app != null)
						return LogProfileIconConverter.Default.Convert(logProfile.Icon, typeof(Drawing), null, app.CultureInfo) as Drawing;
					return null;
				});

				// select title
				var title = Global.Run(() =>
				{
					if (logProfile == null)
						return app?.GetString("Session.Empty");
					if (this.addedLogFilePaths.IsEmpty())
						return logProfile.Name;
					return $"{logProfile.Name} ({this.addedLogFilePaths.Count})";
				});

				// update properties
				this.SetValue(IconProperty, icon);
				this.SetValue(TitleProperty, title);
			});
			this.updateTitleAndIconAction.Execute();
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
				// update state
				this.activationTokens.Add(it);
				if (this.activationTokens.Count > 1)
					return;
				this.Logger.LogWarning("Activate");
				this.SetValue(IsActivatedProperty, true);

				// update log updating interval
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().UpdateInterval = this.ContinuousLogReadingUpdateInterval;
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

			// update title
			this.updateTitleAndIconAction.Schedule();

			// load marked logs
			this.LoadMarkedLogs(fileName);
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


		// Clear all log files.
		void ClearLogFiles()
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canClearLogFiles.Value)
				return;
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to cler log files.");

			// save marked logs to file immediately
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// clear
			this.DisposeLogReaders(true);
			this.addedLogFilePaths.Clear();

			// clear data source error
			this.hasLogDataSourceCreationFailure = false;
			this.checkDataSourceErrorsAction.Execute();

			// update title
			this.updateTitleAndIconAction.Schedule();
		}


		/// <summary>
		/// Command to clear all added log files.
		/// </summary>
		public ICommand ClearLogFilesCommand { get; }


		// Compare displayable logs.
		int CompareDisplayableLogs(DisplayableLog? x, DisplayableLog? y) => this.compareDisplayableLogsDelegate(x, y);
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
					return Math.Max(Math.Min(this.Settings.GetValueOrDefault(ULogViewer.Settings.ContinuousLogReadingUpdateInterval), ULogViewer.Settings.MaxContinuousLogReadingUpdateInterval), ULogViewer.Settings.MinContinuousLogReadingUpdateInterval);
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

			// prepare log writer
			using var dataOutput = new StringLogDataOutput(app);
			using var logWriter = this.CreateLogWriter(dataOutput);
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
				if (e.PropertyName == nameof(LogWriter.State))
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
				await app.Clipboard.SetTextAsync(dataOutput.String ?? "");
				this.Logger.LogDebug("Logs copying completed");
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
			// select logs reading task factory
			var readingTaskFactory = dataSource.CreationOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName))
				? (fileLogsReadingTaskFactory ?? new TaskFactory(new FixedThreadsTaskScheduler(1)).Also(it => this.fileLogsReadingTaskFactory = it))
				: defaultLogsReadingTaskFactory;

			// create log reader
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile to create log reader.");
			var logReader = new LogReader(dataSource, readingTaskFactory).Also(it =>
			{
				if (profile.IsContinuousReading)
					it.UpdateInterval = this.ContinuousLogReadingUpdateInterval;
				it.IsContinuousReading = profile.IsContinuousReading;
				it.LogLevelMap = profile.LogLevelMapForReading;
				if (profile.LogPatterns.IsNotEmpty())
					it.LogPatterns = profile.LogPatterns;
				else
					it.LogPatterns = new LogPattern[] { new LogPattern("^(?<Message>.*)", false, false) };
				it.LogStringEncoding = profile.LogStringEncodingForReading;
				if (profile.IsContinuousReading)
					it.MaxLogCount = this.Settings.GetValueOrDefault(ULogViewer.Settings.MaxContinuousLogCount);
				it.TimestampCultureInfo = profile.TimestampCultureInfoForReading;
				it.TimestampEncoding = profile.TimestampEncodingForReading;
				it.TimestampFormat = profile.TimestampFormatForReading;
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

			// update state
			if (this.addedLogFilePaths.IsNotEmpty())
				this.canClearLogFiles.Update(true);
			this.canMarkUnmarkLogs.Update(true);
			this.canPauseResumeLogsReading.Update(profile.IsContinuousReading);
			this.canReloadLogs.Update(true);
			this.SetValue(HasLogReadersProperty, true);
		}


		// Create log writer for current log profile.
		LogWriter CreateLogWriter(ILogDataOutput dataOutput)
		{
			// check state
			var profile = this.LogProfile ?? throw new InternalStateCorruptedException("No log profile.");
			var app = this.Application as App ?? throw new InternalStateCorruptedException("No application.");
			var writingFormat = profile.LogWritingFormat ?? "";

			// prepare log writer
			var logWriter = new LogWriter(dataOutput);
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
			logWriter.TimestampCultureInfo = profile.TimestampCultureInfoForWriting;
			logWriter.TimestampFormat = string.IsNullOrEmpty(profile.TimestampFormatForWriting) ? profile.TimestampFormatForReading : profile.TimestampFormatForWriting;
			return logWriter;
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
			this.DisposeLogReaders(false);

			// clear logs
			foreach (var displayableLog in this.allLogs)
				displayableLog.Dispose();
			this.allLogs.Clear();
			this.allLogsByLogFilePath.Clear();
			this.displayableLogGroup = this.displayableLogGroup.DisposeAndReturnNull();

			// detach from log profile
			this.LogProfile?.Let(it => it.PropertyChanged -= this.OnLogProfilePropertyChanged);

			// stop watch
			this.logsReadingWatch.Stop();

			// dispose task factories
			(this.fileLogsReadingTaskFactory?.Scheduler as IDisposable)?.Dispose();

			// call base
			base.Dispose(disposing);
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
					this.canClearLogFiles.Update(false);
					this.canMarkUnmarkLogs.Update(false);
					this.canPauseResumeLogsReading.Update(false);
					this.canReloadLogs.Update(false);
					this.SetValue(HasLogReadersProperty, false);
					this.SetValue(IsLogsReadingPausedProperty, false);
				}
			}
		}


		// Dispose all log readers.
		void DisposeLogReaders(bool removeLogs)
		{
			foreach (var logReader in this.logReaders.ToArray())
				this.DisposeLogReader(logReader, removeLogs);
		}


		/// <summary>
		/// Get number of filtered logs.
		/// </summary>
		public int FilteredLogCount { get => this.GetValue(FilteredLogCountProperty); }


		/// <summary>
		/// Check whether errors are found in all data sources or not.
		/// </summary>
		public bool HasAllDataSourceErrors { get => this.GetValue(HasAllDataSourceErrorsProperty); }


		/// <summary>
		/// Check whether <see cref="LastLogsReadingDuration"/> is valid or not.
		/// </summary>
		public bool HasLastLogsReadingDuration { get => this.GetValue(HasLastLogsReadingDurationProperty); }


		/// <summary>
		/// Check whether at least one log reader created or not.
		/// </summary>
		public bool HasLogReaders { get => this.GetValue(HasLogReadersProperty); }


		/// <summary>
		/// Check whether at least one log is read or not.
		/// </summary>
		public bool HasLogs { get => this.GetValue(HasLogsProperty); }


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
		/// Check whether working directory has been set or not.
		/// </summary>
		public bool HasWorkingDirectory { get => this.GetValue(HasWorkingDirectoryProperty); }


		/// <summary>
		/// Get icon of session.
		/// </summary>
		public Drawing? Icon { get => this.GetValue(IconProperty); }


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
		/// Check data sources are not ready for reading logs.
		/// </summary>
		public bool IsWaitingForDataSources { get => this.GetValue(IsWaitingForDataSourcesProperty); }


		/// <summary>
		/// Check whether working directory is needed or not.
		/// </summary>
		public bool IsWorkingDirectoryNeeded { get => this.GetValue(IsWorkingDirectoryNeededProperty); }


		/// <summary>
		/// Get the duration of last logs reading.
		/// </summary>
		public TimeSpan? LastLogsReadingDuration { get => this.GetValue(LastLogsReadingDurationProperty); }


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
												var lineNumber = jsonObject.GetProperty("MarkedLineNumber").GetInt32();
												var timestamp = (DateTime?)null;
												if (jsonObject.TryGetProperty("MarkedTimestamp", out var timestampElement))
													timestamp = DateTime.Parse(timestampElement.GetString().AsNonNull());
												markedLogInfos.Add(new MarkedLogInfo(fileName, lineNumber, timestamp));
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
		/// Get progress of logs filtering.
		/// </summary>
		public double LogsFilteringProgress { get => this.GetValue(LogsFilteringProgressProperty); }


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


		// Mark or unmark logs.
		void MarkUnmarkLogs(IEnumerable<DisplayableLog> logs)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canMarkUnmarkLogs.Value)
				return;
			var allLogsAreMarked = true;
			foreach (var log in logs)
			{
				if (!log.IsMarked)
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
					if (log.IsMarked)
					{
						log.IsMarked = false;
						this.markedLogs.Remove(log);
						log.FileName?.Let(it => this.markedLogsChangedFilePaths.Add(it));
					}
				}
			}
			else
			{
				this.Logger.LogTrace("Mark log(s)");
				foreach (var log in logs)
				{
					if(!log.IsMarked)
					{
						log.IsMarked = true;
						this.markedLogs.Add(log);
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
					if (!log.IsMarked)
					{
						log.IsMarked = true;
						this.markedLogs.Add(log);
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
					foreach (DisplayableLog displayableLog in e.OldItems.AsNonNull())
						displayableLog.Dispose();
					break;
				case NotifyCollectionChangedAction.Reset:
					this.markedLogs.Clear();
					this.markedLogs.AddAll(this.allLogs.TakeWhile(it => it.IsMarked));
					break;
			}
			if (!this.IsDisposed)
			{
				if (!this.logFilter.IsFilteringNeeded)
					this.SetValue(HasLogsProperty, this.allLogs.IsNotEmpty());
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
				if (this.logFilter.IsFilteringNeeded)
					this.SetValue(HasLogsProperty, this.logFilter.FilteredLogs.IsNotEmpty());
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
				case nameof(DisplayableLogFilter.FilteringProgress):
					this.SetValue(LogsFilteringProgressProperty, this.logFilter.FilteringProgress);
					break;
				case nameof(DisplayableLogFilter.IsFiltering):
					this.SetValue(IsFilteringLogsProperty, this.logFilter.IsFiltering);
					break;
				case nameof(DisplayableLogFilter.IsFilteringNeeded):
					if (this.logFilter.IsFilteringNeeded)
					{
						this.SetValue(LogsProperty, logFilter.FilteredLogs);
						this.SetValue(HasLogsProperty, logFilter.FilteredLogs.IsNotEmpty());
					}
					else
					{
						this.SetValue(LogsProperty, this.allLogs.AsReadOnly());
						this.SetValue(HasLogsProperty, this.allLogs.IsNotEmpty());
					}
					this.SetValue(IsFilteringLogsNeededProperty, this.logFilter.IsFilteringNeeded);
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
				case nameof(LogProfile.ColorIndicator):
					this.SynchronizationContext.Post(() => this.ReloadLogs(false, true));
					break;
				case nameof(LogProfile.DataSourceProvider):
					this.SynchronizationContext.Post(() => this.ReloadLogs(true, true));
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
				case nameof(LogProfile.TimestampFormatForReading):
					this.SynchronizationContext.Post(() => this.ReloadLogs(true, false));
					break;
				case nameof(LogProfile.Name):
					this.updateTitleAndIconAction.Schedule();
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
							var removedLogs = new HashSet<Log>(oldItems.Cast<Log>());
							this.allLogs.RemoveAll(it => removedLogs.Contains(it.Log));
						}
						logReader.DataSource.CreationOptions.FileName?.Let(fileName =>
						{
							if (this.allLogsByLogFilePath.TryGetValue(fileName, out var localLogs))
								localLogs.RemoveRange(e.OldStartingIndex, oldItems.Count);
						});
					});
					break;
				case NotifyCollectionChangedAction.Reset:
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
					this.updateIsReadingLogsAction.Schedule();
					break;
			}
		}


		// Called when property changed.
		protected override void OnPropertyChanged(ObservableProperty property, object? oldValue, object? newValue)
		{
			base.OnPropertyChanged(property, oldValue, newValue);
			if (property == AllLogCountProperty)
				this.UpdateIsLogsWritingAvailable(this.LogProfile);
			else if (property == HasLogsProperty
				|| property == IsCopyingLogsProperty)
			{
				this.UpdateIsLogsWritingAvailable(this.LogProfile);
			}
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
			else if (property == LastLogsReadingDurationProperty)
				this.SetValue(HasLastLogsReadingDurationProperty, this.LastLogsReadingDuration != null);
			else if (property == LogFiltersCombinationModeProperty
				|| property == LogLevelFilterProperty
				|| property == LogProcessIdFilterProperty
				|| property == LogTextFilterProperty
				|| property == LogThreadIdFilterProperty)
			{
				this.updateLogFilterAction.Schedule();
			}
		}


		// Called when setting changed.
		protected override void OnSettingChanged(SettingChangedEventArgs e)
		{
			base.OnSettingChanged(e);
			if (e.Key == ULogViewer.Settings.ContinuousLogReadingUpdateInterval)
			{
				if (this.LogProfile?.IsContinuousReading == true && this.logReaders.IsNotEmpty())
					this.logReaders.First().UpdateInterval = this.ContinuousLogReadingUpdateInterval;
			}
			else if (e.Key == ULogViewer.Settings.MaxContinuousLogCount)
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
			if (!this.canReloadLogs.Value || this.logReaders.IsEmpty())
				return;
			var profile = this.LogProfile;
			if (profile == null)
				throw new InternalStateCorruptedException("No log profile to reload logs.");

			// save marked logs
			this.saveMarkedLogsAction.ExecuteIfScheduled();

			// clear logs
			var isContinuousReading = profile.IsContinuousReading;
			var dataSourceOptions = new List<LogDataSourceOptions>();
			if (isContinuousReading && !recreateLogReaders)
			{
				foreach (var logReader in this.logReaders)
					logReader.ClearLogs();
				this.Logger.LogWarning($"Clear logs in {this.logReaders.Count} log reader(s)");
			}
			else
			{
				// collect data source options
				foreach (var logReader in this.logReaders)
					dataSourceOptions.Add(logReader.DataSource.CreationOptions);
				this.Logger.LogWarning($"Reload logs with {dataSourceOptions.Count} log reader(s)");

				// dispose log readers
				this.DisposeLogReaders(true);

				// clear data source error
				this.hasLogDataSourceCreationFailure = false;
				this.checkDataSourceErrorsAction.Execute();
			}

			// setup log comparer
			this.UpdateDisplayableLogComparison();

			// update display log properties
			if (updateDisplayLogProperties)
				this.UpdateDisplayLogProperties();

			// recreate log readers
			if (dataSourceOptions.IsNotEmpty())
			{
				var dataSourceProvider = profile.DataSourceProvider;
				foreach (var dataSourceOption in dataSourceOptions)
				{
					// create data source and reader
					var dataSource = this.CreateLogDataSourceOrNull(dataSourceProvider, dataSourceOption);
					if (dataSource != null)
						this.CreateLogReader(dataSource);
					else
					{
						this.hasLogDataSourceCreationFailure = true;
						this.checkDataSourceErrorsAction.Schedule();
					}

					// load marked logs
					if (dataSourceOption.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
						this.LoadMarkedLogs(dataSourceOption.FileName.AsNonNull());
				}
			}
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
			this.canResetLogProfile.Update(false);
			this.SetValue(HasWorkingDirectoryProperty, false);
			this.SetValue(IsLogFileNeededProperty, false);
			this.SetValue(IsReadingLogsContinuouslyProperty, false);
			this.SetValue(IsWorkingDirectoryNeededProperty, false);
			this.UpdateIsLogsWritingAvailable(null);
			this.UpdateValidLogLevels();
			this.SetValue(WorkingDirectoryNameProperty, null);
			this.SetValue(WorkingDirectoryPathProperty, null);

			// clear profile
			this.Logger.LogWarning($"Reset log profile '{profile.Name}'");
			this.SetValue(LogProfileProperty, null);

			// cancel filtering
			this.logFilter.FilteringLogProperties = DisplayLogPropertiesProperty.DefaultValue;
			this.updateLogFilterAction.Cancel();

			// dispose log readers
			this.DisposeLogReaders(false);

			// clear data source error
			this.hasLogDataSourceCreationFailure = false;
			this.checkDataSourceErrorsAction.Execute();

			// clear logs
			foreach (var displayableLog in this.allLogs)
				displayableLog.Dispose();
			this.allLogs.Clear();
			this.allLogsByLogFilePath.Clear();
			this.displayableLogGroup = this.displayableLogGroup.DisposeAndReturnNull();

			// clear file name table
			this.addedLogFilePaths.Clear();

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


		// Save all logs to file.
		void SaveAllLogs(string? fileName)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canSaveAllLogs.Value)
				return;
			this.SaveLogs(fileName, this.allLogs);
		}


		/// <summary>
		/// Command to save all <see cref="DisplayableLog"/> to file.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="string"/>.</remarks>
		public ICommand SaveAllLogsCommand { get; }


		// Save logs to file.
		void SaveLogs(string? fileName)
		{
			this.VerifyAccess();
			this.VerifyDisposed();
			if (!this.canSaveLogs.Value)
				return;
			this.SaveLogs(fileName, this.Logs);
		}
		async void SaveLogs(string? fileName, IList<DisplayableLog> logs)
		{
			// check state
			if (logs.IsEmpty())
				return;
			if (fileName == null)
				throw new ArgumentNullException(nameof(fileName));
			var app = this.Application as App ?? throw new InternalStateCorruptedException("No application.");

			// prepare log writer
			using var dataOutput = new FileLogDataOutput(app, fileName);
			using var logWriter = this.CreateLogWriter(dataOutput);
			var markedLogs = this.markedLogs;
			var syncLock = new object();
			var isCompleted = false;
			logWriter.Logs = new Log[logs.Count].Also(it =>
			{
				for (var i = it.Length - 1; i >= 0; --i)
					it[i] = logs[i].Log;
			});
			logWriter.LogsToGetLineNumber = new HashSet<Log>().Also(it =>
			{
				for (var i = markedLogs.Count - 1; i >= 0; --i)
					it.Add(markedLogs[i].Log);
			});
			logWriter.WriteFileNames = false;
			logWriter.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(LogWriter.State))
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
			this.Logger.LogDebug($"Start saving logs to '{fileName}'");
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

			// complete and save marked logs
			if (logWriter.State == LogWriterState.Stopped)
			{
				this.Logger.LogDebug("Logs saving completed, save marked logs");
				var markedLogInfos = new List<MarkedLogInfo>().Also(markedLogInfos =>
				{
					foreach (var pair in logWriter.LineNumbers)
						markedLogInfos.Add(new MarkedLogInfo(fileName, pair.Value, pair.Key.Timestamp));
				});
				this.SaveMarkedLogs(fileName, markedLogInfos);
			}
			else
				this.Logger.LogError("Logs saving failed");
			if (!this.IsDisposed)
				this.SetValue(IsSavingLogsProperty, false);
		}


		/// <summary>
		/// Command to save <see cref="Logs"/> to file.
		/// </summary>
		/// <remarks>Type of parameter is <see cref="string"/>.</remarks>
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
					markedLog.LineNumber?.Let(lineNumber => markedLogInfos.Add(new MarkedLogInfo(logFileName, lineNumber, markedLog.Log.Timestamp)));
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


		// Set log profile.
		void SetLogProfile(LogProfile? profile)
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

			// prepare displayable log group
			this.displayableLogGroup = new DisplayableLogGroup(profile);

			// setup log comparer
			this.UpdateDisplayableLogComparison();

			// update valid log levels
			this.UpdateValidLogLevels();

			// read logs or wait for more actions
			var dataSourceOptions = profile.DataSourceOptions;
			var dataSourceProvider = profile.DataSourceProvider;
			var startReadingLogs = true;
			if (dataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)))
			{
				if (!dataSourceOptions.IsOptionSet(nameof(LogDataSourceOptions.FileName)))
				{
					this.Logger.LogDebug("No file name specified, waiting for adding file");
					this.SetValue(IsLogFileNeededProperty, true);
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
				else
				{
					this.SetValue(HasWorkingDirectoryProperty, true);
					this.SetValue(WorkingDirectoryNameProperty, Path.GetFileName(dataSourceOptions.WorkingDirectory));
					this.SetValue(WorkingDirectoryPathProperty, dataSourceOptions.WorkingDirectory);
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
		}


		/// <summary>
		/// Command to set specific log profile.
		/// </summary>
		/// <remarks>Type of command parameter is <see cref="LogProfile"/>.</remarks>
		public ICommand SetLogProfileCommand { get; }


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
			this.DisposeLogReaders(true);

			this.Logger.LogDebug($"Set working directory to '{directory}'");

			// update state
			this.SetValue(WorkingDirectoryNameProperty, Path.GetFileName(directory));
			this.SetValue(WorkingDirectoryPathProperty, directory);
			this.SetValue(HasWorkingDirectoryProperty, true);

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
		/// Get title of session.
		/// </summary>
		public string? Title { get => this.GetValue(TitleProperty); }


		// Update comparison for displayable logs.
		void UpdateDisplayableLogComparison()
		{
			var profile = this.LogProfile.AsNonNull();
			this.compareDisplayableLogsDelegate = profile.SortKey switch
			{
				LogSortKey.BeginningTimestamp => CompareDisplayableLogsByBeginningTimestamp,
				LogSortKey.EndingTimestamp => CompareDisplayableLogsByEndingTimestamp,
				LogSortKey.Timestamp => CompareDisplayableLogsByTimestamp,
				_ => CompareDisplayableLogsById,
			};
			if (profile.SortDirection == SortDirection.Descending)
				this.compareDisplayableLogsDelegate = this.compareDisplayableLogsDelegate.Invert();
		}


		// Update list of display log properties according to profile.
		void UpdateDisplayLogProperties()
		{
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.SetValue(DisplayLogPropertiesProperty, DisplayLogPropertiesProperty.DefaultValue);
				this.logFilter.FilteringLogProperties = DisplayLogPropertiesProperty.DefaultValue;
			}
			else
			{
				var app = (IApplication)this.Application;
				var visibleLogProperties = profile.VisibleLogProperties;
				var displayLogProperties = new List<DisplayableLogProperty>();
				foreach (var logProperty in visibleLogProperties)
					displayLogProperties.Add(new DisplayableLogProperty(app, logProperty));
				if (displayLogProperties.IsEmpty())
					displayLogProperties.Add(new DisplayableLogProperty(app, nameof(DisplayableLog.Message), "RawData", null));
				this.SetValue(DisplayLogPropertiesProperty, displayLogProperties.AsReadOnly());
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
				this.canSaveAllLogs.Update(false);
				this.canSaveLogs.Update(false);
			}
			else
			{
				this.canCopyLogs.Update(this.Application is App && !this.IsCopyingLogs);
				this.canCopyLogsWithFileNames.Update(this.canCopyLogs.Value && profile.DataSourceProvider.IsSourceOptionRequired(nameof(LogDataSourceOptions.FileName)));
				this.canSaveAllLogs.Update(this.Application is App && !this.IsSavingLogs && this.AllLogCount > 0);
				this.canSaveLogs.Update(this.canSaveAllLogs.Value && this.HasLogs);
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
				this.SetValue(ValidLogLevelsProperty, logLevels.ToList().AsReadOnly());
			}
		}


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


		/// <summary>
		/// Get <see cref="Workspace"/> which session belongs to.
		/// </summary>
		public Workspace Workspace { get; }
	}
}
