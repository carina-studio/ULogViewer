using Avalonia.Media;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Group of <see cref="DisplayableLog"/>.
	/// </summary>
	class DisplayableLogGroup : BaseDisposable, IApplicationObject
	{
		// Fields.
		readonly Dictionary<string, IBrush> colorIndicatorBrushes = new Dictionary<string, IBrush>();
		Func<DisplayableLog, string>? colorIndicatorKeyGetter;
		readonly LinkedList<DisplayableLog> displayableLogs = new LinkedList<DisplayableLog>();
		readonly Dictionary<LogLevel, IBrush> levelBrushes = new Dictionary<LogLevel, IBrush>();
		readonly Random random = new Random();


		/// <summary>
		/// Initialize new <see cref="DisplayableLogGroup"/> instance.
		/// </summary>
		/// <param name="profile">Log profile.</param>
		public DisplayableLogGroup(LogProfile profile)
		{
			// setup properties
			this.Application = profile.Application;
			this.LogProfile = profile;

			// add event handlers
			this.Application.Settings.SettingChanged += this.OnSettingChanged;
			this.Application.StringsUpdated += this.OnApplicationStringsUpdated;
			profile.PropertyChanged += this.OnLogProfilePropertyChanged;

			// setup brushes
			this.UpdateColorIndicatorBrushes();
			this.UpdateLevelBrushes();
		}


		/// <summary>
		/// Get <see cref="IApplication"/> instance.
		/// </summary>
		public IApplication Application { get; }


		/// <summary>
		/// Create new <see cref="DisplayableLog"/> instance.
		/// </summary>
		/// <param name="reader">Log reader which reads the log.</param>
		/// <param name="log">Log to be wrapped.</param>
		/// <returns><see cref="DisplayableLog"/>.</returns>
		public DisplayableLog CreateDisplayableLog(LogReader reader, Log log)
		{
			this.VerifyAccess();
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
			this.Application.StringsUpdated -= this.OnApplicationStringsUpdated;
			this.LogProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;

			// clear resources
			this.colorIndicatorBrushes.Clear();
			this.levelBrushes.Clear();
		}


		/// <summary>
		/// Get <see cref="IBrush"/> of color indicator for given log.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <returns><see cref="IBrush"/> of color indicator.</returns>
		internal IBrush? GetColorIndicatorBrush(DisplayableLog log)
		{
			this.VerifyDisposed();
			if (this.colorIndicatorKeyGetter == null)
				return null;
			var key = this.colorIndicatorKeyGetter(log);
			if (this.colorIndicatorBrushes.TryGetValue(key, out var brush))
				return brush.AsNonNull();
			brush = new SolidColorBrush(Color.FromArgb(255, (byte)this.random.Next(100, 201), (byte)this.random.Next(100, 201), (byte)this.random.Next(100, 201)));
			this.colorIndicatorBrushes[key] = brush;
			return brush;
		}


		/// <summary>
		/// Get <see cref="IBrush"/> for given log.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		/// <returns><see cref="IBrush"/> for given log.</returns>
		internal IBrush GetLevelBrush(DisplayableLog log)
		{
			this.VerifyDisposed();
			if (this.levelBrushes.TryGetValue(log.Level, out var brush))
				return brush.AsNonNull();
			if (this.levelBrushes.TryGetValue(LogLevel.Undefined, out brush))
				return brush.AsNonNull();
			throw new ArgumentException($"Cannot get brush for log level {log.Level}.");
		}


		/// <summary>
		/// Get related log profile.
		/// </summary>
		public LogProfile LogProfile { get; }


		// Called when application string resources updated.
		void OnApplicationStringsUpdated(object? sender, EventArgs e)
		{
			var node = this.displayableLogs.First;
			while (node != null)
			{
				node.Value.OnApplicationStringsUpdated();
				node = node.Next;
			}
		}


		/// <summary>
		/// Called when new <see cref="DisplayableLog"/> has been created.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		internal void OnDisplayableLogCreated(DisplayableLog log)
		{
			this.displayableLogs.AddLast(log.TrackingNode);
		}


		/// <summary>
		/// Called when <see cref="DisplayableLog"/> has been disposed.
		/// </summary>
		/// <param name="log"><see cref="DisplayableLog"/>.</param>
		internal void OnDisplayableLogDisposed(DisplayableLog log)
		{
			this.displayableLogs.Remove(log.TrackingNode);
		}


		// Called when property of log profile has been changed.
		void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch(e.PropertyName)
			{
				case nameof(LogProfile.ColorIndicator):
					{
						this.UpdateColorIndicatorBrushes();
						var node = this.displayableLogs.First;
						while (node != null)
						{
							node.Value.OnStyleResourcesUpdated();
							node = node.Next;
						}
					}
					break;
				case nameof(LogProfile.TimestampFormatForDisplaying):
					{
						var node = this.displayableLogs.First;
						while (node != null)
						{
							node.Value.OnTimestampFormatChanged();
							node = node.Next;
						}
					}
					break;
			}
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == Settings.DarkMode)
			{
				this.SynchronizationContext.Post(() =>
				{
					this.UpdateLevelBrushes();
					var node = this.displayableLogs.First;
					while (node != null)
					{
						node.Value.OnStyleResourcesUpdated();
						node = node.Next;
					}
				});
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
		}


		// Update level brushes.
		void UpdateLevelBrushes()
		{
			this.levelBrushes.Clear();
			foreach (var level in (LogLevel[])Enum.GetValues(typeof(LogLevel)))
			{
				var brush = LogLevelBrushConverter.Default.Convert(level, typeof(IBrush), null, this.Application.CultureInfo) as IBrush;
				if (brush != null)
					this.levelBrushes[level] = brush;
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
