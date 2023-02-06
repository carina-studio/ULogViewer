using System.Threading.Tasks;
using CarinaStudio.AppSuite.Testing;
using CarinaStudio.ULogViewer.ViewModels;
using NUnit.Framework;

namespace CarinaStudio.ULogViewer.Testing.Sessions;

// Base class for test case of Session.
abstract class BaseTest : TestCase
{
    // Fields.
    Workspace? workspace;


    // Constructor.
    protected BaseTest(IULogViewerApplication app, string name) : base(app, "Sessions", name)
    { }


    // Get Workspace for testing.
    protected Workspace Workspace => this.workspace ?? throw new AssertionException("No Workspace for testing.");


    /// <inheritdoc/>
    protected override async Task OnSetupAsync()
    {
        // call base
        await base.OnSetupAsync();

        // find workspace
        var mainWindow = this.Application.LatestActiveMainWindow ?? throw new AssertionException("No main window for testing.");
        this.workspace = (mainWindow.DataContext as Workspace) ?? throw new AssertionException("No Workspace for testing.");
    }


    /// <inheritdoc/>
    protected override Task OnTearDownAsync()
    {
        this.workspace = null;
        return base.OnTearDownAsync();
    }
}