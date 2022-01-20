﻿using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Workspace contains <see cref="Session"/>(s).
	/// </summary>
	class Workspace : AppSuite.ViewModels.MainWindowViewModel<IULogViewerApplication>
	{
		/// <summary>
		/// Property of <see cref="ActiveSession"/>.
		/// </summary>
		public static readonly ObservableProperty<Session?> ActiveSessionProperty = ObservableProperty.Register<Workspace, Session?>(nameof(ActiveSession));


		// Fields.
		IDisposable? sessionActivationToken;
		readonly ObservableCollection<Session> sessions = new ObservableCollection<Session>();


		/// <summary>
		/// Initialize new <see cref="Workspace"/> instance.
		/// </summary>
		/// <param name="savedState">Saved state in JSON format.</param>
		public Workspace(JsonElement? savedState) : base()
		{
			// setup properties
			this.Sessions = this.sessions.AsReadOnly();

			// restore
			savedState?.Let(savedState => this.RestoreState(savedState));
		}


		/// <summary>
		/// Get or set active <see cref="Session"/>.
		/// </summary>
		public Session? ActiveSession 
		{
			get => this.GetValue(ActiveSessionProperty);
			set => this.SetValue(ActiveSessionProperty, value);
		}


		/// <summary>
		/// Attach given session to this workspace.
		/// </summary>
		/// <param name="index">Index of position to place sessopn in <see cref="Sessions"/>.</param>
		/// <param name="session">Session to attach.</param>
		public void AttachSession(int index, Session session)
		{
			// check state and parameter
			this.VerifyAccess();
			this.VerifyDisposed();
			if (index < 0 || index > this.sessions.Count)
				throw new ArgumentOutOfRangeException();
			if (session.Owner == this)
				return;
			
			// detach from current session
			(session.Owner as Workspace)?.DetachSession(session);

			// attach
			this.Logger.LogDebug($"Attach {session} at position {index}, count: {this.sessions.Count + 1}");
			session.Owner = this;
			session.PropertyChanged += this.OnSessionPropertyChanged;
			this.sessions.Insert(index, session);
		}


		/// <summary>
		/// Create new session.
		/// </summary>
		/// <param name="logProfile">Initial log profile.</param>
		/// <returns>Created session.</returns>
		public Session CreateAndAttachSession(LogProfile? logProfile = null) => this.CreateAndAttachSession(this.sessions.Count, logProfile);


		/// <summary>
		/// Create new session.
		/// </summary>
		/// <param name="index">Position of created session.</param>
		/// <param name="logProfile">Initial log profile.</param>
		/// <returns>Created session.</returns>
		public Session CreateAndAttachSession(int index, LogProfile? logProfile = null)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();

			// check parameter
			if (index < 0 || index > this.sessions.Count)
				throw new ArgumentOutOfRangeException();

			// create session
			var session = new Session(this.Application);
			this.AttachSession(index, session);

			// set log profile
			if (logProfile != null && !session.SetLogProfileCommand.TryExecute(logProfile))
				this.Logger.LogWarning($"Unable to set initial log profile '{logProfile.Name}' to session '{session}'");

			// complete
			return session;
		}


		/// <summary>
		/// Create new session and add log files immediately.
		/// </summary>
		/// <param name="logProfile">Log profile.</param>
		/// <param name="filePaths">Log file paths.</param>
		/// <returns>Created session.</returns>
		public Session CreateAndAttachSessionWithLogFiles(LogProfile logProfile, IEnumerable<string> filePaths) => this.CreateAndAttachSessionWithLogFiles(this.sessions.Count, logProfile, filePaths);


		/// <summary>
		/// Create new session and add log files immediately.
		/// </summary>
		/// <param name="index">Position of created session.</param>
		/// <param name="logProfile">Log profile.</param>
		/// <param name="filePaths">Log file paths.</param>
		/// <returns>Created session.</returns>
		public Session CreateAndAttachSessionWithLogFiles(int index, LogProfile logProfile, IEnumerable<string> filePaths)
		{
			// create session
			var session = this.CreateAndAttachSession(index, logProfile);
			if (session.LogProfile != logProfile)
				return session;

			// add log files
			foreach (var filePath in filePaths)
			{
				if (!session.AddLogFileCommand.TryExecute(filePath))
				{
					this.Logger.LogWarning($"Unable to add log file '{filePath}' to session '{session}'");
					break;
				}
			}

			// complete
			return session;
		}


		/// <summary>
		/// Create new session and set working directory immediately.
		/// </summary>
		/// <param name="logProfile">Log profile.</param>
		/// <param name="directory">Working directory path.</param>
		/// <returns>Created session.</returns>
		public Session CreateAndAttachSessionWithWorkingDirectory(LogProfile logProfile, string directory) => this.CreateAndAttachSessionWithWorkingDirectory(this.sessions.Count, logProfile, directory);


		/// <summary>
		/// Create new session and set working directory immediately.
		/// </summary>
		/// <param name="index">Position of created session.</param>
		/// <param name="logProfile">Log profile.</param>
		/// <param name="directory">Working directory path.</param>
		/// <returns>Created session.</returns>
		public Session CreateAndAttachSessionWithWorkingDirectory(int index, LogProfile logProfile, string directory)
		{
			// create session
			var session = this.CreateAndAttachSession(index, logProfile);
			if (session.LogProfile != logProfile)
				return session;

			// set working directory
			if (!session.SetWorkingDirectoryCommand.TryExecute(directory))
				this.Logger.LogWarning($"Unable to set working directory '{directory}' to session '{session}'");

			// complete
			return session;
		}


		/// <summary>
		/// Detach, close and dispose given session.
		/// </summary>
		/// <param name="session">Session to close.</param>
		public async void DetachAndCloseSession(Session session)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();
			if (session.Owner != this)
				return;

			// detach
			this.DetachSession(session);

			this.Logger.LogDebug($"Close {session}");

			// wait for task completion
			await session.WaitForNecessaryTasksAsync();

			// dispose
			session.Dispose();
		}


		/// <summary>
		/// Detach given session from this workspace.
		/// </summary>
		/// <param name="session">Session to detach.</param>
		public void DetachSession(Session session)
		{
			// check state
			this.VerifyAccess();
			if (session.Owner != this)
				return;
			
			// find session
			var index = this.sessions.IndexOf(session);
			if (index < 0)
				return;
			
			// detach
			this.Logger.LogDebug($"Detach {session} at position {index}, count: {this.sessions.Count - 1}");
			this.sessions.RemoveAt(index);
			session.PropertyChanged -= this.OnSessionPropertyChanged;
			session.Owner = null;

			// update active session
			if (this.ActiveSession == session && !this.IsDisposed)
				this.ActiveSession = null;
		}


		// Dispose.
		protected override void Dispose(bool disposing)
		{
			// ignore disposing from finalizer
			if (!disposing)
			{
				base.Dispose(disposing);
				return;
			}

			// check thread
			this.VerifyAccess();

			// dispose sessions
			foreach (var session in this.sessions.ToArray())
			{
				this.DetachSession(session);
				session.Dispose();
			}
			this.sessions.Clear();

			// call base
			base.Dispose(disposing);
		}


		/// <summary>
		/// Move given session in <see cref="Sessions"/>.
		/// </summary>
		/// <param name="index">Index of session to be moved.</param>
		/// <param name="newIndex">Index of new position in <see cref="Sessions"/> before moving.</param>
		public void MoveSession(int index, int newIndex)
		{
			this.VerifyAccess();
			if (index < 0 || index >= this.sessions.Count)
				throw new ArgumentOutOfRangeException();
			if (newIndex < 0 || newIndex >= this.sessions.Count)
				throw new ArgumentOutOfRangeException();
			if (index == newIndex)
				return;
			this.sessions.Move(index, newIndex);
		}


		// Called when property changed.
		protected override void OnPropertyChanged(ObservableProperty property, object? oldValue, object? newValue)
		{
			base.OnPropertyChanged(property, oldValue, newValue);
			if (property == ActiveSessionProperty)
			{
				var session = (newValue as Session);
				if (session != null && !this.sessions.Contains(session))
					throw new ArgumentException($"Cannot activate {session} which is not belong to this workspace.");
				this.sessionActivationToken = this.sessionActivationToken.DisposeAndReturnNull();
				if (session != null)
					this.sessionActivationToken = session.Activate();
				this.InvalidateTitle();
			}
		}


		// Called when property of session has been changed.
		void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(Session.Title):
					if (sender == this.ActiveSession)
						this.InvalidateTitle();
					break;
			}
		}


		// Update title.
        protected override string? OnUpdateTitle()
        {
			var titleBuilder = new StringBuilder("ULogViewer");
			var postfixBuilder = new StringBuilder();
			var session = this.ActiveSession;
			if (this.Application.IsRunningAsAdministrator)
				postfixBuilder.Append(this.Application.GetString("Common.Administrator"));
			if (this.Application.IsDebugMode)
			{
				if (postfixBuilder.Length > 0)
					postfixBuilder.Append('|');
				postfixBuilder.Append(this.Application.GetString("Common.DebugMode"));
			}
			if (postfixBuilder.Length > 0)
			{
				titleBuilder.Append(" (");
				titleBuilder.Append(postfixBuilder);
				titleBuilder.Append(')');
			}
			if (session != null)
				titleBuilder.Append($" - {session.Title}");
			return titleBuilder.ToString();
		}


        /// <summary>
        /// Restore instance state from persistent state.
        /// </summary>
        /// <returns>True if instance state has been restored successfully.</returns>
        bool RestoreState(JsonElement jsonState)
		{
			// check state
			if (jsonState.ValueKind != JsonValueKind.Object)
			{
				this.Logger.LogError("Root element of saved state is not a JSON object");
				return false;
			}

			// close current sessions
			foreach (var session in this.sessions.ToArray())
				this.DetachAndCloseSession(session);

			// restore sessions
			if (jsonState.TryGetProperty("Sessions", out var jsonSessions) && jsonSessions.ValueKind == JsonValueKind.Array)
			{
				foreach (var jsonSession in jsonSessions.EnumerateArray())
				{
					var session = this.CreateAndAttachSession();
					try
					{
						session.RestoreState(jsonSession);
					}
					catch (Exception ex)
					{
						this.Logger.LogError(ex, $"Failed to restore state of session at position {this.sessions.Count - 1}");
					}
				}
			}

			// restore active session
			if (jsonState.TryGetProperty(nameof(ActiveSession), out var jsonValue)
				&& jsonValue.TryGetInt32(out var index)
				&& index >= 0
				&& index < this.sessions.Count)
			{
				this.ActiveSession = this.sessions[index];
			}

			// complete
			return true;
		}


		/// <inheritdoc/>
		public override bool SaveState(Utf8JsonWriter jsonWriter)
		{
			this.Logger.LogTrace("Start saving state");

			// start object
			jsonWriter.WriteStartObject();

			// save active session
			int activeSessionIndex = this.ActiveSession != null ? this.sessions.IndexOf(this.ActiveSession) : -1;
			if (activeSessionIndex >= 0)
				jsonWriter.WriteNumber(nameof(ActiveSession), activeSessionIndex);

			// save sessions state
			jsonWriter.WritePropertyName("Sessions");
			jsonWriter.WriteStartArray();
			foreach (var session in this.sessions)
				session.SaveState(jsonWriter);
			jsonWriter.WriteEndArray();

			// complete
			jsonWriter.WriteEndObject();
			this.Logger.LogTrace("Complete saving state");
			return true;
		}


		/// <summary>
		/// Get list of <see cref="Session"/>s.
		/// </summary>
		public IList<Session> Sessions { get; }
	}
}
