using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CarinaStudio.AppSuite.Controls;
using CarinaStudio.AppSuite.Controls.Highlighting;
using CarinaStudio.AppSuite.Media;
using CarinaStudio.Configuration;
using CarinaStudio.Controls;
using CarinaStudio.Threading;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.ViewModels;
using CarinaStudio.Windows.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace CarinaStudio.ULogViewer.Controls
{
	/// <summary>
	/// Dialog to show message of log.
	/// </summary>
	partial class LogStringPropertyDialog : AppSuite.Controls.Dialog<IULogViewerApplication>
	{
		// Static fields.
		static readonly StyledProperty<bool> IsPropertyValueTextEditorFocusedProperty = AvaloniaProperty.Register<LogStringPropertyDialog, bool>(nameof(IsPropertyValueTextEditorFocused), false);
		static readonly StyledProperty<string> LogPropertyDisplayNameProperty = AvaloniaProperty.Register<LogStringPropertyDialog, string>(nameof(LogPropertyDisplayName), "");


		// Fields.
		readonly RegexTextBox findTextTextBox;
		readonly SyntaxHighlightingTextBox propertyValueTextBox;
		readonly SyntaxHighlightingToken propertyValueHighlightingToken = new();


		/// <summary>
		/// Initialize new <see cref="LogStringPropertyDialog"/> instance.
		/// </summary>
		public LogStringPropertyDialog()
		{
			this.SetTextWrappingCommand = new Command<bool>(this.SetTextWrapping);
			AvaloniaXamlLoader.Load(this);
			this.findTextTextBox = this.Get<RegexTextBox>(nameof(findTextTextBox));
			this.propertyValueTextBox = this.Get<SyntaxHighlightingTextBox>(nameof(propertyValueTextBox)).Also(it =>
			{
				it.GetObservable(BoundsProperty).Subscribe(_ =>
					it.Margin = default);
				it.DefinitionSet = new SyntaxHighlightingDefinitionSet("Found text").Also(definitionSet =>
				{
					this.propertyValueHighlightingToken.Background = this.FindResourceOrDefault<IBrush>("Brush/LogStringPropertyDialog.FoundText.Background", Brushes.LightGray);
					this.propertyValueHighlightingToken.Foreground = this.FindResourceOrDefault<IBrush>("Brush/LogStringPropertyDialog.FoundText.Foreground", Brushes.Red);
					definitionSet.TokenDefinitions.Add(this.propertyValueHighlightingToken);
				});
			});
			this.SetValue(LogPropertyDisplayNameProperty, LogPropertyNameConverter.Default.Convert(nameof(Log.Message)));
			this.AddHandler(KeyDownEvent, (_, e) =>
			{
				if (e.Key == Avalonia.Input.Key.F)
				{
					var modifier = Platform.IsMacOS ? KeyModifiers.Meta : KeyModifiers.Control;
					if ((e.KeyModifiers & modifier) != 0)
					{
						this.findTextTextBox.Focus();
						this.findTextTextBox.SelectAll();
						e.Handled = true;
					}
				}
			}, Avalonia.Interactivity.RoutingStrategies.Tunnel);
			this.Settings.SettingChanged += this.OnSettingChanged;
			this.ApplySystemAccentColor();
			this.UpdateLogFontFamily();
		}


		// Apply system accent color.
		void ApplySystemAccentColor()
        {
			var app = (App)this.Application;
			if (!app.TryFindResource("SystemAccentColor", out var res) || res is not Color accentColor)
				return;
			this.Resources["Brush/TextArea.Selection.Background"] = new SolidColorBrush(Color.FromArgb(0x70, accentColor.R, accentColor.G, accentColor.B));
        }


		// Check whether propertyValueTextEditor is focused or not.
		bool IsPropertyValueTextEditorFocused { get => this.GetValue<bool>(IsPropertyValueTextEditorFocusedProperty); }


		/// <summary>
		/// Get or set log to show message.
		/// </summary>
		public DisplayableLog? Log { get; set; }


		/// <summary>
		/// Get or set display name of log property.
		/// </summary>
		public string LogPropertyDisplayName
		{
			get => this.GetValue<string>(LogPropertyDisplayNameProperty);
			set => this.SetValue<string>(LogPropertyDisplayNameProperty, value);
		}


		/// <summary>
		/// Get or set name of log property.
		/// </summary>
		public string LogPropertyName { get; set; } = nameof(Logs.Log.Message);


		/// <inheritdoc/>
		protected override void OnClosed(EventArgs e)
		{
			this.Settings.SettingChanged -= this.OnSettingChanged;
			base.OnClosed(e);
		}


		// Called when property of find text text box changed.
		void OnFindTextTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == RegexTextBox.ObjectProperty)
			{
				this.propertyValueHighlightingToken.Pattern = this.findTextTextBox.Object;
				this.propertyValueTextBox.Margin = new(1);
			}
		}


		// Called when opened.
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			this.propertyValueTextBox.Text = this.LogPropertyName.Let(propertyName =>
			{
				var value = "";
				this.Log?.TryGetProperty(this.LogPropertyName, out value);
				return value;
			});
			this.SynchronizationContext.Post(this.findTextTextBox.Focus);

			// [Workaround] Reduce possibility of showing with min height on Linux.
			if (Platform.IsLinux)
			{
				var height = this.FindResourceOrDefault<double>("Double/LogStringPropertyDialog.Height", 500);
				this.SynchronizationContext.PostDelayed(() => this.Height = height, 50);
				this.SynchronizationContext.PostDelayed(() => this.Height = height, 150);
			}
		}


		// Called when setting changed.
		void OnSettingChanged(object? sender, SettingChangedEventArgs e)
		{
			if (e.Key == SettingKeys.LogFontFamily)
				this.UpdateLogFontFamily();
		}


        // set text wrapping.
        void SetTextWrapping(bool wrap)
		{
			if (this.propertyValueTextBox == null)
				return;
			ScrollViewer.SetHorizontalScrollBarVisibility(this.propertyValueTextBox, wrap ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
			this.propertyValueTextBox.Margin = new(1);
			this.propertyValueTextBox.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
		}


		// Command to set text wrapping.
		public ICommand SetTextWrappingCommand { get; }


		// Update font family of log.
		void UpdateLogFontFamily()
		{
			var name = this.Settings.GetValueOrDefault(SettingKeys.LogFontFamily);
			if (string.IsNullOrEmpty(name))
				name = SettingKeys.DefaultLogFontFamily;
			var builtInFontFamily = BuiltInFonts.FontFamilies.FirstOrDefault(it => it.FamilyNames.Contains(name));
			this.propertyValueTextBox.FontFamily = builtInFontFamily ?? new FontFamily(name);
		}
	}
}
