# ULogViewer
ULogViewer is a [.NET](https://dotnet.microsoft.com/) based cross-platform universal log viewer written by C# which supports reading and parsing various type of logs.
The project is still under development but most of functions relate to reading/parsing/displaying logs are ready.

## Screenshot (Windows)
<img src="https://carina-studio.github.io/ULogViewer/Screenshot_Windows_Dark_Thumb.png" width="250"/><img src="https://carina-studio.github.io/ULogViewer/Screenshot_Windows_Light_Thumb.png" width="250"/>

## Screenshot (OSX)
<img src="https://carina-studio.github.io/ULogViewer/Screenshot_OSX_Dark_Thumb.png" width="250"/><img src="https://carina-studio.github.io/ULogViewer/Screenshot_OSX_Light_Thumb.png" width="250"/>

## ⭐Log data sources
- Standard Output (stdout)
- Files
- Windows Event Logs (Windows only)
- HTTP (Not yet supported).
- SSL (Not yet supported).

## ⭐Log profiles
Each log profile defines:
- What log data source should be used.
- How to parse log data into structured logs.
- What properties of log should be displayed in the list.
- How to output logs back to text (ex, copying).

Currently there are 7 built-in log profiles:
- Android Device Log.
- Android Log Files.
- Git Log.
- Windows Event Logs (Application/System/Secutiry/Setup).

You can also create, copy or export your own log profiles according to your requirement.

## ⭐Log filtering
Log filtering is the most important feature in ULogViewer which helps you to find and analyze the problem from logs.
You can filter logs by:
- Text filter described by regular expression.
- Level(Priority) of log.
- Process ID of log if available.
- Thread ID of log if available.

For text filter, you can also predefine some filters you may use frequently and filter logs by cobination of these text filters.

## ⭐Log marking
When viewing logs, you can mark some logs which are important for you. There is a separated view to list all marked logs to help you to jump to marked log quickly.
Marked logs will be kept if you are viewing logs from files so that you don't need to mark them again when you open log files next time.

## 📥Download
The latest version is 0.13.0.804.
- 📦[Windows (x64)](https://github.com/carina-studio/ULogViewer/releases/download/0.13.0.804/ULogViewer-0.13.0.804-win-x64.zip)
- 📦[Linux (x64)](https://github.com/carina-studio/ULogViewer/releases/download/0.13.0.804/ULogViewer-0.13.0.804-linux-x64.zip)
- 📦[OSX (x64)](https://github.com/carina-studio/ULogViewer/releases/download/0.13.0.804/ULogViewer-0.13.0.804-osx-x64.zip)

You can also find and download all releases [HERE](https://github.com/carina-studio/ULogViewer/releases).

## 💻Installation
Currently ULogViewer is built as portable package, you can just unzip the package and run ULogViewer executable directly without installing .NET runtime environment.

### Ubuntu User
If you want to run ULogViewer on Ubuntu (also for other Linux distributions), please grant execution permission to ULogViewer first. If you want to create an entry on desktop, please follow the steps:
1. Create a file *(name)*.desktop in ~/.local/share/applications. ex, ~/.local/share/applications/ulogviewer.desktop.
2. Open the .desktop file and put the following content:

```
[Desktop Entry]  
Name=ULogViewer  
Comment=  
Exec=(path to executable)
Icon=(path to AppIcon_128px.png in ULogViewer folder)
Terminal=false  
Type=Application
```

3. After saving the file, you should see the entry shown on desktop or application list.

Reference: [How can I edit/create new launcher items in Unity by hand?
](https://askubuntu.com/questions/13758/how-can-i-edit-create-new-launcher-items-in-unity-by-hand)

### macOS User
If you want to run ULogViewer on macOS, please do the following steps first:
1. Grant execution permission to ULogViewer. For ex: run command ```chmod 755 ULogViewer``` in terminal.
2. Right click on ULogViewer > ```Open``` > Click ```Open``` on the pop-up window.

You may see that system shows message like ```"XXX.dylib" can't be opened because Apple cannot check it for malicious software``` when you trying to launch ULogViewer. Once you encounter such problem, please follow the steps:
1. Open ```System Preference``` of macOS.
2. Choose ```Security & Privacy``` > ```General``` > Find the blocked library on the bottom and click ```Allow Anyway```.
3. Try launching ULogViewer again.
4. Repeat step 1~3 until all libraries are allowed. 

## 📦Upgrade
Currently ULogViewer has no installation package nor auto updater. To upgrade ULogViewer, you just need to extract new package and override all existing files.

## 🤝Dependencies
- [.NET](https://dotnet.microsoft.com/)
- [AppBase](https://github.com/carina-studio/AppBase)
- [Avalonia](https://github.com/AvaloniaUI/Avalonia)
- [Avalonia XAML Behaviors](https://github.com/wieslawsoltes/AvaloniaBehaviors)
- [NLog](https://github.com/NLog/NLog)
- [NUnit](https://github.com/nunit/nunit)
- [ReactiveUI](https://github.com/reactiveui/ReactiveUI)
