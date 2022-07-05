using CarinaStudio.AppSuite.Converters;
using System;

namespace CarinaStudio.ULogViewer.Converters
{
    /// <summary>
    /// Set of converters for enumerations.
    /// </summary>
    static class EnumConverters
    {
        /// <summary>
        /// Converter for <see cref="ViewModels.Analysis.DisplayableLogAnalysisResultType"/> to string.
        /// </summary>
        public static readonly EnumConverter DisplayableLogAnalysisResultType = new EnumConverter(App.Current, typeof(ViewModels.Analysis.DisplayableLogAnalysisResultType));
        /// <summary>
        /// Converter for <see cref="Logs.LogLevel"/> to string.
        /// </summary>
        public static readonly EnumConverter LogLevel = new EnumConverter(App.Current, typeof(Logs.LogLevel));
    }
}
