using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.Windows.Input;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to select <see cref="DateTime"/>.
    /// </summary>
    partial class DateTimeSelectionDialog : AppSuite.Controls.InputDialog
    {
        // Fields.
        readonly DateTimeTextBox dateTimeTextBox;


        /// <summary>
        /// Initialize new <see cref="DateTimeSelectionDialog"/> instance.
        /// </summary>
        public DateTimeSelectionDialog()
        {
            AvaloniaXamlLoader.Load(this);
            this.dateTimeTextBox = this.Get<DateTimeTextBox>(nameof(dateTimeTextBox));
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)this.dateTimeTextBox.Value.GetValueOrDefault());


        // Initial date time to show.
        public DateTime? InitialDateTime { get; set; }


        // Message to show.
        public string? Message { get; set; }


        // Selected date changed.
        void OnDateTimeTextBoxPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == DateTimeTextBox.IsTextValidProperty || e.Property == DateTimeTextBox.ValueProperty)
                this.InvalidateInput();
        }


        /// <inheritdoc/>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyModifiers == 0 && e.Key == Key.Enter && e.Source == this.dateTimeTextBox && !e.Handled)
            {
                e.Handled = true;
                this.GenerateResultCommand.TryExecute();
            }
        }


        // Dialog opened.
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            this.FindControl<Avalonia.Controls.TextBlock>("messageTextBlock").AsNonNull().Text = this.Message ?? " ";
            this.InitialDateTime?.Let(it =>
            {
                this.dateTimeTextBox.Value = it;
            });
            this.SynchronizationContext.Post(_ =>
            {
                this.dateTimeTextBox.Focus();
                this.dateTimeTextBox.SelectAll();
            }, null);
        }


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() && this.dateTimeTextBox.IsTextValid && this.dateTimeTextBox.Value.HasValue;
    }
}
