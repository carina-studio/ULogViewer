namespace CarinaStudio.ULogViewer.Net;

class MicrosoftLearnSearchProvider : SimpleSearchProvider
{
    public MicrosoftLearnSearchProvider(IULogViewerApplication app) : base(app, "MicrosoftLearn", "https://learn.microsoft.com/search/?terms=", "%20")
    { }
}