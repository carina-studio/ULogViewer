namespace CarinaStudio.ULogViewer.Net;

class GoogleSearchProvider : SimpleSearchProvider
{
    public GoogleSearchProvider(IULogViewerApplication app) : base(app, "Google", "https://www.google.com/search?q=")
    { }
}