using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;
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
		readonly PatternEditor patternEditor;
		readonly ToggleSwitch repeatableSwitch;
		readonly ToggleSwitch skippableSwitch;


		/// <summary>
		/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
		/// </summary>
		public LogPatternEditorDialog()
		{
			AvaloniaXamlLoader.Load(this);
			this.patternEditor = this.Get<PatternEditor>(nameof(patternEditor)).Also(it =>
			{
				it.GetObservable(PatternEditor.PatternProperty).Subscribe(_ => this.InvalidateInput());
			});
			this.repeatableSwitch = this.Get<ToggleSwitch>(nameof(repeatableSwitch));
			this.skippableSwitch = this.Get<ToggleSwitch>(nameof(skippableSwitch));
		}


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var newLogPattern = new LogPattern(this.patternEditor.Pattern.AsNonNull(), this.repeatableSwitch.IsChecked.GetValueOrDefault(), this.skippableSwitch.IsChecked.GetValueOrDefault());
			return Task.FromResult((object?)newLogPattern);
		}


		/// <summary>
		/// Get or set <see cref="LogPattern"/> to be edited.
		/// </summary>
		public LogPattern? LogPattern { get; set; }


		/// <inheritdoc/>
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			var logPattern = this.LogPattern;
			if (logPattern != null)
			{
				this.patternEditor.Pattern = logPattern.Regex;
				this.repeatableSwitch.IsChecked = logPattern.IsRepeatable;
				this.skippableSwitch.IsChecked = logPattern.IsSkippable;
			}
			this.SynchronizationContext.Post(() =>
			{
				if (!this.patternEditor.ShowTutorialIfNeeded(this.Get<TutorialPresenter>("tutorialPresenter"), this.patternEditor))
					this.patternEditor.Focus();
			});
		}


		// Validate input.
		protected override bool OnValidateInput() =>
			base.OnValidateInput() && this.patternEditor.Pattern != null;
	}
}
