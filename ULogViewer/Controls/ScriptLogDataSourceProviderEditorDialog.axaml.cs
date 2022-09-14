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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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


	// Fields.
	readonly ToggleButton addSupportedSourceOptionButton;
	readonly ContextMenu addSupportedSourceOptionMenu;
	readonly TextBox displayNameTextBox;
	bool isProviderShown = false;
	readonly Avalonia.Controls.ListBox supportedSourceOptionListBox;
	readonly SortedObservableList<SupportedSourceOption> supportedSourceOptions = new((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture));
	readonly SortedObservableList<MenuItem> unsupportedSourceOptionMenuItems = new((lhs, rhs) => string.Compare(lhs.DataContext as string, rhs.DataContext as string, true, CultureInfo.InvariantCulture));
	readonly SortedObservableList<string> unsupportedSourceOptions = new((lhs, rhs) => string.Compare(lhs, rhs, true, CultureInfo.InvariantCulture), LogDataSourceOptions.OptionNames);


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProviderEditorDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProviderEditorDialog()
	{
		this.SupportedSourceOptions = this.supportedSourceOptions.AsReadOnly();
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
		this.displayNameTextBox = this.Get<TextBox>(nameof(displayNameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
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


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		// call base
		base.OnOpened(e);

		// setup initial window size and position
		(this.Screens.ScreenFromWindow(this.PlatformImpl) ?? this.Screens.Primary)?.Let(screen =>
		{
			var workingArea = screen.WorkingArea;
			var widthRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitWidthRatio);
			var heightRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitHeightRatio);
			var pixelDensity = Platform.IsMacOS ? 1.0 : screen.PixelDensity;
			var left = (workingArea.TopLeft.X + workingArea.Width * (1 - widthRatio) / 2); // in device pixels
			var top = (workingArea.TopLeft.Y + workingArea.Height * (1 - heightRatio) / 2); // in device pixels
			var width = (workingArea.Width * widthRatio) / pixelDensity;
			var height = (workingArea.Height * heightRatio) / pixelDensity;
			var sysDecorSize = this.GetSystemDecorationSizes();
			this.Position = new((int)(left + 0.5), (int)(top + 0.5));
			this.Width = width;
			this.Height = (height - sysDecorSize.Top - sysDecorSize.Bottom);
		});

		// show provider
		var provider = this.Provider;
		if (provider != null)
		{
			this.displayNameTextBox.Text = provider.DisplayName;
			foreach (var option in provider.SupportedSourceOptions)
			{
				this.unsupportedSourceOptions.Remove(option);
				this.supportedSourceOptions.Add(new(option, provider.RequiredSourceOptions.Contains(option)));
			}
		}
		foreach (var option in this.unsupportedSourceOptions)
			this.unsupportedSourceOptionMenuItems.Add(this.CreateUnsupportedSourceOptionMenuItem(option));
		this.isProviderShown = true;

		// setup initial focus
		this.SynchronizationContext.Post(() =>
		{
			this.Get<ScrollViewer>("contentScrollViewer").ScrollToHome();
			this.displayNameTextBox.Focus();
		});

		// enable script running
		if (!this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript))
		{
			this.SynchronizationContext.PostDelayed(async () =>
			{
				if (this.Settings.GetValueOrDefault(AppSuite.SettingKeys.EnableRunningScript) || this.IsClosed)
					return;
				var result = await new MessageDialog()
				{
					Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
					DefaultResult = AppSuite.Controls.MessageDialogResult.No,
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = this.GetResourceObservable("String/ApplicationOptions.EnableRunningScript.ConfirmEnabling"),
				}.ShowDialog(this);
				if (result == AppSuite.Controls.MessageDialogResult.Yes)
					this.Settings.SetValue<bool>(AppSuite.SettingKeys.EnableRunningScript, true);
				else
					this.Close();
			}, 300);
		}
	}


	/// <inheritdoc/>
	protected override bool OnValidateInput() =>
		base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.displayNameTextBox.Text);


	// Open online documentation.
	void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/ScriptLogDataSource");


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
	/// Get or set script log data source provider to edit.
	/// </summary>
	public ScriptLogDataSourceProvider? Provider { get; set; }


	// Show menu of adding supported log data source options.
	void ShowAddSupportedSourceOptionMenu() =>
		this.addSupportedSourceOptionMenu.Open(this.addSupportedSourceOptionButton);


	// Get supported log data source options.
	IList<SupportedSourceOption> SupportedSourceOptions { get; }
}
