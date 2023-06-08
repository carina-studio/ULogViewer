using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.IO;
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
class LogDataSourceOptionsDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
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
	static readonly StyledProperty<bool> IsSelectingFileNameProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsSelectingFileName));
	static readonly StyledProperty<bool> IsSelectingWorkingDirectoryProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsSelectingWorkingDirectory));
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
			it.GotFocus += (_, _) =>
			{
				this.saveCommandTextBoxSelectionAction?.Cancel();
				it.SelectionStart = this.commandTextBoxSelection.Start.GetValueOrDefault();
				it.SelectionEnd = this.commandTextBoxSelection.End.GetValueOrDefault();
			};
			it.LostFocus += (_, _) =>
				this.saveCommandTextBoxSelectionAction?.Cancel();
			it.GetObservable(TextBox.SelectionEndProperty).Subscribe(_ =>
				this.saveCommandTextBoxSelectionAction?.Schedule());
			it.GetObservable(TextBox.SelectionStartProperty).Subscribe(_ =>
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
		this.setupCommands.CollectionChanged += (_, _) => this.InvalidateInput();
		this.setupCommandsListBox = this.Get<AppSuite.Controls.ListBox>(nameof(setupCommandsListBox));
		this.teardownCommands.CollectionChanged += (_, _) => this.InvalidateInput();
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
		get => this.GetValue(DataSourceProviderProperty);
		set => this.SetValue(DataSourceProviderProperty, value);
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
	/// Command to edit setup/teardown command.
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
		if (this.GetValue(IsConnectionStringSupportedProperty))
			options.ConnectionString = this.connectionStringStringTextBox.Text?.Trim();
		if (this.IsEncodingSupported)
			options.Encoding = this.encodingComboBox.SelectedItem as Encoding;
		if (this.IsFileNameSupported)
			options.FileName = this.fileNameTextBox.Text?.Trim();
		if (this.GetValue(IsFormatJsonDataSupportedProperty))
			options.FormatJsonData = this.formatJsonDataSwitch.IsChecked.GetValueOrDefault();
		if (this.GetValue(IsFormatXmlDataSupportedProperty))
			options.FormatXmlData = this.formatXmlDataSwitch.IsChecked.GetValueOrDefault();
		if (this.GetValue(IsIncludeStandardErrorSupportedProperty))
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
		if (this.GetValue(IsResourceOnAzureSupportedProperty))
			options.IsResourceOnAzure = this.isResourceOnAzureSwitch.IsChecked.GetValueOrDefault();
		if (this.IsSetupCommandsSupported)
			options.SetupCommands = this.setupCommands;
		if (this.IsTeardownCommandsSupported)
			options.TeardownCommands = this.teardownCommands;
		if (this.IsUriSupported)
			options.Uri = this.uriTextBox.Object;
		if (this.IsUserNameSupported)
			options.UserName = this.userNameTextBox.Text?.Trim();
		if (this.GetValue(IsUseTextShellSupportedProperty))
			options.UseTextShell = this.useTextShellSwitch.IsChecked.GetValueOrDefault();
		if (this.IsWorkingDirectorySupported)
			options.WorkingDirectory = this.workingDirectoryTextBox.Text?.Trim();
		return Task.FromResult((object?)options);
	}


	// Data source options states.
	bool IsCategoryRequired => this.GetValue(IsCategoryRequiredProperty);
	bool IsCategorySupported => this.GetValue(IsCategorySupportedProperty);
	bool IsCommandRequired => this.GetValue(IsCommandRequiredProperty);
	bool IsCommandSupported => this.GetValue(IsCommandSupportedProperty);
	bool IsFileNameSupported => this.GetValue(IsFileNameSupportedProperty);
	bool IsEncodingSupported => this.GetValue(IsEncodingSupportedProperty);
	bool IsIPEndPointSupported => this.GetValue(IsIPEndPointSupportedProperty);
	bool IsPasswordRequired => this.GetValue(IsPasswordRequiredProperty);
	bool IsPasswordSupported => this.GetValue(IsPasswordSupportedProperty);
	bool IsQueryStringRequired => this.GetValue(IsQueryStringRequiredProperty);
	bool IsQueryStringSupported => this.GetValue(IsQueryStringSupportedProperty);
	bool IsSetupCommandsRequired => this.GetValue(IsSetupCommandsRequiredProperty);
	bool IsSetupCommandsSupported => this.GetValue(IsSetupCommandsSupportedProperty);
	bool IsTeardownCommandsRequired => this.GetValue(IsTeardownCommandsRequiredProperty);
	bool IsTeardownCommandsSupported => this.GetValue(IsTeardownCommandsSupportedProperty);
	bool IsUserNameRequired => this.GetValue(IsUserNameRequiredProperty);
	bool IsUserNameSupported => this.GetValue(IsUserNameSupportedProperty);
	bool IsUriSupported => this.GetValue(IsUriSupportedProperty);
	bool IsWorkingDirectorySupported => this.GetValue(IsWorkingDirectorySupportedProperty);


	/// <summary>
	/// Check whether file name selection is on-going or not.
	/// </summary>
	public bool IsSelectingFileName => this.GetValue(IsSelectingFileNameProperty);


	/// <summary>
	/// Check whether working directory selection is on-going or not.
	/// </summary>
	public bool IsSelectingWorkingDirectory => this.GetValue(IsSelectingWorkingDirectoryProperty);


	/// <summary>
	/// Get or set whether the options is used for template or not.
	/// </summary>
	public bool IsTemplate
	{
		get =>this.GetValue(IsTemplateProperty);
		set => this.SetValue(IsTemplateProperty, value);
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
		if (this.GetValue(IsConnectionStringSupportedProperty))
		{
			this.connectionStringStringTextBox.Text = options.ConnectionString;
			firstEditor ??= this.connectionStringStringTextBox;
		}
		if (this.IsFileNameSupported)
		{
			this.fileNameTextBox.Text = options.FileName;
			firstEditor ??= this.fileNameTextBox;
		}
		if (this.GetValue(IsFormatJsonDataSupportedProperty))
			this.formatJsonDataSwitch.IsChecked = options.FormatJsonData;
		if (this.GetValue(IsFormatXmlDataSupportedProperty))
			this.formatXmlDataSwitch.IsChecked = options.FormatXmlData;
		if (this.GetValue(IsIncludeStandardErrorSupportedProperty))
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
		if (this.GetValue(IsResourceOnAzureSupportedProperty))
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
		if (this.GetValue(IsUseTextShellSupportedProperty))
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
			this.SynchronizationContext.Post(() => firstEditor.Focus());
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
		if (this.GetValue(IsConnectionStringRequiredProperty) && string.IsNullOrWhiteSpace(this.connectionStringStringTextBox.Text))
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
		var provider = this.GetValue(DataSourceProviderProperty);
		if (provider != null)
		{
			var isTemplate = this.GetValue(IsTemplateProperty);
			this.SetValue(CategoryReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.Category)));
			this.SetValue(CommandReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.Command)));
			this.SetValue(ConnectionStringReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.ConnectionString)));
			this.SetValue(IsAzureRelatedDataSourceProviderProperty, provider is AzureCliLogDataSourceProvider);
			this.SetValue(IsCategoryRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Category)));
			this.SetValue(IsCategorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Category)));
			this.SetValue(IsCommandRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Command)));
			this.SetValue(IsCommandSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Command)));
			this.SetValue(IsConnectionStringRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.ConnectionString)));
			this.SetValue(IsConnectionStringSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.ConnectionString)));
			this.SetValue(IsEncodingSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Encoding)));
			this.SetValue(IsFileNameSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FileName)));
			this.SetValue(IsFormatJsonDataSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FormatJsonData)));
			this.SetValue(IsFormatXmlDataSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FormatXmlData)));
			this.SetValue(IsIncludeStandardErrorSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IncludeStandardError)));
			this.SetValue(IsIPEndPointSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IPEndPoint)));
			this.SetValue(IsPasswordRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Password)));
			this.SetValue(IsPasswordSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Password)));
			this.SetValue(IsQueryStringRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.QueryString)));
			this.SetValue(IsQueryStringSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.QueryString)));
			this.SetValue(IsResourceOnAzureSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.IsResourceOnAzure)));
			this.SetValue(IsSetupCommandsRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.SetupCommands)));
			this.SetValue(IsSetupCommandsSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.SetupCommands)));
			this.SetValue(IsTeardownCommandsRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.TeardownCommands)));
			this.SetValue(IsTeardownCommandsSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.TeardownCommands)));
			this.SetValue(IsUriSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Uri)));
			this.SetValue(IsUserNameRequiredProperty, !isTemplate && provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.UserName)));
			this.SetValue(IsUserNameSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UserName)));
			this.SetValue(IsUseTextShellSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UseTextShell)));
			this.SetValue(IsWorkingDirectorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory)));
			this.SetValue(QueryStringReferenceUriProperty, provider.GetSourceOptionReferenceUri(nameof(LogDataSourceOptions.QueryString)));
		}
		else
		{
			this.SetValue(CategoryReferenceUriProperty, null);
			this.SetValue(CommandReferenceUriProperty, null);
			this.SetValue(ConnectionStringReferenceUriProperty, null);
			this.SetValue(IsAzureRelatedDataSourceProviderProperty, false);
			this.SetValue(IsCategoryRequiredProperty, false);
			this.SetValue(IsCategorySupportedProperty, false);
			this.SetValue(IsCommandRequiredProperty, false);
			this.SetValue(IsCommandSupportedProperty, false);
			this.SetValue(IsConnectionStringRequiredProperty, false);
			this.SetValue(IsConnectionStringSupportedProperty, false);
			this.SetValue(IsEncodingSupportedProperty, false);
			this.SetValue(IsFileNameSupportedProperty, false);
			this.SetValue(IsFormatJsonDataSupportedProperty, false);
			this.SetValue(IsFormatXmlDataSupportedProperty, false);
			this.SetValue(IsIncludeStandardErrorSupportedProperty, false);
			this.SetValue(IsIPEndPointSupportedProperty, false);
			this.SetValue(IsPasswordRequiredProperty, false);
			this.SetValue(IsPasswordSupportedProperty, false);
			this.SetValue(IsQueryStringRequiredProperty, false);
			this.SetValue(IsQueryStringSupportedProperty, false);
			this.SetValue(IsResourceOnAzureSupportedProperty, false);
			this.SetValue(IsSetupCommandsRequiredProperty, false);
			this.SetValue(IsSetupCommandsSupportedProperty, false);
			this.SetValue(IsTeardownCommandsRequiredProperty, false);
			this.SetValue(IsTeardownCommandsSupportedProperty, false);
			this.SetValue(IsUriSupportedProperty, false);
			this.SetValue(IsUserNameRequiredProperty, false);
			this.SetValue(IsUserNameRequiredProperty, false);
			this.SetValue(IsUseTextShellSupportedProperty, false);
			this.SetValue(IsWorkingDirectorySupportedProperty, false);
			this.SetValue(QueryStringReferenceUriProperty, null);
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
		if (this.IsSelectingFileName)
			return;
		this.SetValue(IsSelectingFileNameProperty, true);
		var options = await new FilePickerOpenOptions().AlsoAsync(async options =>
		{
			await (Path.GetDirectoryName(this.fileNameTextBox.Text?.Trim())?.LetAsync(async path =>
			{
				if (path.IsValidFilePath() && await CarinaStudio.IO.Directory.ExistsAsync(path))
					options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(path);
			}) ?? Task.CompletedTask);
		});
		var fileName = (await this.StorageProvider.OpenFilePickerAsync(options)).Let(it => 
			it.Count == 1 ? it[0].TryGetLocalPath() : null);
		if (!string.IsNullOrEmpty(fileName))
			this.fileNameTextBox.Text = fileName;
		this.SetValue(IsSelectingFileNameProperty, false);
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
		if (this.IsSelectingWorkingDirectory)
			return;
		this.SetValue(IsSelectingWorkingDirectoryProperty, true);
		var options = await new FolderPickerOpenOptions().AlsoAsync(async options =>
		{
			await (this.workingDirectoryTextBox.Text?.Trim().LetAsync(async path =>
			{
				if (path.IsValidFilePath() && await CarinaStudio.IO.Directory.ExistsAsync(path))
					options.SuggestedStartLocation = await this.StorageProvider.TryGetFolderFromPathAsync(path);
			}) ?? Task.CompletedTask);
		});
		var dirPath = (await this.StorageProvider.OpenFolderPickerAsync(options)).Let(it => 
			it.Count == 1 ? it[0].TryGetLocalPath() : null);
		if (!string.IsNullOrEmpty(dirPath))
			this.workingDirectoryTextBox.Text = dirPath;
		this.SetValue(IsSelectingWorkingDirectoryProperty, false);
	}


	/// <summary>x
	/// Setup commands.
	/// </summary>
	public IList<string> SetupCommands => this.setupCommands;


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
	public IList<string> TeardownCommands => this.teardownCommands;
}