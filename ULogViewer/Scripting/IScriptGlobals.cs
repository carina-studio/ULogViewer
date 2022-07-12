namespace CarinaStudio.ULogViewer.Scripting;
using System.Threading;

/// <summary>
/// Interface of global environment for script.
/// </summary>
public interface IScriptGlobals<TContext> where TContext : IContext
{
    /// <summary>
    /// Get application.
    /// </summary>
    IApplication App { get; }


    /// <summary>
    /// Get cancellation token of running script.
    /// </summary>
    CancellationToken CancellationToken { get; }


    /// <summary>
    /// Get context.
    /// </summary>
    TContext Context { get; }
}