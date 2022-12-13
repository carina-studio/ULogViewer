# User Agreement of ULogViewer
 ---
+ Version: 1.5
+ Update: 2022/8/16

This is the User Agreement of ULogViewer which you need to read before you using ULogViewer. The User Agreement may be updated in the future and you can check it on the website of ULogViewer. It means that you have agreed this User Agreement once you start using ULogViewer.


## Scope of User Agreement
ULogViewer is a software based-on Open Source Project. The ULogViewer mentioned after includes **ONLY** the executable files or zipped files which are exact same as the files provided by the following pages:

+ [Website of ULogViewer](https://carinastudio.azurewebsites.net/ULogViewer/)
+ [Project and release pages of ULogViewer on GitHub](https://github.com/carina-studio/ULogViewer)

This User Agreement will be applied when you use ULogViewer 2.0 and any future versions before the version specified in next version of User Agreement.


## Debug Mode
ULogViewer has built-in Debug Mode which is disabled by default. You can enable Debug Mode manually by launching ULogViewer with **-debug** argument.


## External Dependencies

### Android SDK
In order to use **'Android Device Log'**, **'Android Device Event Log'** and **'Android Device System Trace'** log profiles, you need to install [Android SDK or Android Studio](https://developer.android.com/studio) on your device first.

### Azure Command-Line Interface (CLI)
In order to use full features of **'Azure CLI'**, **'MySQL Database'** and **'SQL Server Database'** data sources, you need to install [Azure CLI](https://docs.microsoft.com/cli/azure/) on your device first.

### Git
In order to use **'Git Log'** and **'Git Log (Simple)'** log profiles, you need to install [Git](https://git-scm.com/) on your device first.

### Trace Conversion Tool
In order to use **'Android Device System Trace'** built-in log profile on **macOS/Linux**, you need to install [Trace Conversion Tool](https://perfetto.dev/docs/quickstart/traceconv) on your device first.

### Resize and Rotate Extension for X Window System (XRandR)
To detect display settings and apply user interface scale factor on **Linux**. Need to restart application to take effect after installation.


## File Access
Except for system files, all necessary files of ULogViewer are placed inside the directory of ULogViewer (include directory of .NET Runtime if you installed .NET on your computer). No other file access needed when running ULogViewer without loading logs except for the followings:

+ Read **/proc/meminfo** to get physical memory information on Linux.
+ Read **/etc/paths** to get global paths on macOS.
+ Read/Write Temporary directory of system for placing runtime resources.
+ Other necessary file access by .NET or 3rd-Party Libraries.

### File Access When Loading Logs
+ The file which contains raw logs will be opened in **Read** mode.
+ The \*.ulvmark file side-by-side with log file will be opened in **Read** mode.

### File Access When Viewing Logs
+ The \*.ulvmark file side-by-side with log file will be opened in **Read/Write** mode.

### File Access When Saving Logs
+ The file which raw logs written to will be opened in **Read/Write** mode.
+ The \*.ulvmark file side-by-side with log file will be opened in **Read/Write** mode.

### File Access When Self Updating
+ Downloaded packages and backed-up application files will be placed inside Temporary directory of system.

Other file access outside from executable of ULogViewer are not dominated by this User Agreement.


## Network Access
ULogViewer will access network in the following cases:

### Loading Logs through Network
Network access is needed when the source of logs is one of the following:

+ **Azure CLI**.
+ **HTTP/HTTPS**.
+ **MySQL Database**.
+ **SQL Server Database**.
+ **TCP Server**.
+ **UDP Server**.
+ **File** with the file outside from local machine.
+ **Log Data Source Script** which accesses network.

### Application Update Checking
ULogViewer downloads manifest from website of ULogViewer periodically to check whether application update is available or not.

### Self Updating
There are 4 type of data will be downloaded when updating ULogViewer:

+ Manifest of auto updater component to check which auto updater is suitable for self updating.
+ Manifest of ULogViewer to check which update package is suitable for self updating.
+ Package of auto updater.
+ Update package of ULogViewer.

Other network access outside from executable of ULogViewer are not dominated by this User Agreement.


## External Command Execution
There are some necessary external command execution when running ULogViewer:

+ Run **dotnet** to check the version of .NET installed on device.
+ Run **explorer** to open File Explorer on Windows.
+ Run **open** to open Finder on mscOS.
+ Run **defaults** to check system language and theme mode on macOS.
+ Run **nautilus** or **xdg-open** to open File Manager on Linux.
+ Run **gnome-shell** to check GUI environment on Linux.
+ Run **cmd** to update PATH environment variable on Windows if needed.
+ Run **osascript** to update /etc/paths on macOS if needed.
+ Run **xrandr** to detect display settings and apply user interface scale factor on Linux.

Except for necessary cases above, external command execution will happen when the source of logs is **Azure CLI** or **Standard Output (stdout)**. You can check the list of commands and arguments in the **Data source** options dialog when editing **Data Source** of log profile.

Please noticed that we **DON’T** guarantee the result of external command execution. It all depends on the behavior of external command and executable which you should take care of.


## Modification of Your Computer
Except for file access and the following cases, ULogViewer **WON’T** change the settings of your computer.

Please noticed that we **DON’T** guarantee your computer won’t be modified after executing external command. You should take care of it by yourself especially when running ULogViewer as Administrator on Windows.

### Editing PATH Environment Variable on Windows

#### Adding Path
All added paths will be set to PATH environment variable of **User** scope.

#### Removing Path
If removed path was listed in PATH environment variable of **Machine** scope, ULogViewer will runs cmd command with **Administrator** privilige to update PATH environment variable.

### Editing /etc/paths on macOS
ULogViewer will runs **osascript** command with **Administrator** privilige to update /etc/paths file.

### Running Script
Scripts running in ULogViewer are allowed accessing .NET features including file access, network access, computer modification, etc. Therefore, running scripts may modify even damage your computer. You need to check scripts carefully before running them.


## License and Copyright
ULogViewer is an Open Source Project of Carina Studio under [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) license. All icons except for application icon are distributed under [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) or [CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license) license. Please refer to [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) for more information of icons and its license.  

Application icon is made by [Freepik](https://www.freepik.com/) from [Flaticon](https://www.flaticon.com/).

License and copyright of logs loaded into ULogViewer or saved by ULogViewer is not dominated by this User Agreement. You should take care of the license and copyright of logs by yourself.


## Contact Us
If you have any concern of this User Agreement, please create an issue on [GitHub](https://github.com/carina-studio/ULogViewer/issues) or send e-mail to [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com).