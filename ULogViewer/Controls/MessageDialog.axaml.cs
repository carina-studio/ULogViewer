using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.ULogViewer.Converters;
using ReactiveUI;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Message dialog.
	/// </summary>
	partial class MessageDialog : BaseDialog
	{
		/// <summary>
		/// Property of <see cref="Button1Result"/>.
		/// </summary>
		public static readonly AvaloniaProperty<MessageDialogResult?> Button1ResultProperty = AvaloniaProperty.Register<MessageDialog, MessageDialogResult?>(nameof(Button1Result));
		/// <summary>
		/// Property of <see cref="Button1Text"/>.
		/// </summary>
		public static readonly AvaloniaProperty<string?> Button1TextProperty = AvaloniaProperty.Register<MessageDialog, string?>(nameof(Button1Text));
		/// <summary>
		/// Property of <see cref="Button2Result"/>.
		/// </summary>
		public static readonly AvaloniaProperty<MessageDialogResult?> Button2ResultProperty = AvaloniaProperty.Register<MessageDialog, MessageDialogResult?>(nameof(Button2Result));
		/// <summary>
		/// Property of <see cref="Button2Text"/>.
		/// </summary>
		public static readonly AvaloniaProperty<string?> Button2TextProperty = AvaloniaProperty.Register<MessageDialog, string?>(nameof(Button2Text));
		/// <summary>
		/// Property of <see cref="Button3Result"/>.
		/// </summary>
		public static readonly AvaloniaProperty<MessageDialogResult?> Button3ResultProperty = AvaloniaProperty.Register<MessageDialog, MessageDialogResult?>(nameof(Button3Result));
		/// <summary>
		/// Property of <see cref="Button3Text"/>.
		/// </summary>
		public static readonly AvaloniaProperty<string?> Button3TextProperty = AvaloniaProperty.Register<MessageDialog, string?>(nameof(Button3Text));
		/// <summary>
		/// Property of <see cref="IconDrawing"/>.
		/// </summary>
		public static readonly AvaloniaProperty<Drawing?> IconDrawingProperty = AvaloniaProperty.Register<MessageDialog, Drawing?>(nameof(IconDrawing));
		/// <summary>
		/// Property of <see cref="IsButton1Visible"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsButton1VisibleProperty = AvaloniaProperty.Register<MessageDialog, bool>(nameof(IsButton1Visible));
		/// <summary>
		/// Property of <see cref="IsButton2Visible"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsButton2VisibleProperty = AvaloniaProperty.Register<MessageDialog, bool>(nameof(IsButton2Visible));
		/// <summary>
		/// Property of <see cref="IsButton3Visible"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsButton3VisibleProperty = AvaloniaProperty.Register<MessageDialog, bool>(nameof(IsButton3Visible));
		/// <summary>
		/// Property of <see cref="Message"/>.
		/// </summary>
		public static readonly AvaloniaProperty<string?> MessageProperty = AvaloniaProperty.Register<MessageDialog, string?>(nameof(Message));


		// Fields.
		MessageDialogResult? result;


		/// <summary>
		/// Initialize new <see cref="MessageDialog"/> instance.
		/// </summary>
		public MessageDialog()
		{
			// create command
			this.SelectResultCommand = ReactiveCommand.Create<MessageDialogResult?>(this.SelectResult);

			// initialize
			InitializeComponent();
		}


		/// <summary>
		/// Get result of button 1.
		/// </summary>
		public MessageDialogResult? Button1Result { get => this.GetValue<MessageDialogResult?>(Button1ResultProperty); }


		/// <summary>
		/// Get text of button 1.
		/// </summary>
		public string? Button1Text { get => this.GetValue<string?>(Button1TextProperty); }


		/// <summary>
		/// Get result of button 2.
		/// </summary>
		public MessageDialogResult? Button2Result { get => this.GetValue<MessageDialogResult?>(Button2ResultProperty); }


		/// <summary>
		/// Get text of button 2.
		/// </summary>
		public string? Button2Text { get => this.GetValue<string?>(Button2TextProperty); }


		/// <summary>
		/// Get result of button 3.
		/// </summary>
		public MessageDialogResult? Button3Result { get => this.GetValue<MessageDialogResult?>(Button3ResultProperty); }


		/// <summary>
		/// Get text of button 3.
		/// </summary>
		public string? Button3Text { get => this.GetValue<string?>(Button3TextProperty); }


		/// <summary>
		/// Get or set buttons.
		/// </summary>
		public MessageDialogButtons Buttons { get; set; } = MessageDialogButtons.OK;


		/// <summary>
		/// Get or set icon.
		/// </summary>
		public new MessageDialogIcon Icon { get; set; } = MessageDialogIcon.Information;


		/// <summary>
		/// Get <see cref="Drawing"/> according to <see cref="Icon"/>.
		/// </summary>
		public Drawing? IconDrawing { get => this.GetValue<Drawing?>(IconDrawingProperty); }


		// Initialize Avalonia components.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		/// <summary>
		/// Check whether button 1 is visible or not.
		/// </summary>
		public bool IsButton1Visible { get => this.GetValue<bool>(IsButton1VisibleProperty); }


		/// <summary>
		/// Check whether button 2 is visible or not.
		/// </summary>
		public bool IsButton2Visible { get => this.GetValue<bool>(IsButton2VisibleProperty); }


		/// <summary>
		/// Check whether button 3 is visible or not.
		/// </summary>
		public bool IsButton3Visible { get => this.GetValue<bool>(IsButton3VisibleProperty); }


		/// <summary>
		/// Get or set message to show.
		/// </summary>
		public string? Message
		{
			get => this.GetValue<string?>(MessageProperty);
			set => this.SetValue<string?>(MessageProperty, value);
		}


		// Called when closing.
		protected override void OnClosing(CancelEventArgs e)
		{
			if (this.result == null)
				e.Cancel = true;
			base.OnClosing(e);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			// setup icon
			var app = (App)this.Application;
			if (app.Resources.TryGetResource($"Drawing.{this.Icon}", out var res) && res is Drawing drawing)
				this.SetValue<Drawing?>(IconDrawingProperty, drawing);

			// setup buttons
			switch (this.Buttons)
			{
				case MessageDialogButtons.OK:
					this.SetValue<MessageDialogResult?>(Button1ResultProperty, MessageDialogResult.OK);
					this.SetValue<string?>(Button1TextProperty, app.GetString("Common.OK"));
					this.SetValue<bool>(IsButton1VisibleProperty, true);
					break;
				case MessageDialogButtons.OKCancel:
					this.SetValue<MessageDialogResult?>(Button1ResultProperty, MessageDialogResult.OK);
					this.SetValue<MessageDialogResult?>(Button2ResultProperty, MessageDialogResult.Cancel);
					this.SetValue<string?>(Button1TextProperty, app.GetString("Common.OK"));
					this.SetValue<string?>(Button2TextProperty, app.GetString("Common.Cancel"));
					this.SetValue<bool>(IsButton1VisibleProperty, true);
					this.SetValue<bool>(IsButton2VisibleProperty, true);
					break;
				case MessageDialogButtons.YesNo:
					this.SetValue<MessageDialogResult?>(Button1ResultProperty, MessageDialogResult.Yes);
					this.SetValue<MessageDialogResult?>(Button2ResultProperty, MessageDialogResult.No);
					this.SetValue<string?>(Button1TextProperty, app.GetString("Common.Yes"));
					this.SetValue<string?>(Button2TextProperty, app.GetString("Common.No"));
					this.SetValue<bool>(IsButton1VisibleProperty, true);
					this.SetValue<bool>(IsButton2VisibleProperty, true);
					break;
				case MessageDialogButtons.YesNoCancel:
					this.SetValue<MessageDialogResult?>(Button1ResultProperty, MessageDialogResult.Yes);
					this.SetValue<MessageDialogResult?>(Button2ResultProperty, MessageDialogResult.No);
					this.SetValue<MessageDialogResult?>(Button3ResultProperty, MessageDialogResult.Cancel);
					this.SetValue<string?>(Button1TextProperty, app.GetString("Common.Yes"));
					this.SetValue<string?>(Button2TextProperty, app.GetString("Common.No"));
					this.SetValue<string?>(Button3TextProperty, app.GetString("Common.Cancel"));
					this.SetValue<bool>(IsButton1VisibleProperty, true);
					this.SetValue<bool>(IsButton2VisibleProperty, true);
					this.SetValue<bool>(IsButton3VisibleProperty, true);
					break;
				default:
					throw new ArgumentException();
			}

			// call base
			base.OnOpened(e);
		}


		// Select result.
		void SelectResult(MessageDialogResult? result)
		{
			if (result != null)
			{
				this.result = result;
				this.Close(result);
			}
		}


		/// <summary>
		/// Command to select result.
		/// </summary>
		public ICommand SelectResultCommand { get; }
	}


	/// <summary>
	/// Combination of buttons of <see cref="MessageDialog"/>.
	/// </summary>
	enum MessageDialogButtons
	{
		/// <summary>
		/// OK.
		/// </summary>
		OK,
		/// <summary>
		/// OK and Cancel.
		/// </summary>
		OKCancel,
		/// <summary>
		/// Yes and No.
		/// </summary>
		YesNo,
		/// <summary>
		/// Yes, No and Cancel.
		/// </summary>
		YesNoCancel,
	}


	/// <summary>
	/// Icon of <see cref="MessageDialog"/>.
	/// </summary>
	enum MessageDialogIcon
	{
		/// <summary>
		/// Information.
		/// </summary>
		Information,
		/// <summary>
		/// Question.
		/// </summary>
		Question,
		/// <summary>
		/// Warning.
		/// </summary>
		Warning,
		/// <summary>
		/// Error.
		/// </summary>
		Error,
	}


	/// <summary>
	/// Result of <see cref="MessageDialog"/>
	/// </summary>
	enum MessageDialogResult
	{
		/// <summary>
		/// OK.
		/// </summary>
		OK,
		/// <summary>
		/// Cancel.
		/// </summary>
		Cancel,
		/// <summary>
		/// Yes.
		/// </summary>
		Yes,
		/// <summary>
		/// No.
		/// </summary>
		No,
	}
}
