using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ScriptLogDataSourceProvider"/>s.
/// </summary>
class ScriptLogDataSourceProviderEditorDialog : Dialog<IULogViewerApplication>
{
	// Supported source option.
	public class SupportedSourceOption
	{
		// Fields.
		bool? isRequired;

		// Constructor.
		public SupportedSourceOption(string name, bool isRequired)
		{
			this.CanBeRequired = LogDataSourceOptions.IsValueTypeOption(name)
				? name switch
				{
					nameof(LogDataSourceOptions.ProcessId) => true,
					_ => false,
				}
				: name switch
				{
					nameof(LogDataSourceOptions.Encoding)
						or nameof(LogDataSourceOptions.EnvironmentVariables)
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


	// Static fields.
	static readonly DirectProperty<ScriptLogDataSourceProviderEditorDialog, bool> AreValidParametersProperty = AvaloniaProperty.RegisterDirect<ScriptLogDataSourceProviderEditorDialog, bool>(nameof(AreValidParameters), d => d.areValidParameters);
	static readonly DirectProperty<ScriptLogDataSourceProviderEditorDialog, Uri?> ClosingReaderScriptDocumentUriProperty = AvaloniaProperty.RegisterDirect<ScriptLogDataSourceProviderEditorDialog, Uri?>(nameof(ClosingReaderScriptDocumentUri), d => d.closingReaderScriptDocumentUri);
	static readonly Dictionary<ScriptLogDataSourceProvider, ScriptLogDataSourceProviderEditorDialog> Dialogs = new();
	static readonly DirectProperty<ScriptLogDataSourceProviderEditorDialog, bool> IsNewProviderProperty = AvaloniaProperty.RegisterDirect<ScriptLogDataSourceProviderEditorDialog, bool>(nameof(IsNewProvider), d => d.isNewProvider);
	static readonly StyledProperty<bool> IsEmbeddedProviderProperty = AvaloniaProperty.Register<ScriptLogDataSourceProviderEditorDialog, bool>(nameof(IsEmbeddedProvider));
	static readonly DirectProperty<ScriptLogDataSourceProviderEditorDialog, Uri?> OpeningReaderScriptDocumentUriProperty = AvaloniaProperty.RegisterDirect<ScriptLogDataSourceProviderEditorDialog, Uri?>(nameof(OpeningReaderScriptDocumentUri), d => d.openingReaderScriptDocumentUri);
	static readonly DirectProperty<ScriptLogDataSourceProviderEditorDialog, Uri?> ReadingLineScriptDocumentUriProperty = AvaloniaProperty.RegisterDirect<ScriptLogDataSourceProviderEditorDialog, Uri?>(nameof(ReadingLineScriptDocumentUri), d => d.readingLineScriptDocumentUri);


	// Fields.
	readonly ToggleButton addSupportedSourceOptionButton;
	readonly ContextMenu addSupportedSourceOptionMenu;
	bool areValidParameters;
	Uri? closingReaderScriptDocumentUri;
	readonly TextBox displayNameTextBox;
	bool isNewProvider;
	bool isProviderShown;
	Uri? openingReaderScriptDocumentUri;
	Uri? readingLineScriptDocumentUri;
	readonly Avalonia.Controls.ListBox supportedSourceOptionListBox;
	readonly SortedObservableList<SupportedSourceOption> supportedSourceOptions = new((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture));
	readonly SortedObservableList<MenuItem> unsupportedSourceOptionMenuItems = new((lhs, rhs) => string.Compare(lhs.DataContext as string, rhs.DataContext as string, true, CultureInfo.InvariantCulture));
	readonly SortedObservableList<string> unsupportedSourceOptions = new((lhs, rhs) => string.Compare(lhs, rhs, true, CultureInfo.InvariantCulture), LogDataSourceOptions.OptionNames);
	readonly ScheduledAction validateParametersAction;


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProviderEditorDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProviderEditorDialog()
	{
		var isInit = true;
		this.ApplyCommand = new Command(async () => await this.ApplyAsync(false), this.GetObservable(AreValidParametersProperty));
		this.CompleteEditingCommand = new Command(this.CompleteEditing, this.GetObservable(AreValidParametersProperty));
		this.RemoveSupportedSourceOptionCommand = new Command<SupportedSourceOption>(this.RemoveSupportedSourceOption);
		this.SupportedSourceOptions = ListExtensions.AsReadOnly(this.supportedSourceOptions);
		this.UnsupportedSourceOptions = ListExtensions.AsReadOnly(this.unsupportedSourceOptions);
		AvaloniaXamlLoader.Load(this);
		if (Platform.IsLinux)
			this.WindowStartupLocation = WindowStartupLocation.Manual;
		this.addSupportedSourceOptionButton = this.Get<ToggleButton>(nameof(addSupportedSourceOptionButton));
		this.addSupportedSourceOptionMenu = ((ContextMenu)this.Resources[nameof(addSupportedSourceOptionMenu)].AsNonNull()).Also(it =>
		{
			it.ItemsSource = this.unsupportedSourceOptionMenuItems;
			it.Closed += (_, _) => this.SynchronizationContext.Post(() => this.addSupportedSourceOptionButton.IsChecked = false);
			it.Opened += (_, _) =>
			{
				ToolTip.SetIsOpen(this.addSupportedSourceOptionButton, false);
				this.SynchronizationContext.Post(() => this.addSupportedSourceOptionButton.IsChecked = true);
			};
		});
		this.displayNameTextBox = this.Get<TextBox>(nameof(displayNameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.validateParametersAction?.Schedule());
		});
		this.supportedSourceOptionListBox = this.Get<Avalonia.Controls.ListBox>(nameof(supportedSourceOptionListBox));
		this.validateParametersAction = new(() =>
		{
			this.SetAndRaise(AreValidParametersProperty, ref this.areValidParameters, this.IsEmbeddedProvider || !string.IsNullOrWhiteSpace(this.displayNameTextBox.Text));
		});
		isInit = false;
		this.UpdateDocumentUris();
	}


	// Add supported source option.
	void AddSupportedSourceOption(MenuItem menuItem)
	{
		var option = (string)menuItem.DataContext.AsNonNull();
		this.unsupportedSourceOptions.Remove(option);
		this.unsupportedSourceOptionMenuItems.Remove(menuItem);
		this.supportedSourceOptions.Add(new(option, false));
	}
	
	
	// Command to apply current script set.
	public ICommand ApplyCommand { get; }
	
	
	// Apply current script set.
	async Task<ScriptLogDataSourceProvider?> ApplyAsync(bool willClose)
	{
		// check compilation error
		//
		
		// create or update provider
		var provider = this.Provider;
		if (provider is null)
		{
			if (!this.isNewProvider)
				return null;
			provider = new ScriptLogDataSourceProvider(this.Application);
		}
		provider.DisplayName = this.displayNameTextBox.Text;
		provider.SetSupportedSourceOptions(
			this.supportedSourceOptions.Select(it => it.Name),
			this.supportedSourceOptions.Where(it => it.IsRequired == true).Select(it => it.Name)
		);
		
		// complete
		return provider;
	}
	
	
	// Whether all parameters are valid or not.
	public bool AreValidParameters => this.GetValue(AreValidParametersProperty);


	// URI of document of closing reader script.
	public Uri? ClosingReaderScriptDocumentUri => this.GetValue(ClosingReaderScriptDocumentUriProperty);
	
	
	// Complete editing.
	async Task CompleteEditing()
	{
		var provider = await this.ApplyAsync(true);
		if (provider is not null)
			this.Close(provider);
	}
	
	
	/// <summary>
	/// Command to complete editing.
	/// </summary>
	public ICommand CompleteEditingCommand { get; }


	// Create menu item for unsupported log data source option.
	MenuItem CreateUnsupportedSourceOptionMenuItem(string option) => new MenuItem().Also(menuItem =>
	{
		menuItem.Click += (_, _) =>
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


	/// <summary>
	/// Get or set whether the provider is newly created or not.
	/// </summary>
	public bool IsNewProvider => this.isNewProvider;


	/// <summary>
	/// Get or set whether the provider is embedded in another container or not.
	/// </summary>
	public bool IsEmbeddedProvider
	{
		get => this.GetValue(IsEmbeddedProviderProperty);
		init => this.SetValue(IsEmbeddedProviderProperty, value);
	}


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		if (this.Provider is not null 
		    && Dialogs.TryGetValue(this.Provider, out var dialog)
		    && dialog == this)
		{
			Dialogs.Remove(this.Provider);
		}
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnFirstMeasurementCompleted(Size measuredSize)
	{
		// call base
		base.OnFirstMeasurementCompleted(measuredSize);
		
		// setup initial window size and position
		(this.Screens.ScreenFromWindow(this) ?? this.Screens.Primary)?.Let(screen =>
		{
			var workingArea = screen.WorkingArea;
			var widthRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitWidthRatio);
			var heightRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitHeightRatio);
			var scaling = screen.Scaling;
			var left = (workingArea.TopLeft.X + workingArea.Width * (1 - widthRatio) / 2); // in device pixels
			var top = (workingArea.TopLeft.Y + workingArea.Height * (1 - heightRatio) / 2); // in device pixels
			var sysDecorSize = this.GetSystemDecorationSizes();
			this.Position = new((int)(left + 0.5), (int)(top + 0.5));
			this.SynchronizationContext.Post(() =>
			{
				this.Width = (workingArea.Width * widthRatio) / scaling;
				this.Height = ((workingArea.Height * heightRatio) / scaling) - sysDecorSize.Top - sysDecorSize.Bottom;
			}, DispatcherPriority.Send);
		});
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		_ = this.OnOpenedAsync();
	}
	
	
	// Handle dialog opened asynchronously.
	async Task OnOpenedAsync()
	{
		// request running script
		await this.RequestEnablingRunningScriptAsync();

		// setup initial focus
		this.SynchronizationContext.Post(() =>
		{
			if (this.IsEmbeddedProvider)
				this.supportedSourceOptionListBox.Focus();
			else
				this.displayNameTextBox.Focus();
		});
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		// call base
		base.OnOpening(e);
		
		// show provider
		var provider = this.Provider;
		if (provider is not null)
		{
			if (!this.IsEmbeddedProvider)
				this.displayNameTextBox.Text = provider.DisplayName;
			foreach (var option in provider.SupportedSourceOptions)
			{
				this.unsupportedSourceOptions.Remove(option);
				this.supportedSourceOptions.Add(new(option, provider.RequiredSourceOptions.Contains(option)));
			}
		}
		else
		{
			this.SetAndRaise(IsNewProviderProperty, ref this.isNewProvider, true);
		}
		foreach (var option in this.unsupportedSourceOptions)
			this.unsupportedSourceOptionMenuItems.Add(this.CreateUnsupportedSourceOptionMenuItem(option));
		this.isProviderShown = true;
		
		// validate
		this.validateParametersAction.Schedule();
	}


	/// <summary>
	/// Open online documentation.
	/// </summary>
#pragma warning disable CA1822
	public void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/ScriptLogDataSource");
#pragma warning restore CA1822
	
	
	// URI of document of opening reader script.
	public Uri? OpeningReaderScriptDocumentUri => this.GetValue(OpeningReaderScriptDocumentUriProperty);
	
	
	// URI of document of reading line script.
	public Uri? ReadingLineScriptDocumentUri => this.GetValue(ReadingLineScriptDocumentUriProperty);


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
	public ScriptLogDataSourceProvider? Provider { get; init; }
	
	
	/// <summary>
	/// Show dialog to edit provider.
	/// </summary>
	/// <param name="parent">Parent window.</param>
	/// <param name="provider">Provider to edit.</param>
	/// <param name="isEmbedded">Whether the provider is embedded in log profile or not.</param>
	public static void Show(Avalonia.Controls.Window parent, ScriptLogDataSourceProvider? provider, bool isEmbedded)
	{
		if (provider is not null && Dialogs.TryGetValue(provider, out var dialog))
		{
			dialog.ActivateAndBringToFront();
			return;
		}
		dialog = new ScriptLogDataSourceProviderEditorDialog
		{
			IsEmbeddedProvider = isEmbedded,
			Provider = provider,
		};
		if (provider is not null)
			Dialogs[provider] = dialog;
		dialog.Show(parent);
	}


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


	// Update URIs of document of script.
	void UpdateDocumentUris()
	{ }
}
