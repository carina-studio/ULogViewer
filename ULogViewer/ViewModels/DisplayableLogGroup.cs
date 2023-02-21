using Avalonia.Media;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
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
	partial class DisplayableLogGroup : BaseDisposable, IApplicationObject
	{
		// Constants.
		const int RecentlyUsedColorIndicatorColorCount = 8;


		// Static fields.
		static readonly long BaseMemorySize = Memory.EstimateInstanceSize<DisplayableLogGroup>();
		static Regex? ExtraCaptureRegex;
		static int NextId = 1;
		static Regex? TextFilterBracketAndSeparatorRegex;


		// Fields.
		IList<Regex> activeTextFilters = Array.Empty<Regex>();
		readonly Dictionary<DisplayableLogAnalysisResultType, IImage> analysisResultIndicatorIcons = new();
		readonly Dictionary<string, IBrush> colorIndicatorBrushes = new();
		Func<DisplayableLog, string>? colorIndicatorKeyGetter;
		DisplayableLog? displayableLogsHead;
		readonly Dictionary<string, IBrush> levelBackgroundBrushes = new();
		readonly Dictionary<string, IBrush> levelForegroundBrushes = new();
		readonly ILogger logger;
		int maxDisplayLineCount;
		long memorySize = BaseMemorySize;
		readonly Random random = new();
		readonly Queue<Color> recentlyUsedColorIndicatorColors = new(RecentlyUsedColorIndicatorColorCount);
		int? selectedProcessId;
		int? selectedThreadId;
		readonly Stopwatch stopwatch = new();
		IBrush? textHighlightingBackground;
		IBrush? textHighlightingForeground;
		readonly ScheduledAction updateLevelMapAction;
		readonly ScheduledAction updateTextHighlightingDefSetAction;


		/// <summary>
		/// Initialize new <see cref="DisplayableLogGroup"/> instance.
		/// </summary>
		/// <param name="profile">Log profile.</param>
		public DisplayableLogGroup(LogProfile profile)
		{
			// get ID
			this.Id = NextId++;

			// start watch
			var app = profile.Application;
			if (app.IsDebugMode)
				this.stopwatch.Start();
			
			// create logger
			this.logger = app.LoggerFactory.CreateLogger($"{nameof(DisplayableLogGroup)}-{this.Id}");
			
			// setup properties
			this.Application = app;
			this.LogProfile = profile;
			this.maxDisplayLineCount = Math.Max(1, app.Settings.GetValueOrDefault(SettingKeys.MaxDisplayLineCountForEachLog));
			this.MemoryUsagePolicy = app.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy);
			this.TextHighlightingDefinitionSet = new($"Text Highlighting of {this}");
			this.CheckMaxLogExtraNumber();
			this.UpdateLevelMapForDisplaying();

			// setup actions
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
											catch
											{ }
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
									catch
									{ }
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
		/// Raised when a set of analysis result has been remoed from a log.
		/// </summary>
		public event DirectDisplayableLogEventHandler? AnalysisResultRemoved;


		/// <summary>
		/// Get <see cref="IULogViewerApplication"/> instance.
		/// </summary>
		public IULogViewerApplication Application { get; }


		// Check maximum log extra number.
		void CheckMaxLogExtraNumber()
		{
			var maxNumber = 0;
			ExtraCaptureRegex ??= CreateExtraCaptureRegex();
			foreach (var pattern in this.LogProfile.LogPatterns)
			{
				var match = ExtraCaptureRegex.Match(pattern.Regex.ToString());
				while (match.Success)
				{
					if (int.TryParse(match.Groups["Number"].Value, out var number) && number > maxNumber)
						maxNumber = number;
					match = match.NextMatch();
				}
			}
			this.MaxLogExtraNumber = Math.Min(Log.ExtraCapacity, maxNumber);
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


		// Dispose.
		protected override void Dispose(bool disposing)
		{
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
		public int Id { get; }


		/// <summary>
		/// Get map of converting from <see cref="Logs.LogLevel"/> to string.
		/// </summary>
		public IDictionary<Logs.LogLevel, string> LevelMapForDisplaying { get; private set; }


		/// <summary>
		/// Get related log profile.
		/// </summary>
		public LogProfile LogProfile { get; }


		/// <summary>
		/// Get maximum line count to display for each log.
		/// </summary>
		public int MaxDisplayLineCount { get => this.maxDisplayLineCount; }


		/// <summary>
		/// Get maximum number of extras provided by each <see cref="Log"/> by <see cref="LogProfile"/>.
		/// </summary>
		public int MaxLogExtraNumber { get; private set; }


		/// <summary>
		/// Get size of memory usage by the group in bytes.
		/// </summary>
		public long MemorySize { get => this.memorySize; }


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
		internal void OnDisplayableLogCreated(DisplayableLog log)
		{
			if (this.displayableLogsHead != null)
			{
				log.Next = this.displayableLogsHead;
				this.displayableLogsHead.Previous = log;
			}
			this.displayableLogsHead = log;
			Interlocked.Add(ref this.memorySize, log.MemorySize);
		}


		/// <summary>
		/// Called when <see cref="DisplayableLog"/> has been disposed.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		internal void OnDisplayableLogDisposed(DisplayableLog log)
		{
			if (log.Previous != null)
				log.Previous.Next = log.Next;
			if (log.Next != null)
				log.Next.Previous = log.Previous;
			if (this.displayableLogsHead == log)
				this.displayableLogsHead = log.Next;
			log.Next = null;
			log.Previous = null;
			Interlocked.Add(ref this.memorySize, -log.MemorySize);
		}


		/// <summary>
		/// Called when memory size of <see cref="DisplayableLog"/> has been changed.
		/// </summary>
		/// <param name="diff">Difference of memory size.</param>
		internal void OnDisplayableLogMemorySizeChanged(long diff) =>
			Interlocked.Add(ref this.memorySize, diff);


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
					this.CheckMaxLogExtraNumber();
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
			else if (e.Key == SettingKeys.MemoryUsagePolicy)
			{
				var newPolicy = (MemoryUsagePolicy)e.Value;
				this.MemoryUsagePolicy = newPolicy;
				var log = this.displayableLogsHead;
				while (log != null)
				{
					log.OnMemoryUsagePolicyChanged(newPolicy);
					log = log.Next;
				}
			}
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


		// Update color indicator brushes.
		void UpdateColorIndicatorBrushes()
		{
			// clear brushes
			this.colorIndicatorBrushes.Clear();

			// setup key getter
			this.colorIndicatorKeyGetter = this.LogProfile.ColorIndicator switch
			{
				LogColorIndicator.FileName => it => it.FileName ?? "",
				LogColorIndicator.ProcessId => it => it.ProcessId?.ToString() ?? "",
				LogColorIndicator.ProcessName => it => it.ProcessName ?? "",
				LogColorIndicator.ThreadId => it => it.ThreadId?.ToString() ?? "",
				LogColorIndicator.ThreadName => it => it.ThreadName ?? "",
				LogColorIndicator.UserId => it => it.UserId ?? "",
				LogColorIndicator.UserName => it => it.UserName ?? "",
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
				foreach ((var s, var level) in this.LogProfile.LogLevelMapForReading)
					it.TryAdd(level, s);
				foreach ((var level, var s) in this.LogProfile.LogLevelMapForWriting)
					it[level] = s;
			});
			var log = this.displayableLogsHead;
			while (log != null)
			{
				log.OnLevelMapForDisplayingChanged();
				log = log.Next;
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
