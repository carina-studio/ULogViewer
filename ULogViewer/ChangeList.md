# Changes in ULogViewer 2026.2
 ---

## New Features
+ Added support for Japanese language.
+ Added support for automatic log profile selection: drop log files into the window and ULogViewer will search for the log profiles which support them.
+ Added support for opening the log files in a directory by dropping the directory into the window.

## Improvement
+ The application no longer needs to be restarted when the Chinese environment changes after modifying the `Language` option.
+ Added a button to open the script log window when editing scripts.
+ Added a hint of dropping files to the tab without log profile.
+ Added support for dropping more than one directory into the window.
+ Applied the new window style of macOS 26.

## Behavior Changes
+ The log profile selector is no longer shown after creating a new tab by default, and can be changed by the `Log Profile Selection for a New Tab` option.
+ Dropping files and directories at the same time opens the valid files in the directories as well.
+ When the `Log Profile Selection for a New Tab` option is `Auto`, dropping a directory no longer provides the log profile selector which contains the log profiles using a working directory.

## Bug Fixing
+ Fixed the failure to use `Noto Sans` in the Chinese environment.
+ Fixed an issue that the timestamp of reading log may not be updated after changing the timestamp format.
+ Fixed an issue that the custom title of tab is lost after restarting the application.
+ Fixed an issue that the setting of using command-line shell to run commands is reset after restarting the application.
+ Fixed an issue that the custom title of tab is not shown when reading logs from single file or by command.
+ Fixed an issue that the working directory is not changed when dropping a directory into the tab which already has a working directory.
+ Fixed potential stability issues.
+ Minor bug fixing.