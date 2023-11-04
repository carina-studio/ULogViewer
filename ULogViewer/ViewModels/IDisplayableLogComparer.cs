using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Comparer of <see cref="DisplayableLog"/>.
/// </summary>
interface IDisplayableLogComparer : IComparer<DisplayableLog>, IEquatable<IDisplayableLogComparer>
{
    /// <summary>
    /// Get direction of sorting <see cref="DisplayableLog"/>.
    /// </summary>
    SortDirection SortDirection { get; }
}

/// <summary>
/// Default implementation of <see cref="IDisplayableLogComparer"/>.
/// </summary>
class DisplayableLogComparer : IDisplayableLogComparer
{
    // Fields.
    readonly Comparison<DisplayableLog> comparison;
    readonly SortDirection sortDirection;


    /// <summary>
    /// Initialize new <see cref="DisplayableLogComparer"/> instance.
    /// </summary>
    /// <param name="comparison">Comparison to compare <see cref="DisplayableLog"/>.</param>
    /// <param name="sortDirection">Direction of sorting <see cref="DisplayableLog"/>.</param>
    public DisplayableLogComparer(Comparison<DisplayableLog> comparison, SortDirection sortDirection)
    {
        this.comparison = comparison;
        this.sortDirection = sortDirection;
    }


    /// <inheritdoc/>
    public int Compare(DisplayableLog? lhs, DisplayableLog? rhs)
    {
        if (lhs is null)
        {
            if (rhs is null)
                return 0;
            return -1;
        }
        if (rhs is null)
            return 1;
        return this.comparison(lhs, rhs);
    }


    /// <inheritdoc/>
    public bool Equals(IDisplayableLogComparer? other) =>
        other is DisplayableLogComparer comparer
        && comparer.comparison == this.comparison
        && comparer.sortDirection == this.sortDirection;


    /// <inheritdoc/>
    public SortDirection SortDirection => this.sortDirection;
}