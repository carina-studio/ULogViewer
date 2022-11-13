using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit or create <see cref="PredefinedLogTextFilter"/>.
	/// </summary>
	partial class PredefinedLogTextFilterEditorDialog : AppSuite.Controls.Window<IULogViewerApplication>
	{
		// Static fields.
		static readonly StyledProperty<bool> AreValidParametersProperty = AvaloniaProperty.Register<PredefinedLogTextFilterEditorDialog, bool>("AreValidParameters");
		static readonly Dictionary<PredefinedLogTextFilter, PredefinedLogTextFilterEditorDialog> DialogWithEditingRuleSets = new();


		// Fields.
		PredefinedLogTextFilter? editingFilter;
		Regex? initialPattern;
		readonly TextBox nameTextBox;
		readonly PatternEditor patternEditor;
		readonly ScheduledAction validateParametersAction;


		// Constructor.
		public PredefinedLogTextFilterEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.nameTextBox = this.Get<TextBox>(nameof(nameTextBox)).Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ =>
					this.validateParametersAction?.Schedule());
			});
			this.patternEditor = this.Get<PatternEditor>(nameof(patternEditor)).Also(it =>
			{
				it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ =>
					this.validateParametersAction?.Schedule());
			});
			this.validateParametersAction = new(() =>
			{
				if (this.IsClosed)
					return;
				if (string.IsNullOrWhiteSpace(this.nameTextBox.Text)
					|| this.patternEditor.Pattern == null)
				{
					this.SetValue<bool>(AreValidParametersProperty, false);
				}
				else
					this.SetValue<bool>(AreValidParametersProperty, true);
			});
		}


		/// <summary>
		/// Complete editing.
		/// </summary>
		public void CompleteEditing()
		{
			// validate parameters
			this.validateParametersAction.ExecuteIfScheduled();
			if (!this.GetValue<bool>(AreValidParametersProperty))
				return;
			
			// edit or add filter
			var name = this.nameTextBox.Text.AsNonNull();
			var regex = this.patternEditor.Pattern.AsNonNull();
			var filter = this.editingFilter;
			if (filter != null)
			{
				filter.Name = name;
				filter.Regex = regex;
			}
			else
				filter = new PredefinedLogTextFilter(this.Application, name, regex);
			if (!PredefinedLogTextFilterManager.Default.Filters.Contains(filter))
				PredefinedLogTextFilterManager.Default.AddFilter(filter);

			// close window
			this.Close();
		}


		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			if (this.editingFilter != null)
				DialogWithEditingRuleSets.Remove(this.editingFilter);
			base.OnClosed(e);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var filter = this.editingFilter;
			if (filter == null)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Create"));
				this.patternEditor.Pattern = this.initialPattern;
			}
			else
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Edit"));
				this.nameTextBox.Text = filter.Name;
				this.patternEditor.Pattern = filter.Regex;
			}
			this.SynchronizationContext.Post(() =>
			{
				if (!this.patternEditor.ShowTutorialIfNeeded(this.Get<TutorialPresenter>("tutorialPresenter"), this.nameTextBox))
					this.nameTextBox.Focus();
			});
		}


		/// <inheritdoc/>
		protected override WindowTransparencyLevel OnSelectTransparentLevelHint() =>
			WindowTransparencyLevel.None;


		/// <summary>
		/// Show dialog to edit given text filter.
		/// </summary>
		/// <param name="parent">Parent window.</param>
		/// <param name="filter">Text filter to edit.</param>
		/// <param name="regex">Preferred regex for text filter.</param>
		public static void Show(Avalonia.Controls.Window parent, PredefinedLogTextFilter? filter, Regex? regex)
		{
			// show existing dialog
			if (filter != null && DialogWithEditingRuleSets.TryGetValue(filter, out var dialog))
			{
				dialog?.ActivateAndBringToFront();
				return;
			}

			// show dialog
			dialog = new()
			{
				editingFilter = filter,
				initialPattern = regex,
			};
			if (filter != null)
				DialogWithEditingRuleSets[filter] = dialog;
			dialog.Show(parent);
		}
	}
}
