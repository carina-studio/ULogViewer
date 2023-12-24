using CarinaStudio.AppSuite;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer;

class QuickGuideDocumentSource : DocumentSource
{
    // Constructor.
    public QuickGuideDocumentSource(IAppSuiteApplication app) : base(app)
    { }


    /// <inheritdoc/>
    public override IList<ApplicationCulture> SupportedCultures { get; } = new[]
    {
        ApplicationCulture.EN_US,
        ApplicationCulture.ZH_CN,
        ApplicationCulture.ZH_TW,
    };


    /// <inheritdoc/>
    public override Uri Uri => this.Culture switch
    {
        ApplicationCulture.ZH_CN => new("avares://ULogViewer/QuickGuide-zh-CN.md"),
        ApplicationCulture.ZH_TW => new("avares://ULogViewer/QuickGuide-zh-TW.md"),
        _ => new("avares://ULogViewer/QuickGuide.md"),
    };
}