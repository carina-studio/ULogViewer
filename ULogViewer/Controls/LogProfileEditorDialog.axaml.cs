using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="LogProfile"/>.
	/// </summary>
	partial class LogProfileEditorDialog : BaseDialog
	{
		/// <summary>
		/// <see cref="IValueConverter"/> to convert <see cref="LogProfileIcon"/> to display name.
		/// </summary>
		public static readonly IValueConverter LogProfileIconNameConverter = new EnumConverter<LogProfileIcon>(App.Current);


		// Static fields.
		static readonly AvaloniaProperty<bool> IsValidDataSourceOptionsProperty = AvaloniaProperty.Register<LogProfileEditorDialog, bool>(nameof(IsValidDataSourceOptions), true);
		static readonly AvaloniaProperty<UnderlyingLogDataSource> UnderlyingDataSourceProperty = AvaloniaProperty.Register<LogProfileEditorDialog, UnderlyingLogDataSource>(nameof(UnderlyingDataSource), UnderlyingLogDataSource.Undefined);


		// Fields.
		LogDataSourceOptions dataSourceOptions;
		readonly ComboBox dataSourceProviderComboBox;
		readonly ComboBox iconComboBox;
		readonly ObservableCollection<LogPattern> logPatterns = new ObservableCollection<LogPattern>();
		readonly TextBox nameTextBox;
		readonly ComboBox sortDirectionComboBox;
		readonly ComboBox sortKeyComboBox;
		readonly TextBox timestampFormatForReadingTextBox;
		readonly TextBox timestampFormatForDisplayingTextBox;
		readonly ToggleSwitch workingDirNeededSwitch;


		/// <summary>
		/// Initialize new <see cref="LogProfileEditorDialog"/> instance.
		/// </summary>
		public LogProfileEditorDialog()
		{
			this.LogPatterns = this.logPatterns.Also(it =>
			{
				it.CollectionChanged += (_, e) => this.InvalidateInput();
			}).AsReadOnly();
			InitializeComponent();
			this.dataSourceProviderComboBox = this.FindControl<ComboBox>("dataSourceProviderComboBox").AsNonNull();
			this.iconComboBox = this.FindControl<ComboBox>("iconComboBox").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.sortDirectionComboBox = this.FindControl<ComboBox>("sortDirectionComboBox").AsNonNull();
			this.sortKeyComboBox = this.FindControl<ComboBox>("sortKeyComboBox").AsNonNull();
			this.timestampFormatForReadingTextBox = this.FindControl<TextBox>("timestampFormatForReadingTextBox").AsNonNull();
			this.timestampFormatForDisplayingTextBox = this.FindControl<TextBox>("timestampFormatForDisplayingTextBox").AsNonNull();
			this.workingDirNeededSwitch = this.FindControl<ToggleSwitch>("workingDirNeededSwitch").AsNonNull();
		}


		// Add log pattern.
		async void AddLogPattern()
		{
			var logPattern = await new LogPatternEditorDialog().ShowDialog<LogPattern>(this);
			if (logPattern != null)
				this.logPatterns.Add(logPattern);
		}


		// Edit log pattern.
		async void EditLogPattern(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			if (item.DataContext is not LogPattern logPattern)
				return;
			var newLlogPattern = await new LogPatternEditorDialog()
			{
				LogPattern = logPattern
			}.ShowDialog<LogPattern>(this);
			if (newLlogPattern != null && newLlogPattern != logPattern)
				this.logPatterns[index] = newLlogPattern;
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Check whether log data source options is valid or not.
		bool IsValidDataSourceOptions { get => this.GetValue<bool>(IsValidDataSourceOptionsProperty); }


		// Log patterns.
		IList<LogPattern> LogPatterns { get; }


		/// <summary>
		/// Get or set <see cref="LogProfile"/> to be edited.
		/// </summary>
		public LogProfile? LogProfile { get; set; }


		// All log profile icons.
		IList<LogProfileIcon> LogProfileIcons { get; } = (LogProfileIcon[])Enum.GetValues(typeof(LogProfileIcon));


		// Move log pattern down.
		void MoveLogPatternDown(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			if (index < this.logPatterns.Count - 1)
				this.logPatterns.Move(index, index + 1);
		}


		// Move log pattern up.
		void MoveLogPatternUp(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index <= 0)
				return;
			this.logPatterns.Move(index, index - 1);
		}


		// Called when selection of data source provider changed.
		void OnDataSourceProviderComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			this.SetValue<UnderlyingLogDataSource>(UnderlyingDataSourceProperty, dataSourceProvider?.UnderlyingSource ?? UnderlyingLogDataSource.Undefined);
			this.InvalidateInput();
		}


		// Called when property of editor control has been changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			var property = e.Property;
			if (property == TextBox.TextProperty)
			{
				this.InvalidateInput();
			}
		}


		// Generate result.
		protected override object? OnGenerateResult()
		{
			return null;
		}


		// Called when pointer released on description text.
		void OnLinkDescriptionPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (sender is Control control && control.Tag is string uri)
				this.OpenLink(uri);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Create");
				this.dataSourceProviderComboBox.SelectedItem = LogDataSourceProviders.All.FirstOrDefault(it => it is FileLogDataSourceProvider);
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
				this.sortDirectionComboBox.SelectedItem = SortDirection.Ascending;
				this.sortKeyComboBox.SelectedItem = LogSortKey.Timestamp;
			}
			else if (!profile.IsBuiltIn)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Edit");
				this.dataSourceOptions = profile.DataSourceOptions;
				this.dataSourceProviderComboBox.SelectedItem = profile.DataSourceProvider;
				this.iconComboBox.SelectedItem = profile.Icon;
				foreach (var logPattern in profile.LogPatterns)
					this.logPatterns.Add(logPattern);
				this.nameTextBox.Text = profile.Name;
				this.sortDirectionComboBox.SelectedItem = profile.SortDirection;
				this.sortKeyComboBox.SelectedItem = profile.SortKey;
				this.timestampFormatForDisplayingTextBox.Text = profile.TimestampFormatForDisplaying;
				this.timestampFormatForReadingTextBox.Text = profile.TimestampFormatForReading;
				this.workingDirNeededSwitch.IsChecked = profile.IsWorkingDirectoryNeeded;
			}
			else
				this.SynchronizationContext.Post(this.Close);
			this.nameTextBox.Focus();
			base.OnOpened(e);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			// call base
			if (!base.OnValidateInput())
				return false;

			// check data source and options
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			if (dataSourceProvider == null)
				return false;
			if (dataSourceProvider.UnderlyingSource == UnderlyingLogDataSource.StandardOutput && string.IsNullOrWhiteSpace(this.dataSourceOptions.Command))
			{
				this.SetValue<bool>(IsValidDataSourceOptionsProperty, false);
				return false;
			}
			this.SetValue<bool>(IsValidDataSourceOptionsProperty, true);

			// check name
			if (string.IsNullOrEmpty(this.nameTextBox.Text))
				return false;

			// check log patterns
			if (this.logPatterns.IsEmpty())
				return false;

			// ok
			return true;
		}


		// Remove log pattern.
		void RemoveLogPattern(ListBoxItem item)
		{
			var index = (item.GetVisualParent() as Panel)?.Children?.IndexOf(item) ?? -1;
			if (index < 0)
				return;
			this.logPatterns.RemoveAt(index);
		}


		// Set log data source options.
		async void SetDataSourceOptions()
		{
			var dataSourceProvider = (this.dataSourceProviderComboBox.SelectedItem as ILogDataSourceProvider);
			if (dataSourceProvider == null)
				return;
			var options = await new LogDataSourceOptionsDialog()
			{
				Options = this.dataSourceOptions,
				UnderlyingLogDataSource = dataSourceProvider.UnderlyingSource,
			}.ShowDialog<LogDataSourceOptions?>(this);
			if (options != null)
			{
				this.dataSourceOptions = options.Value;
				this.InvalidateInput();
			}
		}


		// Get underlying type of log data source.
		UnderlyingLogDataSource UnderlyingDataSource { get => this.GetValue<UnderlyingLogDataSource>(UnderlyingDataSourceProperty); }
	}
}
