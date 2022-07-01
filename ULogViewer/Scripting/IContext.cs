using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.Scripting;

/// <summary>
/// Context for running script.
/// </summary>
public interface IContext
{
    /// <summary>
    /// Get data for running script.
    /// </summary>
    IDictionary<string, object> Data { get; }


    /// <summary>
    /// Get logger.
    /// </summary>
    ILogger Logger { get; }
}