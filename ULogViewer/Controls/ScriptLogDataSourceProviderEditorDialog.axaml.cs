using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Collections;
using CarinaStudio.Configuration;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs.DataSources;
using CarinaStudio.ULogViewer.Scripting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to edit <see cref="ScriptLogDataSourceProvider"/>s.
/// </summary>
partial class ScriptLogDataSourceProviderEditorDialog : CarinaStudio.Controls.InputDialog<IULogViewerApplication>
{
	// Supported source option.
	public class SupportedSourceOption
	{
		// Fields.
		bool? isRequired;

		// Constructor.
		public SupportedSourceOption(string name, bool isRequired)
		{
			this.CanBeRequired = name switch
			{
				nameof(LogDataSourceOptions.Encoding)
				or nameof(LogDataSourceOptions.FormatJsonData)
				or nameof(LogDataSourceOptions.FormatXmlData)
				or nameof(LogDataSourceOptions.IncludeStandardError)
				or nameof(LogDataSourceOptions.IsResourceOnAzure)
				or nameof(LogDataSourceOptions.SetupCommands)
				or nameof(LogDataSourceOptions.TeardownCommands) => false,
				_ => true,
			};
			this.isRequired = this.CanBeRequired ? isRequired : null;
			this.Name = name;
		}

		// Whether option can be required or not.
		public bool CanBeRequired { get; }

		// Whether option is required or not.
		public bool? IsRequired
		{
			get => this.isRequired;
			set
			{
				if (this.CanBeRequired)
					this.isRequired = value;
			}
		}

		// Option name.
		public string Name { get; }
	}


	// Fields.
	LogDataSourceScript? closingReaderScript;
	readonly SortedObservableList<CompilationResult> closingReaderScriptCompilationResults = new(CompareCompilationResult);
	readonly ScriptEditor closingReaderScriptEditor;
	readonly ScheduledAction compileClosingReaderScriptAction;
	readonly ScheduledAction compileOpeningReaderScriptAction;
	readonly ScheduledAction compileReadingLineScriptAction;
	readonly TextBox displayNameTextBox;
	LogDataSourceScript? openingReaderScript;
	readonly SortedObservableList<CompilationResult> openingReaderScriptCompilationResults = new(CompareCompilationResult);
	readonly ScriptEditor openingReaderScriptEditor;
	LogDataSourceScript? readingLineScript;
	readonly SortedObservableList<CompilationResult> readingLineScriptCompilationResults = new(CompareCompilationResult);
	readonly ScriptEditor readingLineScriptEditor;
	readonly SortedObservableList<SupportedSourceOption> supportedSourceOptions = new((lhs, rhs) => string.Compare(lhs.Name, rhs.Name, true, CultureInfo.InvariantCulture));


	/// <summary>
	/// Initialize new <see cref="ScriptLogDataSourceProviderEditorDialog"/> instance.
	/// </summary>
	public ScriptLogDataSourceProviderEditorDialog()
	{
		this.ClosingReaderScriptCompilationResults = this.closingReaderScriptCompilationResults.AsReadOnly();
		this.OpeningReaderScriptCompilationResults = this.openingReaderScriptCompilationResults.AsReadOnly();
		this.ReadingLineScriptCompilationResults = this.readingLineScriptCompilationResults.AsReadOnly();
		this.SupportedSourceOptions = this.supportedSourceOptions.AsReadOnly();
		AvaloniaXamlLoader.Load(this);
		this.closingReaderScriptEditor = this.Get<ScriptEditor>(nameof(closingReaderScriptEditor)).Also(it =>
		{
			void ScheduleCompilation()
			{
				closingReaderScript = null;
				this.compileClosingReaderScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			}
			it.GetObservable(ScriptEditor.LanguageProperty).Subscribe(_ => ScheduleCompilation());
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ => ScheduleCompilation());
		});
		this.compileClosingReaderScriptAction = new(async () =>
		{
			var script = this.closingReaderScript ?? this.CreateScript(this.closingReaderScriptEditor).Also(it => this.closingReaderScript = it);
			if (script != null)
			{
				this.Logger.LogTrace("Start compiling closing reader script");
				var result = await script.CompileAsync();
				if (this.closingReaderScript != script)
					return;
				this.Logger.LogTrace("Closing reader script compilation " + (result ? "succeeded" : "failed") + ", result count: " + script.CompilationResults.Count);
				this.closingReaderScriptCompilationResults.Clear();
				this.closingReaderScriptCompilationResults.AddAll(script.CompilationResults);
			}
			else
				this.closingReaderScriptCompilationResults.Clear();
		});
		this.compileOpeningReaderScriptAction = new(async () =>
		{
			var script = this.openingReaderScript ?? this.CreateScript(this.openingReaderScriptEditor).Also(it => this.openingReaderScript = it);
			if (script != null)
			{
				this.Logger.LogTrace("Start compiling opening reader script");
				var result = await script.CompileAsync();
				if (this.openingReaderScript != script)
					return;
				this.Logger.LogTrace("Opening reader script compilation " + (result ? "succeeded" : "failed") + ", result count: " + script.CompilationResults.Count);
				this.openingReaderScriptCompilationResults.Clear();
				this.openingReaderScriptCompilationResults.AddAll(script.CompilationResults);
			}
			else
				this.openingReaderScriptCompilationResults.Clear();
		});
		this.compileReadingLineScriptAction = new(async () =>
		{
			var script = this.readingLineScript ?? this.CreateScript(this.readingLineScriptEditor).Also(it => this.readingLineScript = it);
			if (script != null)
			{
				this.Logger.LogTrace("Start compiling reading line script");
				var result = await script.CompileAsync();
				if (this.readingLineScript != script)
					return;
				this.Logger.LogTrace("Reading line script compilation " + (result ? "succeeded" : "failed") + ", result count: " + script.CompilationResults.Count);
				this.readingLineScriptCompilationResults.Clear();
				this.readingLineScriptCompilationResults.AddAll(script.CompilationResults);
			}
			else
				this.readingLineScriptCompilationResults.Clear();
		});
		this.displayNameTextBox = this.Get<TextBox>(nameof(displayNameTextBox)).Also(it =>
		{
			it.GetObservable(TextBox.TextProperty).Subscribe(_ => this.InvalidateInput());
		});
		this.openingReaderScriptEditor = this.Get<ScriptEditor>(nameof(openingReaderScriptEditor)).Also(it =>
		{
			void ScheduleCompilation()
			{
				openingReaderScript = null;
				this.compileOpeningReaderScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			}
			it.GetObservable(ScriptEditor.LanguageProperty).Subscribe(_ => ScheduleCompilation());
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ => ScheduleCompilation());
		});
		this.readingLineScriptEditor = this.Get<ScriptEditor>(nameof(readingLineScriptEditor)).Also(it =>
		{
			void ScheduleCompilation()
			{
				readingLineScript = null;
				this.compileReadingLineScriptAction?.Reschedule(this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.DelayToCompileScriptWhenEditing));
			}
			it.GetObservable(ScriptEditor.LanguageProperty).Subscribe(_ => ScheduleCompilation());
			it.GetObservable(ScriptEditor.SourceProperty).Subscribe(_ => ScheduleCompilation());
		});
	}


	// Get compilation results of closing reader script.
	IList<CompilationResult> ClosingReaderScriptCompilationResults { get; }


	// Compare compilation result.
	static int CompareCompilationResult(CompilationResult lhs, CompilationResult rhs)
	{
		var result = (int)rhs.Type - (int)lhs.Type;
		if (result != 0)
			return result;
		result = lhs.StartPosition.GetValueOrDefault().Item1 - rhs.StartPosition.GetValueOrDefault().Item1;
		if (result != 0)
			return result;
		result = lhs.StartPosition.GetValueOrDefault().Item2 - rhs.StartPosition.GetValueOrDefault().Item2;
		if (result != 0)
			return result;
		result = string.Compare(lhs.Message, rhs.Message, true, CultureInfo.InvariantCulture);
		if (result != 0)
			return result;
		return lhs.GetHashCode() - rhs.GetHashCode();
	}


	// Create script.
	LogDataSourceScript? CreateScript(ScriptEditor? editor) => editor?.Source?.Let(source =>
	{
		if (string.IsNullOrEmpty(source))
			return null;
		return new LogDataSourceScript(this.Application, editor.Language, source);
	});


	/// <inheritdoc/>
	protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
	{
		var provider = this.Provider ?? new ScriptLogDataSourceProvider(this.Application);
		provider.DisplayName = this.displayNameTextBox.Text;
		provider.ClosingReaderScript = this.closingReaderScript ?? this.CreateScript(this.closingReaderScriptEditor);
		provider.OpeningReaderScript = this.openingReaderScript ?? this.CreateScript(this.openingReaderScriptEditor);
		provider.ReadingLineScript = this.readingLineScript ?? this.CreateScript(this.readingLineScriptEditor);
		provider.SetSupportedSourceOptions(
			this.supportedSourceOptions.Select(it => it.Name),
			this.supportedSourceOptions.Where(it => it.IsRequired == true).Select(it => it.Name)
		);
		return Task.FromResult<object?>(provider);
	}


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		this.compileClosingReaderScriptAction.Cancel();
		this.compileOpeningReaderScriptAction.Cancel();
		this.compileReadingLineScriptAction.Cancel();
		base.OnClosed(e);
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		// call base
		base.OnOpened(e);

		// setup initial window size and position
		(this.Screens.ScreenFromWindow(this.PlatformImpl) ?? this.Screens.Primary)?.Let(screen =>
		{
			var workingArea = screen.WorkingArea;
			var widthRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitWidthRatio);
			var heightRatio = this.Application.Configuration.GetValueOrDefault(ConfigurationKeys.LogAnalysisScriptSetEditorDialogInitHeightRatio);
			var pixelDensity = Platform.IsMacOS ? 1.0 : screen.PixelDensity;
			var left = (workingArea.TopLeft.X + workingArea.Width * (1 - widthRatio) / 2); // in device pixels
			var top = (workingArea.TopLeft.Y + workingArea.Height * (1 - heightRatio) / 2); // in device pixels
			var width = (workingArea.Width * widthRatio) / pixelDensity;
			var height = (workingArea.Height * heightRatio) / pixelDensity;
			var sysDecorSize = this.GetSystemDecorationSizes();
			this.Position = new((int)(left + 0.5), (int)(top + 0.5));
			this.Width = width;
			this.Height = (height - sysDecorSize.Top - sysDecorSize.Bottom);
		});

		// show provider
		var provider = this.Provider;
		if (provider != null)
		{
			this.closingReaderScript = provider.ClosingReaderScript;
			this.closingReaderScript?.Let(script =>
			{
				this.closingReaderScriptEditor.Language = script.Language;
				this.closingReaderScriptEditor.Source = script.Source;
			});
			this.displayNameTextBox.Text = provider.DisplayName;
			this.openingReaderScript = provider.OpeningReaderScript;
			this.openingReaderScript?.Let(script =>
			{
				this.openingReaderScriptEditor.Language = script.Language;
				this.openingReaderScriptEditor.Source = script.Source;
			});
			this.readingLineScript = provider.ReadingLineScript;
			this.readingLineScript?.Let(script =>
			{
				this.readingLineScriptEditor.Language = script.Language;
				this.readingLineScriptEditor.Source = script.Source;
			});
			foreach (var option in provider.SupportedSourceOptions)
				this.supportedSourceOptions.Add(new(option, provider.RequiredSourceOptions.Contains(option)));
			this.compileClosingReaderScriptAction.Schedule();
			this.compileOpeningReaderScriptAction.Schedule();
			this.compileReadingLineScriptAction.Schedule();
		}
		else
		{
			this.closingReaderScriptEditor.Language = this.Settings.GetValueOrDefault(SettingKeys.DefaultScriptLanguage);
			this.openingReaderScriptEditor.Language = this.Settings.GetValueOrDefault(SettingKeys.DefaultScriptLanguage);
			this.readingLineScriptEditor.Language = this.Settings.GetValueOrDefault(SettingKeys.DefaultScriptLanguage);
		}

		// setup initial focus
		this.SynchronizationContext.Post(() =>
		{
			this.Get<ScrollViewer>("contentScrollViewer").ScrollToHome();
			this.displayNameTextBox.Focus();
		});

		// enable script running
		if (!this.Settings.GetValueOrDefault(SettingKeys.EnableRunningScript))
		{
			this.SynchronizationContext.Post(async () =>
			{
				if (this.Settings.GetValueOrDefault(SettingKeys.EnableRunningScript) || this.IsClosed)
					return;
				var result = await new MessageDialog()
				{
					Buttons = AppSuite.Controls.MessageDialogButtons.YesNo,
					DefaultResult = AppSuite.Controls.MessageDialogResult.No,
					Icon = AppSuite.Controls.MessageDialogIcon.Warning,
					Message = this.Application.GetString("Script.MessageForEnablingRunningScript"),
				}.ShowDialog(this);
				if (result == AppSuite.Controls.MessageDialogResult.Yes)
					this.Settings.SetValue<bool>(SettingKeys.EnableRunningScript, true);
				else
					this.Close();
			});
		}
	}


	/// <inheritdoc/>
	protected override bool OnValidateInput() =>
		base.OnValidateInput() && !string.IsNullOrWhiteSpace(this.displayNameTextBox.Text);


	// Open online documentation.
	void OpenDocumentation() =>
		Platform.OpenLink("https://carinastudio.azurewebsites.net/ULogViewer/Scripting");
	

	// Get compilation results of opening reader script.
	IList<CompilationResult> OpeningReaderScriptCompilationResults { get; }


	/// <summary>
	/// Get or set script log data source provider to edit.
	/// </summary>
	public ScriptLogDataSourceProvider? Provider { get; set; }


	// Get compilation results of reading line script.
	IList<CompilationResult> ReadingLineScriptCompilationResults { get; }


	// Get supported log data source options.
	IList<SupportedSourceOption> SupportedSourceOptions { get; }
}
