using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;

namespace CarinaStudio.ULogViewer.ViewModels
{
    /// <summary>
    /// Application information.
    /// </summary>
    class AppInfo : AppSuite.ViewModels.ApplicationInfo
    {
        // Icon.
        public override IBitmap Icon => AvaloniaLocator.Current.GetService<IAssetLoader>().Let(it =>
        {
            return it.Open(new Uri("avares://ULogViewer/AppIcon.ico")).Use(stream => new Bitmap(stream));
        });


        // URI of GitHub project.
        public override Uri? GitHubProjectUri => new Uri("https://github.com/carina-studio/ULogViewer");
    }
}
