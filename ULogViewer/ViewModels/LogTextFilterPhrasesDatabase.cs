using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Database of log text filter phrases.
/// </summary>
static class LogTextFilterPhrasesDatabase
{
    /// <summary>
    /// Clear database.
    /// </summary>
    /// <returns>Task of clearing database.</returns>
    public static Task ClearAsync()
    {
        return Task.CompletedTask;
    }
    
    
    /// <summary>
    /// Close database.
    /// </summary>
    /// <returns>Task of closing database.</returns>
    public static Task CloseAsync()
    {
        return Task.CompletedTask;
    }
    
    
    /// <summary>
    /// Initialize.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <returns>Task of initialization.</returns>
    public static Task InitializeAsync(IULogViewerApplication app)
    {
        return Task.CompletedTask;
    }


    /// <summary>
    /// Select candidate phrases for text filter input.
    /// </summary>
    /// <param name="prefix">Prefix of phrase.</param>
    /// <param name="postfix">Post of phrase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of candidate phrases selection.</returns>
    public static Task<IList<string>> SelectCandidatePhrasesAsync(string prefix, string? postfix, CancellationToken cancellationToken)
    {
        return Task.FromResult<IList<string>>(Array.Empty<string>());
    }


    /// <summary>
    /// Update phrases in database by given regular expression.
    /// </summary>
    /// <param name="regex">Regular expression.</param>
    /// <returns>Task of updating phrases.</returns>
    public static Task UpdatePhrasesAsync(Regex regex)
    {
        return Task.CompletedTask;
    }
}