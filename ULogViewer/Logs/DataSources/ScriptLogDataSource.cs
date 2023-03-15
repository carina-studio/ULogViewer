using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Scripting;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Script-based implementation of <see cref="ILogDataSource"/>.
/// </summary>
class ScriptLogDataSource : BaseLogDataSource, IScriptRunningHost
{
    // Constructor.
    internal ScriptLogDataSource(ScriptLogDataSourceProvider provider, LogDataSourceOptions options) : base(provider, options)
    { }


    /// <inheritdoc/>
    IAppSuiteApplication IApplicationObject<IAppSuiteApplication>.Application => this.Application;


    /// <inheritdoc/>
    bool IScriptRunningHost.IsRunningScripts => this.State switch
    {
        LogDataSourceState.OpeningReader
        or LogDataSourceState.ReaderOpened
        or LogDataSourceState.ClosingReader => true,
        _ => false,
    };


    /// <inheritdoc/>
    protected override void OnReaderClosed()
    {
        base.OnReaderClosed();
    }


    /// <inheritdoc/>
    protected override Task<(LogDataSourceState, TextReader?)> OpenReaderCoreAsync(CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }


    /// <inheritdoc/>
    protected override Task<LogDataSourceState> PrepareCoreAsync(CancellationToken cancellationToken)
    {
        throw new System.NotImplementedException();
    }


    /// <inheritdoc/>
    public event EventHandler<ScriptRuntimeErrorEventArgs>? ScriptRuntimeErrorOccurred;
}


/// <summary>
/// <see cref="IContext"/> for log data source script.
/// </summary>
public interface ILogDataSourceScriptContext : IUserInteractiveContext
{
    /// <summary>
    /// Get options of data source.
    /// </summary>
    LogDataSourceOptions Options { get; }
}