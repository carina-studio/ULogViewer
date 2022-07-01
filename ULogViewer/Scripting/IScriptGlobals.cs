namespace CarinaStudio.ULogViewer.Scripting;

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
    /// Get context.
    /// </summary>
    TContext Context { get; }
}