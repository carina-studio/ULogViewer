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
        public override Uri? PrivacyPolicyUri => this.Application.PrivacyPolicyVersion?.Let(it =>
            new Uri($"https://carinastudio.azurewebsites.net/Documents/ULogViewer/PrivacyPolicy?version={it.Major}.{it.Minor}"));


        // URI of user agreement.
        public override Uri? UserAgreementUri => this.Application.UserAgreementVersion?.Let(it =>
            new Uri($"https://carinastudio.azurewebsites.net/Documents/ULogViewer/UserAgreement?version={it.Major}.{it.Minor}"));
    }
}
