using CarinaStudio.Logging;
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
    /// Clear phrases from database.
    /// </summary>
    /// <returns>Task of clearing database. The result if number of phrases cleared from database.</returns>
    public static Task<int> ClearAsync()
    {
        return Task.FromResult(1);
    }


    /// <summary>
    /// Raised before start clearing database.
    /// </summary>
    public static event EventHandler? Clearing;
    
    
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
    /// Check whether database is a newly created one or not.
    /// </summary>
    public static bool IsNewlyCreated { get; private set; }


    /// <summary>
    /// Select candidate phrases for text filter input.
    /// </summary>
    /// <param name="prefix">Prefix of phrase.</param>
    /// <param name="postfix">Post of phrase.</param>
    /// <param name="ignoreCase">True to select phrases with case insensitive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of candidate phrases selection.</returns>
    public static Task<IList<string>> SelectCandidatePhrasesAsync(string prefix, string? postfix, bool ignoreCase, CancellationToken cancellationToken)
    {
        return Task.FromResult<IList<string>>(Array.Empty<string>());
    }


    /// <summary>
    /// Update phrases in database by given regular expression.
    /// </summary>
    /// <param name="regex">Regular expression.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of updating phrases.</returns>
    public static Task UpdatePhrasesAsync(Regex regex, CancellationToken cancellationToken) =>
        UpdatePhrasesAsync(regex.ToString(), cancellationToken);


    /// <summary>
    /// Update phrases in database by given regular expression.
    /// </summary>
    /// <param name="pattern">Pattern of log text filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task of updating phrases.</returns>
    public static Task UpdatePhrasesAsync(string pattern, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}