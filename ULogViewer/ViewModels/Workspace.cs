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
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels
{
	/// <summary>
	/// Workspace contains <see cref="Session"/>(s).
	/// </summary>
	class Workspace : ViewModel
	{
		/// <summary>
		/// Property of <see cref="ActiveSession"/>.
		/// </summary>
		public static readonly ObservableProperty<Session?> ActiveSessionProperty = ObservableProperty.Register<Workspace, Session?>(nameof(ActiveSession));
		/// <summary>
		/// Property of <see cref="Title"/>.
		/// </summary>
		public static readonly ObservableProperty<string?> TitleProperty = ObservableProperty.Register<Workspace, string?>(nameof(Title));


		// Fields.
		IDisposable? sessionActivationToken;
		readonly ObservableCollection<Session> sessions = new ObservableCollection<Session>();
		readonly ScheduledAction updateTitleAction;


		/// <summary>
		/// Initialize new <see cref="Workspace"/> instance.
		/// </summary>
		/// <param name="app">Application.</param>
		public Workspace(IApplication app) : base(app)
		{
			// setup properties
			this.Sessions = this.sessions.AsReadOnly();

			// create scheduled actions
			this.updateTitleAction = new ScheduledAction(() =>
			{
				if (this.IsDisposed)
					return;
				var session = this.ActiveSession;
				if (session == null)
					this.SetValue(TitleProperty, "ULogViewer");
				else
					this.SetValue(TitleProperty, $"ULogViewer - {session.Title}");
			});
			this.updateTitleAction.Execute();
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
		public void CloseSession(Session session)
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
			session.WaitForNecessaryTasksAsync();

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
				this.updateTitleAction.Schedule();
			}
		}


		// Called when property of session has been changed.
		void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(Session.Title):
					if (sender == this.ActiveSession)
						this.updateTitleAction.Schedule();
					break;
			}
		}


		/// <summary>
		/// Get list of <see cref="Session"/>s.
		/// </summary>
		public IList<Session> Sessions { get; }


		/// <summary>
		/// Get title.
		/// </summary>
		public string? Title { get => this.GetValue(TitleProperty); }


		// Wait for necessary task.
		public override async Task WaitForNecessaryTasksAsync()
		{
			await base.WaitForNecessaryTasksAsync();
			foreach(var session in this.sessions)
				await session.WaitForNecessaryTasksAsync();
		}
	}
}
