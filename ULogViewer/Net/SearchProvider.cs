using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace CarinaStudio.ULogViewer.Net;

/// <summary>
/// Provider of searching on internet.
/// </summary>
abstract class SearchProvider : BaseApplicationObject<IULogViewerApplication>, INotifyPropertyChanged
{
    // Fields.
    string? name;


    /// <summary>
    /// Initialize new <see cref="SearchProvider"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="id">Unique ID of provider.</param>
    protected SearchProvider(IULogViewerApplication app, string id) : base(app)
    {
        this.Id = id;
        app.StringsUpdated += (_, _) =>
        {
            if (this.name != null)
            {
                this.name = null;
                this.PropertyChanged?.Invoke(this, new(nameof(Name)));
            }
        };
    }


    /// <summary>
    /// Get unique ID of provider.
    /// </summary>
    public string Id { get; }


    /// <summary>
    /// Get name of provider.
    /// </summary>
    public string Name
    {
        get
        {
            this.name ??= this.Application.GetStringNonNull($"SearchProvider.{this.Id}", this.Id);
            return this.name;
        }
    }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <inheritdoc/>
    public override string ToString() =>
        this.Name;


    /// <summary>
    /// Try creating URI for searching.
    /// </summary>
    /// <param name="keywords">List of keywords.</param>
    /// <param name="uri">Generated URI.</param>
    /// <returns>True if URI has been created successfully.</returns>
    public abstract bool TryCreateSearchUri(IList<string> keywords, [NotNullWhen(true)] out Uri? uri);
}