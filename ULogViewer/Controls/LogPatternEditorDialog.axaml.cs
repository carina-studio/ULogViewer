using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Editor of <see cref="LogPattern"/>.
	/// </summary>
	partial class LogPatternEditorDialog : BaseDialog
	{
		// Static fields.
		static readonly AvaloniaProperty<bool> AreValidParamsProperty = AvaloniaProperty.Register<LogPatternEditorDialog, bool>(nameof(AreValidParams));


		// Fields.
		readonly RegexTextBox regexTextBox;
		readonly ToggleSwitch repeatableSwitch;
		readonly ToggleSwitch skippableSwitch;
		readonly ScheduledAction validateParamsAction;


		/// <summary>
		/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
		/// </summary>
		public LogPatternEditorDialog()
		{
			InitializeComponent();
			this.regexTextBox = this.FindControl<RegexTextBox>("regexTextBox");
			this.repeatableSwitch = this.FindControl<ToggleSwitch>("repeatableSwitch");
			this.skippableSwitch = this.FindControl<ToggleSwitch>("skippableSwitch");
			this.validateParamsAction = new ScheduledAction(() =>
			{
				this.SetValue<bool>(AreValidParamsProperty, this.regexTextBox.IsTextValid && this.regexTextBox.Regex != null);
			});
		}


		// Check whether parameters are valid or not.
		bool AreValidParams { get => this.GetValue<bool>(AreValidParamsProperty); }


		// Complete pattern editing.
		void CompleteEditing()
		{
			// check state
			this.regexTextBox.Validate();
			this.validateParamsAction.Execute();
			if (!this.AreValidParams)
				return;

			// create pattern
			var editingLogPattern = this.LogPattern;
			var newLogPattern = new LogPattern(this.regexTextBox.Regex.AsNonNull(), this.repeatableSwitch.IsChecked.GetValueOrDefault(), this.skippableSwitch.IsChecked.GetValueOrDefault());
			if (editingLogPattern != null && editingLogPattern == newLogPattern)
			{
				this.Close(editingLogPattern);
				return;
			}

			// complete
			this.Close(newLogPattern);
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set <see cref="LogPattern"/> to be edited.
		/// </summary>
		public LogPattern? LogPattern { get; set; }


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var logPattern = this.LogPattern;
			if (logPattern != null)
			{
				this.regexTextBox.Regex = logPattern.Regex;
				this.repeatableSwitch.IsChecked = logPattern.IsRepeatable;
				this.skippableSwitch.IsChecked = logPattern.IsSkippable;
			}
			this.regexTextBox.Focus();
		}


		// Called when property of regex text box changed.
		void OnRegexTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.IsTextValidProperty || e.Property == RegexTextBox.RegexProperty)
				this.validateParamsAction.Reschedule();
		}
	}
}
