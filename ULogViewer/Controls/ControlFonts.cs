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
        this.LogFontSize = Math.Max(Math.Min(SettingKeys.MaxLogFontSize, settings.GetValueOrDefault(SettingKeys.LogFontSize)), SettingKeys.MinLogFontSize);
        this.UpdateLogFontFamily(false);
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


    // Font family of log.
    public FontFamily LogFontFamily { get; private set; }


    // Get font size of log.
	public double LogFontSize { get; private set; }


    // Called when setting changed.
    void OnSettingChanged(object? sender, SettingChangedEventArgs e)
    {
        var key = e.Key;
        if (key == SettingKeys.LogFontFamily)
            this.UpdateLogFontFamily(true);
        else if (key == SettingKeys.LogFontSize)
        {
            this.LogFontSize = Math.Max(Math.Min(SettingKeys.MaxLogFontSize, (int)e.Value), SettingKeys.MinLogFontSize);
            this.PropertyChanged?.Invoke(this, new(nameof(LogFontSize)));
        }
        else if (key == SettingKeys.PatternFontFamily)
            this.UpdatePatternFontFamily(true);
    }


    // Font family of pattern.
    public FontFamily PatternFontFamily { get; private set; }


    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;


    // Update log font.
    [MemberNotNull(nameof(LogFontFamily))]
    void UpdateLogFontFamily(bool notifyPropertyChanged)
    {
        this.LogFontFamily = this.Application.Settings.GetValueOrDefault(SettingKeys.LogFontFamily).Let(it =>
        {
            if (string.IsNullOrEmpty(it))
                it = SettingKeys.DefaultLogFontFamily;
            return BuiltInFonts.FontFamilies.FirstOrDefault(font => font.FamilyNames.Contains(it)) ?? new(it);
        });
        if (notifyPropertyChanged)
            this.PropertyChanged?.Invoke(this, new(nameof(LogFontFamily)));
    }


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
