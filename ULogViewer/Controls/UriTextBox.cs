using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using CarinaStudio.Threading;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// <see cref="TextBox"/> which treat input text as <see cref="Uri"/>.
	/// </summary>
	class UriTextBox : TextBox, IStyleable
	{
		/// <summary>
		/// Property of <see cref="IsTextValid"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsTextValidProperty = AvaloniaProperty.Register<UriTextBox, bool>(nameof(IsTextValid), true);
		/// <summary>
		/// Property of <see cref="Uri"/>.
		/// </summary>
		public static readonly AvaloniaProperty<Uri?> UriProperty = AvaloniaProperty.Register<UriTextBox, Uri?>(nameof(Uri), null);
		/// <summary>
		/// Property of <see cref="UriKind"/>.
		/// </summary>
		public static readonly AvaloniaProperty<UriKind> UriKindProperty = AvaloniaProperty.Register<UriTextBox, UriKind>(nameof(IsTextValid), UriKind.Absolute);
		/// <summary>
		/// Property of <see cref="ValidationDelay"/>.
		/// </summary>
		public static readonly AvaloniaProperty<int> ValidationDelayProperty = AvaloniaProperty.Register<UriTextBox, int>(nameof(ValidationDelay), 500, coerce: (_, it) => Math.Max(0, it));


		// Fields.
		readonly IObservable<object?> invalidTextBrush;
		IDisposable? invalidTextBrushBinding;
		readonly ScheduledAction validateAction;


		/// <summary>
		/// Initialize new <see cref="UriTextBox"/> instance.
		/// </summary>
		public UriTextBox()
		{
			this.invalidTextBrush = this.GetResourceObservable("Brush.TextBox.Foreground.Error");
			this.validateAction = new ScheduledAction(() => this.Validate());
		}


		/// <summary>
		/// Get whether input <see cref="TextBox.Text"/> represent a valid <see cref="Uri"/> or not.
		/// </summary>
		public bool IsTextValid { get => this.GetValue<bool>(IsTextValidProperty); }


		// Called when propery changed.
		protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
		{
			base.OnPropertyChanged(change);
			var property = change.Property;
			if (property == IsTextValidProperty)
			{
				if (this.IsTextValid)
					this.invalidTextBrushBinding = this.invalidTextBrushBinding.DisposeAndReturnNull();
				else
					this.invalidTextBrushBinding = this.Bind(ForegroundProperty, this.invalidTextBrush);
			}
			else if (property == TextProperty)
			{
				if (string.IsNullOrEmpty(this.Text))
					this.validateAction.Reschedule();
				else
					this.validateAction.Reschedule(this.ValidationDelay);
			}
			else if (property == UriProperty)
			{
				var uri = (change.NewValue.Value as Uri);
				if (uri != null)
				{
					var text = this.Text?.Trim() ?? "";
					if (text.Length == 0 || !Uri.TryCreate(text, this.UriKind, out var currentUri) || uri != currentUri)
						this.Text = uri.ToString();
				}
				else if (this.Text != null)
					this.Text = "";
			}
			else if (property == UriKindProperty)
				this.validateAction.Execute();
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
		/// Get or set <see cref="Uri"/>.
		/// </summary>
		public Uri? Uri
		{
			get => this.GetValue<Uri?>(UriProperty);
			set => this.SetValue<Uri?>(UriProperty, value);
		}


		/// <summary>
		/// Get or set target <see cref="UriKind"/>.
		/// </summary>
		public UriKind UriKind
		{
			get => this.GetValue<UriKind>(UriKindProperty);
			set => this.SetValue<UriKind>(UriKindProperty, value);
		}


		/// <summary>
		/// Validate input <see cref="TextBox.Text"/> and generate corresponding <see cref="Uri"/>.
		/// </summary>
		/// <returns>True if input <see cref="TextBox.Text"/> generates a valid <see cref="Uri"/>.</returns>
		public bool Validate()
		{
			// check state
			this.VerifyAccess();

			// cancel scheduled validation
			this.validateAction.Cancel();

			// trim spaces
			var text = this.Text ?? "";
			var trimmedText = text.Trim();
			if (text != trimmedText)
			{
				text = trimmedText;
				this.Text = trimmedText;
				this.validateAction.Cancel();
			}

			// clear URI
			if (text.Length == 0)
			{
				this.SetValue<Uri?>(UriProperty, null);
				this.SetValue<bool>(IsTextValidProperty, true);
				return true;
			}

			// try build URI
			if(!Uri.TryCreate(text, this.UriKind, out var uri))
			{
				this.SetValue<bool>(IsTextValidProperty, false);
				return false;
			}

			// complete
			this.SetValue<Uri?>(UriProperty, uri);
			this.SetValue<bool>(IsTextValidProperty, true);
			return true;
		}


		/// <summary>
		/// Get or set the delay of validating <see cref="Uri"/> after user typing in milliseconds.
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
