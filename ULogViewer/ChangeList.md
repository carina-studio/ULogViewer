# What's Change in ULogViewer 3.0
 ---

## New Features
+ Support setting **'Cooperative log analysis script'** which is embedded in log profile, and will be run automatically when using the log profile.
+ Support embedding **Log data source script** into log profile and set it as data source of log profile.
+ Syntax highlighting for **Regular Expression**, **Date and Time Format**, **Time Span Format**, **Query String (SQL)** and **Command-Line Shell Command**.
+ Highlight text sequences in logs which are matched by text filter.
+ Highlight PID and TID which are same as PID and TID of selected log.
+ Support running commands by specified command-line shell when using **'Standard Output (stdout)'** log data source.
+ Support filtering logs with given accuracy by text of selected log property.
+ Support selecting intersection/union of filter conditions dynamically according to current conditions.
+ Support right-clicking on **'Create new tab'** button to select log profile and set to new tab directly.
+ Add button beside **'Select/Change log profile'** button for opening menu to select log profile quickly.
+ Track recently used log profiles and list in log profile selection dialog and menu.
+ Support clicking on **'Earliest timestamp of log/Minimum time span of log'** or **'Latest timestamp of log/Maximum time span of log'** to select the log.
+ Support creating new operation duration analysis rule which begins from or is ended with another rule.
+ Support converting to log level from raw value of specified log property.
+ Support different colors for icons of log profile, log analysis rule set and log analysis script.
+ Support specifying text color for each log property.
+ Add **'Timestamp of reading log'** property for log and support using it as key of log sorting.
+ Add new built-in log profiles: 
    + Apache Access Log Files
    + Apache Error Log Files
    + Apple Devices Log
    + Apple Device Simulators Log
    + Specific Apple Device Log
    + Specific Apple Device Simulator Log

+ Add new built-in log profile templates: 
    + Specific Apple Device Log
    + Specific Apple Device Simulator Log

+ Add built-in fonts:
    + IBM Plex Mono
    + Roboto
    + Roboto Mono
    + Source Code Pro

+ Support customizing text color of each visible log property.
+ Support specifying font for **Regular Expression**, **Date and Time Format**, **Time Span Format**, **Query String (SQL)** and **Command-Line Shell Command**.
+ Support specifying font for script editor.
+ Add **'Use compact layout'** setting for device with small screen.
+ Try keeping view position of logs after changing view of logs.
+ Support **Python 3.4** as script language.
+ Add **Operation Counting** log analysis to extract number of operations in each time frame.
+ Add **'Quantity'** and **'Byte size'** to each result of log analysis.
+ Automatically generate statistic of **'Duration'**, **'Quantity'** and **'Byte size'** of selected results of log analysis.
+ Support selecting the log with earliest/latest timestamp (time span) by clicking **'Earliest/Latest timestamp of log'** button at bottom of log viewer.
+ Add directory of [**Homebrew**](https://brew.sh/) as default path on **macOS** to search command.
+ Add **'Script log output window'** for better script debugging experience.
+ Support searching selected property of log on the Internet.

## Improvement
+ Allow editing pattern (Regular Expression) directly in text area. You can still edit pattern detailedly by clicking the button at right hand side of text area.
+ Automatically use log level mapping of log reading if specific log level is undefined in log level mapping of log writing.
+ Allow editing built-in log profile and new log profile will be created automatically.
+ No need to import related types and namespaces manually in script of log analysis and log data source.
+ Select proper scale factor of screen on **Linux** automatically.
+ Show **Level** log property in special way.
+ Improve control of history of log text filter.
+ Show indicator on toolbar when new log analysis result has been generated in backgroud.
+ Show sample result when typing Date and Time Format.
+ Improve layout of items on toolbar.
+ More icons for log profile.
+ Improve UX of text pattern editing.
+ Improve distribution of color of color indicator.
+ Support showing progress on dock tile icon on **macOS**.
+ Improve performance and memory usage.
+ Improve displaying of Chinese.
+ Update internal script running flow.
+ Other UI/UX Improvement.

## Behavior Changes
+ Change **'Save memory usage aggressively'** setting to **'Policy of memory usage'** to provide better control of balance of CPU/memory usage. 
+ Use **⌘** key for multi-selection of items on **macOS** instead of using **Ctrl**.
+ Align application activation/deactivation behavior on **macOS**.
+ Allow using empty display name for log property.

## Bug Fixing
+ Minor bug fixing.