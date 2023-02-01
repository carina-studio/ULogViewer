using Avalonia.Media;
using CarinaStudio.AppSuite.Media;
using CarinaStudio.Configuration;
#if DEBUG
using CarinaStudio.Threading;
#endif
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CarinaStudio.ULogViewer.Controls;

// Font manager for controls.
class ControlFonts : BaseApplicationObject<IULogViewerApplication>, INotifyPropertyChanged
{
    // Static fields.
    static ControlFonts? _Default;


    // Constructor.
    ControlFonts(IULogViewerApplication app) : base(app)
    {
        var settings = app.Settings;
        settings.SettingChanged += this.OnSettingChanged;
        this.UpdatePatternFontFamily(false);
    }


    // Default instance.
    public static ControlFonts Default => _Default ?? throw new InvalidOperationException();


    // Initialize.
    internal static void Initialize(IULogViewerApplication app)
    {
        if (_Default != null)
            throw new InvalidOperationException();
#if DEBUG
        app.VerifyAccess();
#endif
        _Default = new(app);
    }


    // Called when setting changed.
    void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        if (e.Key == SettingKeys.PatternFontFamily)
            this.UpdatePatternFontFamily(true);
    }


    // Font family of pattern.
    public FontFamily PatternFontFamily { get; private set; }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    // Update pattern font.
    [MemberNotNull(nameof(PatternFontFamily))]
    void UpdatePatternFontFamily(bool notifyPropertyChanged)
    {
        this.PatternFontFamily = this.Application.Settings.GetValueOrDefault(SettingKeys.PatternFontFamily).Let(it =>
        {
            if (string.IsNullOrEmpty(it))
                it = SettingKeys.DefaultPatternFontFamily;
            return BuiltInFonts.FontFamilies.FirstOrDefault(font => font.FamilyNames.Contains(it)) ?? new(it);
        });
        if (notifyPropertyChanged)
            this.PropertyChanged?.Invoke(this, new(nameof(PatternFontFamily)));
    }
}
