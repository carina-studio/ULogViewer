using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user select precondition of log reading.
    /// </summary>
    partial class LogReadingPreconditionDialog : AppSuite.Controls.InputDialog
    {
        /// <summary>
        /// Log reading precondition.
        /// </summary>
        public struct LogReadingPrecondition
        {
            public DateTime? BeginningTimestamp;
            public DateTime? EndingTimestamp;
        }


        // Fields.
        readonly DateTimeTextBox beginningTimestampTextBox;
        readonly DateTimeTextBox endingTimestampTextBox;
        readonly RadioButton noPreconditionRadioButton;
        readonly RadioButton timestampsRadioButton;


        // Constructor.
        public LogReadingPreconditionDialog()
        {
            AvaloniaXamlLoader.Load(this);
            this.beginningTimestampTextBox = this.FindControl<DateTimeTextBox>(nameof(beginningTimestampTextBox)).Also(it =>
            {
                it.GetObservable(DateTimeTextBox.IsTextValidProperty).Subscribe(_ => this.InvalidateInput());
                it.GetObservable(DateTimeTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
            });
            this.endingTimestampTextBox = this.FindControl<DateTimeTextBox>(nameof(endingTimestampTextBox)).Also(it =>
            {
                it.GetObservable(DateTimeTextBox.IsTextValidProperty).Subscribe(_ => this.InvalidateInput());
                it.GetObservable(DateTimeTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
            });
            this.noPreconditionRadioButton = this.FindControl<RadioButton>(nameof(noPreconditionRadioButton)).Also(it =>
            {
                it.GetObservable(RadioButton.IsCheckedProperty).Subscribe(_ => this.InvalidateInput());
            });
            this.timestampsRadioButton = this.FindControl<RadioButton>(nameof(timestampsRadioButton)).Also(it =>
            {
                it.GetObservable(RadioButton.IsCheckedProperty).Subscribe(_ => this.InvalidateInput());
            });
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)new LogReadingPrecondition().Also(it =>
            {
                if (this.noPreconditionRadioButton.IsChecked == true)
                    return;
                if (this.timestampsRadioButton.IsChecked == true)
                {
                    it.BeginningTimestamp = this.beginningTimestampTextBox.Value;
                    it.EndingTimestamp = this.endingTimestampTextBox.Value;
                }
            }));


        // Window opened
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            var precondition = this.Precondition;
            if (precondition.BeginningTimestamp.HasValue || precondition.EndingTimestamp.HasValue)
            {
                this.timestampsRadioButton.IsChecked = true;
                this.beginningTimestampTextBox.Value = precondition.BeginningTimestamp;
                this.endingTimestampTextBox.Value = precondition.EndingTimestamp;
            }
            else
            {
                var timestamp = DateTime.Now;
                this.noPreconditionRadioButton.IsChecked = true;
                this.beginningTimestampTextBox.Value = timestamp - TimeSpan.FromDays(365);
                this.endingTimestampTextBox.Value = timestamp;
            }
        }


        // Validate input.
        protected override bool OnValidateInput()
        {
            if (!base.OnValidateInput())
                return false;
            if (this.noPreconditionRadioButton.IsChecked == true)
                return true;
            if (this.timestampsRadioButton.IsChecked == true)
            {
                if (this.beginningTimestampTextBox.IsTextValid && this.beginningTimestampTextBox.Value.HasValue)
                    return true;
                if (this.endingTimestampTextBox.IsTextValid && this.endingTimestampTextBox.Value.HasValue)
                    return true;
                return false;
            }
            return false;
        }
        

        // Precondition.
        public LogReadingPrecondition Precondition { get; set; }
    }
}
