# ULogViewer
[![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/releases/tag/3.0.13.128) ![](https://img.shields.io/github/downloads/carina-studio/ULogViewer/total) [![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/commits/master) [![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

A cross-platform log viewer that supports reading, parsing, and analyzing various types of logs. Please visit the [Website](https://carinastudio.azurewebsites.net/ULogViewer/) for more details.

跨平台日誌檢視器，支援多種形式之日誌讀取、解析與分析。請參閱 [網站](https://carinastudio.azurewebsites.net/ULogViewer/) 以取得更多資訊。

![](https://carinastudio.azurewebsites.net/ULogViewer/Banner.png?v=2)

## 📥 Download 2026.0 RC

### Windows
[![](https://img.shields.io/badge/x64-blue?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-win-x64.zip)
[![](https://img.shields.io/badge/x86-blue?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-win-x86.zip)
[![](https://img.shields.io/badge/arm64-blue?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-win-arm64.zip)

### macOS
[![](https://img.shields.io/badge/Apple%20Silicon%20(arm64)-blueviolet?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-osx-arm64.zip)
[![](https://img.shields.io/badge/x64-blueviolet?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-osx-x64.zip)

### Linux
[![](https://img.shields.io/badge/x64-orange?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-linux-x64.zip)
[![](https://img.shields.io/badge/arm64-orange?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/2026.0.3/ULogViewer-2026.0.3-linux-arm64.zip)

## 📥 Download 4.1.7.411

### Windows
[![](https://img.shields.io/badge/x64-blue?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-win-x64.zip)
[![](https://img.shields.io/badge/x86-blue?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-win-x86.zip)
[![](https://img.shields.io/badge/arm64-blue?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-win-arm64.zip)

### macOS
[![](https://img.shields.io/badge/Apple%20Silicon%20(arm64)-blueviolet?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-osx-arm64.zip)
[![](https://img.shields.io/badge/x64-blueviolet?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-osx-x64.zip)

### Linux
[![](https://img.shields.io/badge/x64-orange?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-linux-x64.zip)
[![](https://img.shields.io/badge/arm64-orange?style=for-the-badge)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.7.411/ULogViewer-4.1.7.411-linux-arm64.zip)

## 📣 What's Change in 2026.0 RC
- Support filtering log profiles in the `Select log profile` dialog.
- Support temporarily showing raw log lines even when [log patterns](https://carinastudio.azurewebsites.net//ULogViewer/HowToReadAndParseLogs#LogPatterns) are defined.
- Support filtering logs by multiple log levels.
- Support editing visible log properties of the current non-built-in log profile in the log viewer.
- Support importing application data from an existing ULogViewer instance.
- Support using `C# 14` as scripting language.
- Improve user experience of the `Select log profile` dialog and log chart.

[Know more about](https://carinastudio.azurewebsites.net/ULogViewer/ChangeList#PreviewChangeList)

## 📣 What's Change in 4.1
- Support setting text filter as `exclusionary` to filter logs **without** text matched by the pattern.
- Support formatting [CLEF](https://clef-json.org/) data when reading raw logs through `Standard output (stdout)`, `File` and `HTTP`.
- Add `CLEF Files` built-in log profile.
- Support specifying default log level in case of value of raw log level cannot be mapped or not presented.
- Add `Error`, `Exception` and `Warning` log properties.
- Use different colors for different log levels.
- Add more icons for log profile.
- Allow specifying locale of time span/timestamp for log reading and writing.

[Know more about](https://carinastudio.azurewebsites.net/ULogViewer/ChangeList#StableChangeList)

## 🤝 Dependencies
- [.NET](https://dotnet.microsoft.com/)
- [AppBase](https://github.com/carina-studio/AppBase)
- [AppSuiteBase](https://github.com/carina-studio/AppSuiteBase)
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)
- [Avalonia XAML Behaviors](https://github.com/wieslawsoltes/AvaloniaBehaviors)
- [evtx](https://github.com/EricZimmerman/evtx)
- [IronPython 3](https://github.com/IronLanguages/ironpython3)
- [Jint](https://github.com/sebastienros/jint)
- [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2)
- [MySqlConnector](https://github.com/mysql-net/MySqlConnector)
- [NLog](https://github.com/NLog/NLog)
- [NUnit](https://github.com/nunit/nunit)
- [Roslyn](https://github.com/dotnet/roslyn)
- [System.Data.SQLite](https://system.data.sqlite.org/)
