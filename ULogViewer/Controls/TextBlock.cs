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
        /// Property of <see cref="IsMultiLineText"/>.
        /// </summary>
        public static readonly AvaloniaProperty<bool> IsMultiLineTextProperty = AvaloniaProperty.RegisterDirect<TextBlock, bool>(nameof(IsMultiLineText), v => v.isMultiLineText);
        /// <summary>
        /// Property of <see cref="IsTextTrimmed"/>.
        /// </summary>
        public static readonly AvaloniaProperty<bool> IsTextTrimmedProperty = AvaloniaProperty.RegisterDirect<TextBlock, bool>(nameof(IsTextTrimmed), v => v.isTextTrimmed);


        // Fields.
        bool isMultiLineText;
        bool isTextTrimmed;


        /// <summary>
        /// Initialize new <see cref="TextBlock"/> instance.
        /// </summary>
        public TextBlock()
        { }


        /// <summary>
        /// Check whether text inside the <see cref="TextBlock"/> has multiple lines or not.
        /// </summary>
        public bool IsMultiLineText { get => this.isMultiLineText; }


        /// <summary>
        /// Check whether text inside the <see cref="TextBlock"/> has been trimmed or not.
        /// </summary>
        public bool IsTextTrimmed { get => this.isTextTrimmed; }


        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
        {
            var measuredSize = base.MeasureOverride(availableSize);
            if (double.IsFinite(availableSize.Width))
            {
                // check multi line
                var text = this.GetValue<string?>(TextProperty);
                if (string.IsNullOrEmpty(text))
                    this.SetAndRaise<bool>(IsMultiLineTextProperty, ref this.isMultiLineText, false);
                else if (text.IndexOf('\n') >= 0)
                    this.SetAndRaise<bool>(IsMultiLineTextProperty, ref this.isMultiLineText, true);
                else if (this.TextWrapping == TextWrapping.NoWrap)
                    this.SetAndRaise<bool>(IsMultiLineTextProperty, ref this.isMultiLineText, false);
                else
                {
                    var minSize = base.MeasureOverride(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    this.SetAndRaise<bool>(IsMultiLineTextProperty, ref this.isMultiLineText, measuredSize.Height > minSize.Height + 0.1);
                }

                // check trimming
                if (this.TextTrimming != TextTrimming.None)
                {
                    if (this.TextWrapping == TextWrapping.NoWrap)
                    {
                        var minSize = base.MeasureOverride(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        this.SetAndRaise<bool>(IsTextTrimmedProperty, ref this.isTextTrimmed, minSize.Width > measuredSize.Width);
                    }
                    else
                    {
                        var minSize = base.MeasureOverride(new Size(availableSize.Width, double.PositiveInfinity));
                        this.SetAndRaise<bool>(IsTextTrimmedProperty, ref this.isTextTrimmed, minSize.Height > measuredSize.Height);
                    }
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
