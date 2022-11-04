using Avalonia.Media;
using CarinaStudio.Configuration;
using CarinaStudio.Data.Converters;
using CarinaStudio.Diagnostics;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Group of <see cref="DisplayableLog"/>.
	/// </summary>
	class DisplayableLogGroup : BaseDisposable, IApplicationObject
	{
		// Static fields.
		static readonly Regex ExtraCaptureRegex = new(@"\(\?\<Extra(?<Number>[\d]+)\>");


		// Static fields.
		static readonly long BaseMemorySize = Memory.EstimateInstanceSize<DisplayableLogGroup>();


		// Fields.
		readonly Dictionary<DisplayableLogAnalysisResultType, IImage> analysisResultIndicatorIcons = new();
		readonly Dictionary<string, IBrush> colorIndicatorBrushes = new();
		Func<DisplayableLog, string>? colorIndicatorKeyGetter;
		DisplayableLog? displayableLogsHead;
		readonly Dictionary<string, IBrush> levelBrushes = new();
		int maxDisplayLineCount;
		readonly Random random = new();


		/// <summary>
		/// Initialize new <see cref="DisplayableLogGroup"/> instance.
		/// </summary>
		/// <param name="profile">Log profile.</param>
		public DisplayableLogGroup(LogProfile profile)
		{
			// setup properties
			this.Application = profile.Application;
			this.LogProfile = profile;
			this.maxDisplayLineCount = Math.Max(1, this.Application.Settings.GetValueOrDefault(SettingKeys.MaxDisplayLineCountForEachLog));
			this.MemorySize = BaseMemorySize;
			this.MemoryUsagePolicy = this.Application.Settings.GetValueOrDefault(SettingKeys.MemoryUsagePolicy);
			this.CheckMaxLogExtraNumber();

			// add event handlers
			this.Application.Settings.SettingChanged += this.OnSettingChanged;
			this.Application.PropertyChanged += this.OnApplicationPropertyChanged;
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
			profile.PropertyChanged += this.OnLogProfilePropertyChanged;

			// setup brushes
			this.UpdateColorIndicatorBrushes();
			this.UpdateLevelBrushes();
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
			this.levelBrushes.Clear();
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
				return brush.AsNonNull();
			brush = new SolidColorBrush(Color.FromArgb(255, (byte)this.random.Next(100, 201), (byte)this.random.Next(100, 201), (byte)this.random.Next(100, 201)));
			this.colorIndicatorBrushes[key] = brush;
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
		/// Get <see cref="IBrush"/> for given log.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <returns><see cref="IBrush"/> for given log.</returns>
		internal IBrush GetLevelBrush(DisplayableLog log, string? state = null)
		{
			if (this.IsDisposed)
				return Brushes.Transparent;
			if(!string.IsNullOrEmpty(state) && this.levelBrushes.TryGetValue($"{log.Level}.{state}", out var brush))
				return brush.AsNonNull();
			if (this.levelBrushes.TryGetValue(log.Level.ToString(), out brush))
				return brush.AsNonNull();
			if (this.levelBrushes.TryGetValue(nameof(LogLevel.Undefined), out brush))
				return brush.AsNonNull();
			throw new ArgumentException($"Cannot get brush for log level {log.Level}.");
		}


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
		public long MemorySize { get; private set; }


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
					this.UpdateLevelBrushes();
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
			this.MemorySize += log.MemorySize;
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
			this.MemorySize -= log.MemorySize;
		}


		/// <summary>
		/// Called when memory size of <see cref="DisplayableLog"/> has been changed.
		/// </summary>
		/// <param name="diff">Difference of memory size.</param>
		internal void OnDisplayableLogMemorySizeChanged(long diff)
		{
			this.MemorySize += diff;
		}


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
			this.levelBrushes.Clear();
			var converter = LogLevelBrushConverter.Default;
			foreach (var level in (LogLevel[])Enum.GetValues(typeof(LogLevel)))
			{
				if (converter.Convert(level, typeof(IBrush), null, this.Application.CultureInfo) is IBrush brush)
					this.levelBrushes[level.ToString()] = brush;
				if (converter.Convert(level, typeof(IBrush), "PointerOver", this.Application.CultureInfo) is IBrush pointerOverBrush)
					this.levelBrushes[$"{level}.PointerOver"] = pointerOverBrush;
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
