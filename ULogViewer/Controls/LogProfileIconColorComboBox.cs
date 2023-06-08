using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using CarinaStudio.AppSuite.Converters;
using CarinaStudio.Controls;
using CarinaStudio.ULogViewer.Logs.Profiles;
using System;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ComboBox"/> to select <see cref="LogProfileIconColor"/>.
/// </summary>
class LogProfileIconColorComboBox : ComboBox
{
    // Static fields.
    static readonly IValueConverter BackgroundConverter = new FuncValueConverter<LogProfileIconColor, IBrush?>(color =>
    {
        if (color == LogProfileIconColor.Default)
            return App.Current.FindResourceOrDefault<IBrush?>("ComboBoxItemForeground");
        return App.Current.FindResourceOrDefault<IBrush?>($"Brush/LogProfileIconColor.{color}");
    });
    static readonly IValueConverter NameConverter = new EnumConverter(App.CurrentOrNull, typeof(LogProfileIconColor));


    /// <summary>
    /// Initialize new <see cref="LogProfileIconColorComboBox"/>.
    /// </summary>
    public LogProfileIconColorComboBox()
    {
        this.DataTemplates.Add(new FuncDataTemplate(typeof(LogProfileIconColor), (_, _) =>
        {
            var rootPanel = new Grid().Also(rootPanel =>
            {
                rootPanel.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Auto));
                rootPanel.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            });
            new Border().Also(border =>
            {
                border.Classes.Add("ComboBoxItem_Icon");
                border.Bind(Border.BackgroundProperty, new Binding { Converter = BackgroundConverter });
                border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("ComboBoxItemForeground"));
                border.Bind(Border.BorderThicknessProperty, this.GetResourceObservable("Thickness/LogProfileIconColorComboBox.Icon.Border"));
                border.Bind(Border.CornerRadiusProperty, this.GetResourceObservable("CornerRadius/LogProfileIconColorComboBox.Icon"));
                rootPanel.Children.Add(border);
            });
            new Avalonia.Controls.TextBlock().Also(textBlock =>
            {
                textBlock.Classes.Add("ComboBoxItem_TextBlock");
                textBlock.Bind(Avalonia.Controls.TextBlock.TextProperty, new Binding() { Converter = NameConverter });
                Grid.SetColumn(textBlock, 1);
                rootPanel.Children.Add(textBlock);
            });
            return rootPanel;
        }));
        base.ItemsSource = Enum.GetValues<LogProfileIconColor>();
        this.SelectedIndex = 0;
    }


    /// <summary>
    /// Get items.
    /// </summary>
    public new object? ItemsSource => base.Items;


    /// <summary>
    /// Get or set selected <see cref="LogProfileIconColor"/>.
    /// </summary>
    /// <value></value>
    public new LogProfileIconColor? SelectedItem
    {
        get => (LogProfileIconColor?)base.SelectedItem;
        set => base.SelectedItem = value;
    }


    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ComboBox);
}