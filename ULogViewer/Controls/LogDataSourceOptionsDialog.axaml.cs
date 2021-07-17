using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.Logs.DataSources;
using System;
using System.Collections.ObjectModel;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="LogDataSourceOptions"/>.
	/// </summary>
	partial class LogDataSourceOptionsDialog : BaseDialog
	{
		/// <summary>
		/// Property of <see cref="UnderlyingLogDataSource"/>.
		/// </summary>
		public static readonly AvaloniaProperty<UnderlyingLogDataSource> UnderlyingLogDataSourceProperty = AvaloniaProperty.Register<LogDataSourceOptionsDialog, UnderlyingLogDataSource>(nameof(UnderlyingLogDataSource), UnderlyingLogDataSource.File);


		// Fields.
		readonly TextBox commandTextBox;
		readonly ObservableCollection<string> setupCommands = new ObservableCollection<string>();
		readonly ListBox setupCommandsListBox;
		readonly ObservableCollection<string> teardownCommands = new ObservableCollection<string>();
		readonly ListBox teardownCommandsListBox;
		readonly TextBox workingDirectoryTextBox;


		/// <summary>
		/// Initialize new <see cref="LogDataSourceOptionsDialog"/>.
		/// </summary>
		public LogDataSourceOptionsDialog()
		{
			InitializeComponent();
			this.commandTextBox = this.FindControl<TextBox>("commandTextBox").AsNonNull();
			this.setupCommandsListBox = this.FindControl<ListBox>("setupCommandsListBox").AsNonNull();
			this.teardownCommandsListBox = this.FindControl<ListBox>("teardownCommandsListBox").AsNonNull();
			this.workingDirectoryTextBox = this.FindControl<TextBox>("workingDirectoryTextBox").AsNonNull();
		}


		// Add setup command.
		async void AddSetupCommand()
		{ }


		// Add teardown command.
		async void AddTeardownCommand()
		{ }


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Called when property of editor control changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			var property = e.Property;
			if (property == TextBox.TextProperty)
				this.InvalidateInput();
		}


		// Generate valid result.
		protected override object? OnGenerateResult()
		{
			var options = new LogDataSourceOptions();
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.File:
					break;
				case UnderlyingLogDataSource.StandardOutput:
					options.Command = this.commandTextBox.Text.AsNonNull().Trim();
					options.SetupCommands = this.setupCommands;
					options.TeardownCommands = this.teardownCommands;
					options.WorkingDirectory = this.workingDirectoryTextBox.Text?.Trim();
					break;
			}
			return options;
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var options = this.Options;
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.File:
					break;
				case UnderlyingLogDataSource.StandardOutput:
					this.commandTextBox.Text = options.Command;
					foreach (var command in options.SetupCommands)
						this.setupCommands.Add(command);
					foreach (var command in options.TeardownCommands)
						this.teardownCommands.Add(command);
					this.workingDirectoryTextBox.Text = options.WorkingDirectory;
					this.commandTextBox.Focus();
					break;
			}
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			if (!base.OnValidateInput())
				return false;
			switch (this.UnderlyingLogDataSource)
			{
				case UnderlyingLogDataSource.File:
					return false;
				case UnderlyingLogDataSource.StandardOutput:
					return !string.IsNullOrEmpty(this.commandTextBox.Text?.Trim());
				default:
					return false;
			}
		}


		/// <summary>
		/// Get or set <see cref="LogDataSourceOptions"/> to be edited.
		/// </summary>
		public LogDataSourceOptions Options { get; set; }


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
