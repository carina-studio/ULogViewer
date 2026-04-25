using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CarinaStudio.AppSuite;
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
        var baseUri = $"avares://{this.Application.Assembly.GetName().Name}";
        this.Badges = isProVersion 
            ? [ this.Application.FindResourceOrDefault<IImage?>("Image/Icon.Professional").AsNonNull() ]
            : [ ];
        using var bannerImageStream = this.Application.EffectiveThemeMode == ThemeMode.Dark
            ? AssetLoader.Open(new($"{baseUri}/AppInfoBanner-Dark.png"))
            : AssetLoader.Open(new($"{baseUri}/AppInfoBanner-Light.png"));
        this.BannerImage = new Bitmap(bannerImageStream);
    }


    /// <inheritdoc/>
    public override IList<IImage> Badges { get; }


    /// <inheritdoc/>
    public override IImage? BannerImage { get; }


    /// <inheritdoc/>
    public override Uri GitHubProjectUri => new("https://github.com/carina-studio/ULogViewer");

    
    /// <inheritdoc/>
    public override Uri WebsiteUri => new("https://carinastudio.azurewebsites.net/ULogViewer/");
}