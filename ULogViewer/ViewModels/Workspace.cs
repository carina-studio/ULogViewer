using CarinaStudio.Collections;
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


		// Attach to Session.
		void AttachToSession(Session session)
		{
			// add event handler
			session.PropertyChanged += this.OnSessionPropertyChanged;
		}


		/// <summary>
		/// Close and dispose given session.
		/// </summary>
		/// <param name="session">Session to close.</param>
		public async void CloseSession(Session session)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();

			// check parameter
			var index = this.sessions.IndexOf(session);
			if (index < 0)
			{
				this.Logger.LogError($"Unknown session '{session}' to close");
				return;
			}

			this.Logger.LogDebug($"Close session '{session}' at position {index}");

			// remove from list
			this.sessions.RemoveAt(index);

			// deactivate
			if (this.ActiveSession == session)
				this.ActiveSession = null;

			// detach
			this.DetachFromSession(session);

			// wait for task completion
			await session.WaitForNecessaryTasksAsync();

			// dispose
			session.Dispose();
		}


		/// <summary>
		/// Create new session.
		/// </summary>
		/// <param name="logProfile">Initial log profile.</param>
		/// <returns>Created session.</returns>
		public Session CreateSession(LogProfile? logProfile = null) => this.CreateSession(this.sessions.Count, logProfile);


		/// <summary>
		/// Create new session.
		/// </summary>
		/// <param name="index">Position of created session.</param>
		/// <param name="logProfile">Initial log profile.</param>
		/// <returns>Created session.</returns>
		public Session CreateSession(int index, LogProfile? logProfile = null)
		{
			// check state
			this.VerifyAccess();
			this.VerifyDisposed();

			// check parameter
			if (index < 0 || index > this.sessions.Count)
				throw new ArgumentOutOfRangeException();

			// create session
			var session = new Session(this);
			this.sessions.Insert(index, session);
			this.AttachToSession(session);
			this.Logger.LogDebug($"Session '{session}' created at position {index}");

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
		public Session CreateSessionWithLogFiles(LogProfile logProfile, IEnumerable<string> filePaths) => this.CreateSessionWithLogFiles(this.sessions.Count, logProfile, filePaths);


		/// <summary>
		/// Create new session and add log files immediately.
		/// </summary>
		/// <param name="index">Position of created session.</param>
		/// <param name="logProfile">Log profile.</param>
		/// <param name="filePaths">Log file paths.</param>
		/// <returns>Created session.</returns>
		public Session CreateSessionWithLogFiles(int index, LogProfile logProfile, IEnumerable<string> filePaths)
		{
			// create session
			var session = this.CreateSession(index, logProfile);
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
		public Session CreateSessionWithWorkingDirectory(LogProfile logProfile, string directory) => this.CreateSessionWithWorkingDirectory(this.sessions.Count, logProfile, directory);


		/// <summary>
		/// Create new session and set working directory immediately.
		/// </summary>
		/// <param name="index">Position of created session.</param>
		/// <param name="logProfile">Log profile.</param>
		/// <param name="directory">Working directory path.</param>
		/// <returns>Created session.</returns>
		public Session CreateSessionWithWorkingDirectory(int index, LogProfile logProfile, string directory)
		{
			// create session
			var session = this.CreateSession(index, logProfile);
			if (session.LogProfile != logProfile)
				return session;

			// set working directory
			if (!session.SetWorkingDirectoryCommand.TryExecute(directory))
				this.Logger.LogWarning($"Unable to set working directory '{directory}' to session '{session}'");

			// complete
			return session;
		}


		// Detach from Session.
		void DetachFromSession(Session session)
		{
			// remove event handler
			session.PropertyChanged -= this.OnSessionPropertyChanged;
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
				this.DetachFromSession(session);
				session.Dispose();
			}
			this.sessions.Clear();

			// call base
			base.Dispose(disposing);
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
				this.CloseSession(session);

			// restore sessions
			if (jsonState.TryGetProperty("Sessions", out var jsonSessions) && jsonSessions.ValueKind == JsonValueKind.Array)
			{
				foreach (var jsonSession in jsonSessions.EnumerateArray())
				{
					var session = this.CreateSession();
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
		public override void SaveState(Utf8JsonWriter jsonWriter)
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
		}


		/// <summary>
		/// Get list of <see cref="Session"/>s.
		/// </summary>
		public IList<Session> Sessions { get; }
	}
}
