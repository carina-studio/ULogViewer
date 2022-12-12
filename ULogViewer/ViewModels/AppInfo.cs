using Avalonia.Media;
using CarinaStudio.Controls;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels
{
    /// <summary>
    /// Application information.
    /// </summary>
    class AppInfo : AppSuite.ViewModels.ApplicationInfo
    {
        // Constructor.
        public AppInfo()
        {
            var isProVersion = this.Application.ProductManager.Let(it =>
                !it.IsMock && it.IsProductActivated(ULogViewer.Products.Professional));
            if (isProVersion)
            {
                this.Application.TryGetResource<IImage>("Image/Icon.Professional", out var icon);
                this.Badges = new IImage[] { icon.AsNonNull() };
            }
            else
                this.Badges = Array.Empty<IImage>();
        }


        // Badges.
        public override IList<IImage> Badges { get; }


        // URI of GitHub project.
        public override Uri? GitHubProjectUri => new("https://github.com/carina-studio/ULogViewer");


        // URI of website.
        public override Uri? WebsiteUri =>
            new($"https://carinastudio.azurewebsites.net/ULogViewer/");
    }
}
