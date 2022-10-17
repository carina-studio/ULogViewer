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
	/// Dialog to edit <see cref="VariablesComparisonCondition"/>.
	/// </summary>
	partial class VarsComparisonEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		readonly ComboBox comparisonTypeComboBox;
		readonly TextBox lhsVarTextBox;
		readonly TextBox rhsVarTextBox;


		/// <summary>
		/// Initialize new <see cref="VarsComparisonEditorDialog"/> instance.
		/// </summary>
		public VarsComparisonEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.comparisonTypeComboBox = this.Get<ComboBox>(nameof(comparisonTypeComboBox));
			this.lhsVarTextBox = this.Get<TextBox>(nameof(lhsVarTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.rhsVarTextBox = this.Get<TextBox>(nameof(rhsVarTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
			});
		}


		/// <summary>
		/// Get or set condition to be edited.
		/// </summary>
		public VariablesComparisonCondition? Condition { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var condition = this.Condition;
			var newCondition = new VariablesComparisonCondition(this.lhsVarTextBox.Text.AsNonNull().Trim(), (ComparisonType)this.comparisonTypeComboBox.SelectedItem.AsNonNull(), this.rhsVarTextBox.Text.AsNonNull().Trim());
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
				this.lhsVarTextBox.Text = condition.LhsVariable;
				this.rhsVarTextBox.Text = condition.RhsVariable;
			}
			else
				this.comparisonTypeComboBox.SelectedItem = ComparisonType.Equivalent;
			this.SynchronizationContext.Post(this.lhsVarTextBox.Focus);
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() 
			&& !string.IsNullOrWhiteSpace(this.lhsVarTextBox.Text) 
			&& !string.IsNullOrWhiteSpace(this.rhsVarTextBox.Text);
	}
}
