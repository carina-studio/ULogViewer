using CarinaStudio.AppSuite;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;

/// <summary>
/// Log analyzer based-on user defined script.
/// </summary>
class ScriptDisplayableLogAnalyzer : BaseDisplayableLogAnalyzer<ScriptDisplayableLogAnalyzer.AnalysisToken>, IScriptRunningHost
{
    /// <summary>
    /// Token of analysis.
    /// </summary>
    public class AnalysisToken
    {
    }


    // Fields.
    readonly List<LogAnalysisScriptSet> attachedScriptSets = new();
    bool isCooperativeLogAnalysis;
    readonly ObservableList<DisplayableLogProperty> logProperties = new();
    readonly ObservableList<LogAnalysisScriptSet> scriptSets = new();


    /// <summary>
    /// Initialize new <see cref="ScriptDisplayableLogAnalyzer"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="source">Source logs.</param>
    /// <param name="comparison">Comparison of source logs.</param>
    /// <param name="priority">Priority of logs processing.</param>
    public ScriptDisplayableLogAnalyzer(IULogViewerApplication app, IList<DisplayableLog> source, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Default) : base(app, source, comparison, priority)
    { 
        this.logProperties.CollectionChanged += this.OnLogPropertiesChanged;
        this.scriptSets.CollectionChanged += this.OnScriptSetsChanged;
    }


    /// <inheritdoc/>
    IAppSuiteApplication IApplicationObject<IAppSuiteApplication>.Application => this.Application;


    /// <summary>
    /// Get or set whether <see cref="ScriptSets"/> are cooperative log analysis script sets of one or more log profiles or not.
    /// </summary>
    public bool IsCooperativeLogAnalysis 
    { 
        get => this.isCooperativeLogAnalysis;
        set
        {
            this.VerifyAccess();
            this.VerifyDisposed();
            if (this.isCooperativeLogAnalysis == value)
                return;
            this.isCooperativeLogAnalysis = value;
            this.InvalidateProcessing();
            this.OnPropertyChanged(nameof(IsCooperativeLogAnalysis));
        }
    }


    /// <inheritdoc/>
    bool IScriptRunningHost.IsRunningScripts => this.IsProcessing;


    /// <inheritdoc/>
    protected override AnalysisToken CreateProcessingToken(out bool isProcessingNeeded)
    {
        isProcessingNeeded = false;
        return new();
    }


    /// <summary>
    /// Get list of log properties to be included in analysis.
    /// </summary>
    public IList<DisplayableLogProperty> LogProperties { get => this.logProperties; }


    // Called whe list of log properties changed.
    void OnLogPropertiesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        this.InvalidateProcessing();


    /// <inheritdoc/>
    protected override bool OnProcessLog(AnalysisToken token, DisplayableLog log, out IList<DisplayableLogAnalysisResult> result)
    {
        throw new NotImplementedException();
    }


    // Called when script set changed.
    void OnScriptSetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LogAnalysisScriptSet.AnalysisScript):
            case nameof(LogAnalysisScriptSet.SetupScript):
                this.InvalidateProcessing();
                break;
        }
    }


    // Called when list of script sets changed.
    void OnScriptSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<LogAnalysisScriptSet>().Let(it =>
                {
                    foreach (var scriptSet in it)
                        scriptSet.PropertyChanged += this.OnScriptSetPropertyChanged;
                    this.attachedScriptSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<LogAnalysisScriptSet>().Let(it =>
                {
                    foreach (var scriptSet in it)
                        scriptSet.PropertyChanged -= this.OnScriptSetPropertyChanged;
                    this.attachedScriptSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                break;
            case NotifyCollectionChangedAction.Replace:
                e.OldItems!.Cast<LogAnalysisScriptSet>().Let(it =>
                {
                    foreach (var scriptSet in it)
                        scriptSet.PropertyChanged -= this.OnScriptSetPropertyChanged;
                    this.attachedScriptSets.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                });
                e.NewItems!.Cast<LogAnalysisScriptSet>().Let(it =>
                {
                    foreach (var scriptSet in it)
                        scriptSet.PropertyChanged += this.OnScriptSetPropertyChanged;
                    this.attachedScriptSets.InsertRange(e.NewStartingIndex, it);
                });
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var scriptSet in this.attachedScriptSets)
                    scriptSet.PropertyChanged -= this.OnScriptSetPropertyChanged;
                this.attachedScriptSets.Clear();
                foreach (var scriptSet in this.scriptSets)
                    scriptSet.PropertyChanged += this.OnScriptSetPropertyChanged;
                this.attachedScriptSets.AddRange(this.scriptSets);
                break;
            default:
                throw new NotSupportedException("Unsupported change of script sets.");
        }
        this.InvalidateProcessing();
    }


    /// <inheritdoc/>
    public event EventHandler<ScriptRuntimeErrorEventArgs>? ScriptRuntimeErrorOccurred;


    /// <summary>
    /// Get list of script sets to analyze log.
    /// </summary>
    public IList<LogAnalysisScriptSet> ScriptSets { get => this.scriptSets; }
}