@echo off

IF [%1] == [] (
	echo No version specified
	exit
)

echo ***** Start building packages %1 *****

IF not exist Packages (
	mkdir Packages
)

SET APP_NAME=ULogViewer

echo ***** Windows (x64) *****
dotnet publish %APP_NAME% -c Release-Windows -p:PublishProfile=win-x64
IF %ERRORLEVEL% NEQ 0 ( 
   exit
)

PowerShell -NoLogo -Command Compress-Archive -Force -Path .\%APP_NAME%\bin\Release\net5.0\publish\win-x64\* -DestinationPath .\Packages\%APP_NAME%-%1-win-x64.zip
PowerShell -NoLogo -Command Get-FileHash .\Packages\%APP_NAME%-%1-win-x64.zip > .\Packages\%APP_NAME%-%1-win-x64.txt

echo ***** Linux (x64) *****
dotnet publish %APP_NAME% -c Release -p:PublishProfile=linux-x64
IF %ERRORLEVEL% NEQ 0 ( 
   exit
)

PowerShell -NoLogo -Command Compress-Archive -Force -Path .\%APP_NAME%\bin\Release\net5.0\publish\linux-x64\* -DestinationPath .\Packages\%APP_NAME%-%1-linux-x64.zip
PowerShell -NoLogo -Command Get-FileHash .\Packages\%APP_NAME%-%1-linux-x64.zip > .\Packages\%APP_NAME%-%1-linux-x64.txt

echo ***** OSX (x64) *****
dotnet publish %APP_NAME% -c Release -p:PublishProfile=osx-x64
IF %ERRORLEVEL% NEQ 0 ( 
   exit
)

PowerShell -NoLogo -Command Compress-Archive -Force -Path .\%APP_NAME%\bin\Release\net5.0\publish\osx-x64\* -DestinationPath .\Packages\%APP_NAME%-%1-osx-x64.zip
PowerShell -NoLogo -Command Get-FileHash .\Packages\%APP_NAME%-%1-osx-x64.zip > .\Packages\%APP_NAME%-%1-osx-x64.txt