using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
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
		/// <summary>
		/// Property of <see cref="IsValidFilterParams"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsValidFilterParamsProperty = AvaloniaProperty.Register<PredefinedLogTextFilterEditorDialog, bool>(nameof(IsValidFilterParams));


		// Fields.
		readonly ToggleSwitch ignoreCaseSwitch;
		readonly TextBox nameTextBox;
		readonly RegexTextBox regexTextBox;
		readonly ScheduledAction validateFilterParamsAction;


		// Constructor.
		public PredefinedLogTextFilterEditorDialog()
		{
			InitializeComponent();
			this.ignoreCaseSwitch = this.FindControl<ToggleSwitch>("ignoreCaseSwitch").AsNonNull();
			this.nameTextBox = this.FindControl<TextBox>("nameTextBox").AsNonNull();
			this.regexTextBox = this.FindControl<RegexTextBox>("regexTextBox").AsNonNull();
			this.validateFilterParamsAction = new ScheduledAction(this.ValidateFilterParams);
		}


		/// <summary>
		/// Get or set <see cref="PredefinedLogTextFilter"/> to be edited.
		/// </summary>
		public PredefinedLogTextFilter? Filter { get; set; }


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Check whether parameters of filter is valid or not.
		/// </summary>
		public bool IsValidFilterParams { get => this.GetValue<bool>(IsValidFilterParamsProperty); }


		// Called when property of name text box changed.
		void OnNameTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == TextBox.TextProperty)
				this.validateFilterParamsAction?.Schedule();
		}


		// Called when OK clicked.
		void OnOKClick(object? sender, RoutedEventArgs e)
		{
			if (!this.IsValidFilterParams)
				return;
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
			this.Close(filter);
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
			}
			else
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String.PredefinedLogTextFilterEditorDialog.Title.Edit"));
				this.nameTextBox.Text = filter.Name;
				this.regexTextBox.Regex = this.Regex ?? filter.Regex;
				this.ignoreCaseSwitch.IsChecked = ((this.regexTextBox.Regex?.Options ?? RegexOptions.None) & RegexOptions.IgnoreCase) != 0;
			}
			this.nameTextBox.Focus();
			this.validateFilterParamsAction.Schedule();
		}


		// Called when property of regex text box changed.
		void OnRegexTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.IsTextValidProperty || e.Property == RegexTextBox.RegexProperty)
				this.validateFilterParamsAction?.Schedule();
		}


		/// <summary>
		/// Get or set <see cref="Regex"/> of text filter.
		/// </summary>
		public Regex? Regex { get; set; }


		// Validate filter parameters.
		void ValidateFilterParams()
		{
			// check name
			var name = this.nameTextBox.Text?.Trim() ?? "";
			if (name.Length == 0)
			{
				this.SetValue<bool>(IsValidFilterParamsProperty, false);
				return;
			}

			// check regex
			if (!this.regexTextBox.IsTextValid || this.regexTextBox.Regex == null)
			{
				this.SetValue<bool>(IsValidFilterParamsProperty, false);
				return;
			}

			// ok
			this.SetValue<bool>(IsValidFilterParamsProperty, true);
		}
	}
}
