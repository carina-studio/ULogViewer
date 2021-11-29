using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to select <see cref="DateTime"/>.
    /// </summary>
    partial class DateTimeSelectionDialog : InputDialog
    {
        // Fields.
        readonly AppSuite.Controls.DatePicker datePicker;
        readonly NumericUpDown hoursUpDown;
        readonly NumericUpDown minutesUpDown;
        readonly NumericUpDown secondsUpDown;


        /// <summary>
        /// Initialize new <see cref="DateTimeSelectionDialog"/> instance.
        /// </summary>
        public DateTimeSelectionDialog()
        {
            InitializeComponent();
            this.datePicker = this.FindControl<AppSuite.Controls.DatePicker>(nameof(datePicker));
            this.hoursUpDown = this.FindControl<NumericUpDown>(nameof(hoursUpDown));
            this.minutesUpDown = this.FindControl<NumericUpDown>(nameof(minutesUpDown));
            this.secondsUpDown = this.FindControl<NumericUpDown>(nameof(secondsUpDown));
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
        {
            var date = this.datePicker.SelectedDate.GetValueOrDefault().Date;
            return Task.FromResult((object?)new DateTime(date.Year, date.Month, date.Day, (int)this.hoursUpDown.Value, (int)this.minutesUpDown.Value, (int)this.secondsUpDown.Value));
        }


        // Initial date time to show.
        public DateTime? InitialDateTime { get; set; }


        // Initialize.
        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


        // Message to show.
        public string? Message { get; set; }


        // Selected date changed.
        void OnDatePickerSelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e) =>
            this.InvalidateInput();


        // Dialog opened.
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            this.FindControl<Avalonia.Controls.TextBlock>("messageTextBlock").AsNonNull().Text = this.Message ?? " ";
            this.InitialDateTime?.Let(it =>
            {
                this.datePicker.SelectedDate = new DateTimeOffset(it.Date);
                this.hoursUpDown.Value = it.Hour;
                this.minutesUpDown.Value = it.Minute;
                this.secondsUpDown.Value = it.Second;
            });
        }


        // Selected time changed.
        void OnTimePickerSelectedTimeChanged(object? sender, TimePickerSelectedValueChangedEventArgs e) =>
            this.InvalidateInput();


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() && this.datePicker.SelectedDate != null;
    }
}
