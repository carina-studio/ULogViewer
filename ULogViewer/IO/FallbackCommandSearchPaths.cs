using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace CarinaStudio.ULogViewer.IO;

/// <summary>
/// Predefined fall-back paths to search command.
/// </summary>
static class FallbackCommandSearchPaths
{
    /// <summary>
    /// Fall-back paths to search Android SDK platform tools.
    /// </summary>
    public static readonly ISet<string> AndroidSdkPlatformTools = Global.Run(() =>
    {
        if (Platform.IsWindows)
        {
            var userDirPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return ImmutableHashSet.Create(Path.Combine(userDirPath, "Android\\Sdk\\platform-tools"));
        }
        if (Platform.IsMacOS)
        {
            var userDirPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return ImmutableHashSet.Create(Path.Combine(userDirPath, "Library/Android/sdk/platform-tools"));
        }
        return ImmutableHashSet<string>.Empty;
    });
    
    
    /// <summary>
    /// Fall-back paths to search git.
    /// </summary>
    public static readonly ISet<string> Git = Global.Run(() =>
    {
        if (Platform.IsWindows)
        {
            var programDirPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return ImmutableHashSet.Create(Path.Combine(programDirPath, "Git\\cmd"));
        }
        return ImmutableHashSet<string>.Empty;
    });
}