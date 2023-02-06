using CarinaStudio.Collections;
using CarinaStudio.ULogViewer.ViewModels;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Testing.Sessions;

// Test for memory leakage of Session.
class SessionLeakageTest : BaseTest
{
    // Fields.
    readonly List<Session> createdSessions = new();


    // Constructor.
    public SessionLeakageTest(IULogViewerApplication app) : base(app, "Session Leakage")
    { }


    /// <inheritdoc/>
    protected override async Task OnRunAsync(CancellationToken cancellationToken)
    {
        // create sessions
        var sessionRefs = new List<WeakReference<Session>>();
        var logProfile = Logs.Profiles.LogProfileManager.Default.Profiles.First(it => it.Id == "ULogViewerMemoryLog");
        var randon = new Random();
        for (var i = 0; i < 10; ++i)
        {
            var session = this.Workspace.CreateAndAttachSession(logProfile);
            this.createdSessions.Add(session);
            sessionRefs.Add(new(session));
        }

        // close sessions
        while (this.createdSessions.IsNotEmpty())
        {
            var session = this.createdSessions.SelectRandomElement();
            await Task.Delay(randon.Next(100, 301), cancellationToken);
            this.Workspace.DetachAndCloseSession(session);
            this.createdSessions.Remove(session);
        }

        // check memory leakage
        await WaitForConditionAsync(() =>
        {
            this.Application.PerformGC(GCCollectionMode.Forced);
            for (var i = sessionRefs.Count - 1; i >= 0; --i)
            {
                if (!sessionRefs[i].TryGetTarget(out var _))
                    sessionRefs.RemoveAt(i);
            }
            return sessionRefs.IsEmpty();
        }, $"{sessionRefs.Count} session instances are still remain.", cancellationToken);
    }


    /// <inheritdoc/>
    protected override Task OnTearDownAsync()
    {
        // close remaining sessions
        if (this.createdSessions.IsNotEmpty())
        {
            foreach (var session in this.createdSessions)
                this.Workspace.DetachAndCloseSession(session);
            this.createdSessions.Clear();
        }

        // call base
        return base.OnTearDownAsync();
    }
}