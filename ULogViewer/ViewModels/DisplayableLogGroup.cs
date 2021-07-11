using Avalonia.Media;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
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
		readonly LinkedList<DisplayableLog> displayableLogs = new LinkedList<DisplayableLog>();
		readonly Dictionary<LogLevel, IBrush> levelBrushes = new Dictionary<LogLevel, IBrush>();


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

			// setup level brushes
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
		}


		/// <summary>
		/// Get <see cref="IBrush"/> for given log level.
		/// </summary>
		/// <param name="level">Log level.</param>
		/// <returns><see cref="IBrush"/> for given log level.</returns>
		internal IBrush GetLevelBrush(LogLevel level)
		{
			if (this.levelBrushes.TryGetValue(level, out var brush))
				return brush.AsNonNull();
			if (this.levelBrushes.TryGetValue(LogLevel.Undefined, out brush))
				return brush.AsNonNull();
			throw new ArgumentException($"Cannot get brush for log level {level}.");
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
			if (e.PropertyName == nameof(LogProfile.TimestampFormatForDisplaying))
			{
				var node = this.displayableLogs.First;
				while (node != null)
				{
					node.Value.OnTimestampFormatChanged();
					node = node.Next;
				}
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


		// Update level brushes.
		void UpdateLevelBrushes()
		{
			if (this.Application is not App app)
				return;
			var resources = app.Styles;
			this.levelBrushes.Clear();
			foreach (var level in (LogLevel[])Enum.GetValues(typeof(LogLevel)))
			{
				if (resources.TryGetResource($"Brush.DisplayableLog.Level.{level}", out var res))
					this.levelBrushes[level] = (IBrush)res.AsNonNull();
			}
		}


		// Interface implementations.
		public bool CheckAccess() => this.Application.CheckAccess();
		CarinaStudio.IApplication IApplicationObject.Application { get => this.Application; }
		public SynchronizationContext SynchronizationContext { get => this.Application.SynchronizationContext; }
	}
}
