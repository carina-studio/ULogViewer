using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
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


    // Static fields.
	static readonly StyledProperty<bool> CanFilterLogsByNonTextFiltersProperty = AvaloniaProperty.Register<SessionView, bool>(nameof(CanFilterLogsByNonTextFilters), false);
    static readonly SettingKey<bool> IsShowingHelpButtonOnLogTextFilterConfirmedKey = new("SessionView.IsShowingHelpButtonOnLogTextFilterConfirmed");
    static readonly StyledProperty<bool> ShowHelpButtonOnLogTextFilterProperty = AvaloniaProperty.Register<SessionView, bool>("ShowHelpButtonOnLogTextFilter");


    // Fields.
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
    readonly Avalonia.Controls.ListBox predefinedLogTextFilterListBox;
    readonly SortedObservableList<PredefinedLogTextFilter> predefinedLogTextFilters;
    readonly ToggleButton predefinedLogTextFiltersButton;
    readonly Popup predefinedLogTextFiltersPopup;
    readonly HashSet<PredefinedLogTextFilter> selectedPredefinedLogTextFilters = new();
    readonly ScheduledAction updateLogTextFilterTextBoxClassesAction;


    // Attach to predefined log text filter
	void AttachToPredefinedLogTextFilter(PredefinedLogTextFilter filter) => filter.PropertyChanged += this.OnPredefinedLogTextFilterPropertyChanged;


    // Attach to predefined log text filters
	void AttachToPredefinedLogTextFilters()
    {
        this.predefinedLogTextFilters.AddAll(PredefinedLogTextFilterManager.Default.Filters);
        foreach (var filter in PredefinedLogTextFilterManager.Default.Filters)
            this.AttachToPredefinedLogTextFilter(filter);
        ((INotifyCollectionChanged)PredefinedLogTextFilterManager.Default.Filters).CollectionChanged += this.OnPredefinedLogTextFiltersChanged;
    }


    // Check whether at least one non-text log filter is supported or not.
    public bool CanFilterLogsByNonTextFilters => 
        this.GetValue(CanFilterLogsByNonTextFiltersProperty);
    

    /// <summary>
    /// Clear predefined log text filter selection.
    /// </summary>
    public void ClearPredefinedLogTextFilterSelection()
    {
        this.predefinedLogTextFilterListBox.SelectedItems?.Clear();
        this.commitLogFiltersAction.Reschedule();
    }


    // Get delay of commit log filters.
	int CommitLogFilterParamsDelay { get => Math.Max(SettingKeys.MinUpdateLogFilterDelay, Math.Min(SettingKeys.MaxUpdateLogFilterDelay, this.Settings.GetValueOrDefault(SettingKeys.UpdateLogFilterDelay))); }


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


    // Compare predefined log text filters.
    static int ComparePredefinedLogTextFilters(PredefinedLogTextFilter? x, PredefinedLogTextFilter? y)
    {
        if (x == null)
        {
            if (y == null)
                return 0;
            return -1;
        }
        if (y == null)
            return 1;
        var result = string.Compare(x.Name, y.Name, true, CultureInfo.InvariantCulture);
        if (result != 0)
            return result;
        return x.GetHashCode() - y.GetHashCode();
    }


    // Confirm whether keep showing help button on log text filter or not.
    async void ConfirmShowingHelpButtonOnLogTextFilter()
    {
        if (this.PersistentState.GetValueOrDefault(IsShowingHelpButtonOnLogTextFilterConfirmedKey))
            return;
        if (this.attachedWindow == null)
            return;
        var dialog = new MessageDialog()
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
        var newFilter = new PredefinedLogTextFilter(this.Application, newName, filter.Regex);
        PredefinedLogTextFilterEditorDialog.Show(this.attachedWindow, newFilter, null);
    }


    /// <summary>
    /// Command to copy predefined log text filter.
    /// </summary>
    public ICommand CopyPredefinedLogTextFilterCommand { get; }


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
        ((INotifyCollectionChanged)PredefinedLogTextFilterManager.Default.Filters).CollectionChanged -= this.OnPredefinedLogTextFiltersChanged;
        foreach (var filter in this.predefinedLogTextFilters)
            this.DetachFromPredefinedLogTextFilter(filter);
        this.predefinedLogTextFilters.Clear();
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


    // Called when selection of list box of predefined log text filter has been changed.
    void OnPredefinedLogTextFilterListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var filter in e.RemovedItems.Cast<PredefinedLogTextFilter>())
            this.selectedPredefinedLogTextFilters.Remove(filter);
        foreach (var filter in e.AddedItems.Cast<PredefinedLogTextFilter>())
            this.selectedPredefinedLogTextFilters.Add(filter);
        if (this.selectedPredefinedLogTextFilters.Count != this.predefinedLogTextFilterListBox.SelectedItems!.Count)
        {
            // [Workaround] Need to sync selection back to control because selection will be cleared when popup opened
            if (this.selectedPredefinedLogTextFilters.IsNotEmpty())
            {
                var isScheduled = this.commitLogFiltersAction?.IsScheduled ?? false;
                this.selectedPredefinedLogTextFilters.ToArray().Let(it =>
                {
                    this.SynchronizationContext.Post(() =>
                    {
                        this.predefinedLogTextFilterListBox.SelectedItems!.Clear();
                        foreach (var filter in it)
                            this.predefinedLogTextFilterListBox.SelectedItems.Add(filter);
                        if (!isScheduled)
                            this.commitLogFiltersAction?.Cancel();
                    });
                });
            }
        }
        else
            this.commitLogFiltersAction.Reschedule(this.CommitLogFilterParamsDelay);
    }


    // Called when property of predefined log text filter has been changed.
    void OnPredefinedLogTextFilterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not PredefinedLogTextFilter filter)
            return;
        switch (e.PropertyName)
        {
            case nameof(PredefinedLogTextFilter.Name):
                this.predefinedLogTextFilters.Sort(filter);
                break;
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
                {
                    this.AttachToPredefinedLogTextFilter(filter);
                    this.predefinedLogTextFilters.Add(filter);
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                foreach (var filter in e.OldItems.AsNonNull().Cast<PredefinedLogTextFilter>())
                {
                    this.DetachFromPredefinedLogTextFilter(filter);
                    this.predefinedLogTextFilters.Remove(filter);
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
    /// Sorted predefined log text filters.
    /// </summary>
    public IList<PredefinedLogTextFilter> PredefinedLogTextFilters { get => this.predefinedLogTextFilters; }


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
                    this.predefinedLogTextFilterListBox.SelectedItems!.Add(textFilter);
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