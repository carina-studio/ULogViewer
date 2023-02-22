namespace CarinaStudio.ULogViewer.Net;

class StackOverflowSearchProvider : SimpleSearchProvider
{
    public StackOverflowSearchProvider(IULogViewerApplication app) : base(app, "StackOverflow", "https://stackoverflow.com/search?q=")
    { }
}