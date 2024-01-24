using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Input.Platform;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit pattern in <see cref="Regex"/>.
/// </summary>
class PatternEditor : CarinaStudio.Controls.UserControl<IULogViewerApplication>
{
	/// <summary>
	/// Property of <see cref="IsCapturingGroupsEnabled"/>.
	/// </summary>
	public static readonly StyledProperty<bool> IsCapturingGroupsEnabledProperty = AvaloniaProperty.Register<PatternEditor, bool>(nameof(IsCapturingGroupsEnabled));
	/// <summary>
	/// Property of <see cref="IsCapturingLogPropertiesEnabled"/>.
	/// </summary>
	public static readonly StyledProperty<bool> IsCapturingLogPropertiesEnabledProperty = AvaloniaProperty.Register<PatternEditor, bool>(nameof(IsCapturingLogPropertiesEnabled));
	/// <summary>
	/// Property of <see cref="IsPatternTextValid"/>.
	/// </summary>
	public static readonly DirectProperty<PatternEditor, bool> IsPatternTextValidProperty = AvaloniaProperty.RegisterDirect<PatternEditor, bool>(nameof(IsPatternTextValid), e => e.isPatternTextValid);
	/// <summary>
	/// Property of <see cref="IsPhraseInputAssistanceEnabled"/>.
	/// </summary>
	public static readonly DirectProperty<PatternEditor, bool> IsPhraseInputAssistanceEnabledProperty = AvaloniaProperty.RegisterDirect<PatternEditor, bool>(nameof(IsPhraseInputAssistanceEnabled), e => e.isPhraseInputAssistanceEnabled, (e, isEnabled) => e.IsPhraseInputAssistanceEnabled = isEnabled);
	/// <summary>
	/// Property of <see cref="Pattern"/>.
	/// </summary>
	public static readonly DirectProperty<PatternEditor, Regex?> PatternProperty = AvaloniaProperty.RegisterDirect<PatternEditor, Regex?>(nameof(Pattern), e => e.pattern, (e, p) => e.Pattern = p);
	/// <summary>
	/// Property of <see cref="Watermark"/>.
	/// </summary>
	public static readonly StyledProperty<string?> WatermarkProperty = AvaloniaProperty.Register<PatternEditor, string?>(nameof(Watermark));
	
	
	// Provider for assistance of phrase input.
	class PhraseInputAssistanceProvider : IPhraseInputAssistanceProvider
	{
		// Fields.
		readonly RegexTextBox regexTextBox;
		
		// Constructor.
		public PhraseInputAssistanceProvider(RegexTextBox regexTextBox) =>
			this.regexTextBox = regexTextBox;
		
		/// <inheritdoc/>
		public Task<IList<string>> SelectCandidatePhrasesAsync(string prefix, string? postfix, CancellationToken cancellationToken) =>
			LogTextFilterPhrasesDatabase.SelectCandidatePhrasesAsync(prefix, postfix, this.regexTextBox.IgnoreCase, cancellationToken);
	}


	// Constants.
    const string RegexOptionsFormat = "PatternEditor.RegexOptions";


	// Fields.
	readonly Button editPatternButton;
	bool hasDialog;
	bool isPatternTextValid = true;
	bool isPhraseInputAssistanceEnabled;
	Regex? pattern;
	readonly RegexTextBox patternTextBox;
	readonly ScheduledAction updatePhrasesDatabaseAction;
	// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
	readonly ScheduledAction updatePredefinedCapturingGroupsAction;
	Avalonia.Controls.Window? window;


	/// <summary>
	/// Initialize new <see cref="PatternEditor"/> instance.
	/// </summary>
	public PatternEditor()
	{
		AvaloniaXamlLoader.Load(this);
		this.editPatternButton = this.Get<Button>(nameof(editPatternButton));
		this.patternTextBox = this.Get<RegexTextBox>(nameof(patternTextBox)).Also(it =>
		{
			it.GetObservable(RegexTextBox.HasOpenedAssistanceMenusProperty).Subscribe(hasOpenedMenus =>
			{
				if (hasOpenedMenus)
					this.updatePhrasesDatabaseAction?.Cancel();
				else if (this.isPhraseInputAssistanceEnabled)
					this.updatePhrasesDatabaseAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogTextFilterPhrasesDatabaseUpdateDelay));
			});
			it.GetObservable(RegexTextBox.IsTextValidProperty).Subscribe(isValid =>
			{
				this.SetAndRaise(IsPatternTextValidProperty, ref this.isPatternTextValid, isValid);
				this.SetAndRaise(PatternProperty, ref this.pattern, isValid ? it.Object : null);
			});
			it.GetObservable(RegexTextBox.ObjectProperty).Subscribe(pattern =>
			{
				this.SetAndRaise(PatternProperty, ref this.pattern, pattern);
				if (this.isPhraseInputAssistanceEnabled)
					this.updatePhrasesDatabaseAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogTextFilterPhrasesDatabaseUpdateDelay));
			});
		});
		this.updatePhrasesDatabaseAction = new(() =>
		{
			if (this.window is null 
			    || !this.isPhraseInputAssistanceEnabled 
			    || this.patternTextBox.HasOpenedAssistanceMenus)
			{
				return;
			}
			this.patternTextBox.Object?.Let(it => LogTextFilterPhrasesDatabase.UpdatePhrasesAsync(it, default));
		});
		this.updatePredefinedCapturingGroupsAction = new(() =>
		{
			if (this.IsCapturingGroupsEnabled && this.IsCapturingLogPropertiesEnabled)
			{
				foreach (var propertyName in RegexEditorDialog.LogPropertyNames)
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
					this.patternTextBox.PredefinedGroups.Add(group);
				}
			}
			else
				this.patternTextBox.PredefinedGroups.Clear();
		});
		this.GetObservable(IsCapturingGroupsEnabledProperty).Subscribe(_ =>
			this.updatePredefinedCapturingGroupsAction.Schedule());
		this.GetObservable(IsCapturingLogPropertiesEnabledProperty).Subscribe(_ =>
			this.updatePredefinedCapturingGroupsAction.Schedule());
	}


	/// <summary>
	/// Copy <see cref="Pattern"/> to clipboard.
	/// </summary>
	public void CopyPattern()
	{
		// check state
		this.VerifyAccess();
		var patternText = this.patternTextBox.Text;
		if (string.IsNullOrEmpty(patternText))
			return;
		
		// copy
		try
		{
			var options = this.patternTextBox.IgnoreCase
				? RegexOptions.IgnoreCase
				: RegexOptions.None;
			var data = BitConverter.GetBytes((int)options);
			_ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAndDataAsync(patternText, RegexOptionsFormat, data);
		}
		// ReSharper disable once EmptyGeneralCatchClause
		catch
		{ }
	}


	/// <summary>
	/// Start editing pattern.
	/// </summary>
	public async void EditPattern()
	{
		// check state
		this.VerifyAccess();
		if (this.window is null || this.hasDialog)
			return;
		
		// update phrases database
		this.updatePhrasesDatabaseAction.ExecuteIfScheduled();
		
		// edit
		this.hasDialog = true;
		var regex = await new RegexEditorDialog
		{
			IgnoreCase = this.patternTextBox.IgnoreCase,
			InitialRegexText = this.patternTextBox.Text,
			IsCapturingGroupsEnabled = this.GetValue(IsCapturingGroupsEnabledProperty),
			IsCapturingLogPropertiesEnabled = this.GetValue(IsCapturingLogPropertiesEnabledProperty),
			IsPhraseInputAssistanceEnabled = this.isPhraseInputAssistanceEnabled,
		}.ShowDialog<Regex?>(this.window);
		this.hasDialog = false;
		if (regex == null)
		{
			this.patternTextBox.Focus();
			return;
		}
		
		// update pattern
		this.patternTextBox.IgnoreCase = (regex.Options & RegexOptions.IgnoreCase) != 0;
		this.patternTextBox.Object = regex;
		this.updatePhrasesDatabaseAction.Cancel();
		this.SetAndRaise(PatternProperty, ref this.pattern, regex);
		this.patternTextBox.Focus();
	}


	/// <summary>
	/// Get or set whether group capturing is enabled or not.
	/// </summary>
	public bool IsCapturingGroupsEnabled
	{
		get => this.GetValue(IsCapturingGroupsEnabledProperty);
		set => this.SetValue(IsCapturingGroupsEnabledProperty, value);
	}


	/// <summary>
	/// Get or set whether log property capturing is enabled or not.
	/// </summary>
	public bool IsCapturingLogPropertiesEnabled
	{
		get => this.GetValue(IsCapturingLogPropertiesEnabledProperty);
		set => this.SetValue(IsCapturingLogPropertiesEnabledProperty, value);
	}


	/// <summary>
	/// Check whether the text of pattern is valid or not.
	/// </summary>
	public bool IsPatternTextValid => this.isPatternTextValid;
	
	
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
				this.patternTextBox.PhraseInputAssistanceProvider = new PhraseInputAssistanceProvider(this.patternTextBox);
				if (!this.patternTextBox.HasOpenedAssistanceMenus)
					this.updatePhrasesDatabaseAction.Schedule();
			}
			else
			{
				this.patternTextBox.PhraseInputAssistanceProvider = null;
				this.updatePhrasesDatabaseAction.Cancel();
			}
			this.SetAndRaise(IsPhraseInputAssistanceEnabledProperty, ref this.isPhraseInputAssistanceEnabled, value);
		}
	}
	

	/// <inheritdoc/>
	protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		base.OnAttachedToLogicalTree(e);
		this.window = this.FindLogicalAncestorOfType<Avalonia.Controls.Window>().AsNonNull();
	}


	/// <inheritdoc/>
	protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		this.updatePhrasesDatabaseAction.ExecuteIfScheduled();
		this.window = null;
		base.OnDetachedFromLogicalTree(e);
	}


	/// <inheritdoc/>
	protected override void OnGotFocus(GotFocusEventArgs e)
	{
		base.OnGotFocus(e);
		if (e.Source is not Button)
			this.patternTextBox.Focus();
	}


	/// <summary>
	/// Paste pattern from clipboard if available.
	/// </summary>
	public async void PastePattern()
	{
		// check state
		this.VerifyAccess();
		if (this.window is null || this.hasDialog)
			return;
		
		// get data from clipboard
		var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
		if (clipboard is null)
			return;
        var text = await clipboard.GetTextAsync();
        if (string.IsNullOrEmpty(text))
            return;
		var options = this.patternTextBox.IgnoreCase
			? RegexOptions.IgnoreCase
			: RegexOptions.None;
        try
        {
            (await clipboard.GetDataAsync(RegexOptionsFormat))?.Let(it => 
                options = (RegexOptions)BitConverter.ToInt32((byte[])it));
        }
        // ReSharper disable once EmptyGeneralCatchClause
        catch
        { }
        // confirm replacing current pattern
        if (this.pattern is not null)
        {
			this.hasDialog = true;
            var result = await new MessageDialog
            {
                Buttons = MessageDialogButtons.YesNo,
                Icon = MessageDialogIcon.Question,
                Message = App.Current.GetObservableString("PatternEditor.ConfirmReplacingPattern"),
            }.ShowDialog(window);
			this.hasDialog = false;
            if (result == MessageDialogResult.No)
			{
				this.patternTextBox.Focus();
                return;
			}
        }

		// update pattern
		this.patternTextBox.IgnoreCase = (options & RegexOptions.IgnoreCase) != 0;
        try
        {
            this.patternTextBox.Object = new Regex(text, options);
        }
        catch
        {
			this.patternTextBox.Text = text;
        }
		this.patternTextBox.Focus();
	}


	/// <summary>
	/// Get or set pattern to be edited.
	/// </summary>
	public Regex? Pattern
	{
		get => this.pattern;
		set
		{
			this.VerifyAccess();
			if (value != null)
				this.patternTextBox.IgnoreCase = (value.Options & RegexOptions.IgnoreCase) != 0;
			this.patternTextBox.Object = value;
			this.updatePhrasesDatabaseAction.Cancel();
			this.SetAndRaise(PatternProperty, ref this.pattern, value);
		}
	}

	
#pragma warning disable CA1822
#pragma warning disable IDE0060
	/// <summary>
	/// Show tutorial if needed.
	/// </summary>
	/// <param name="presenter">Presenter to show tutorial.</param>
	/// <param name="focusedControl">Control to focus after closing tutorial.</param>
	/// <returns>True if tutorial is being shown.</returns>
	public bool ShowTutorialIfNeeded(ITutorialPresenter presenter, Control? focusedControl = null) =>
		false;
#pragma warning restore CA1822
#pragma warning restore IDE0060


	/// <summary>
	/// Get or set watermark to be shown in editor.
	/// </summary>
	public string? Watermark
	{
		get => this.GetValue(WatermarkProperty);
		set => this.SetValue(WatermarkProperty, value);
	}
}