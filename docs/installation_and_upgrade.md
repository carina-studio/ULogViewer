---
title: ULogViewer
---

# How to Install and Upgrade ULogViewer

## 💻Installation
Currently ULogViewer is built as portable package, you can just unzip the package and run ULogViewer executable directly without installing .NET runtime environment.

### macOS User
If you want to run ULogViewer on macOS, please do the following steps first:
1. Grant execution permission to ULogViewer. For ex: run command ```chmod 755 ULogViewer``` in terminal.
2. Right click on ULogViewer > ```Open``` > Click ```Open``` on the pop-up window.

You may see that system shows message like ```"XXX.dylib" can't be opened because Apple cannot check it for malicious software``` when you trying to launch ULogViewer. Once you encounter such problem, please follow the steps:
1. Open ```System Preference``` of macOS.
2. Choose ```Security & Privacy``` > ```General``` > Find the blocked library on the bottom and click ```Allow Anyway```.
3. Try launching ULogViewer again.
4. Repeat step 1~3 until all libraries are allowed. 

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

## 📦Upgrade
Currently ULogViewer has no installation package nor auto updater. To upgrade ULogViewer, you just need to extract new package and override all existing files.


<br/>📔[Back to Home](index.md)
