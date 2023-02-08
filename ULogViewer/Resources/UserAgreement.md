# User Agreement of ULogViewer
 ---
+ Version: 2.0
+ Update: 2023/2/8

This is the User Agreement of ULogViewer which you need to read before you using ULogViewer. The User Agreement may be updated in the future and you can check it on the website of ULogViewer. It means that you have agreed this User Agreement once you start using ULogViewer.


## Scope of User Agreement
ULogViewer is a software based-on Open Source Project. The ULogViewer mentioned after includes **ONLY** the executable files or zipped files which are exact same as the files provided by the following pages:

+ [Website of ULogViewer](https://carinastudio.azurewebsites.net/ULogViewer/)
+ [Project and release pages of ULogViewer on GitHub](https://github.com/carina-studio/ULogViewer)

This User Agreement will be applied when you use ULogViewer 3.0 and any future versions before the version specified in next version of User Agreement.


## Debug Mode
ULogViewer has built-in Debug Mode which is disabled by default. You can enable Debug Mode through **About ULogViewer > Restart in Debug Mode**.


## External Dependencies

### Android SDK
In order to use **'Android Device Log'**, **'Android Device Event Log'** and **'Android Device System Trace'** log profiles, you need to install [Android SDK or Android Studio](https://developer.android.com/studio) on your device first.

### Azure Command-Line Interface (CLI)
In order to use full features of **'Azure CLI'**, **'MySQL Database'** and **'SQL Server Database'** data sources, you need to install [Azure CLI](https://docs.microsoft.com/cli/azure/) on your device first.

### Git
In order to use **'Git Log'** and **'Git Log (Simple)'** log profiles, you need to install [Git](https://git-scm.com/) on your device first.

### Trace Conversion Tool
In order to use **'Android Device System Trace'** built-in log profile on **macOS/Linux**, you need to install [Trace Conversion Tool](https://perfetto.dev/docs/quickstart/traceconv) on your device first.

### Command-Line Tools for Xcode
In order to use **'Apple Device Simulators Log'** and **'Specific Apple Device Simulator Log'** built-in log profile on **macOS**, you need to install [Command-Line Tools for Xcode](https://developer.apple.com/xcode/). If you install Command-Line Tools for Xcode with Xcode, you need to enable it by setting **'Xcode > Settings > Locations > Command Line Tools'** to **'Xcode'**.


## File Access
Except for system files, all necessary files of ULogViewer are placed inside the directory of ULogViewer. No other file access needed when running ULogViewer without loading/importing/saving/exporting data to/from ULogViewer except for the followings:

+ Read **/proc/meminfo** to get physical memory information on Linux.
+ Read **/etc/paths** to get global paths on macOS.
+ Read/Write Temporary directory of system for placing runtime resources.
+ Other necessary file access by .NET or 3rd-Party Libraries.

### Loading Logs
+ The file which contains raw logs will be opened in **Read** mode.
+ The \*.ulvmark file side-by-side with log file will be opened in **Read** mode.

### Viewing Logs
+ The \*.ulvmark file side-by-side with log file will be opened in **Read/Write** mode.

### Saving Logs
+ The file which raw logs written to will be opened in **Read/Write** mode.
+ The \*.ulvmark file side-by-side with log file will be opened in **Read/Write** mode.

### Importing Log Profile
+ The \*.json file of log profile will be opened in **Read** mode.

### Importing Predefined Text Filter
+ The \*.json file of predefined text filter will be opened in **Read** mode.

### Importing Log Analysis Rule Set
+ The \*.json file of log analysis rule set will be opened in **Read** mode.

### Importing Log Analysis Script
+ The \*.json file of log analysis script will be opened in **Read** mode.

### Importing Log Data Source Script
+ The \*.json file of log data source script will be opened in **Read** mode.

### Exporting Log Profile
+ The \*.json file of exported log profile will be opened in **Read/Write** mode.

### Exporting Predefined Text Filter
+ The \*.json file of exported predefined text filter will be opened in **Read/Write** mode.

### Exporting Log Analysis Rule Set
+ The \*.json file of exported log analysis rule set will be opened in **Read/Write** mode.

### Exporting Log Analysis Script
+ The \*.json file of exported log analysis script will be opened in **Read/Write** mode.

### Exporting Log Data Source Script
+ The \*.json file of exported log data source script will be opened in **Read/Write** mode.

### Self Updating
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

### Network Connection Check
ULogViewer contacts with the following servers to check network connection:

+ [Cloudflare](https://www.cloudflare.com/)
+ [Google DNS](https://dns.google/)
+ [OpenDNS](https://www.opendns.com/)

ULogViewer contacts with the following servers to check public [IP address](https://en.wikipedia.org/wiki/IP_address) of device:

+ [https://ipv4.icanhazip.com](https://ipv4.icanhazip.com/)
+ [http://checkip.dyndns.org](http://checkip.dyndns.org/)

### ULogViewer Pro Activation
ULogViewer contacts with server of [Carina Studio](https://carinastudio.azurewebsites.net/) in the following cases:

+ Activating ULogViewer Pro.
+ Using ULogViewer if you have already activated ULogViewer Pro.

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
+ Run **cmd** to update PATH environment variable on Windows if needed.
+ Run **osascript** to update /etc/paths on macOS if needed.interface scale factor on Linux.

Except for necessary cases above, external command execution will happen when the source of logs is **'Azure CLI'** or **'Standard Output (stdout)'**. You can check the list of commands and arguments in the **'Data source'** options dialog when editing **'Data Source'** of log profile.

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

Application icon is modified from icons made by [Freepik](https://www.freepik.com/) from [Flaticon](https://www.flaticon.com/).

Built-in fonts **'Roboto'** and **'Roboto Mono'** are distributed under [Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0), **'IBM Plex Mono'** and **'Source Code Pro'** are distributed under [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL).

License and copyright of logs loaded into ULogViewer or saved by ULogViewer is not dominated by this User Agreement. You should take care of the license and copyright of logs by yourself.


## Contact Us
If you have any concern of this User Agreement, please create an issue on [GitHub](https://github.com/carina-studio/ULogViewer/issues) or send e-mail to [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com).