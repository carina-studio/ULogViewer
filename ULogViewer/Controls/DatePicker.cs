using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Date picker.
    /// </summary>
    class DatePicker : Avalonia.Controls.DatePicker, IStyleable
    {
        // Apply template.
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // call base.
            base.OnApplyTemplate(e);

            // setup day text
            e.NameScope.Find<TextBlock>("DayText")?.Also(it =>
            {
                it.Text = App.Current.GetString("DatePicker.Day");
                it.VerticalAlignment = VerticalAlignment.Center;
                it.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBlock.TextProperty && (e.NewValue as string) == "day")
                    {
                        var text = App.Current.GetString("DatePicker.Day");
                        if (text != "day")
                            it.Text = text;
                    }
                };
            });

            // setup month text
            e.NameScope.Find<TextBlock>("MonthText")?.Also(it =>
            {
                it.Text = App.Current.GetString("DatePicker.Month");
                it.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBlock.TextProperty && (e.NewValue as string) == "month")
                    {
                        var text = App.Current.GetString("DatePicker.Month");
                        if (text != "day")
                            it.Text = text;
                    }
                };
            });

            // setup year text
            e.NameScope.Find<TextBlock>("YearText")?.Also(it =>
            {
                it.Text = App.Current.GetString("DatePicker.Year");
                it.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBlock.TextProperty && (e.NewValue as string) == "year")
                    {
                        var text = App.Current.GetString("DatePicker.Year");
                        if (text != "day")
                            it.Text = text;
                    }
                };
            });
        }


        // Style key.
        Type IStyleable.StyleKey { get; } = typeof(Avalonia.Controls.DatePicker);
    }
}
