using System.ComponentModel;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.Json;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Component of <see cref="Session"/>
/// </summary>
abstract class SessionComponent : ViewModel<IULogViewerApplication>
{
    // Fields.
    LogProfile? attachedLogProfile;
    readonly ISessionInternalAccessor internalAccessor;
    readonly IDisposable logProfileObserverToken;


    /// <summary>
    /// Initialize new <see cref="SessionComponent"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="internalAccessor">Accessor to internal state of session.</param>
    protected SessionComponent(Session session, ISessionInternalAccessor internalAccessor) : base(session.Application)
    { 
        // setup fields and properties
        this.internalAccessor = internalAccessor;
        this.Session = session;
        this.Owner = session;

        // attach to session
        session.AllComponentsCreated += this.OnAllComponentsCreated;
        (session.AllLogs as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnAllLogsChanged);
        internalAccessor.DisplayableLogGroupCreated += this.OnDisplayableLogGroupCreated;
        this.logProfileObserverToken = session.GetValueAsObservable(Session.LogProfileProperty).Subscribe(logProfile =>
        {
            var prevLogProfile = this.attachedLogProfile;
            if (prevLogProfile == logProfile)
                return;
            this.attachedLogProfile = logProfile;
            this.OnLogProfileChanged(prevLogProfile, logProfile);
        });
        session.RestoringState += this.OnRestoreState;
        session.SavingState += this.OnSaveState;
    }


    /// <summary>
    /// Get all logs.
    /// </summary>
    protected IList<DisplayableLog> AllLogs { get => this.Session.AllLogs; }


    /// <summary>
    /// Compare logs by session if available.
    /// </summary>
    /// <param name="lhs">Left hand side log.</param>
    /// <param name="rhs">Right hand side log.</param>
    /// <returns>Comparison result.</returns>
    protected int CompareLogs(DisplayableLog? lhs, DisplayableLog? rhs) =>
        this.Session.CompareDisplayableLogs(lhs, rhs);
    

    /// <summary>
    /// Get group of displayable logs.
    /// </summary>
    protected DisplayableLogGroup? DisplayableLogGroup { get => this.internalAccessor.DisplayableLogGroup; }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // check thread
        if (disposing)
            this.VerifyAccess();
        
        // detach from log profile
        this.Session.LogProfile?.Let(it => it.PropertyChanged -= this.OnLogProfilePropertyChanged);
        
        // detach from session
        this.Session.AllComponentsCreated -= this.OnAllComponentsCreated;
        (this.Session.AllLogs as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnAllLogsChanged);
        this.internalAccessor.DisplayableLogGroupCreated -= this.OnDisplayableLogGroupCreated;
        this.logProfileObserverToken.Dispose();
        this.Session.RestoringState -= this.OnRestoreState;
        this.Session.SavingState -= this.OnSaveState;

        // call base
        base.Dispose(disposing);
    }


    /// <summary>
    /// Raised when error message generated.
    /// </summary>
    public event EventHandler<MessageEventArgs>? ErrorMessageGenerated;


    /// <summary>
    /// Generate error message.
    /// </summary>
    /// <param name="message">Message.</param>
    protected void GenerateErrorMessage(string message)
    {
        this.VerifyAccess();
        if (!this.IsDisposed)
            this.ErrorMessageGenerated?.Invoke(this, new(message));
    }


    /// <summary>
    /// Get current log profile.
    /// </summary>
    protected LogProfile? LogProfile { get => this.attachedLogProfile; }


    /// <summary>
    /// Get current memory usage in bytes.
    /// </summary>
    public virtual long MemorySize { get => 0L; }


    /// <summary>
    /// Get memory usage policy.
    /// </summary>
    protected MemoryUsagePolicy MemoryUsagePolicy { get => this.internalAccessor.MemoryUsagePolicy; }


    /// <summary>
    /// Called when all instances of session components are created.
    /// </summary>
    protected virtual void OnAllComponentsCreated()
    { }


    // Called when logs changed.
    void OnAllLogsChanged(object? _, NotifyCollectionChangedEventArgs e) =>
        this.OnAllLogsChanged(e);


    /// <summary>
    /// Called when <see cref="AllLogs"/> changed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAllLogsChanged(NotifyCollectionChangedEventArgs e)
    { }


    // Called when new group of displayable logs created.
    void OnDisplayableLogGroupCreated(object? sender, EventArgs e) =>
        this.OnDisplayableLogGroupCreated();


    /// <summary>
    /// Called when new group of displayable logs created.
    /// </summary>
    protected virtual void OnDisplayableLogGroupCreated()
    { }


    /// <summary>
    /// Called when log profile changed.
    /// </summary>
    /// <param name="prevLogProfile">Previous log profile.</param>
    /// <param name="newLogProfile">New log profile.</param>
    protected virtual void OnLogProfileChanged(LogProfile? prevLogProfile, LogProfile? newLogProfile)
    {
        if (prevLogProfile != null)
            prevLogProfile.PropertyChanged -= this.OnLogProfilePropertyChanged;
        if (newLogProfile != null)
            newLogProfile.PropertyChanged += this.OnLogProfilePropertyChanged;
    }


    // Called when property of current log profile changed.
    void OnLogProfilePropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        this.OnLogProfilePropertyChanged(e);


    /// <summary>
    /// Called when property of current log profile changed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnLogProfilePropertyChanged(PropertyChangedEventArgs e)
    { }


    /// <inheritdoc/>
    protected override void OnOwnerChanged(ViewModel? prevOwner, ViewModel? newOwner)
    {
        base.OnOwnerChanged(prevOwner, newOwner);
        if (newOwner != null && newOwner != this.Session)
            throw new InvalidOperationException("Cannot change owner of SessionComponent.");
    }


    /// <summary>
    /// Restore state from JSON data.
    /// </summary>
    /// <param name="element">JSON element which contains state.</param>
    protected virtual void OnRestoreState(JsonElement element)
    { }


    /// <summary>
    /// Save state in JSON format.
    /// </summary>
    /// <param name="writer">Writer.</param>
    protected virtual void OnSaveState(Utf8JsonWriter writer)
    { }


    /// <summary>
    /// Get session which is attached to.
    /// </summary>
    protected Session Session { get; }
}
