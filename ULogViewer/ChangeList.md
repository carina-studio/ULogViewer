# What's Change in ULogViewer 2026.0
 ---

## New Features
+ Support showing raw log lines temporarily even when [log patterns](https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs#LogPatterns) were defined.
+ Support filtering logs with multiple log levels.
+ Change of width of log property column in log viewer will be saved back to log profile, even for built-in log profiles.
+ Pre-defined log level mapping will be used if no log level mapping defined in log profile.

## Improvement
+ Allow reordering items in ```Edit log profile```, ```Edit operation duration analysis rule``` and ```Data source options``` dialogs by mouse dragging instead of clicking action button.
+ Better indications for required or invalid value in ```Edit log profile``` and ```Data source options``` dialogs.
+ Improve user experience of using ```Select log profile``` dialog.
+ Show selected file name, command, IP endpoint, Process ID/name, URL or working directory on tab.
+ Add more default paths to search commands.
+ Text for log level mapping will be treated as case-insensitive.
+ All log levels will be available to filter logs if no log level mapping defined in current log profile.

## Behavior Changes
+ 

## Bug Fixing
+ Minor bug fixing.