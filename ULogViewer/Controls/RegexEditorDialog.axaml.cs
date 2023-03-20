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
	/// Editor of <see cref="Regex"/>.
	/// </summary>
	partial class RegexEditorDialog : InputDialog<IULogViewerApplication>
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
		static readonly StyledProperty<bool> HasTestResultProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>("HasTestResult");
		static readonly StyledProperty<bool> IsCapturingGroupsEnabledProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>(nameof(IsCapturingGroupsEnabled));
		static readonly StyledProperty<bool> IsCapturingLogPropertiesEnabledProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>(nameof(IsCapturingLogPropertiesEnabled));
		static readonly StyledProperty<string?> TestLogLineProperty = AvaloniaProperty.Register<RegexEditorDialog, string?>("TestLogLine");
		static readonly StyledProperty<bool> TestResultProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>("TestResult");


		// Fields.
		readonly ObservableList<Tuple<string, string>> capturedGroups = new();
		readonly RegexTextBox regexTextBox;
		readonly ScheduledAction testAction;
		readonly TextBox testLogLineTextBox;


		/// <summary>
		/// Initialize new <see cref="RegexEditorDialog"/> instance.
		/// </summary>
		public RegexEditorDialog()
		{
			this.CapturedGroups = ListExtensions.AsReadOnly(this.capturedGroups);
			AvaloniaXamlLoader.Load(this);
			this.regexTextBox = this.FindControl<RegexTextBox>(nameof(regexTextBox))!.Also(it =>
			{
				it.GetObservable(RegexTextBox.IsTextValidProperty).Subscribe(_ =>
				{
					this.InvalidateInput();
					this.testAction?.Schedule();
				});
				it.GetObservable(RegexTextBox.ObjectProperty).Subscribe(_ =>
				{
					this.InvalidateInput();
					this.testAction?.Schedule();
				});
			});
			this.testAction = new ScheduledAction(() =>
			{
				var regex = this.regexTextBox.Object;
				var testLine = this.GetValue<string?>(TestLogLineProperty);
				this.capturedGroups.Clear();
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
						this.capturedGroups.Add(new(name, group.Value));
                    }
				}
				else
					this.SetValue(TestResultProperty, false);
				this.SetValue<bool>(HasTestResultProperty, true);
			});
			this.testLogLineTextBox = this.FindControl<TextBox>(nameof(testLogLineTextBox)).AsNonNull();
		}


		/// <summary>
		/// List of captured groups.
		/// </summary>
		public IList<Tuple<string, string>> CapturedGroups { get; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult<object?>(this.regexTextBox.Object);
		

		// Ignore case of pattern.
		public bool? IgnoreCase { get; set; }


		// Initial Regex.
		public Regex? InitialRegex { get; set; }


		// Initial text of regex.
		public string? InitialRegexText { get; set; }


		// Whether group capturing is enabled or not.
		public bool IsCapturingGroupsEnabled
		{
			get => this.GetValue<bool>(IsCapturingGroupsEnabledProperty);
			set => this.SetValue<bool>(IsCapturingGroupsEnabledProperty, value);
		}


		// Whether log property capturing is enabled or not.
		public bool IsCapturingLogPropertiesEnabled
		{
			get => this.GetValue<bool>(IsCapturingLogPropertiesEnabledProperty);
			set => this.SetValue<bool>(IsCapturingLogPropertiesEnabledProperty, value);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var testLogLineDescriptionTextBlock = this.Get<TextBlock>("testLogLineDescriptionTextBlock");
			if (this.IsCapturingGroupsEnabled)
			{
				if (this.IsCapturingLogPropertiesEnabled)
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
						this.regexTextBox.PredefinedGroups.Add(group);
					}
					testLogLineDescriptionTextBlock.Bind(TextBlock.TextProperty, this.Application.GetObservableString("RegexEditorDialog.TestLogLine.Description.CapturingLogProperties"));
				}
				else
					testLogLineDescriptionTextBlock.Bind(TextBlock.TextProperty, this.Application.GetObservableString("RegexEditorDialog.TestLogLine.Description.CapturingGroups"));
			}
			else
				testLogLineDescriptionTextBlock.Bind(TextBlock.TextProperty, this.Application.GetObservableString("RegexEditorDialog.TestLogLine.Description"));
			var regex = this.InitialRegex;
			if (regex != null)
			{
				this.regexTextBox.IgnoreCase = this.IgnoreCase ?? (regex.Options & RegexOptions.IgnoreCase) != 0;
				this.regexTextBox.Object = regex;
			}
			else if (this.InitialRegexText != null)
			{
				this.regexTextBox.IgnoreCase = this.IgnoreCase ?? true;
				this.regexTextBox.Text = this.InitialRegexText;
			}
			else
				this.regexTextBox.IgnoreCase = this.IgnoreCase ?? true;
			this.SynchronizationContext.Post(this.regexTextBox.Focus);
		}


		// Property changed.
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
			if (change.Property == TestLogLineProperty)
				this.testAction.Schedule();
        }


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() && this.regexTextBox.IsTextValid && this.regexTextBox.Object != null;
	}
}
