# Changes in ULogViewer 2026.2
 ---

## New Features
+ Added support for Japanese language.
+ Added support for automatic log profile selection: drop log files into the window and ULogViewer will search for the log profiles which support them.

## Improvement
+ The application no longer needs to be restarted when the Chinese environment changes after modifying the `Language` option.
+ Added a button to open the script log window when editing scripts.
+ Added a hint of dropping files to the tab without log profile.
+ Applied the new window style of macOS 26.

## Behavior Changes
+ The log profile selector is no longer shown after creating a new tab by default, and can be changed by the `Log Profile Selection for a New Tab` option.

## Bug Fixing
+ Fixed the failure to use `Noto Sans` in the Chinese environment.
+ Fixed an issue that the timestamp of reading log may not be updated after changing the timestamp format.
+ Fixed an issue that the custom title of tab is lost after restarting the application.
+ Fixed an issue that the setting of using command-line shell to run commands is reset after restarting the application.
+ Fixed an issue that the custom title of tab is not shown when reading logs from single file or by command.
+ Fixed potential stability issues.
+ Minor bug fixing.