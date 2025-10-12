using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.LogicalTree;
using CarinaStudio.AppSuite;
using CarinaStudio.Threading;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CarinaStudio.ULogViewer.Controls;

/// <summary>
/// <see cref="ComboBox"/> let user select a culture info.
/// </summary>
public class CultureInfoComboBox : ComboBox
{
    // Constants.
    const int ClearSearchTextTimeout = 1000;
    
    
    // Static fields.
    static readonly IValueConverter Converter = new FuncValueConverter<CultureInfo, string>(cultureInfo => $"{cultureInfo!.Name} ({cultureInfo.DisplayName})");
    
    
    // Fields.
    ScheduledAction? clearSearchTextAction;
    StringBuilder? searchTextBuffer;


    /// <summary>
    /// Initialize new <see cref="CultureInfoComboBox"/> instance.
    /// </summary>
    public CultureInfoComboBox()
    {
        this.ItemsSource = CultureInfos;
        this.DropDownClosed += (_, _) => this.clearSearchTextAction?.Execute();
        this.UpdateItemTemplate();
    }
    
    
    /// <summary>
    /// List of culture infos to be listed in the control.
    /// </summary>
    public static IReadOnlyList<CultureInfo> CultureInfos { get; } = CultureInfo.GetCultures(CultureTypes.SpecificCultures).Also(it =>
    {
        Array.Sort(it, (lhs, rhs) => string.CompareOrdinal(lhs.Name, rhs.Name));
    }).AsReadOnly();


    /// <inheritdoc/>
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        IAppSuiteApplication.Current.StringsUpdated += this.OnStringsUpdated;
    }


    /// <inheritdoc/>
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        IAppSuiteApplication.Current.StringsUpdated -= this.OnStringsUpdated;
        base.OnDetachedFromLogicalTree(e);
    }


    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // call base
        base.OnKeyDown(e);
        
        // search text
        if (this.IsDropDownOpen)
        {
            var symbol = e.KeySymbol?.Trim();
            if (symbol is not null && symbol.Length == 1)
            {
                var c = symbol[0];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '-')
                {
                    this.searchTextBuffer ??= new();
                    if (this.searchTextBuffer.Length < 16)
                        this.searchTextBuffer.Append(c);
                    this.Search();
                }
                else if (c == '\b' && this.searchTextBuffer is not null && this.searchTextBuffer.Length > 0)
                {
                    this.searchTextBuffer.Remove(this.searchTextBuffer.Length - 1, 1);
                    this.Search();
                }
            }
        }
    }


    // Called when application strings updated.
    void OnStringsUpdated(object? sender, EventArgs e) => this.UpdateItemTemplate();
    
    
    // Search culture info.
    void Search()
    {
        if (this.searchTextBuffer is null || this.searchTextBuffer.Length == 0)
            return;
        var prefix = this.searchTextBuffer.ToString();
        for (var i = 0; i < CultureInfos.Count; ++i)
        {
            if (CultureInfos[i].Name.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                this.SelectedIndex = i;
                break;
            }
        }
        this.clearSearchTextAction ??= new(() => this.searchTextBuffer?.Clear());
        this.clearSearchTextAction.Reschedule(ClearSearchTextTimeout);
    }


    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ComboBox);
    
    
    // Update item template.
    void UpdateItemTemplate()
    {
        this.ItemTemplate = new FuncDataTemplate(typeof(CultureInfo), (_, _) =>
        {
            return new TextBlock().Also(it =>
            {
#pragma warning disable IL2026
                it.Bind(TextBlock.TextProperty, new Binding { Converter = Converter });
#pragma warning restore IL2026
                it.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
            });
        }, true);
    }
}