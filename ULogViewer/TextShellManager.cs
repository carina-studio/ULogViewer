using Avalonia;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Manager for installed text shell.
/// </summary>
class TextShellManager
{
    // Constants.
    const int RefreshInstalledTextShellsDelay = 1000;


    // Static fields.
    static TextShellManager? DefaultInstance;


    // Fields.
    readonly IULogViewerApplication app;
    Avalonia.Controls.Window? attachedActiveWindow;
    readonly Task initTask;
    readonly SortedObservableList<TextShell> installedTextShells = new((l, r) => (int)l - (int)r);
    readonly IObserver<bool> isWindowActiveObserver;
    IDisposable? isWindowActiveObserverToken;
    readonly ILogger logger;
    readonly ScheduledAction refreshInstalledTextShellsAction;
    IDictionary<TextShell, string> textShellExePaths = new Dictionary<TextShell, string>();


    // Constructor.
    TextShellManager(IULogViewerApplication app)
    {
        // setup fields and properties
        this.app = app;
        this.InstalledTextShells = ListExtensions.AsReadOnly(this.installedTextShells);
        this.logger = app.LoggerFactory.CreateLogger(nameof(TextShellManager));

        // setup actions
        this.isWindowActiveObserver = new Observer<bool>(isActive =>
        {
            if (isActive)
                this.refreshInstalledTextShellsAction!.Cancel();
            else
                this.refreshInstalledTextShellsAction!.Schedule(RefreshInstalledTextShellsDelay);
        });
        this.refreshInstalledTextShellsAction = new(() => 
        {
            if (this.app.LatestActiveWindow?.IsActive != true)
                _ = this.UpdateInstalledTextShells(false);
        });

        // attach to application
        app.PropertyChanged += (sender, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(IULogViewerApplication.IsShutdownStarted):
                    this.refreshInstalledTextShellsAction.Cancel();
                    this.isWindowActiveObserverToken?.Dispose();
                    break;
                case nameof(IULogViewerApplication.LatestActiveWindow):
                    if (this.attachedActiveWindow != null)
                        this.isWindowActiveObserverToken = this.isWindowActiveObserverToken.DisposeAndReturnNull();
                    this.attachedActiveWindow = this.app.LatestActiveWindow;
                    if (this.attachedActiveWindow != null)
                        this.isWindowActiveObserverToken = this.attachedActiveWindow.GetObservable(Avalonia.Controls.Window.IsActiveProperty).Subscribe(this.isWindowActiveObserver);
                    else
                        this.refreshInstalledTextShellsAction.Schedule(RefreshInstalledTextShellsDelay);
                    break;
            }
        };

        // setup installed text shells
        this.initTask = this.UpdateInstalledTextShells(true);
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static TextShellManager Default { get => DefaultInstance ?? throw new InvalidOperationException(); }


    // Get all installed text shells and its path.
    Dictionary<TextShell, string> GetInstalledTextShells()
    {
        var shellMap = new Dictionary<TextShell, string>();
        if (Platform.IsWindows)
        {
            // Cmd
            shellMap[TextShell.CommandPrompt] = "cmd";

            // PowerShell
#pragma warning disable CA1416
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\PowerShell\\1\\ShellIds\\Microsoft.PowerShell");
                if (key != null)
                {
                    key.GetValue("Path")?.ToString()?.Let(path =>
                    {
                        shellMap[TextShell.PowerShell] = path;
                    });
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking existence of PowerShell");
            }
#pragma warning restore CA1416
        }
        else
        {
            // sh
            try
            {
                if (File.Exists("/bin/sh"))
                    shellMap[TextShell.BourneShell] = "/bin/sh";
                else if (File.Exists("/usr/bin/sh"))
                    shellMap[TextShell.BourneShell] = "/usr/bin/sh";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking existence of sh");
            }

            // bash
            try
            {
                if (File.Exists("/bin/bash"))
                    shellMap[TextShell.BourneAgainShell] = "/bin/bash";
                else if (File.Exists("/usr/bin/bash"))
                    shellMap[TextShell.BourneShell] = "/usr/bin/bash";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking existence of bash");
            }

            // csh
            try
            {
                if (File.Exists("/bin/csh"))
                    shellMap[TextShell.CShell] = "/bin/csh";
                else if (File.Exists("/usr/bin/csh"))
                    shellMap[TextShell.CShell] = "/usr/bin/csh";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking existence of csh");
            }

            // fish
            try
            {
                if (Platform.IsMacOS && File.Exists("/opt/homebrew/bin/fish"))
                    shellMap[TextShell.FriendlyInteractiveShell] = "/opt/homebrew/bin/fish";
                if (!shellMap.ContainsKey(TextShell.FriendlyInteractiveShell))
                {
                    if (File.Exists("/usr/bin/fish"))
                        shellMap[TextShell.FriendlyInteractiveShell] = "/usr/bin/fish";
                    if (File.Exists("/usr/local/bin/fish"))
                        shellMap[TextShell.FriendlyInteractiveShell] = "/usr/local/bin/fish";
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking existence of fish");
            }

            // zsh
            try
            {
                if (File.Exists("/bin/zsh"))
                    shellMap[TextShell.ZShell] = "/bin/zsh";
                else if (File.Exists("/usr/bin/zsh"))
                    shellMap[TextShell.ZShell] = "/usr/bin/zsh";
                else if (File.Exists("/usr/local/bin/zsh"))
                    shellMap[TextShell.ZShell] = "/usr/local/bin/zsh";
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error occurred while checking existence of zsh");
            }

            // PowerShell
            //
        }
        return shellMap;
    }


    /// <summary>
    /// Initialize asynchronously.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    internal static async Task InitializeAsync(IULogViewerApplication app)
    {
        // check state
        if (DefaultInstance != null)
            throw new InvalidOperationException();

        // create instance.
        var defaultInstance = new TextShellManager(app);

        // wait for initialization
        await defaultInstance.initTask;
        foreach (var shell in defaultInstance.installedTextShells)
            defaultInstance.logger.LogDebug("'{shell}' found on system", shell);

        // complete
        DefaultInstance = defaultInstance;
    }


    /// <summary>
    /// Get list of text shells installed on system.
    /// </summary>
    public IList<TextShell> InstalledTextShells { get; }


    /// <summary>
    /// Refresh list of text shells installed on system asynchronously.
    /// </summary>
    /// <returns>Task of refreshing list.</returns>
    public Task RefreshInstalledTextShellsAsync()
    {
        this.refreshInstalledTextShellsAction.Cancel();
        return this.UpdateInstalledTextShells(false);
    }
    

    /// <summary>
    /// Try getting executable path of default text shell.
    /// </summary>
    /// <param name="path">Path of executable path of default text shell.</param>
    /// <returns>True if path got successfully.</returns>
    public bool TryGetDefaultTextShellPath([NotNullWhen(true)] out string? path) =>
        this.textShellExePaths.TryGetValue(this.app.Settings.GetValueOrDefault(SettingKeys.DefaultTextShell), out path);
    

    /// <summary>
    /// Try getting executable path of default text shell.
    /// </summary>
    /// <param name="defaultTextShell">Default text shell.</param>
    /// <param name="path">Path of executable path of default text shell.</param>
    /// <returns>True if path got successfully.</returns>
    public bool TryGetDefaultTextShellPath(out TextShell defaultTextShell, [NotNullWhen(true)] out string? path)
    {
        defaultTextShell = this.app.Settings.GetValueOrDefault(SettingKeys.DefaultTextShell);
        return this.textShellExePaths.TryGetValue(defaultTextShell, out path);
    }


    // Update list of text shells installed on system.
    async Task UpdateInstalledTextShells(bool isInit)
    {
        this.logger.LogTrace("Update installed text shells [start]");

        // update installed shells
        var shellMap = await Task.Run(() => this.GetInstalledTextShells());
        var installedTextShells = this.installedTextShells;
        for (var i = installedTextShells.Count - 1; i >= 0; --i)
        {
            var shell = installedTextShells[i];
            if (!shellMap.ContainsKey(shell))
            {
                this.logger.LogWarning("'{shell}' was removed from system", shell);
                installedTextShells.RemoveAt(i);
            }
        }
        foreach (var shell in shellMap.Keys)
        {
            if (!installedTextShells.Contains(shell))
            {
                if (!isInit)
                    this.logger.LogDebug("'{shell}' was added to system", shell);
                installedTextShells.Add(shell);
            }
        }
        this.textShellExePaths = shellMap;
        if (installedTextShells.IsEmpty())
            this.logger.LogError("No text shell installed on system");

        // update settings
        var settings = this.app.Settings;
        var defaultTextShell = settings.GetValueOrDefault(SettingKeys.DefaultTextShell);
        if (shellMap.ContainsKey(defaultTextShell))
        {
            if (isInit)
                this.logger.LogDebug("Default text shell is '{shell}'", defaultTextShell);
            else
                this.logger.LogTrace("Default text shell is '{shell}'", defaultTextShell);
        }
        else
        {
            // select another text shell
            var newShell = SettingKeys.DefaultTextShell.DefaultValue;
            if (!shellMap.ContainsKey(newShell))
            {
                if (installedTextShells.IsNotEmpty())
                    newShell = installedTextShells.First();
            }

            // update settings
            this.logger.LogWarning("Set default shell to '{newShell}'", newShell);
            settings.SetValue<TextShell>(SettingKeys.DefaultTextShell, newShell);
        }

        this.logger.LogTrace("Update installed text shells [end]");
    }
}