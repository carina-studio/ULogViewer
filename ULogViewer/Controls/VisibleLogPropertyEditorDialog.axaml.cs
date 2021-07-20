using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.Logs;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit visible <see cref="LogProperty"/>.
	/// </summary>
	partial class VisibleLogPropertyEditorDialog : BaseDialog
	{
		// Fields.
		readonly ComboBox displayNameComboBox;
		readonly ComboBox nameComboBox;
		readonly ToggleSwitch specifyWidthSwitch;
		readonly NumericUpDown widthUpDown;


		/// <summary>
		/// Initialize new <see cref="VisibleLogPropertyEditorDialog"/> instance.
		/// </summary>
		public VisibleLogPropertyEditorDialog()
		{
			InitializeComponent();
			this.displayNameComboBox = this.FindControl<ComboBox>("displayNameComboBox").AsNonNull();
			this.nameComboBox = this.FindControl<ComboBox>("nameComboBox").AsNonNull();
			this.specifyWidthSwitch = this.FindControl<ToggleSwitch>("specifyWidthSwitch").AsNonNull();
			this.widthUpDown = this.FindControl<NumericUpDown>("widthUpDown").AsNonNull();
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set <see cref="LogProperty"/> to be edited.
		/// </summary>
		public LogProperty? LogProperty { get; set; }


		// Generate result.
		protected override object? OnGenerateResult()
		{
			var width = this.specifyWidthSwitch.IsChecked.GetValueOrDefault() ? null : (int?)this.widthUpDown.Value;
			return new LogProperty((string)this.nameComboBox.SelectedItem.AsNonNull(), (string)this.displayNameComboBox.SelectedItem.AsNonNull(), width);
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
				this.widthUpDown.Value = 100;
			}
			else
			{
				this.nameComboBox.SelectedItem = property.Name;
				this.displayNameComboBox.SelectedItem = property.DisplayName;
				property.Width.Let(it =>
				{
					this.specifyWidthSwitch.IsChecked = it.HasValue;
					this.widthUpDown.Value = it ?? 100;
				});
			}
			this.nameComboBox.Focus();
			base.OnOpened(e);
		}
	}
}
