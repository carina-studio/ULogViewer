using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.IO;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using CarinaStudio.Windows.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

partial class SessionView
{
    // Constants.
    const int UpdateLogAnalysisParamsDelay = 500;


    // Static fields.
    static readonly SettingKey<bool> IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey = new("SessionView.IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShown");
    static readonly StyledProperty<bool> IsScrollingToLatestLogAnalysisResultNeededProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(IsScrollingToLatestLogAnalysisResultNeeded), true);
    static readonly SettingKey<bool> IsLogAnalysisPanelTutorialShownKey = new("SessionView.IsLogAnalysisPanelTutorialShown");
    static readonly SettingKey<bool> IsSelectLogAnalysisRuleSetsTutorialShownKey = new("SessionView.IsSelectLogAnalysisRuleSetsTutorialShown");
    static readonly SettingKey<bool> IsShowAllLogsForLogAnalysisResultTutorialShownKey = new("SessionView.IsShowAllLogsForLogAnalysisResultTutorialShown");


    // Fields.
    readonly ToggleButton createLogAnalysisRuleSetButton;
	readonly ContextMenu createLogAnalysisRuleSetMenu;
    bool isPointerPressedOnLogAnalysisResultListBox;
    readonly Avalonia.Controls.ListBox keyLogAnalysisRuleSetListBox;
    IDisposable logAnalysisPanelVisibilityObserverToken = EmptyDisposable.Default;
    readonly Avalonia.Controls.ListBox logAnalysisResultListBox;
    ScrollViewer? logAnalysisResultScrollViewer;
    readonly ToggleButton logAnalysisRuleSetsButton;
    readonly Popup logAnalysisRuleSetsPopup;
    readonly Avalonia.Controls.ListBox logAnalysisScriptSetListBox;
    readonly Avalonia.Controls.ListBox operationCountingAnalysisRuleSetListBox;
	readonly Avalonia.Controls.ListBox operationDurationAnalysisRuleSetListBox;
    readonly ScheduledAction scrollToLatestLogAnalysisResultAction;
    readonly HashSet<KeyLogAnalysisRuleSet> selectedKeyLogAnalysisRuleSets = new();
    readonly HashSet<LogAnalysisScriptSet> selectedLogAnalysisScriptSets = new();
    readonly HashSet<OperationCountingAnalysisRuleSet> selectedOperationCountingAnalysisRuleSets = new();
    readonly HashSet<OperationDurationAnalysisRuleSet> selectedOperationDurationAnalysisRuleSets = new();
    readonly ScheduledAction updateLogAnalysisAction;


    // Attach to view-model of log analysis.
    void AttachToLogAnalysis(LogAnalysisViewModel logAnalysis)
    {
        (logAnalysis.AnalysisResults as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnLogAnalysisResultsChanged);
        (logAnalysis.KeyLogAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
        (logAnalysis.LogAnalysisScriptSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
        (logAnalysis.OperationCountingAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
        (logAnalysis.OperationDurationAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged += this.OnSessionLogAnalysisRuleSetsChanged);
    }


    /// <summary>
    /// Create cooperative log analysis script set for current log profile.
    /// </summary>
    public async void CreateCooperativeLogAnalysisScriptSet()
    {
        // check state
        if (this.DataContext is not Session session)
            return;
        var profile = session.LogProfile;
        if (profile == null || profile.CooperativeLogAnalysisScriptSet != null)
            return;
        if (this.attachedWindow == null)
            return;
        
        // create script set
        var scriptSet = await new LogAnalysisScriptSetEditorDialog()
        {
            IsEmbeddedScriptSet = true,
        }.ShowDialog<LogAnalysisScriptSet?>(this.attachedWindow);
        if (scriptSet == null)
            return;
        if (this.DataContext != session || session.LogProfile != profile)
            return;
        profile.CooperativeLogAnalysisScriptSet = scriptSet;
    }


    /// <summary>
    /// Clear log analysis rule set selection.
    /// </summary>
    public void ClearLogAnalysisRuleSetSelection()
    {
        this.keyLogAnalysisRuleSetListBox.SelectedItems!.Clear();
        this.logAnalysisScriptSetListBox.SelectedItems!.Clear();
        this.operationCountingAnalysisRuleSetListBox.SelectedItems!.Clear();
        this.operationDurationAnalysisRuleSetListBox.SelectedItems!.Clear();
        this.updateLogAnalysisAction.Reschedule();
        this.IsScrollingToLatestLogAnalysisResultNeeded = true;
    }


    /// <summary>
    /// Create new key log analysis rule set.
    /// </summary>
    public void CreateKeyLogAnalysisRuleSet()
    {
        if (this.attachedWindow != null)
            KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
    }


    /// <summary>
    /// Create new log analysis rule set.
    /// </summary>
    public void CreateLogAnalysisRuleSet() =>
        this.createLogAnalysisRuleSetMenu.Open(this.createLogAnalysisRuleSetButton);
    

    /// <summary>
    /// Create new log analysis script set.
    /// </summary>
    public void CreateLogAnalysisScriptSet()
    {
        if (this.attachedWindow != null)
            LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, null);
    }


    /// <summary>
    /// Create new operation counting analysis rule set.
    /// </summary>
    public void CreateOperationCountingAnalysisRuleSet()
    {
        if (this.attachedWindow != null)
            OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
    }


    /// <summary>
    /// Create new operation duration analysis rule set.
    /// </summary>
    public void CreateOperationDurationAnalysisRuleSet()
    {
        if (this.attachedWindow != null)
            OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, null);
    }


    // Detach from view-model of log analysis.
    void DetachFromLogAnalysis(LogAnalysisViewModel logAnalysis)
    {
        (logAnalysis.AnalysisResults as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnLogAnalysisResultsChanged);
        (logAnalysis.KeyLogAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
        (logAnalysis.LogAnalysisScriptSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
        (logAnalysis.OperationCountingAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
        (logAnalysis.OperationDurationAnalysisRuleSets as INotifyCollectionChanged)?.Let(it =>
            it.CollectionChanged -= this.OnSessionLogAnalysisRuleSetsChanged);
    }


    /// <summary>
    /// Edit cooperative log analysis script set for current log profile.
    /// </summary>
    public async void EditCooperativeLogAnalysisScriptSet()
    {
        // check state
        if (this.DataContext is not Session session)
            return;
        var profile = session.LogProfile;
        if (profile == null || profile.CooperativeLogAnalysisScriptSet == null)
            return;
        if (this.attachedWindow == null)
            return;
        
        // create script set
        var scriptSet = await new LogAnalysisScriptSetEditorDialog()
        {
            IsEmbeddedScriptSet = true,
            ScriptSetToEdit = new LogAnalysisScriptSet(profile.CooperativeLogAnalysisScriptSet, ""),
        }.ShowDialog<LogAnalysisScriptSet?>(this.attachedWindow);
        if (scriptSet == null)
            return;
        if (this.DataContext != session || session.LogProfile != profile)
            return;
        profile.CooperativeLogAnalysisScriptSet = scriptSet;
    }


    // Edit given key log analysis rule set.
    void EditKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
    {
        if (ruleSet == null || this.attachedWindow == null)
            return;
        KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
    }


    /// <summary>
    /// Command to edit given key log analysis rule set.
    /// </summary>
    public ICommand EditKeyLogAnalysisRuleSetCommand { get; }


    // Edit given log analysis script set.
    void EditLogAnalysisScriptSet(LogAnalysisScriptSet? scriptSet)
    {
        if (scriptSet == null || this.attachedWindow == null)
            return;
        LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, scriptSet);
    }


    /// <summary>
    /// Command to edit given log analysis script set.
    /// </summary>
    public ICommand EditLogAnalysisScriptSetCommand { get; }


    // Edit given operation counting analysis rule set.
    void EditOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet? ruleSet)
    {
        if (ruleSet == null || this.attachedWindow == null)
            return;
        OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
    }


    /// <summary>
    /// Command to edit given operation counting analysis rule set.
    /// </summary>
    public ICommand EditOperationCountingAnalysisRuleSetCommand { get; }


    // Edit given operation duration analysis rule set.
    void EditOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
    {
        if (ruleSet == null || this.attachedWindow == null)
            return;
        OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ruleSet);
    }


    /// <summary>
    /// Command to edit given operation duration analysis rule set.
    /// </summary>
    public ICommand EditOperationDurationAnalysisRuleSetCommand { get; }


    // Export given key log analysis rule set.
    async void ExportKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
    {
        // check state
        if (ruleSet == null || this.attachedWindow == null)
            return;
        
        // select file
        var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // export
        try
        {
            await ruleSet.SaveAsync(fileName, false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to export key log analysis rule set '{ruleSetId}' to '{fileName}'", ruleSet.Id, fileName);
            this.OnExportLogAnalysisRuleSetFailed(ruleSet.Name, fileName);
        }
    }


    /// <summary>
    /// Command to export given key log analysis rule set.
    /// </summary>
    public ICommand ExportKeyLogAnalysisRuleSetCommand { get; }


    // Export given log analysis script set.
    async void ExportLogAnalysisScriptSet(LogAnalysisScriptSet? scriptSet)
    {
        // check state
        if (scriptSet == null || this.attachedWindow == null)
            return;
        
        // select file
        var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // export
        try
        {
            await scriptSet.SaveAsync(fileName, false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to export log analysis script set '{scriptSetId}' to '{fileName}'", scriptSet.Id, fileName);
            this.OnExportLogAnalysisRuleSetFailed(scriptSet.Name, fileName);
        }
    }


    /// <summary>
    /// Command to export given log analysis script set.
    /// </summary>
    public ICommand ExportLogAnalysisScriptSetCommand { get; }


    // Export given operation counting analysis rule set.
    async void ExportOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet? ruleSet)
    {
        // check state
        if (ruleSet == null || this.attachedWindow == null)
            return;
        
        // select file
        var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // export
        try
        {
            await ruleSet.SaveAsync(fileName, false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to export operation counting analysis rule set '{ruleSetId}' to '{fileName}'", ruleSet.Id,  fileName);
            this.OnExportLogAnalysisRuleSetFailed(ruleSet.Name, fileName);
        }
    }


    /// <summary>
    /// Command to export given operation counting analysis rule set.
    /// </summary>
    public ICommand ExportOperationCountingAnalysisRuleSetCommand { get; }


    // Export given operation duration analysis rule set.
    async void ExportOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
    {
        // check state
        if (ruleSet == null || this.attachedWindow == null)
            return;
        
        // select file
        var fileName = await this.SelectFileToExportLogAnalysisRuleSetAsync();
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // export
        try
        {
            await ruleSet.SaveAsync(fileName, false);
        }
        catch (Exception ex)
        {
            this.Logger.LogError(ex, "Failed to export operation duration analysis rule set '{ruleSetId}' to '{fileName}'", ruleSet.Id, fileName);
            this.OnExportLogAnalysisRuleSetFailed(ruleSet.Name, fileName);
        }
    }


    /// <summary>
    /// Command to export given operation duration analysis rule set.
    /// </summary>
    public ICommand ExportOperationDurationAnalysisRuleSetCommand { get; }


    /// <summary>
    /// Import log analysis rule set.
    /// </summary>
    public async void ImportLogAnalysisRuleSet()
    {
        // check state
        if (this.attachedWindow == null)
            return;
        
        // select file
        var fileName = (await this.attachedWindow.StorageProvider.OpenFilePickerAsync(new()
        {
            FileTypeFilter = new[]
            {
                new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json"))
                {
                    Patterns = new[] { "*.json" }
                }
            }
        })).Let(it =>
        {
            if (it.Count == 1 && it[0].TryGetUri(out var uri))
                return uri.LocalPath;
            return null;
        });
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // try loading rule set
        var klaRuleSet = await Global.RunOrDefaultAsync(async () => await KeyLogAnalysisRuleSet.LoadAsync(this.Application, fileName, true));
        var laScriptSet = klaRuleSet == null
            ? await Global.RunOrDefaultAsync(async () => await LogAnalysisScriptSet.LoadAsync(this.Application, fileName))
            : null;
        var ocaRuleSet = klaRuleSet == null && laScriptSet == null
            ? await Global.RunOrDefaultAsync(async () => await OperationCountingAnalysisRuleSet.LoadAsync(this.Application, fileName, true))
            : null;
        var odaRuleSet = klaRuleSet == null && laScriptSet == null && ocaRuleSet == null
            ? await Global.RunOrDefaultAsync(async () => await OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileName, true))
            : null;
        if (this.attachedWindow == null)
            return;
        if (klaRuleSet == null 
            && laScriptSet == null
            && ocaRuleSet == null
            && odaRuleSet == null)
        {
            _ = new MessageDialog()
            {
                Icon = MessageDialogIcon.Error,
                Message = new FormattedString().Also(it =>
                {
                    it.Arg1 = fileName;
                    it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.FailedToImportLogAnalysisRuleSet"));
                }),
            }.ShowDialog(this.attachedWindow);
            return;
        }

        // edit and add rule set
        if (klaRuleSet != null)
            KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, klaRuleSet);
        else if (laScriptSet != null)
            LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, laScriptSet);
        else if (ocaRuleSet != null)
            OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ocaRuleSet);
        else if (odaRuleSet != null)
            OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, odaRuleSet);
    }


    // Get or set whether scrolling to latest log analysis result is needed or not.
    public bool IsScrollingToLatestLogAnalysisResultNeeded
    {
        get => this.GetValue<bool>(IsScrollingToLatestLogAnalysisResultNeededProperty);
        set => this.SetValue<bool>(IsScrollingToLatestLogAnalysisResultNeededProperty, value);
    }


    // Called when failed to export log analysis rule set.
    void OnExportLogAnalysisRuleSetFailed(string? ruleSetName, string fileName)
    {
        if (this.attachedWindow != null)
        {
            _ = new MessageDialog()
            {
                Icon = MessageDialogIcon.Error,
                Message = new FormattedString().Also(it =>
                {
                    it.Arg1 = ruleSetName;
                    it.Arg2 = fileName;
                    it.Bind(FormattedString.FormatProperty, this.GetResourceObservable("String/SessionView.FailedToExportLogAnalysisRuleSet"));
                }),
            }.ShowDialog(this.attachedWindow);
        }
    }


    // Called when user clicked the indicator of log analysis result.
    void OnLogAnalysisResultIndicatorClicked(DisplayableLog log)
    {
        if (this.DataContext is not Session session)
            return;
        var firstResult = (DisplayableLogAnalysisResult?)null;
        this.logAnalysisResultListBox.SelectedItems!.Clear();
        foreach (var result in log.AnalysisResults)
        {
            firstResult ??= result;
            this.logAnalysisResultListBox.SelectedItems.Add(result);
        }
        if (firstResult != null)
        {
            session.LogAnalysis.IsPanelVisible = true;
            this.logAnalysisResultListBox.ScrollIntoView(firstResult);
        }
    }


    // Called when pointer pressed on log analysis result list box.
    void OnLogAnalysisResultListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // clear selection
        var point = e.GetCurrentPoint(this.logAnalysisResultListBox);
        var hitControl = this.logAnalysisResultListBox.InputHitTest(point.Position).Let(it =>
        {
            if (it == null)
                return (IVisual?)null;
            var listBoxItem = it.FindAncestorOfType<ListBoxItem>(true);
            if (listBoxItem != null)
                return listBoxItem;
            return it.FindAncestorOfType<ScrollBar>(true);
        });
        if (hitControl == null)
            this.SynchronizationContext.Post(() => this.logAnalysisResultListBox.SelectedItems!.Clear());
        else if (hitControl is ListBoxItem && !this.IsMultiSelectionKeyPressed(e.KeyModifiers) && point.Properties.IsLeftButtonPressed)
        {
            // [Workaround] Clear selection first to prevent performance issue of changing selection from multiple items
            this.logAnalysisResultListBox.SelectedItems!.Clear();
        }
        this.isPointerPressedOnLogAnalysisResultListBox = point.Properties.IsLeftButtonPressed;
    }


    // Called when pointer released on log analysis result list box.
    void OnLogAnalysisResultListBoxPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left)
            this.isPointerPressedOnLogAnalysisResultListBox = false;
    }


    // Called when pointer wheel change on log analysis result list box.
    void OnLogAnalysisResultListBoxPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        this.SynchronizationContext.Post(() => this.UpdateIsScrollingToLatestLogAnalysisResultNeeded(-e.Delta.Y));
    }


    // Called when log analysis result list box scrolled.
    void OnLogAnalysisResultListBoxScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (this.isPointerPressedOnLogAnalysisResultListBox 
            || this.pressedKeys.Contains(Avalonia.Input.Key.Down)
            || this.pressedKeys.Contains(Avalonia.Input.Key.Up)
            || this.pressedKeys.Contains(Avalonia.Input.Key.Home)
            || this.pressedKeys.Contains(Avalonia.Input.Key.End))
        {
            this.UpdateIsScrollingToLatestLogAnalysisResultNeeded(e.OffsetDelta.Y);
        }
    }


    // Called when double clicked on item in log analysis result list box.
    void OnLogAnalysisResultListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e) =>
        this.OnLogAnalysisResultListBoxSelectionChanged(true);


    // Called when log analysis result list box selection changed.
    void OnLogAnalysisResultListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e) =>
        this.OnLogAnalysisResultListBoxSelectionChanged(false);
    void OnLogAnalysisResultListBoxSelectionChanged(bool forceReSelection)
    {
        this.SynchronizationContext.Post(() =>
        {
            var count = this.logAnalysisResultListBox.SelectedItems!.Count;
            if (count == 0)
                return;
            if (count == 1 
                && this.logAnalysisResultListBox.SelectedItem is DisplayableLogAnalysisResult result
                && this.DataContext is Session session)
            {
                // select log to focus
                var log = this.isAltKeyPressed
                    ? (result.EndingLog ?? result.Log ?? result.BeginningLog)
                    : (result.BeginningLog ?? result.Log ?? result.EndingLog);
                var isLogSelected = log != null 
                    && this.logListBox.SelectedItems!.Count == 1
                    && Global.Run(() =>
                    {
                        var selectedLog = this.logListBox.SelectedItem as DisplayableLog;
                        if (result.Log != null && result.Log == selectedLog)
                            return true;
                        if (result.BeginningLog != null && result.BeginningLog == selectedLog)
                            return true;
                        if (result.EndingLog != null && result.EndingLog == selectedLog)
                            return true;
                        return false;
                    });

                // focus on log
                if (log != null && (forceReSelection || !isLogSelected))
                {
                    log.Let(new Func<DisplayableLog, Task>(async (log) =>
                    {
                        // show all logs if needed
                        if (!session.Logs.Contains(log))
                        {
                            // cancel showing marked logs only
                            var isLogFound = false;
                            var window = this.attachedWindow as MainWindow;
                            if (session.IsShowingMarkedLogsTemporarily)
                            {
                                // cancel showing marked logs only
                                if (!session.ToggleShowingMarkedLogsTemporarilyCommand.TryExecute())
                                    return;
                                await Task.Yield();
                                
                                // show tutorial
                                isLogFound = session.Logs.Contains(log);
                                if (isLogFound 
                                    && !this.PersistentState.GetValueOrDefault(IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey)
                                    && window != null)
                                {
                                    window.ShowTutorial(new Tutorial().Also(it =>
                                    {
                                        it.Anchor = this.FindControl<Control>("showMarkedLogsOnlyButton");
                                        it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.CancelShowingMarkedLogsOnlyForSelectingLogAnalysisResult"));
                                        it.Dismissed += (_, _) =>
                                            this.PersistentState.SetValue<bool>(IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey, true);
                                        it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
                                        it.IsSkippingAllTutorialsAllowed = false;
                                    }));
                                }
                            }

                            // show all logs
                            if (!isLogFound)
                            {
                                // show all logs
                                if (session.IsShowingAllLogsTemporarily || !session.ToggleShowingAllLogsTemporarilyCommand.TryExecute())
                                    return;
                                await Task.Yield();
                                
                                // show tutorial
                                if (!this.PersistentState.GetValueOrDefault(IsShowAllLogsForLogAnalysisResultTutorialShownKey)
                                    && window != null)
                                {
                                    window.ShowTutorial(new Tutorial().Also(it =>
                                    {
                                        it.Anchor = this.FindControl<Control>("showAllLogsTemporarilyButton");
                                        it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.ShowAllLogsTemporarilyForSelectingLogAnalysisResult"));
                                        it.Dismissed += (_, _) =>
                                            this.PersistentState.SetValue<bool>(IsShowAllLogsForLogAnalysisResultTutorialShownKey, true);
                                        it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
                                        it.IsSkippingAllTutorialsAllowed = false;
                                    }));
                                }
                            }
                        }

                        // select log
                        this.logListBox.SelectedItems!.Clear();
                        this.logListBox.SelectedItem = log;
                        this.ScrollToLog(log);
                        this.IsScrollingToLatestLogNeeded = false;
                    }));
                }

                // cancel auto scrolling
                this.IsScrollingToLatestLogAnalysisResultNeeded = false;
            }
            this.logListBox.Focus();
        });
    }


    // Called when collection of log analysis results has been changed.
    void OnLogAnalysisResultsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.DataContext is not Session session)
            return;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (this.IsScrollingToLatestLogAnalysisResultNeeded)
                    this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (session.LogAnalysis.AnalysisResults.IsEmpty())
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                break;
            case NotifyCollectionChangedAction.Reset:
                if (session.LogAnalysis.AnalysisResults.IsEmpty())
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                else
                    this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
                break;
        }
    }


    // Called when selection of list box of log analysis rule sets has been changed.
    void OnLogAnalysisRuleSetListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // update selected rule sets
        var listBox = (Avalonia.Controls.ListBox)sender.AsNonNull();
        foreach (var ruleSet in e.RemovedItems)
        {
            if (ruleSet is KeyLogAnalysisRuleSet klaRuleSets)
                this.selectedKeyLogAnalysisRuleSets.Remove(klaRuleSets);
            else if (ruleSet is LogAnalysisScriptSet laScriptSet)
                this.selectedLogAnalysisScriptSets.Remove(laScriptSet);
            else if (ruleSet is OperationCountingAnalysisRuleSet ocaRuleSets)
                this.selectedOperationCountingAnalysisRuleSets.Remove(ocaRuleSets);
            else if (ruleSet is OperationDurationAnalysisRuleSet odaRuleSets)
                this.selectedOperationDurationAnalysisRuleSets.Remove(odaRuleSets);
            else if (ruleSet != null)
                throw new NotImplementedException();
        }
        foreach (var ruleSet in e.AddedItems)
        {
            if (ruleSet is KeyLogAnalysisRuleSet klaRuleSets)
                this.selectedKeyLogAnalysisRuleSets.Add(klaRuleSets);
            else if (ruleSet is LogAnalysisScriptSet laScriptSet)
                this.selectedLogAnalysisScriptSets.Add(laScriptSet);
            else if (ruleSet is OperationCountingAnalysisRuleSet ocaRuleSets)
                this.selectedOperationCountingAnalysisRuleSets.Add(ocaRuleSets);
            else if (ruleSet is OperationDurationAnalysisRuleSet odaRuleSets)
                this.selectedOperationDurationAnalysisRuleSets.Add(odaRuleSets);
            else if (ruleSet != null)
                throw new NotImplementedException();
        }

        // sync back to UI if needed
        var selectedRuleSetCount = 0;
        var copiedSelectedRuleSets = (IEnumerable)Array.Empty<object>();
        var syncBackToUI = Global.Run(() =>
        {
            if (listBox == this.keyLogAnalysisRuleSetListBox)
            {
                selectedRuleSetCount = this.selectedKeyLogAnalysisRuleSets.Count;
                if (selectedRuleSetCount != listBox.SelectedItems!.Count)
                {
                    copiedSelectedRuleSets = this.selectedKeyLogAnalysisRuleSets.ToArray();
                    return true;
                }
                return false;
            }
            if (listBox == this.logAnalysisScriptSetListBox)
            {
                selectedRuleSetCount = this.selectedLogAnalysisScriptSets.Count;
                if (selectedRuleSetCount != listBox.SelectedItems!.Count)
                {
                    copiedSelectedRuleSets = this.selectedLogAnalysisScriptSets.ToArray();
                    return true;
                }
                return false;
            }
            if (listBox == this.operationCountingAnalysisRuleSetListBox)
            {
                selectedRuleSetCount = this.selectedOperationCountingAnalysisRuleSets.Count;
                if (selectedRuleSetCount != listBox.SelectedItems!.Count)
                {
                    copiedSelectedRuleSets = this.selectedOperationCountingAnalysisRuleSets.ToArray();
                    return true;
                }
                return false;
            }
            if (listBox == this.operationDurationAnalysisRuleSetListBox)
            {
                selectedRuleSetCount = this.selectedOperationDurationAnalysisRuleSets.Count;
                if (selectedRuleSetCount != listBox.SelectedItems!.Count)
                {
                    copiedSelectedRuleSets = this.selectedOperationDurationAnalysisRuleSets.ToArray();
                    return true;
                }
                return false;
            }
            throw new NotImplementedException();
        });
        if (syncBackToUI)
        {
            // [Workaround] Need to sync selection back to control because selection will be cleared when popup opened
            if (selectedRuleSetCount > 0)
            {
                var isScheduled = this.updateLogAnalysisAction?.IsScheduled ?? false;
                this.SynchronizationContext.Post(() =>
                {
                    listBox.SelectedItems!.Clear();
                    foreach (var ruleSet in copiedSelectedRuleSets)
                        listBox.SelectedItems.Add(ruleSet);
                    if (!isScheduled)
                        this.updateLogAnalysisAction?.Cancel();
                });
            }
        }
        else
            this.updateLogAnalysisAction.Reschedule(UpdateLogAnalysisParamsDelay);
    }


    // Called when runtime error occurred by log analysis script.
    void OnLogAnalysisScriptRuntimeErrorOccurred(object? sender, ScriptRuntimeErrorEventArgs e)
    {
        // check state
        if (this.attachedWindow == null)
            return;
        if (e.ScriptContainer is not LogAnalysisScriptSet scriptSet)
            return;
        bool isCooperativeLogAnalysis = (this.DataContext as Session)?.LogProfile?.CooperativeLogAnalysisScriptSet == scriptSet;
        string scriptType;
        if (scriptSet.AnalysisScript == e.Script)
            scriptType = "AnalysisScript";
        else if (scriptSet.SetupScript == e.Script)
            scriptType = "SetupScript";
        else
            scriptType = "UnknownScript";
        
        // generate message
        var message = new FormattedString();
        if (isCooperativeLogAnalysis)
            message.Bind(FormattedString.Arg1Property, this.Application.GetObservableString("SessionView.LogAnalysisScript.CooperativeLogAnalysis"));
        else
            message.Arg1 = scriptSet.Name;
        message.Bind(FormattedString.FormatProperty, this.Application.GetObservableString($"SessionView.LogAnalysisScript.RuntimeError.{scriptType}"));

        // show dialog
        _ = new ScriptRuntimeErrorDialog()
        {
            Error = e.Error,
            Message = message,
        }.ShowDialog(this.attachedWindow);
    }


    // Called when list of log analysis of session changed.
    void OnSessionLogAnalysisRuleSetsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.DataContext is not Session session)
            return;
        var isUpdateScheduled = this.updateLogAnalysisAction.IsScheduled;
        var syncBack = false;
        var selectedItems = Global.Run(() =>
        {
            if (sender == session.LogAnalysis.KeyLogAnalysisRuleSets)
                return this.keyLogAnalysisRuleSetListBox.SelectedItems;
            else if (sender == session.LogAnalysis.LogAnalysisScriptSets)
                return this.logAnalysisScriptSetListBox.SelectedItems;
            else if (sender == session.LogAnalysis.OperationCountingAnalysisRuleSets)
                return this.operationCountingAnalysisRuleSetListBox.SelectedItems;
            else if (sender == session.LogAnalysis.OperationDurationAnalysisRuleSets)
                return this.operationDurationAnalysisRuleSetListBox.SelectedItems;
            else
                throw new NotImplementedException();
        });
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var ruleSet in e.NewItems!)
                {
                    if (!selectedItems!.Contains(ruleSet))
                    {
                        syncBack = true;
                        selectedItems.Add(ruleSet);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var ruleSet in e.OldItems!)
                {
                    if (selectedItems!.Contains(ruleSet))
                    {
                        syncBack = true;
                        selectedItems.Remove(ruleSet);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                syncBack = true;
                selectedItems!.Clear();
                foreach (var ruleSet in (IList)sender.AsNonNull())
                    selectedItems.Add(ruleSet);
                break;
            default:
                this.Logger.LogError("Unsupported change of key log analysis rule sets: {action}", e.Action);
                break;
        }
        if (syncBack && !isUpdateScheduled)
            this.updateLogAnalysisAction.Cancel();
    }


    /// <summary>
    /// Open online documentation.
    /// </summary>
#pragma warning disable CA1822
    public void OpenLogAnalysisDocumentation() =>
        Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis");
#pragma warning restore CA1822


    // Remove given key log analysis rule set.
    async void RemoveKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet? ruleSet)
    {
        if (ruleSet == null || this.attachedWindow == null)
            return;
        if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(ruleSet.Name, false))
        {
            KeyLogAnalysisRuleSetEditorDialog.CloseAll(ruleSet);
            KeyLogAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
        }
    }


    /// <summary>
    /// Command to remove given key log analysis rule set.
    /// </summary>
    public ICommand RemoveKeyLogAnalysisRuleSetCommand { get; }


    // Remove given log analysis script set.
    async void RemoveLogAnalysisScriptSet(LogAnalysisScriptSet? scriptSet)
    {
        if (scriptSet == null || this.attachedWindow == null)
            return;
        if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(scriptSet.Name, true))
        {
            LogAnalysisScriptSetEditorDialog.CloseAll(scriptSet);
            LogAnalysisScriptSetManager.Default.RemoveScriptSet(scriptSet);
        }
    }


    /// <summary>
    /// Command to remove given log analysis script set.
    /// </summary>
    public ICommand RemoveLogAnalysisScriptSetCommand { get; }


    // Remove given operation counting analysis rule set.
    async void RemoveOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet? ruleSet)
    {
        if (ruleSet == null || this.attachedWindow == null)
            return;
        if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(ruleSet.Name, false))
        {
            OperationCountingAnalysisRuleSetEditorDialog.CloseAll(ruleSet);
            OperationCountingAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
        }
    }


    /// <summary>
    /// Command to remove given operation counting analysis rule set.
    /// </summary>
    public ICommand RemoveOperationCountingAnalysisRuleSetCommand { get; }


    // Remove given operation duration analysis rule set.
    async void RemoveOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet? ruleSet)
    {
        if (ruleSet == null || this.attachedWindow == null)
            return;
        if (await this.ConfirmRemovingLogAnalysisRuleSetAsync(ruleSet.Name, false))
        {
            OperationDurationAnalysisRuleSetEditorDialog.CloseAll(ruleSet);
            OperationDurationAnalysisRuleSetManager.Default.RemoveRuleSet(ruleSet);
        }
    }


    /// <summary>
    /// Command to remove given operation duration analysis rule set.
    /// </summary>
    public ICommand RemoveOperationDurationAnalysisRuleSetCommand { get; }


    // Show UI for user to select file to export log analysis rule set.
    async Task<string?> SelectFileToExportLogAnalysisRuleSetAsync()
    {
        if (this.attachedWindow == null)
            return null;
        return (await this.attachedWindow.StorageProvider.SaveFilePickerAsync(new ()
        {
            DefaultExtension = ".json",
            FileTypeChoices = new[]
            {
                new FilePickerFileType(this.Application.GetStringNonNull("FileFormat.Json")) { Patterns = new[] { "*.json" }}
            }
        }))?.Let(it =>
        {
            if (!it.TryGetUri(out var uri))
                return null;
            var path = uri.LocalPath;
            if (!PathEqualityComparer.Default.Equals(Path.GetExtension(path), ".json"))
                path += ".json";
            return path;
        });
    }


    // Show tutorial of log analysis rule sets if needed.
    void ShowLogAnalysisRuleSetsTutorial()
    {
        // check state
        if (this.PersistentState.GetValueOrDefault(IsSelectLogAnalysisRuleSetsTutorialShownKey))
            return;
        if (this.attachedWindow is not MainWindow window || window.CurrentTutorial != null || !window.IsActive)
            return;
        if (this.DataContext is not Session session || !session.IsActivated || !session.LogAnalysis.IsPanelVisible)
            return;
        if (!this.logAnalysisRuleSetsButton.IsVisible)
            return;

        // show tutorial
        window.ShowTutorial(new Tutorial().Also(it =>
        {
            it.Anchor = this.logAnalysisRuleSetsButton;
            it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.SelectLogAnalysisRuleSets"));
            it.Dismissed += (_, _) => 
                this.PersistentState.SetValue<bool>(IsSelectLogAnalysisRuleSetsTutorialShownKey, true);
            it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
            it.IsSkippingAllTutorialsAllowed = false;
        }));
    }


    // Update auto scrolling state according to user scrolling state.
    void UpdateIsScrollingToLatestLogAnalysisResultNeeded(double userScrollingDelta)
    {
        var scrollViewer = this.logAnalysisResultScrollViewer;
        if (scrollViewer == null)
            return;
        var logProfile = (this.HasLogProfile ? (this.DataContext as Session)?.LogProfile : null);
        if (logProfile == null)
            return;
        var offset = scrollViewer.Offset;
        if (Math.Abs(offset.Y) < 0.5 && offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height)
            return;
        if (this.IsScrollingToLatestLogAnalysisResultNeeded)
        {
            if (userScrollingDelta < 0)
            {
                this.Logger.LogDebug("Cancel auto scrolling of log analysis result because of user scrolling up");
                this.IsScrollingToLatestLogAnalysisResultNeeded = false;
            }
        }
        else
        {
            if (userScrollingDelta > 0 && ((scrollViewer.Offset.Y + scrollViewer.Viewport.Height) / scrollViewer.Extent.Height) >= 0.999)
            {
                this.Logger.LogDebug("Start auto scrolling of log analysis result because of user scrolling down");
                this.IsScrollingToLatestLogAnalysisResultNeeded = true;
            }
        }
    }
}