# What's Change in ULogViewer 3.1
 ---

## New Features
+ Allow toggling visibilities of data sources of chart.
+ Allow changing command to read logs directly in log viewer.
+ Allow specifying **Requirement of Working Directory** for each log profile.
+ Add **Raw Text From Standard Output (stdout)** and **Specific Android Device System Trace** built-in log profile and template.
+ Add **Host Name**, **Module**, **Sub Module**, **System** and **Sub System** log property names.
+ Place holder (##_##) is also supported in **Setup Commands** and **Teardown Commands** of log data source options.
+ Support adding description to each pattern of log line in log profile.
+ Add **ProcessId** and **ProcessName** fields in log data source options.
+ Support showing labels on X axis of log chart.
+ Support using built-in font (Noto Sans) for Chinese.
+ Support renaming group of log text filters.
+ Show timestamp of log in tool tip of chart.

## Improvement
+ Improve logs filtering/analysis with descending sort direction.
+ Show indicators for required options to start reading logs.
+ Add side navigation bar to dialogs with more content.
+ Show more information of log analysis rules in editor.
+ Show improved notifications after performing operations such as exporting log profile or saving logs to file.
+ Improve input of CJK text on **macOS**.
+ Improve animations of user interface.
+ Allow resizing dialogs with complex content.
+ Add more details of UI element.

## Behavior Changes
+ Showing multiple lines of log in viewer has been removed.
+ Initial log profile won't be set to new opened window.

## Bug Fixing
+ Minor bug fixing.