using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit or create <see cref="PredefinedLogTextFilter"/>.
	/// </summary>
	partial class PredefinedLogTextFilterEditorDialog : BaseDialog
	{
		// Fields.
		readonly ToggleSwitch ignoreCaseSwitch;
		readonly TextBox nameTextBox;
		readonly RegexTextBox regexTextBox;


		// Constructor.
		public PredefinedLogTextFilterEditorDialog()
		{
			InitializeComponent();
			this.ignoreCaseSwitch = this.FindControl<ToggleSwitch>("ignoreCaseSwitch").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.regexTextBox = this.FindControl<RegexTextBox>("regexTextBox").AsNonNull();
		}


		/// <summary>
		/// Get or set <see cref="PredefinedLogTextFilter"/> to be edited.
		/// </summary>
		public PredefinedLogTextFilter? Filter { get; set; }


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// Generate result.
		protected override object? OnGenerateResult()
		{
			var name = this.nameTextBox.Text;
			var regex = this.regexTextBox.Regex.AsNonNull();
			var filter = this.Filter;
			if (filter != null)
			{
				filter.Name = name;
				filter.Regex = regex;
			}
			else
				filter = new PredefinedLogTextFilter(this.Application, name, regex);
			return filter;
		}


		// Called when pointer released on link text block.
		void OnLinkDescriptionPointerReleased(object? sender, PointerReleasedEventArgs e)
		{
			if (e.InitialPressMouseButton != MouseButton.Left)
				return;
			if ((sender as Control)?.Tag is string uri)
				this.OpenLink(uri);
		}


		// Called when property of name text box changed.
		void OnNameTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == TextBox.TextProperty)
				this.InvalidateInput();
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var filter = this.Filter;
			if (filter == null)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String.PredefinedLogTextFilterEditorDialog.Title.Create"));
				this.regexTextBox.Regex = this.Regex;
				this.ignoreCaseSwitch.IsChecked = true;
			}
			else
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String.PredefinedLogTextFilterEditorDialog.Title.Edit"));
				this.nameTextBox.Text = filter.Name;
				this.regexTextBox.Regex = this.Regex ?? filter.Regex;
				this.ignoreCaseSwitch.IsChecked = ((this.regexTextBox.Regex?.Options ?? RegexOptions.None) & RegexOptions.IgnoreCase) != 0;
			}
			this.nameTextBox.Focus();
		}


		// Called when property of regex text box changed.
		void OnRegexTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.IsTextValidProperty || e.Property == RegexTextBox.RegexProperty)
				this.InvalidateInput();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			// call base
			if (!base.OnValidateInput())
				return false;

			// check name
			var name = this.nameTextBox.Text?.Trim() ?? "";
			if (name.Length == 0)
				return false;

			// check regex
			if (!this.regexTextBox.IsTextValid || this.regexTextBox.Regex == null)
				return false;

			// ok
			return true;
		}


		/// <summary>
		/// Get or set <see cref="Regex"/> of text filter.
		/// </summary>
		public Regex? Regex { get; set; }
	}
}
