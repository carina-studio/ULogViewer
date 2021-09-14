@echo off

IF [%1] == [] (
	echo No version specified
	exit
)

echo ***** Start building packages %1 *****

IF not exist Packages (
	mkdir Packages
)

echo ***** Windows (x64) *****
dotnet publish ULogViewer -c Release -p:PublishProfile=win-x64
IF %ERRORLEVEL% NEQ 0 ( 
   exit
)

PowerShell -NoLogo -Command Compress-Archive -Force -Path .\ULogViewer\bin\Release\net5.0\publish\win-x64\* -DestinationPath .\Packages\ULogViewer-%1-win-x64.zip

echo ***** Linux (x64) *****
dotnet publish ULogViewer -c Release -p:PublishProfile=linux-x64
IF %ERRORLEVEL% NEQ 0 ( 
   exit
)

PowerShell -NoLogo -Command Compress-Archive -Force -Path .\ULogViewer\bin\Release\net5.0\publish\linux-x64\* -DestinationPath .\Packages\ULogViewer-%1-linux-x64.zip

echo ***** OSX (x64) *****
dotnet publish ULogViewer -c Release -p:PublishProfile=osx-x64
IF %ERRORLEVEL% NEQ 0 ( 
   exit
)

PowerShell -NoLogo -Command Compress-Archive -Force -Path .\ULogViewer\bin\Release\net5.0\publish\osx-x64\* -DestinationPath .\Packages\ULogViewer-%1-osx-x64.zip