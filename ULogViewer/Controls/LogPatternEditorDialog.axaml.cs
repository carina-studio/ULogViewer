using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Editor of <see cref="LogPattern"/>.
	/// </summary>
	partial class LogPatternEditorDialog : InputDialog<IULogViewerApplication>
	{
		// Fields.
		Regex? regex;
		readonly TextBox patternTextBox;
		readonly ToggleSwitch repeatableSwitch;
		readonly ToggleSwitch skippableSwitch;


		/// <summary>
		/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
		/// </summary>
		public LogPatternEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.patternTextBox = this.FindControl<TextBox>(nameof(patternTextBox)).AsNonNull();
			this.repeatableSwitch = this.FindControl<ToggleSwitch>(nameof(repeatableSwitch)).AsNonNull();
			this.skippableSwitch = this.FindControl<ToggleSwitch>(nameof(skippableSwitch)).AsNonNull();
		}


		// Edit regex.
		async void EditRegex()
		{
			var regex = await new RegexEditorDialog()
			{
				InitialRegex = this.regex,
				IsCapturingGroupsEnabled = true,
				IsCapturingLogPropertiesEnabled = true,
			}.ShowDialog<Regex?>(this);
			if (regex != null)
			{
				this.regex = regex;
				this.patternTextBox.Text = regex.ToString();
				this.InvalidateInput();
			}
		}


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var editingLogPattern = this.LogPattern;
			var newLogPattern = new LogPattern(this.regex.AsNonNull(), this.repeatableSwitch.IsChecked.GetValueOrDefault(), this.skippableSwitch.IsChecked.GetValueOrDefault());
			if (editingLogPattern != null && editingLogPattern == newLogPattern)
				return Task.FromResult((object?)editingLogPattern);
			return Task.FromResult((object?)newLogPattern);
		}


		/// <summary>
		/// Get or set <see cref="LogPattern"/> to be edited.
		/// </summary>
		public LogPattern? LogPattern { get; set; }


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var logPattern = this.LogPattern;
			var editPatternButton = this.FindControl<Control>("editPatternButton").AsNonNull();
			if (logPattern != null)
			{
				this.regex = logPattern.Regex;
				this.patternTextBox.Text = this.regex.ToString();
				this.repeatableSwitch.IsChecked = logPattern.IsRepeatable;
				this.skippableSwitch.IsChecked = logPattern.IsSkippable;
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


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() && this.regex != null;
	}
}
