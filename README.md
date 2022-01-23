# ULogViewer [![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/releases/tag/0.33.0.1223) [![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/commits/master) [![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading and parsing various type of logs.

## 📥 Download

Operating System                      | Download | Version | Screenshot
:------------------------------------:|:--------:|:-------:|:----------:
Windows 8/10/11                       |[x86](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-win-x86.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-win-x64.zip)  &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-win-arm64.zip)|0.34.0.123 (Preview)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows.png)
Windows 7<br/>*(.NET Runtime needed)* |[x86](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-win-x86-fx-dependent.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-win-x64-fx-dependent.zip)|0.34.0.123 (Preview)|
macOS 11/12                           |[x64](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-osx-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/0.34.0.123/ULogViewer-0.34.0.123-osx-arm64.zip)|0.34.0.123 (Preview)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_macOS_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_macOS.png)
Linux                                 |[x64](https://github.com/carina-studio/ULogViewer/releases/download/0.33.0.1223/ULogViewer-0.33.0.1223-linux-x64.zip)|0.33.0.1223* (Preview)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Ubuntu_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Ubuntu.png)

- *Currently there is a serious issue on Linux version which is caused by dependent library. We will release Linux version once the dependent library updated.*
- [How to Install and Upgrade ULogViewer](https://carina-studio.github.io/ULogViewer/installation_and_upgrade.html)

## 📣 What's Change in 0.34.0.123
- Support arranging tabs by dragging.
- Support opening tab in new window.
- Support layouting windows in horizontal, vertical or tile mode.
- Support using system theme mode on ```macOS```.
- Add ```Android Device Event Log```, ```macOS Installation Log```, ```macOS System Wide Log```, ```macOS System Log Files``` built-in log profiles.
- Support hiding side panel to get more space for viewing logs.
- Support decompression while reading logs from ```.gz``` file.
- Improve tab scrolling UX.
- Improve toolbar scrolling UX.
- Reduce size of update packages for auto updating.
- Other UX improvement.
- Use system menu bar on ```macOS```.
- Use single process to manage all windows.
- Update dependent libraries.
- Fix issue of unable to launch on ```ARM64``` PC.
- Other bug fixing.

## ⭐ Log data sources
- Standard Output (stdout)
- Files
- Windows Event Logs (Windows only)
- HTTP/HTTPS
- TCP (without SSL)
- UDP
- SQLite

## ⭐ Log profiles
Each log profile defines:
- What log data source should be used.
- How to parse log data into structured logs.
- What properties of log should be displayed in the list.
- How to output logs back to text (ex, copying).

Currently there are 18 built-in log profiles:
- Android Device Log.
- Android Log Files.
- Android Kernel Log Files.
- Android System Trace File.
- Git Log.
- Git Log (Simple).
- Linux Kernel Log.
- Linux Kernel Log Files.
- Linux System Log.
- Linux System Log Files.
- NLog (TCP).
- Raw Text In Files.
- Raw HTTP/HTTPS Response.
- Raw Text From TCP Client.
- Windows Event Logs (Application/System/Secutiry/Setup).

You can also create, copy or export your own log profiles according to your requirement.

## ⭐ Log filtering
Log filtering is the most important feature in ULogViewer which helps you to find and analyze the problem from logs.
You can filter logs by:
- Text filter described by regular expression.
- Level(Priority) of log.
- Process ID of log if available.
- Thread ID of log if available.

For text filter, you can also predefine some filters you may use frequently and filter logs by cobination of these text filters.

## ⭐ Log marking
When viewing logs, you can mark some logs with different colors which are important for you. There is a separated view to list all marked logs to help you to jump to marked log quickly.
Marked logs will be kept if you are viewing logs from files so that you don't need to mark them again when you open log files next time.

## 📔 Other Topics
- [How Does ULogViewer Read and Parse Logs](https://carina-studio.github.io/ULogViewer/logs_reading_flow.html)

## 🤝 Dependencies
- [.NET](https://dotnet.microsoft.com/)
- [AppBase](https://github.com/carina-studio/AppBase)
- [AppSuiteBase](https://github.com/carina-studio/AppSuiteBase)
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)
- [Avalonia XAML Behaviors](https://github.com/wieslawsoltes/AvaloniaBehaviors)
- [NLog](https://github.com/NLog/NLog)
- [NUnit](https://github.com/nunit/nunit)
- [System.Data.SQLite](https://system.data.sqlite.org/)
