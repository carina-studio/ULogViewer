using CarinaStudio.Threading;
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
    /// <summary>
    /// Initialize new <see cref="SessionComponent"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    protected SessionComponent(Session session) : base(session.Application)
    { 
        // setup fields and properties
        this.Session = session;
        this.Owner = session;

        // attach to session
        (session.AllLogs as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnAllLogsChanged);
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


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // check thread
        if (disposing)
            this.VerifyAccess();
        
        // detach from session
        (this.Session.AllLogs as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnAllLogsChanged);
        this.Session.RestoringState -= this.OnRestoreState;
        this.Session.SavingState -= this.OnSaveState;
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
    /// Get current memory usage in bytes.
    /// </summary>
    public virtual long MemorySize { get => 0L; }


    // Called when logs changed.
    void OnAllLogsChanged(object? _, NotifyCollectionChangedEventArgs e) =>
        this.OnAllLogsChanged(e);


    /// <summary>
    /// Called when <see cref="AllLogs"/> changed.
    /// </summary>
    /// <param name="e">Event data.</param>
    protected virtual void OnAllLogsChanged(NotifyCollectionChangedEventArgs e)
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
