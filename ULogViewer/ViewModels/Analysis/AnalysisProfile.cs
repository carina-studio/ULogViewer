using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Profile of log analysis.
/// </summary>
abstract class AnalysisProfile : BaseApplicationObject<IULogViewerApplication>, INotifyPropertyChanged
{
    // Static fields.
    
    static readonly Dictionary<string, AnalysisProfile> profiles = new();
    static readonly Random random = new();


    /// <summary>
    /// Initialize new <see cref="AnalysisProfile"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="id">Unique ID.</param>
    /// <param name="isBuiltIn">Is built-in or not.</param>
    protected AnalysisProfile(IULogViewerApplication app, string id, bool isBuiltIn) : base(app)
    { 
        profiles.Add(id, this);
        this.Id = id;
        this.IsBuiltIn = isBuiltIn;
    }


    /// <summary>
    /// Generate random unique ID.
    /// </summary>
    /// <returns>Generated ID.</returns>
    public static string GenerateId()
    {
        var idBuffer = new char[8];
        while (true)
        {
            for (var i = idBuffer.Length - 1; i >= 0; --i)
            {
                var n = random.Next(36);
                idBuffer[i] = n <= 9 ? (char)('0' + n) : (char)('a' + (n - 10));
            }
            var id = new string(idBuffer);
            if (!profiles.ContainsKey(id))
                return id;
        }
    }


    /// <summary>
    /// Get unique ID of profile.
    /// </summary>
    public string Id { get; }


    /// <summary>
    /// Check whether profile is built-in or not.
    /// </summary>
    public bool IsBuiltIn { get; }


    /// <summary>
    /// Raise <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="propertyName">Name of property.</param>
    protected void OnPropertyChanged(string propertyName) =>
        this.PropertyChanged?.Invoke(this, new(propertyName));


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;
}