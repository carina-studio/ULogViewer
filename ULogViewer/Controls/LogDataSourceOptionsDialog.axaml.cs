using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="LogDataSourceOptions"/>.
	/// </summary>
	partial class LogDataSourceOptionsDialog : BaseDialog
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
		/// Property of <see cref="UnderlyingLogDataSource"/>.
		/// </summary>
		public static readonly AvaloniaProperty<UnderlyingLogDataSource> UnderlyingLogDataSourceProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, UnderlyingLogDataSource>(nameof(UnderlyingLogDataSource), UnderlyingLogDataSource.Undefined);


		// Fields.
		readonly TextBox categoryTextBox;
		readonly TextBox commandTextBox;
		readonly TextBox dbFileNameTextBox;
		readonly TextBox dbPasswordTextBox;
		readonly TextBox dbQueryStringTextBox;
		readonly EnumComboBox dbSourceTypeComboBox;
		readonly UriTextBox dbUriTextBox;
		readonly TextBox dbUserNameTextBox;
		readonly ComboBox encodingComboBox;
		readonly TextBox fileNameTextBox;
		readonly ObservableList<string> setupCommands = new ObservableList<string>();
		readonly ListBox setupCommandsListBox;
		readonly ObservableList<string> teardownCommands = new ObservableList<string>();
		readonly ListBox teardownCommandsListBox;
		readonly UriTextBox uriTextBox;
		readonly TextBox workingDirectoryTextBox;
		readonly TextBox wrPasswordTextBox;
		readonly TextBox wrUserNameTextBox;


		/// <summary>
		/// Initialize new <see cref="LogDataSourceOptionsDialog"/>.
		/// </summary>
		public LogDataSourceOptionsDialog()
		{
			InitializeComponent();
			this.categoryTextBox = this.FindControl<TextBox>("categoryTextBox").AsNonNull();
			this.commandTextBox = this.FindControl<TextBox>("commandTextBox").AsNonNull();
			this.dbFileNameTextBox = this.FindControl<TextBox>("dbFileNameTextBox").AsNonNull();
			this.dbPasswordTextBox = this.FindControl<TextBox>("dbPasswordTextBox").AsNonNull();
			this.dbQueryStringTextBox = this.FindControl<TextBox>("dbQueryStringTextBox").AsNonNull();
			this.dbSourceTypeComboBox = this.FindControl<EnumComboBox>("dbSourceTypeComboBox").AsNonNull().Also(it =>
			{
				it.EnumType = typeof(DatabaseSourceType);
			});
			this.dbUriTextBox = this.FindControl<UriTextBox>("dbUriTextBox").AsNonNull();
			this.dbUserNameTextBox = this.FindControl<TextBox>("dbUserNameTextBox").AsNonNull();
			this.encodingComboBox = this.FindControl<ComboBox>("encodingComboBox").AsNonNull();
			this.fileNameTextBox = this.FindControl<TextBox>("fileNameTextBox").AsNonNull();
			this.setupCommandsListBox = this.FindControl<ListBox>("setupCommandsListBox").AsNonNull();
			this.teardownCommandsListBox = this.FindControl<ListBox>("teardownCommandsListBox").AsNonNull();
			this.uriTextBox = this.FindControl<UriTextBox>("uriTextBox").AsNonNull();
			this.workingDirectoryTextBox = this.FindControl<TextBox>("workingDirectoryTextBox").AsNonNull();
			this.wrPasswordTextBox = this.FindControl<TextBox>("wrPasswordTextBox").AsNonNull();
			this.wrUserNameTextBox = this.FindControl<TextBox>("wrUserNameTextBox").AsNonNull();
		}


		// Add setup command.
		async void AddSetupCommand()
		{
			var command = (await new TextInputDialog()
			{
				Message = this.Application.GetString("LogDataSourceOptionsDialog.Command"),
				Title = this.Application.GetString("LogDataSourceOptionsDialog.SetupCommands"),
			}.ShowDialog<string>(this))?.Trim();
			if (!string.IsNullOrEmpty(command))
				this.setupCommands.Add(command);
		}


		// Add teardown command.
		async void AddTeardownCommand()
		{
			var command = (await new TextInputDialog()
			{
				Message = this.Application.GetString("LogDataSourceOptionsDialog.Command"),
				Title = this.Application.GetString("LogDataSourceOptionsDialog.TeardownCommands"),
			}.ShowDialog<string>(this))?.Trim();
			if (!string.IsNullOrEmpty(command))
				this.teardownCommands.Add(command);
		}


		// All encodings.
		IList<EncodingInfo> Encodings { get; } = Encoding.GetEncodings();


		// Edit given setup or teardown command.
		async void EditSetupTeardownCommand(ListBoxItem item)
		{
			// find index of command
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;

			// edit
			var isSetupCommand = (item.Parent == this.setupCommandsListBox);
			var newCommand = (await new TextInputDialog()
			{
				Message = this.Application.GetString("LogDataSourceOptionsDialog.Command"),
				Text = (item.DataContext as string),
				Title = this.Application.GetString(isSetupCommand ? "LogDataSourceOptionsDialog.SetupCommands" : "LogDataSourceOptionsDialog.TeardownCommands"),
			}.ShowDialog<string>(this))?.Trim();
			if (string.IsNullOrEmpty(newCommand))
				return;

			// update command
			if (isSetupCommand)
				this.setupCommands[index] = newCommand;
			else
				this.teardownCommands[index] = newCommand;
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Move given setup or teardown command down.
		void MoveSetupTeardownCommandDown(ListBoxItem item)
		{
			// find index of command
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;

			// move command
			var commands = (item.Parent == this.setupCommandsListBox ? this.setupCommands : this.teardownCommands);
			if (index < commands.Count - 1)
				commands.Move(index, index + 1);
		}


		// Move given setup or teardown command up.
		void MoveSetupTeardownCommandUp(ListBoxItem item)
		{
			// find index of command
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index <= 0)
				return;

			// move command
			var commands = (item.Parent == this.setupCommandsListBox ? this.setupCommands : this.teardownCommands);
			commands.Move(index, index - 1);
		}


		// Called when property of editor control changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			var property = e.Property;
			if (property == UriTextBox.IsTextValidProperty
				|| property == ComboBox.SelectedItemProperty
				|| (property == TextBox.TextProperty && sender is not UriTextBox)
				|| property == UriTextBox.UriProperty)
			{
				this.InvalidateInput();
			}
		}


		// Generate valid result.
		protected override object? OnGenerateResult()
		{
			var options = new LogDataSourceOptions();
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.Database:
					options.QueryString = this.dbQueryStringTextBox.Text.AsNonNull().Trim();
					switch ((DatabaseSourceType)this.dbSourceTypeComboBox.SelectedItem.AsNonNull())
					{
						case DatabaseSourceType.File:
							options.FileName = this.dbFileNameTextBox.Text?.Trim();
							break;
						case DatabaseSourceType.Network:
							options.Uri = this.dbUriTextBox.Uri.AsNonNull();
							break;
					}
					options.UserName = this.dbUserNameTextBox.Text?.Trim();
					options.Password = this.dbPasswordTextBox.Text?.Trim();
					break;
				case UnderlyingLogDataSource.File:
					options.FileName = this.fileNameTextBox.Text?.Trim();
					options.Encoding = this.encodingComboBox.SelectedItem?.Let(it =>
					{
						return ((EncodingInfo)it).GetEncoding();
					});
					break;
				case UnderlyingLogDataSource.StandardOutput:
					options.Command = this.commandTextBox.Text.AsNonNull().Trim();
					options.SetupCommands = this.setupCommands;
					options.TeardownCommands = this.teardownCommands;
					options.WorkingDirectory = this.workingDirectoryTextBox.Text?.Trim();
					break;
				case UnderlyingLogDataSource.WebRequest:
					options.Password = this.wrPasswordTextBox.Text?.Trim();
					options.Uri = this.uriTextBox.Uri.AsNonNull();
					options.UserName = this.wrUserNameTextBox.Text?.Trim();
					break;
				case UnderlyingLogDataSource.WindowsEventLogs:
					options.Category = this.categoryTextBox.Text.AsNonNull().Trim();
					break;
			}
			return options;
		}


		// Called when selection in list box changed.
		void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			if (e.AddedItems.Count > 0)
				this.SynchronizationContext.Post(() => listBox.SelectedItem = null);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var options = this.Options;
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.Database:
					this.dbQueryStringTextBox.Text = options.QueryString?.Trim();
					if (options.Uri != null)
					{
						this.dbSourceTypeComboBox.SelectedItem = DatabaseSourceType.Network;
						this.dbUriTextBox.Uri = options.Uri;
						this.dbUriTextBox.Focus();
					}
					else
					{
						this.dbSourceTypeComboBox.SelectedItem = DatabaseSourceType.File;
						this.dbFileNameTextBox.Text = options.FileName?.Trim();
						this.dbFileNameTextBox.Focus();
					}
					this.dbUserNameTextBox.Text = options.UserName?.Trim();
					this.dbPasswordTextBox.Text = options.Password?.Trim();
					break;
				case UnderlyingLogDataSource.File:
					this.fileNameTextBox.Text = options.FileName?.Trim();
					this.encodingComboBox.SelectedItem = options.Encoding?.Let(it =>
					{
						return this.Encodings.FirstOrDefault(info => info.CodePage == it.CodePage);
					}) ?? Global.Run(() =>
					{
						var utf8CodePage = Encoding.UTF8.CodePage;
						return this.Encodings.FirstOrDefault(info => info.CodePage == utf8CodePage) ?? this.Encodings[0];
					});
					this.fileNameTextBox.Focus();
					break;
				case UnderlyingLogDataSource.StandardOutput:
					this.commandTextBox.Text = options.Command?.Trim();
					this.setupCommands.AddRange(options.SetupCommands);
					this.teardownCommands.AddRange(options.TeardownCommands);
					this.workingDirectoryTextBox.Text = options.WorkingDirectory;
					this.commandTextBox.Focus();
					break;
				case UnderlyingLogDataSource.WebRequest:
					this.uriTextBox.Uri = options.Uri;
					this.wrPasswordTextBox.Text = options.Password?.Trim();
					this.wrUserNameTextBox.Text = options.UserName?.Trim();
					this.uriTextBox.Focus();
					break;
				case UnderlyingLogDataSource.WindowsEventLogs:
					this.categoryTextBox.Text = options.Category?.Trim();
					this.categoryTextBox.Focus();
					break;
			}
			base.OnOpened(e);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			if (!base.OnValidateInput())
				return false;
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.Database:
					if (string.IsNullOrWhiteSpace(this.dbQueryStringTextBox.Text))
						return false;
					return this.dbSourceTypeComboBox.SelectedItem.Let(type =>
					{
						if (type == null)
							return false;
						return (DatabaseSourceType)type switch
						{
							DatabaseSourceType.File => true,
							DatabaseSourceType.Network => this.dbUriTextBox.Let(it => it.Validate() && it.Uri != null),
							_ => false,
						};
					});
				case UnderlyingLogDataSource.File:
					return true;
				case UnderlyingLogDataSource.StandardOutput:
					return !string.IsNullOrEmpty(this.commandTextBox.Text?.Trim());
				case UnderlyingLogDataSource.WebRequest:
					return this.uriTextBox.Let(it => it.Validate() && it.Uri != null);
				case UnderlyingLogDataSource.WindowsEventLogs:
					return !string.IsNullOrWhiteSpace(this.categoryTextBox.Text);
				default:
					return false;
			}
		}


		/// <summary>
		/// Get or set <see cref="LogDataSourceOptions"/> to be edited.
		/// </summary>
		public LogDataSourceOptions Options { get; set; }


		// Remove given setup or teardown command.
		void RemoveSetupTeardownCommand(ListBoxItem item)
		{
			// find index of command
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;

			// remove command
			if (item.Parent == this.setupCommandsListBox)
				this.setupCommands.RemoveAt(index);
			else
				this.teardownCommands.RemoveAt(index);
		}


		// Select file name.
		async void SelectFileName()
		{
			var fileNames = await new OpenFileDialog()
			{
				InitialFileName = this.fileNameTextBox.Text?.Trim()
			}.ShowAsync(this);
			if (fileNames == null || fileNames.IsEmpty())
				return;
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.Database:
					this.dbFileNameTextBox.Text = fileNames[0];
					break;
				case UnderlyingLogDataSource.File:
					this.fileNameTextBox.Text = fileNames[0];
					break;
			}
		}


		// Select working directory.
		async void SelectWorkingDirectory()
		{
			var dirPath = await new OpenFolderDialog()
			{
				Directory = this.workingDirectoryTextBox.Text?.Trim()
			}.ShowAsync(this);
			if (!string.IsNullOrEmpty(dirPath))
				this.workingDirectoryTextBox.Text = dirPath;
		}


		// Setup commands.
		IList<string> SetupCommands { get => this.setupCommands; }


		// Teardown commands.
		IList<string> TeardownCommands { get => this.teardownCommands; }


		/// <summary>
		/// Get or set underlying source of <see cref="ILogDataSource"/>.
		/// </summary>
		public UnderlyingLogDataSource UnderlyingLogDataSource
		{
			get => this.GetValue<UnderlyingLogDataSource>(UnderlyingLogDataSourceProperty);
			set => this.SetValue<UnderlyingLogDataSource>(UnderlyingLogDataSourceProperty, value);
		}
	}
}
