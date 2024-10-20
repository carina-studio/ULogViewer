@echo off

set APP_NAME=ULogViewer
set RID_LIST=linux-arm64 linux-x64
set CONFIG=Release
set FRAMEWORK=net9.0
set SELF_CONTAINED=true
set TRIM_ASSEMBLIES=true
set TESTING_MODE_BUILD=false
set PACKAGING_TOOL_PATH=PackagingTool\bin\Release\%FRAMEWORK%\CarinaStudio.ULogViewer.Packaging.dll
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

REM Build packaging tool
dotnet build --project PackagingTool -c Release -f %FRAMEWORK%
if %ERRORLEVEL% neq 0 ( 
    exit
)

REM Get current version
dotnet %PACKAGING_TOOL_PATH% get-current-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
if %ERRORLEVEL% neq 0 ( 
    del /Q Packages\Packaging.txt
    exit
)
set /p CURRENT_VERSION=<Packages\Packaging.txt
echo Version: %CURRENT_VERSION%

REM Get previous version
dotnet %PACKAGING_TOOL_PATH% get-previous-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
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
    dotnet restore %APP_NAME% -r %%r
    if %ERRORLEVEL% neq 0 ( 
        echo Failed to restore project: %ERRORLEVEL%
        del /Q Packages\Packaging.txt
        exit
    )
    dotnet clean %APP_NAME% -c %CONFIG% -r %%r
    if %ERRORLEVEL% neq 0 ( 
        echo Failed to clean project: %ERRORLEVEL%
        del /Q Packages\Packaging.txt
        exit
    )

    REM Build project
    dotnet publish %APP_NAME% -c %CONFIG% -r %%r --self-contained %SELF_CONTAINED% -p:PublishTrimmed=%TRIM_ASSEMBLIES% -p:TestingModeBuild=%TESTING_MODE_BUILD%
    if %ERRORLEVEL% neq 0 (
        echo Failed to build project: %ERRORLEVEL%
        del /Q Packages\Packaging.txt
        exit
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
    dotnet %PACKAGING_TOOL_PATH% create-diff-packages linux %PREVIOUS_VERSION% %CURRENT_VERSION%
)

REM Generate package manifest
REM dotnet %PACKAGING_TOOL_PATH% create-package-manifest linux %APP_NAME% %CURRENT_VERSION%

REM Complete
del /Q Packages\Packaging.txt