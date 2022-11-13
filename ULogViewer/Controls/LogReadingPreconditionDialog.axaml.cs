using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Dialog to let user select precondition of log reading.
    /// </summary>
    partial class LogReadingPreconditionDialog : AppSuite.Controls.InputDialog
    {
        // Static fields.
        static readonly StyledProperty<bool> IsCancellationAllowedProperty = AvaloniaProperty.Register<LogReadingPreconditionDialog, bool>(nameof(IsCancellationAllowed), true);
        static readonly StyledProperty<bool> IsReadingFromFilesProperty = AvaloniaProperty.Register<LogReadingPreconditionDialog, bool>(nameof(IsReadingFromFiles), false);


        // Fields.
        readonly DateTimeTextBox beginningTimestampTextBox;
        readonly DateTimeTextBox endingTimestampTextBox;
        readonly Avalonia.Controls.TextBlock invalidTimestampRangeTextBlock;
        bool isResultGenerated;
        readonly CheckBox timestampsCheckBox;


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
            this.invalidTimestampRangeTextBlock = this.Get<Avalonia.Controls.TextBlock>(nameof(invalidTimestampRangeTextBlock));
            this.timestampsCheckBox = this.Get<CheckBox>(nameof(timestampsCheckBox)).Also(it =>
            {
                it.GetObservable(RadioButton.IsCheckedProperty).Subscribe(isChecked => 
                {
                    this.InvalidateInput();
                    if (isChecked == true)
                    {
                        this.beginningTimestampTextBox.Focus();
                        this.beginningTimestampTextBox.SelectAll();
                    }
                });
            });
        }


        // Generate result.
        protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
            Task.FromResult((object?)Global.Run(() =>
            {
                var precondition = new Logs.LogReadingPrecondition();
                this.isResultGenerated = true;
                if (this.timestampsCheckBox.IsChecked == true)
                    precondition.TimestampRange = (this.beginningTimestampTextBox.Value, this.endingTimestampTextBox.Value);
                return precondition;
            }));
        

        // Whether cancellation is allowed or not.
        public bool IsCancellationAllowed
        {
            get => this.GetValue<bool>(IsCancellationAllowedProperty);
            set => this.SetValue<bool>(IsCancellationAllowedProperty, value);
        }


        // Whether logs will be read from files or not.
        public bool IsReadingFromFiles
        {
            get => this.GetValue<bool>(IsReadingFromFilesProperty);
            set => this.SetValue<bool>(IsReadingFromFilesProperty, value);
        }


        // Called when closing.
        protected override void OnClosing(CancelEventArgs e)
        {
            if (!this.isResultGenerated && !this.IsCancellationAllowed)
                e.Cancel = true;
            base.OnClosing(e);
        }


        // Window opened
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            var precondition = this.Precondition;
            if (!precondition.TimestampRange.IsUniversal)
            {
                this.timestampsCheckBox.IsChecked = true;
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
            if (this.invalidTimestampRangeTextBlock != null)
                this.invalidTimestampRangeTextBlock.IsVisible = false;
            if (!base.OnValidateInput())
                return false;
            if (this.timestampsCheckBox.IsChecked == true)
            {
                if (this.beginningTimestampTextBox.IsTextValid && this.beginningTimestampTextBox.Value.HasValue)
                {
                    if (this.endingTimestampTextBox.IsTextValid && this.endingTimestampTextBox.Value.HasValue)
                    {
                        if (this.beginningTimestampTextBox.Value.Value >= this.endingTimestampTextBox.Value.Value)
                        {
                            if (this.invalidTimestampRangeTextBlock != null)
                                this.invalidTimestampRangeTextBlock.IsVisible = true;
                            return false;
                        }
                    }
                    return true;
                }
                else if (this.endingTimestampTextBox.IsTextValid && this.endingTimestampTextBox.Value.HasValue)
                    return true;
                return false;
            }
            return true;
        }
        

        // Precondition.
        public Logs.LogReadingPrecondition Precondition { get; set; }


        /// <summary>
		/// Select precondition before reading logs from files.
		/// </summary>
		public bool SelectPreconditionForFiles
		{
			get => this.Settings.GetValueOrDefault(SettingKeys.SelectLogReadingPreconditionForFiles);
			set => this.Settings.SetValue<bool>(SettingKeys.SelectLogReadingPreconditionForFiles, value);
		}
    }
}
