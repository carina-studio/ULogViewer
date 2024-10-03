# ULogViewer 
[![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/releases/tag/3.0.13.128) ![](https://img.shields.io/github/downloads/carina-studio/ULogViewer/total) [![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/commits/master) [![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading, parsing and analysing various type of logs. Please visit the [Website](https://carinastudio.azurewebsites.net/ULogViewer/) for more details.

ULogViewer 是一個由 C# 撰寫並基於 [.NET](https://dotnet.microsoft.com/) 的跨平台通用日誌檢視器，支援多種形式之日誌讀取、解析與分析。請參閱 [網站](https://carinastudio.azurewebsites.net/ULogViewer/) 以取得更多資訊。

<img alt="ULogViewer" src="https://carinastudio.azurewebsites.net/ULogViewer/Banner.png"/>

## 📥 Download

### 4.1.0.1003 Preview
[![](https://img.shields.io/badge/Windows-x64-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-win-x64.zip)
[![](https://img.shields.io/badge/Windows-arm64-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-win-arm64.zip)
[![](https://img.shields.io/badge/Windows-x86-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-win-x86.zip)

[![](https://img.shields.io/badge/macOS-arm64%20(Apple%20Silicon)-blueviolet?style=flat-square&logo=apple&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-osx-arm64.zip)
[![](https://img.shields.io/badge/macOS-x64-blueviolet?style=flat-square&logo=apple&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-osx-x64.zip)

[![](https://img.shields.io/badge/Linux-x64-orange?style=flat-square&logo=linux&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-linux-x64.zip)
[![](https://img.shields.io/badge/Linux-arm64-orange?style=flat-square&logo=linux&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.1.0.1003/ULogViewer-4.1.0.1003-linux-arm64.zip)

### 4.0.13.1002
[![](https://img.shields.io/badge/Windows-x64-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-win-x64.zip)
[![](https://img.shields.io/badge/Windows-arm64-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-win-arm64.zip)
[![](https://img.shields.io/badge/Windows-x86-blue?style=flat-square&logo=windows&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-win-x86.zip)

[![](https://img.shields.io/badge/macOS-arm64%20(Apple%20Silicon)-blueviolet?style=flat-square&logo=apple&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-osx-arm64.zip)
[![](https://img.shields.io/badge/macOS-x64-blueviolet?style=flat-square&logo=apple&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-osx-x64.zip)

[![](https://img.shields.io/badge/Linux-x64-orange?style=flat-square&logo=linux&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-linux-x64.zip)
[![](https://img.shields.io/badge/Linux-arm64-orange?style=flat-square&logo=linux&logoColor=fff)](https://github.com/carina-studio/ULogViewer/releases/download/4.0.13.1002/ULogViewer-4.0.13.1002-linux-arm64.zip)

## 📣 What's Change in 4.1 Preview
- Support setting text filter as ```exclusionary``` to filter logs **without** text matched by the pattern.
- Support formatting [CLEF](https://clef-json.org/) data when reading raw logs through ```Standard output (stdout)```, ```File``` and ```HTTP```.
- Add ```CLEF Files``` built-in log profile.
- Support specifying default log level in case of value of raw log level cannot be mapped or not presented.
- Add ```Error```, ```Exception``` and ```Warning``` log properties.
- Use different colors for different log levels.
- Add more icons for log profile.

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
