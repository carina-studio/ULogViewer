using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;
using System;

namespace CarinaStudio.ULogViewer.Controls
{
    /// <summary>
    /// Time picker.
    /// </summary>
    class TimePicker : Avalonia.Controls.TimePicker, IStyleable
    {
        // Apply template.
        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            // call base.
            base.OnApplyTemplate(e);

            // setup minute text
            e.NameScope.Find<TextBlock>("MinuteTextBlock")?.Also(it =>
            {
                it.Text = App.Current.GetString("TimePicker.Minute");
                it.VerticalAlignment = VerticalAlignment.Center;
                it.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBlock.TextProperty)
                    {
                        it.Text = this.SelectedTime?.Let(it=>
                        {
                            return App.Current.GetFormattedString("TimePicker.MinuteFormat", it.Minutes);
                        }) ?? App.Current.GetString("TimePicker.Minute");
                    }
                };
            });

            // setup hour text
            e.NameScope.Find<TextBlock>("HourTextBlock")?.Also(it =>
            {
                it.Text = App.Current.GetString("TimePicker.Hour");
                it.PropertyChanged += (_, e) =>
                {
                    if (e.Property == TextBlock.TextProperty)
                    {
                        it.Text = this.SelectedTime?.Let(it =>
                        {
                            var hours = it.Hours;
                            if (!this.ClockIdentifier.StartsWith("24"))
                            {
                                hours %= 12;
                                if (hours == 0)
                                    hours = 12;
                            }
                            return App.Current.GetFormattedString("TimePicker.HourFormat", hours);
                        }) ?? App.Current.GetString("TimePicker.Hour");
                    }
                };
            });
        }


        // Style key.
        Type IStyleable.StyleKey { get; } = typeof(Avalonia.Controls.TimePicker);
    }
}
