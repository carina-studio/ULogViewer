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
                this.Badges = new IImage[0];
        }


        // Badges.
        public override IList<IImage> Badges { get; }


        // URI of GitHub project.
        public override Uri? GitHubProjectUri => new Uri("https://github.com/carina-studio/ULogViewer");


        // URI of privacy policy.
        public override Uri? PrivacyPolicyUri => this.Application.CultureInfo.ToString() switch
        {
            "zh-TW" => new Uri("https://carina-studio.github.io/ULogViewer/privacy_policy_zh-TW.html"),
            _ => new Uri("https://carina-studio.github.io/ULogViewer/privacy_policy.html"),
        };


        // URI of user agreement.
        public override Uri? UserAgreementUri => this.Application.CultureInfo.ToString() switch
        {
            "zh-TW" => new Uri("https://carina-studio.github.io/ULogViewer/user_agreement_zh-TW.html"),
            _ => new Uri("https://carina-studio.github.io/ULogViewer/user_agreement.html"),
        };
    }
}
