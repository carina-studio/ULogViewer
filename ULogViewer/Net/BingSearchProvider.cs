namespace CarinaStudio.ULogViewer.Net;

class BingSearchProvider : SimpleSearchProvider
{
    public BingSearchProvider(IULogViewerApplication app) : base(app, "Bing", "https://www.bing.com/search?q=")
    { }
}