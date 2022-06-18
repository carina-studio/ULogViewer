using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis.ContextualBased;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="PushVariableAction"/>.
	/// </summary>
	partial class PushVarEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly TextBox stackTextBox;
		readonly TextBox varTextBox;


		/// <summary>
		/// Initialize new <see cref="PushVarEditorDialog"/> instance.
		/// </summary>
		public PushVarEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.stackTextBox = this.Get<TextBox>(nameof(stackTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.varTextBox = this.Get<TextBox>(nameof(varTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
		}


		/// <summary>
		/// Get or set action to be edited.
		/// </summary>
		public PushVariableAction? Action { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult<object?>(new PushVariableAction(this.varTextBox.Text.AsNonNull().Trim(), this.stackTextBox.Text.AsNonNull().Trim()));


		// Dialog opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var action = this.Action;
			if (action != null)
			{
				this.stackTextBox.Text = action.Stack;
				this.varTextBox.Text = action.Variable;
			}
			this.SynchronizationContext.Post(this.varTextBox.Focus);
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() 
			&& !string.IsNullOrWhiteSpace(this.stackTextBox.Text) 
			&& !string.IsNullOrWhiteSpace(this.varTextBox.Text);
	}
}
