namespace CarinaStudio.ULogViewer.Net;

class AppleDeveloperForumsSearchProvider : SimpleSearchProvider
{
    public AppleDeveloperForumsSearchProvider(IULogViewerApplication app) : base(app, "AppleDeveloperForums", "https://developer.apple.com/forums/search/?q=")
    { }
}