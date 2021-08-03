using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using CarinaStudio.Threading;
using System;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// <see cref="TextBox"/> which accept regular expression.
	/// </summary>
	class RegexTextBox : TextBox, IStyleable
	{
		/// <summary>
		/// Property of <see cref="IgnoreCase"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IgnoreCaseProperty = AvaloniaProperty.Register<RegexTextBox, bool>(nameof(IgnoreCase), true);
		/// <summary>
		/// Property of <see cref="IsTextValid"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsTextValidProperty = AvaloniaProperty.Register<RegexTextBox, bool>(nameof(IsTextValid), true);
		/// <summary>
		/// Property of <see cref="Regex"/>.
		/// </summary>
		public static readonly AvaloniaProperty<Regex?> RegexProperty = AvaloniaProperty.Register<RegexTextBox, Regex?>(nameof(Regex));
		/// <summary>
		/// Property of <see cref="ValidationDelay"/>.
		/// </summary>
		public static readonly AvaloniaProperty<int> ValidationDelayProperty = AvaloniaProperty.Register<RegexTextBox, int>(nameof(ValidationDelay), 500, coerce: (_, it) => Math.Max(0, it));


		// Fields.
		readonly IObservable<object?> invalidTextBrush;
		IDisposable? invalidTextBrushBinding;
		readonly ScheduledAction validateAction;


		/// <summary>
		/// Initialize new <see cref="RegexTextBox"/> instance.
		/// </summary>
		public RegexTextBox()
		{
			this.invalidTextBrush = this.GetResourceObservable("Brush.TextBox.Foreground.Error");
			this.validateAction = new ScheduledAction(() => this.Validate());
		}


		/// <summary>
		/// Get or set whether case in <see cref="Regex"/> can be ignored or not.
		/// </summary>
		public bool IgnoreCase
		{
			get => this.GetValue<bool>(IgnoreCaseProperty);
			set => this.SetValue<bool>(IgnoreCaseProperty, value);
		}


		/// <summary>
		/// Get whether input <see cref="TextBox.Text"/> represent a valid <see cref="Regex"/> or not.
		/// </summary>
		public bool IsTextValid { get => this.GetValue<bool>(IsTextValidProperty); }


		// Called when propery changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			var property = change.Property;
			if (property == IgnoreCaseProperty)
				this.validateAction.Execute();
			else if (property == IsTextValidProperty)
			{
				if (this.IsTextValid)
					this.invalidTextBrushBinding = this.invalidTextBrushBinding.DisposeAndReturnNull();
				else
					this.invalidTextBrushBinding = this.Bind(ForegroundProperty, this.invalidTextBrush);
			}
			else if (property == RegexProperty)
			{
				var regex = (change.NewValue.Value as Regex);
				if (regex != null)
				{
					if (regex.ToString() != this.Text)
						this.Text = regex.ToString();
				}
				else if (this.Text.Length > 0)
					this.Text = "";
			}
			else if (property == TextProperty)
			{
				if (string.IsNullOrEmpty(this.Text))
					this.validateAction.Reschedule();
				else
					this.validateAction.Reschedule(this.ValidationDelay);
			}
			else if (property == ValidationDelayProperty)
			{
				if (this.validateAction.IsScheduled)
					this.validateAction.Reschedule(this.ValidationDelay);
			}
		}


		// Called when input text.
		protected override void OnTextInput(TextInputEventArgs e)
		{
			if (string.IsNullOrEmpty(this.Text))
			{
				var text = e.Text;
				if (text != null && text.Length > 0 && char.IsWhiteSpace(text[0]))
					e.Handled = true;
			}
			base.OnTextInput(e);
		}


		/// <summary>
		/// Get or set <see cref="Regex"/>.
		/// </summary>
		public Regex? Regex
		{
			get => this.GetValue<Regex?>(RegexProperty);
			set => this.SetValue<Regex?>(RegexProperty, value);
		}


		/// <summary>
		/// Validate input <see cref="TextBox.Text"/> and generate corresponding <see cref="Regex"/>.
		/// </summary>
		/// <returns>True if input <see cref="TextBox.Text"/> generates a valid <see cref="Regex"/>.</returns>
		public bool Validate()
		{
			// check state
			this.VerifyAccess();

			// cancel scheduled validation
			this.validateAction.Cancel();

			// trim leading spaces
			var text = this.Text ?? "";
			if (text.Length > 0 && char.IsWhiteSpace(text[0]))
			{
				text = text.TrimStart();
				this.Text = text;
				this.validateAction.Cancel();
			}

			// no regex needed
			if (text.Length == 0)
			{
				this.Regex = null;
				this.SetValue<bool>(IsTextValidProperty, true);
				return true;
			}

			// check current regex
			var options = (this.IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
			if (this.Regex != null && this.Regex.ToString() == text && this.Regex.Options == options)
			{
				this.SetValue<bool>(IsTextValidProperty, true);
				return true;
			}

			// create regex
			try
			{
				this.Regex = new Regex(text, options);
				this.SetValue<bool>(IsTextValidProperty, true);
				return true;
			}
			catch
			{
				this.SetValue<bool>(IsTextValidProperty, false);
				return false;
			}
		}


		/// <summary>
		/// Get or set the delay of validating <see cref="Regex"/> after user typing in milliseconds.
		/// </summary>
		public int ValidationDelay
		{
			get => this.GetValue<int>(ValidationDelayProperty);
			set => this.SetValue<int>(ValidationDelayProperty, value);
		}


		// Interface implementations.
		Type IStyleable.StyleKey => typeof(TextBox);
	}
}
