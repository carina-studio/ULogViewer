using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Data.Converters;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Group of <see cref="DisplayableLog"/>.
	/// </summary>
	partial class DisplayableLogGroup : BaseDisposable, IApplicationObject, ILogGroup
	{
		/// <summary>
		/// Maximum number of log reader can be added to each group.
		/// </summary>
		public const int MaxLogReaderCount = 255;
		
		
		// Control block of log reader.
		class LogReaderInfo(LogReader reader, byte localId)
		{
			// Fields.
			public int DisplayableLogCount;
			public readonly byte LocalId = localId;
			public readonly LogReader LogReader = reader;
		}


		// Token of progressive logs removing.
		class ProgressiveLogsRemovingToken(DisplayableLogGroup group, Func<bool> triggerAction): BaseDisposable
		{
			// Dispose.
			protected override void Dispose(bool disposing)
			{
				if (disposing) 
					group.CancelProgressiveLogsRemoving(this);
			}
			
			// Whether logs removing was triggered or not.
			public bool IsTriggered { get; private set; }
			
			// Trigger logs removing.
			public bool Trigger()
			{
				if (triggerAction())
				{
					IsTriggered = true;
					return true;
				}
				return false;
			}
		}
		
		
		// Constants.
		const int RecentlyUsedColorIndicatorColorCount = 8;


		// Static fields.
		static readonly long BaseMemorySize = Memory.EstimateInstanceSize<DisplayableLogGroup>();
		static Regex? ExtraCaptureRegex;
		static readonly SortedList<uint, DisplayableLogGroup> InstancesById = new();
		static readonly long LogReaderInfoKeyValuePairMemorySize = Memory.EstimateInstanceSize<KeyValuePair<byte, LogReaderInfo>>();
		static readonly long LogReaderInfoMemorySize = Memory.EstimateInstanceSize<LogReaderInfo>();
		static uint NextId = 1;
		static Regex? TextFilterBracketAndSeparatorRegex;


		// Fields.
		ProgressiveLogsRemovingToken? activeProgressiveLogsRemovingToken;
		IList<Regex> activeTextFilters = Array.Empty<Regex>();
		readonly Dictionary<DisplayableLogAnalysisResultType, IImage> analysisResultIndicatorIcons = new();
		readonly Dictionary<string, IBrush> colorIndicatorBrushes = new();
		Func<DisplayableLog, string>? colorIndicatorKeyGetter;
		DisplayableLog? displayableLogsHead;
		readonly Dictionary<string, IBrush> levelBackgroundBrushes = new();
		readonly Dictionary<string, IBrush> levelForegroundBrushes = new();
		readonly ILogger logger;
		readonly SortedList<byte, LogReaderInfo> logReaderInfosByLocalId = new();
		readonly Dictionary<LogReader, LogReaderInfo> logReaderInfosByLogReader = new();
		readonly int maxDisplayLineCount = 1;
		long memorySize = BaseMemorySize 
		                  + (Memory.EstimateCollectionInstanceSize(LogReaderInfoKeyValuePairMemorySize, 0) << 1);
		readonly object memorySizeLock = new();
		readonly Random random = new();
		readonly Queue<Color> recentlyUsedColorIndicatorColors = new(RecentlyUsedColorIndicatorColorCount);
		readonly List<ProgressiveLogsRemovingToken> scheduledProgressiveLogsRemovingTokens = new();
		int? selectedProcessId;
		int? selectedThreadId;
		readonly Stopwatch stopwatch = new();
		IBrush? textHighlightingBackground;
		IBrush? textHighlightingForeground;
		readonly ScheduledAction triggerProgressiveLogsRemovingAction;
		readonly ScheduledAction updateLevelMapAction;
		readonly ScheduledAction updateTextHighlightingDefSetAction;


		/// <summary>
		/// Initialize new <see cref="DisplayableLogGroup"/> instance.
		/// </summary>
		/// <param name="profile">Log profile.</param>
		public DisplayableLogGroup(LogProfile profile)
		{
			// get ID
			uint id;
			do
			{
				id = unchecked(NextId++);
				if (id == 0)
					id = NextId++;
			} while (InstancesById.TryGetValue(id, out _));
			this.Id = id;
			InstancesById.Add(id, this);

			// start watch
			var app = profile.Application;
			if (app.IsDebugMode)
				this.stopwatch.Start();
			
			// create logger
			this.logger = app.LoggerFactory.CreateLogger($"{nameof(DisplayableLogGroup)}-{this.Id}");
			
			// setup properties
			this.Application = app;
			this.LogProfile = profile;
			this.MemoryUsagePolicy = app.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy);
			this.TextHighlightingDefinitionSet = new($"Text Highlighting of {this}");
			this.CheckLogExtras();
			this.UpdateLevelMapForDisplaying();

			// setup actions
			this.triggerProgressiveLogsRemovingAction = new(() =>
			{
				if (this.activeProgressiveLogsRemovingToken != null)
					return;
				while (this.scheduledProgressiveLogsRemovingTokens.IsNotEmpty())
				{
					var token = this.scheduledProgressiveLogsRemovingTokens[0];
					this.scheduledProgressiveLogsRemovingTokens.RemoveAt(0);
					if (token.Trigger())
					{
						this.activeProgressiveLogsRemovingToken = token;
						this.logger.LogDebug("Progressive logs removing triggered, pending: {count}", this.scheduledProgressiveLogsRemovingTokens.Count);
						break;
					}
				}
				if (this.activeProgressiveLogsRemovingToken == null && this.scheduledProgressiveLogsRemovingTokens.IsEmpty())
					this.logger.LogDebug("All progressive logs removing completed");
			});
			this.updateTextHighlightingDefSetAction = new(() =>
			{
				var definitions = this.TextHighlightingDefinitionSet.TokenDefinitions;
				definitions.Clear();
				if (this.activeTextFilters.IsNotEmpty())
				{
					var areTextFiltersValid = true;
					foreach (var textFilter in this.activeTextFilters)
					{
						if (Utility.IsAllMatchingRegex(textFilter))
						{
							areTextFiltersValid = false;
							break;
						}
					}
					if (areTextFiltersValid)
					{
						this.textHighlightingBackground ??= this.Application.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.HighlightedText.Background", Brushes.LightGray);
						this.textHighlightingForeground ??= this.Application.FindResourceOrDefault<IBrush>("Brush/SessionView.LogListBox.Item.HighlightedText.Foreground", Brushes.Yellow);
						foreach (var textFilter in this.activeTextFilters)
						{
							var originalPattern = textFilter.ToString();
							while (originalPattern.Length >= 2 && originalPattern[0] == '(' && originalPattern[^1] == ')')
								originalPattern = originalPattern[1..^1];
							TextFilterBracketAndSeparatorRegex ??= CreateTextFilterBracketAndSeparatorRegex();
							var match = TextFilterBracketAndSeparatorRegex.Match(originalPattern);
							if (match.Success)
							{
								var start = 0;
								var bracketCount = 0;
								var options = textFilter.Options;
								var subPatternBuffer = new StringBuilder();
								while (match.Success)
								{
									if (match.Index > start)
										subPatternBuffer.Append(originalPattern[start..match.Index]);
									start = match.Index + match.Length;
									if (match.Groups["StartBracket"].Success)
									{
										++bracketCount;
										subPatternBuffer.Append('(');
									}
									else if (match.Groups["EndBracket"].Success)
									{
										if (bracketCount > 0)
										{
											--bracketCount;
											subPatternBuffer.Append(')');
										}
									}
									else if (bracketCount == 0)
									{
										if (subPatternBuffer.Length > 0)
										{
											try
											{
												definitions.Add(new()
												{
													Background = this.textHighlightingBackground,
													Foreground = this.textHighlightingForeground,
													Pattern = new(subPatternBuffer.ToString(), options),
												});
											}
											// ReSharper disable EmptyGeneralCatchClause
											catch
											{ }
											// ReSharper restore EmptyGeneralCatchClause
											subPatternBuffer.Clear();
										}
									}
									else
										subPatternBuffer.Append(match.Groups["Separator"].Value);
									match = match.NextMatch();
								}
								if (start < originalPattern.Length)
									subPatternBuffer.Append(originalPattern[start..^0]);
								if (subPatternBuffer.Length > 0)
								{
									try
									{
										definitions.Add(new()
										{
											Background = this.textHighlightingBackground,
											Foreground = this.textHighlightingForeground,
											Pattern = new(subPatternBuffer.ToString(), options),
										});
									}
									// ReSharper disable EmptyGeneralCatchClause
									catch
									{ }
									// ReSharper restore EmptyGeneralCatchClause
								}
							}
							else
							{
								definitions.Add(new()
								{
									Background = this.textHighlightingBackground,
									Foreground = this.textHighlightingForeground,
									Pattern = textFilter,
								});
							}
						}
					}
				}
			});
			this.updateLevelMapAction = new(this.UpdateLevelMapForDisplaying);

			// add event handlers
			app.Settings.SettingChanged += this.OnSettingChanged;
			app.PropertyChanged += this.OnApplicationPropertyChanged;
			app.StringsUpdated += this.OnApplicationStringsUpdated;
			profile.PropertyChanged += this.OnLogProfilePropertyChanged;

			// setup brushes
			this.UpdateColorIndicatorBrushes();
			this.UpdateLevelBrushes();
		}


		/// <summary>
		/// Get or set list of text filters which are applied on the group.
		/// </summary>
		public IList<Regex> ActiveTextFilters
		{
			get => this.activeTextFilters;
			set
			{
				this.VerifyAccess();
				this.VerifyDisposed();
				this.activeTextFilters = value.IsNullOrEmpty() 
					? Array.Empty<Regex>() 
					: new List<Regex>(value).AsReadOnly();
				this.updateTextHighlightingDefSetAction.Schedule();
			}
		}


		/// <summary>
		/// Raised when a set of analysis result has been added to a log.
		/// </summary>
		public event DirectDisplayableLogEventHandler? AnalysisResultAdded;


		/// <summary>
		/// Raised when a set of analysis result has been removed from a log.
		/// </summary>
		public event DirectDisplayableLogEventHandler? AnalysisResultRemoved;


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }
		
		
		// Cancel scheduled or on going progressive logs removing.
		void CancelProgressiveLogsRemoving(ProgressiveLogsRemovingToken token)
		{
			this.VerifyAccess();
			if (this.activeProgressiveLogsRemovingToken == token)
			{
				this.logger.LogDebug("Complete current progressive logs removing");
				this.activeProgressiveLogsRemovingToken = null;
				this.triggerProgressiveLogsRemovingAction.Schedule();
			}
			else if (this.scheduledProgressiveLogsRemovingTokens.Remove(token))
				this.logger.LogDebug("Cancel scheduled progressive logs removing, pending: {count}", this.scheduledProgressiveLogsRemovingTokens.Count);
		}


		// Check state of Extra* log properties.
		[MemberNotNull(nameof(LogExtraNumbers))]
		void CheckLogExtras()
		{
			ExtraCaptureRegex ??= CreateExtraCaptureRegex();
			var extraNumbers = new SortedObservableList<int>();
			foreach (var pattern in this.LogProfile.LogPatterns)
			{
				var match = ExtraCaptureRegex.Match(pattern.Regex.ToString());
				while (match.Success)
				{
					if (int.TryParse(match.Groups["Number"].Value, out var index) && index > 0 && index <= Log.ExtraCapacity)
						Global.RunWithoutError(() => extraNumbers.Add(index));
					match = match.NextMatch();
				}
			}
			this.LogExtraNumbers = ListExtensions.AsReadOnly(extraNumbers);
			this.LogExtraNumberCount = extraNumbers.Count;
		}


		/// <summary>
		/// Raised when brushes of color indicator of log are needed to be updated.
		/// </summary>
		public event EventHandler? ColorIndicatorBrushesUpdated;


		/// <summary>
		/// Create new <see cref="DisplayableLog"/> instance.
		/// </summary>
		/// <param name="reader">Log reader which reads the log.</param>
		/// <param name="log">Log to be wrapped.</param>
		/// <returns><see cref="DisplayableLog"/>.</returns>
		public DisplayableLog CreateDisplayableLog(LogReader reader, Log log)
		{
#if DEBUG
			this.VerifyAccess();
#endif
			this.VerifyDisposed();
			return new DisplayableLog(this, reader, log);
		}


		// Create regex to find extra capture in pattern of raw log line.
		[GeneratedRegex(@"\(\?\<Extra(?<Number>[\d]+)\>")]
		private static partial Regex CreateExtraCaptureRegex();


		// Create regex to find bracket and separator in text filter.
		[GeneratedRegex(@"(?<=(^|[^\\])(\\\\)*)(?<StartBracket>\()|(?<=(^|[^\\])(\\\\)*)(?<Separator>(\\\$){1,2})|(?<=(^|[^\\])(\\\\)*)(?<EndBracket>\))")]
		private static partial Regex CreateTextFilterBracketAndSeparatorRegex();
		
		
		/// <summary>
		/// Raised when debug message has been generated.
		/// </summary>
		public event EventHandler<MessageEventArgs>? DebugMessageGenerated; 


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// remove from ID table
			if (disposing)
			{
				this.VerifyAccess();
				InstancesById.Remove(this.Id);
			}
			else
			{
				this.SynchronizationContext.Post(() =>
				{
					if (InstancesById.TryGetValue(this.Id, out var instance) && ReferenceEquals(this, instance))
						InstancesById.Remove(this.Id);
				});
			}
			
			// ignore managed resources
			if (!disposing)
				return;

			// check thread
			this.VerifyAccess();

			// remove event handlers
			this.Application.Settings.SettingChanged -= this.OnSettingChanged;
			this.Application.PropertyChanged -= this.OnApplicationPropertyChanged;
			this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
			this.LogProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;

			// clear resources
			this.colorIndicatorBrushes.Clear();
			this.levelBackgroundBrushes.Clear();
			this.levelForegroundBrushes.Clear();
			this.recentlyUsedColorIndicatorColors.Clear();

			// cancel actions
			this.updateLevelMapAction.Cancel();
			this.updateTextHighlightingDefSetAction.Cancel();

			// stop watch
			this.stopwatch.Stop();
			
			// clear log reader info
			this.logReaderInfosByLogReader.Clear();
			this.logReaderInfosByLocalId.Clear();
		}


		/// <summary>
		/// Get icon for analysis result indicator.
		/// </summary>
		/// <param name="type">Type of analysis result.</param>
		/// <returns>Icon for analysis result indicator.</returns>
		internal IImage? GetAnalysisResultIndicatorIcon(DisplayableLogAnalysisResultType type)
		{
			if (this.analysisResultIndicatorIcons.TryGetValue(type, out var icon))
				return icon;
			icon = DisplayableLogAnalysisResultIconConverter.Default.Convert<IImage?>(type);
			if (icon != null)
				this.analysisResultIndicatorIcons[type] = icon;
			return icon;
		}


		/// <summary>
		/// Get <see cref="IBrush"/> of color indicator for given log.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <returns><see cref="IBrush"/> of color indicator.</returns>
		internal IBrush? GetColorIndicatorBrush(DisplayableLog log)
		{
			if (this.colorIndicatorKeyGetter == null)
				return null;
			return this.GetColorIndicatorBrushInternal(this.colorIndicatorKeyGetter(log));
		}


		/// <summary>
		/// Get <see cref="IBrush"/> of color indicator for given file name.
		/// </summary>
		/// <param name="fileName">File name.</param>
		/// <returns><see cref="IBrush"/> of color indicator.</returns>
		internal IBrush? GetColorIndicatorBrush(string fileName)
		{
			if (this.LogProfile.ColorIndicator != LogColorIndicator.FileName)
				return null;
			return this.GetColorIndicatorBrushInternal(fileName);
		}


		// Get brush for color indicator.
		IBrush? GetColorIndicatorBrushInternal(string key)
		{
			if (this.IsDisposed)
				return null;
			if (this.colorIndicatorBrushes.TryGetValue(key, out var brush))
				return brush;
			var color = Colors.Transparent;
			for (var i = RecentlyUsedColorIndicatorColorCount << 1; i > 0; --i)
			{
				color = Color.FromArgb(255, 
					(byte)(this.random.Next(32, 101) << 1), 
					(byte)(this.random.Next(32, 101) << 1), 
					(byte)(this.random.Next(32, 101) << 1)
				);
				var closeToRuColors = false;
				foreach (var ruColor in this.recentlyUsedColorIndicatorColors)
				{
					var rDistance = Math.Abs(color.R - ruColor.R);
					var gDistance = Math.Abs(color.G - ruColor.G);
					var bDistance = Math.Abs(color.B - ruColor.B);
					var mDistance = Math.Max(Math.Max(rDistance, gDistance), bDistance);
					if (mDistance <= 48)
					{
						closeToRuColors = true;
						break;
					}
					if (rDistance <= 32 && gDistance <= 32 && bDistance <= 32)
					{
						closeToRuColors = true;
						break;
					}
				}
				if (!closeToRuColors)
					break;
			}
			brush = new SolidColorBrush(color);
			this.colorIndicatorBrushes[key] = brush;
			while (this.recentlyUsedColorIndicatorColors.Count >= RecentlyUsedColorIndicatorColorCount)
				this.recentlyUsedColorIndicatorColors.Dequeue();
			this.recentlyUsedColorIndicatorColors.Enqueue(color);
			return brush;
		}


		/// <summary>
		/// Get tip text of color indicator for given log.
		/// </summary>
		/// <param name="log">Log.</param>
		/// <returns>Tip text.</returns>
		internal string? GetColorIndicatorTip(DisplayableLog log) =>
			this.colorIndicatorKeyGetter?.Invoke(log);


		/// <summary>
		/// Get background <see cref="IBrush"/> for given log.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <param name="state">State.</param>
		/// <returns><see cref="IBrush"/> for given log.</returns>
		internal IBrush GetLevelBackgroundBrush(DisplayableLog log, string? state = null)
		{
			if (this.IsDisposed)
				return Brushes.Transparent;
			if(!string.IsNullOrEmpty(state) && this.levelBackgroundBrushes.TryGetValue($"{log.Level}.{state}", out var brush))
				return brush.AsNonNull();
			if (this.levelBackgroundBrushes.TryGetValue(log.Level.ToString(), out brush))
				return brush.AsNonNull();
			if (this.levelBackgroundBrushes.TryGetValue(nameof(Logs.LogLevel.Undefined), out brush))
				return brush.AsNonNull();
			throw new ArgumentException($"Cannot get background brush for log level {log.Level}.");
		}


		/// <summary>
		/// Get foreground <see cref="IBrush"/> for given log.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <param name="state">State.</param>
		/// <returns><see cref="IBrush"/> for given log.</returns>
		internal IBrush GetLevelForegroundBrush(DisplayableLog log, string? state = null)
		{
			if (this.IsDisposed)
				return Brushes.Transparent;
			if(!string.IsNullOrEmpty(state) && this.levelForegroundBrushes.TryGetValue($"{log.Level}.{state}", out var brush))
				return brush.AsNonNull();
			if (this.levelForegroundBrushes.TryGetValue(log.Level.ToString(), out brush))
				return brush.AsNonNull();
			if (this.levelForegroundBrushes.TryGetValue(nameof(Logs.LogLevel.Undefined), out brush))
				return brush.AsNonNull();
			throw new ArgumentException($"Cannot get foreground brush for log level {log.Level}.");
		}


		/// <summary>
		/// Get unique ID of group.
		/// </summary>
		public uint Id { get; }


		/// <summary>
		/// Get map of converting from <see cref="Logs.LogLevel"/> to string.
		/// </summary>
		public IDictionary<Logs.LogLevel, string> LevelMapForDisplaying { get; private set; }
		
		
		/// <summary>
		/// Get number of Extra* log properties were defined.
		/// </summary>
		public int LogExtraNumberCount { get; private set; }
		
		
		/// <summary>
		/// Get list of numbers of Extra* log properties.
		/// </summary>
		public IList<int> LogExtraNumbers { get; private set; }


		/// <summary>
		/// Get related log profile.
		/// </summary>
		public LogProfile LogProfile { get; }


		/// <summary>
		/// Get maximum line count to display for each log.
		/// </summary>
		public int MaxDisplayLineCount => this.maxDisplayLineCount;


		/// <summary>
		/// Get size of memory usage by the group in bytes.
		/// </summary>
		public long MemorySize => this.memorySize;


		/// <summary>
		/// Policy of memory usage.
		/// </summary>
		public MemoryUsagePolicy MemoryUsagePolicy { get; private set; }


		/// <summary>
		/// Called when a set of analysis result has been added to a log.
		/// </summary>
		/// <param name="log">Log.</param>
		internal void OnAnalysisResultAdded(DisplayableLog log) =>
			this.AnalysisResultAdded?.Invoke(this, log);
		

		/// <summary>
		/// Called when a set of analysis result has been removed from a log.
		/// </summary>
		/// <param name="log">Log.</param>
		internal void OnAnalysisResultRemoved(DisplayableLog log) =>
			this.AnalysisResultRemoved?.Invoke(this, log);


		// Called when application property changed.
		void OnApplicationPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IULogViewerApplication.EffectiveThemeMode))
			{
				this.SynchronizationContext.Post(() =>
				{
					this.analysisResultIndicatorIcons.Clear();
					this.textHighlightingBackground = null;
					this.textHighlightingForeground = null;
					this.UpdateLevelBrushes();
					this.updateTextHighlightingDefSetAction.Execute();
					var log = this.displayableLogsHead;
					while (log != null)
					{
						log.OnStyleResourcesUpdated();
						log = log.Next;
					}
				});
			}
		}


		// Called when application string resources updated.
		void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			var log = this.displayableLogsHead;
			while (log != null)
			{
				log.OnApplicationStringsUpdated();
				log = log.Next;
			}
		}


		/// <summary>
		/// Called when new <see cref="DisplayableLog"/> has been created.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <param name="reader">Log reader which reads the log.</param>
		/// <param name="readerLocalId">Local ID of log reader.</param>
		internal void OnDisplayableLogCreated(DisplayableLog log, LogReader reader, out byte readerLocalId)
		{
			// add to list
			if (this.displayableLogsHead != null)
			{
				log.Next = this.displayableLogsHead;
				this.displayableLogsHead.Previous = log;
			}
			this.displayableLogsHead = log;
			
			// create log reader info if needed
			if (this.logReaderInfosByLogReader.TryGetValue(reader, out var readerInfo))
				readerLocalId = readerInfo.LocalId;
			else
			{
				var isReaderLocalIdFound = false;
				var candidateReaderLocalId = (byte)(this.logReaderInfosByLogReader.Count + 1);
				if (this.logReaderInfosByLocalId.Count < MaxLogReaderCount)
				{
					for (var i = MaxLogReaderCount; i > 0; --i)
					{
						if (!this.logReaderInfosByLocalId.ContainsKey(candidateReaderLocalId))
						{
							isReaderLocalIdFound = true;
							break;
						}
						unchecked
						{
							++candidateReaderLocalId;
							if (candidateReaderLocalId == 0)
								candidateReaderLocalId = 1;
						}
					}
				}
				if (!isReaderLocalIdFound)
					throw new NotSupportedException($"Too many log readers in the displayable log group '{this.Id}'.");
				this.logger.LogTrace("Add log reader '{readerId}' with local ID '{localId}'", reader.Id, candidateReaderLocalId);
				readerInfo = new LogReaderInfo(reader, candidateReaderLocalId);
				readerLocalId = candidateReaderLocalId;
				this.logReaderInfosByLocalId.Add(candidateReaderLocalId, readerInfo);
				this.logReaderInfosByLogReader.Add(reader, readerInfo);
				this.UpdateMemorySize((LogReaderInfoKeyValuePairMemorySize << 1) + LogReaderInfoMemorySize);
			}
			++readerInfo.DisplayableLogCount;
			
			// update memory size
			this.UpdateMemorySize(log.MemorySize);
		}


		/// <summary>
		/// Called when <see cref="DisplayableLog"/> has been disposed.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		internal void OnDisplayableLogDisposed(DisplayableLog log)
		{
			// remove from list
			if (log.Previous != null)
				log.Previous.Next = log.Next;
			if (log.Next != null)
				log.Next.Previous = log.Previous;
			if (this.displayableLogsHead == log)
				this.displayableLogsHead = log.Next;
			log.Next = null;
			log.Previous = null;
			
			// remove from log reader info
			if (this.logReaderInfosByLocalId.TryGetValue(log.LogReaderLocalId, out var readerInfo))
			{
				--readerInfo.DisplayableLogCount;
				if (readerInfo.DisplayableLogCount <= 0)
				{
					this.logger.LogTrace("Remove log reader '{readerId}' with local ID '{localId}'", readerInfo.LogReader.Id, readerInfo.LocalId);
					this.logReaderInfosByLocalId.Remove(readerInfo.LocalId);
					this.logReaderInfosByLogReader.Remove(readerInfo.LogReader);
					this.UpdateMemorySize(-((LogReaderInfoKeyValuePairMemorySize << 1) + LogReaderInfoMemorySize));
				}
			}
			
			// update memory size
			this.UpdateMemorySize(-log.MemorySize);
		}


		/// <summary>
		/// Called when memory size of <see cref="DisplayableLog"/> has been changed.
		/// </summary>
		/// <param name="diff">Difference of memory size.</param>
		internal void OnDisplayableLogMemorySizeChanged(long diff) =>
			this.UpdateMemorySize(diff);


		// Called when property of log profile has been changed.
		void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch(e.PropertyName)
			{
				case nameof(LogProfile.ColorIndicator):
					{
						this.UpdateColorIndicatorBrushes();
						var log = this.displayableLogsHead;
						while (log != null)
						{
							log.OnStyleResourcesUpdated();
							log = log.Next;
						}
					}
					break;
				case nameof(LogProfile.LogLevelMapForReading):
				case nameof(LogProfile.LogLevelMapForWriting):
					this.updateLevelMapAction.Schedule();
					break;
				case nameof(LogProfile.LogPatterns):
					this.CheckLogExtras();
					break;
				case nameof(LogProfile.TimeSpanFormatForDisplaying):
					{
						var log = this.displayableLogsHead;
						while (log != null)
						{
							log.OnTimeSpanFormatChanged();
							log = log.Next;
						}
					}
					break;
				case nameof(LogProfile.TimestampFormatForDisplaying):
					{
						var log = this.displayableLogsHead;
						while (log != null)
						{
							log.OnTimestampFormatChanged();
							log = log.Next;
						}
					}
					break;
			}
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			/*
			if (e.Key == SettingKeys.MaxDisplayLineCountForEachLog)
			{
				this.maxDisplayLineCount = (int)e.Value;
				var log = this.displayableLogsHead;
				while (log != null)
				{
					log.OnMaxDisplayLineCountChanged();
					log = log.Next;
				}
			}
			*/
		}


		/// <summary>
		/// Remove <see cref="DisplayableLogAnalysisResult"/>s from logs which were generated by given analyzer.
		/// </summary>
		/// <param name="analyzer"><see cref="DisplayableLogAnalysisResult"/> which generates results.</param>
		public void RemoveAnalysisResults(IDisplayableLogAnalyzer<DisplayableLogAnalysisResult> analyzer)
		{
#if DEBUG
			this.VerifyAccess();
#endif
			this.VerifyDisposed();
			var log = this.displayableLogsHead;
			while (log != null)
			{
				log.RemoveAnalysisResults(analyzer);
				log = log.Next;
			}
		}
		
		
		// Schedule progressive logs removing.
		IDisposable ILogGroup.ScheduleProgressiveLogsRemoving(Func<bool> triggerAction)
		{
			this.VerifyAccess();
			var token = new ProgressiveLogsRemovingToken(this, triggerAction);
			this.scheduledProgressiveLogsRemovingTokens.Add(token);
			this.logger.LogDebug("Schedule progressive logs removing, pending: {count}", this.scheduledProgressiveLogsRemovingTokens.Count);
			this.triggerProgressiveLogsRemovingAction.Schedule();
			return token;
		}


		/// <summary>
		/// Get or set process ID which is currently selected by user.
		/// </summary>
		public int? SelectedProcessId
		{
			get => this.selectedProcessId;
			set
			{
				this.VerifyAccess();
				if (this.selectedProcessId == value)
					return;
				this.selectedProcessId = value;
				var time = this.Application.IsDebugMode ? this.stopwatch.ElapsedMilliseconds : 0L;
				var log = this.displayableLogsHead;
				while (log != null)
				{
					log.OnSelectedProcessIdChanged();
					log = log.Next;
				}
				if (time > 0)
					this.logger.LogTrace("[Performance] Took {duration} ms to update selected PID", this.stopwatch.ElapsedMilliseconds - time);
			}
		}


		/// <summary>
		/// Get or set process ID which is currently selected by user.
		/// </summary>
		public int? SelectedThreadId
		{
			get => this.selectedThreadId;
			set
			{
				this.VerifyAccess();
				if (this.selectedThreadId == value)
					return;
				this.selectedThreadId = value;
				var time = this.Application.IsDebugMode ? this.stopwatch.ElapsedMilliseconds : 0L;
				var log = this.displayableLogsHead;
				while (log != null)
				{
					log.OnSelectedThreadIdChanged();
					log = log.Next;
				}
				if (time > 0)
					this.logger.LogTrace("[Performance] Took {duration} ms to update selected TID", this.stopwatch.ElapsedMilliseconds - time);
			}
		}


		/// <summary>
		/// Get definition set of text highlighting.
		/// </summary>
		public SyntaxHighlightingDefinitionSet TextHighlightingDefinitionSet { get; }


		/// <inheritdoc/>
		public override string ToString() =>
			$"{nameof(DisplayableLogGroup)}-{this.Id}";


		/// <summary>
		/// Try getting instance of <see cref="DisplayableLogGroup"/> by its ID.
		/// </summary>
		/// <param name="id">ID.</param>
		/// <param name="group"><see cref="DisplayableLogGroup"/> with given ID or Null if instance cannot be found.</param>
		/// <returns>True if instance found.</returns>
		internal static bool TryGetInstanceById(uint id, [NotNullWhen(true)] out DisplayableLogGroup? group) =>
			InstancesById.TryGetValue(id, out group);


		/// <summary>
		/// Try getting log reader by given local ID.
		/// </summary>
		/// <param name="localId">Local ID of log reader.</param>
		/// <param name="reader">Log reader.</param>
		/// <returns>True if log reader got successfully.</returns>
		internal bool TryGetLogReaderByLocalId(byte localId, [NotNullWhen(true)] out LogReader? reader)
		{
			if (this.logReaderInfosByLocalId.TryGetValue(localId, out var readerInfo))
			{
				reader = readerInfo.LogReader;
				return true;
			}
			reader = null;
			return false;
		}


		// Update color indicator brushes.
		void UpdateColorIndicatorBrushes()
		{
			// clear brushes
			this.colorIndicatorBrushes.Clear();

			// setup key getter
			this.colorIndicatorKeyGetter = this.LogProfile.ColorIndicator switch
			{
				LogColorIndicator.FileName => it => it.FileName?.ToString() ?? "",
				LogColorIndicator.ProcessId => it => it.ProcessId?.ToString() ?? "",
				LogColorIndicator.ProcessName => it => it.ProcessName?.ToString() ?? "",
				LogColorIndicator.ThreadId => it => it.ThreadId?.ToString() ?? "",
				LogColorIndicator.ThreadName => it => it.ThreadName?.ToString() ?? "",
				LogColorIndicator.UserId => it => it.UserId?.ToString() ?? "",
				LogColorIndicator.UserName => it => it.UserName?.ToString() ?? "",
				_ => null,
			};

			// raise event
			this.ColorIndicatorBrushesUpdated?.Invoke(this, EventArgs.Empty);
		}


		// Update level brushes.
		void UpdateLevelBrushes()
		{
			this.levelBackgroundBrushes.Clear();
			this.levelForegroundBrushes.Clear();
			var bgConverter = LogLevelBrushConverter.Background;
			var fgConverter = LogLevelBrushConverter.Foreground;
			foreach (var level in (Logs.LogLevel[])Enum.GetValues(typeof(Logs.LogLevel)))
			{
				if (bgConverter.Convert(level, typeof(IBrush), null, this.Application.CultureInfo) is IBrush bgBrush)
					this.levelBackgroundBrushes[level.ToString()] = bgBrush;
				if (fgConverter.Convert(level, typeof(IBrush), null, this.Application.CultureInfo) is IBrush fgBrush)
					this.levelForegroundBrushes[level.ToString()] = fgBrush;
				if (fgConverter.Convert(level, typeof(IBrush), "PointerOver", this.Application.CultureInfo) is IBrush pointerOverBrush)
					this.levelForegroundBrushes[$"{level}.PointerOver"] = pointerOverBrush;
			}
		}


		[MemberNotNull(nameof(LevelMapForDisplaying))]
		void UpdateLevelMapForDisplaying()
		{
			this.LevelMapForDisplaying = new Dictionary<Logs.LogLevel, string>().Also(it =>
			{
				foreach (var (s, level) in this.LogProfile.LogLevelMapForReading)
					it.TryAdd(level, s);
				foreach (var (level, s) in this.LogProfile.LogLevelMapForWriting)
					it[level] = s;
			});
			var log = this.displayableLogsHead;
			while (log != null)
			{
				log.OnLevelMapForDisplayingChanged();
				log = log.Next;
			}
		}
		
		
		// Update memory size
		void UpdateMemorySize(long diff)
		{
			lock (this.memorySizeLock)
			{
				this.memorySize += diff;
				if (this.memorySize < 0)
				{
#if DEBUG
					throw new InternalStateCorruptedException($"Memory size becomes negative: {this.memorySize}, diff: {diff}.");
#endif
					this.logger.LogError("Memory size becomes negative: {size}, diff: {diff}, stack trace: {st}", this.memorySize, diff, Environment.StackTrace);
					this.memorySize = 0;
					this.DebugMessageGenerated?.Invoke(this, new("Memory size becomes negative."));
				}
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		IApplication IApplicationObject.Application => this.Application;
		public SynchronizationContext SynchronizationContext => this.Application.SynchronizationContext;
	}
}
