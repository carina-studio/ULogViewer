using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user select precondition of log reading.
    /// </summary>
    class LogReadingPreconditionDialog : AppSuite.Controls.InputDialog
    {
        // Static fields.
        static readonly StyledProperty<bool> IsCancellationAllowedProperty = AvaloniaProperty.Register<LogReadingPreconditionDialog, bool>(nameof(IsCancellationAllowed), true);
        static readonly StyledProperty<bool> IsReadingFromFilesProperty = AvaloniaProperty.Register<LogReadingPreconditionDialog, bool>(nameof(IsReadingFromFiles), false);
        static readonly StyledProperty<bool> IsTimestampRangeValidProperty = AvaloniaProperty.Register<LogReadingPreconditionDialog, bool>("IsTimestampRangeValid", true);


        // Fields.
        readonly DateTimeTextBox beginningTimestampTextBox;
        readonly DateTimeTextBox endingTimestampTextBox;
        bool isResultGenerated;
        readonly ToggleSwitch timestampsSwitch;


        // Constructor.
        public LogReadingPreconditionDialog()
        {
            AvaloniaXamlLoader.Load(this);
            this.beginningTimestampTextBox = this.Get<DateTimeTextBox>(nameof(beginningTimestampTextBox)).Also(it =>
            {
                it.GetObservable(DateTimeTextBox.IsTextValidProperty).Subscribe(_ => this.InvalidateInput());
                it.GetObservable(DateTimeTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
            });
            this.endingTimestampTextBox = this.Get<DateTimeTextBox>(nameof(endingTimestampTextBox)).Also(it =>
            {
                it.GetObservable(DateTimeTextBox.IsTextValidProperty).Subscribe(_ => this.InvalidateInput());
                it.GetObservable(DateTimeTextBox.ValueProperty).Subscribe(_ => this.InvalidateInput());
            });
            this.timestampsSwitch = this.Get<ToggleSwitch>(nameof(timestampsSwitch)).Also(it =>
            {
                it.GetObservable(ToggleSwitch.IsCheckedProperty).Subscribe(_ => 
                {
                    this.InvalidateInput();
                });
            });
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)Global.Run(() =>
            {
                var precondition = new Logs.LogReadingPrecondition();
                this.isResultGenerated = true;
                if (this.timestampsSwitch.IsChecked == true)
                    precondition.TimestampRange = (this.beginningTimestampTextBox.Value, this.endingTimestampTextBox.Value);
                return precondition;
            }));
        

        // Whether cancellation is allowed or not.
        public bool IsCancellationAllowed
        {
            get => this.GetValue(IsCancellationAllowedProperty);
            set => this.SetValue(IsCancellationAllowedProperty, value);
        }


        // Whether logs will be read from files or not.
        public bool IsReadingFromFiles
        {
            get => this.GetValue(IsReadingFromFilesProperty);
            set => this.SetValue(IsReadingFromFilesProperty, value);
        }


        // Called when closing.
        protected override void OnClosing(WindowClosingEventArgs e)
        {
            if (!this.isResultGenerated && !this.IsCancellationAllowed)
                e.Cancel = true;
            base.OnClosing(e);
        }


        /// <inheritdoc/>
        protected override void OnOpening(EventArgs e)
        {
            base.OnOpening(e);
            var precondition = this.Precondition;
            if (!precondition.TimestampRange.IsUniversal)
            {
                this.timestampsSwitch.IsChecked = true;
                this.beginningTimestampTextBox.Value = precondition.TimestampRange.Start;
                this.endingTimestampTextBox.Value = precondition.TimestampRange.End;
            }
            else
            {
                this.endingTimestampTextBox.Value = DateTime.Now;
            }
        }


        // Validate input.
        protected override bool OnValidateInput()
        {
            if (this.beginningTimestampTextBox.IsTextValid && this.beginningTimestampTextBox.Value.HasValue
                && this.endingTimestampTextBox.IsTextValid && this.endingTimestampTextBox.Value.HasValue
                && this.beginningTimestampTextBox.Value.Value >= this.endingTimestampTextBox.Value.Value)
            {
                this.SetValue(IsTimestampRangeValidProperty, false);
                if (this.timestampsSwitch.IsChecked == true)
                    return false;
            }
            else
                this.SetValue(IsTimestampRangeValidProperty, true);
            return base.OnValidateInput();
        }
        

        // Precondition.
        public Logs.LogReadingPrecondition Precondition { get; set; }


        /// <summary>
		/// Select precondition before reading logs from files.
		/// </summary>
		public bool SelectPreconditionForFiles
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.SelectLogReadingPreconditionForFiles);
			set => this.Settings.SetValue(SettingKeys.SelectLogReadingPreconditionForFiles, value);
		}
    }
}
