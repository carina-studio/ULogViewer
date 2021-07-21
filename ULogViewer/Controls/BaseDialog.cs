using Avalonia;
using CarinaStudio.Threading;
using ReactiveUI;
using System;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Base class for dialog in ULogViewer.
	/// </summary>
	abstract class BaseDialog : BaseWindow
	{
		/// <summary>
		/// Property of <see cref="IsValidInput"/>.
		/// </summary>
		public static readonly AvaloniaProperty<bool> IsValidInputProperty = AvaloniaProperty.Register<BaseDialog, bool>(nameof(IsValidInput));


		// Fields.
		BaseWindow? ownerWindow;
		readonly ScheduledAction validateInputAction;


		/// <summary>
		/// Initialize new <see cref="BaseDialog"/> instance.
		/// </summary>
		protected BaseDialog()
		{
			this.CancelCommand = ReactiveCommand.Create(this.Close);
			this.GenerateResultCommand = ReactiveCommand.Create(() =>
			{
				this.VerifyAccess();
				if (!this.ValidateInput())
					return;
				this.Close(this.OnGenerateResult());
			}, this.GetObservable<bool>(IsValidInputProperty));
			this.validateInputAction = new ScheduledAction(() => this.ValidateInput());
		}


		/// <summary>
		/// Command to close dialog without result.
		/// </summary>
		public ICommand CancelCommand { get; }


		/// <summary>
		/// Command to generate result with valid input and close dialog.
		/// </summary>
		public ICommand GenerateResultCommand { get; }


		/// <summary>
		/// Invalid input of dialog.
		/// </summary>
		protected void InvalidateInput() => this.validateInputAction.Schedule();


		/// <summary>
		/// Check whether input of dialog is valid or not.
		/// </summary>
		public bool IsValidInput { get => this.GetValue<bool>(IsValidInputProperty); }


		// Called when closed.
		protected override void OnClosed(EventArgs e)
		{
			this.ownerWindow?.OnDialogClosed(this);
			this.ownerWindow = null;
			base.OnClosed(e);
		}


		/// <summary>
		/// Called to generate result with valid input and close dialog.
		/// </summary>
		/// <returns>Result of dialog.</returns>
		protected abstract object? OnGenerateResult();


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			this.InvalidateInput();
			this.ownerWindow = (this.Owner as BaseWindow)?.Also(it => it.OnDialogOpened(this));
		}


		/// <summary>
		/// Called to validate input of dialog.
		/// </summary>
		/// <returns>True if input is valid.</returns>
		protected virtual bool OnValidateInput() => true;


		/// <summary>
		/// Validate input of dialog.
		/// </summary>
		/// <returns>True if input is valid.</returns>
		protected bool ValidateInput()
		{
			this.validateInputAction.Cancel();
			var isValid = this.OnValidateInput();
			this.SetValue<bool>(IsValidInputProperty, isValid);
			return isValid;
		}
	}
}
