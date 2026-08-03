using CarinaStudio.AppSuite;
using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer;

class QuickStartGuideDocumentSource(IAppSuiteApplication app) : DocumentSource(app)
{
    /// <inheritdoc/>
    public override IList<ApplicationCulture> SupportedCultures { get; } =
    [
        ApplicationCulture.EN_US,
        ApplicationCulture.JA_JP,
        ApplicationCulture.ZH_CN,
        ApplicationCulture.ZH_TW,
    ];


    /// <inheritdoc/>
    public override Uri Uri => this.Culture switch
    {
        ApplicationCulture.JA_JP => new("avares://ULogViewer/Resources/QuickStartGuide/QuickStartGuide-ja-JP.md"),
        ApplicationCulture.ZH_CN => new("avares://ULogViewer/Resources/QuickStartGuide/QuickStartGuide-zh-CN.md"),
        ApplicationCulture.ZH_TW => new("avares://ULogViewer/Resources/QuickStartGuide/QuickStartGuide-zh-TW.md"),
        _ => new("avares://ULogViewer/Resources/QuickStartGuide/QuickStartGuide.md"),
    };
}