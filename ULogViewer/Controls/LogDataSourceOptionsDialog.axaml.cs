using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
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
		public static readonly AvaloniaProperty<ILogDataSourceProvider?> DataSourceProviderProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, ILogDataSourceProvider?>(nameof(DataSourceProvider));


		// Static fields.
		static readonly AvaloniaProperty<bool> IsCategoryRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCategoryRequired));
		static readonly AvaloniaProperty<bool> IsCategorySupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCategorySupported));
		static readonly AvaloniaProperty<bool> IsCommandRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCommandRequired));
		static readonly AvaloniaProperty<bool> IsCommandSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsCommandSupported));
		static readonly AvaloniaProperty<bool> IsEncodingSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsEncodingSupported));
		static readonly AvaloniaProperty<bool> IsFileNameSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsFileNameSupported));
		static readonly AvaloniaProperty<bool> IsPasswordRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsPasswordRequired));
		static readonly AvaloniaProperty<bool> IsPasswordSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsPasswordSupported));
		static readonly AvaloniaProperty<bool> IsQueryStringRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsQueryStringRequired));
		static readonly AvaloniaProperty<bool> IsQueryStringSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsQueryStringSupported));
		static readonly AvaloniaProperty<bool> IsSetupCommandsRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsSetupCommandsRequired));
		static readonly AvaloniaProperty<bool> IsSetupCommandsSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsSetupCommandsSupported));
		static readonly AvaloniaProperty<bool> IsTeardownCommandsRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsTeardownCommandsRequired));
		static readonly AvaloniaProperty<bool> IsTeardownCommandsSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsTeardownCommandsSupported));
		static readonly AvaloniaProperty<bool> IsUriRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUriRequired));
		static readonly AvaloniaProperty<bool> IsUriSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUriSupported));
		static readonly AvaloniaProperty<bool> IsUserNameRequiredProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUserNameRequired));
		static readonly AvaloniaProperty<bool> IsUserNameSupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsUserNameSupported));
		static readonly AvaloniaProperty<bool> IsWorkingDirectorySupportedProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, bool>(nameof(IsWorkingDirectorySupported));


		// Fields.
		readonly TextBox categoryTextBox;
		readonly TextBox commandTextBox;
		readonly ComboBox encodingComboBox;
		readonly TextBox fileNameTextBox;
		readonly TextBox passwordTextBox;
		readonly TextBox queryStringTextBox;
		readonly ObservableList<string> setupCommands = new ObservableList<string>();
		readonly ListBox setupCommandsListBox;
		readonly ObservableList<string> teardownCommands = new ObservableList<string>();
		readonly ListBox teardownCommandsListBox;
		readonly UriTextBox uriTextBox;
		readonly TextBox userNameTextBox;
		readonly TextBox workingDirectoryTextBox;


		/// <summary>
		/// Initialize new <see cref="LogDataSourceOptionsDialog"/>.
		/// </summary>
		public LogDataSourceOptionsDialog()
		{
			InitializeComponent();
			this.categoryTextBox = this.FindControl<TextBox>("categoryTextBox").AsNonNull();
			this.commandTextBox = this.FindControl<TextBox>("commandTextBox").AsNonNull();
			this.FindControl<Panel>("editorsPanel").AsNonNull().Let(it =>
			{
				// remove empty space created by last separator
				var separatorHeight = this.TryFindResource("Double/Dialog.Separator.Height", out var res) && res is double height ? height : 0.0;
				var margin = it.Margin;
				it.Margin = new Thickness(margin.Left, margin.Top, margin.Right, margin.Bottom - separatorHeight);
			});
			this.encodingComboBox = this.FindControl<ComboBox>("encodingComboBox").AsNonNull();
			this.fileNameTextBox = this.FindControl<TextBox>("fileNameTextBox").AsNonNull();
			this.passwordTextBox = this.FindControl<TextBox>("passwordTextBox").AsNonNull();
			this.queryStringTextBox = this.FindControl<TextBox>("queryStringTextBox").AsNonNull();
			this.setupCommands.CollectionChanged += (_, e) => this.InvalidateInput();
			this.setupCommandsListBox = this.FindControl<ListBox>("setupCommandsListBox").AsNonNull();
			this.teardownCommands.CollectionChanged += (_, e) => this.InvalidateInput();
			this.teardownCommandsListBox = this.FindControl<ListBox>("teardownCommandsListBox").AsNonNull();
			this.uriTextBox = this.FindControl<UriTextBox>("uriTextBox").AsNonNull();
			this.userNameTextBox = this.FindControl<TextBox>("userNameTextBox").AsNonNull();
			this.workingDirectoryTextBox = this.FindControl<TextBox>("workingDirectoryTextBox").AsNonNull();
		}


		// Add setup command.
		async void AddSetupCommand()
		{
			var command = (await new AppSuite.Controls.TextInputDialog()
			{
				Message = this.Application.GetString("LogDataSourceOptionsDialog.Command"),
				Title = this.Application.GetString("LogDataSourceOptionsDialog.SetupCommands"),
			}.ShowDialog(this))?.Trim();
			if (!string.IsNullOrWhiteSpace(command))
			{
				this.setupCommands.Add(command);
				this.SelectListBoxItem(this.setupCommandsListBox, this.setupCommands.Count - 1);
			}
		}


		// Add teardown command.
		async void AddTeardownCommand()
		{
			var command = (await new AppSuite.Controls.TextInputDialog()
			{
				Message = this.Application.GetString("LogDataSourceOptionsDialog.Command"),
				Title = this.Application.GetString("LogDataSourceOptionsDialog.TeardownCommands"),
			}.ShowDialog(this))?.Trim();
			if (!string.IsNullOrWhiteSpace(command))
			{
				this.teardownCommands.Add(command);
				this.SelectListBoxItem(this.teardownCommandsListBox, this.teardownCommands.Count - 1);
			}
		}


		/// <summary>
		/// Get or set <see cref="ILogDataSourceProvider"/> which needs the <see cref="LogDataSourceOptions"/>.
		/// </summary>
		public ILogDataSourceProvider? DataSourceProvider
		{
			get => this.GetValue<ILogDataSourceProvider?>(DataSourceProviderProperty);
			set => this.SetValue<ILogDataSourceProvider?>(DataSourceProviderProperty, value);
		}


		// All encodings.
		IList<Encoding> Encodings { get; } = Encoding.GetEncodings().Let(it =>
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
			var listBox = (ListBox)item.Parent.AsNonNull();
			var isSetupCommand = (listBox == this.setupCommandsListBox);
			var index = isSetupCommand ? this.setupCommands.IndexOf((string)item.DataContext.AsNonNull()) : this.teardownCommands.IndexOf((string)item.DataContext.AsNonNull());
			if (index < 0)
				return;

			// edit
			var newCommand = (await new AppSuite.Controls.TextInputDialog()
			{
				InitialText = (item.DataContext as string),
				Message = this.Application.GetString("LogDataSourceOptionsDialog.Command"),
				Title = this.Application.GetString(isSetupCommand ? "LogDataSourceOptionsDialog.SetupCommands" : "LogDataSourceOptionsDialog.TeardownCommands"),
			}.ShowDialog(this))?.Trim();
			if (string.IsNullOrEmpty(newCommand))
				return;

			// update command
			if (isSetupCommand)
				this.setupCommands[index] = newCommand;
			else
				this.teardownCommands[index] = newCommand;
			this.SelectListBoxItem((ListBox)item.Parent.AsNonNull(), index);
		}


		// Generate valid result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var options = new LogDataSourceOptions();
			if (this.IsCategorySupported)
				options.Category = this.categoryTextBox.Text?.Trim();
			if (this.IsCommandSupported)
				options.Command = this.commandTextBox.Text?.Trim();
			if (this.IsEncodingSupported)
				options.Encoding = this.encodingComboBox.SelectedItem as Encoding;
			if (this.IsFileNameSupported)
				options.FileName = this.fileNameTextBox.Text?.Trim();
			if (this.IsQueryStringSupported)
				options.QueryString = this.queryStringTextBox.Text?.Trim();
			if (this.IsPasswordSupported)
				options.Password = this.passwordTextBox.Text?.Trim();
			if (this.IsSetupCommandsSupported)
				options.SetupCommands = this.setupCommands;
			if (this.IsTeardownCommandsSupported)
				options.TeardownCommands = this.teardownCommands;
			if (this.IsUriSupported)
				options.Uri = this.uriTextBox.Uri;
			if (this.IsUserNameSupported)
				options.UserName = this.userNameTextBox.Text?.Trim();
			if (this.IsWorkingDirectorySupported)
				options.WorkingDirectory = this.workingDirectoryTextBox.Text?.Trim();
			return Task.FromResult((object?)options);
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Data source options states.
		bool IsCategoryRequired { get => this.GetValue<bool>(IsCategoryRequiredProperty); }
		bool IsCategorySupported { get => this.GetValue<bool>(IsCategorySupportedProperty); }
		bool IsCommandRequired { get => this.GetValue<bool>(IsCommandRequiredProperty); }
		bool IsCommandSupported { get => this.GetValue<bool>(IsCommandSupportedProperty); }
		bool IsFileNameSupported { get => this.GetValue<bool>(IsFileNameSupportedProperty); }
		bool IsEncodingSupported { get => this.GetValue<bool>(IsEncodingSupportedProperty); }
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
		bool IsUriRequired { get => this.GetValue<bool>(IsUriRequiredProperty); }
		bool IsUriSupported { get => this.GetValue<bool>(IsUriSupportedProperty); }
		bool IsWorkingDirectorySupported { get => this.GetValue<bool>(IsWorkingDirectorySupportedProperty); }


		// Move given setup or teardown command down.
		void MoveSetupTeardownCommandDown(ListBoxItem item)
		{
			// find index of command
			var listBox = (ListBox)item.Parent.AsNonNull();
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


		// Move given setup or teardown command up.
		void MoveSetupTeardownCommandUp(ListBoxItem item)
		{
			// find index of command
			var listBox = (ListBox)item.Parent.AsNonNull();
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


		// Called when double-tapped on list box.
		void OnListBoxDoubleTapped(object? sender, RoutedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			var selectedItem = listBox.SelectedItem;
			if (selectedItem == null
				|| !listBox.TryFindListBoxItem(selectedItem, out var listBoxItem)
				|| listBoxItem == null
				|| !listBoxItem.IsPointerOver)
			{
				return;
			}
			if (listBox == this.setupCommandsListBox || listBox == this.teardownCommandsListBox)
				this.EditSetupTeardownCommand(listBoxItem);
		}


		// Called when list box lost focus.
		void OnListBoxLostFocus(object? sender, RoutedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			listBox.SelectedItems.Clear();
		}


		// Called when selection in list box changed.
		void OnListBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			if (sender is not ListBox listBox)
				return;
			if (listBox.SelectedIndex >= 0)
				listBox.ScrollIntoView(listBox.SelectedIndex);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			// put options to control, must keep same order as controls in window
			var options = this.Options;
			var firstEditor = (Control?)null;
			if (this.IsCategorySupported)
			{
				this.categoryTextBox.Text = options.Category;
				firstEditor = firstEditor ?? this.categoryTextBox;
			}
			if (this.IsCommandSupported)
			{
				this.commandTextBox.Text = options.Command;
				firstEditor = firstEditor ?? this.commandTextBox;
			}
			if (this.IsFileNameSupported)
			{
				this.fileNameTextBox.Text = options.FileName;
				firstEditor = firstEditor ?? this.fileNameTextBox;
			}
			if (this.IsWorkingDirectorySupported)
			{
				this.workingDirectoryTextBox.Text = options.WorkingDirectory;
				firstEditor = firstEditor ?? this.workingDirectoryTextBox;
			}
			if (this.IsUriSupported)
			{
				this.uriTextBox.Uri = options.Uri;
				firstEditor = firstEditor ?? this.uriTextBox;
			}
			if (this.IsEncodingSupported)
			{
				this.encodingComboBox.SelectedItem = options.Encoding ?? Encoding.UTF8;
				firstEditor = firstEditor ?? this.encodingComboBox;
			}
			if (this.IsQueryStringSupported)
			{
				this.queryStringTextBox.Text = options.QueryString;
				firstEditor = firstEditor ?? this.queryStringTextBox;
			}
			if (this.IsUserNameSupported)
			{
				this.userNameTextBox.Text = options.UserName;
				firstEditor = firstEditor ?? this.userNameTextBox;
			}
			if (this.IsPasswordSupported)
			{
				this.passwordTextBox.Text = options.Password;
				firstEditor = firstEditor ?? this.passwordTextBox;
			}
			if (this.IsSetupCommandsSupported)
			{
				this.setupCommands.AddRange(options.SetupCommands);
				firstEditor = firstEditor ?? this.setupCommandsListBox;
			}
			if (this.IsTeardownCommandsSupported)
			{
				this.teardownCommands.AddRange(options.TeardownCommands);
				firstEditor = firstEditor ?? this.teardownCommandsListBox;
			}

			// move focus to first editor
			if (firstEditor != null)
				firstEditor.Focus();
			else
				this.SynchronizationContext.Post(this.Close);

			// call base
			base.OnOpened(e);
		}


		// Called when property changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == DataSourceProviderProperty)
			{
				var provider = (change.NewValue.Value as ILogDataSourceProvider);
				if (provider != null)
				{
					this.SetValue<bool>(IsCategoryRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Category)));
					this.SetValue<bool>(IsCategorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Category)));
					this.SetValue<bool>(IsCommandRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Command)));
					this.SetValue<bool>(IsCommandSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Command)));
					this.SetValue<bool>(IsEncodingSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Encoding)));
					this.SetValue<bool>(IsFileNameSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.FileName)));
					this.SetValue<bool>(IsPasswordRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Password)));
					this.SetValue<bool>(IsPasswordSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Password)));
					this.SetValue<bool>(IsQueryStringRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.QueryString)));
					this.SetValue<bool>(IsQueryStringSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.QueryString)));
					this.SetValue<bool>(IsSetupCommandsRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.SetupCommands)));
					this.SetValue<bool>(IsSetupCommandsSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.SetupCommands)));
					this.SetValue<bool>(IsTeardownCommandsRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.TeardownCommands)));
					this.SetValue<bool>(IsTeardownCommandsSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.TeardownCommands)));
					this.SetValue<bool>(IsUriRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.Uri)));
					this.SetValue<bool>(IsUriSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.Uri)));
					this.SetValue<bool>(IsUserNameRequiredProperty, provider.IsSourceOptionRequired(nameof(LogDataSourceOptions.UserName)));
					this.SetValue<bool>(IsUserNameSupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.UserName)));
					this.SetValue<bool>(IsWorkingDirectorySupportedProperty, provider.IsSourceOptionSupported(nameof(LogDataSourceOptions.WorkingDirectory)));
				}
				else
				{
					this.SetValue<bool>(IsCategoryRequiredProperty, false);
					this.SetValue<bool>(IsCategorySupportedProperty, false);
					this.SetValue<bool>(IsCommandRequiredProperty, false);
					this.SetValue<bool>(IsCommandSupportedProperty, false);
					this.SetValue<bool>(IsEncodingSupportedProperty, false);
					this.SetValue<bool>(IsFileNameSupportedProperty, false);
					this.SetValue<bool>(IsPasswordRequiredProperty, false);
					this.SetValue<bool>(IsPasswordSupportedProperty, false);
					this.SetValue<bool>(IsQueryStringRequiredProperty, false);
					this.SetValue<bool>(IsQueryStringSupportedProperty, false);
					this.SetValue<bool>(IsSetupCommandsRequiredProperty, false);
					this.SetValue<bool>(IsSetupCommandsSupportedProperty, false);
					this.SetValue<bool>(IsTeardownCommandsRequiredProperty, false);
					this.SetValue<bool>(IsTeardownCommandsSupportedProperty, false);
					this.SetValue<bool>(IsUriRequiredProperty, false);
					this.SetValue<bool>(IsUriSupportedProperty, false);
					this.SetValue<bool>(IsUserNameRequiredProperty, false);
					this.SetValue<bool>(IsUserNameSupportedProperty, false);
					this.SetValue<bool>(IsWorkingDirectorySupportedProperty, false);
				}
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
			if (this.IsQueryStringRequired && string.IsNullOrWhiteSpace(this.queryStringTextBox.Text))
				return false;
			if (this.IsPasswordRequired && string.IsNullOrWhiteSpace(this.passwordTextBox.Text))
				return false;
			if (this.IsSetupCommandsRequired && this.setupCommands.IsEmpty())
				return false;
			if (this.IsTeardownCommandsRequired && this.teardownCommands.IsEmpty())
				return false;
			if (this.IsUriRequired && (this.uriTextBox.Uri == null || !this.uriTextBox.IsTextValid))
				return false;
			if (this.IsUserNameRequired && string.IsNullOrWhiteSpace(this.userNameTextBox.Text))
				return false;
			return true;
		}


		/// <summary>
		/// Get or set <see cref="LogDataSourceOptions"/> to be edited.
		/// </summary>
		public LogDataSourceOptions Options { get; set; }


		// Remove given setup or teardown command.
		void RemoveSetupTeardownCommand(ListBoxItem item)
		{
			// find index of command
			var listBox = (ListBox)item.Parent.AsNonNull();
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


		// Select file name.
		async void SelectFileName()
		{
			var fileNames = await new OpenFileDialog()
			{
				InitialFileName = this.fileNameTextBox.Text?.Trim()
			}.ShowAsync(this);
			if (fileNames == null || fileNames.IsEmpty())
				return;
			this.fileNameTextBox.Text = fileNames[0];
		}


		// Select given item in list box.
		void SelectListBoxItem(ListBox listBox, int index)
		{
			this.SynchronizationContext.Post(() =>
			{
				listBox.SelectedItems.Clear();
				if (index < 0 || index >= listBox.GetItemCount())
					return;
				listBox.Focus();
				listBox.SelectedIndex = index;
				listBox.ScrollIntoView(index);
			});
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
	}
}
