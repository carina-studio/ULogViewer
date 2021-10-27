using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Logs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Editor of <see cref="LogPattern"/>.
	/// </summary>
	partial class LogPatternEditorDialog : AppSuite.Controls.InputDialog<IULogViewerApplication>
	{
		/// <summary>
		/// List of property names of log.
		/// </summary>
		public static readonly IList<string> LogPropertyNames = Log.PropertyNames.Where(it =>
		{
			return it switch
			{
				nameof(Log.FileName) => false,
				nameof(Log.LineNumber) => false,
				_ => true,
			};
		}).ToList().AsReadOnly();


		// Fields.
		readonly ToggleButton addLogPropertyGroupButton;
		readonly ContextMenu addLogPropertyGroupMenu;
		readonly RegexTextBox regexTextBox;
		readonly ToggleSwitch repeatableSwitch;
		readonly ToggleSwitch skippableSwitch;


		/// <summary>
		/// Initialize new <see cref="LogPatternEditorDialog"/> instance.
		/// </summary>
		public LogPatternEditorDialog()
		{
			InitializeComponent();
			this.addLogPropertyGroupButton = this.FindControl<ToggleButton>("addLogPropertyGroupButton").AsNonNull();
			this.addLogPropertyGroupMenu = ((ContextMenu)this.Resources["addLogPropertyGroupMenu"].AsNonNull()).Also(it =>
			{
				var itemTemplate = it.DataTemplates[0];
				var menuItems = new List<MenuItem>();
				foreach (var propertyName in LogPropertyNames)
				{
					var header = itemTemplate.Build(propertyName);
					menuItems.Add(new MenuItem().Also(menuItem =>
					{
						menuItem.Bind(MenuItem.CommandProperty, new Binding() { Path = nameof(InsertLogPropertyGroup), Source = this });
						menuItem.CommandParameter = propertyName;
						menuItem.DataContext = propertyName;
						menuItem.Header = header;
					}));
				}
				it.Items = menuItems;
				it.MenuClosed += (_, e) => this.SynchronizationContext.Post(() => this.addLogPropertyGroupButton.IsChecked = false);
				it.MenuOpened += (_, e) => this.SynchronizationContext.Post(() => this.addLogPropertyGroupButton.IsChecked = true);
			});
			this.regexTextBox = this.FindControl<RegexTextBox>("regexTextBox");
			this.repeatableSwitch = this.FindControl<ToggleSwitch>("repeatableSwitch");
			this.skippableSwitch = this.FindControl<ToggleSwitch>("skippableSwitch");
		}


		// Generate result.
		protected override Task<object?> GenerateResultAsync(CancellationToken cancellationToken)
		{
			var editingLogPattern = this.LogPattern;
			var newLogPattern = new LogPattern(this.regexTextBox.Regex.AsNonNull(), this.repeatableSwitch.IsChecked.GetValueOrDefault(), this.skippableSwitch.IsChecked.GetValueOrDefault());
			if (editingLogPattern != null && editingLogPattern == newLogPattern)
				return Task.FromResult((object?)editingLogPattern);
			return Task.FromResult((object?)newLogPattern);
		}


		// Initialize.
		private void InitializeComponent() => AvaloniaXamlLoader.Load(this);


		// INsert log property group into pattern.
		void InsertLogPropertyGroup(string propertyName)
		{
			var currentRegex = this.regexTextBox.Text ?? "";
			var selectionStart = this.regexTextBox.SelectionStart;
			var selectionEnd = this.regexTextBox.SelectionEnd;
			var newRegex = new StringBuilder();
			if (selectionStart > 0)
				newRegex.Append(currentRegex.Substring(0, selectionStart));
			newRegex.Append("(?<");
			newRegex.Append(propertyName);
			newRegex.Append(">)");
			newRegex.Append(currentRegex.Substring(selectionEnd));
			selectionStart += propertyName.Length + 4;
			this.regexTextBox.Text = newRegex.ToString();
			this.regexTextBox.SelectionStart = selectionStart;
			this.regexTextBox.SelectionEnd = selectionStart;
			this.SynchronizationContext.Post(this.regexTextBox.Focus);
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
			if (logPattern != null)
			{
				this.regexTextBox.Regex = logPattern.Regex;
				this.repeatableSwitch.IsChecked = logPattern.IsRepeatable;
				this.skippableSwitch.IsChecked = logPattern.IsSkippable;
			}
			this.regexTextBox.Focus();
		}


		// Called when property of regex text box changed.
		void OnRegexTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.IsTextValidProperty || e.Property == RegexTextBox.RegexProperty)
				this.InvalidateInput();
		}


		// Validate input.
		protected override bool OnValidateInput()
		{
			return base.OnValidateInput() && this.regexTextBox.IsTextValid && this.regexTextBox.Regex != null;
		}


		// Show menu to add log property group.
		void ShowAddLogPropertyGroupMenu()
		{
			if (this.addLogPropertyGroupMenu.PlacementTarget == null)
				this.addLogPropertyGroupMenu.PlacementTarget = this.addLogPropertyGroupButton;
			this.addLogPropertyGroupMenu.Open(this);
		}
	}
}
