using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Editor of <see cref="LogPattern"/>.
	/// </summary>
	partial class LogPatternEditorDialog : InputDialog<IULogViewerApplication>
	{
		/// <summary>
		/// List of property names of log.
		/// </summary>
		public static readonly IList<string> LogPropertyNames = Log.PropertyNames.Where(it =>
		{
			return it switch
			{
				nameof(Log.FileName) => false,
				nameof(Log.LineNumber) => false,
				_ => true,
			};
		}).ToList().AsReadOnly();


		// Static fields.
		static readonly Regex DefaultRegexGroupNameRegex = new("^[\\d]+$");
		static readonly AvaloniaProperty<bool> HasTestResultProperty = AvaloniaProperty.Register<LogPatternEditorDialog, bool>("HasTestResult");
		static readonly AvaloniaProperty<string?> TestLogLineProperty = AvaloniaProperty.Register<LogPatternEditorDialog, string?>("TestLogLine");
		static readonly AvaloniaProperty<bool> TestResultProperty = AvaloniaProperty.Register<LogPatternEditorDialog, bool>("TestResult");


		// Fields.
		readonly ObservableList<Tuple<string, string>> capturedLogProperties = new();
		readonly RegexTextBox regexTextBox;
		readonly ToggleSwitch repeatableSwitch;
		readonly ToggleSwitch skippableSwitch;
		readonly ScheduledAction testAction;


		/// <summary>
		/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
		/// </summary>
		public LogPatternEditorDialog()
		{
			this.CapturedLogProperties = this.capturedLogProperties.AsReadOnly();
			InitializeComponent();
			this.regexTextBox = this.FindControl<RegexTextBox>(nameof(regexTextBox)).AsNonNull().Also(it =>
			{
				foreach (var propertyName in LogPropertyNames)
				{
					var group = new RegexGroup().Also(group =>
					{
						group.Bind(RegexGroup.DisplayNameProperty, new Binding()
                        {
							Converter = Converters.LogPropertyNameConverter.Default,
							Path = nameof(RegexGroup.Name),
							Source = group,
                        });
						group.Name = propertyName;
					});
					it.PredefinedGroups.Add(group);
				}
			});
			this.repeatableSwitch = this.FindControl<ToggleSwitch>(nameof(repeatableSwitch)).AsNonNull();
			this.skippableSwitch = this.FindControl<ToggleSwitch>(nameof(skippableSwitch)).AsNonNull();
			this.testAction = new ScheduledAction(() =>
			{
				var regex = this.regexTextBox.Regex;
				var testLine = this.GetValue<string?>(TestLogLineProperty);
				this.capturedLogProperties.Clear();
				if (!this.regexTextBox.IsTextValid || regex == null || string.IsNullOrEmpty(testLine))
				{
					this.SetValue<bool>(HasTestResultProperty, false);
					return;
				}
				var match = regex.Match(testLine);
				if (match.Success)
				{
					this.SetValue(TestResultProperty, true);
					foreach (var name in match.Groups.Keys)
                    {
						var group = match.Groups[name];
						if (DefaultRegexGroupNameRegex.IsMatch(name))
							continue;
						this.capturedLogProperties.Add(new(name, group.Value));
                    }
				}
				else
					this.SetValue(TestResultProperty, false);
				this.SetValue<bool>(HasTestResultProperty, true);
			});
		}


		// List of captured log properties.
		IList<Tuple<string, string>> CapturedLogProperties { get; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var editingLogPattern = this.LogPattern;
			var newLogPattern = new LogPattern(this.regexTextBox.Regex.AsNonNull(), this.repeatableSwitch.IsChecked.GetValueOrDefault(), this.skippableSwitch.IsChecked.GetValueOrDefault());
			if (editingLogPattern != null && editingLogPattern == newLogPattern)
				return Task.FromResult((object?)editingLogPattern);
			return Task.FromResult((object?)newLogPattern);
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


		// Property changed.
        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);
			if (change.Property == TestLogLineProperty)
				this.testAction.Schedule();
        }


        // Called when property of regex text box changed.
        void OnRegexTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.IsTextValidProperty || e.Property == RegexTextBox.RegexProperty)
			{
				this.InvalidateInput();
				this.testAction.Schedule();
			}
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && this.regexTextBox.IsTextValid && this.regexTextBox.Regex != null;
		}
	}
}
