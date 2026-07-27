#!/bin/bash

APP_NAME="ULogViewer"
FRAMEWORK="net10.0"
DEFAULT_RID_LIST=("linux-x64" "linux-arm64")
RID_LIST=()
CONFIG="Release"
TRIM_ASSEMBLIES="true"
TESTING_MODE_BUILD="false"
GENERATE_DIFF_PACKAGES="true"
RUN_TESTS="true"

# Print usage of this script.
print_usage() {
    echo "Usage: BuildLinuxPackages.sh [options]"
    echo " "
    echo "Options:"
    echo "  -h, --help           Print this help message and exit."
    echo "  --config <name>      Build configuration to use. (Default: $CONFIG)"
    echo "  --rid <rid>          Runtime identifier to build package for, can be specified multiple times."
    echo "                       Supported: ${DEFAULT_RID_LIST[*]}. (Default: all of them)"
    echo "  --no-trim            Do not trim assemblies while publishing the application."
    echo "  --testing-mode       Build the application in testing mode."
    echo "  --no-tests           Do not run test cases before building packages."
    echo "  --no-diff-packages   Do not generate diff packages."
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
            IS_RID_SUPPORTED="false"
            for SUPPORTED_RID in "${DEFAULT_RID_LIST[@]}"; do
                if [ "$SUPPORTED_RID" = "$2" ]; then
                    IS_RID_SUPPORTED="true"
                    break
                fi
            done
            if [ "$IS_RID_SUPPORTED" = "false" ]; then
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
                RID_LIST+=("$2")
            fi
            shift 2
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
        --no-diff-packages)
            GENERATE_DIFF_PACKAGES="false"
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
if [ "$GENERATE_DIFF_PACKAGES" = "true" ]; then
    PREV_VERSION=$(dotnet run PackagingTool.cs -- get-previous-version $APP_NAME/$APP_NAME.csproj $VERSION)
    if [ ! -z "$PREV_VERSION" ]; then
        echo "Previous version: $PREV_VERSION"
    fi
fi

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

    echo " " 
    echo "[$RID]"
    echo " "

    # clean
    rm -r ./$APP_NAME/bin/$CONFIG/$FRAMEWORK/$RID
    dotnet restore $APP_NAME -r $RID
    if [ "$?" != "0" ]; then
        exit
    fi
    dotnet clean $APP_NAME -c $CONFIG -r $RID
    if [ "$?" != "0" ]; then
        exit
    fi
    
    # build
    dotnet publish $APP_NAME -c $CONFIG -r $RID --self-contained true -p:PublishTrimmed=$TRIM_ASSEMBLIES -p:TestingModeBuild=$TESTING_MODE_BUILD
    if [ "$?" != "0" ]; then
        exit
    fi

    # zip package
    ditto -c -k --sequesterRsrc "./$APP_NAME/bin/$CONFIG/$FRAMEWORK/$RID/publish/" "./Packages/$VERSION/$APP_NAME-$PACKAGE_VERSION-$RID.zip"
    if [ "$?" != "0" ]; then
        exit
    fi

done

# Generate diff packages
if [ "$GENERATE_DIFF_PACKAGES" = "true" ] && [ ! -z "$PREV_VERSION" ]; then
    dotnet run PackagingTool.cs -- create-diff-packages linux $PREV_VERSION $VERSION
fi

# Generate package manifest
# dotnet run PackagingTool.cs -- create-package-manifest linux $APP_NAME $VERSION