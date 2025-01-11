APP_NAME="ULogViewer"
FRAMEWORK="net8.0"
RID_LIST=("linux-x64" "linux-arm64")
CONFIG="Release"
TRIM_ASSEMBLIES="true"
TESTING_MODE_BUILD="false"
PACKAGING_TOOL_PATH="PackagingTool/bin/Release/$FRAMEWORK/CarinaStudio.ULogViewer.Packaging.dll"
echo "********** Start building $APP_NAME **********"

# Build packaging tool
dotnet build PackagingTool -c Release -f $FRAMEWORK
if [ "$?" != "0" ]; then
    exit
fi

# Get application version
VERSION=$(dotnet $PACKAGING_TOOL_PATH get-current-version $APP_NAME/$APP_NAME.csproj)
if [ "$?" != "0" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi
echo "Version: $VERSION"
PREV_VERSION=$(dotnet $PACKAGING_TOOL_PATH get-previous-version $APP_NAME/$APP_NAME.csproj $VERSION)
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
    ditto -c -k --sequesterRsrc "./$APP_NAME/bin/$CONFIG/$FRAMEWORK/$RID/publish/" "./Packages/$VERSION/$APP_NAME-$VERSION-$RID.zip"
    if [ "$?" != "0" ]; then
        exit
    fi

done

# Generate diff packages
if [ ! -z "$PREV_VERSION" ]; then
    dotnet $PACKAGING_TOOL_PATH create-diff-packages linux $PREV_VERSION $VERSION
fi

# Generate package manifest
# dotnet $PACKAGING_TOOL_PATH create-package-manifest linux $APP_NAME $VERSION