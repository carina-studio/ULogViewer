using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

partial class SessionView
{
    /// <summary>
    /// <see cref="IValueConverter"/> to convert from <see cref="FilterCombinationMode"/> to <see cref="Geometry"/> for toolbar icon.
    /// </summary>
    public static readonly IValueConverter LogFilterCombinationModeIconConverter = new FuncValueConverter<FilterCombinationMode, Geometry?>(mode =>
    {
        var app = App.CurrentOrNull;
        return mode switch
        {
            FilterCombinationMode.Intersection => app?.FindResourceOrDefault<Geometry>("Geometry/Intersection"),
            FilterCombinationMode.Union => app?.FindResourceOrDefault<Geometry>("Geometry/Union"),
            _ => app?.FindResourceOrDefault<Geometry>("Geometry/FilterCombinationMode.Auto"),
        };
    });
    
    
    // Constants.
    const int PredefinedLogTextFilterGroupControlFirstIndex = 2;


    // Static fields.
	static readonly StyledProperty<bool> CanFilterLogsByNonTextFiltersProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(CanFilterLogsByNonTextFilters), false);
    static readonly StyledProperty<bool> HasSelectedPredefinedLogTextFiltersProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(HasSelectedPredefinedLogTextFilters), false);
    static readonly SettingKey<bool> IsShowingHelpButtonOnLogTextFilterConfirmedKey = new("SessionView.IsShowingHelpButtonOnLogTextFilterConfirmed");
    static readonly StyledProperty<bool> ShowHelpButtonOnLogTextFilterProperty = AvaloniaProperty.Register<SessionView, bool>("ShowHelpButtonOnLogTextFilter");


    // Fields.
    readonly HashSet<PredefinedLogTextFilter> changingGroupSelectedPredefinedLogTextFilters = new();
    readonly Button clearLogTextFilterButton;
    readonly ScheduledAction commitLogFiltersAction;
    readonly MenuItem filterByLogPropertyMenuItem;
    readonly Button ignoreLogTextFilterCaseButton;
    bool isCommitingLogFilters;
    bool isShowingHelpButtonOnLogTextFilterConfirmationNeeded;
    readonly ToggleButton logFilterCombinationModeButton;
    readonly ContextMenu logFilterCombinationModeMenu;
    readonly Button logFilteringHelpButton;
    readonly ComboBox logLevelFilterComboBox;
    readonly IntegerTextBox logProcessIdFilterTextBox;
    readonly RegexTextBox logTextFilterTextBox;
	readonly IntegerTextBox logThreadIdFilterTextBox;
    IDataTemplate? predefinedLogTextFilterGroupDataTemplate;
    readonly Avalonia.Controls.ListBox predefinedLogTextFilterListBox;
    readonly Panel predefinedLogTextFiltersAndGroupsPanel;
    readonly ToggleButton predefinedLogTextFiltersButton;
    readonly Popup predefinedLogTextFiltersPopup;
    readonly HashSet<PredefinedLogTextFilter> selectedPredefinedLogTextFilters = new();
    readonly ScheduledAction updateLogTextFilterTextBoxClassesAction;


    // Attach to predefined log text filter
	void AttachToPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged += this.OnPredefinedLogTextFilterPropertyChanged;


    // Attach to predefined log text filters
	void AttachToPredefinedLogTextFilters()
    {
        var manager = PredefinedLogTextFilterManager.Default;
        foreach (var filter in PredefinedLogTextFilterManager.Default.Filters)
            this.AttachToPredefinedLogTextFilter(filter);
        var groups = manager.Groups;
        for (var i = 0; i < groups.Count; ++i)
            this.predefinedLogTextFiltersAndGroupsPanel.Children.Insert(PredefinedLogTextFilterGroupControlFirstIndex + i, this.CreateControl(groups[i]));
        ((INotifyCollectionChanged)manager.Groups).CollectionChanged += this.OnPredefinedLogTextFilterGroupsChanged;
        ((INotifyCollectionChanged)manager.Filters).CollectionChanged += this.OnPredefinedLogTextFiltersChanged;
    }


    // Check whether at least one non-text log filter is supported or not.
    public bool CanFilterLogsByNonTextFilters => 
        this.GetValue(CanFilterLogsByNonTextFiltersProperty);
    

    /// <summary>
    /// Clear predefined log text filter selection.
    /// </summary>
    public void ClearPredefinedLogTextFilterSelection()
    {
        foreach (var control in this.predefinedLogTextFiltersAndGroupsPanel.Children)
        {
            if (control is Avalonia.Controls.ListBox listBox)
                listBox.SelectedItems?.Clear();
            else
                control.FindLogicalDescendantOfType<Avalonia.Controls.ListBox>()?.SelectedItems?.Clear();
        }
        this.SetValue(HasSelectedPredefinedLogTextFiltersProperty, false);
        this.commitLogFiltersAction.Reschedule();
    }


    // Get delay of commit log filters.
	int CommitLogFilterParamsDelay => Math.Max(SettingKeys.MinUpdateLogFilterDelay, Math.Min(SettingKeys.MaxUpdateLogFilterDelay, this.Settings.GetValueOrDefault(SettingKeys.UpdateLogFilterDelay)));


    // Commit log filters to view-model.
    void CommitLogFilters()
    {
        // get session
        if (this.DataContext is not Session session)
            return;

        // update text filters
        this.isCommitingLogFilters = true;
        session.LogFiltering.PredefinedTextFilters.Clear();
        session.LogFiltering.PredefinedTextFilters.AddAll(this.selectedPredefinedLogTextFilters);
        this.isCommitingLogFilters = false;
    }


    // Confirm whether keep showing help button on log text filter or not.
    async void ConfirmShowingHelpButtonOnLogTextFilter()
    {
        if (this.PersistentState.GetValueOrDefault(IsShowingHelpButtonOnLogTextFilterConfirmedKey))
            return;
        if (this.attachedWindow == null)
            return;
        var dialog = new MessageDialog
        {
            Buttons = MessageDialogButtons.YesNo,
            DoNotAskOrShowAgain = true,
            Icon = MessageDialogIcon.Question,
            Message = this.Application.GetObservableString("SessionView.ConfirmShowingHelpButtonOnLogTextFilter"),
        };
        var result = await dialog.ShowDialog(this.attachedWindow);
        this.Settings.SetValue<bool>(SettingKeys.ShowHelpButtonOnLogTextFilter, result == MessageDialogResult.Yes);
        this.PersistentState.SetValue<bool>(IsShowingHelpButtonOnLogTextFilterConfirmedKey, dialog.DoNotAskOrShowAgain.GetValueOrDefault());
    }


    // Copy selected predefined log text filter.
    void CopyPredefinedLogTextFilter(PredefinedLogTextFilter filter)
    {
        if (this.attachedWindow == null)
            return;
        var newName = Utility.GenerateName(filter.Name, name => 
            PredefinedLogTextFilterManager.Default.Filters.FirstOrDefault(it => it.Name == name) != null);
        var newFilter = new PredefinedLogTextFilter(this.Application, newName, filter.Regex)
        {
            GroupName = filter.GroupName,
        };
        PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, newFilter, null);
    }


    /// <summary>
    /// Command to copy predefined log text filter.
    /// </summary>
    public ICommand CopyPredefinedLogTextFilterCommand { get; }


    // Create control for predefined log text filter group.
    Control CreateControl(PredefinedLogTextFilterGroup group)
    {
        if (this.predefinedLogTextFilterGroupDataTemplate is null)
        {
            var manager = PredefinedLogTextFilterManager.Default;
            this.predefinedLogTextFilterGroupDataTemplate = this.predefinedLogTextFiltersAndGroupsPanel.DataTemplates.First(it => it.Match(manager.DefaultGroup));
        }
        var control = this.predefinedLogTextFilterGroupDataTemplate.Build(group).AsNonNull();
        control.DataContext = group;
        control.FindLogicalDescendantOfType<Expander>(true)?.Let(expander =>
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
                    expander.Margin = this.Application.FindResourceOrDefault<Thickness>("Thickness/SessionView.PredefinedLogTextFilterGroup.Margin");
            };
            expander.FindLogicalDescendantOfType<Avalonia.Controls.ListBox>()?.Let(listBox =>
            {
                listBox.AddHandler(PointerPressedEvent, this.OnPredefinedLogTextFilterListBoxPointerPressed, RoutingStrategies.Tunnel);
                listBox.SelectionChanged += this.OnPredefinedLogTextFilterListBoxSelectionChanged;
            });
        });
        return control;
    }


    /// <summary>
    /// Create predefined log text filter.
    /// </summary>
    public void CreatePredefinedLogTextFilter()
    {
        if (this.attachedWindow == null)
            return;
        PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, null, this.logTextFilterTextBox.Object);
    }


    // Detach from predefined log text filter.
	void DetachFromPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged -= this.OnPredefinedLogTextFilterPropertyChanged;


    // Detach from predefined log text filters.
    void DetachFromPredefinedLogTextFilters()
    {
        var manager = PredefinedLogTextFilterManager.Default;
        ((INotifyCollectionChanged)manager.Groups).CollectionChanged -= this.OnPredefinedLogTextFilterGroupsChanged;
        ((INotifyCollectionChanged)manager.Filters).CollectionChanged -= this.OnPredefinedLogTextFiltersChanged;
        foreach (var filter in manager.Filters)
            this.DetachFromPredefinedLogTextFilter(filter);
        this.predefinedLogTextFiltersAndGroupsPanel.Children.Let(controls =>
        {
            for (var i = controls.Count - 1; i >= 0; --i)
            {
                var control = controls[i];
                if (control.DataContext is PredefinedLogTextFilterGroup)
                {
                    control.DataContext = null;
                    controls.RemoveAt(i);
                }
            }
        });
    }


    // Edit given predefined log text filter.
    void EditPredefinedLogTextFilter(PredefinedLogTextFilter? filter)
    {
        if (filter == null || this.attachedWindow == null)
            return;
        PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, filter, null);
    }


    /// <summary>
    /// Command to edit given predefined log text filter.
    /// </summary>
    public ICommand EditPredefinedLogTextFilterCommand { get; }


    /// <summary>
    /// Whether one or more predefined log text filters are selected or not.
    /// </summary>
    public bool HasSelectedPredefinedLogTextFilters => this.GetValue(HasSelectedPredefinedLogTextFiltersProperty);


    // Called when all log filters applied for filtering.
    void OnLogFiltersApplied(object? sender, EventArgs e)
    {
        // start scrolling to log around current position
        if ((sender as LogFilteringViewModel)?.IsFilteringNeeded == true)
            this.StartKeepingCurrentDisplayedLogRange();
    }
    
    
    // Called when list of predefined log text filters changed.
    void OnPredefinedLogTextFilterGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                e.NewItems!.Cast<PredefinedLogTextFilterGroup>().Let(groups =>
                {
                    var childControls = this.predefinedLogTextFiltersAndGroupsPanel.Children;
                    for (int i = 0, count = groups.Count; i < count; ++i)
                        childControls.Insert(PredefinedLogTextFilterGroupControlFirstIndex + e.NewStartingIndex + i, this.CreateControl(groups[i]));
                });
                break;
            case NotifyCollectionChangedAction.Remove:
                e.OldItems!.Cast<PredefinedLogTextFilterGroup>().Let(groups =>
                {
                    var childControls = this.predefinedLogTextFiltersAndGroupsPanel.Children;
                    for (var i = groups.Count - 1; i >= 0; --i)
                    {
                        childControls[e.OldStartingIndex + i].DataContext = null;
                        childControls.RemoveAt(PredefinedLogTextFilterGroupControlFirstIndex + e.OldStartingIndex + i);
                    }
                });
                break;
            default:
                throw new NotSupportedException();
        }
    }
    
    
    // Called when pointer pressed on list box of predefined log text filter.
    void OnPredefinedLogTextFilterListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
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
        foreach (var control in this.predefinedLogTextFiltersAndGroupsPanel.Children)
        {
            var listBox = control.FindLogicalDescendantOfType<Avalonia.Controls.ListBox>(true);
            if (listBox is null || ReferenceEquals(listBox, sender))
                continue;
            listBox.SelectedItems?.Clear();
        }
    }


    // Called when selection of list box of predefined log text filter has been changed.
    void OnPredefinedLogTextFilterListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var manager = PredefinedLogTextFilterManager.Default;
        foreach (var filter in e.RemovedItems.Cast<PredefinedLogTextFilter>())
        {
            if (manager.IsFilterGroupChanging(filter))
            {
                this.changingGroupSelectedPredefinedLogTextFilters.Add(filter);
                this.SynchronizationContext.Post(() =>
                {
                    if (this.changingGroupSelectedPredefinedLogTextFilters.Remove(filter))
                    {
                        var groupName = filter.GroupName;
                        if (groupName is null)
                            this.predefinedLogTextFilterListBox.SelectedItems?.Add(filter);
                        else
                        {
                            var listBox = this.predefinedLogTextFiltersAndGroupsPanel.Children.FirstOrDefault(it => 
                                it.DataContext is PredefinedLogTextFilterGroup group
                                && group.Name == groupName)?.FindLogicalDescendantOfType<Avalonia.Controls.ListBox>();
                            listBox?.SelectedItems?.Add(filter);
                        }
                        this.selectedPredefinedLogTextFilters.Add(filter);
                    }
                });
            }
            this.selectedPredefinedLogTextFilters.Remove(filter);
        }
        foreach (var filter in e.AddedItems.Cast<PredefinedLogTextFilter>())
            this.selectedPredefinedLogTextFilters.Add(filter);
        this.commitLogFiltersAction.Reschedule(this.CommitLogFilterParamsDelay);
        this.SetValue(HasSelectedPredefinedLogTextFiltersProperty, this.selectedPredefinedLogTextFilters.IsNotEmpty());
    }


    // Called when property of predefined log text filter has been changed.
    void OnPredefinedLogTextFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PredefinedLogTextFilter filter)
            return;
        switch (e.PropertyName)
        {
            case nameof(PredefinedLogTextFilter.Regex):
                if (this.predefinedLogTextFilterListBox.SelectedItems!.Contains(filter))
                    this.commitLogFiltersAction.Reschedule();
                break;
        }
    }


    // Called when list of predefined log text filters has been changed.
    void OnPredefinedLogTextFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                foreach (var filter in e.NewItems.AsNonNull().Cast<PredefinedLogTextFilter>())
                    this.AttachToPredefinedLogTextFilter(filter);
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var filter in e.OldItems.AsNonNull().Cast<PredefinedLogTextFilter>())
                {
                    this.DetachFromPredefinedLogTextFilter(filter);
                    this.changingGroupSelectedPredefinedLogTextFilters.Remove(filter);
                    this.selectedPredefinedLogTextFilters.Remove(filter);
                }
                break;
        }
    }


    // Called when selection of predefined log text filters has been changed.
    void OnSelectedPredefinedLogTextFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.isCommitingLogFilters)
            return;
        var isUpdateFilterScheduled = this.commitLogFiltersAction.IsScheduled;
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                this.predefinedLogTextFilterListBox.SelectedItems?.Let(selectedItems =>
                {
                    foreach (var filter in e.NewItems!.Cast<PredefinedLogTextFilter>())
                        selectedItems.Add(filter);
                });
                if (!isUpdateFilterScheduled)
                    this.commitLogFiltersAction.Cancel();
                break;
            case NotifyCollectionChangedAction.Remove:
                this.predefinedLogTextFilterListBox.SelectedItems?.Let(selectedItems =>
                {
                    foreach (var filter in e.OldItems!.Cast<PredefinedLogTextFilter>())
                        selectedItems.Remove(filter);
                });
                if (!isUpdateFilterScheduled)
                    this.commitLogFiltersAction.Cancel();
                break;
            case NotifyCollectionChangedAction.Reset:
                this.predefinedLogTextFilterListBox.SelectedItems?.Let(selectedItems =>
                {
                    selectedItems.Clear();
                    if (sender is IEnumerable<PredefinedLogTextFilter> filters)
                    {
                        foreach (var filter in filters)
                            selectedItems.Add(filter);
                    }
                });
                if (!isUpdateFilterScheduled)
                    this.commitLogFiltersAction.Cancel();
                break;
        }
    }


    // Called when setting 'ShowHelpButtonOnLogTextFilter' changed.
    void OnShowHelpButtonOnLogTextFilterSettingChanged(bool value)
    {
        if (value)
            this.SetValue(ShowHelpButtonOnLogTextFilterProperty, true);
        else
        {
            this.SetValue(ShowHelpButtonOnLogTextFilterProperty, false);
            this.isShowingHelpButtonOnLogTextFilterConfirmationNeeded = false;
            this.PersistentState.SetValue<bool>(IsShowingHelpButtonOnLogTextFilterConfirmedKey, true);
        }
    }


    // Called when setting 'UpdateLogFilterDelay' changed.
    void OnUpdateLogFilterDelaySettingChanged() =>
        this.logTextFilterTextBox.ValidationDelay = this.CommitLogFilterParamsDelay;


    /// <summary>
    /// Open online documentation.
    /// </summary>
    public void OpenLogFilteringDocumentation()
    {
        if (!Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogFiltering"))
            return;
        this.SynchronizationContext.PostDelayed(() =>
        {
            if (this.attachedWindow?.IsActive == true)
            {
                this.isShowingHelpButtonOnLogTextFilterConfirmationNeeded = false;
                this.ConfirmShowingHelpButtonOnLogTextFilter();
            }
            else
                this.isShowingHelpButtonOnLogTextFilterConfirmationNeeded = true;
        }, 1000);
    }


    /// <summary>
    /// Open online documentation.
    /// </summary>
#pragma warning disable CA1822
    public void OpenPredefinedTextFiltersDocumentation() =>
        Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogFiltering#PredefinedTextFilters");
#pragma warning restore CA1822


    /// <summary>
    /// Definition set of regex syntax highlighting.
    /// </summary>
    public SyntaxHighlightingDefinitionSet RegexSyntaxHighlightingDefinitionSet { get; }


    // Remove given predefined log text filter.
#pragma warning disable CA1822
    void RemovePredefinedLogTextFilter(PredefinedLogTextFilter? filter)
    {
        if (filter == null)
            return;
        PredefinedLogTextFilterManager.Default.RemoveFilter(filter);
    }
#pragma warning restore CA1822


    /// <summary>
    /// Command to remove given predefined log text filter.
    /// </summary>
    public ICommand RemovePredefinedLogTextFilterCommand { get; }


    /// <summary>
    /// Show menu to select log filter combination mode.
    /// </summary>
    public void ShowLogFiltersCombinationModeMenu() =>
        this.logFilterCombinationModeMenu.Open(this.logFilterCombinationModeButton);
    

    // Sync log text filters back to control.
    void SyncLogTextFiltersBack()
    {
        if (this.DataContext is not Session session)
            return;
        if (session.LogFiltering.PredefinedTextFilters.IsNotEmpty())
        {
            this.SynchronizationContext.Post(() =>
            {
                foreach (var textFilter in session.LogFiltering.PredefinedTextFilters)
                {
                    var groupName = textFilter.GroupName;
                    if (groupName is null)
                        this.predefinedLogTextFilterListBox.SelectedItems?.Add(textFilter);
                    else
                    {
                        var listBox = this.predefinedLogTextFiltersAndGroupsPanel.Children.FirstOrDefault(it => 
                            it.DataContext is PredefinedLogTextFilterGroup group
                            && group.Name == groupName)?.FindLogicalDescendantOfType<Avalonia.Controls.ListBox>();
                        listBox?.SelectedItems?.Add(textFilter);
                    }
                    this.selectedPredefinedLogTextFilters.Add(textFilter);
                }
                this.commitLogFiltersAction.Cancel();
            });
        }
    }


    // Update CanFilterLogsByNonTextFilters property.
    void UpdateCanFilterLogsByNonTextFilters()
    {
        if (this.DataContext is Session session)
        {
            this.SetValue(CanFilterLogsByNonTextFiltersProperty, this.validLogLevels.Count > 1 
                || session.LogFiltering.IsProcessIdFilterEnabled 
                || session.LogFiltering.IsThreadIdFilterEnabled);
        }
        else
            this.SetValue(CanFilterLogsByNonTextFiltersProperty, false);
    }
    

    // [Workaround] Force update content shown in combobox.
    void UpdateLogLevelFilterComboBoxStrings()
    {
        var isScheduled = this.commitLogFiltersAction.IsScheduled;
        var selectedIndex = this.logLevelFilterComboBox.SelectedIndex;
        if (selectedIndex > 0)
            this.logLevelFilterComboBox.SelectedIndex = 0;
        else
            this.logLevelFilterComboBox.SelectedIndex = 1;
        this.logLevelFilterComboBox.SelectedIndex = selectedIndex;
        if (!isScheduled)
            this.commitLogFiltersAction.Cancel();
    }
    

    // Update classes of log text filter text box.
    void UpdateLogTextFilterTextBoxClasses()
    {
        var visibleActions = 0;
        if (this.clearLogTextFilterButton.IsEffectivelyVisible)
            ++visibleActions;
        if (this.ignoreLogTextFilterCaseButton.IsEffectivelyVisible)
            ++visibleActions;
        if (this.logFilteringHelpButton.IsEffectivelyVisible)
            ++visibleActions;
        var className = visibleActions switch
        {
            0 => null,
            1 => "WithInPlaceAction",
            _ => $"With{visibleActions}InPlaceActions",
        };
        if (className == null)
            this.logTextFilterTextBox.Classes.Clear();
        else if (!this.logTextFilterTextBox.Classes.Contains(className))
        {
            this.logTextFilterTextBox.Classes.Clear();
            this.logTextFilterTextBox.Classes.Add(className);
        }
    }
}