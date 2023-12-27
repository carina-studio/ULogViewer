# Quick-Start Guide for ULogViewer
 ---
+ [Select a Log Profile](#select-a-log-profile)
+ [Create or Edit a Log Profile](#create-or-edit-a-log-profile)
+ [Start Reading Logs](#start-reading-logs)
+ [Mark Logs](#mark-logs)

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
2. Click ***Edit '(name)' …*** item. Please noted that you cannot edit if it is a built-in log profile. You can refer to [Copy Existing Log Profile](#copy-existing-log-profile) to copy a built-in log profile.

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
1. Select one or more logs and press **M** to mark/unmark them.
2. Right-click on selected logs, click ***Mark logs>No color*** or ***Unmark logs*** item.
3. Move mouse on the left hand side of logs and click ![](Circle_Outline_24px.png).
4. Move mouse on the left hand side of logs, right-click on ![](Circle_Outline_24px.png) and click ***No color*** or ***Unmark logs*** item.

### Mark Logs with Color
There are 3 ways to mark logs with color:
1. Select one or more logs and press **Ctrl+Alt+1** ~ **Ctrl+Alt+8** (**⌥⌘1** ~ **⌥⌘8** on macOS) to mark them with color you want.
2. Right-click on selected logs, click ***Mark logs*** then click a color item.
3. Move mouse on the left hand side of logs, right-click on ![](Circle_Outline_24px.png) and click a color item.

[Back to Top](#-quick-start-guide-for-ulogviewer)
