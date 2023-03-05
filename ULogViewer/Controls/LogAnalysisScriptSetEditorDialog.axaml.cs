using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels.Analysis.Scripting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ContextualBasedAnalysisCondition"/>s.
/// </summary>
partial class LogAnalysisScriptSetEditorDialog : CarinaStudio.Controls.ApplicationWindow<IULogViewerApplication>
{
	// Constants.
	const int InitSizeSetDelay = 100;
	const int InitSizeSetTimeout = 1000;


	// Static fields.
	static readonly DirectProperty<LogAnalysisScriptSetEditorDialog, bool> AreValidParametersProperty = AvaloniaProperty.RegisterDirect<LogAnalysisScriptSetEditorDialog, bool>("AreValidParameters", d => d.areValidParameters);
	static readonly Dictionary<LogAnalysisScriptSet, LogAnalysisScriptSetEditorDialog> Dialogs = new();
	static readonly SettingKey<bool> DonotShowRestrictionsWithNonProVersionKey = new("LogAnalysisScriptSetEditorDialog.DonotShowRestrictionsWithNonProVersion");
	static readonly StyledProperty<bool> IsEmbeddedScriptSetProperty = AvaloniaProperty.Register<LogAnalysisScriptSetEditorDialog, bool>(nameof(IsEmbeddedScriptSet));


	// Fields.
	bool areValidParameters;
	readonly ScheduledAction completeSettingInitSizeAction;
	Size? expectedInitSize;
	readonly LogProfileIconColorComboBox iconColorComboBox;
	readonly LogProfileIconComboBox iconComboBox;
	readonly IDisposable initBoundsObserverToken;
	readonly IDisposable initHeightObserverToken;
	readonly Stopwatch initSizeSetStopWatch = new();
	readonly IDisposable initWidthObserverToken;
	readonly TextBox nameTextBox;
	LogAnalysisScriptSet? scriptSetToEdit;
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="LogAnalysisScriptSetEditorDialog"/> instance.
	/// </summary>
	public LogAnalysisScriptSetEditorDialog()
	{
		AvaloniaXamlLoader.Load(this);
		if (Platform.IsLinux)
			this.WindowStartupLocation = WindowStartupLocation.Manual;
		this.completeSettingInitSizeAction = new(() =>
		{
			if (!this.expectedInitSize.HasValue)
				return;
			var expectedSize = this.expectedInitSize.Value;
			if (this.IsOpened && this.initSizeSetStopWatch.ElapsedMilliseconds <= InitSizeSetTimeout)
			{
				if (Math.Abs(this.Bounds.Width - expectedSize.Width) > 10
					|| Math.Abs(this.Bounds.Height - expectedSize.Height) > 10)
				{
					this.completeSettingInitSizeAction!.Schedule(InitSizeSetDelay);
					return;
				}
			}
			this.initSizeSetStopWatch.Stop();
			this.initBoundsObserverToken!.Dispose();
			this.initHeightObserverToken!.Dispose();
			this.initWidthObserverToken!.Dispose();
			this.OnInitialSizeSet();
		});
		this.iconColorComboBox = this.Get<LogProfileIconColorComboBox>(nameof(iconColorComboBox));
		this.iconComboBox = this.Get<LogProfileIconComboBox>(nameof(iconComboBox));
		this.initBoundsObserverToken = this.GetObservable(BoundsProperty).Subscribe(bounds =>
		{
			this.OnInitialWidthChanged(bounds.Width);
			this.OnInitialHeightChanged(bounds.Height);
		});
		this.initHeightObserverToken = this.GetObservable(HeightProperty).Subscribe(this.OnInitialHeightChanged);
		this.initWidthObserverToken = this.GetObservable(WidthProperty).Subscribe(this.OnInitialWidthChanged);
		this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise(AreValidParametersProperty, ref this.areValidParameters, this.IsEmbeddedScriptSet || !string.IsNullOrWhiteSpace(this.nameTextBox.Text));
		});
		this.GetObservable(IsEmbeddedScriptSetProperty).Subscribe(_ => this.validateParametersAction.Schedule());
	}


	/// <summary>
	/// Close all window related to given script set.
	/// </summary>
	/// <param name="scriptSet">Script set.</param>
	public static void CloseAll(LogAnalysisScriptSet scriptSet)
	{
		if (!Dialogs.TryGetValue(scriptSet, out var dialog))
			dialog?.Close();
	}


	/// <summary>
	/// Complete editing.
	/// </summary>
	public async void CompleteEditing()
	{
		var scriptSet = this.scriptSetToEdit ?? new(this.Application);
		scriptSet.Name = this.nameTextBox.Text?.Trim();
		scriptSet.Icon = this.iconComboBox.SelectedItem.GetValueOrDefault();
		scriptSet.IconColor = this.iconColorComboBox.SelectedItem.GetValueOrDefault();
		if (!this.IsEmbeddedScriptSet && !LogAnalysisScriptSetManager.Default.ScriptSets.Contains(scriptSet))
		{
			if (!this.Application.ProductManager.IsProductActivated(Products.Professional)
				&& !LogAnalysisScriptSetManager.Default.CanAddScriptSet)
			{
				await new MessageDialog()
				{
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/LogAnalysisScriptSetEditorDialog.CannotAddMoreScriptSetWithoutProVersion"),
				}.ShowDialog(this);
				return;
			}
			LogAnalysisScriptSetManager.Default.AddScriptSet(scriptSet);
		}
		this.Close(scriptSet);
	}


	/// <summary>
	/// Get or set whether the script set is embedded in another container or not.
	/// </summary>
	public bool IsEmbeddedScriptSet
	{
		get => this.GetValue(IsEmbeddedScriptSetProperty);
		set => this.SetValue(IsEmbeddedScriptSetProperty, value);
	}


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.scriptSetToEdit != null 
			&& Dialogs.TryGetValue(this.scriptSetToEdit, out var dialog)
			&& this == dialog)
		{
			Dialogs.Remove(this.scriptSetToEdit);
		}
		this.completeSettingInitSizeAction.ExecuteIfScheduled();
		base.OnClosed(e);
	}


	// Called when initial height of window changed.
	void OnInitialHeightChanged(double height)
	{
		if (!this.IsOpened || !this.expectedInitSize.HasValue)
			return;
		var expectedHeight = this.expectedInitSize.Value.Height;
		if (Math.Abs(expectedHeight - height) <= 1 && Math.Abs(expectedHeight - this.Bounds.Height) <= 1)
			this.completeSettingInitSizeAction.Schedule(InitSizeSetDelay);
		else
		{
			this.Height = expectedHeight;
			this.completeSettingInitSizeAction.Reschedule(InitSizeSetDelay);
		}
	}


	// Called when initial size of window has been set.
	async void OnInitialSizeSet()
	{
		if (this.IsClosed)
			return;
		if (this.scriptSetToEdit == null
			&& !this.Application.ProductManager.IsProductActivated(Products.Professional))
		{
			if (!LogAnalysisScriptSetManager.Default.CanAddScriptSet)
			{
				await new MessageDialog()
				{
					Icon = MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/LogAnalysisScriptSetEditorDialog.CannotAddMoreScriptSetWithoutProVersion"),
				}.ShowDialog(this);
				this.IsEnabled = false;
				this.SynchronizationContext.PostDelayed(this.Close, 300); // [Workaround] Prevent crashing on macOS.
				return;
			}
			if (!this.PersistentState.GetValueOrDefault(DonotShowRestrictionsWithNonProVersionKey))
			{
				var messageDialog = new MessageDialog()
				{
					DoNotAskOrShowAgain = false,
					Icon = MessageDialogIcon.Information,
					Message = this.GetResourceObservable("String/LogAnalysisScriptSetEditorDialog.RestrictionsOfNonProVersion"),
				};
				await messageDialog.ShowDialog(this);
				if (messageDialog.DoNotAskOrShowAgain == true)
					this.PersistentState.SetValue<bool>(DonotShowRestrictionsWithNonProVersionKey, true);
			}
		}
		await this.RequestEnablingRunningScriptAsync();
		this.nameTextBox.Focus();
	}


	// Called when initial width of window changed.
	void OnInitialWidthChanged(double width)
	{
		if (!this.IsOpened || !this.expectedInitSize.HasValue)
			return;
		var expectedWidth = this.expectedInitSize.Value.Width;
		if (Math.Abs(expectedWidth - width) <= 1 && Math.Abs(expectedWidth - this.Bounds.Width) <= 1)
			this.completeSettingInitSizeAction.Schedule(InitSizeSetDelay);
		else
		{
			this.Width = expectedWidth;
			this.completeSettingInitSizeAction.Reschedule(InitSizeSetDelay);
		}
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		// call base
		base.OnOpened(e);

		// setup initial window size and position
		this.initSizeSetStopWatch.Start();
		(this.Screens.ScreenFromWindow(this.PlatformImpl.AsNonNull()) ?? this.Screens.Primary)?.Let(screen =>
		{
			var workingArea = screen.WorkingArea;
			var widthRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitWidthRatio);
			var heightRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitHeightRatio);
			var scaling = Platform.IsMacOS ? 1.0 : screen.Scaling;
			var left = (workingArea.TopLeft.X + workingArea.Width * (1 - widthRatio) / 2); // in device pixels
			var top = (workingArea.TopLeft.Y + workingArea.Height * (1 - heightRatio) / 2); // in device pixels
			var width = (workingArea.Width * widthRatio) / scaling;
			var height = (workingArea.Height * heightRatio) / scaling;
			var sysDecorSize = this.GetSystemDecorationSizes();
			this.Position = new((int)(left + 0.5), (int)(top + 0.5));
			this.expectedInitSize = new Size(width, height - sysDecorSize.Top - sysDecorSize.Bottom);
			this.expectedInitSize.Value.Let(it =>
			{
				this.Width = it.Width;
				this.Height = it.Height;
			});
		});
		this.completeSettingInitSizeAction.Schedule(InitSizeSetDelay);

		// show script
		var scriptSet = this.scriptSetToEdit;
		if (scriptSet != null)
		{
			if (!this.IsEmbeddedScriptSet)
			{
				this.iconColorComboBox.SelectedItem = scriptSet.IconColor;
				this.iconComboBox.SelectedItem = scriptSet.Icon;
				this.nameTextBox.Text = scriptSet.Name;
			}
		}
		else
		{
			this.iconComboBox.SelectedItem = LogProfileIcon.Analysis;
		}

		// setup initial focus
		var scrollViewer = this.Get<ScrollViewer>("contentScrollViewer");
		(scrollViewer.Content as Control)?.Let(it => it.Opacity = 0);
		this.SynchronizationContext.Post(() =>
		{
			scrollViewer.ScrollToHome();
			if (!this.IsEmbeddedScriptSet)
				this.nameTextBox.Focus();
			(scrollViewer.Content as Control)?.Let(it => it.Opacity = 1);
		});
	}


	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis#LogAnalysisScript");
#pragma warning restore CA1822


	// Request running script.
	async Task RequestEnablingRunningScriptAsync()
	{
		if (!this.IsOpened || this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript))
			return;
		if (!await new EnableRunningScriptDialog().ShowDialog(this))
		{
			this.IsEnabled = false;
			this.SynchronizationContext.PostDelayed(this.Close, 300); // [Workaround] Prevent crashing on macOS.
		}
	}


	/// <summary>
	/// Get or set log analysis script set to be edited.
	/// </summary>
	public LogAnalysisScriptSet? ScriptSetToEdit
	{
		get => this.scriptSetToEdit;
		set
		{
			this.VerifyAccess();
			if (this.IsOpened)
				throw new InvalidOperationException();
			this.scriptSetToEdit = value;
		}
	}


	/// <summary>
	/// Show dialog to edit script set.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="scriptSet">Script set to edit.</param>
	public static void Show(Avalonia.Controls.Window parent, LogAnalysisScriptSet? scriptSet)
	{
		if (scriptSet != null && Dialogs.TryGetValue(scriptSet, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}
		dialog = new LogAnalysisScriptSetEditorDialog()
		{
			scriptSetToEdit = scriptSet,
		};
		if (scriptSet != null)
			Dialogs[scriptSet] = dialog;
		dialog.Show(parent);
	}
}
