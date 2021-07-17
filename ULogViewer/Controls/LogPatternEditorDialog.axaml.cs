using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.Logs;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Editor of <see cref="LogPattern"/>.
	/// </summary>
	partial class LogPatternEditorDialog : BaseDialog
	{
		// Fields.
		readonly RegexTextBox regexTextBox;
		readonly ToggleSwitch repeatableSwitch;
		readonly ToggleSwitch skippableSwitch;


		/// <summary>
		/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
		/// </summary>
		public LogPatternEditorDialog()
		{
			InitializeComponent();
			this.regexTextBox = this.FindControl<RegexTextBox>("regexTextBox");
			this.repeatableSwitch = this.FindControl<ToggleSwitch>("repeatableSwitch");
			this.skippableSwitch = this.FindControl<ToggleSwitch>("skippableSwitch");
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set <see cref="LogPattern"/> to be edited.
		/// </summary>
		public LogPattern? LogPattern { get; set; }


		// Generate result.
		protected override object? OnGenerateResult()
		{
			// create pattern
			var editingLogPattern = this.LogPattern;
			var newLogPattern = new LogPattern(this.regexTextBox.Regex.AsNonNull(), this.repeatableSwitch.IsChecked.GetValueOrDefault(), this.skippableSwitch.IsChecked.GetValueOrDefault());
			if (editingLogPattern != null && editingLogPattern == newLogPattern)
				return editingLogPattern;
			return newLogPattern;
		}


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
				this.InvalidateInput();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && this.regexTextBox.IsTextValid && this.regexTextBox.Regex != null;
		}
	}
}
