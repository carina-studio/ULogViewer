using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace CarinaStudio.ULogViewer.ViewModels;

/// <summary>
/// Filter on list of <see cref="DisplayableLog"/>.
/// </summary>
interface IDisplayableLogFilter : IDisplayableLogProcessor
{
    /// <summary>
    /// Get list of filtered logs.
    /// </summary>
    /// <remarks>The list implements <see cref="INotifyCollectionChanged"/> interface.</remarks>
    IList<DisplayableLog> FilteredLogs { get; }
}