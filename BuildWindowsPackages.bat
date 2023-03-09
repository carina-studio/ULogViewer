@echo off

set APP_NAME=ULogViewer
set RID_LIST=win-x64 win-x86 win-arm64
set CONFIG=Release-Windows
set FRAMEWORK=net7.0
set SELF_CONTAINED=true
set TRIM_ASSEMBLIES=true
set ERRORLEVEL=0

echo ********** Start building %APP_NAME% **********

REM Create base directory
IF not exist Packages (
    echo Create directory 'Packages'
	mkdir Packages
    if %ERRORLEVEL% neq 0 ( 
        exit
    )
)

REM Get current version
dotnet run --project PackagingTool get-current-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
if %ERRORLEVEL% neq 0 ( 
    del /Q Packages\Packaging.txt
    exit
)
set /p CURRENT_VERSION=<Packages\Packaging.txt
echo Version: %CURRENT_VERSION%

REM Get previous version
dotnet run --project PackagingTool get-previous-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
if %ERRORLEVEL% neq 0 ( 
    del /Q Packages\Packaging.txt
    exit
)
set /p PREVIOUS_VERSION=<Packages\Packaging.txt
if [%PREVIOUS_VERSION%] neq [] (
	echo Previous version: %PREVIOUS_VERSION%
)

REM Create output directory
if not exist Packages\%CURRENT_VERSION% (
    echo Create directory 'Packages\%CURRENT_VERSION%'
    mkdir Packages\%CURRENT_VERSION%
)

REM Build packages
(for %%r in (%RID_LIST%) do (
    REM Start building slf-contained package
    echo .
    echo [%%r]
    echo .

    REM Clear project
    if exist %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\ (
        echo Delete output directory '%APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish'
        rmdir %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish /s /q
    )

    REM Build project
    dotnet publish %APP_NAME% -c %CONFIG% -r %%r --self-contained %SELF_CONTAINED% -p:PublishTrimmed=%TRIM_ASSEMBLIES%
    if %ERRORLEVEL% neq 0 (
        echo Failed to build project: %ERRORLEVEL%
        del /Q Packages\Packaging.txt
        exit
    )
    if exist %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\ULogViewer.png (
        del /Q %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\ULogViewer.png
    )

    REM Generate package
    start /Wait PowerShell -NoLogo -Command Compress-Archive -Force -Path %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\* -DestinationPath Packages\%CURRENT_VERSION%\%APP_NAME%-%CURRENT_VERSION%-%%r.zip
    if %ERRORLEVEL% neq 0 (
        echo Failed to generate package: %ERRORLEVEL%
        del /Q Packages\Packaging.txt
        exit
    )
))

REM Generate diff packages
if [%PREVIOUS_VERSION%] neq [] (
    dotnet run --project PackagingTool create-diff-packages win %PREVIOUS_VERSION% %CURRENT_VERSION%
)

REM Generate package manifest
dotnet run --project PackagingTool create-package-manifest win %APP_NAME% %CURRENT_VERSION%

REM Complete
del /Q Packages\Packaging.txt