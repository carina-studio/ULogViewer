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
        readonly DatePicker datePicker;
        readonly TimePicker timePicker;


        /// <summary>
        /// Initialize new <see cref="DateTimeSelectionDialog"/> instance.
        /// </summary>
        public DateTimeSelectionDialog()
        {
            InitializeComponent();
            this.datePicker = this.FindControl<DatePicker>(nameof(datePicker));
            this.timePicker = this.FindControl<TimePicker>(nameof(timePicker));
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
        {
            var date = this.datePicker.SelectedDate.GetValueOrDefault().Date;
            var time = this.timePicker.SelectedTime.GetValueOrDefault();
            return Task.FromResult((object?)new DateTime(date.Year, date.Month, date.Day, time.Hours, time.Minutes, 0));
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
            this.FindControl<TextBlock>("messageTextBlock").AsNonNull().Text = this.Message ?? " ";
            this.InitialDateTime?.Let(it =>
            {
                this.datePicker.SelectedDate = new DateTimeOffset(it.Date);
                this.timePicker.SelectedTime = new TimeSpan(it.Hour, it.Minute, 0);
            });
        }


        // Selected time changed.
        void OnTimePickerSelectedTimeChanged(object? sender, TimePickerSelectedValueChangedEventArgs e) =>
            this.InvalidateInput();


        // Validate input.
        protected override bool OnValidateInput() =>
            base.OnValidateInput() && this.datePicker.SelectedDate != null && this.timePicker.SelectedTime != null;
    }
}
