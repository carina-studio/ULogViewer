using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels;

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
        this.Badges = isProVersion 
            ? [ this.Application.FindResourceOrDefault<IImage?>("Image/Icon.Professional").AsNonNull() ]
            : [ ];
    }


    // Badges.
    public override IList<IImage> Badges { get; }


    // URI of GitHub project.
    public override Uri GitHubProjectUri => new("https://github.com/carina-studio/ULogViewer");
    

    // URI of website.
    public override Uri WebsiteUri => new("https://carinastudio.azurewebsites.net/ULogViewer/");
}