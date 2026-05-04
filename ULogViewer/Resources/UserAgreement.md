# ULogViewer User Agreement
 ---
+ Version: 2.6
+ Update: 2026/5/4

This is the ULogViewer User Agreement which you need to read before using ULogViewer. The User Agreement may be updated in the future and you can check it on the ULogViewer website. It means that you have agreed to this User Agreement once you start using ULogViewer.


## User Agreement Scope
ULogViewer is an open-source project of Carina Studio. The ULogViewer mentioned after includes **ONLY** the executable files or zipped files which are exact same as the files provided by the following pages:

+ [ULogViewer Website](https://carinastudio.azurewebsites.net/ULogViewer/)
+ [ULogViewer project and release pages on GitHub](https://github.com/carina-studio/ULogViewer)

If you build ULogViewer from source code, your use of that build is governed solely by the [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) license, not by this User Agreement.

This User Agreement will apply to ULogViewer 2026.0 and any future versions until the version specified in the next User Agreement update.


## Debug Mode
ULogViewer has built-in Debug Mode which is disabled by default. You can enable Debug Mode through **About ULogViewer > Restart in Debug Mode**.


## External Dependencies

### Android SDK Platform Tools
In order to use **'Android Device Log'**, **'Android Device Event Log'**, **'Android Device System Trace'**, **'Android System Memory Monitor'**, **'Android Process Memory Monitor'**, **'Specific Android Device Event Log'**, **'Specific Android Device Log'** and **'Specific Android Device System Trace'** log profiles, you need to install [Android SDK Platform Tools](https://developer.android.com/tools/releases/platform-tools) or [Android Studio](https://developer.android.com/studio) on your device first.

### Azure Command-Line Interface (CLI)
In order to use full features of **'Azure CLI'**, **'MySQL Database'** and **'SQL Server Database'** data sources, you need to install [Azure CLI](https://docs.microsoft.com/cli/azure/) on your device first.

### Git
In order to use **'Git Log'** and **'Git Log (Simple)'** log profiles, you need to install [Git](https://git-scm.com/) on your device first.

### libimobiledevice
In order to use **'Apple Devices Log'** and **'Specific Apple Device Log'** log profiles, you need to install [libimobiledevice](https://libimobiledevice.org/) on your device first.

+ [For Windows User](https://github.com/iFred09/libimobiledevice-windows)
+ [For macOS User](https://formulae.brew.sh/formula/libimobiledevice)
+ [For Linux User](https://command-not-found.com/idevicesyslog)

### Trace Conversion Tool
In order to use **'Android Device System Trace'** and **'Specific Android Device System Trace'** built-in log profile on **macOS/Linux**, you need to install [Trace Conversion Tool](https://perfetto.dev/docs/quickstart/traceconv) on your device first.

### Command-Line Tools for Xcode
In order to use **'Apple Device Simulators Log'** and **'Specific Apple Device Simulator Log'** built-in log profile on **macOS**, you need to install [Command-Line Tools for Xcode](https://developer.apple.com/xcode/). If you install Command-Line Tools for Xcode with Xcode, you need to enable it by setting **'Xcode > Settings > Locations > Command Line Tools'** to **'Xcode'**.


## File Access
Except for system files, all necessary files of ULogViewer are placed inside the ULogViewer directory. On **macOS**, due to app signing requirements, app data is stored in the **Application Support** directory (`~/Library/Application Support/CarinaStudio/ULogViewer/`) rather than inside the app bundle. On **Windows** and **Linux**, app data is stored in the application directory itself. No other file access needed when running ULogViewer without loading/importing/saving/exporting data to/from ULogViewer except for the followings:

+ Read **/proc/meminfo** to get physical memory information on Linux.
+ Read **/etc/paths** to get global paths on macOS.
+ Read/Write system Temporary directory for placing runtime resources.
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
+ Downloaded packages and backed-up application files will be placed inside the system Temporary directory.

### Exporting Application Logs
+ The \*.zip file contains application logs will be opened in **Read/Write** mode.

Other file access outside of the ULogViewer executable are not dominated by this User Agreement.


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

### Application Update Checking
ULogViewer downloads manifest from [GitHub](https://github.com/carina-studio/ULogViewer) periodically to check whether an application update is available.

### Self Updating
There are 4 types of data will be downloaded when updating ULogViewer:

+ Auto updater component manifest to check which auto updater is suitable for self updating.
+ ULogViewer manifest to check which update package is suitable for self updating.
+ Auto updater package.
+ ULogViewer update package.

### Taking Memory Snapshot
[dotMemory](https://www.jetbrains.com/dotmemory/) is the main tool for memory usage analysis by Carina Studio. When you start taking a memory snapshot for the first time in debug mode, all necessary files of [dotMemory](https://www.jetbrains.com/dotmemory/) will be downloaded into the ULogViewer directory.

Other network access outside of the ULogViewer executable are not dominated by this User Agreement.


## External Command Execution
There are some necessary external command execution when running ULogViewer:

+ Run **dotnet** to check the version of .NET installed on device.
+ Run **explorer** to open File Explorer on Windows.
+ Run **open** to open Finder on macOS.
+ Run **defaults** to check system language and theme mode on macOS.
+ Run **nautilus** or **xdg-open** to open File Manager on Linux.
+ Run **cmd** to update PATH environment variable on Windows if needed.
+ Run **osascript** to update /etc/paths on macOS if needed.
+ Run **gsettings** to check system theme mode on Linux.

Except for necessary cases above, external command execution will happen when the source of logs is **'Azure CLI'** or **'Standard Output (stdout)'**. You can check the list of commands and arguments in the **'Data source'** options dialog when editing **'Data Source'** of log profile.

Please note that we **DON'T** guarantee the result of external command execution. It all depends on the behavior of external command and executable which you should take care of.


## Modification of Your Computer
Except for file access and the following cases, ULogViewer **WON'T** change the settings of your computer.

Please note that we **DON'T** guarantee your computer won't be modified after executing external command. You should take care of it by yourself especially when running ULogViewer as Administrator on Windows.

### Editing PATH Environment Variable on Windows

#### Adding Path
All added paths will be set to PATH environment variable of **User** scope.

#### Removing Path
If removed path was listed in PATH environment variable of **Machine** scope, ULogViewer will run cmd command with **Administrator** privilege to update PATH environment variable.

### Editing /etc/paths on macOS
ULogViewer will run **osascript** command with **Administrator** privilege to update /etc/paths file.

### Running Script
Scripts running in ULogViewer are allowed accessing .NET features including file access, network access, computer modification, etc. Therefore, running scripts may modify or even damage your computer. You need to check scripts carefully before running them.


## Disclaimer
ULogViewer is provided **"AS IS"** without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose, and non-infringement. Carina Studio makes no warranty that ULogViewer will meet your requirements or that its operation will be uninterrupted or error-free.

To the fullest extent permitted by applicable law, in no event shall Carina Studio be liable for any direct, indirect, incidental, special, exemplary, or consequential damages (including but not limited to loss of data, loss of profits, or business interruption) arising out of or in connection with the use or inability to use ULogViewer, even if advised of the possibility of such damages.


## License and Copyright
ULogViewer is an open-source project of Carina Studio under [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) license. All icons except for application icon are distributed under [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE), [CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license) or [Universal Multimedia License Agreement for Icons8](https://intercom.help/icons8-7fb7577e8170/en/articles/5534926-universal-multimedia-licensing-agreement-for-icons8) license. Please refer to [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks), [SVG Repo](https://www.svgrepo.com/), [Icons8](https://icons8.com/), [Google Fonts Icons](https://fonts.google.com/icons), [Phosphor Icons](https://phosphoricons.com/) and [Tabler Icons](https://tabler.io/icons) for more information of icons and their licenses.

Built-in fonts **'Roboto'** and **'Roboto Mono'** are distributed under [Apache License 2.0](http://www.apache.org/licenses/LICENSE-2.0), **'IBM Plex Mono'**, **'Noto Sans SC'**, **'Noto Sans TC'** and **'Source Code Pro'** are distributed under [Open Font License](https://scripts.sil.org/cms/scripts/page.php?site_id=nrsi&id=OFL).

License and copyright of logs loaded into ULogViewer or saved by ULogViewer is not dominated by this User Agreement. You should take care of the license and copyright of logs by yourself.


## Contact Us
If you have any concern about this User Agreement, please create an issue on [GitHub](https://github.com/carina-studio/ULogViewer/issues) or send e-mail to [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com).
