using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using CarinaStudio.Collections;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;
using System.Collections.Generic;

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
		public static readonly IValueConverter LogProfileNameConverter = new EnumConverter<LogProfileIcon>(App.Current);


		// Fields.
		readonly ComboBox iconComboBox;
		readonly TextBox nameTextBox;


		/// <summary>
		/// Initialize new <see cref="LogProfileEditorDialog"/> instance.
		/// </summary>
		public LogProfileEditorDialog()
		{
			InitializeComponent();
			this.iconComboBox = this.FindControl<ComboBox>("iconComboBox").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Get or set <see cref="LogProfile"/> to be edited.
		/// </summary>
		public LogProfile? LogProfile { get; set; }


		// All log profile icons.
		IList<LogProfileIcon> LogProfileIcons { get; } = (LogProfileIcon[])Enum.GetValues(typeof(LogProfileIcon));


		// Called when property of editor control has been changed.
		void OnEditorControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			var property = e.Property;
			if (property == TextBox.TextProperty)
				this.InvalidateInput();
		}


		// Generate result.
		protected override object? OnGenerateResult()
		{
			return null;
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			var profile = this.LogProfile;
			if (profile == null)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Create");
				this.iconComboBox.SelectedItem = LogProfileIcon.File;
			}
			else if (!profile.IsBuiltIn)
			{
				this.Title = this.Application.GetString("LogProfileEditorDialog.Title.Edit");
				this.nameTextBox.Text = profile.Name;
				this.iconComboBox.SelectedItem = profile.Icon;
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

			// check name
			if (string.IsNullOrEmpty(this.nameTextBox.Text))
				return false;

			// ok
			return true;
		}
	}
}
