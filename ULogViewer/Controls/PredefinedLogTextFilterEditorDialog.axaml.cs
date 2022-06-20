using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
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
		static readonly AvaloniaProperty<bool> AreValidParametersProperty = AvaloniaProperty.Register<PredefinedLogTextFilterEditorDialog, bool>("AreValidParameters");
		static readonly Dictionary<PredefinedLogTextFilter, PredefinedLogTextFilterEditorDialog> DialogWithEditingRuleSets = new();


		// Fields.
		PredefinedLogTextFilter? editingFilter;
		Regex? regex;
		readonly TextBox nameTextBox;
		readonly TextBox patternTextBox;
		readonly ScheduledAction validateParametersAction;


		// Constructor.
		public PredefinedLogTextFilterEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.nameTextBox = this.FindControl<TextBox>(nameof(nameTextBox))!.Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ =>
					this.validateParametersAction?.Schedule());
			});
			this.patternTextBox = this.FindControl<TextBox>(nameof(patternTextBox)).AsNonNull();
			this.validateParametersAction = new(() =>
			{
				if (this.IsClosed)
					return;
				if (string.IsNullOrWhiteSpace(this.nameTextBox.Text)
					|| this.regex == null)
				{
					this.SetValue<bool>(AreValidParametersProperty, false);
				}
				else
					this.SetValue<bool>(AreValidParametersProperty, true);
			});
		}


		// Complete editing.
		void CompleteEditing()
		{
			// validate parameters
			this.validateParametersAction.ExecuteIfScheduled();
			if (!this.GetValue<bool>(AreValidParametersProperty))
				return;
			
			// edit or add filter
			var name = this.nameTextBox.Text;
			var regex = this.regex.AsNonNull();
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


		// Copy pattern.
		void CopyPattern(TextBox textBox) =>
			textBox.CopyTextIfNotEmpty();


		// Edit pattern.
		async void EditPattern()
		{
			var regex = await new RegexEditorDialog()
			{
				InitialRegex = this.regex,
			}.ShowDialog<Regex?>(this);
			if (regex != null)
			{
				this.regex = regex;
				this.patternTextBox.Text = regex.ToString();
				this.validateParametersAction.Schedule();
			}
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
			var editPatternButton = this.FindControl<Control>("editPatternButton").AsNonNull();
			if (filter == null)
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Create"));
				this.patternTextBox.Text = this.regex?.ToString();
			}
			else
			{
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Edit"));
				this.nameTextBox.Text = filter.Name;
				this.patternTextBox.Text = filter.Regex.ToString();
				this.regex = filter.Regex;
			}
			if (!this.Application.PersistentState.GetValueOrDefault(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey))
			{
				this.FindControl<TutorialPresenter>("tutorialPresenter")!.ShowTutorial(new Tutorial().Also(it =>
				{
					it.Anchor = editPatternButton;
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/RegexEditorDialog.Tutorial.ClickButtonToEditPattern"));
					it.Dismissed += (_, e) =>
					{
						this.Application.PersistentState.SetValue<bool>(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey, true);
						editPatternButton.Focus();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.IsSkippingAllTutorialsAllowed = false;
				}));
			}
			else
				this.SynchronizationContext.Post(editPatternButton.Focus);
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
				regex = regex,
			};
			if (filter != null)
				DialogWithEditingRuleSets[filter] = dialog;
			dialog.Show(parent);
		}
	}
}
