using System;

namespace CarinaStudio.ULogViewer.Logs.DataSources;

/// <summary>
/// Provider of <see cref="ScriptLogDataSource"/> which is embedded in log profile.
/// </summary>
class EmbeddedScriptLogDataSourceProvider : ScriptLogDataSourceProvider, ILogDataSourceProvider
{
    // Fields.
    // ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
    readonly EventHandler appStringsUpdatedHandler; // Need to keep as strong reference
    // ReSharper restore PrivateFieldCanBeConvertedToLocalVariable
    readonly IDisposable appStringsUpdatedHandlerToken;


    /// <summary>
    /// Initialize new <see cref="EmbeddedScriptLogDataSourceProvider"/> instance.
    /// </summary>
    /// <param name="template">Template provider.</param>
    public EmbeddedScriptLogDataSourceProvider(ScriptLogDataSourceProvider template) : base(template, "")
    { 
        this.appStringsUpdatedHandler = (_, _) => 
            this.OnPropertyChanged(nameof(DisplayName));
        this.appStringsUpdatedHandlerToken = this.Application.AddWeakEventHandler(nameof(IApplication.StringsUpdated), this.appStringsUpdatedHandler);
    }


    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.appStringsUpdatedHandlerToken.Dispose();
        base.Dispose(disposing);
    }


    /// <summary>
    /// Get or set display name of provider.
    /// </summary>
    public new string DisplayName
    {
        get => this.Application.GetStringNonNull("EmbeddedScriptLogDataSourceProvider.DisplayName");
        set => throw new InvalidOperationException();
    }


    /// <inheritdoc/>
    string ILogDataSourceProvider.DisplayName => this.DisplayName;
}
