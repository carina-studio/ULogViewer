# ULogViewer [![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/releases/tag/1.0.7.613) ![](https://img.shields.io/github/downloads/carina-studio/ULogViewer/total) [![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/commits/master) [![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading, parsing and analysing various type of logs.

## 📥 Download

### Preview
Operating System                      | Download | Version | Screenshot
:------------------------------------:|:--------:|:-------:|:----------:
Windows 8/10/11                       |[x86](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-win-x86.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-win-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-win-arm64.zip)|2.0.1.801 (Preview)|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Windows_Preview_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Windows_Preview.png)
macOS 11/12                           |[x64](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-osx-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-osx-arm64.zip)|2.0.1.801 (Preview)|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_macOS_Preview_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_macOS_Preview.png)
Linux                                 |[x64](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-linux-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/2.0.1.801/ULogViewer-2.0.1.801-linux-arm64.zip)|2.0.1.801 (Preview)|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Fedora_Preview_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Fedora_Preview.png)

### Stable
Operating System                      | Download | Version | Screenshot
:------------------------------------:|:--------:|:-------:|:----------:
Windows 8/10/11                       |[x86](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-win-x86.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-win-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-win-arm64.zip)|1.0.7.613|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Windows_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Windows.png)
Windows 7<br/>*(.NET Runtime needed)* |[x86](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-win-x86-fx-dependent.zip) &#124; [x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-win-x64-fx-dependent.zip)|1.0.7.613|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Windows7_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Windows7.png)
macOS 11/12                           |[x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-osx-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-osx-arm64.zip)|1.0.7.613|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_macOS_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_macOS.png)
Linux                                 |[x64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-linux-x64.zip) &#124; [arm64](https://github.com/carina-studio/ULogViewer/releases/download/1.0.7.613/ULogViewer-1.0.7.613-linux-arm64.zip)|1.0.7.613|[<img src="https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Fedora_Thumb.png" width="150"/>](https://carinastudio.azurewebsites.net/ULogViewer/Screenshots/Screenshot_Fedora.png)

- [Installation and Upgrade Guide](https://carinastudio.azurewebsites.net/ULogViewer/InstallAndUpgrade)

## 📣 What's Change in 2.0 (Preview)
- Log analysis including finding key logs, calculating duration of operation, even writing your own script to analyze logs.
- Listing and managing added log files in side panel.
- Categorizing logs by timestamp automatically.
- Setting precondition to control logs loaded from files.
- Supporting writing your own script to read raw log data.
- Support setting log profile as template.
- New ```Azure CLI```, ```MySQL Database``` and ```SQL Server Database``` built-in log data sources.
- New ```Android Device System Trace```, ```Azure Webapp log Files```, ```ULogViewer Log File``` and ```ULogViewer Real-time Log``` built-in log profiles.
- New ```Azure Webapp Log Stream``` built-in log profile template.
- Supporting editing PATH environment variable.
- Adding ```zh-CN``` language support.
- UI/UX Improvement.

## 📣 What's Change in 1.0
- Support specifying delay between restarting reading logs for continuous reading case.
- Support input assistance for regular expression and string interpolation format.
- Add UI to help verifying log line pattern including captured log properties.
- Support auto update on ```macOS```.
- Finetune UI for ```Windows```.
- Other UX improvement.
- Update dependent libraries.
- Other bug fixing.

## 🗄 Log data sources
- Standard Output (stdout).
- Files.
- Windows Event Logs (Windows only).
- HTTP/HTTPS.
- TCP (without SSL).
- UDP.
- SQLite Database.
- Azure CLI [Pro][v2.0+].
- MySQL Database [Pro][v2.0+].
- SQL Server Database [Pro][v2.0+].
- Log Data Source Script [Pro][v2.0+].

## 📖 Log profiles
Each log profile defines:
- What log data source should be used.
- How to parse log data into structured logs.
- What properties of log should be displayed in the list.
- How to output logs back to text (ex, copying).

Currently there are 27 built-in log profiles:
- Android Device Event Log.
- Android Device Log.
- Android Device System Trace [macOS/Linux][v2.0+].
- Android Log Files.
- Android Kernel Log Files.
- Android System Trace File.
- Azure Webapp log Files [Pro][v2.0+].
- Azure Webapp Log Stream [Template][Pro][v2.0+].
- Git Log.
- Git Log (Simple).
- Linux Kernel Log [Linux].
- Linux Kernel Log Files.
- Linux System Log [Linux].
- Linux System Log Files.
- macOS Installation Log [macOS].
- macOS System Log Files.
- macOS System Wide Log [macOS].
- NLog (TCP).
- Raw Text In Files.
- Raw HTTP/HTTPS Response.
- Raw Text From TCP Client.
- ULogViewer Log File [v2.0+].
- ULogViewer Real-time Log [v2.0+].
- Windows Event Logs (Application) [Windows].
- Windows Event Logs (System) [Windows].
- Windows Event Logs (Secutiry) [Windows].
- Windows Event Logs (Setup) [Windows].

You can also create, copy or export your own log profiles according to your requirement.

## 🔍 Log filtering
Log filtering is the most important feature in ULogViewer which helps you to find and analyze the problem from logs.
You can filter logs by:
- Text filter described by regular expression.
- Level(Priority) of log.
- Process ID of log if available.
- Thread ID of log if available.

For text filter, you can also predefine some filters you may use frequently and filter logs by cobination of these text filters.

## 📌 Log marking
When viewing logs, you can mark some logs with different colors which are important for you. There is a separated side panel to list all marked logs to help you to jump to marked log quickly.
Marked logs will be kept if you are viewing logs from files so that you don't need to mark them again when you open log files next time.

## 📊 Log Analysis (v2.0+)
Except for log filtering, you can also define rule sets or write scripts to analyze logs. Log analysis runs in background separately and generate results to a separated side panel. Currently there are 3 type of log analysis are supported:
- **Key Log Analysis**

  Find log with specific text pattern and level. You can extract information from log and put it to the result message.
- **Operation Duration Analysis**

  Find operation marked by specific starting and ending logs and calculate the duration of it. You can extract information from log and put it to the result message.
- **Log Analysis Script**

  Write script to analyze logs according to your requirement completely.

Log analysis is enabled only in Pro version.

## 📔 Other Topics
- [How Does ULogViewer Read and Parse Logs](https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs)
- [Log Filtering](https://carinastudio.azurewebsites.net/ULogViewer/LogFiltering)
- [Scripting in ULogViewer](https://carinastudio.azurewebsites.net/ULogViewer/Scripting)
- [Log Analysis](https://carinastudio.azurewebsites.net/ULogViewer/LogAnalysis)
- [Log Data Source Script](https://carinastudio.azurewebsites.net/ULogViewer/ScriptLogDataSource)

## 🤝 Dependencies
- [.NET](https://dotnet.microsoft.com/)
- [AppBase](https://github.com/carina-studio/AppBase)
- [AppSuiteBase](https://github.com/carina-studio/AppSuiteBase)
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [AvaloniaEdit](https://github.com/AvaloniaUI/AvaloniaEdit)
- [Avalonia XAML Behaviors](https://github.com/wieslawsoltes/AvaloniaBehaviors)
- [Jint](https://github.com/sebastienros/jint)
- [NLog](https://github.com/NLog/NLog)
- [NUnit](https://github.com/nunit/nunit)
- [System.Data.SQLite](https://system.data.sqlite.org/)
