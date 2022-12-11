using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user input datetime format.
    /// </summary>
    partial class DateTimeFormatInputDialog : InputDialog
    {
        // Static fields.
        static readonly DirectProperty<DateTimeFormatInputDialog, string?> SampleResultProperty = AvaloniaProperty.RegisterDirect<DateTimeFormatInputDialog, string?>("SampleResult", d => d.sampleResult);


        // Fields.
        readonly DateTimeFormatTextBox formatTextBox;
        readonly DateTime sampleDateTime = DateTime.Now;
        string? sampleResult;
        readonly ScheduledAction updateSampleResultAction;


        // Constructor.
        public DateTimeFormatInputDialog()
        {
			AvaloniaXamlLoader.Load(this);
            this.formatTextBox = this.Get<DateTimeFormatTextBox>(nameof(formatTextBox));
            this.updateSampleResultAction = new(() =>
            {
                var format = this.formatTextBox.Object;
                if (!this.formatTextBox.IsTextValid || string.IsNullOrEmpty(format))
                    this.SetAndRaise(SampleResultProperty, ref this.sampleResult, null);
                else
                    this.SetAndRaise(SampleResultProperty, ref this.sampleResult, this.sampleDateTime.ToString(format));
            });
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)formatTextBox.Text);


        // Initial datetime format to show.
        public string? InitialFormat { get; set; }


        // Property of format textbox changed.
        void OnFormatTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DateTimeFormatTextBox.IsTextValidProperty
                || e.Property == DateTimeFormatTextBox.ObjectProperty)
            {
                this.InvalidateInput();
                this.updateSampleResultAction.Schedule();
            }
        }


        // Key up.
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (!e.Handled && e.Source == this.formatTextBox && e.Key == Key.Enter)
                this.GenerateResultCommand.TryExecute();
        }


        // Dialog opened.
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            this.formatTextBox.Text = this.InitialFormat;
            this.updateSampleResultAction.Execute();
            this.SynchronizationContext.Post(_ =>
            {
                this.formatTextBox.Focus();
                this.formatTextBox.SelectAll();
            }, null);
        }


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() 
            && this.formatTextBox.IsTextValid
            && !string.IsNullOrEmpty(this.formatTextBox.Object);
    }
}
