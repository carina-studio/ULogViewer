namespace CarinaStudio.ULogViewer.Net;

class AppleDeveloperSearchProvider : SimpleSearchProvider
{
    public AppleDeveloperSearchProvider(IULogViewerApplication app) : base(app, "AppleDeveloper", "https://developer.apple.com/search/?q=", "%20")
    { }
}