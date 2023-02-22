namespace CarinaStudio.ULogViewer.Net;

class ZhihuSearchProvider : SimpleSearchProvider
{
    public ZhihuSearchProvider(IULogViewerApplication app) : base(app, "Zhihu", "https://www.zhihu.com/search?q=", "%20")
    { }
}