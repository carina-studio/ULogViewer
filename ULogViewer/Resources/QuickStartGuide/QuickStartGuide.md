# Quick-Start Guide for ULogViewer
 ---
+ [Select a Log Profile](#select-a-log-profile)
+ [Create or Edit a Log Profile](#create-or-edit-a-log-profile)
+ [Start Reading Logs](#start-reading-logs)
+ [Mark Logs](#mark-logs)
+ [Filter Logs](#filter-logs)

## Select a Log Profile
To start loading and viewing logs, you need to select a proper log profile first. 
There are 3 ways to select or change log profile:

### Log Profile Selection Menu
1. Click drop-down arrow besides ![](LogProfile_Outline_24px.png) on tool bar.
2. Select a log profile.

### Log Profile Selection Dialog
1. Click ![](LogProfile_Outline_24px.png) on tool bar.
2. Find a log profile.
3. Double-click on log profile, or select log profile and click [OK] button.

### New Tab with Log Profile
1. Right-click on ![](Add_24px.png) on the row of tabs.
2. Select a log profile.

[Back to Top](#-quick-start-guide-for-ulogviewer)


## Create or Edit a Log Profile
You may need to create or modify a log profile in order to match your requirement. 
Please refer to [here](https://carinastudio.azurewebsites.net/ULogViewer/HowToReadAndParseLogs) to get more details about log profile.

### Edit Current Log Profile
1. Click drop-down arrow besides ![](LogProfile_Outline_24px.png) on tool bar.
2. Click ***Edit '(name)' …*** item. Please note that you cannot edit if it is a built-in log profile. You can refer to [Copy Existing Log Profile](#copy-existing-log-profile) to copy a built-in log profile.

### Edit a Log Profile
1. Click ![](LogProfile_Outline_24px.png) on tool bar.
2. Find a log profile.
3. Move mouse onto the log profile and click ![](Edit_Outline_24px.png).

### Create a New Log Profile
1. Click ![](LogProfile_Outline_24px.png) on tool bar.
2. Click [Create…] button

### Copy Current Log Profile
1. Click drop-down arrow besides ![](LogProfile_Outline_24px.png) on tool bar.
2. Click ***Copy '(name)' …*** item.

### Copy Existing Log Profile
1. Click ![](LogProfile_Outline_24px.png) on tool bar.
2. Find a log profile.
3. Move mouse onto the log profile and click ![](Copy_Outline_24px.png).

### Import Existing Log Profile
1. Click ![](LogProfile_Outline_24px.png) on tool bar.
2. Click [Import] button.

[Back to Top](#-quick-start-guide-for-ulogviewer)


## Start Reading Logs
Each log profile defines what parameters are needed to start reading logs. For example, a file, a directory or an IP endpoint.
You don't need to setup parameters if the log profile has already prepared proper parameters to start reading log. For example, a command to be executed.

The followings are parameters you may be asked to setup before start reading logs:

### File
#### Add One or More Files
There are 2 ways to add file(s) to ULogViewer:
+ Click ![](AddFile_Outline_24px.png) on tool bar and select one or more files. 
+ Drag one or more files from File Explorer (Finder on macOS) to working area of ULogViewer.

#### Clear Added Files
1. Click ![](Delete_Outline_24px.png) on tool bar.

#### Remove Added File
1. Click ![](File_Text_Outline_24px.png) on the side bar to open ***Added log files*** panel.
2. Right-click on the file you want to remove.
3. Click ***Remove log file*** item.

### Working Directory
1. Click ![](Folder_Outline_24px.png) on tool bar.
2. Select a directory.

### Command
1. Click ![](Terminal_Outline_24px.png) on tool bar.
2. Setup command to be executed.

### IP Endpoint
1. Click ![](IPAddress_Outline_24px.png) on tool bar.
2. Set IP address and port.

### URI
1. Click ![](Uri_Outline_24px.png) on tool bar.
2. Set an URI.

### Identifier of Process (PID)
1. Click ![](Process_Outline_24px.png) on tool bar.
2. Set process identifier.

### Name of Process
1. Click ![](Process_Outline_24px.png) on tool bar.
2. Set process name.

[Back to Top](#-quick-start-guide-for-ulogviewer)


## Mark Logs
You can mark one or more interested logs to make them be easier to be found later.
All marked logs will be listed in ***Marked logs*** panel.
Marked logs will be persisted if logs are read from file(s).

### Open Marked Logs Panel
1. Click ![](Marks_Outline_24px.png) on the side bar.

### Mark/Unmark Logs
There are 4 ways to mark/unmark logs:
+ Select one or more logs and press **M** to mark/unmark them.
+ Right-click on selected logs, click ***Mark logs>No color*** or ***Unmark logs*** item.
+ Move mouse on the left hand side of logs and click ![](Circle_Outline_24px.png).
+ Move mouse on the left hand side of logs, right-click on ![](Circle_Outline_24px.png) and click ***No color*** or ***Unmark logs*** item.

### Mark Logs with Color
There are 3 ways to mark logs with color:
+ Select one or more logs and press **Ctrl+Alt+1** ~ **Ctrl+Alt+8** (**⌥⌘1** ~ **⌥⌘8** on macOS) to mark them with color you want.
+ Right-click on selected logs, click ***Mark logs*** then click a color item.
+ Move mouse on the left hand side of logs, right-click on ![](Circle_Outline_24px.png) and click a color item.

[Back to Top](#-quick-start-guide-for-ulogviewer)


## Filter logs
Log filtering is one of the most important feature in ULogViewer which helps you to find and analyze the problem from logs.

### Text Filters
Logs can be filtered according to visible log properties and text filters.
One or more text filters can be applied on filtering logs and text filters will be evaluated with **OR/Union** mode.

#### Set Text Filter
1. Press **Ctrl+F** (**⌘F** on macOS) or click on text filter input field on tool bar.
2. Set text filter in Regular Expression. Please refer to [here](https://carinastudio.azurewebsites.net/ULogViewer/RegularExpressions) for more details about using Regular Expressions in ULogViewer.

You can press **Up/Down** key when focusing on text filter input field to navigate through history of text filter in current tab.

#### Save Text Filter
There are 3 ways to save text filter:
+ Focus on text filter input field on tool bar and press **Ctrl+S** (**⌘S** on macOS). Please note that you need to set text filter first.
+ Click ![](Filters_Outline_24px.png) on tool bar and click [Create…] button. 
+ Press **Ctrl+P** (**⌘P** on macOS) and click [Create…] button.

#### Apply Saved Text Filters
There are 2 ways to apply saved text filters:
+ Click ![](Filters_Outline_24px.png) on tool bar and select one or more text filters.
+ Press **Ctrl+P** (**⌘P** on macOS) and select one or more text filters.

To select multiple saved text filters, please pressing **Shift** or **Ctrl** (**⇧** or **⌘** on macOS) when selecting.

#### Use Log Property as Text Filter
1. Right-click on a log property.
2. Click ***Filter by '(value)'*** item and select accuracy.

### Level Filter
1. Select what log level you want to see on level drop down field on tool bar.

Please note that the level filter will be valid only when ***Level*** log property is defined in current log profile.

### Process Identifier (PID) Filter
There are 2 ways to set PID filter:
+ Click PID input field on tool bar.
+ Right-click on selected logs and click ***Filter by selected PID*** or ***Filter by selected PID only*** item.

Please note that the PID filter will be valid only when ***ProcessId*** log property is defined in current log profile.

### Thread Identifier (TID) Filter
There are 2 ways to set TID filter:
+ Click TID input field on tool bar.
+ Right-click on selected logs and click ***Filter by selected TID*** or ***Filter by selected TID only*** item.

Please note that the TID filter will be valid only when ***ThreadId*** log property is defined in current log profile.

### Combination of Text Filters and Other Filters
Level, PID and TID filters are evaluated in **AND** mode and text filters are evaluated in **OR** mode.
You can choose how to combine text filters and other filters by clicking button between text filter input field and other filter field on tool bar:
+ ![](FilterCombinationMode_Auto_24px.png) Auto.
+ ![](Union_24px.png) OR/Union.
+ ![](Intersection_24px.png) AND/Intersection.

### Show Only Marked Logs Temporarily
You are allowed showing only marked logs temporarily when one or more filters are applied.
There are 2 ways to toggle:
+ Press **Alt+M** (**⌥M** on macOS).
+ Click ![](MarkedOnly_Outline_24px.png) on tool bar.

### Show All Logs Temporarily
You are allowed showing all logs temporarily when one or more filters are applied.
There are 2 ways to toggle:
+ Press **Alt+A** (**⌥A** on macOS).
+ Click ![](Visibility_Outline_24px.png) on tool bar.

### Clear All Filters
1. Click ![](ClearFilters_Outline_24px.png) on tool bar.

[Back to Top](#-quick-start-guide-for-ulogviewer)