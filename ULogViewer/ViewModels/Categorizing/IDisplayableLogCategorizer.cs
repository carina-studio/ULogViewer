using System;
using System.Collections.Generic;

namespace CarinaStudio.ULogViewer.ViewModels.Categorizing;

/// <summary>
/// Component to categorize <see cref="DisplayableLog"/>.
/// </summary>
/// <typeparam name="TCategory">Type of category.</typeparam>
interface IDisplayableLogCategorizer<out TCategory> : IDisplayableLogProcessor where TCategory : DisplayableLogCategory
{
    /// <summary>
    /// Get list of categories.
    /// </summary>
    IReadOnlyList<TCategory> Categories { get; }
}