using System;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs;

/// <summary>
/// Group of logs.
/// </summary>
interface ILogGroup : IThreadDependent
{
    /// <summary>
    /// Schedule to trigger progressive logs removing.
    /// </summary>
    /// <param name="triggerAction">Function to be called to trigger logs removing.</param>
    /// <returns><see cref="IDisposable"/> represents scheduled or on going progressive logs removing.</returns>
    IDisposable ScheduleProgressiveLogsRemoving(Func<bool> triggerAction);
    
    
    /// <summary>
    /// Select proper <see cref="TaskFactory"/> for logs reading in background.
    /// </summary>
    /// <param name="source">Source to read logs.</param>
    TaskFactory SelectLogsReadingTaskFactory(ILogDataSource source);
}