namespace CarinaStudio.ULogViewer.Net;

class BaiduSearchProvider : SimpleSearchProvider
{
    public BaiduSearchProvider(IULogViewerApplication app) : base(app, "Baidu", "https://www.baidu.com/s?wd=", "%20")
    { }
}