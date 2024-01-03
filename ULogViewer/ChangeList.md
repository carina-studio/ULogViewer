# What's Change in ULogViewer 4.0
 ---

## New Features
+ Allow toggling visibilities of data sources of chart.
+ Allow changing command to read logs directly in log viewer.
+ Allow stopping reading logs continuously from data source.
+ Allow viewing history of log text filters.
+ Allow specifying **Requirement of Working Directory** for each log profile.
+ Add **Raw Text From Standard Output (stdout)** and **Specific Android Device System Trace** built-in log profile and template.
+ Add **Host Name**, **Module**, **Sub Module**, **System** and **Sub System** log property names.
+ Place holder (##_##) is also supported in **Setup Commands** and **Teardown Commands** of log data source options.
+ Support adding description to each pattern of log line in log profile.
+ Add **EnvironmentVariables**, **ProcessId** and **ProcessName** fields in log data source options.
+ Allow showing dialog by script to select one or more custom items.
+ Support showing labels on X axis of log chart.
+ Support using built-in font (Noto Sans) for Chinese.
+ Support renaming group of log text filters.
+ Support showing horizontal and vertical lines between logs.
+ Show timestamp of log in tool tip of chart.
+ Allow using IP address and URI in clipboard automatically for reading logs.
+ Allow selecting common IP addresses in dialog of IP endpoint.

## Improvement
+ Add Quick-Start Guide.
+ Support formatting JSON data which contains multiple root elements while reading logs.
+ Support showing multiple lines for each log in **Marked logs** panel if there is no log property defined to be shown in current log profile.
+ Add [Apply] button to dialogs of editing script.
+ Allow editing current log data source script through clicking item in **'Other actions'** (or in **'Tools'** menu on **macOS**).
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
+ Open log file action menu by right-clicking on log file instead of clicking button on log file.
+ Only one group of log text filter can be created before activating ULogViewer Pro.
+ **\\ (Backslash)** character will no longer be valid for name of group of log text filter.
+ The history of log text filter of tab won't be overridden when applying new log text filter after navigating through the history.

## Bug Fixing
+ Minor bug fixing.