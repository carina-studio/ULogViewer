using CarinaStudio.AppSuite.ViewModels;
using System;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Application update view-model.
/// </summary>
class AppUpdater : ApplicationUpdater
{
    /// <inheritdoc/>
    protected override bool OnCheckAutoUpdateSupport(Version version)
    {
        if (Platform.IsMacOS && version.Major >= 5)
            return false;
        return base.OnCheckAutoUpdateSupport(version);
    }
}