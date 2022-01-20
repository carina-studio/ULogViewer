using Avalonia;
using Avalonia.Media;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Extended <see cref="Avalonia.Controls.TextBlock"/>.
    /// </summary>
    class TextBlock : Avalonia.Controls.TextBlock
    {
        /// <summary>
        /// Property of <see cref="IsTextTrimmed"/>.
        /// </summary>
        public static readonly AvaloniaProperty<bool> IsTextTrimmedProperty = AvaloniaProperty.Register<TextBlock, bool>(nameof(IsTextTrimmed));


        /// <summary>
        /// Initialize new <see cref="TextBlock"/> instance.
        /// </summary>
        public TextBlock()
        { }


        /// <summary>
        /// Check whether text inside the <see cref="TextBlock"/> has been trimmed or not.
        /// </summary>
        public bool IsTextTrimmed { get => this.GetValue<bool>(IsTextTrimmedProperty); }


        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            var measuredSize = base.MeasureOverride(availableSize);
            if (double.IsFinite(availableSize.Width) && this.TextTrimming != TextTrimming.None)
            {
                if (this.TextWrapping == TextWrapping.NoWrap)
                {
                    var minSize = base.MeasureOverride(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    this.SetValue<bool>(IsTextTrimmedProperty, minSize.Width > measuredSize.Width);
                }
                else
                {
                    var minSize = base.MeasureOverride(new Size(availableSize.Width, double.PositiveInfinity));
                    this.SetValue<bool>(IsTextTrimmedProperty, minSize.Height > measuredSize.Height);
                }
            }
            return base.MeasureOverride(availableSize);
        }


        /// <inheritdoc/>
        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == TextTrimmingProperty 
                && ((TextTrimming)((object?)change.NewValue.Value).AsNonNull()) == TextTrimming.None)
            {
                this.SetValue<bool>(IsTextTrimmedProperty, false);
            }
        }
    }
}
