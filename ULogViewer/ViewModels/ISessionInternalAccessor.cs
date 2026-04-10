using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Interface to access internal state of <see cref="Session"/>.
/// </summary>
interface ISessionInternalAccessor
{
    /// <summary>
    /// Get group of displayable logs.
    /// </summary>
    DisplayableLogGroup? DisplayableLogGroup { get; }


    /// <summary>
    /// Raised when group of displayable log created.
    /// </summary>
    event EventHandler? DisplayableLogGroupCreated;


    /// <summary>
    /// Get memory usage policy.
    /// </summary>
    MemoryUsagePolicy MemoryUsagePolicy { get; }


    /// <summary>
    /// Prepare the properties for usage tracking.
    /// </summary>
    /// <returns>The properties for usage tracking.</returns>
    IDictionary<string, string> PrepareUsageTrackingProperties();
}