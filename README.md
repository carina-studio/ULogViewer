# ULogViewer
[![](https://img.shields.io/github/release-date-pre/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/releases/tag/3.0.13.128) ![](https://img.shields.io/github/downloads/carina-studio/ULogViewer/total) [![](https://img.shields.io/github/last-commit/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/commits/master) [![](https://img.shields.io/github/license/carina-studio/ULogViewer?style=flat)](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE)

A cross-platform, agent-native log viewer for reading, parsing, and analyzing various types of logs. Please visit the [Website](https://carinastudio.net/ULogViewer/) for more details.

跨平台且支援 AI 代理的日誌檢視器，提供多種形式之日誌讀取、解析與分析。請參閱 [網站](https://carinastudio.net/ULogViewer/) 以取得更多資訊。

![](https://carinastudio.net/ULogViewer/Banner-v2.png?v=1)

## ⚠️ NOTICE
If you encounter failure of upgrading from `2026.1.0 Preview`, `2026.1.1 RC`, or `2026.1.2` on Windows, please manually close all `mcp.exe` processes and try again.

## 📥 Download 2026.2

### Windows
[![](https://img.shields.io/badge/x64-blue?style=for-the-badge)](https://packages.carinastudio.net/ULogViewer/2026.2.2/ULogViewer-2026.2.2-win-x64.zip)
[![](https://img.shields.io/badge/arm64-blue?style=for-the-badge)](https://packages.carinastudio.net/ULogViewer/2026.2.2/ULogViewer-2026.2.2-win-arm64.zip)

### macOS
[![](https://img.shields.io/badge/Apple%20Silicon%20(arm64)-blueviolet?style=for-the-badge)](https://packages.carinastudio.net/ULogViewer/2026.2.2/ULogViewer-2026.2.2-osx-arm64.zip)
[![](https://img.shields.io/badge/x64-blueviolet?style=for-the-badge)](https://packages.carinastudio.net/ULogViewer/2026.2.2/ULogViewer-2026.2.2-osx-x64.zip)

### Linux
[![](https://img.shields.io/badge/x64-orange?style=for-the-badge)](https://packages.carinastudio.net/ULogViewer/2026.2.2/ULogViewer-2026.2.2-linux-x64.zip)
[![](https://img.shields.io/badge/arm64-orange?style=for-the-badge)](https://packages.carinastudio.net/ULogViewer/2026.2.2/ULogViewer-2026.2.2-linux-arm64.zip)

## ⭐ Supported Log Data Sources
A log data source reads raw log data into ULogViewer. It can be a file, a network stream, a database, or a script you write.

| Category | Log Data Sources |
|---|---|
| Local | Standard Output (stdout)<br/>Files |
| Windows | Windows Event Log<br/>Windows Event Log File |
| Network | HTTP/HTTPS<br/>TCP (without SSL)<br/>UDP |
| Database | SQLite Database<br/>MySQL Database `Pro`<br/>SQL Server Database `Pro` |
| Cloud | Azure CLI `Pro` |
| Script | Log Data Source Script `Pro`<br/>Embedded Log Data Source Script `Pro` |

[Know more about](https://carinastudio.net/ULogViewer/HowToReadAndParseLogs)

## ⭐ Log Profiles
Each log profile defines:
- What log data source should be used.
- How to parse log data into structured logs.
- Which properties of the log should be displayed in the list.
- Which properties of the log should be used to generate charts. `Pro`
- How to output logs back to text (e.g., copying).

## ⭐ Log Filtering
Log filtering is one of the most important features in ULogViewer that helps you find and analyze problems from logs. You can filter logs by:
- Text filter defined by a regular expression.
- Level (Priority) of the log.
- Process ID of the log, if available.
- Thread ID of the log, if available.
- Marked logs.

For text filter, you can also predefine some filters you may use frequently and filter logs by combination of these text filters.

[Know more about](https://carinastudio.net/ULogViewer/LogFiltering)

## ⭐ Log Marking
When viewing logs, you can mark some logs with different colors which are important for you. There is a separated side panel to list all marked logs to help you to jump to marked log quickly. Marked logs will be kept if you are viewing logs from files so that you don’t need to mark them again when you open log files next time.

## ⭐ Log Analysis
Except for log filtering, you can also define rule sets or write scripts to analyze logs. Log analysis runs in the background separately and generates results to a separate side panel. Currently there are 4 types of log analysis supported:
- **Key Log Analysis**<br/>Find logs with a specific text pattern and level. You can extract information from the log and put it in the result message.
- **Operation Duration Analysis**<br/>Find operations marked by specific starting and ending logs and calculate their duration. You can extract information from the log and put it in the result message.
- **Operation Counting Analysis**<br/>Find operations marked with a specific text pattern and level, then count the number of operations that occurred for each given time frame. You can extract information from the log and put it in the result message.
- **Log Analysis Script**<br/>Write script to analyze logs according to your requirement completely.

[Know more about](https://carinastudio.net/ULogViewer/LogAnalysis)

## ⭐ AI Integration
Starting from 2026.1, ULogViewer Pro can expose your logs to AI clients through the Model Context Protocol (MCP). You can ask the AI assistant in your client to inspect logs, query tabs, set filters, mark logs and more. The following MCP clients have been verified to work with ULogViewer:
- Claude Code
- Claude Desktop
- Codex
- Cursor
- Gemini CLI
- LM Studio
- Windsurf

MCP access is disabled by default. You can enable it in Options, where you can also control which actions (filtering, marking, selection, window activation) are allowed.

Logs are redacted by Sensitive Data Protection before they leave ULogViewer, masking content such as email addresses and IP addresses. It is applied to MCP responses by default, and its rules can be edited in Options.

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
