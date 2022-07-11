using ASControls = CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Base implementation of <see cref="IContext"/>.
/// </summary>
abstract class BaseContext : IContext
{
    // Fields.
    readonly IDictionary<string, object> data = new ConcurrentDictionary<string, object>();
    readonly IULogViewerApplication app;
    volatile int userInputWaitingCount;


    /// <summary>
    /// Initialize new <see cref="BaseContext"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="name">Name of context.</param>
    public BaseContext(IULogViewerApplication app, string name)
    {
        this.app = app;
        this.Logger = app.LoggerFactory.CreateLogger(string.IsNullOrWhiteSpace(name) ? this.GetType().Name : name);
    }


    /// <summary>
    /// Get data for running script.
    /// </summary>
    public IDictionary<string, object> Data { get => this.data; }


    static MessageDialogResult GetDefaultMessageDialogResult(MessageDialogButtons buttons) => buttons switch
    {
        MessageDialogButtons.OK => MessageDialogResult.OK,
        MessageDialogButtons.OKCancel
        or MessageDialogButtons.YesNoCancel => MessageDialogResult.Cancel,
        MessageDialogButtons.YesNo => MessageDialogResult.No,
        _ => throw new NotImplementedException(),
    };


    /// <summary>
    /// Get or set whether showing message dialog is allowed or not.
    /// </summary>
    internal bool IsShowingMessageDialogAllowed { get; set; } = true;


    /// <summary>
    /// Get or set whether showing text input dialog is allowed or not.
    /// </summary>
    internal bool IsShowingTextInputDialogAllowed { get; set; } = true;


    /// <summary>
    /// Check whether context is waiting for user input or not.
    /// </summary>
    internal bool IsWaitingForUserInput { get => this.userInputWaitingCount > 0; }


    /// <summary>
    /// Raised when <see cref="IsWaitingForUserInput"/> changed.
    /// </summary>
    internal event Action<bool>? IsWaitingForUserInputChanged;


    /// <inheritdoc/>
    public ILogger Logger { get; }


    /// <inheritdoc/>
    public MessageDialogResult ShowMessageDialog(string? message, MessageDialogIcon icon, MessageDialogButtons buttons)
    {
        if (!this.IsShowingMessageDialogAllowed)
            return GetDefaultMessageDialogResult(buttons);
        var waitingCount = Interlocked.Increment(ref this.userInputWaitingCount);
        if (waitingCount == 1)
            this.IsWaitingForUserInputChanged?.Invoke(true);
        try
        {
            var result = GetDefaultMessageDialogResult(buttons);
            if (this.app.CheckAccess())
            {
                var window = this.app.LatestActiveMainWindow;
                if (window != null)
                {
                    var taskCompletionSource = new TaskCompletionSource();
                    this.app.SynchronizationContext.Post(async () =>
                    {
                        var dialog = new ASControls.MessageDialog()
                        {
                            Buttons = (ASControls.MessageDialogButtons)buttons,
                            DoNotAskOrShowAgain = false,
                            Icon = (ASControls.MessageDialogIcon)icon,
                            Message = message,
                        };
                        result = (MessageDialogResult)await dialog.ShowDialog(window);
                        this.IsShowingMessageDialogAllowed = !dialog.DoNotAskOrShowAgain.GetValueOrDefault();
                        taskCompletionSource.SetResult();
                    });
                    while (!taskCompletionSource.Task.IsCompleted)
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }
            }
            else
            {
                new object().Lock(syncLock =>
                {
                    this.app.SynchronizationContext.Post(async () =>
                    {
                        var window = this.app.LatestActiveMainWindow;
                        if (window != null)
                        {
                            var dialog = new ASControls.MessageDialog()
                            {
                                Buttons = (ASControls.MessageDialogButtons)buttons,
                                DoNotAskOrShowAgain = false,
                                Icon = (ASControls.MessageDialogIcon)icon,
                                Message = message,
                            };
                            result = (MessageDialogResult)await dialog.ShowDialog(window);
                            this.IsShowingMessageDialogAllowed = !dialog.DoNotAskOrShowAgain.GetValueOrDefault();
                        }
                        lock (syncLock)
                            Monitor.Pulse(syncLock);
                    });
                    Monitor.Wait(syncLock);
                });
            }
            return result;
        }
        finally
        {
            waitingCount = Interlocked.Decrement(ref this.userInputWaitingCount);
            if (waitingCount == 0)
                this.IsWaitingForUserInputChanged?.Invoke(false);
        }
    }


    /// <inheritdoc/>
    public string? ShowTextInputDialog(string? message, string? initialText)
    {
        if (!this.IsShowingTextInputDialogAllowed)
            return null;
        var waitingCount = Interlocked.Increment(ref this.userInputWaitingCount);
        if (waitingCount == 1)
            this.IsWaitingForUserInputChanged?.Invoke(true);
        try
        {
            var result = (string?)null;
            if (this.app.CheckAccess())
            {
                var window = this.app.LatestActiveMainWindow;
                if (window != null)
                {
                    var taskCompletionSource = new TaskCompletionSource();
                    this.app.SynchronizationContext.Post(async () =>
                    {
                        var dialog = new ASControls.TextInputDialog()
                        {
                            DoNotShowAgain = false,
                            InitialText = initialText,
                            Message = message,
                        };
                        result = await dialog.ShowDialog(window);
                        this.IsShowingTextInputDialogAllowed = !dialog.DoNotShowAgain.GetValueOrDefault();
                        taskCompletionSource.SetResult();
                    });
                    while (!taskCompletionSource.Task.IsCompleted)
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                }
            }
            else
            {
                new object().Lock(syncLock =>
                {
                    this.app.SynchronizationContext.Post(async () =>
                    {
                        var window = this.app.LatestActiveMainWindow;
                        if (window != null)
                        {
                            var dialog = new ASControls.TextInputDialog()
                            {
                                DoNotShowAgain = false,
                                InitialText = initialText,
                                Message = message,
                            };
                            result = await dialog.ShowDialog(window);
                            this.IsShowingTextInputDialogAllowed = !dialog.DoNotShowAgain.GetValueOrDefault();
                        }
                        lock (syncLock)
                            Monitor.Pulse(syncLock);
                    });
                    Monitor.Wait(syncLock);
                });
            }
            return result;
        }
        finally
        {
            waitingCount = Interlocked.Decrement(ref this.userInputWaitingCount);
            if (waitingCount == 0)
                this.IsWaitingForUserInputChanged?.Invoke(false);
        }
    }
}