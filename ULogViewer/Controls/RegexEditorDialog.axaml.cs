using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Editor of <see cref="Regex"/>.
/// </summary>
class RegexEditorDialog : InputDialog<IULogViewerApplication>
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


	// Provider for assistance of phrase input.
	class PhraseInputAssistanceProvider : IPhraseInputAssistanceProvider
	{
		public Task<IList<string>> SelectCandidatePhrasesAsync(string prefix, string? postfix, CancellationToken cancellationToken) =>
			LogTextFilterPhrasesDatabase.SelectCandidatePhrasesAsync(prefix, postfix, cancellationToken);
	}


	// Static fields.
	static readonly Regex DefaultRegexGroupNameRegex = new("^[\\d]+$");
	static readonly StyledProperty<bool> HasTestResultProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>("HasTestResult");
	static readonly StyledProperty<bool> IsCapturingGroupsEnabledProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>(nameof(IsCapturingGroupsEnabled));
	static readonly StyledProperty<bool> IsCapturingLogPropertiesEnabledProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>(nameof(IsCapturingLogPropertiesEnabled));
	static readonly StyledProperty<string?> TestLogLineProperty = AvaloniaProperty.Register<RegexEditorDialog, string?>("TestLogLine");
	static readonly StyledProperty<bool> TestResultProperty = AvaloniaProperty.Register<RegexEditorDialog, bool>("TestResult");


	// Fields.
	readonly ObservableList<Tuple<string, string>> capturedGroups = new();
	bool isPhraseInputAssistanceEnabled;
	readonly RegexTextBox regexTextBox;
	readonly ScheduledAction testAction;
	readonly TextBox testLogLineTextBox;
	readonly ScheduledAction updatePhrasesDatabaseAction;


	/// <summary>
	/// Initialize new <see cref="RegexEditorDialog"/> instance.
	/// </summary>
	public RegexEditorDialog()
	{
		this.CapturedGroups = ListExtensions.AsReadOnly(this.capturedGroups);
		AvaloniaXamlLoader.Load(this);
		this.regexTextBox = this.FindControl<RegexTextBox>(nameof(regexTextBox))!.Also(it =>
		{
			it.GetObservable(RegexTextBox.HasOpenedAssistanceMenusProperty).Subscribe(hasOpenedMenus =>
			{
				if (hasOpenedMenus)
					this.updatePhrasesDatabaseAction?.Cancel();
				else if (this.isPhraseInputAssistanceEnabled)
					this.updatePhrasesDatabaseAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogTextFilterPhrasesDatabaseUpdateDelay));
			});
			it.GetObservable(RegexTextBox.IsTextValidProperty).Subscribe(_ =>
			{
				this.InvalidateInput();
				this.testAction?.Schedule();
			});
			it.GetObservable(RegexTextBox.ObjectProperty).Subscribe(_ =>
			{
				this.InvalidateInput();
				this.testAction?.Schedule();
				if (this.isPhraseInputAssistanceEnabled)
					this.updatePhrasesDatabaseAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogTextFilterPhrasesDatabaseUpdateDelay));
			});
		});
		this.testAction = new ScheduledAction(() =>
		{
			var regex = this.regexTextBox.Object;
			var testLine = this.GetValue(TestLogLineProperty);
			this.capturedGroups.Clear();
			if (!this.regexTextBox.IsTextValid || regex == null || string.IsNullOrEmpty(testLine))
			{
				this.SetValue(HasTestResultProperty, false);
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
			this.SetValue(HasTestResultProperty, true);
		});
		this.testLogLineTextBox = this.FindControl<TextBox>(nameof(testLogLineTextBox)).AsNonNull();
		this.updatePhrasesDatabaseAction = new(() =>
		{
			if (!this.IsOpened 
			    || !this.isPhraseInputAssistanceEnabled 
			    || this.regexTextBox.HasOpenedAssistanceMenus)
			{
				return;
			}
			this.regexTextBox.Object?.Let(it => LogTextFilterPhrasesDatabase.UpdatePhrasesAsync(it, default));
		});
	}


	/// <summary>
	/// List of captured groups.
	/// </summary>
	public IList<Tuple<string, string>> CapturedGroups { get; }


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		this.updatePhrasesDatabaseAction.ExecuteIfScheduled();
		return Task.FromResult<object?>(this.regexTextBox.Object);
	}
	

	// Ignore case of pattern.
	public bool? IgnoreCase { get; set; }


	// Initial Regex.
	public Regex? InitialRegex { get; set; }


	// Initial text of regex.
	public string? InitialRegexText { get; set; }


	// Whether group capturing is enabled or not.
	public bool IsCapturingGroupsEnabled
	{
		get => this.GetValue(IsCapturingGroupsEnabledProperty);
		set => this.SetValue(IsCapturingGroupsEnabledProperty, value);
	}


	// Whether log property capturing is enabled or not.
	public bool IsCapturingLogPropertiesEnabled
	{
		get => this.GetValue(IsCapturingLogPropertiesEnabledProperty);
		set => this.SetValue(IsCapturingLogPropertiesEnabledProperty, value);
	}


	/// <summary>
	/// Get or set whether assistance of phrase input is enabled or not.
	/// </summary>
	public bool IsPhraseInputAssistanceEnabled
	{
		get => this.isPhraseInputAssistanceEnabled;
		set
		{
			this.VerifyAccess();
			if (this.isPhraseInputAssistanceEnabled == value)
				return;
			if (value)
			{
				this.regexTextBox.PhraseInputAssistanceProvider = new PhraseInputAssistanceProvider();
				if (!this.regexTextBox.HasOpenedAssistanceMenus)
					this.updatePhrasesDatabaseAction.Schedule();
			}
			else
			{
				this.regexTextBox.PhraseInputAssistanceProvider = null;
				this.updatePhrasesDatabaseAction.Cancel();
			}
			this.isPhraseInputAssistanceEnabled = value;
		}
	}


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		this.updatePhrasesDatabaseAction.Cancel();
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() => this.regexTextBox.Focus());
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
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
		if (regex is not null)
		{
			this.regexTextBox.IgnoreCase = this.IgnoreCase ?? (regex.Options & RegexOptions.IgnoreCase) != 0;
			this.regexTextBox.Object = regex;
		}
		else if (this.InitialRegexText is not null)
		{
			this.regexTextBox.IgnoreCase = this.IgnoreCase ?? true;
			this.regexTextBox.Text = this.InitialRegexText;
			this.regexTextBox.Validate();
		}
		else
			this.regexTextBox.IgnoreCase = this.IgnoreCase ?? true;
		this.updatePhrasesDatabaseAction.Cancel();
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
