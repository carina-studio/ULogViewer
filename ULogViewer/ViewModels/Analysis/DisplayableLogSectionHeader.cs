namespace CarinaStudio.ULogViewer.ViewModels.Analysis;

/// <summary>
/// Header of section of <see cref="DisplayableLog"/>.
/// </summary>
class DisplayableLogSectionHeader : DisplayableLogAnalysisResult
{
    /// <summary>
    /// Initialize new <see cref="DisplayableLogSectionHeader"/> instance.
    /// </summary>
    /// <param name="message">Message.</param>
    public DisplayableLogSectionHeader(string? message = null) : base(message)
    { }


    /// <inheritdoc/>
    public override DisplayableLogAnalysisResult Clone() =>
        new DisplayableLogSectionHeader(this.Message);
}