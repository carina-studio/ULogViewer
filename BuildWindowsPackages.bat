@echo off

setlocal EnableDelayedExpansion

set APP_NAME=ULogViewer
set DEFAULT_RID_LIST=win-x64 win-x86 win-arm64
set RID_LIST=
set CONFIG=Release
set FRAMEWORK=net10.0
set SELF_CONTAINED=true
set TRIM_ASSEMBLIES=true
set TESTING_MODE_BUILD=false
set GENERATE_DIFF_PACKAGES=true
set RUN_TESTS=true

REM Parse arguments
:parse_arguments
if "%~1"=="" goto arguments_parsed
if /I "%~1"=="-h" goto print_usage_and_exit
if /I "%~1"=="--help" goto print_usage_and_exit
if /I "%~1"=="--rid" goto parse_rid_argument
if /I "%~1"=="--config" (
    if "%~2"=="" (
        echo Missing value of '--config'
        exit /b 1
    )
    set CONFIG=%~2
    shift
    shift
    goto parse_arguments
)
if /I "%~1"=="--no-trim" (
    set TRIM_ASSEMBLIES=false
    shift
    goto parse_arguments
)
if /I "%~1"=="--testing-mode" (
    set TESTING_MODE_BUILD=true
    shift
    goto parse_arguments
)
if /I "%~1"=="--no-tests" (
    set RUN_TESTS=false
    shift
    goto parse_arguments
)
if /I "%~1"=="--no-diff-packages" (
    set GENERATE_DIFF_PACKAGES=false
    shift
    goto parse_arguments
)
echo Unknown argument: %~1
echo.
call :print_usage
exit /b 1

REM Select the runtime identifier to build package for
:parse_rid_argument
if "%~2"=="" (
    echo Missing value of '--rid'
    exit /b 1
)
set IS_RID_SUPPORTED=false
for %%r in (%DEFAULT_RID_LIST%) do (
    if /I "%%r"=="%~2" set IS_RID_SUPPORTED=true
)
if "%IS_RID_SUPPORTED%"=="false" (
    echo Unsupported runtime identifier: %~2
    echo Supported runtime identifiers: %DEFAULT_RID_LIST%
    exit /b 1
)
set IS_RID_SELECTED=false
if not "%RID_LIST%"=="" (
    for %%r in (%RID_LIST%) do (
        if /I "%%r"=="%~2" set IS_RID_SELECTED=true
    )
)
if "%IS_RID_SELECTED%"=="false" (
    if "%RID_LIST%"=="" (
        set RID_LIST=%~2
    ) else (
        set RID_LIST=!RID_LIST! %~2
    )
)
shift
shift
goto parse_arguments

REM Print usage of this script and exit
:print_usage_and_exit
call :print_usage
exit /b 0

REM Print usage of this script
:print_usage
echo Usage: BuildWindowsPackages.bat [options]
echo.
echo Options:
echo   -h, --help           Print this help message and exit.
echo   --config ^<name^>      Build configuration to use. (Default: %CONFIG%)
echo   --rid ^<rid^>          Runtime identifier to build package for, can be specified multiple times.
echo                        Supported: %DEFAULT_RID_LIST%. (Default: all of them)
echo   --no-trim            Do not trim assemblies while publishing the application.
echo   --testing-mode       Build the application in testing mode.
echo   --no-tests           Do not run test cases before building packages.
echo   --no-diff-packages   Do not generate diff packages.
goto :eof

REM Select all runtime identifiers if none of them was specified
:arguments_parsed
if "%RID_LIST%"=="" set RID_LIST=%DEFAULT_RID_LIST%

echo ********** Start building %APP_NAME% **********

REM Run test cases
if /I "%RUN_TESTS%"=="true" (
    echo Run test cases
    dotnet test %APP_NAME%.Tests -c %CONFIG%
    if !ERRORLEVEL! neq 0 (
        echo Test cases failed
        exit /b 1
    )
)

REM Create base directory
IF not exist Packages (
    echo Create directory 'Packages'
	mkdir Packages
    if !ERRORLEVEL! neq 0 (
        exit
    )
)

REM Get current version
dotnet run PackagingTool.cs -- get-current-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
if !ERRORLEVEL! neq 0 (
    del /Q Packages\Packaging.txt
    exit
)
set /p CURRENT_VERSION=<Packages\Packaging.txt
dotnet run PackagingTool.cs -- get-current-informational-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
set /p CURRENT_INFORMATIONAL_VERSION=<Packages\Packaging.txt
set PACKAGE_VERSION=%CURRENT_VERSION%
if not [%CURRENT_INFORMATIONAL_VERSION%] == [] set PACKAGE_VERSION=%CURRENT_INFORMATIONAL_VERSION%
echo Version: %CURRENT_VERSION% (%PACKAGE_VERSION%)

REM Get previous version
if /I not "%GENERATE_DIFF_PACKAGES%"=="true" goto previous_version_checked
dotnet run PackagingTool.cs -- get-previous-version %APP_NAME%\%APP_NAME%.csproj > Packages\Packaging.txt
if !ERRORLEVEL! neq 0 ( 
    del /Q Packages\Packaging.txt
    exit
)
set /p PREVIOUS_VERSION=<Packages\Packaging.txt
if [%PREVIOUS_VERSION%] neq [] (
	echo Previous version: %PREVIOUS_VERSION%
)
:previous_version_checked

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
    dotnet publish %APP_NAME% -c %CONFIG% -r %%r --self-contained %SELF_CONTAINED% -p:PublishTrimmed=%TRIM_ASSEMBLIES% -p:TestingModeBuild=%TESTING_MODE_BUILD%
    if !ERRORLEVEL! neq 0 (
        echo Failed to build project: !ERRORLEVEL!
        del /Q Packages\Packaging.txt
        exit
    )
    if exist %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\ULogViewer.png (
        del /Q %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\ULogViewer.png
    )

    REM Generate package
    start /Wait PowerShell -ExecutionPolicy RemoteSigned -NoLogo -Command Compress-Archive -Force -Path %APP_NAME%\bin\%CONFIG%\%FRAMEWORK%\%%r\publish\* -DestinationPath Packages\%CURRENT_VERSION%\%APP_NAME%-%PACKAGE_VERSION%-%%r.zip
    if !ERRORLEVEL! neq 0 (
        echo Failed to generate package: !ERRORLEVEL!
        del /Q Packages\Packaging.txt
        exit
    )
))

REM Generate diff packages
if /I "%GENERATE_DIFF_PACKAGES%"=="true" (
    if [%PREVIOUS_VERSION%] neq [] (
        dotnet run PackagingTool.cs -- create-diff-packages win %PREVIOUS_VERSION% %CURRENT_VERSION%
    )
)

REM Generate package manifest
REM dotnet run PackagingTool.cs -- create-package-manifest win %APP_NAME% %CURRENT_VERSION%

REM Complete
del /Q Packages\Packaging.txt