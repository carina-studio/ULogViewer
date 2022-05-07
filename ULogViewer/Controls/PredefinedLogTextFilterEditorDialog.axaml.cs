using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to edit or create <see cref="PredefinedLogTextFilter"/>.
	/// </summary>
	partial class PredefinedLogTextFilterEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		// Fields.
		Regex? regex;
		readonly TextBox nameTextBox;
		readonly TextBox patternTextBox;


		// Constructor.
		public PredefinedLogTextFilterEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.nameTextBox = this.FindControl<TextBox>(nameof(nameTextBox))!.Also(it =>
			{
				it.GetObservable(TextBox.TextProperty).Subscribe(_ =>
					this.InvalidateInput());
			});
			this.patternTextBox = this.FindControl<TextBox>(nameof(patternTextBox)).AsNonNull();
		}


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
				this.InvalidateInput();
			}
		}


		/// <summary>
		/// Get or set <see cref="PredefinedLogTextFilter"/> to be edited.
		/// </summary>
		public PredefinedLogTextFilter? Filter { get; set; }


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var name = this.nameTextBox.Text;
			var regex = this.regex.AsNonNull();
			var filter = this.Filter;
			if (filter != null)
			{
				filter.Name = name;
				filter.Regex = regex;
			}
			else
				filter = new PredefinedLogTextFilter(this.Application, name, regex);
			return Task.FromResult((object?)filter);
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var filter = this.Filter;
			if (filter == null)
				this.Bind(TitleProperty, this.GetResourceObservable("String/PredefinedLogTextFilterEditorDialog.Title.Create"));
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
					it.Anchor = this.FindControl<Control>("editPatternButton");
					it.Bind(Tutorial.DescriptionProperty, this.GetResourceObservable("String/RegexEditorDialog.Tutorial.ClickButtonToEditPattern"));
					it.Dismissed += (_, e) =>
					{
						this.Application.PersistentState.SetValue<bool>(RegexEditorDialog.IsClickButtonToEditPatternTutorialShownKey, true);
						this.nameTextBox.Focus();
					};
					it.Icon = (IImage?)this.FindResource("Image/Icon.Lightbulb.Colored");
					it.IsSkippingAllTutorialsAllowed = false;
				}));
			}
			else
				this.SynchronizationContext.Post(this.nameTextBox.Focus);
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			// call base
			if (!base.OnValidateInput())
				return false;

			// check name
			var name = this.nameTextBox.Text?.Trim() ?? "";
			if (name.Length == 0)
				return false;

			// check regex
			if (this.regex == null)
				return false;

			// ok
			return true;
		}


		/// <summary>
		/// Get or set <see cref="Regex"/> of text filter.
		/// </summary>
		public Regex? Regex { get; set; }
	}
}
