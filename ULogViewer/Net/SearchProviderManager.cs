using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CarinaStudio.ULogViewer.Net;

/// <summary>
/// Manager of <see cref="SearchProvider"/>s.
/// </summary>
class SearchProviderManager : BaseApplicationObject<IULogViewerApplication>, INotifyPropertyChanged
{
    // Static fields.
    static SearchProviderManager? _default;


    // Constructor.
    SearchProviderManager(IULogViewerApplication app) : base(app)
    {
        this.Providers = ListExtensions.AsReadOnly(new SearchProvider[]
        {
            new AppleDeveloperSearchProvider(app),
            new AppleDeveloperForumsSearchProvider(app),
            new BaiduSearchProvider(app),
            new BingSearchProvider(app),
            new GitHubIssuesSearchProvider(app),
            new GoogleSearchProvider(app),
            new GoogleDevelopersSearchProvider(app),
            new MicrosoftForumsSearchProvider(app),
            new MicrosoftLearnSearchProvider(app),
            new StackExchangeSearchProvider(app),
            new StackOverflowSearchProvider(app),
            new ZhihuSearchProvider(app),
        });
        this.UpdateDefaultProvider(this.Settings.GetValueOrDefault(SettingKeys.DefaultSearchProvider), false);
        this.Settings.SettingChanged += (_, e) =>
        {
            if (e.Key == SettingKeys.DefaultSearchProvider)
                this.UpdateDefaultProvider((string)e.Value, true);
        };
    }


    /// <summary>
    /// Get default instance.
    /// </summary>
    public static SearchProviderManager Default => _default ?? throw new InvalidOperationException();


    /// <summary>
    /// Get default provider.
    /// </summary>
    public SearchProvider DefaultProvider { get; private set; }


    // Initialize.
    internal static void Initialize(IULogViewerApplication app)
    {
        if (_default != null)
            throw new InvalidOperationException();
        app.VerifyAccess();
        _default = new(app);
    }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    /// <summary>
    /// Get list of providers.
    /// </summary>
    public IList<SearchProvider> Providers { get; }


    // Update default provider.
    [MemberNotNull(nameof(DefaultProvider))]
    void UpdateDefaultProvider(string id, bool notifyChanged)
    {
        var provider = string.IsNullOrEmpty(id) ? null : this.Providers.FirstOrDefault(it => it.Id == id);
        provider ??= this.Providers.First(it => it is GoogleSearchProvider);
        if (this.DefaultProvider != provider)
        {
            this.DefaultProvider = provider;
            if (notifyChanged)
                this.PropertyChanged?.Invoke(this, new(nameof(DefaultProvider)));
        }
    }
}