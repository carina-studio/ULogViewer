using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Filter of <see cref="DisplayableLog"/>.
	/// </summary>
	class DisplayableLogFilter : BaseDisposable, IApplicationObject, INotifyPropertyChanged
	{
		// Filtering parameters.
		class FilteringParams
		{
			// Fields.
			public FilterCombinationMode CombinationMode;
			public volatile int CompletedChunkId;
			public int ConcurrencyLevel;
			public readonly object FilteringChunkLock = new object();
			public bool HasLogProcessId;
			public bool HasLogThreadId;
			public bool IncludeMarkedLogs;
			public Logs.LogLevel Level;
			public IList<Func<DisplayableLog, string?>> LogTextPropertyGetters = new Func<DisplayableLog, string?>[0];
			public int NextChunkId = 1;
			public int? ProcessId;
			public int? ThreadId;
			public IList<Regex> TextRegexList = new Regex[0];
		}


		// Constants.
		const int ChunkSize = 40960;


		// Static fields.
		static readonly Regex allMatchingPatternRegex = new Regex(@"^\.[\*\+]{0,1}$");


		// Fields.
		FilterCombinationMode combinationMode = FilterCombinationMode.Intersection;
		volatile FilteringParams? currentFilterParams;
		readonly SortedObservableList<DisplayableLog> filteredLogs;
		IList<DisplayableLogProperty> filteringLogProperties = new DisplayableLogProperty[0];
		readonly TaskFactory filteringTaskFactory;
		readonly FixedThreadsTaskScheduler filteringTaskScheduler;
		bool includeMarkedLogs = true;
		Logs.LogLevel level = Logs.LogLevel.Undefined;
		readonly int maxFilteringConcurrencyLevel = Environment.ProcessorCount;
		int? processId;
		readonly ScheduledAction startFilteringLogsAction;
		IList<Regex> textRegexList = new Regex[0];
		int? threadId;
		readonly SortedObservableList<DisplayableLog> unfilteredLogs;


		/// <summary>
		/// Initialize new <see cref="DisplayableLogFilter"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="sourceLogs">Source list of logs.</param>
		/// <param name="comparer"><see cref="IComparer{T}"/> which used on <paramref name="sourceLogs"/>.</param>
		public DisplayableLogFilter(IApplication app, IList<DisplayableLog> sourceLogs, IComparer<DisplayableLog> comparer) : this(app, sourceLogs, comparer.Compare)
		{ }


		/// <summary>
		/// Initialize new <see cref="DisplayableLogFilter"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		/// <param name="sourceLogs">Source list of logs.</param>
		/// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
		public DisplayableLogFilter(IApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison)
		{
			// create lists
			this.filteredLogs = new SortedObservableList<DisplayableLog>(comparison);
			this.unfilteredLogs = new SortedObservableList<DisplayableLog>(comparison);

			// setup properties
			this.Application = app;
			this.FilteredLogs = this.filteredLogs.AsReadOnly();
			this.SourceLogs = sourceLogs;

			// create schedule actions
			this.startFilteringLogsAction = new ScheduledAction(this.StartFilteringLogs);

			// setup task scheduler
			this.filteringTaskScheduler = new FixedThreadsTaskScheduler(this.maxFilteringConcurrencyLevel);
			this.filteringTaskFactory = new TaskFactory(this.filteringTaskScheduler);
			
			// attach to source logs
			(sourceLogs as INotifyCollectionChanged)?.Let(it => it.CollectionChanged += this.OnSourceLogsChanged);
		}


		/// <summary>
		/// Get application.
		/// </summary>
		public IApplication Application { get; }


		// Cancel current filtering.
		void CancelFiltering()
		{
			// check state
			var filteringParams = this.currentFilterParams;
			if (filteringParams == null)
				return;

			// cancel all chunk filtering
			lock (filteringParams.FilteringChunkLock)
			{
				this.currentFilterParams = null;
				Monitor.PulseAll(filteringParams.FilteringChunkLock);
			}

			// clear logs
			this.unfilteredLogs.Clear();
			this.filteredLogs.Clear();

			// update state
			if (this.IsFiltering)
			{
				this.IsFiltering = false;
				this.FilteringProgress = 0;
				this.OnPropertyChanged(nameof(FilteringProgress));
				this.OnPropertyChanged(nameof(IsFiltering));
			}
		}


		/// <summary>
		/// Get or set mode to combine condition of <see cref="TextRegexList"/> and other conditions excluding <see cref="IncludeMarkedLogs"/>.
		/// </summary>
		public FilterCombinationMode CombinationMode
		{
			get => this.combinationMode;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.combinationMode == value)
					return;
				this.combinationMode = value;
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(CombinationMode));
			}
		}


		// Dispose
		protected override void Dispose(bool disposing)
		{
			// ignore managed resources
			if (!disposing)
			{
				this.filteringTaskScheduler.Dispose();
				return;
			}

			// check thread
			this.VerifyAccess();

			// detach from source logs
			(this.SourceLogs as INotifyCollectionChanged)?.Let(it => it.CollectionChanged -= this.OnSourceLogsChanged);

			// cancecl filtering
			this.CancelFiltering();

			// dispose task scheduler
			this.filteringTaskScheduler.Dispose();
		}


		/// <summary>
		/// Get list of filtered logs.
		/// </summary>
		/// <remarks>The list implements <see cref="INotifyCollectionChanged"/> interface.</remarks>
		public IList<DisplayableLog> FilteredLogs { get; }


		/// <summary>
		/// Get or set list of <see cref="DisplayableLogProperty"/> to be considered into filtering.
		/// </summary>
		public IList<DisplayableLogProperty> FilteringLogProperties
		{
			get => this.filteringLogProperties;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.filteringLogProperties.SequenceEqual(value))
					return;
				this.filteringLogProperties = new List<DisplayableLogProperty>(value).AsReadOnly();
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(FilteringLogProperties));
			}
		}


		/// <summary>
		/// Get current progress of filtering. Range of progress is [0.0, 1.0].
		/// </summary>
		public double FilteringProgress { get; private set; }


		// Filter chunk of logs.
		void FilterChunk(FilteringParams filteringParams, int chunkId, IList<DisplayableLog> logs)
		{
			// delay to prevent busy on main thread
			Thread.Sleep(30);

			// check state
			if (this.currentFilterParams != filteringParams)
				return;

			// filter logs
			var filteredLogs = new List<DisplayableLog>();
			var combinationMode = filteringParams.CombinationMode;
			var includeMarkLogs = filteringParams.IncludeMarkedLogs;
			var level = filteringParams.Level;
			var pid = filteringParams.ProcessId;
			var tid = filteringParams.ThreadId;
			var textRegexList = filteringParams.TextRegexList;
			var textRegexCount = textRegexList.Count;
			var textPropertyGetters = filteringParams.LogTextPropertyGetters;
			var textPropertyCount = textPropertyGetters.Count;
			var textToMatchBuilder = new StringBuilder();
			for (int i = 0, count = logs.Count; i < count; ++i)
			{
				// check marking state
				var log = logs[i];
				if (includeMarkLogs && log.IsMarked)
				{
					filteredLogs.Add(log);
					continue;
				}

				// check text regex
				var isTextFilteringNeeded = (textRegexCount > 0 && textPropertyCount > 0);
				var isTextRegexMatched = false;
				if (isTextFilteringNeeded)
				{
					for (var j = 0; j < textPropertyCount; ++j)
					{
						if (j > 0)
							textToMatchBuilder.Append('█'); // special separator between text properties
						textToMatchBuilder.Append(textPropertyGetters[j](log));
					}
					for (var j = textRegexCount - 1; j >= 0; --j)
					{
						if (textRegexList[j].IsMatch(textToMatchBuilder.ToString()))
						{
							isTextRegexMatched = true;
							break;
						}
					}
					textToMatchBuilder.Remove(0, textToMatchBuilder.Length);
				}
				if (isTextRegexMatched && isTextFilteringNeeded && combinationMode == FilterCombinationMode.Union)
				{
					filteredLogs.Add(log);
					continue;
				}

				// check level
				var areOtherConditionsMatched = true;
				if (level != Logs.LogLevel.Undefined && log.Level != level)
					areOtherConditionsMatched = false;
				if (areOtherConditionsMatched && pid != null && filteringParams.HasLogProcessId)
					areOtherConditionsMatched = (pid == log.ProcessId);
				if (areOtherConditionsMatched && tid != null && filteringParams.HasLogThreadId)
					areOtherConditionsMatched = (tid == log.ThreadId);

				// filter
				if (areOtherConditionsMatched)
				{
					if (!isTextFilteringNeeded || isTextRegexMatched || combinationMode == FilterCombinationMode.Union)
						filteredLogs.Add(log);
				}
			}

			// wait for previous chunks
			while (true)
			{
				lock (filteringParams.FilteringChunkLock)
				{
					if (this.currentFilterParams != filteringParams)
						return;
					if (filteringParams.CompletedChunkId == chunkId - 1)
					{
						this.SynchronizationContext.Post(() => this.OnChunkFiltered(filteringParams, chunkId, filteredLogs));
						return;
					}
					Monitor.Wait(filteringParams.FilteringChunkLock);
				}
			}
		}


		// Start filtering next chunk.
		void FilterNextChunk(FilteringParams filteringParams)
		{
			// check state
			if (this.currentFilterParams != filteringParams)
				return;
			if (this.unfilteredLogs.IsEmpty())
			{
				if (this.IsFiltering)
				{
					this.IsFiltering = false;
					this.FilteringProgress = 1;
					this.OnPropertyChanged(nameof(FilteringProgress));
					this.OnPropertyChanged(nameof(IsFiltering));
				}
				return;
			}
			if (filteringParams.ConcurrencyLevel >= this.maxFilteringConcurrencyLevel)
				return;

			// update state
			if (!this.IsFiltering)
			{
				this.IsFiltering = true;
				this.OnPropertyChanged(nameof(IsFiltering));
			}
			this.FilteringProgress = 1.0 - ((double)this.unfilteredLogs.Count / this.SourceLogs.Count);
			this.OnPropertyChanged(nameof(FilteringProgress));

			// start filtering
			var chunkId = filteringParams.NextChunkId++;
			var logs = this.unfilteredLogs.Let(it =>
			{
				if (it.Count > ChunkSize)
				{
					var array = it.ToArray(0, ChunkSize);
					it.RemoveRange(0, ChunkSize);
					return array;
				}
				else
				{
					var array = it.ToArray();
					it.Clear();
					return array;
				}
			});
			++filteringParams.ConcurrencyLevel;
			this.filteringTaskFactory.StartNew(() => this.FilterChunk(filteringParams, chunkId, logs));
		}


		/// <summary>
		/// Get or set whether marked logs should be filtered out or not.
		/// </summary>
		public bool IncludeMarkedLogs
		{
			get => this.includeMarkedLogs;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.includeMarkedLogs == value)
					return;
				this.includeMarkedLogs = value;
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(IncludeMarkedLogs));
			}
		}


		// Check whether given regex can match all strings or not.
		bool IsAllMatchingRegex(Regex regex)
		{
			var patterns = regex.ToString().Split('|');
			for (var i = patterns.Length - 1; i >= 0; --i)
			{
				var pattern = patterns[i];
				if (pattern.Length == 0)
					return true;
				if (allMatchingPatternRegex.IsMatch(pattern))
					return true;
			}
			return false;
		}


		/// <summary>
		/// Check whether logs are being filtered or not.
		/// </summary>
		public bool IsFiltering { get; private set; }


		/// <summary>
		/// Check whether logs filtering is needed or not according to current filtering parameters.
		/// </summary>
		public bool IsFilteringNeeded { get; private set; }


		/// <summary>
		/// Get or set level of log to be filtered.
		/// </summary>
		public Logs.LogLevel Level
		{
			get => this.level;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.level == value)
					return;
				this.level = value;
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(Level));
			}
		}


		// Called when chunk of logs filtered.
		void OnChunkFiltered(FilteringParams filteringParams, int chunkId, IList<DisplayableLog> filteredLogs)
		{
			// unlock next chunk
			lock (filteringParams.FilteringChunkLock)
			{
				if (filteringParams.CompletedChunkId != chunkId - 1)
					throw new InternalStateCorruptedException("Incorrect order of completing chunk filtering.");
				filteringParams.CompletedChunkId = chunkId;
				Monitor.PulseAll(filteringParams.FilteringChunkLock);
			}

			// update state
			--filteringParams.ConcurrencyLevel;

			// check state
			if (this.currentFilterParams != filteringParams)
				return;

			// add filtered logs
			if (filteredLogs.IsNotEmpty())
			{
				// remove logs which are not contained in source log list
				var sourceIndex = -1;
				var sourceLogs = this.SourceLogs;
				var filteredIndex = filteredLogs.Count - 1;
				var comparer = this.filteredLogs.Comparer;
				while (filteredIndex >= 0)
				{
					sourceIndex = sourceLogs.IndexOf(filteredLogs[filteredIndex]);
					if (sourceIndex >= 0)
					{
						--sourceIndex;
						--filteredIndex;
						break;
					}
					filteredLogs.RemoveAt(filteredIndex--);
				}
				while (filteredIndex >= 0 && sourceIndex >= 0)
				{
					var result = comparer.Compare(sourceLogs[sourceIndex], filteredLogs[filteredIndex]);
					if (result == 0)
					{
						--sourceIndex;
						--filteredIndex;
					}
					else if (result > 0)
						--sourceIndex;
					else
						filteredLogs.RemoveAt(filteredIndex--);
				}
				while (filteredIndex >= 0)
					filteredLogs.RemoveAt(filteredIndex--);

				// add filtered list
				this.filteredLogs.AddAll(filteredLogs);
			}

			// start filtering next chunk
			this.FilterNextChunk(filteringParams);
		}


		// Raise PropertyChanged event.
		void OnPropertyChanged(string propertyName) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));


		// Called when source logs changed.
		void OnSourceLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch(e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (this.currentFilterParams != null)
					{
						this.unfilteredLogs.AddAll(e.NewItems.AsNonNull().Cast<DisplayableLog>(), true);
						for (var i = this.maxFilteringConcurrencyLevel; i > 0; --i)
							this.FilterNextChunk(this.currentFilterParams);
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					{
						var removedLogs = e.OldItems.AsNonNull().Cast<DisplayableLog>();
						this.unfilteredLogs.RemoveAll(removedLogs, true);
						this.filteredLogs.RemoveAll(removedLogs, true);
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					this.startFilteringLogsAction.Schedule();
					break;
				default:
					throw new InvalidOperationException($"Unsupported change of source log list: {e.Action}.");
			}
		}


		/// <summary>
		/// Get or set process ID of log to filter.
		/// </summary>
		public int? ProcessId
		{
			get => this.processId;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.processId == value)
					return;
				this.processId = value;
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(ProcessId));
			}
		}


		/// <summary>
		/// Get source list of <see cref="DisplayableLog"/> to be filtered.
		/// </summary>
		public IList<DisplayableLog> SourceLogs { get; }


		// Start filtering logs.
		void StartFilteringLogs()
		{
			// check state
			if (this.IsDisposed)
				return;

			// cancel current filtering
			this.CancelFiltering();

			// check log properties
			var isFilteringNeeded = false;
			var filteringParams = new FilteringParams();
			var textPropertyGetters = new List<Func<DisplayableLog, string?>>();
			foreach (var logProperty in this.filteringLogProperties)
			{
				if (DisplayableLog.HasStringLogProperty(logProperty.Name))
				{
					textPropertyGetters.Add(DisplayableLog.CreateLogPropertyGetter<string?>(logProperty.Name));
					isFilteringNeeded = true;
				}
				else if (logProperty.Name == nameof(DisplayableLog.ProcessId))
					filteringParams.HasLogProcessId = true;
				else if (logProperty.Name == nameof(DisplayableLog.ThreadId))
					filteringParams.HasLogThreadId = true;
			}
			if (isFilteringNeeded)
			{
				if (this.textRegexList.IsNotEmpty())
				{
					foreach (var regex in this.textRegexList)
					{
						if (this.IsAllMatchingRegex(regex))
						{
							isFilteringNeeded = false;
							break;
						}
					}
				}
				else
					isFilteringNeeded = false;
			}
			if (!isFilteringNeeded)
				isFilteringNeeded = (this.level != Logs.LogLevel.Undefined);
			if (!isFilteringNeeded)
				isFilteringNeeded = (this.processId != null);
			if (!isFilteringNeeded)
				isFilteringNeeded = (this.threadId != null);

			// no need to filter
			if (!isFilteringNeeded)
			{
				this.FilteringProgress = 0;
				this.OnPropertyChanged(nameof(FilteringProgress));
				if (this.IsFiltering)
				{
					this.IsFiltering = false;
					this.OnPropertyChanged(nameof(IsFiltering));
				}
				if (this.IsFilteringNeeded)
				{
					this.IsFilteringNeeded = false;
					this.OnPropertyChanged(nameof(IsFilteringNeeded));
				}
				return;
			}

			// start filtering
			if (!this.IsFilteringNeeded)
			{
				this.IsFilteringNeeded = true;
				this.OnPropertyChanged(nameof(IsFilteringNeeded));
			}
			filteringParams.CombinationMode = this.combinationMode;
			filteringParams.IncludeMarkedLogs = this.includeMarkedLogs;
			filteringParams.Level = this.level;
			filteringParams.LogTextPropertyGetters = textPropertyGetters;
			filteringParams.ProcessId = this.processId;
			filteringParams.TextRegexList = this.textRegexList;
			filteringParams.ThreadId = this.threadId;
			this.unfilteredLogs.AddAll(this.SourceLogs, true);
			this.currentFilterParams = filteringParams;
			for (var i = 0; i < this.maxFilteringConcurrencyLevel; ++i)
				this.FilterNextChunk(filteringParams);
		}


		/// <summary>
		/// Get or set list of <see cref="Regex"/> to filter text properties.
		/// </summary>
		public IList<Regex> TextRegexList
		{
			get => this.textRegexList;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.textRegexList.SequenceEqual(value))
					return;
				this.textRegexList = new List<Regex>(value).AsReadOnly();
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(TextRegexList));
			}
		}


		/// <summary>
		/// Get or set thread ID of log to filter.
		/// </summary>
		public int? ThreadId
		{
			get => this.threadId;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				if (this.threadId == value)
					return;
				this.threadId = value;
				this.startFilteringLogsAction.Schedule();
				this.OnPropertyChanged(nameof(ThreadId));
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public event PropertyChangedEventHandler? PropertyChanged;
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
