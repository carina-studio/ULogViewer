using CarinaStudio.Configuration;
using System;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Configuration keys.
/// </summary>
sealed class ConfigurationKeys
{
    /// <summary>
    /// Interval between each displayable log chunk filtering in milliseconds.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkFilteringPaddingInterval = new(nameof(DisplayableLogChunkFilteringPaddingInterval), 50);
    /// <summary>
    /// Size of chunk of displayable log filtering.
    /// </summary>
    public static readonly SettingKey<int> DisplayableLogChunkFilteringSize = new(nameof(DisplayableLogChunkFilteringSize), 16384);


    // Constructor.
    ConfigurationKeys() 
    { }
}