---
title: ULogViewer
---

# User Agreement
- Version: 1.1.
- Update: 2021/12/12.

This is the User Agreement of ```ULogViewer``` which you need to read before you using ```ULogViewer```. 
The User Agreement may be updated in the future and you can check it on the website of ```ULogViewer```. 
It means that you have agreed this User Agreement once you start using ```ULogViewer```.

## Scope of User Agreement
```ULogViewer``` is an Open Source Project. The ```ULogViewer``` mentioned after includes **ONLY** the executable files or zipped files which are exact same as the files provided by the following pages:
* [Website of ULogViewer](https://carina-studio.github.io/ULogViewer/)
* [Project and release pages of ULogViewer on GitHub](https://github.com/carina-studio/ULogViewer)

This User Agreement will be applied when you use ```ULogViewer```.

## File Access
Except for system files, all necessary files of ```ULogViewer``` are placed inside the directory of ```ULogViewer``` (include directory of ```.NET Runtime``` if you installed ```.NET``` on your computer). No other file access needed when running ```ULogViewer``` without loading logs except for the followings:

* Read ```/proc/meminfo``` to get physical memory information on Linux.
* Read/Write ```Temporary``` directory of system for placing runtime resources.
* Other necessary file access by ```.NET``` or ```3rd-Party Libraries```.

### File Access When Loading Logs
* The file which contains raw logs will be opened in ```Read``` mode.
* The ```*.ulvmark``` file side-by-side with log file will be opened in ```Read``` mode.

### File Access When Viewing Logs
* The ```*.ulvmark``` file side-by-side with log file will be opened in ```Read/Write``` mode.

### File Access When Saving Logs
* The file which raw logs written to will be opened in ```Read/Write``` mode.
* The ```*.ulvmark``` file side-by-side with log file will be opened in ```Read/Write``` mode.

### File Access When Self Updating
* Downloaded packages and backed-up application files will be placed inside ```Temporary``` directory of system.

Other file access outside from executable of ```ULogViewer``` are not dominated by this User Agreement.

## Network Access
```ULogViewer``` will access network in the following cases:

### Loading Logs through Network
Network access is needed when the source of logs is one of the following:
* ```HTTP/HTTPS```
* ```TCP Server```
* ```UDP Server```
* ```File``` with the file outside from local machine.

### Application Update Checking
```ULogViewer``` downloads manifest from website of ```ULogViewer``` periodically to check whether application update is available or not.

### Self Updating
There are 4 type of data will be downloaded when updating ```ULogViewer```:
* Manifest of auto updater component to check which auto updater is suitable for self updating.
* Manifest of ```ULogViewer``` to check which update package is suitable for self updating.
* Package of auto updater.
* Update package of ```ULogViewer```.

Other network access outside from executable of ```ULogViewer``` are not dominated by this User Agreement.

## External Command Execution
External command execution will happen when the source of logs is ```Standard Output (stdout)```. You can check the list of commands and arguments in the ```Data source options``` dialog when editing ```Data Source``` of log profile.

Please noticed that we **DON'T** guarantee the result of external command execution. It all depends on the behavior of external command and executable which you should take care of.

## Modification of Your Computer
Except for file access, ```ULogViewer``` **WON'T** change the settings of your computer.

Please noticed that we **DON'T** guarantee your computer won't be modified after executing external command. You should take care of it by yourself especially when running ```ULogViewer``` as Administrator on Windows.

## License and Copyright
```ULogViewer``` is an Open Source Project of ```Carina Studio``` under [MIT](https://github.com/carina-studio/ULogViewer/blob/master/LICENSE) license. All icons except for application icon are distributed under [MIT](https://en.wikipedia.org/wiki/MIT_License) or [CC 4.0](https://en.wikipedia.org/wiki/Creative_Commons_license) license. Please refer to [MahApps.Metro.IconPacks](https://github.com/MahApps/MahApps.Metro.IconPacks) for more information of icons and its license.

Application icon is made by [Freepik](https://www.freepik.com/) from [Flaticon](https://www.flaticon.com/).

License and copyright of logs loaded into ```ULogViewer``` or saved by ```ULogViewer``` is not dominated by this User Agreement. You should take care of the license and copyright of logs by yourself.

## Contact Us
If you have any concern of this User Agreement, please create an issue on [GitHub](https://github.com/carina-studio/ULogViewer/issues) or send e-mail to [carina.software.studio@gmail.com](mailto:carina.software.studio@gmail.com).


<br/>📔[Back to Home](index.md)