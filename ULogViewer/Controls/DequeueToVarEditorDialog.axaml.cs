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
	/// Dialog to edit <see cref="DequeueToVariableAction"/>.
	/// </summary>
	class DequeueToVarEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly TextBox queueTextBox;
		readonly TextBox varTextBox;


		/// <summary>
		/// Initialize new <see cref="DequeueToVarEditorDialog"/> instance.
		/// </summary>
		public DequeueToVarEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.queueTextBox = this.Get<TextBox>(nameof(queueTextBox)).Also(it =>
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
		public DequeueToVariableAction? Action { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken) =>
			Task.FromResult<object?>(new DequeueToVariableAction(this.queueTextBox.Text.AsNonNull().Trim(), this.varTextBox.Text.AsNonNull().Trim()));


		/// <inheritdoc/>
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			this.SynchronizationContext.Post(() => this.queueTextBox.Focus());
		}


		/// <inheritdoc/>
		protected override void OnOpening(EventArgs e)
		{
			base.OnOpening(e);
			this.Action?.Let(it =>
			{
				this.queueTextBox.Text = it.Queue;
				this.varTextBox.Text = it.Variable;
			});
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() 
			&& !string.IsNullOrWhiteSpace(this.queueTextBox.Text) 
			&& !string.IsNullOrWhiteSpace(this.varTextBox.Text);
	}
}
