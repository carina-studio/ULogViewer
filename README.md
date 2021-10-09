[![Download](https://img.shields.io/github/v/release/carina-studio/ULogViewer?include_prereleases&style=for-the-badge&color=blue&logo=Windows&label=Windows)](https://github.com/carina-studio/ULogViewer/releases/download/0.26.1.1009/ULogViewer-0.26.1.1009-win-x64.zip)
[![Download](https://img.shields.io/github/v/release/carina-studio/ULogViewer?include_prereleases&style=for-the-badge&color=blueviolet&logo=Apple&label=macOS)](https://github.com/carina-studio/ULogViewer/releases/download/0.26.1.1009/ULogViewer-0.26.1.1009-osx-x64.zip)
[![Download](https://img.shields.io/github/v/release/carina-studio/ULogViewer?include_prereleases&style=for-the-badge&color=orange&logo=Linux&logoColor=ffffff&label=Linux)](https://github.com/carina-studio/ULogViewer/releases/download/0.26.1.1009/ULogViewer-0.26.1.1009-linux-x64.zip)

[![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/releases/tag/0.26.1.1009)
[![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/commits/master)
[![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

# ULogViewer
ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading and parsing various type of logs.
The project is still under development but most of functions relate to reading/parsing/displaying logs are ready.

## 📷Screenshot
### Windows
<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows_Dark_Thumb.png" width="250"/><img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows_Light_Thumb.png" width="250"/>

### macOS
<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_OSX_Dark_Thumb.png" width="250"/><img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_OSX_Light_Thumb.png" width="250"/>

## ⭐Log data sources
- Standard Output (stdout)
- Files
- Windows Event Logs (Windows only)
- HTTP/HTTPS
- TCP (without SSL)
- SQLite

## ⭐Log profiles
Each log profile defines:
- What log data source should be used.
- How to parse log data into structured logs.
- What properties of log should be displayed in the list.
- How to output logs back to text (ex, copying).

Currently there are 7 built-in log profiles:
- Android Device Log.
- Android Log Files.
- Git Log.
- Windows Event Logs (Application/System/Secutiry/Setup).

You can also create, copy or export your own log profiles according to your requirement.

## ⭐Log filtering
Log filtering is the most important feature in ULogViewer which helps you to find and analyze the problem from logs.
You can filter logs by:
- Text filter described by regular expression.
- Level(Priority) of log.
- Process ID of log if available.
- Thread ID of log if available.

For text filter, you can also predefine some filters you may use frequently and filter logs by cobination of these text filters.

## ⭐Log marking
When viewing logs, you can mark some logs which are important for you. There is a separated view to list all marked logs to help you to jump to marked log quickly.
Marked logs will be kept if you are viewing logs from files so that you don't need to mark them again when you open log files next time.

## 📔Topics
- [How to Install and Upgrade ULogViewer](https://carina-studio.github.io/ULogViewer/installation_and_upgrade.html)
- [How Does ULogViewer Read and Parse Logs](https://carina-studio.github.io/ULogViewer/logs_reading_flow.html)

## 🤝Dependencies
- [.NET](https://dotnet.microsoft.com/)
- [AppBase](https://github.com/carina-studio/AppBase)
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)
- [Avalonia XAML Behaviors](https://github.com/wieslawsoltes/AvaloniaBehaviors)
- [NLog](https://github.com/NLog/NLog)
- [NUnit](https://github.com/nunit/nunit)
- [ReactiveUI](https://github.com/reactiveui/ReactiveUI)
- [System.Data.SQLite](https://system.data.sqlite.org/)
