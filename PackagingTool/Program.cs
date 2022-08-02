using CarinaStudio.AppSuite.Packaging;

namespace CarinaStudio.ULogViewer.Packaging;

static class Program
{
    static int Main(string[] args) =>
        (int)new PackagingTool().Run(args);
}