# Changes in ULogViewer 2026.0
 ---

## New Features
+ Added support for temporarily showing raw log lines even when [log patterns](https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs#LogPatterns) are defined.
+ Changes to the width of the log property column in the log viewer will be saved to the log profile, including built‑in profiles.
+ The pre-defined log level mapping will be used if no log level mapping defined in log profile.
+ Added support for applying the system text size to selected parts of user interface on Windows and macOS.

## Improvement
+ Allowed reordering items in the ```Edit log profile``` and ```Data source options``` dialogs by dragging with the mouse instead of using action buttons.
+ Improved indicators for required or invalid values in the ```Edit log profile``` and ```Data source options```, and more dialogs.
+ Improved the user experience of the ```Select log profile``` dialog.
+ Show the selected file name, command, IP endpoint, process ID/name, URL or working directory on the tab.
+ Added more default paths for searching commands.
+ Text used for log level mapping is now treated as case‑insensitive.
+ All log levels will be available for filtering logs if no log level mapping defined in current log profile.

## Behavior Changes
+ The ```Use compact layout``` option is no longer supported.
+ The application need to be restarted if the Chinese environment changes after modifying the ```Language``` option.

## Bug Fixing
+ Minor bug fixing.