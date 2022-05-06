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
		static readonly AvaloniaProperty<bool> HasTestResultProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>("HasTestResult");
		static readonly AvaloniaProperty<bool> IsCapturingGroupEnabledProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>(nameof(IsCapturingGroupEnabled));
		static readonly AvaloniaProperty<string?> TestLogLineProperty = AvaloniaProperty.Register<RegexEditorDialog, string?>("TestLogLine");
		static readonly AvaloniaProperty<bool> TestResultProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>("TestResult");


		// Fields.
		readonly ObservableList<Tuple<string, string>> capturedGroups = new();
		readonly RegexTextBox regexTextBox;
		readonly ScheduledAction testAction;


		/// <summary>
		/// Initialize new <see cref="RegexEditorDialog"/> instance.
		/// </summary>
		public RegexEditorDialog()
		{
			this.CapturedGroups = this.capturedGroups.AsReadOnly();
			AvaloniaXamlLoader.Load(this);
			this.regexTextBox = this.FindControl<RegexTextBox>(nameof(regexTextBox))!.Also(it =>
			{
				if (this.IsCapturingGroupEnabled)
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
				}
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
		}


		// List of captured groups.
		IList<Tuple<string, string>> CapturedGroups { get; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult<object?>(this.regexTextBox.Object);


		// Initial Regex.
		public Regex? InitialRegex { get; set; }


		// Whether group capturing is enabled or not.
		public bool IsCapturingGroupEnabled
		{
			get => this.GetValue<bool>(IsCapturingGroupEnabledProperty);
			set => this.SetValue<bool>(IsCapturingGroupEnabledProperty, value);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var regex = this.InitialRegex;
			if (regex != null)
			{
				this.regexTextBox.IgnoreCase = (regex.Options & RegexOptions.IgnoreCase) != 0;
				this.regexTextBox.Object = regex;
			}
			else
				this.regexTextBox.IgnoreCase = true;
			this.SynchronizationContext.Post(this.regexTextBox.Focus);
		}


		// Property changed.
        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
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
