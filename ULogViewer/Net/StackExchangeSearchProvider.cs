namespace CarinaStudio.ULogViewer.Net;

class StackExchangeSearchProvider : SimpleSearchProvider
{
    public StackExchangeSearchProvider(IULogViewerApplication app) : base(app, "StackExchange", "https://stackexchange.com/search?q=")
    { }
}