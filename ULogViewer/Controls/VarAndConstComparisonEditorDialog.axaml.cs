using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels.Analysis;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit <see cref="VariableAndConstantComparisonCondition"/>.
	/// </summary>
	partial class VarAndConstComparisonEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ComboBox comparisonTypeComboBox;
		readonly TextBox constantTextBox;
		readonly TextBox varTextBox;


		/// <summary>
		/// Initialize new <see cref="VarAndConstComparisonEditorDialog"/> instance.
		/// </summary>
		public VarAndConstComparisonEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.comparisonTypeComboBox = this.Get<ComboBox>(nameof(comparisonTypeComboBox));
			this.constantTextBox = this.Get<TextBox>(nameof(constantTextBox));
			this.varTextBox = this.Get<TextBox>(nameof(varTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
		}


		/// <summary>
		/// Get or set condition to be edited.
		/// </summary>
		public VariableAndConstantComparisonCondition? Condition { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var condition = this.Condition;
			var newCondition = new VariableAndConstantComparisonCondition(this.varTextBox.Text.AsNonNull().Trim(), (ComparisonType)this.comparisonTypeComboBox.SelectedItem.AsNonNull(), this.constantTextBox.Text ?? "");
			if (newCondition != condition)
				return Task.FromResult<object?>(newCondition);
			return Task.FromResult<object?>(condition);
		}


		// Dialog opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var condition = this.Condition;
			if (condition != null)
			{
				this.comparisonTypeComboBox.SelectedItem = condition.ComparisonType;
				this.constantTextBox.Text = condition.Constant;
				this.varTextBox.Text = condition.Variable;
			}
			else
				this.comparisonTypeComboBox.SelectedItem = ComparisonType.Equivalent;
			this.SynchronizationContext.Post(this.varTextBox.Focus);
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.varTextBox.Text);
	}
}
