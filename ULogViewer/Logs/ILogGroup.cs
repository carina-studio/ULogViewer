using System;
using CarinaStudio.Threading;

namespace CarinaStudio.ULogViewer.Logs;

/// <summary>
/// Group of logs.
/// </summary>
public interface ILogGroup: IThreadDependent
{
    /// <summary>
    /// Schedule to trigger progressive logs removing.
    /// </summary>
    /// <param name="triggerAction">Function to be called to trigger logs removing.</param>
    /// <returns><see cref="IDisposable"/> represents scheduled or on going progressive logs removing.</returns>
    IDisposable ScheduleProgressiveLogsRemoving(Func<bool> triggerAction);
}