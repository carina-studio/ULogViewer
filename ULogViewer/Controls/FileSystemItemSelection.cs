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
    /// <returns>Task of selecting a file.</returns>
    public static async Task<string?> SelectFileToExportLogAnalysisRuleSetAsync(Window window)
    {
        return (await window.StorageProvider.SaveFilePickerAsync(new ()
        {
            DefaultExtension = ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(IAvaloniaApplication.CurrentOrNull?.GetStringNonNull("FileFormat.Json", "JSON"))
                {
                    Patterns = new[] { "*.json" }
                }
            }
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
    /// Let user select a file to export log profile.
    /// </summary>
    /// <param name="window">Window.</param>
    /// <returns>Task of selecting a file.</returns>
    public static async Task<string?> SelectFileToExportLogProfileAsync(Window window)
    {
        return (await window.StorageProvider.SaveFilePickerAsync(new()
        {
            DefaultExtension = ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(IAvaloniaApplication.CurrentOrNull?.GetStringNonNull("FileFormat.Json", "JSON"))
                {
                    Patterns = new[] { "*.json" }
                }
            }
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
}