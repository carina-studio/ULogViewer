using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
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
using CarinaStudio.ULogViewer.Net;
using CarinaStudio.ULogViewer.ViewModels;
using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// Dialog to show message of log.
/// </summary>
partial class LogStringPropertyDialog : AppSuite.Controls.Dialog<IULogViewerApplication>
{
	// Static fields.
	static readonly StyledProperty<bool> CanSearchOnInternetProperty = AvaloniaProperty.Register<LogStringPropertyDialog, bool>("CanSearchOnInternet", false);
	static readonly StyledProperty<string> LogPropertyDisplayNameProperty = AvaloniaProperty.Register<LogStringPropertyDialog, string>(nameof(LogPropertyDisplayName), "");


	// Fields.
	readonly RegexTextBox findTextTextBox;
	readonly SyntaxHighlightingTextBox propertyValueTextBox;
	readonly SyntaxHighlightingToken propertyValueHighlightingToken = new();
	readonly ScheduledAction updateCanSearchOnInternetAction;


	/// <summary>
	/// Initialize new <see cref="LogStringPropertyDialog"/> instance.
	/// </summary>
	public LogStringPropertyDialog()
	{
		AvaloniaXamlLoader.Load(this);
		this.findTextTextBox = this.Get<RegexTextBox>(nameof(findTextTextBox));
		this.propertyValueTextBox = this.Get<SyntaxHighlightingTextBox>(nameof(propertyValueTextBox)).Also(it =>
		{
			it.GetObservable(BoundsProperty).Subscribe(_ =>
				it.Margin = default);
			it.GetObservable(TextBox.SelectionStartProperty).Subscribe(() => this.updateCanSearchOnInternetAction?.Schedule());
			it.GetObservable(TextBox.SelectionEndProperty).Subscribe(() => this.updateCanSearchOnInternetAction?.Schedule());
			it.DefinitionSet = new SyntaxHighlightingDefinitionSet("Found text").Also(definitionSet =>
			{
				this.propertyValueHighlightingToken.Background = this.FindResourceOrDefault<IBrush>("Brush/LogStringPropertyDialog.FoundText.Background", Brushes.LightGray);
				this.propertyValueHighlightingToken.Foreground = this.FindResourceOrDefault<IBrush>("Brush/LogStringPropertyDialog.FoundText.Foreground", Brushes.Red);
				definitionSet.TokenDefinitions.Add(this.propertyValueHighlightingToken);
			});
			(it.ContextMenu?.Items as IList)?.Let(items =>
			{
				foreach (var item in items)
				{
					if (item is MenuItem menuItem && menuItem.Name == "searchOnInternetMenuItem")
					{
						var providers = SearchProviderManager.Default.Providers;
						var subItems = new MenuItem[providers.Count];
						for (var i = 0; i < providers.Count; ++i)
						{
							var provider = providers[i];
							var commandBindingToken = default(IDisposable);
							var headerBindingToken = default(IDisposable);
							var iconBindingToken = default(IDisposable);
							subItems[i] = new MenuItem().Also(subItem =>
							{
								var iconImage = new Avalonia.Controls.Image().Also(it =>
								{
									it.Classes.Add("MenuItem_Icon");
								});
								subItem.CommandParameter = provider;
								subItem.Icon = iconImage;
								subItem.AttachedToLogicalTree += (_, _) =>
								{
									commandBindingToken = subItem.Bind(MenuItem.CommandProperty, new Binding { Path = nameof(SearchOnInternet), Source = this });
									headerBindingToken = subItem.Bind(HeaderedSelectingItemsControl.HeaderProperty, new Binding { Path = nameof(SearchProvider.Name), Source = provider });
									iconBindingToken = iconImage.Bind(Avalonia.Controls.Image.SourceProperty, new Binding { Source = provider, Converter = SearchProviderIconConverters.Default });
								};
								subItem.DetachedFromLogicalTree += (_, _) =>
								{
									commandBindingToken = commandBindingToken.DisposeAndReturnNull();
									headerBindingToken = headerBindingToken.DisposeAndReturnNull();
									iconBindingToken = iconBindingToken.DisposeAndReturnNull();
								};
							});
						}
						menuItem.ItemsSource = subItems;
					}
				}
			});
		});
		this.updateCanSearchOnInternetAction = new(this.UpdateCanSearchOnInternet);
		this.Get<CheckBox>("wrapTextCheckBox").Also(it =>
		{
			it.IsCheckedChanged += (_, _) =>
			{
				var isChecked = it.IsChecked.GetValueOrDefault();
				ScrollViewer.SetHorizontalScrollBarVisibility(this.propertyValueTextBox, isChecked ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
				this.propertyValueTextBox.Margin = new(1);
				this.propertyValueTextBox.TextWrapping = isChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
			};
		});
		this.SetValue(LogPropertyDisplayNameProperty, LogPropertyNameConverter.Default.Convert(nameof(Log.Message)));
		this.AddHandler(KeyDownEvent, (_, e) =>
		{
			if (e.Key == Key.F)
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


	// Generate regex to split text into keywords.
	[GeneratedRegex(@"\s+")]
	private static partial Regex CreateTextSplitterRegex();


	/// <summary>
	/// Get or set log to show message.
	/// </summary>
	public DisplayableLog? Log { get; init; }


	/// <summary>
	/// Get or set display name of log property.
	/// </summary>
	public string LogPropertyDisplayName
	{
		get => this.GetValue(LogPropertyDisplayNameProperty);
		set => this.SetValue(LogPropertyDisplayNameProperty, value);
	}


	/// <summary>
	/// Get or set name of log property.
	/// </summary>
	public string LogPropertyName { get; init; } = nameof(Logs.Log.Message);


	/// <inheritdoc/>
	protected override void OnClosed(EventArgs e)
	{
		this.Settings.SettingChanged -= this.OnSettingChanged;
		base.OnClosed(e);
	}


	// Called when property of find text box changed.
	void OnFindTextTextBoxPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
	{
		if (e.Property == RegexTextBox.ObjectProperty)
		{
			this.propertyValueHighlightingToken.Pattern = this.findTextTextBox.Object;
			this.propertyValueTextBox.Margin = new(1);
		}
	}


	/// <inheritdoc/>
	protected override void OnOpened(EventArgs e)
	{
		base.OnOpened(e);
		this.SynchronizationContext.Post(() => this.findTextTextBox.Focus());
	}


	/// <inheritdoc/>
	protected override void OnOpening(EventArgs e)
	{
		base.OnOpening(e);
		this.propertyValueTextBox.Text = this.LogPropertyName.Let(_ =>
		{
			var value = "";
			this.Log?.TryGetProperty(this.LogPropertyName, out value);
			return value;
		});
	}


	// Called when setting changed.
	void OnSettingChanged(object? sender, SettingChangedEventArgs e)
	{
		if (e.Key == SettingKeys.LogFontFamily)
			this.UpdateLogFontFamily();
	}


	/// <summary>
	/// Search selected text on the internet.
	/// </summary>
	/// <param name="parameter">Search provider.</param>
	public void SearchOnInternet(object? parameter)
	{
		if (parameter is not SearchProvider provider)
			return;
		var text = this.propertyValueTextBox.SelectedText.Trim();
		if (string.IsNullOrEmpty(text))
			return;
		var keywords = CreateTextSplitterRegex().Split(text);
		if (provider.TryCreateSearchUri(keywords, out var uri))
			Platform.OpenLink(uri);
	}


	// Check whether searching on the Internet is available or not.
	void UpdateCanSearchOnInternet() =>
		this.SetValue(CanSearchOnInternetProperty, !string.IsNullOrWhiteSpace(this.propertyValueTextBox.SelectedText));


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