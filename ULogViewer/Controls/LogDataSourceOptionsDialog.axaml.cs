using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="LogDataSourceOptions"/>.
/// </summary>
partial class LogDataSourceOptionsDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
{
	/// <summary>
	/// Type of database source.
	/// </summary>
	public enum DatabaseSourceType
	{
		/// <summary>
		/// File.
		/// </summary>
		File,
		/// <summary>
		/// Network.
		/// </summary>
		Network,
	}


	/// <summary>
	/// Property of <see cref="DataSourceProvider"/>.
	/// </summary>
	public static readonly StyledProperty<ILogDataSourceProvider?> DataSourceProviderProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, ILogDataSourceProvider?>(nameof(DataSourceProvider));


	// Static fields.
	static readonly StyledProperty<Uri?> CategoryReferenceUriProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, Uri?>("CategoryReferenceUri");
	static readonly StyledProperty<Uri?> CommandReferenceUriProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, Uri?>("CommandReferenceUri");
	static readonly StyledProperty<Uri?> ConnectionStringReferenceUriProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, Uri?>("ConnectionStringReferenceUri");
	static readonly StyledProperty<bool> IsAzureRelatedDataSourceProviderProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsAzureRelatedDataSourceProvider");
	static readonly StyledProperty<bool> IsCategoryRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCategoryRequired));
	static readonly StyledProperty<bool> IsCategorySupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCategorySupported));
	static readonly StyledProperty<bool> IsCommandRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCommandRequired));
	static readonly StyledProperty<bool> IsCommandSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCommandSupported));
	static readonly StyledProperty<bool> IsConnectionStringRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsConnectionStringRequired");
	static readonly StyledProperty<bool> IsConnectionStringSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsConnectionStringSupported");
	static readonly StyledProperty<bool> IsEncodingSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsEncodingSupported));
	static readonly StyledProperty<bool> IsFileNameSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsFileNameSupported));
	static readonly StyledProperty<bool> IsFormatJsonDataSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsFormatJsonDataSupported");
	static readonly StyledProperty<bool> IsFormatXmlDataSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsFormatXmlDataSupported");
	static readonly StyledProperty<bool> IsIncludeStandardErrorSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsIncludeStandardErrorSupported");
	static readonly StyledProperty<bool> IsIPEndPointSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsIPEndPointSupported));
	static readonly StyledProperty<bool> IsPasswordRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsPasswordRequired));
	static readonly StyledProperty<bool> IsPasswordSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsPasswordSupported));
	static readonly StyledProperty<bool> IsQueryStringRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsQueryStringRequired));
	static readonly StyledProperty<bool> IsQueryStringSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsQueryStringSupported));
	static readonly StyledProperty<bool> IsResourceOnAzureSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsResourceOnAzureSupported");
	static readonly SettingKey<bool> IsSelectAzureResourcesTutorialShownKey = new("LogDataSourceOptionsDialog.IsSelectAzureResourcesTutorialShown");
	static readonly StyledProperty<bool> IsSetupCommandsRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsSetupCommandsRequired));
	static readonly StyledProperty<bool> IsSetupCommandsSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsSetupCommandsSupported));
	static readonly StyledProperty<bool> IsTeardownCommandsRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsTeardownCommandsRequired));
	static readonly StyledProperty<bool> IsTeardownCommandsSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsTeardownCommandsSupported));
	static readonly StyledProperty<bool> IsTemplateProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsTemplate));
	static readonly StyledProperty<bool> IsUriSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUriSupported));
	static readonly StyledProperty<bool> IsUseTextShellSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>("IsUseTextShellSupported");
	static readonly StyledProperty<bool> IsUserNameRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUserNameRequired));
	static readonly StyledProperty<bool> IsUserNameSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUserNameSupported));
	static readonly StyledProperty<bool> IsWorkingDirectorySupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsWorkingDirectorySupported));
	static readonly StyledProperty<Uri?> QueryStringReferenceUriProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, Uri?>("QueryStringReferenceUri");


	// Fields.
	readonly TextBox categoryTextBox;
	readonly TextBox commandTextBox;
	Range<int> commandTextBoxSelection;
	readonly TextBox connectionStringStringTextBox;
	readonly ComboBox encodingComboBox;
	readonly TextBox fileNameTextBox;
	readonly ToggleSwitch formatJsonDataSwitch;
	readonly ToggleSwitch formatXmlDataSwitch;
	readonly ToggleSwitch includeStderrSwitch;
	readonly IPAddressTextBox ipAddressTextBox;
	readonly ToggleSwitch isResourceOnAzureSwitch;
	readonly TextBox passwordTextBox;
	readonly IntegerTextBox portTextBox;
	readonly TextBox queryStringTextBox;
	readonly ScheduledAction saveCommandTextBoxSelectionAction;
	readonly ObservableList<string> setupCommands = new();
	readonly AppSuite.Controls.ListBox setupCommandsListBox;
	readonly ObservableList<string> teardownCommands = new();
	readonly AppSuite.Controls.ListBox teardownCommandsListBox;
	readonly UriTextBox uriTextBox;
	readonly TextBox userNameTextBox;
	readonly ToggleSwitch useTextShellSwitch;
	readonly TextBox workingDirectoryTextBox;


	/// <summary>
	/// Initialize new <see cref="LogDataSourceOptionsDialog"/>.
	/// </summary>
	public LogDataSourceOptionsDialog()
	{
		this.EditSetupTeardownCommandCommand = new Command<ListBoxItem>(this.EditSetupTeardownCommand);
		this.MoveSetupTeardownCommandDownCommand = new Command<ListBoxItem>(this.MoveSetupTeardownCommandDown);
		this.MoveSetupTeardownCommandUpCommand = new Command<ListBoxItem>(this.MoveSetupTeardownCommandUp);
		this.RemoveSetupTeardownCommandCommand = new Command<ListBoxItem>(this.RemoveSetupTeardownCommand);
		this.CommandSyntaxHighlightingDefinitionSet = Highlighting.TextShellCommandSyntaxHighlighting.CreateDefinitionSet(this.Application);
		this.SqlSyntaxHighlightingDefinitionSet = Highlighting.SqlSyntaxHighlighting.CreateDefinitionSet(this.Application);
		AvaloniaXamlLoader.Load(this);
		this.categoryTextBox = this.Get<TextBox>(nameof(categoryTextBox));
		this.commandTextBox = this.Get<TextBox>(nameof(commandTextBox)).Also(it =>
		{
			it.GotFocus += (_, e) =>
			{
				this.saveCommandTextBoxSelectionAction?.Cancel();
				it.SelectionStart = this.commandTextBoxSelection.Start.GetValueOrDefault();
				it.SelectionEnd = this.commandTextBoxSelection.End.GetValueOrDefault();
			};
			it.LostFocus += (_, e) =>
				this.saveCommandTextBoxSelectionAction?.Cancel();
			it.GetObservable(TextBox.SelectionEndProperty).Subscribe(end =>
				this.saveCommandTextBoxSelectionAction?.Schedule());
			it.GetObservable(TextBox.SelectionStartProperty).Subscribe(start =>
				this.saveCommandTextBoxSelectionAction?.Schedule());
		});
		this.connectionStringStringTextBox = this.Get<TextBox>(nameof(connectionStringStringTextBox));
		this.encodingComboBox = this.Get<ComboBox>(nameof(encodingComboBox));
		this.fileNameTextBox = this.Get<TextBox>(nameof(fileNameTextBox));
		this.formatJsonDataSwitch = this.Get<ToggleSwitch>(nameof(formatJsonDataSwitch));
		this.formatXmlDataSwitch = this.Get<ToggleSwitch>(nameof(formatXmlDataSwitch));
		this.includeStderrSwitch = this.Get<ToggleSwitch>(nameof(includeStderrSwitch));
		this.ipAddressTextBox = this.Get<IPAddressTextBox>(nameof(ipAddressTextBox));
		this.isResourceOnAzureSwitch = this.Get<ToggleSwitch>(nameof(isResourceOnAzureSwitch));
		this.passwordTextBox = this.Get<TextBox>(nameof(passwordTextBox));
		this.portTextBox = this.Get<IntegerTextBox>(nameof(portTextBox));
		this.queryStringTextBox = this.Get<TextBox>(nameof(queryStringTextBox));
		this.saveCommandTextBoxSelectionAction = new(() =>
		{
			if (this.commandTextBox.IsFocused)
				this.commandTextBoxSelection = new(this.commandTextBox.SelectionStart, this.commandTextBox.SelectionEnd);
		});
		this.setupCommands.CollectionChanged += (_, e) => this.InvalidateInput();
		this.setupCommandsListBox = this.Get<AppSuite.Controls.ListBox>(nameof(setupCommandsListBox));
		this.teardownCommands.CollectionChanged += (_, e) => this.InvalidateInput();
		this.teardownCommandsListBox = this.Get<AppSuite.Controls.ListBox>(nameof(teardownCommandsListBox));
		this.uriTextBox = this.Get<UriTextBox>(nameof(uriTextBox));
		this.userNameTextBox = this.Get<TextBox>(nameof(userNameTextBox));
		this.useTextShellSwitch = this.Get<ToggleSwitch>(nameof(useTextShellSwitch));
		this.workingDirectoryTextBox = this.Get<TextBox>(nameof(workingDirectoryTextBox));
	}


	/// <summary>
	/// Add setup command.
	/// </summary>
	public async void AddSetupCommand()
	{
		var command = (await new TextShellCommandInputDialog().ShowDialog<string?>(this))?.Trim();
		if (!string.IsNullOrWhiteSpace(command))
		{
			this.setupCommands.Add(command);
			this.SelectListBoxItem(this.setupCommandsListBox, this.setupCommands.Count - 1);
		}
	}


	/// <summary>
	/// Add teardown command.
	/// </summary>
	public async void AddTeardownCommand()
	{
		var command = (await new TextShellCommandInputDialog().ShowDialog<string?>(this))?.Trim();
		if (!string.IsNullOrWhiteSpace(command))
		{
			this.teardownCommands.Add(command);
			this.SelectListBoxItem(this.teardownCommandsListBox, this.teardownCommands.Count - 1);
		}
	}


	/// <summary>
	/// Syntax highlighting definition set for text-shell command.
	/// </summary>
	public SyntaxHighlightingDefinitionSet CommandSyntaxHighlightingDefinitionSet { get; }


	/// <summary>
	/// Get or set <see cref="ILogDataSourceProvider"/> which needs the <see cref="LogDataSourceOptions"/>.
	/// </summary>
	public ILogDataSourceProvider? DataSourceProvider
	{
		get => this.GetValue<ILogDataSourceProvider?>(DataSourceProviderProperty);
		set => this.SetValue<ILogDataSourceProvider?>(DataSourceProviderProperty, value);
	}


	/// <summary>
	/// All encodings.
	/// </summary>
	public IList<Encoding> Encodings { get; } = Encoding.GetEncodings().Let(it =>
	{
		var array = new Encoding[it.Length];
		for (var i = it.Length - 1; i >= 0; --i)
			array[i] = it[i].GetEncoding();
		return array;
	});


	// Edit given setup or teardown command.
	async void EditSetupTeardownCommand(ListBoxItem item)
	{
		// find index of command
		var listBox = (Avalonia.Controls.ListBox)item.Parent.AsNonNull();
		var isSetupCommand = (listBox == this.setupCommandsListBox);
		var index = isSetupCommand ? this.setupCommands.IndexOf((string)item.DataContext.AsNonNull()) : this.teardownCommands.IndexOf((string)item.DataContext.AsNonNull());
		if (index < 0)
			return;

		// edit
		var newCommand = (await new TextShellCommandInputDialog()
		{
			InitialCommand = (item.DataContext as string),
		}.ShowDialog<string?>(this))?.Trim();
		if (string.IsNullOrEmpty(newCommand))
			return;

		// update command
		if (isSetupCommand)
			this.setupCommands[index] = newCommand;
		else
			this.teardownCommands[index] = newCommand;
		this.SelectListBoxItem((Avalonia.Controls.ListBox)item.Parent.AsNonNull(), index);
	}


	/// <summary>
	/// Comand to edit setup/teardown command.
	/// </summary>
	public ICommand EditSetupTeardownCommandCommand { get; }


	// Generate valid result.
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var options = new LogDataSourceOptions();
		if (this.IsCategorySupported)
			options.Category = this.categoryTextBox.Text?.Trim();
		if (this.IsCommandSupported)
			options.Command = this.commandTextBox.Text?.Trim();
		if (this.GetValue<bool>(IsConnectionStringSupportedProperty))
			options.ConnectionString = this.connectionStringStringTextBox.Text?.Trim();
		if (this.IsEncodingSupported)
			options.Encoding = this.encodingComboBox.SelectedItem as Encoding;
		if (this.IsFileNameSupported)
			options.FileName = this.fileNameTextBox.Text?.Trim();
		if (this.GetValue<bool>(IsFormatJsonDataSupportedProperty))
			options.FormatJsonData = this.formatJsonDataSwitch.IsChecked.GetValueOrDefault();
		if (this.GetValue<bool>(IsFormatXmlDataSupportedProperty))
			options.FormatXmlData = this.formatXmlDataSwitch.IsChecked.GetValueOrDefault();
		if (this.GetValue<bool>(IsIncludeStandardErrorSupportedProperty))
			options.IncludeStandardError = this.includeStderrSwitch.IsChecked.GetValueOrDefault();
		if (this.IsIPEndPointSupported)
		{
			this.ipAddressTextBox.Object?.Let(address =>
			{
				options.IPEndPoint = new IPEndPoint(address, (int)this.portTextBox.Value.GetValueOrDefault());
			});
		}
		if (this.IsQueryStringSupported)
			options.QueryString = this.queryStringTextBox.Text?.Trim();
		if (this.IsPasswordSupported)
			options.Password = this.passwordTextBox.Text?.Trim();
		if (this.GetValue<bool>(IsResourceOnAzureSupportedProperty))
			options.IsResourceOnAzure = this.isResourceOnAzureSwitch.IsChecked.GetValueOrDefault();
		if (this.IsSetupCommandsSupported)
			options.SetupCommands = this.setupCommands;
		if (this.IsTeardownCommandsSupported)
			options.TeardownCommands = this.teardownCommands;
		if (this.IsUriSupported)
			options.Uri = this.uriTextBox.Object;
		if (this.IsUserNameSupported)
			options.UserName = this.userNameTextBox.Text?.Trim();
		if (this.GetValue<bool>(IsUseTextShellSupportedProperty))
			options.UseTextShell = this.useTextShellSwitch.IsChecked.GetValueOrDefault();
		if (this.IsWorkingDirectorySupported)
			options.WorkingDirectory = this.workingDirectoryTextBox.Text?.Trim();
		return Task.FromResult((object?)options);
	}


	// Data source options states.
	bool IsCategoryRequired { get => this.GetValue<bool>(IsCategoryRequiredProperty); }
	bool IsCategorySupported { get => this.GetValue<bool>(IsCategorySupportedProperty); }
	bool IsCommandRequired { get => this.GetValue<bool>(IsCommandRequiredProperty); }
	bool IsCommandSupported { get => this.GetValue<bool>(IsCommandSupportedProperty); }
	bool IsFileNameSupported { get => this.GetValue<bool>(IsFileNameSupportedProperty); }
	bool IsEncodingSupported { get => this.GetValue<bool>(IsEncodingSupportedProperty); }
	bool IsIPEndPointSupported { get => this.GetValue<bool>(IsIPEndPointSupportedProperty); }
	bool IsPasswordRequired { get => this.GetValue<bool>(IsPasswordRequiredProperty); }
	bool IsPasswordSupported { get => this.GetValue<bool>(IsPasswordSupportedProperty); }
	bool IsQueryStringRequired { get => this.GetValue<bool>(IsQueryStringRequiredProperty); }
	bool IsQueryStringSupported { get => this.GetValue<bool>(IsQueryStringSupportedProperty); }
	bool IsSetupCommandsRequired { get => this.GetValue<bool>(IsSetupCommandsRequiredProperty); }
	bool IsSetupCommandsSupported { get => this.GetValue<bool>(IsSetupCommandsSupportedProperty); }
	bool IsTeardownCommandsRequired { get => this.GetValue<bool>(IsTeardownCommandsRequiredProperty); }
	bool IsTeardownCommandsSupported { get => this.GetValue<bool>(IsTeardownCommandsSupportedProperty); }
	bool IsUserNameRequired { get => this.GetValue<bool>(IsUserNameRequiredProperty); }
	bool IsUserNameSupported { get => this.GetValue<bool>(IsUserNameSupportedProperty); }
	bool IsUriSupported { get => this.GetValue<bool>(IsUriSupportedProperty); }
	bool IsWorkingDirectorySupported { get => this.GetValue<bool>(IsWorkingDirectorySupportedProperty); }


	/// <summary>
	/// Get or set whether the options is used for template or not.
	/// </summary>
	public bool IsTemplate
	{
		get =>this.GetValue<bool>(IsTemplateProperty);
		set => this.SetValue<bool>(IsTemplateProperty, value);
	}


	// Move given setup or teardown command down.
	void MoveSetupTeardownCommandDown(ListBoxItem item)
	{
		// find index of command
		var listBox = (Avalonia.Controls.ListBox)item.Parent.AsNonNull();
		var index = listBox == this.setupCommandsListBox ? this.setupCommands.IndexOf((string)item.DataContext.AsNonNull()) : this.teardownCommands.IndexOf((string)item.DataContext.AsNonNull());
		if (index < 0)
			return;

		// move command
		var commands = (item.Parent == this.setupCommandsListBox ? this.setupCommands : this.teardownCommands);
		if (index < commands.Count - 1)
		{
			commands.Move(index, index + 1);
			++index;
		}
		this.SelectListBoxItem(listBox, index);
	}


	/// <summary>
	/// Command to move given setup or teardown command down.
	/// </summary>
	public ICommand MoveSetupTeardownCommandDownCommand { get; }


	// Move given setup or teardown command up.
	void MoveSetupTeardownCommandUp(ListBoxItem item)
	{
		// find index of command
		var listBox = (Avalonia.Controls.ListBox)item.Parent.AsNonNull();
		var index = listBox == this.setupCommandsListBox ? this.setupCommands.IndexOf((string)item.DataContext.AsNonNull()) : this.teardownCommands.IndexOf((string)item.DataContext.AsNonNull());
		if (index < 0)
			return;

		// move command
		var commands = (item.Parent == this.setupCommandsListBox ? this.setupCommands : this.teardownCommands);
		if (index > 0)
		{
			commands.Move(index, index - 1);
			--index;
		}
		this.SelectListBoxItem(listBox, index);
	}


	/// <summary>
	/// Command to move given setup or teardown command up.
	/// </summary>
	public ICommand MoveSetupTeardownCommandUpCommand { get; }


	// Called when property of editor control changed.
	void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		var property = e.Property;
		if (property == UriTextBox.IsTextValidProperty
			|| property == IPAddressTextBox.IsTextValidProperty
			|| property == ComboBox.SelectedItemProperty
			|| (property == TextBox.TextProperty && sender is not UriTextBox)
			|| property == UriTextBox.ObjectProperty)
		{
			this.InvalidateInput();
		}
	}


	// Called when double-tapped on list box.
	void OnListBoxDoubleClickOnItem(object? sender, ListBoxItemEventArgs e)
	{
		if (sender is not Avalonia.Controls.ListBox listBox)
			return;
		if (!listBox.TryFindListBoxItem(e.Item, out var listBoxItem) || listBoxItem == null)
			return;
		if (listBox == this.setupCommandsListBox || listBox == this.teardownCommandsListBox)
			this.EditSetupTeardownCommand(listBoxItem);
	}


	// Called when list box lost focus.
	void OnListBoxLostFocus(object? sender, RoutedEventArgs e)
	{
		if (sender is not Avalonia.Controls.ListBox listBox)
			return;
		listBox.SelectedItems?.Clear();
	}


	// Called when selection in list box changed.
	void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		if (sender is not Avalonia.Controls.ListBox listBox)
			return;
		if (listBox.SelectedIndex >= 0)
			listBox.ScrollIntoView(listBox.SelectedIndex);
	}


	// Called when opened.
	protected override void OnOpened(EventArgs e)
	{
		// remove last visible separator
		this.Get<Panel>("itemsPanel").Let(it =>
		{
			for (var i = it.Children.Count - 1; i >= 0; --i)
			{
				var child = it.Children[i];
				if (!child.IsVisible)
					continue;
				if (child is Separator)
					it.Children.RemoveAt(i);
				else if (child is Panel itemPanel)
				{
					child = itemPanel.Children[^1];
					if (child is Separator)
						itemPanel.Children.RemoveAt(itemPanel.Children.Count - 1);
				}
				break;
			}
		});
		
		// put options to control, must keep same order as controls in window
		var options = this.Options;
		var firstEditor = (Control?)null;
		if (this.IsCategorySupported)
		{
			this.categoryTextBox.Text = options.Category;
			firstEditor ??= this.categoryTextBox;
		}
		if (this.IsCommandSupported)
		{
			this.commandTextBox.Text = options.Command;
			firstEditor ??= this.commandTextBox;
		}
		if (this.GetValue<bool>(IsConnectionStringSupportedProperty))
		{
			this.connectionStringStringTextBox.Text = options.ConnectionString;
			firstEditor ??= this.connectionStringStringTextBox;
		}
		if (this.IsFileNameSupported)
		{
			this.fileNameTextBox.Text = options.FileName;
			firstEditor ??= this.fileNameTextBox;
		}
		if (this.GetValue<bool>(IsFormatJsonDataSupportedProperty))
			this.formatJsonDataSwitch.IsChecked = options.FormatJsonData;
		if (this.GetValue<bool>(IsFormatXmlDataSupportedProperty))
			this.formatXmlDataSwitch.IsChecked = options.FormatXmlData;
		if (this.GetValue<bool>(IsIncludeStandardErrorSupportedProperty))
			this.includeStderrSwitch.IsChecked = options.IncludeStandardError;
		if (this.IsWorkingDirectorySupported)
		{
			this.workingDirectoryTextBox.Text = options.WorkingDirectory;
			firstEditor ??= this.workingDirectoryTextBox;
		}
		if (this.IsIPEndPointSupported)
		{
			options.IPEndPoint?.Let(it =>
			{
				this.ipAddressTextBox.Object = it.Address;
				this.portTextBox.Value = it.Port;
			});
			firstEditor ??= this.ipAddressTextBox;
		}
		if (this.GetValue<bool>(IsResourceOnAzureSupportedProperty))
			this.isResourceOnAzureSwitch.IsChecked = options.IsResourceOnAzure;
		if (this.IsUriSupported)
		{
			this.uriTextBox.Object = options.Uri;
			firstEditor ??= this.uriTextBox;
		}
		if (this.IsEncodingSupported)
		{
			this.encodingComboBox.SelectedItem = options.Encoding ?? Encoding.UTF8;
			firstEditor ??= this.encodingComboBox;
		}
		if (this.IsQueryStringSupported)
		{
			this.queryStringTextBox.Text = options.QueryString;
			firstEditor ??= this.queryStringTextBox;
		}
		if (this.IsUserNameSupported)
		{
			this.userNameTextBox.Text = options.UserName;
			firstEditor ??= this.userNameTextBox;
		}
		if (this.IsPasswordSupported)
		{
			this.passwordTextBox.Text = options.Password;
			firstEditor ??= this.passwordTextBox;
		}
		if (this.IsSetupCommandsSupported)
		{
			this.setupCommands.AddRange(options.SetupCommands);
			firstEditor ??= this.setupCommandsListBox;
		}
		if (this.GetValue<bool>(IsUseTextShellSupportedProperty))
		{
			this.useTextShellSwitch.IsChecked = options.UseTextShell;
			firstEditor ??= this.useTextShellSwitch;
		}
		if (this.IsTeardownCommandsSupported)
		{
			this.teardownCommands.AddRange(options.TeardownCommands);
			firstEditor ??= this.teardownCommandsListBox;
		}

		// move focus to first editor
		if (firstEditor != null)
		{
			if (this.GetValue<bool>(IsAzureRelatedDataSourceProviderProperty)
				&& this.IsCommandSupported
				&& !this.PersistentState.GetValueOrDefault(IsSelectAzureResourcesTutorialShownKey))
			{
				this.Get<TutorialPresenter>("tutorialPresenter").Let(presenter =>
				{
					presenter.ShowTutorial(new Tutorial().Also(it =>
					{
						it.Anchor = this.Get<Control>("selectAzureResourcesButton");
						it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/LogDataSourceOptionsDialog.Tutorial.SelectAzureResources"));
						it.Dismissed += (_, e) =>
						{
							this.PersistentState.SetValue<bool>(IsSelectAzureResourcesTutorialShownKey, true);
							firstEditor.Focus();
						};
						it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
						it.IsSkippingAllTutorialsAllowed = false;
					}));
				});
			}
			else
				firstEditor.Focus();
		}
		else
			this.SynchronizationContext.Post(this.Close);

		// call base
		base.OnOpened(e);
	}


	// Called when property changed.
	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == DataSourceProviderProperty
			|| change.Property == IsTemplateProperty)
		{
			this.RefreshOptionStates();
		}
	}


	// Validate input.
	protected override bool OnValidateInput()
	{
		if (!base.OnValidateInput())
			return false;
		if (this.IsCategoryRequired && string.IsNullOrWhiteSpace(this.categoryTextBox.Text))
			return false;
		if (this.IsCommandRequired && string.IsNullOrWhiteSpace(this.commandTextBox.Text))
			return false;
		if (this.GetValue<bool>(IsConnectionStringRequiredProperty) && string.IsNullOrWhiteSpace(this.connectionStringStringTextBox.Text))
			return false;
		if (this.IsIPEndPointSupported && !this.ipAddressTextBox.IsTextValid)
			return false;
		if (this.IsQueryStringRequired && string.IsNullOrWhiteSpace(this.queryStringTextBox.Text))
			return false;
		if (this.IsPasswordRequired && string.IsNullOrWhiteSpace(this.passwordTextBox.Text))
			return false;
		if (this.IsSetupCommandsRequired && this.setupCommands.IsEmpty())
			return false;
		if (this.IsTeardownCommandsRequired && this.teardownCommands.IsEmpty())
			return false;
		if (this.IsUserNameRequired && string.IsNullOrWhiteSpace(this.userNameTextBox.Text))
			return false;
		return true;
	}


	/// <summary>
	/// Get or set <see cref="LogDataSourceOptions"/> to be edited.
	/// </summary>
	public LogDataSourceOptions Options { get; set; }


	// Refresh state of all options.
	void RefreshOptionStates()
	{
		var provider = this.GetValue<ILogDataSourceProvider?>(DataSourceProviderProperty);
		if (provider != null)
		{
			var isTemplate = this.GetValue<bool>(IsTemplateProperty);
			this.SetValue<Uri?>(CategoryReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.Category)));
			this.SetValue<Uri?>(CommandReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.Command)));
			this.SetValue<Uri?>(ConnectionStringReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.ConnectionString)));
			this.SetValue<bool>(IsAzureRelatedDataSourceProviderProperty, provider is AzureCliLogDataSourceProvider);
			this.SetValue<bool>(IsCategoryRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Category)));
			this.SetValue<bool>(IsCategorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Category)));
			this.SetValue<bool>(IsCommandRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Command)));
			this.SetValue<bool>(IsCommandSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Command)));
			this.SetValue<bool>(IsConnectionStringRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.ConnectionString)));
			this.SetValue<bool>(IsConnectionStringSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.ConnectionString)));
			this.SetValue<bool>(IsEncodingSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Encoding)));
			this.SetValue<bool>(IsFileNameSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FileName)));
			this.SetValue<bool>(IsFormatJsonDataSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FormatJsonData)));
			this.SetValue<bool>(IsFormatXmlDataSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FormatXmlData)));
			this.SetValue<bool>(IsIncludeStandardErrorSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IncludeStandardError)));
			this.SetValue<bool>(IsIPEndPointSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IPEndPoint)));
			this.SetValue<bool>(IsPasswordRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Password)));
			this.SetValue<bool>(IsPasswordSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Password)));
			this.SetValue<bool>(IsQueryStringRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.QueryString)));
			this.SetValue<bool>(IsQueryStringSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.QueryString)));
			this.SetValue<bool>(IsResourceOnAzureSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IsResourceOnAzure)));
			this.SetValue<bool>(IsSetupCommandsRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.SetupCommands)));
			this.SetValue<bool>(IsSetupCommandsSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.SetupCommands)));
			this.SetValue<bool>(IsTeardownCommandsRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.TeardownCommands)));
			this.SetValue<bool>(IsTeardownCommandsSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.TeardownCommands)));
			this.SetValue<bool>(IsUriSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Uri)));
			this.SetValue<bool>(IsUserNameRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.UserName)));
			this.SetValue<bool>(IsUserNameSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UserName)));
			this.SetValue<bool>(IsUseTextShellSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UseTextShell)));
			this.SetValue<bool>(IsWorkingDirectorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory)));
			this.SetValue<Uri?>(QueryStringReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.QueryString)));
		}
		else
		{
			this.SetValue<Uri?>(CategoryReferenceUriProperty, null);
			this.SetValue<Uri?>(CommandReferenceUriProperty, null);
			this.SetValue<Uri?>(ConnectionStringReferenceUriProperty, null);
			this.SetValue<bool>(IsAzureRelatedDataSourceProviderProperty, false);
			this.SetValue<bool>(IsCategoryRequiredProperty, false);
			this.SetValue<bool>(IsCategorySupportedProperty, false);
			this.SetValue<bool>(IsCommandRequiredProperty, false);
			this.SetValue<bool>(IsCommandSupportedProperty, false);
			this.SetValue<bool>(IsConnectionStringRequiredProperty, false);
			this.SetValue<bool>(IsConnectionStringSupportedProperty, false);
			this.SetValue<bool>(IsEncodingSupportedProperty, false);
			this.SetValue<bool>(IsFileNameSupportedProperty, false);
			this.SetValue<bool>(IsFormatJsonDataSupportedProperty, false);
			this.SetValue<bool>(IsFormatXmlDataSupportedProperty, false);
			this.SetValue<bool>(IsIncludeStandardErrorSupportedProperty, false);
			this.SetValue<bool>(IsIPEndPointSupportedProperty, false);
			this.SetValue<bool>(IsPasswordRequiredProperty, false);
			this.SetValue<bool>(IsPasswordSupportedProperty, false);
			this.SetValue<bool>(IsQueryStringRequiredProperty, false);
			this.SetValue<bool>(IsQueryStringSupportedProperty, false);
			this.SetValue<bool>(IsResourceOnAzureSupportedProperty, false);
			this.SetValue<bool>(IsSetupCommandsRequiredProperty, false);
			this.SetValue<bool>(IsSetupCommandsSupportedProperty, false);
			this.SetValue<bool>(IsTeardownCommandsRequiredProperty, false);
			this.SetValue<bool>(IsTeardownCommandsSupportedProperty, false);
			this.SetValue<bool>(IsUriSupportedProperty, false);
			this.SetValue<bool>(IsUserNameRequiredProperty, false);
			this.SetValue<bool>(IsUserNameRequiredProperty, false);
			this.SetValue<bool>(IsUseTextShellSupportedProperty, false);
			this.SetValue<bool>(IsWorkingDirectorySupportedProperty, false);
			this.SetValue<Uri?>(QueryStringReferenceUriProperty, null);
		}
	}


	// Remove given setup or teardown command.
	void RemoveSetupTeardownCommand(ListBoxItem item)
	{
		// find index of command
		var listBox = (Avalonia.Controls.ListBox)item.Parent.AsNonNull();
		var index = listBox == this.setupCommandsListBox ? this.setupCommands.IndexOf((string)item.DataContext.AsNonNull()) : this.teardownCommands.IndexOf((string)item.DataContext.AsNonNull());
		if (index < 0)
			return;

		// remove command
		if (listBox == this.setupCommandsListBox)
			this.setupCommands.RemoveAt(index);
		else
			this.teardownCommands.RemoveAt(index);
		this.SelectListBoxItem(listBox, -1);
	}


	/// <summary>
	/// Command to remove given setup or teardown command.
	/// </summary>
	public ICommand RemoveSetupTeardownCommandCommand { get; }


	/// <summary>
	/// Select file name.
	/// </summary>
	public async void SelectFileName()
	{
		var fileName = (await this.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions().Also(options =>
		{
			Path.GetDirectoryName(this.fileNameTextBox.Text?.Trim())?.Let(path =>
			{
				options.SuggestedStartLocation = new Avalonia.Platform.Storage.FileIO.BclStorageFolder(path);
			});
		})))?.Let(it =>
		{
			if (it.Count != 1 || !it[0].TryGetUri(out var uri))
				return null;
			return uri.LocalPath;
		});
		this.fileNameTextBox.Text = fileName;
	}


	// Select given item in list box.
	void SelectListBoxItem(Avalonia.Controls.ListBox listBox, int index)
	{
		this.SynchronizationContext.Post(() =>
		{
			listBox.SelectedItems?.Clear();
			if (index < 0 || index >= listBox.GetItemCount())
				return;
			listBox.Focus();
			listBox.SelectedIndex = index;
			listBox.ScrollIntoView(index);
		});
	}


	/// <summary>
	/// Select working directory.
	/// </summary>
	public async void SelectWorkingDirectory()
	{
		var dirPath = (await this.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions().Also(options =>
		{
			this.workingDirectoryTextBox.Text?.Trim()?.Let(path =>
			{
				options.SuggestedStartLocation = new Avalonia.Platform.Storage.FileIO.BclStorageFolder(path);
			});
		})))?.Let(it =>
		{
			if (it.Count != 1 || !it[0].TryGetUri(out var uri))
				return null;
			return uri.LocalPath;
		});
		if (!string.IsNullOrEmpty(dirPath))
			this.workingDirectoryTextBox.Text = dirPath;
	}


	/// <summary>x
	/// Setup commands.
	/// </summary>
	public IList<string> SetupCommands { get => this.setupCommands; }


	/// <summary>
	/// Show options dialog of default text shell.
	/// </summary>
	public void ShowDefaultTextShellOptions() =>
		this.Application.ShowApplicationOptionsDialogAsync(this, AppOptionsDialog.DefaultTextShellSection);
	

	/// <summary>
	/// Syntax highlighting definition set for SQL.
	/// </summary>
	public SyntaxHighlightingDefinitionSet SqlSyntaxHighlightingDefinitionSet { get; }


	/// <summary>
	/// Teardown commands.
	/// </summary>
	public IList<string> TeardownCommands { get => this.teardownCommands; }
}