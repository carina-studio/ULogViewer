using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Input.Platform;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit pattern in <see cref="Regex"/>.
/// </summary>
partial class PatternEditor : CarinaStudio.Controls.UserControl<IULogViewerApplication>
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
	/// Property of <see cref="Pattern"/>.
	/// </summary>
	public static readonly DirectProperty<PatternEditor, Regex?> PatternProperty = AvaloniaProperty.RegisterDirect<PatternEditor, Regex?>(nameof(Pattern), e => e.pattern, (e, p) => e.Pattern = p);


	// Constants.
    const string RegexOptionsFormat = "PatternEditor.RegexOptions";


	// Static fields.
	static readonly SettingKey<bool> IsClickButtonToEditPatternTutorialShownKey = new("PatternEditor.IsClickButtonToEditPatternTutorialShown");
	static readonly SettingKey<bool> IsClickButtonToEditPatternTutorialShownLegacyKey = new("RegexEditorDialog.IsClickButtonToEditPatternTutorialShown");


	// Fields.
	readonly Button editPatternButton;
	bool hasDialog;
	Regex? pattern;
	readonly RegexTextBox patternTextBox;
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
			it.GetObservable(RegexTextBox.IsTextValidProperty).Subscribe(isValid =>
			{
				this.SetAndRaise(PatternProperty, ref this.pattern, isValid ? it.Object : null);
			});
			it.GetObservable(RegexTextBox.ObjectProperty).Subscribe(pattern =>
			{
				this.SetAndRaise(PatternProperty, ref this.pattern, pattern);
			});
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
			_ = App.Current.Clipboard!.SetTextAndDataAsync(patternText, RegexOptionsFormat, data);
		}
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
		if (this.window == null || this.hasDialog)
			return;
		
		// edit
		this.hasDialog = true;
		var regex = await new RegexEditorDialog()
		{
			IgnoreCase = this.patternTextBox.IgnoreCase,
			InitialRegexText = this.patternTextBox.Text,
			IsCapturingGroupsEnabled = this.GetValue<bool>(IsCapturingGroupsEnabledProperty),
			IsCapturingLogPropertiesEnabled = this.GetValue<bool>(IsCapturingLogPropertiesEnabledProperty),
		}.ShowDialog<Regex?>(this.window);
		this.hasDialog = false;
		if (regex == null)
		{
			this.patternTextBox.Focus();
			return;
		}
		
		// update pattern
		if (regex != null)
			this.patternTextBox.IgnoreCase = (regex.Options & RegexOptions.IgnoreCase) != 0;
		this.patternTextBox.Object = regex;
		this.SetAndRaise(PatternProperty, ref this.pattern, regex);
		this.patternTextBox.Focus();
	}


	/// <summary>
	/// Get or set whether group capturing is enabled or not.
	/// </summary>
	public bool IsCapturingGroupsEnabled
	{
		get => this.GetValue<bool>(IsCapturingGroupsEnabledProperty);
		set => this.SetValue<bool>(IsCapturingGroupsEnabledProperty, value);
	}


	/// <summary>
	/// Get or set whether log property capturing is enabled or not.
	/// </summary>
	public bool IsCapturingLogPropertiesEnabled
	{
		get => this.GetValue<bool>(IsCapturingLogPropertiesEnabledProperty);
		set => this.SetValue<bool>(IsCapturingLogPropertiesEnabledProperty, value);
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
		if (this.window == null || this.hasDialog)
			return;
		
		// get data from clipboard
		var clipboard = App.Current.Clipboard;
		if (clipboard == null)
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
        catch
        { }

		// confirm replacing current pattern
        if (this.pattern != null)
        {
			this.hasDialog = true;
            var result = await new MessageDialog()
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
			this.SetAndRaise(PatternProperty, ref this.pattern, value);
		}
	}

	
	/// <summary>
	/// Show tutorial if needed.
	/// </summary>
	/// <param name="presenter">Presenter to show tutorial.</param>
	/// <param name="focusedControl">Control to focus after closing tutorial.</param>
	/// <returns>True if tutorial is being shown.</returns>
	public bool ShowTutorialIfNeeded(ITutorialPresenter presenter, Control? focusedControl = null)
	{
		// check state
		this.VerifyAccess();
		if (this.window == null)
			return false;
		var persistentState = this.PersistentState;
		persistentState.GetRawValue(IsClickButtonToEditPatternTutorialShownLegacyKey)?.Let(it =>
		{
			if (it is bool boolValue)
				persistentState.SetValue<bool>(IsClickButtonToEditPatternTutorialShownKey, boolValue);
			persistentState.ResetValue(IsClickButtonToEditPatternTutorialShownLegacyKey);
		});

		// show first tutorial
		if (!persistentState.GetValueOrDefault(IsClickButtonToEditPatternTutorialShownKey))
		{
			return presenter.ShowTutorial(new Tutorial().Also(it =>
			{
				it.Anchor = this.Get<Control>("editPatternButton");
				it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/PatternEditor.Tutorial.ClickButtonToEditPatternDetailedly"));
				it.Dismissed += (_, e) =>
				{
					persistentState.SetValue<bool>(IsClickButtonToEditPatternTutorialShownKey, true);
					if (!this.ShowTutorialIfNeeded(presenter, focusedControl))
						(focusedControl ?? this.editPatternButton).Focus();
				};
				it.Icon = (IImage?)this.window.FindResource("Image/Icon.Lightbulb.Colored");
				it.IsSkippingAllTutorialsAllowed = false;
			}));
		}

		// no tutorial to show
		return false;
	}
}