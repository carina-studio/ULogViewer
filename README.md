# ULogViewer 
[![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/releases/tag/3.0.8.1010) ![](https://img.shields.io/github/downloads/carina-studio/ULogViewer/total) [![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/commits/master) [![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading, parsing and analysing various type of logs. Please visit the [Website](https://carinastudio.azurewebsites.net/ULogViewer/) for more details.

ULogViewer 是一個由 C# 撰寫並基於 [.NET](https://dotnet.microsoft.com/) 的跨平台通用日誌檢視器，支援多種形式之日誌讀取、解析與分析。請參閱 [網站](https://carinastudio.azurewebsites.net/ULogViewer/) 以取得更多資訊。

<img alt="ULogViewer" src="https://carinastudio.azurewebsites.net/ULogViewer/Banner.png"/>

## 📥 Download

### 3.0.8.1010
[![](https://img.shields.io/badge/Windows-x64-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-win-x64.zip)
[![](https://img.shields.io/badge/Windows-arm64-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-win-arm64.zip)
[![](https://img.shields.io/badge/Windows-x86-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-win-x86.zip)

[![](https://img.shields.io/badge/macOS-arm64%20(M1/M2)-blueviolet?style=flat-square&logo=apple&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-osx-arm64.zip)
[![](https://img.shields.io/badge/macOS-x64-blueviolet?style=flat-square&logo=apple&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-osx-x64.zip)

[![](https://img.shields.io/badge/Linux-x64-orange?style=flat-square&logo=linux&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-linux-x64.zip)
[![](https://img.shields.io/badge/Linux-arm64-orange?style=flat-square&logo=linux&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/3.0.8.1010/ULogViewer-3.0.8.1010-linux-arm64.zip)

## 📣 What's Change in 3.0
- Support embedding log analysis script and log data source script into log profile.
- Support rendering values of log properties as chart.
- Add new ways of matching raw log lines by patterns to make log parsing more flexible.
- Allow grouping of defined text filters.
- Syntax highlighting for ```Regular Expression```, ```Date and Time Format```, ```Time Span Format```, ```Query String (SQL)``` and ```Command-Line Shell Command```.
- Highlight text sequences in logs which are matched by text filter.
- Highlight PID and TID which are same as PID and TID of selected log.
- Add ```Windows Event Log File``` log data source to support reading data from ```Windows XML Event Log``` (*.evtx) files.
- Add 10 built-in log profiles and templates.
- Support ```Python 3.4``` as script language.
- Support searching selected property of log on the Internet.
- Add more log properties.
- Allow editing pattern (Regular Expression) directly in text area.
- Select proper scale factor of screen on ```Linux``` automatically.

[Know more about](https://carinastudio.azurewebsites.net/ULogViewer/ChangeList#PreviewChangeList)

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
