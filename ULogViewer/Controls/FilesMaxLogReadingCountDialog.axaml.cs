using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.Windows.Input;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls;

partial class FilesMaxLogReadingCountDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	// Static fields.
	static readonly StyledProperty<bool> ConfirmMaxLogReadingCountForLargeFilesProperty = AvaloniaProperty.Register<FilesMaxLogReadingCountDialog, bool>("ConfirmMaxLogReadingCountForLargeFiles");
	static readonly StyledProperty<bool> IsCancellationAllowedProperty = AvaloniaProperty.Register<FilesMaxLogReadingCountDialog, bool>(nameof(IsCancellationAllowed), true);
	static readonly StyledProperty<int?> MaxLogReadingCountOfLogProfileProperty = AvaloniaProperty.Register<FilesMaxLogReadingCountDialog, int?>(nameof(MaxLogReadingCountOfLogProfile));
	public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<FilesMaxLogReadingCountDialog, string?>(nameof(Message));


	// Fields.
	bool isResultGenerated;
	readonly ComboBox logReadingWindowComboBox;
	readonly IntegerTextBox maxLogReadingCountTextBox;


	// Constructor.
	public FilesMaxLogReadingCountDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.logReadingWindowComboBox = this.Get<ComboBox>(nameof(logReadingWindowComboBox));
		this.maxLogReadingCountTextBox = this.Get<IntegerTextBox>(nameof(maxLogReadingCountTextBox)).Also(it =>
		{
			it.Minimum = 1;
			it.GetObservable(IntegerTextBox.IsTextValidProperty).Subscribe(this.InvalidateInput);
		});
		this.SetValue(ConfirmMaxLogReadingCountForLargeFilesProperty, this.Settings.GetValueOrDefault(SettingKeys.ConfirmMaxLogReadingCountForLargeFiles));
		this.Settings.SettingChanged += this.OnSettingChanged;
		this.GetObservable(ConfirmMaxLogReadingCountForLargeFilesProperty).Subscribe(confirm => this.Settings.SetValue<bool>(SettingKeys.ConfirmMaxLogReadingCountForLargeFiles, confirm));
	}


	// Generate result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		this.isResultGenerated = true;
		this.LogReadingWindow = (LogReadingWindow)this.logReadingWindowComboBox.SelectedItem.AsNonNull();
		this.MaxLogReadingCount = (int?)this.maxLogReadingCountTextBox.Value;
		return Task.FromResult<object?>(true);
	}
	

	// Initial log reading window to be shown.
	public LogReadingWindow InitialLogReadingWindow { get; set; }


	// Initial max log reading count to be shown.
	public int? InitialMaxLogReadingCount { get; set; }


	// Whether cancellation of dialog is allowed or not.
	public bool IsCancellationAllowed
	{
		get => this.GetValue(IsCancellationAllowedProperty);
		set => this.SetValue(IsCancellationAllowedProperty, value);
	}


	// Log reading window selected by user.
	public LogReadingWindow LogReadingWindow { get; private set; }


	// Max log reading count selected by user.
	public int? MaxLogReadingCount { get; private set; }


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		this.Settings.SettingChanged -= this.OnSettingChanged;
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
	{
		if (!this.IsCancellationAllowed && !this.isResultGenerated)
			e.Cancel = true;
		base.OnClosing(e);
	}


	/// <inheritdoc/>
	protected override void OnEnterKeyClickedOnInputControl(IControl control)
	{
		base.OnEnterKeyClickedOnInputControl(control);
		if (control == this.maxLogReadingCountTextBox)
			this.GenerateResultCommand.TryExecute();
	}


	// Called when setting changed.
	void OnSettingChanged(object? sender, SettingChangedEventArgs e)
	{
		if (e.Key == SettingKeys.ConfirmMaxLogReadingCountForLargeFiles)
			this.SetValue(ConfirmMaxLogReadingCountForLargeFilesProperty, (bool)e.Value);
	}


	// Max log reading count defined in related log profile.
	public int? MaxLogReadingCountOfLogProfile
	{
		get => this.GetValue(MaxLogReadingCountOfLogProfileProperty);
		set => this.SetValue(MaxLogReadingCountOfLogProfileProperty, value);
	}


	// Message.
	public string? Message
	{
		get => this.GetValue(MessageProperty);
		set => this.SetValue(MessageProperty, value);
	}


	// Dialog opened.
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		var maximum = this.MaxLogReadingCountOfLogProfile?.Let(c => Math.Min(int.MaxValue, c)) ?? int.MaxValue;
		if (maximum <= 1)
		{
			this.SynchronizationContext.Post(this.Close);
			return;
		}
		this.logReadingWindowComboBox.SelectedItem = this.InitialLogReadingWindow;
		this.maxLogReadingCountTextBox.Maximum = maximum;
		this.maxLogReadingCountTextBox.Value = this.InitialMaxLogReadingCount?.Let(c => Math.Max(Math.Min(c, maximum), 1));
		this.SynchronizationContext.Post(() =>
		{
			this.maxLogReadingCountTextBox.Focus();
			this.maxLogReadingCountTextBox.SelectAll();
		});
	}


	// Validate input.
	protected override bool OnValidateInput() =>
		base.OnValidateInput()
		&& this.maxLogReadingCountTextBox.IsTextValid;
}
