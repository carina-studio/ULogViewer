namespace CarinaStudio.ULogViewer.Net;

class GoogleDevelopersSearchProvider : SimpleSearchProvider
{
    public GoogleDevelopersSearchProvider(IULogViewerApplication app) : base(app, "GoogleDevelopers", "https://developers.google.com/s/results?q=", "%20")
    { }
}