using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Styling;
using CarinaStudio.AppSuite.Converters;
using CarinaStudio.ULogViewer.Converters;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ComboBox"/> to select <see cref="LogProfileIcon"/>.
/// </summary>
class LogProfileIconComboBox : ComboBox, IStyleable
{
    /// <summary>
    /// Property of <see cref="IconColor"/>.
    /// </summary>
    public static readonly StyledProperty<LogProfileIconColor> IconColorProperty = AvaloniaProperty.Register<LogProfileIconComboBox, LogProfileIconColor>(nameof(IconColor), LogProfileIconColor.Default);


    // Static fields.
    static readonly EnumConverter LogProfileIconNameConverter = new(App.CurrentOrNull, typeof(LogProfileIcon));


    // Fields.
    readonly DataTemplate dataTemplate;


    /// <summary>
    /// Initialize new <see cref="LogProfileIconComboBox"/>.
    /// </summary>
    public LogProfileIconComboBox()
    {
        this.dataTemplate = new DataTemplate()
        {
            Content = new Func<IServiceProvider, object>(_ =>
            {
                var rootPanel = new Grid().Also(rootPanel =>
                {
                    rootPanel.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Auto));
                    rootPanel.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                });
                new Panel().Also(iconPanel =>
                {
                    iconPanel.Classes.Add("ComboBoxItem_Icon");
                    var isSelectedObserverToken = EmptyDisposable.Default;
                    var selectedIcon = new Image().Also(image =>
                    {
                        image.Classes.Add("Icon");
                        image.AttachedToLogicalTree += (_, e) =>
                        {
                            var comboBoxItem = image.FindLogicalAncestorOfType<ComboBoxItem>();
                            if (comboBoxItem != null)
                            {
                                isSelectedObserverToken = comboBoxItem.GetObservable(ComboBoxItem.IsSelectedProperty).Subscribe(isSelected =>
                                    image.IsVisible = isSelected);
                            }
                            else
                                image.IsVisible = false;
                        };
                        image.DetachedFromLogicalTree += (_, e) =>
                        {
                            isSelectedObserverToken.Dispose();
                        };
                        image.Bind(Image.SourceProperty, new Binding()
                        {
                            Converter = LogProfileIconConverter.Default,
                            ConverterParameter = "Light",
                        });
                    });
                    var icon = new Image().Also(image =>
                    {
                        image.Classes.Add("Icon");
                        image.Bind(Image.SourceProperty, new Binding() 
                        { 
                            Converter = LogProfileIconConverter.Default,
                            ConverterParameter = this.GetValue<LogProfileIconColor>(IconColorProperty),
                        });
                    });
                    selectedIcon.GetObservable(Image.IsVisibleProperty).Subscribe(isVisible =>
                        icon.IsVisible = !isVisible);
                    iconPanel.Children.Add(icon);
                    iconPanel.Children.Add(selectedIcon);
                    rootPanel.Children.Add(iconPanel);
                });
                new TextBlock().Also(textBlock =>
                {
                    textBlock.Classes.Add("ComboBoxItem_TextBlock");
                    textBlock.Bind(TextBlock.TextProperty, new Binding() { Converter = LogProfileIconNameConverter });
                    Grid.SetColumn(textBlock, 1);
                    rootPanel.Children.Add(textBlock);
                });
				return new ControlTemplateResult(rootPanel, this.FindNameScope().AsNonNull());
            }),
            DataType = typeof(LogProfileIcon),
        };
        this.DataTemplates.Add(this.dataTemplate);
        this.GetObservable(IconColorProperty).Subscribe(_ =>
        {
            this.DataTemplates.Remove(this.dataTemplate);
            this.DataTemplates.Add(this.dataTemplate);
            var selectedIndex = this.SelectedIndex;
            if (selectedIndex >= 0)
            {
                this.SelectedIndex = -1;
                this.SelectedIndex = selectedIndex;
            }
        });
        base.Items = Enum.GetValues<LogProfileIcon>();
        this.SelectedIndex = 0;
    }


    /// <summary>
    /// Get or set color of icon.
    /// </summary>
    public LogProfileIconColor IconColor
    {
        get => this.GetValue<LogProfileIconColor>(IconColorProperty);
        set => this.SetValue<LogProfileIconColor>(IconColorProperty, value);
    }


    /// <summary>
    /// Get items.
    /// </summary>
    public new object? Items { get => base.Items; }


    /// <summary>
    /// Get or set selected <see cref="LogProfileIcon"/>.
    /// </summary>
    /// <value></value>
    public new LogProfileIcon? SelectedItem
    {
        get => (LogProfileIcon?)base.SelectedItem;
        set => base.SelectedItem = value;
    }


    /// <inheritdoc/>
    Type IStyleable.StyleKey => typeof(ComboBox);
}