using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ScriptLogDataSourceProvider"/>s.
/// </summary>
partial class ScriptLogDataSourceProviderEditorDialog : CarinaStudio.Controls.InputDialog<IULogViewerApplication>
{
	// Supported source option.
	public class SupportedSourceOption
	{
		// Fields.
		bool? isRequired;

		// Constructor.
		public SupportedSourceOption(string name, bool isRequired)
		{
			this.CanBeRequired = !LogDataSourceOptions.IsValueTypeOption(name) && name switch
			{
				nameof(LogDataSourceOptions.Encoding)
				or nameof(LogDataSourceOptions.SetupCommands)
				or nameof(LogDataSourceOptions.TeardownCommands) => false,
				_ => true,
			};
			this.isRequired = this.CanBeRequired ? isRequired : null;
			this.Name = name;
		}

		// Whether option can be required or not.
		public bool CanBeRequired { get; }

		// Whether option is required or not.
		public bool? IsRequired
		{
			get => this.isRequired;
			set
			{
				if (this.CanBeRequired)
					this.isRequired = value;
			}
		}

		// Option name.
		public string Name { get; }
	}


	// Constants.
	const int InitSizeSetDelay = 100;
	const int InitSizeSetTimeout = 1000;


	// Static fields.
	static readonly StyledProperty<bool> IsEmbeddedProviderProperty = AvaloniaProperty.Register<ScriptLogDataSourceProviderEditorDialog, bool>(nameof(IsEmbeddedProvider));


	// Fields.
	readonly ToggleButton addSupportedSourceOptionButton;
	readonly ContextMenu addSupportedSourceOptionMenu;
	readonly ScheduledAction completeSettingInitSizeAction;
	readonly TextBox displayNameTextBox;
	Size? expectedInitSize;
	readonly IDisposable initBoundsObserverToken;
	readonly IDisposable initHeightObserverToken;
	readonly Stopwatch initSizeSetStopWatch = new();
	readonly IDisposable initWidthObserverToken;
	readonly Avalonia.Controls.ListBox supportedSourceOptionListBox;
	readonly SortedObservableList<SupportedSourceOption> supportedSourceOptions = new((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture));
	readonly SortedObservableList<MenuItem> unsupportedSourceOptionMenuItems = new((lhs, rhs) => string.Compare(lhs.DataContext as string, rhs.DataContext as string, true, CultureInfo.InvariantCulture));
	readonly SortedObservableList<string> unsupportedSourceOptions = new((lhs, rhs) => string.Compare(lhs, rhs, true, CultureInfo.InvariantCulture), LogDataSourceOptions.OptionNames);


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProviderEditorDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProviderEditorDialog()
	{
		this.RemoveSupportedSourceOptionCommand = new Command<SupportedSourceOption>(this.RemoveSupportedSourceOption);
		this.SupportedSourceOptions = ListExtensions.AsReadOnly(this.supportedSourceOptions);
		this.UnsupportedSourceOptions = ListExtensions.AsReadOnly(this.unsupportedSourceOptions);
		AvaloniaXamlLoader.Load(this);
		if (Platform.IsLinux)
			this.WindowStartupLocation = WindowStartupLocation.Manual;
		this.addSupportedSourceOptionButton = this.Get<ToggleButton>(nameof(addSupportedSourceOptionButton));
		this.addSupportedSourceOptionMenu = ((ContextMenu)this.Resources[nameof(addSupportedSourceOptionMenu)].AsNonNull()).Also(it =>
		{
			it.Items = this.unsupportedSourceOptionMenuItems;
			it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.addSupportedSourceOptionButton.IsChecked = false);
			it.MenuOpened += (_, e) =>
			{
				ToolTip.SetIsOpen(this.addSupportedSourceOptionButton, false);
				this.SynchronizationContext.Post(() => this.addSupportedSourceOptionButton.IsChecked = true);
			};
		});
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
		this.displayNameTextBox = this.Get<TextBox>(nameof(displayNameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.initBoundsObserverToken = this.GetObservable(BoundsProperty).Subscribe(bounds =>
		{
			this.OnInitialWidthChanged(bounds.Width);
			this.OnInitialHeightChanged(bounds.Height);
		});
		this.initHeightObserverToken = this.GetObservable(HeightProperty).Subscribe(this.OnInitialHeightChanged);
		this.initWidthObserverToken = this.GetObservable(WidthProperty).Subscribe(this.OnInitialWidthChanged);
		this.supportedSourceOptionListBox = this.Get<Avalonia.Controls.ListBox>(nameof(supportedSourceOptionListBox));
	}


	// Add supported source option.
	void AddSupportedSourceOption(MenuItem menuItem)
	{
		var option = (string)menuItem.DataContext.AsNonNull();
		this.unsupportedSourceOptions.Remove(option);
		this.unsupportedSourceOptionMenuItems.Remove(menuItem);
		this.supportedSourceOptions.Add(new(option, false));
	}


	// Create menu item for unsupported log data source option.
	MenuItem CreateUnsupportedSourceOptionMenuItem(string option) => new MenuItem().Also(menuItem =>
	{
		menuItem.Click += (_, e) =>
		{
			this.addSupportedSourceOptionMenu.Close();
			this.AddSupportedSourceOption(menuItem);
		};
		menuItem.DataContext = option;
		menuItem.Header = new TextBlock().Also(it =>
		{
			it.Bind(TextBlock.TextProperty, new Binding()
			{
				Converter = Converters.LogDataSourceOptionConverter.Default,
				Source = option,
			});
		});
	});


	/// <inheritdoc/>
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var provider = this.Provider ?? new ScriptLogDataSourceProvider(this.Application);
		provider.DisplayName = this.displayNameTextBox.Text;
		provider.SetSupportedSourceOptions(
			this.supportedSourceOptions.Select(it => it.Name),
			this.supportedSourceOptions.Where(it => it.IsRequired == true).Select(it => it.Name)
		);
		return Task.FromResult<object?>(provider);
	}


	/// <summary>
	/// Get or set whether the provider is embedded in another container or not.
	/// </summary>
	public bool IsEmbeddedProvider
	{
		get => this.GetValue(IsEmbeddedProviderProperty);
		set => this.SetValue(IsEmbeddedProviderProperty, value);
	}


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
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
		await this.RequestEnablingRunningScriptAsync();
		this.displayNameTextBox.Focus();
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
			this.expectedInitSize = new(width, height - sysDecorSize.Top - sysDecorSize.Bottom);
			this.expectedInitSize.Value.Let(it =>
			{
				this.Width = it.Width;
				this.Height = it.Height;
			});
		});
		this.completeSettingInitSizeAction.Schedule(InitSizeSetDelay);

		// show provider
		var provider = this.Provider;
		if (provider != null)
		{
			if (!this.IsEmbeddedProvider)
				this.displayNameTextBox.Text = provider.DisplayName;
			foreach (var option in provider.SupportedSourceOptions)
			{
				this.unsupportedSourceOptions.Remove(option);
				this.supportedSourceOptions.Add(new(option, provider.RequiredSourceOptions.Contains(option)));
			}
		}
		foreach (var option in this.unsupportedSourceOptions)
			this.unsupportedSourceOptionMenuItems.Add(this.CreateUnsupportedSourceOptionMenuItem(option));

		// setup initial focus
		var scrollViewer = this.Get<ScrollViewer>("contentScrollViewer");
		(scrollViewer.Content as Control)?.Let(it => it.Opacity = 0);
		this.SynchronizationContext.Post(() =>
		{
			scrollViewer.ScrollToHome();
			if (this.IsEmbeddedProvider)
				this.supportedSourceOptionListBox.Focus();
			else
				this.displayNameTextBox.Focus();
			(scrollViewer.Content as Control)?.Let(it => it.Opacity = 1);
		});
	}


	/// <inheritdoc/>
	protected override bool OnValidateInput() =>
		base.OnValidateInput() && (this.IsEmbeddedProvider || !string.IsNullOrWhiteSpace(this.displayNameTextBox.Text));


	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/ScriptLogDataSource");
#pragma warning restore CA1822


	// Remove supported source option.
	void RemoveSupportedSourceOption(SupportedSourceOption option)
	{
		if (this.supportedSourceOptions.Remove(option))
		{
			this.unsupportedSourceOptions.Add(option.Name);
			this.unsupportedSourceOptionMenuItems.Add(this.CreateUnsupportedSourceOptionMenuItem(option.Name));
		}
		this.supportedSourceOptionListBox.SelectedItem = null;
		this.supportedSourceOptionListBox.Focus();
	}


	/// <summary>
	/// Command to remove supported source option.
	/// </summary>
	public ICommand RemoveSupportedSourceOptionCommand { get; }


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
	/// Get or set script log data source provider to edit.
	/// </summary>
	public ScriptLogDataSourceProvider? Provider { get; set; }


	/// <summary>
	/// Show menu of adding supported log data source options.
	/// </summary>
	public void ShowAddSupportedSourceOptionMenu() =>
		this.addSupportedSourceOptionMenu.Open(this.addSupportedSourceOptionButton);


	/// <summary>
	/// Get supported log data source options.
	/// </summary>
	public IList<SupportedSourceOption> SupportedSourceOptions { get; }


	/// <summary>
	/// Get unsupported log data source options.
	/// </summary>
	public IList<string> UnsupportedSourceOptions { get; }
}
