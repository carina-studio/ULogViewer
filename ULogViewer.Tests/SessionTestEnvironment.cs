using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer;

/// <summary>
/// Utility to initialize environment for testing based-on <see cref="Session"/>.
/// </summary>
static class SessionTestEnvironment
{
	// Static fields.
	static Task? initTask;


	/// <summary>
	/// Initialize managers needed by <see cref="Session"/>.
	/// </summary>
	/// <param name="app">Application.</param>
	/// <returns>Task of initialization.</returns>
	public static Task InitializeAsync(IULogViewerApplication app)
	{
		app.VerifyAccess();
		return initTask ??= InitializeCoreAsync(app);
	}


	// Initialize managers sequentially with the same order as application launching.
	static async Task InitializeCoreAsync(IULogViewerApplication app)
	{
		await TextShellManager.InitializeAsync(app);
		await PredefinedLogTextFilterManager.InitializeAsync(app);
		await LogTextFilterPhrasesDatabase.InitializeAsync(app);
		await KeyLogAnalysisRuleSetManager.InitializeAsync(app);
		await LogAnalysisScriptSetManager.InitializeAsync(app);
		await OperationCountingAnalysisRuleSetManager.InitializeAsync(app);
		await OperationDurationAnalysisRuleSetManager.InitializeAsync(app);
	}
}
