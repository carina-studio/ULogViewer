using CarinaStudio.Collections;
using CarinaStudio.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace CarinaStudio.ULogViewer.ViewModels.Categorizing;

/// <summary>
/// Base implementation of <see cref="IDisplayableLogCategorizer{TCategory}"/>.
/// </summary>
abstract class BaseDisplayableLogCategorizer<TProcessingToken, TCategory> : BaseDisplayableLogProcessor<TProcessingToken, TCategory>, IDisplayableLogCategorizer<TCategory> where TProcessingToken : class where TCategory : DisplayableLogCategory
{
    // Fields.
    readonly SortedObservableList<TCategory> categories;
    long categoryMemorySize;


    /// <summary>
    /// Initialize new <see cref="BaseDisplayableLogCategorizer{TProcessingToken, TCategory}"/> instance.
    /// </summary>
    /// <param name="app">Application.</param>
    /// <param name="sourceLogs">Source list of logs.</param>
    /// <param name="comparison"><see cref="Comparison{T}"/> which used on <paramref name="sourceLogs"/>.</param>
    /// <param name="priority">Priority of logs processing.</param>
    protected BaseDisplayableLogCategorizer(IULogViewerApplication app, IList<DisplayableLog> sourceLogs, Comparison<DisplayableLog> comparison, DisplayableLogProcessingPriority priority = DisplayableLogProcessingPriority.Realtime) : base(app, sourceLogs, comparison, priority)
    { 
        this.categories = new((lhs, rhs) => this.CompareSourceLogs(lhs.Log, rhs.Log));
        this.Categories = (IReadOnlyList<TCategory>)ListExtensions.AsReadOnly(this.categories);
    }


    /// <inheritdoc/>
    public IReadOnlyList<TCategory> Categories { get; }


    /// <summary>
    /// Invalidate and update name of all categories.
    /// </summary>
    protected void InvalidateCategoryNames()
    {
        foreach (var category in this.categories)
            category.InvalidateName();
    }


    /// <inheritdoc/>
    public override long MemorySize 
    { 
        get => base.MemorySize 
            + Memory.EstimateCollectionInstanceSize(IntPtr.Size, this.categories.Count)
            + this.categoryMemorySize; 
    }


    /// <inheritdoc/>
    protected override void OnChunkProcessed(TProcessingToken token, List<DisplayableLog> logs, List<TCategory> results)
    {
        if (results.IsEmpty())
            return;
        for (var i = results.Count - 1; i >= 0; --i)
            this.categoryMemorySize += results[i].MemorySize;
        this.categories.AddAll(results, true);
        this.OnPropertyChanged(nameof(MemorySize));
    }


    /// <inheritdoc/>
    protected override bool OnLogInvalidated(DisplayableLog log)
    {
        var index = this.categories.BinarySearch<TCategory, DisplayableLog?>(log, it => it.Log, this.CompareSourceLogs);
        if (index >= 0)
        {
            this.RemoveCategory(index);
            this.InvalidateProcessing();
        }
        return false;
    }


    /// <inheritdoc/>
    protected override void OnProcessingCancelled(TProcessingToken token, bool willStartProcessing)
    {
        this.categories.Clear();
        if (!willStartProcessing && this.MemoryUsagePolicy != MemoryUsagePolicy.BetterPerformance)
            this.categories.TrimExcess();
        if (this.categoryMemorySize != 0L)
        {
            this.categoryMemorySize = 0L;
            this.OnPropertyChanged(nameof(MemorySize));
        }
    }


    /// <inheritdoc/>
    protected override void OnSourceLogsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnSourceLogsChanged(e);
        if (e.Action == NotifyCollectionChangedAction.Remove)
        {
            var categoriesRemoved = false;
            foreach (var log in e.OldItems!.Cast<DisplayableLog>())
            {
                var index = this.categories.BinarySearch<TCategory, DisplayableLog?>(log, it => it.Log, this.CompareSourceLogs);
                if (index >= 0)
                {
                    this.RemoveCategory(index);
                    categoriesRemoved = true;
                }
            }
            if (categoriesRemoved)
                this.InvalidateProcessing();
        }
    }


    // Remove category.
    void RemoveCategory(int index)
    {
        this.categoryMemorySize -= this.categories[index].MemorySize;
        this.categories.RemoveAt(index);
        this.OnPropertyChanged(nameof(MemorySize));
    }


    /// <summary>
    /// Remove existing category.
    /// </summary>
    /// <param name="category">Category to be removed.</param>
    /// <returns>True if category has been removed successfully.</returns>
    protected bool RemoveCategory(TCategory category)
    {
        var index = this.categories.IndexOf(category);
        if (index >= 0)
        {
            this.RemoveCategory(index);
            return true;
        }
        return false;
    }
}