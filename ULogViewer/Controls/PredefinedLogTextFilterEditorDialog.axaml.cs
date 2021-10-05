using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit or create <see cref="PredefinedLogTextFilter"/>.
	/// </summary>
	partial class PredefinedLogTextFilterEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
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


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
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
			return Task.FromResult((object?)filter);
		}


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


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
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Create"));
				this.regexTextBox.Regex = this.Regex;
				this.ignoreCaseSwitch.IsChecked = true;
			}
			else
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Edit"));
				this.nameTextBox.Text = filter.Name;
				this.regexTextBox.Regex = this.Regex ?? filter.Regex;
				this.ignoreCaseSwitch.IsChecked = ((this.regexTextBox.Regex?.Options ?? RegexOptions.None) & RegexOptions.IgnoreCase) != 0;
			}
			this.SynchronizationContext.Post(_ => this.nameTextBox.Focus(), null); // [Workaround] delay to prevent focus got by popup
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
