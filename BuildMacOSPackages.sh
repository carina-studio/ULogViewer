#!/bin/bash

APP_NAME="ULogViewer"
FRAMEWORK="net10.0"
DEFAULT_RID_LIST=("osx-arm64" "osx-x64")
DEFAULT_PUB_PLATFORM_LIST=("osx-arm64" "osx-x64")
RID_LIST=()
PUB_PLATFORM_LIST=()
CONFIG="Release"
TRIM_ASSEMBLIES="true"
TESTING_MODE_BUILD="false"
MACOS_SDK_VERSION="26.0" # Linked SDK version to write into the application binary, opts-in to the window design of macOS 26+
CERT_NAME="" # Name of certification to sign the application
SIGN_PACKAGE="true"
RUN_TESTS="true"

# Print usage of this script.
print_usage() {
    echo "Usage: BuildMacOSPackages.sh [options]"
    echo " "
    echo "Options:"
    echo "  -h, --help        Print this help message and exit."
    echo "  --config <name>   Build configuration to use. (Default: $CONFIG)"
    echo "  --rid <rid>       Runtime identifier to build package for, can be specified multiple times."
    echo "                    Supported: ${DEFAULT_RID_LIST[*]}. (Default: all of them)"
    echo "  --no-sign         Do not sign the built application."
    echo "  --no-trim         Do not trim assemblies while publishing the application."
    echo "  --testing-mode    Build the application in testing mode."
    echo "  --no-tests        Do not run test cases before building packages."
}

# Parse arguments
while [ $# -gt 0 ]; do
    case "$1" in
        -h|--help)
            print_usage
            exit 0
            ;;
        --config)
            if [ -z "$2" ]; then
                echo "Missing value of '--config'"
                exit 1
            fi
            CONFIG="$2"
            shift 2
            ;;
        --rid)
            if [ -z "$2" ]; then
                echo "Missing value of '--rid'"
                exit 1
            fi
            RID_INDEX=-1
            for i in "${!DEFAULT_RID_LIST[@]}"; do
                if [ "${DEFAULT_RID_LIST[$i]}" = "$2" ]; then
                    RID_INDEX=$i
                    break
                fi
            done
            if [ "$RID_INDEX" = "-1" ]; then
                echo "Unsupported runtime identifier: $2"
                echo "Supported runtime identifiers: ${DEFAULT_RID_LIST[*]}"
                exit 1
            fi
            IS_RID_SELECTED="false"
            for SELECTED_RID in "${RID_LIST[@]}"; do
                if [ "$SELECTED_RID" = "$2" ]; then
                    IS_RID_SELECTED="true"
                    break
                fi
            done
            if [ "$IS_RID_SELECTED" = "false" ]; then
                RID_LIST+=("${DEFAULT_RID_LIST[$RID_INDEX]}")
                PUB_PLATFORM_LIST+=("${DEFAULT_PUB_PLATFORM_LIST[$RID_INDEX]}")
            fi
            shift 2
            ;;
        --no-sign)
            SIGN_PACKAGE="false"
            shift
            ;;
        --no-trim)
            TRIM_ASSEMBLIES="false"
            shift
            ;;
        --testing-mode)
            TESTING_MODE_BUILD="true"
            shift
            ;;
        --no-tests)
            RUN_TESTS="false"
            shift
            ;;
        *)
            echo "Unknown argument: $1"
            echo " "
            print_usage
            exit 1
            ;;
    esac
done

# Select all runtime identifiers if none of them was specified
if [ ${#RID_LIST[@]} -eq 0 ]; then
    RID_LIST=("${DEFAULT_RID_LIST[@]}")
    PUB_PLATFORM_LIST=("${DEFAULT_PUB_PLATFORM_LIST[@]}")
fi

echo "********** Start building $APP_NAME **********"

# Run test cases
if [ "$RUN_TESTS" = "true" ]; then
    echo "Run test cases"
    dotnet test $APP_NAME.Tests -c $CONFIG
    if [ "$?" != "0" ]; then
        echo "Test cases failed"
        exit
    fi
fi

# Get application version
VERSION=$(dotnet run PackagingTool.cs -- get-current-version $APP_NAME/$APP_NAME.csproj)
if [ "$?" != "0" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi
INFORMATIONAL_VERSION=$(dotnet run PackagingTool.cs -- get-current-informational-version $APP_NAME/$APP_NAME.csproj)
PACKAGE_VERSION=$VERSION
if [ ! -z "$INFORMATIONAL_VERSION" ]; then
    PACKAGE_VERSION=$INFORMATIONAL_VERSION
fi
echo "Version: $VERSION ($PACKAGE_VERSION)"

# Create output directory
if [[ ! -d "./Packages" ]]; then
    echo "Create directory 'Packages'"
    mkdir ./Packages
    if [ "$?" != "0" ]; then
        exit
    fi
fi
if [[ ! -d "./Packages/$VERSION" ]]; then
    echo "Create directory 'Packages/$VERSION'"
    mkdir ./Packages/$VERSION
    if [ "$?" != "0" ]; then
        exit
    fi
fi

# Build packages
for i in "${!RID_LIST[@]}"; do
    RID=${RID_LIST[$i]}
    PUB_PLATFORM=${PUB_PLATFORM_LIST[$i]}

    echo " " 
    echo "[$PUB_PLATFORM ($RID)]"
    echo " "

    # clean
    rm -r ./$APP_NAME/bin/$CONFIG/$FRAMEWORK/$RID
    dotnet clean $APP_NAME
    dotnet restore $APP_NAME
    if [ "$?" != "0" ]; then
        exit
    fi
    
    # build
    dotnet publish $APP_NAME -c $CONFIG -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=$TRIM_ASSEMBLIES -p:RuntimeIdentifier=$RID -p:TestingModeBuild=$TESTING_MODE_BUILD
    dotnet msbuild $APP_NAME -t:BundleApp -property:Configuration=$CONFIG -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=$TRIM_ASSEMBLIES -p:RuntimeIdentifier=$RID -p:TestingModeBuild=$TESTING_MODE_BUILD
    if [ "$?" != "0" ]; then
        exit
    fi

    # create output directory
    if [[ -d "./Packages/$VERSION/$PUB_PLATFORM" ]]; then
        rm -r ./Packages/$VERSION/$PUB_PLATFORM
    fi
    echo "Create directory 'Packages/$VERSION/$PUB_PLATFORM'"
    mkdir ./Packages/$VERSION/$PUB_PLATFORM
    if [ "$?" != "0" ]; then
        exit
    fi

    # copy .app directory to output directory
    mv ./$APP_NAME/bin/$CONFIG/$FRAMEWORK/$RID/publish/$APP_NAME.app ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app
    if [ "$?" != "0" ]; then
        exit
    fi

    # copy application icon and remove unnecessary files
    cp ./$APP_NAME/$APP_NAME.icns ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/Resources/$APP_NAME.icns
    if [ "$?" != "0" ]; then
        exit
    fi
    rm -rf ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.png
    rm -rf ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.pdb
    rm -rf ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.dSYM

    # [Workaround] Rewrite the linked SDK version of the application binary to opt-in to the window design of macOS 26+.
    # AppKit selects window chrome by the linked SDK version of the main executable, and the .NET apphost is still
    # linked against an old SDK. Must be done before signing, otherwise the signature will be invalidated.
    APP_BINARY="./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/$APP_NAME"
    if [ -z "$(command -v vtool)" ]; then
        echo "Unable to find 'vtool', please install Xcode"
        exit
    fi
    MIN_OS_VERSION=$(vtool -show-build-version "$APP_BINARY" | awk '/minos/ { print $2; exit }')
    if [ -z "$MIN_OS_VERSION" ]; then
        echo "Unable to get minimum OS version from '$APP_BINARY'"
        exit
    fi
    echo "Set linked SDK version of '$APP_BINARY' to $MACOS_SDK_VERSION"
    vtool -set-build-version macos "$MIN_OS_VERSION" "$MACOS_SDK_VERSION" -replace -output "$APP_BINARY" "$APP_BINARY"
    if [ "$?" != "0" ]; then
        exit
    fi

    # sign application
    if [ "$SIGN_PACKAGE" = "true" ]; then
        echo "Sign package 'Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app'"
        codesign --deep --force --options=runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app"
        if [ "$?" != "0" ]; then
            echo "Failed to sign package 'Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app'"
            rm -f "./Packages/$VERSION/$APP_NAME-$PACKAGE_VERSION-$PUB_PLATFORM.zip"
            exit 1
        fi
    else
        echo "Skip signing package 'Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app'"
        codesign --deep --force -s - "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app" # Restore ad-hoc signature invalidated by vtool
    fi

    # zip .app directory
    ditto -c -k --sequesterRsrc --keepParent "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app" "./Packages/$VERSION/$APP_NAME-$PACKAGE_VERSION-$PUB_PLATFORM.zip"
    if [ "$?" != "0" ]; then
        exit
    fi

done

# Generate package manifest
# dotnet run PackagingTool.cs -- create-package-manifest osx $APP_NAME $VERSION