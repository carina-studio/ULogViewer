using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CarinaStudio.IO;
using System.IO;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Provide functions for selecting file or directory.
/// </summary>
public static class FileSystemItemSelection
{
    /// <summary>
    /// Let user select a file to export log analysis rule set.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static Task<string?> SelectFileToExportLogAnalysisRuleSetAsync(Window window, string? title = null)
    {
        title ??= IAvaloniaApplication.CurrentOrNull?.GetString("FileSystemItemSelection.ExportLogAnalysisRuleSet");
        return SelectJsonFileToSave(window, title);
    }
    
    
    /// <summary>
    /// Let user select a file to export log profile.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static Task<string?> SelectFileToExportLogProfileAsync(Window window, string? title = null)
    {
        title ??= IAvaloniaApplication.CurrentOrNull?.GetString("FileSystemItemSelection.ExportLogProfile");
        return SelectJsonFileToSave(window, title);
    }


    /// <summary>
    /// Let user select a file to export logs.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static async Task<string?> SelectFileToExportLogsAsync(Window window, string? title = null)
    {
        var app = IAvaloniaApplication.CurrentOrNull;
        title ??= app?.GetString("FileSystemItemSelection.ExportLogs");
        return (await window.StorageProvider.SaveFilePickerAsync(new()
        {
            DefaultExtension = ".txt",
            FileTypeChoices = new FilePickerFileType[]
            {
                new(app?.GetString("FileFormat.Text", "Text")) { Patterns = new[] { "*.txt" } },
                new(app?.GetString("FileFormat.Log", "Log")) { Patterns = new[] { "*.log" } },
                new(app?.GetString("FileFormat.Json", "Json")) { Patterns = new[] { "*.json" } },
                new(app?.GetString("FileFormat.All", "All files")) { Patterns = new[] { "*.*" } },
            },
            Title = title,
        }))?.Let(it => it.TryGetLocalPath());
    }
    
    
    /// <summary>
    /// Let user select a file to export log data source script.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static Task<string?> SelectFileToExportScriptLogDataSourceProviderAsync(Window window, string? title = null)
    {
        title ??= IAvaloniaApplication.CurrentOrNull?.GetString("FileSystemItemSelection.ExportScriptLogDataSourceProvider");
        return SelectJsonFileToSave(window, title);
    }


    /// <summary>
    /// Let user select a file to import log analysis rule set.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static Task<string?> SelectFileToImportLogAnalysisRuleSetAsync(Window window, string? title = null)
    {
        title ??= IAvaloniaApplication.CurrentOrNull?.GetString("FileSystemItemSelection.ImportLogAnalysisRuleSet");
        return SelectJsonFileToOpen(window, title);
    }
    
    
    /// <summary>
    /// Let user select a file to import log profile.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static Task<string?> SelectFileToImportLogProfileAsync(Window window, string? title = null)
    {
        title ??= IAvaloniaApplication.CurrentOrNull?.GetString("FileSystemItemSelection.ImportLogProfile");
        return SelectJsonFileToOpen(window, title);
    }
    
    
    /// <summary>
    /// Let user select a file to import log data source script.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="title">Title of dialog.</param>
    /// <returns>Task of selecting a file.</returns>
    public static Task<string?> SelectFileToImportScriptLogDataSourceScriptAsync(Window window, string? title = null)
    {
        title ??= IAvaloniaApplication.CurrentOrNull?.GetString("FileSystemItemSelection.ImportScriptLogDataSourceProvider");
        return SelectJsonFileToOpen(window, title);
    }
    
    
    // Select a .json file to open.
    static async Task<string?> SelectJsonFileToOpen(Window window, string? title)
    {
        return (await window.StorageProvider.OpenFilePickerAsync(new()
        {
            FileTypeFilter = new[]
            {
                new FilePickerFileType(IAvaloniaApplication.CurrentOrNull?.GetString("FileFormat.Json", "Json"))
                {
                    Patterns = new[] { "*.json" }
                }
            },
            Title = title,
        })).Let(it => it.Count == 1 ? it[0].TryGetLocalPath() : null);
    }
    
    
    // Select a .json file to save.
    static async Task<string?> SelectJsonFileToSave(Window window, string? title)
    {
        return (await window.StorageProvider.SaveFilePickerAsync(new()
        {
            DefaultExtension = ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(IAvaloniaApplication.CurrentOrNull?.GetString("FileFormat.Json", "Json"))
                {
                    Patterns = new[] { "*.json" }
                }
            },
            Title = title,
        }))?.Let(it =>
        {
            var path = it.TryGetLocalPath();
            if (string.IsNullOrEmpty(path))
                return null;
            if (!PathEqualityComparer.Default.Equals(Path.GetExtension(path), ".json"))
                path += ".json";
            return path;
        });
    }


    /// <summary>
    /// Let user select a working directory.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <param name="initDirectory">Path to initial working directory.</param>
    /// <returns>Task of selecting a directory.</returns>
    public static async Task<string?> SelectWorkingDirectory(Window window, string? initDirectory = null)
    {
        var app = IAvaloniaApplication.CurrentOrNull;
        var options = await new FolderPickerOpenOptions().AlsoAsync(async options =>
        {
            if (!string.IsNullOrEmpty(initDirectory) 
                && initDirectory.IsValidFilePath() 
                && await CarinaStudio.IO.Directory.ExistsAsync(initDirectory))
            {
                options.SuggestedStartLocation = await window.StorageProvider.TryGetFolderFromPathAsync(initDirectory);
            }
            options.Title = app?.GetString("FileSystemItemSelection.SelectWorkingDirectory");
        });
        return (await window.StorageProvider.OpenFolderPickerAsync(options)).Let(it => 
            it.Count == 1 ? it[0].TryGetLocalPath() : null);
    }
}