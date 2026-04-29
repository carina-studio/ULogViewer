APP_NAME="ULogViewer"
FRAMEWORK="net10.0"
RID_LIST=("linux-x64" "linux-arm64")
CONFIG="Release"
TRIM_ASSEMBLIES="true"
TESTING_MODE_BUILD="false"
echo "********** Start building $APP_NAME **********"

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
PREV_VERSION=$(dotnet run PackagingTool.cs -- get-previous-version $APP_NAME/$APP_NAME.csproj $VERSION)
if [ ! -z "$PREV_VERSION" ]; then
    echo "Previous version: $PREV_VERSION"
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
if [ ! -z "$PREV_VERSION" ]; then
    dotnet run PackagingTool.cs -- create-diff-packages linux $PREV_VERSION $VERSION
fi

# Generate package manifest
# dotnet run PackagingTool.cs -- create-package-manifest linux $APP_NAME $VERSION