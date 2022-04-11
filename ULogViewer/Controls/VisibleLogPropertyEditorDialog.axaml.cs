using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit visible <see cref="LogProperty"/>.
	/// </summary>
	partial class VisibleLogPropertyEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ToggleSwitch customDisplayNameSwitch;
		readonly TextBox customDisplayNameTextBox;
		readonly ComboBox displayNameComboBox;
		readonly ComboBox nameComboBox;
		readonly ToggleSwitch specifyWidthSwitch;
		readonly IntegerTextBox widthTextBox;


		/// <summary>
		/// Initialize new <see cref="VisibleLogPropertyEditorDialog"/> instance.
		/// </summary>
		public VisibleLogPropertyEditorDialog()
		{
			InitializeComponent();
			this.customDisplayNameSwitch = this.FindControl<ToggleSwitch>("customDisplayNameSwitch").AsNonNull();
			this.customDisplayNameTextBox = this.FindControl<TextBox>("customDisplayNameTextBox").AsNonNull();
			this.displayNameComboBox = this.FindControl<ComboBox>("displayNameComboBox").AsNonNull();
			this.nameComboBox = this.FindControl<ComboBox>("nameComboBox").AsNonNull();
			this.specifyWidthSwitch = this.FindControl<ToggleSwitch>("specifyWidthSwitch").AsNonNull();
			this.widthTextBox = this.FindControl<IntegerTextBox>("widthTextBox").AsNonNull();
		}


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var displayName = this.customDisplayNameSwitch.IsChecked.GetValueOrDefault()
				 ? this.customDisplayNameTextBox.Text.AsNonNull().Trim()
				 : (string)this.displayNameComboBox.SelectedItem.AsNonNull();
			var width = this.specifyWidthSwitch.IsChecked.GetValueOrDefault() ? (int?)this.widthTextBox.Value : null;
			return Task.FromResult((object?)new LogProperty((string)this.nameComboBox.SelectedItem.AsNonNull(), displayName, width));
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set <see cref="LogProperty"/> to be edited.
		/// </summary>
		public LogProperty? LogProperty { get; set; }


		// Called when property of editor control changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			var property = e.Property;
			if (property == ToggleSwitch.IsCheckedProperty
				|| property == TextBox.TextProperty)
			{
				this.InvalidateInput();
			}
		}


		// Called when selection of name combo box changed.
		void OnNameComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
		{
			this.displayNameComboBox.SelectedItem = this.nameComboBox.SelectedItem;
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var property = this.LogProperty;
			if (property == null)
			{
				this.nameComboBox.SelectedItem = nameof(Log.Message);
				this.specifyWidthSwitch.IsChecked = false;
				this.widthTextBox.Value = 100;
			}
			else
			{
				this.nameComboBox.SelectedItem = property.Name;
				if (DisplayableLogProperty.DisplayNames.Contains(property.DisplayName))
					this.displayNameComboBox.SelectedItem = property.DisplayName;
				else
				{
					this.customDisplayNameSwitch.IsChecked = true;
					this.customDisplayNameTextBox.Text = property.DisplayName;
				}
				property.Width.Let(it =>
				{
					this.specifyWidthSwitch.IsChecked = it.HasValue;
					this.widthTextBox.Value = it ?? 100;
				});
			}
			this.nameComboBox.Focus();
			base.OnOpened(e);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			if (!base.OnValidateInput())
				return false;
			if (this.customDisplayNameSwitch.IsChecked.GetValueOrDefault())
				return !string.IsNullOrWhiteSpace(this.customDisplayNameTextBox.Text);
			return true;
		}
	}
}
