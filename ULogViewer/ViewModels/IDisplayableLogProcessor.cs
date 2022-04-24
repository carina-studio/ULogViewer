using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Processor of list of <see cref="DisplayableLog"/>.
/// </summary>
interface IDisplayableLogProcessor : IApplicationObject<IULogViewerApplication>, IDisposable, INotifyPropertyChanged
{
    /// <summary>
    /// Notify that given log was updated and should be processed again.
    /// </summary>
    /// <param name="log">Log to be processed again.</param>
    void InvalidateLog(DisplayableLog log);


    /// <summary>
    /// Notify that given logs were updated and should be processed again.
    /// </summary>
    /// <param name="logs">Logs to be processed again.</param>
    void InvalidateLogs(IEnumerable<DisplayableLog> logs);


    /// <summary>
    /// Check whether logs processing is on-going or not.
    /// </summary>
    bool IsProcessing { get; }


    /// <summary>
    /// Check whether logs processing is actually needed or not.
    /// </summary>
    bool IsProcessingNeeded { get; }


    /// <summary>
    /// Get size of memory currently used by the instance directly in bytes.
    /// </summary>
    long MemorySize { get; }


    /// <summary>
    /// Get priority of logs processing.
    /// </summary>
    DisplayableLogProcessingPriority ProcessingPriority { get; }


    /// <summary>
    /// Get current progress of processing.
    /// </summary>
    double Progress { get; }


    /// <summary>
    /// Get source list of <see cref="DisplayableLog"/> to be processed.
    /// </summary>
    IList<DisplayableLog> SourceLogs { get; }
}


/// <summary>
/// Priority of processing <see cref="DisplayableLog"/>.
/// </summary>
enum DisplayableLogProcessingPriority 
{
    /// <summary>
    /// Realtime.
    /// </summary>
    Realtime,
    /// <summary>
    /// Default.
    /// </summary>
    Default,
    /// <summary>
    /// Background.
    /// </summary>
    Background,
}