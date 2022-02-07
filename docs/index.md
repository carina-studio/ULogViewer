[![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/releases/tag/1.0.0.207)
[![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/commits/master)
[![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat-square)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading and parsing various type of logs.

## 📥 Download

Operating System                      | Download | Version | Screenshot
:------------------------------------:|:--------:|:-------:|:----------:
Windows 8/10/11                       |[x86](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-win-x86.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-win-x64.zip)  &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-win-arm64.zip)|1.0.0.207 (RC)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows.png)
Windows 7<br/>*(.NET Runtime needed)* |[x86](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-win-x86-fx-dependent.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-win-x64-fx-dependent.zip)|1.0.0.207 (RC)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows7_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Windows7.png)
macOS 11/12                           |[x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-osx-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-osx-arm64.zip)|1.0.0.207 (RC)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_macOS_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_macOS.png)
Linux                                 |[x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.0.207/ULogViewer-1.0.0.207-linux-x64.zip)|1.0.0.207 (RC)|[<img src="https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Fedora_Thumb.png" width="150"/>](https://carina-studio.github.io/ULogViewer/Screenshots/Screenshot_Fedora.png)

- [How to Install and Upgrade ULogViewer](installation_and_upgrade.md)

## 📣 What's Change in 1.0.0.207 RC
- Support specifying delay between restarting reading logs for continuous reading case.
- Support input assistance for regular expression and string interpolation format.
- Add UI to help verifying log line pattern including captured log properties.
- Support auto update on ```macOS```.
- Finetune UI for ```Windows```.
- Other UX improvement.
- Update dependent libraries.
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

Currently there are 22 built-in log profiles:
- Android Device Event Log.
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
- macOS Installation Log.
- macOS System Log Files.
- macOS System Wide Log.
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
When viewing logs, you can mark some logs with different colors which are important for you. There is a separated side panel to list all marked logs to help you to jump to marked log quickly.
Marked logs will be kept if you are viewing logs from files so that you don't need to mark them again when you open log files next time.

## 📔 Other Topics
- [How Does ULogViewer Read and Parse Logs](logs_reading_flow.md)

## 📜 User Agreement
- [English](user_agreement.md)
- [正體中文 (台灣)](user_agreement_zh-TW.md)

## 📜 Privacy Policy
- [English](privacy_policy.md)
- [正體中文 (台灣)](privacy_policy_zh-TW.md)
