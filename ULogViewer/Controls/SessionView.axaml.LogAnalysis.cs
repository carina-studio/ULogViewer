using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Data;
using CarinaStudio.AppSuite.Scripting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
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
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

partial class SessionView
{
    /// <summary>
    /// <see cref="IMultiValueConverter"/> to convert from <see cref="DisplayableLogAnalysisResultType"/> and related states to <see cref="IImage"/>.
    /// </summary>
    public static readonly IMultiValueConverter LogAnalysisResultIconConverter = new FuncMultiValueConverter<object?, IImage?>(values =>
    {
        if (values is not IList valueList 
            || valueList.Count < 3
            || valueList[0] is not DisplayableLogAnalysisResultType type
            || valueList[1] is not bool isPointerOver
            || valueList[2] is not bool isSelected)
        {
            return null;
        }
        var parameter = (isPointerOver && isSelected) ? "Light" : null;
        return DisplayableLogAnalysisResultIconConverter.Default.Convert(type, typeof(IImage), parameter, App.Current.CultureInfo) as IImage;
    });
    
    
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
    bool hasPendingLogAnalysisResultSelectionChange;
    bool isPointerPressedOnLogAnalysisResultListBox;
    bool isSmoothScrollingToLatestLogAnalysisResult;
    readonly Avalonia.Controls.ListBox keyLogAnalysisRuleSetListBox;
    long lastLogAnalysisResultUpdateTime;
    IDisposable? logAnalysisPanelVisibilityObserverToken = EmptyDisposable.Default;
    readonly Avalonia.Controls.ListBox logAnalysisResultListBox;
    ScrollViewer? logAnalysisResultScrollViewer;
    readonly ToggleButton logAnalysisRuleSetsButton;
    readonly Popup logAnalysisRuleSetsPopup;
    readonly Queue<long> logAnalysisResultUpdateTimeQueue = new(LogUpdateIntervalStatisticCount);
    readonly Avalonia.Controls.ListBox logAnalysisScriptSetListBox;
    LogAnalysisScriptSetSelectionContextMenu? logAnalysisScriptSetSelectionContextMenu;
    readonly Avalonia.Controls.ListBox operationCountingAnalysisRuleSetListBox;
	readonly Avalonia.Controls.ListBox operationDurationAnalysisRuleSetListBox;
    readonly ScheduledAction scrollToLatestLogAnalysisResultAction;
    readonly HashSet<KeyLogAnalysisRuleSet> selectedKeyLogAnalysisRuleSets = new();
    readonly HashSet<LogAnalysisScriptSet> selectedLogAnalysisScriptSets = new();
    readonly HashSet<OperationCountingAnalysisRuleSet> selectedOperationCountingAnalysisRuleSets = new();
    readonly HashSet<OperationDurationAnalysisRuleSet> selectedOperationDurationAnalysisRuleSets = new();
    readonly ScheduledAction smoothScrollToLatestLogAnalysisResultAction;
    readonly ScheduledAction updateLogAnalysisAction;


    // Attach to view-model of log analysis.
    void AttachToLogAnalysis(LogAnalysisViewModel logAnalysis)
    {
        // attach to events
        logAnalysis.LogAnalysisScriptRuntimeErrorOccurred += this.OnLogAnalysisScriptRuntimeErrorOccurred;
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
        
        // sync log analysis rule sets to UI
        this.keyLogAnalysisRuleSetListBox.SelectedItems.Let(it =>
        {
            it!.Clear();
            foreach (var ruleSet in logAnalysis.KeyLogAnalysisRuleSets)
                it.Add(ruleSet);
        });
        this.logAnalysisScriptSetListBox.SelectedItems.Let(it =>
        {
            it!.Clear();
            foreach (var scriptSet in logAnalysis.LogAnalysisScriptSets)
                it.Add(scriptSet);
        });
        this.operationCountingAnalysisRuleSetListBox.SelectedItems.Let(it =>
        {
            it!.Clear();
            foreach (var ruleSet in logAnalysis.OperationCountingAnalysisRuleSets)
                it.Add(ruleSet);
        });
        this.operationDurationAnalysisRuleSetListBox.SelectedItems.Let(it =>
        {
            it!.Clear();
            foreach (var ruleSet in logAnalysis.OperationDurationAnalysisRuleSets)
                it.Add(ruleSet);
        });
        this.updateLogAnalysisAction.Cancel();

        // show log analysis results
        if (this.DataContext is Session session && !session.IsRemovingLogFiles)
        {
            this.logAnalysisResultListBox.Bind(Avalonia.Controls.ListBox.ItemsSourceProperty, new Binding
            {
                Path = $"{nameof(Session.LogAnalysis)}.{nameof(LogAnalysisViewModel.AnalysisResults)}"
            });
        }
        
        // start auto scrolling
        if (logAnalysis.AnalysisResults.IsNotEmpty() && this.IsScrollingToLatestLogAnalysisResultNeeded)
        {
            this.isSmoothScrollingToLatestLogAnalysisResult = false;
            this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
            this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
        }
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
    
    
    // Copy selected log analysis rule set.
    void CopyKeyLogAnalysisRuleSet(KeyLogAnalysisRuleSet ruleSet)
    {
        if (this.attachedWindow == null)
            return;
        var newName = Utility.GenerateName(ruleSet.Name, name => 
            KeyLogAnalysisRuleSetManager.Default.RuleSets.FirstOrDefault(it => it.Name == name) != null);
        var newRuleSet = new KeyLogAnalysisRuleSet(ruleSet, newName);
        KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, newRuleSet);
    }


    /// <summary>
    /// Command to copy selected log analysis rule set.
    /// </summary>
    public ICommand CopyKeyLogAnalysisRuleSetCommand { get; }


    // Copy selected log analysis script set.
    void CopyLogAnalysisScriptSet(LogAnalysisScriptSet scriptSet)
    {
        if (this.attachedWindow == null)
            return;
        var newName = Utility.GenerateName(scriptSet.Name, name => 
            LogAnalysisScriptSetManager.Default.ScriptSets.FirstOrDefault(it => it.Name == name) != null);
        var newScriptSet = new LogAnalysisScriptSet(scriptSet, newName);
        LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, newScriptSet);
    }


    /// <summary>
    /// Command to copy selected log analysis script set.
    /// </summary>
    public ICommand CopyLogAnalysisScriptSetCommand { get; }
    
    
    // Copy selected log analysis rule set.
    void CopyOperationCountingAnalysisRuleSet(OperationCountingAnalysisRuleSet ruleSet)
    {
        if (this.attachedWindow == null)
            return;
        var newName = Utility.GenerateName(ruleSet.Name, name => 
            OperationCountingAnalysisRuleSetManager.Default.RuleSets.FirstOrDefault(it => it.Name == name) != null);
        var newRuleSet = new OperationCountingAnalysisRuleSet(ruleSet, newName);
        OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, newRuleSet);
    }


    /// <summary>
    /// Command to copy selected log analysis rule set.
    /// </summary>
    public ICommand CopyOperationCountingAnalysisRuleSetCommand { get; }


    /// <summary>
    /// Copy selected log analysis rule set.
    /// </summary>
    public void CopyOperationDurationAnalysisRuleSet(OperationDurationAnalysisRuleSet ruleSet)
    {
        if (this.attachedWindow == null)
            return;
        var newName = Utility.GenerateName(ruleSet.Name, name => 
            OperationDurationAnalysisRuleSetManager.Default.RuleSets.FirstOrDefault(it => it.Name == name) != null);
        var newRuleSet = new OperationDurationAnalysisRuleSet(ruleSet, newName);
        OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, newRuleSet);
    }


    /// <summary>
    /// Command to copy selected log analysis rule set.
    /// </summary>
    public ICommand CopyOperationDurationAnalysisRuleSetCommand { get; }


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
            LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, default(LogAnalysisScriptSet));
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
        // detach from events
        logAnalysis.LogAnalysisScriptRuntimeErrorOccurred -= this.OnLogAnalysisScriptRuntimeErrorOccurred;
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
        
        // remove log analysis results
        this.logAnalysisResultListBox.SelectedItems?.Clear();
        this.logAnalysisResultListBox.ItemsSource = null;
        
        // stop auto scrolling
        this.isSmoothScrollingToLatestLogAnalysisResult = false;
        this.scrollToLatestLogAnalysisResultAction.Cancel();
        this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
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
        var scriptSet = await new LogAnalysisScriptSetEditorDialog
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
    
    
#if DEBUG
    // Edit given log analysis script set file.
    internal void EditLogAnalysisScriptSetFile()
    { }
#endif


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
        var fileName = await FileSystemItemSelection.SelectFileToExportLogAnalysisRuleSetAsync(this.attachedWindow);
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
            return;
        }
        this.OnExportLogAnalysisRuleSetSucceeded(ruleSet.Name, fileName);
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
        var fileName = await FileSystemItemSelection.SelectFileToExportLogAnalysisRuleSetAsync(this.attachedWindow);
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
            return;
        }
        this.OnExportLogAnalysisRuleSetSucceeded(scriptSet.Name, fileName);
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
        var fileName = await FileSystemItemSelection.SelectFileToExportLogAnalysisRuleSetAsync(this.attachedWindow);
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
            return;
        }
        this.OnExportLogAnalysisRuleSetSucceeded(ruleSet.Name, fileName);
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
        var fileName = await FileSystemItemSelection.SelectFileToExportLogAnalysisRuleSetAsync(this.attachedWindow);
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
            return;
        }
        this.OnExportLogAnalysisRuleSetSucceeded(ruleSet.Name, fileName);
    }


    /// <summary>
    /// Command to export given operation duration analysis rule set.
    /// </summary>
    public ICommand ExportOperationDurationAnalysisRuleSetCommand { get; }
    
    
    /// <summary>
    /// Import cooperative log analysis script from file.
    /// </summary>
    public async void ImportCooperativeLogAnalysisScriptFromFile()
    {
        // select file
        if (this.DataContext is not Session session
            || !session.IsProVersionActivated
            || this.attachedWindow is null)
        {
            return;
        }
        var profile = session.LogProfile;
        if (profile is null)
            return;
        var fileName = await FileSystemItemSelection.SelectFileToImportLogAnalysisRuleSetAsync(this.attachedWindow);
        if (string.IsNullOrEmpty(fileName) 
            || this.DataContext != session 
            || !ReferenceEquals(session.LogProfile, profile))
        {
            return;
        }

        // try loading script set
        var scriptSet = await Global.RunOrDefaultAsync(async () => await LogAnalysisScriptSet.LoadAsync(this.Application, fileName));
        if (scriptSet == null)
        {
            _ = new MessageDialog
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

        // edit script set and replace
        scriptSet = await new LogAnalysisScriptSetEditorDialog
        {
            IsEmbeddedScriptSet = true,
            ScriptSetToEdit = scriptSet,
        }.ShowDialog<LogAnalysisScriptSet?>(this.attachedWindow);
        if (scriptSet is not null 
            && this.DataContext == session 
            && ReferenceEquals(session.LogProfile, profile))
        {
            profile.CooperativeLogAnalysisScriptSet = scriptSet;
        }
    }
    
    
    /// <summary>
    /// Import existing cooperative log analysis script.
    /// </summary>
    public async void ImportExistingCooperativeLogAnalysisScript()
    {
        // select script set
        if (this.attachedWindow is null 
            || this.DataContext is not Session session
            || !session.IsProVersionActivated)
        {
            return;
        }
        var profile = session.LogProfile;
        if (profile is null)
            return;
        var button = this.Get<ToggleButton>("importExistingCooperativeLogAnalysisScriptSetButton");
        button.IsChecked = true;
        this.logAnalysisScriptSetSelectionContextMenu ??= new();
        var scriptSet = await this.logAnalysisScriptSetSelectionContextMenu.OpenAsync(button);
        button.IsChecked = false;
        if (scriptSet is null
            || this.attachedWindow == null
            || this.DataContext != session
            || !ReferenceEquals(session.LogProfile, profile))
        {
            return;
        }

        // edit script set and replace
        scriptSet = await new LogAnalysisScriptSetEditorDialog()
        {
            IsEmbeddedScriptSet = true,
            ScriptSetToEdit = new(scriptSet, ""),
        }.ShowDialog<LogAnalysisScriptSet?>(this.attachedWindow);
        if (scriptSet is not null
            && this.DataContext == session
            && ReferenceEquals(session.LogProfile, profile))
        {
            profile.CooperativeLogAnalysisScriptSet = scriptSet;
        }
    }


    /// <summary>
    /// Import log analysis rule set.
    /// </summary>
    public async void ImportLogAnalysisRuleSet()
    {
        // check state
        if (this.attachedWindow is null)
            return;
        
        // select file
        var fileName = await FileSystemItemSelection.SelectFileToImportLogAnalysisRuleSetAsync(this.attachedWindow);
        if (string.IsNullOrEmpty(fileName))
            return;
        
        // try loading rule set
        var klaRuleSet = await Global.RunOrDefaultAsync(async () => await KeyLogAnalysisRuleSet.LoadAsync(this.Application, fileName, true));
        var laScriptSet = klaRuleSet is null
            ? await Global.RunOrDefaultAsync(async () => await LogAnalysisScriptSet.LoadAsync(this.Application, fileName))
            : null;
        var ocaRuleSet = klaRuleSet is null && laScriptSet is null
            ? await Global.RunOrDefaultAsync(async () => await OperationCountingAnalysisRuleSet.LoadAsync(this.Application, fileName, true))
            : null;
        var odaRuleSet = klaRuleSet is null && laScriptSet is null && ocaRuleSet is null
            ? await Global.RunOrDefaultAsync(async () => await OperationDurationAnalysisRuleSet.LoadAsync(this.Application, fileName, true))
            : null;
        if (this.attachedWindow is null)
            return;
        if (klaRuleSet is null 
            && laScriptSet is null
            && ocaRuleSet is null
            && odaRuleSet is null)
        {
            if (this.attachedWindow is MainWindow mainWindow)
            {
                mainWindow.AddNotification(new Notification().Also(it =>
                {
                    it.BindToResource(Notification.IconProperty, this, "Image/Icon.Error.Colored");
                    it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
                    {
                        it.Arg1 = fileName;
                        it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.FailedToImportLogAnalysisRuleSet"));
                    }));
                }));
            }
            else
            {
                _ = new MessageDialog
                {
                    Icon = MessageDialogIcon.Error,
                    Message = new FormattedString().Also(it =>
                    {
                        it.Arg1 = fileName;
                        it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.FailedToImportLogAnalysisRuleSet"));
                    }),
                }.ShowDialog(this.attachedWindow);
            }
            return;
        }

        // edit and add rule set
        if (klaRuleSet is not null)
            KeyLogAnalysisRuleSetEditorDialog.Show(this.attachedWindow, klaRuleSet);
        else if (laScriptSet is not null)
            LogAnalysisScriptSetEditorDialog.Show(this.attachedWindow, laScriptSet);
        else if (ocaRuleSet is not null)
            OperationCountingAnalysisRuleSetEditorDialog.Show(this.attachedWindow, ocaRuleSet);
        else if (odaRuleSet is not null)
            OperationDurationAnalysisRuleSetEditorDialog.Show(this.attachedWindow, odaRuleSet);
    }


    // Get or set whether scrolling to latest log analysis result is needed or not.
    public bool IsScrollingToLatestLogAnalysisResultNeeded
    {
        get => this.GetValue(IsScrollingToLatestLogAnalysisResultNeededProperty);
        set => this.SetValue(IsScrollingToLatestLogAnalysisResultNeededProperty, value);
    }
    
    
    // Called when failed to export log analysis rule set.
    void OnExportLogAnalysisRuleSetFailed(string? ruleSetName, string fileName)
    {
        if (this.attachedWindow is MainWindow mainWindow)
        {
            mainWindow.AddNotification(new Notification().Also(it =>
            {
                it.BindToResource(Notification.IconProperty, this, "Image/Icon.Error.Colored");
                it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
                {
                    it.Arg1 = ruleSetName;
                    it.Arg2 = fileName;
                    it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.FailedToExportLogAnalysisRuleSet"));
                }));
            }));
        }
        else if (this.attachedWindow is not null)
        {
            _ = new MessageDialog
            {
                Icon = MessageDialogIcon.Error,
                Message = new FormattedString().Also(it =>
                {
                    it.Arg1 = ruleSetName;
                    it.Arg2 = fileName;
                    it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.FailedToExportLogAnalysisRuleSet"));
                }),
            }.ShowDialog(this.attachedWindow);
        }
    }


    // Called when succeeded to export log analysis rule set.
    void OnExportLogAnalysisRuleSetSucceeded(string? ruleSetName, string fileName)
    {
        if (this.attachedWindow is MainWindow mainWindow)
        {
            mainWindow.AddNotification(new Notification().Also(it =>
            {
                if (Platform.IsOpeningFileManagerSupported)
                {
                    it.Actions = new[]
                    {
                        new NotificationAction().Also(it =>
                        {
                            it.Command = new Command(() => Platform.OpenFileManager(fileName));
                            it.Bind(NotificationAction.NameProperty, this.Application.GetObservableString("SessionView.ShowFileInExplorer"));
                        })
                    };
                }
                it.BindToResource(Notification.IconProperty, this, "Image/Icon.Success.Colored");
                it.Bind(Notification.MessageProperty, new FormattedString().Also(it =>
                {
                    it.Arg1 = ruleSetName;
                    it.Arg2 = fileName;
                    it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogAnalysisRuleSetExported"));
                }));
            }));
        }
        else if (this.attachedWindow is not null)
        {
            _ = new MessageDialog
            {
                Icon = MessageDialogIcon.Success,
                Message = new FormattedString().Also(it =>
                {
                    it.Arg1 = ruleSetName;
                    it.Arg2 = fileName;
                    it.Bind(FormattedString.FormatProperty, this.Application.GetObservableString("SessionView.LogAnalysisRuleSetExported"));
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
            if (it is not Visual visual)
                return default(Visual);
            var listBoxItem = visual.FindAncestorOfType<ListBoxItem>(true);
            if (listBoxItem != null)
                return listBoxItem;
            return visual.FindAncestorOfType<ScrollBar>(true);
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
        if (e.InitialPressMouseButton == MouseButton.Left)
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
            || this.pressedKeys.Contains(Key.Down)
            || this.pressedKeys.Contains(Key.Up)
            || this.pressedKeys.Contains(Key.Home)
            || this.pressedKeys.Contains(Key.End))
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
        if (this.hasPendingLogAnalysisResultSelectionChange)
            return;
        this.hasPendingLogAnalysisResultSelectionChange = true;
        this.SynchronizationContext.Post(() =>
        {
            this.hasPendingLogAnalysisResultSelectionChange = false;
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
                if (log is not null && (forceReSelection || !isLogSelected))
                {
                    this.Logger.LogTrace("Start selecting log of analysis result");
                    log.LetAsync(async log =>
                    {
                        // show all logs if needed
                        var isLogFound = session.Logs.Contains(log);
                        if (!isLogFound)
                        {
                            // cancel showing marked logs only
                            var window = this.attachedWindow as MainWindow;
                            if (session.IsShowingMarkedLogsTemporarily)
                            {
                                this.Logger.LogTrace("Cancel showing marked logs only because log of analysis result cannot be found in the list");
                                if (session.ToggleShowingMarkedLogsTemporarilyCommand.TryExecute())
                                {
                                    // check whether log can be found in list or not
                                    if (session.Logs.Contains(log))
                                        isLogFound = true;
                                    else
                                    {
                                        await Task.Delay(100);
                                        isLogFound = session.Logs.Contains(log);
                                    }

                                    // show tutorial
                                    if (isLogFound
                                        && !this.PersistentState.GetValueOrDefault(IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey)
                                        && window is not null)
                                    {
                                        window.ShowTutorial(new Tutorial().Also(it =>
                                        {
                                            it.Anchor = this.Get<Control>("showMarkedLogsOnlyButton");
                                            it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.CancelShowingMarkedLogsOnlyForSelectingLogAnalysisResult"));
                                            it.Dismissed += (_, _) =>
                                                this.PersistentState.SetValue<bool>(IsCancelShowingMarkedLogsForLogAnalysisResultTutorialShownKey, true);
                                            it.Icon = this.FindResourceOrDefault<IImage?>("Image/Icon.Lightbulb.Colored");
                                            it.IsSkippingAllTutorialsAllowed = false;
                                        }));
                                    }
                                }
                                else
                                    this.Logger.LogError("Unable to cancel showing marked logs only");
                            }

                            // show all logs temporarily
                            if (!isLogFound && !session.IsShowingAllLogsTemporarily)
                            {
                                this.Logger.LogTrace("Show all logs temporarily because log of analysis result cannot be found in the list");
                                if (session.ShowAllLogsTemporarilyCommand.TryExecute())
                                {
                                    // wait for log to be ready in list
                                    for (var i = 0; i < 10; ++i)
                                    {
                                        if (session.Logs.Contains(log))
                                        {
                                            isLogFound = true;
                                            break;
                                        }
                                        await Task.Delay(50);
                                    }

                                    // show tutorial
                                    if (!this.PersistentState.GetValueOrDefault(IsShowAllLogsForLogAnalysisResultTutorialShownKey)
                                        && window is not null)
                                    {
                                        window.ShowTutorial(new Tutorial().Also(it =>
                                        {
                                            it.Anchor = this.Get<Control>("showAllLogsTemporarilyButton");
                                            it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/SessionView.Tutorial.ShowAllLogsTemporarilyForSelectingLogAnalysisResult"));
                                            it.Dismissed += (_, _) =>
                                                this.PersistentState.SetValue<bool>(IsShowAllLogsForLogAnalysisResultTutorialShownKey, true);
                                            it.Icon = this.FindResourceOrDefault<IImage?>("Image/Icon.Lightbulb.Colored");
                                            it.IsSkippingAllTutorialsAllowed = false;
                                        }));
                                    }
                                }
                                else
                                    this.Logger.LogError("Unable to show all logs temporarily");
                            }
                        }

                        // select log
                        if (isLogFound)
                        {
                            this.Logger.LogTrace("Complete selecting log of analysis result");
                            this.logListBox.SelectedItems!.Clear();
                            this.logListBox.SelectedItem = log;
                            this.ScrollToLog(log, true);
                            this.IsScrollingToLatestLogNeeded = false;
                        }
                        else
                            this.Logger.LogError("Cannot find log of selected analysis result in the list");
                    });
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
                // statistic update time
                var currentTime = this.stopwatch.ElapsedMilliseconds;
                if (this.lastLogAnalysisResultUpdateTime > 0)
                {
                    var interval = (currentTime - this.lastLogAnalysisResultUpdateTime);
                    if (interval >= LogUpdateIntervalToResetStatistic)
                        this.logAnalysisResultUpdateTimeQueue.Clear();
                    else
                    {
                        while (this.logAnalysisResultUpdateTimeQueue.Count >= LogUpdateIntervalStatisticCount)
                            this.logAnalysisResultUpdateTimeQueue.Dequeue();
                        this.logAnalysisResultUpdateTimeQueue.Enqueue(interval);
                    }
                }
                this.lastLogAnalysisResultUpdateTime = currentTime;
                
                // trigger scrolling to latest result
                if (this.IsScrollingToLatestLogAnalysisResultNeeded && !this.scrollToLatestLogAnalysisResultAction.IsScheduled)
                {
                    // select scrolling interval
                    var averageInternal = this.logAnalysisResultUpdateTimeQueue.Count > 0
                        ? this.logAnalysisResultUpdateTimeQueue.Sum() / (double)this.logAnalysisResultUpdateTimeQueue.Count
                        : -1;
                    var scrollingInterval = averageInternal < 0 || averageInternal >= ScrollingToLatestLogInterval
                        ? ScrollingToLatestLogInterval
                        : SlowScrollingToLatestLogInterval;
                    
                    // scroll
                    if (this.isSmoothScrollingToLatestLogAnalysisResult)
                    {
                        this.isSmoothScrollingToLatestLogAnalysisResult = false;
                        this.smoothScrollToLatestLogAnalysisResultAction.Reschedule(scrollingInterval);
                    }
                    else
                        this.smoothScrollToLatestLogAnalysisResultAction.Schedule(scrollingInterval);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (session.LogAnalysis.AnalysisResults.IsEmpty())
                {
                    this.isSmoothScrollingToLatestLogAnalysisResult = false;
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                    this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                if (session.LogAnalysis.AnalysisResults.IsEmpty())
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                else
                    this.scrollToLatestLogAnalysisResultAction.Schedule(ScrollingToLatestLogInterval);
                this.isSmoothScrollingToLatestLogAnalysisResult = false;
                this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
                break;
        }
    }


    // Called when pointer pressed on list box of log analysis rule sets.
    void OnLogAnalysisRuleSetListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var multiSelectionKeyModifiers = KeyModifiers.Shift | (Platform.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control);
        if ((e.KeyModifiers & multiSelectionKeyModifiers) != 0)
            return;
        if (sender is not Avalonia.Controls.ListBox pressedListBox)
            return;
        var position = e.GetPosition(pressedListBox);
        var pressedButton = (pressedListBox.InputHitTest(position) as Visual)?.FindAncestorOfType<Button>(true);
        if (pressedButton is not null && pressedButton.FindAncestorOfType<Avalonia.Controls.ListBox>() == pressedListBox)
            return;
        if (!ReferenceEquals(sender, this.keyLogAnalysisRuleSetListBox))
            this.keyLogAnalysisRuleSetListBox.SelectedItems?.Clear();
        if (!ReferenceEquals(sender, this.logAnalysisScriptSetListBox))
            this.logAnalysisScriptSetListBox.SelectedItems?.Clear();
        if (!ReferenceEquals(sender, this.operationCountingAnalysisRuleSetListBox))
            this.operationCountingAnalysisRuleSetListBox.SelectedItems?.Clear();
        if (!ReferenceEquals(sender, this.operationDurationAnalysisRuleSetListBox))
            this.operationDurationAnalysisRuleSetListBox.SelectedItems?.Clear();
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
            if (sender == session.LogAnalysis.LogAnalysisScriptSets)
                return this.logAnalysisScriptSetListBox.SelectedItems;
            if (sender == session.LogAnalysis.OperationCountingAnalysisRuleSets)
                return this.operationCountingAnalysisRuleSetListBox.SelectedItems;
            if (sender == session.LogAnalysis.OperationDurationAnalysisRuleSets)
                return this.operationDurationAnalysisRuleSetListBox.SelectedItems;
            throw new NotSupportedException();
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
    
    
    /// <summary>
    /// Remove cooperative log analysis script.
    /// </summary>
    public async void RemoveCooperativeLogAnalysisScript()
    {
        if (this.attachedWindow == null
            || this.DataContext is not Session session)
        {
            return;
        }
        var profile = session.LogProfile;
        if (profile is null)
            return;
        var result = await new MessageDialog()
        {
            Buttons = MessageDialogButtons.YesNo,
            DefaultResult = MessageDialogResult.No,
            Icon = MessageDialogIcon.Question,
            Message = this.Application.GetObservableString("LogProfileEditorDialog.CooperativeLogAnalysisScriptSet.ConfirmDeletion"),
        }.ShowDialog(this.attachedWindow);
        if (result == MessageDialogResult.Yes
            && this.DataContext == session
            && ReferenceEquals(session.LogProfile, profile))
        {
            profile.CooperativeLogAnalysisScriptSet = null;
        }
    }


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
    
    
    // Scroll to latest log analysis result.
    void ScrollToLatestLogAnalysisResult(bool smoothScrolling = true)
    {
        // check state
        if (!this.IsScrollingToLatestLogAnalysisResultNeeded)
            return;
        if (this.DataContext is not Session session)
            return;
        var logProfile = session.LogProfile;
        if (session.LogAnalysis.AnalysisResults.IsEmpty() || logProfile is null || !session.IsActivated)
            return;
				
        // cancel scrolling
        if (this.logAnalysisResultListBox.ContextMenu?.IsOpen == true || this.logMarkingMenu.IsOpen)
        {
            this.isSmoothScrollingToLatestLogAnalysisResult = false;
            this.IsScrollingToLatestLogAnalysisResultNeeded = false;
            return;
        }

        // scroll to latest analysis result
        this.logAnalysisResultScrollViewer?.Let(scrollViewer =>
        {
            var extent = scrollViewer.Extent;
            var viewport = scrollViewer.Viewport;
            if (extent.Height > viewport.Height)
            {
                var currentOffset = scrollViewer.Offset;
                var targetOffset = logProfile.SortDirection == SortDirection.Ascending
                    ? extent.Height - viewport.Height
                    : 0;
                var distanceY = targetOffset - currentOffset.Y;
                if (Math.Abs(distanceY) <= 1)
                {
                    this.isSmoothScrollingToLatestLogAnalysisResult = false;
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                    this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
                }
                if (Math.Abs(distanceY) <= 3 
                    || !smoothScrolling 
                    || !this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.UseSmoothLogScrolling))
                {
                    scrollViewer.Offset = new(currentOffset.X, currentOffset.Y + distanceY);
                    this.isSmoothScrollingToLatestLogAnalysisResult = false;
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                    this.smoothScrollToLatestLogAnalysisResultAction.Cancel();
                }
                else
                {
                    scrollViewer.Offset = new(currentOffset.X, currentOffset.Y + distanceY * SmoothScrollingToLatestLogScale);
                    this.isSmoothScrollingToLatestLogAnalysisResult = true;
                    this.scrollToLatestLogAnalysisResultAction.Cancel();
                    this.smoothScrollToLatestLogAnalysisResultAction.Schedule(SmoothScrollingToLatestLogInterval);
                }
            }
        });
    }
    
    
    // Setup list box of log analysis rule sets.
    void SetupLogAnalysisRuleSetListBox(Avalonia.Controls.ListBox listBox)
    {
        listBox.AddHandler(PointerPressedEvent, this.OnLogAnalysisRuleSetListBoxPointerPressed, RoutingStrategies.Tunnel);
        listBox.SelectionChanged += this.OnLogAnalysisRuleSetListBoxSelectionChanged;
    }


    // Setup expander of log analysis rule sets.
    void SetupLogAnalysisRuleSetsExpander(Expander expander)
    {
        // [Workaround] Force relayout to prevent items not showing
        expander.GetObservable(Expander.IsExpandedProperty).Subscribe(isExpanded =>
        {
            if (isExpanded)
                expander.Margin = new(-1);
        });
        // [Workaround] Force relayout to prevent items not showing
        expander.SizeChanged += (_, _) =>
        {
            if (expander.Margin.Left < 0)
                expander.Margin = this.Application.FindResourceOrDefault<Thickness>("Thickness/SessionView.LogAnalysisRuleSetsPopup.Group.Margin");
        };
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
        if (logProfile is null)
            return;
        var offset = scrollViewer.Offset;
        if (Math.Abs(offset.Y) < 0.5 && offset.Y + scrollViewer.Viewport.Height >= scrollViewer.Extent.Height)
            return;
        if (this.IsScrollingToLatestLogAnalysisResultNeeded)
        {
            if (logProfile.SortDirection == SortDirection.Ascending)
            {
                if (userScrollingDelta < 0)
                {
                    this.Logger.LogDebug("Cancel auto scrolling of log analysis result because of user scrolling up");
                    this.IsScrollingToLatestLogAnalysisResultNeeded = false;
                }
            }
            else
            {
                if (userScrollingDelta > 0)
                {
                    this.Logger.LogDebug("Cancel auto scrolling of log analysis result because of user scrolling down");
                    this.IsScrollingToLatestLogAnalysisResultNeeded = false;
                }
            }
        }
        else
        {
            if (logProfile.SortDirection == SortDirection.Ascending)
            {
                if (userScrollingDelta > 0 && Math.Abs(offset.Y + scrollViewer.Viewport.Height - scrollViewer.Extent.Height) <= 5)
                {
                    this.Logger.LogDebug("Start auto scrolling of log analysis result because of user scrolling down");
                    this.IsScrollingToLatestLogAnalysisResultNeeded = true;
                }
            }
            else
            {
                if (userScrollingDelta < 0 && offset.Y <= 5)
                {
                    this.Logger.LogDebug("Start auto scrolling of log analysis result because of user scrolling up");
                    this.IsScrollingToLatestLogAnalysisResultNeeded = true;
                }
            }
        }
    }
}