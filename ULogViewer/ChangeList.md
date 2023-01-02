# What's Change in ULogViewer 3.0
 ---

## New Features
+ Syntax highlighting for **Regular Expression**, **Date and Time Format**, **Time Span Format**, **Query String (SQL)** and **Command-Line Shell Command**.
+ Highlighting text sequences in logs which are matched by text filter.
+ Highlighting PID and TID which are same as PID and TID of selected log.
+ Supporting running commands by specified command-line shell when using **'Standard Output (stdout)'** log data source.
+ Supporting right-clicking on **'Create new tab'** button to select log profile and set to new tab directly.
+ Adding button beside **'Select/Change log profile'** button for opening menu to select log profile quickly.
+ Supporting clicking on **'Earliest timestamp of log/Minimum time span of log'** or **'Latest timestamp of log/Maximum time span of log'** to select the log.
+ Supporting creating new operation duration analysis rule which begins from or is ended with another rule.
+ Supporting converting to log level from raw value of specified log property.
+ Supporting different colors for icons of log profile, log analysis rule set and log analysis script.
+ Adding new built-in log profiles: 
    + Apache Access Log Files
    + Apache Error Log Files
    + Apple Devices Log
    + Apple Device Simulators Log
    + Specific Apple Device Log
    + Specific Apple Device Simulator Log

+ Adding new built-in log profile templates: 
    + Specific Apple Device Log
    + Specific Apple Device Simulator Log

+ Adding built-in fonts:
    + IBM Plex Mono
    + Roboto
    + Roboto Mono
    + Source Code Pro

+ Supporting customizing text color of each visible log property.
+ Adding **'Use compact layout'** setting for device with small screen.
+ Supporting **Python 3.4** as script language.
+ Adding **'Quantity'** and **'Byte size'** to each result of log analysis.
+ Automatically generating statistic of **'Duration'**, **'Quantity'** and **'Byte size'** of selected results of log analysis.
+ Adding directory of **Homebrew** as default path on **macOS** to search command.

## Improvement
+ Allow editing built-in log profile and new log profile will be created automatically.
+ Selecting proper scale factor of screen on **Linux** automatically.
+ Showing sample result when typing Date and Time Format.
+ Improving layout of items on toolbar.
+ More icons for log profile.
+ Improving UX of text pattern editing.
+ Supporting showing progress on dock tile icon on **macOS**.
+ Improving performance and memory usage of log filtering.
+ Updating internal script running flow.
+ Other UI/UX Improvement.

## Behavior Changes
+ Using **⌘** key for multi-selection of items on **macOS** instead of using **Ctrl**.
+ Aligning application activation/deactivation behavior on **macOS**.

## Bug Fixing
+ Minor bug fixing.