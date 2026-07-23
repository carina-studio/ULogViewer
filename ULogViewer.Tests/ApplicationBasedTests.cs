namespace CarinaStudio.ULogViewer;

/// <summary>
/// Base implementation of tests based-on <see cref="TestApp"/>.
/// </summary>
abstract class ApplicationBasedTests : AppSuite.ApplicationBasedTests<TestApp>
{
	// Create mock application.
	protected override TestApp CreateMockApplication() => TestApp.Initialize();
}
