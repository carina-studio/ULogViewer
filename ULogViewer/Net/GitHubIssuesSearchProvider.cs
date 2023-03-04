namespace CarinaStudio.ULogViewer.Net;

class GitHubIssuesSearchProvider : SimpleSearchProvider
{
    public GitHubIssuesSearchProvider(IULogViewerApplication app) : base(app, "GitHubIssues", "https://github.com/search?type=issues&q=")
    { }
}