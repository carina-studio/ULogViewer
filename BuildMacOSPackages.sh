APP_NAME="ULogViewer"
FRAMEWORK="net10.0"
RID_LIST=("osx-arm64" "osx-x64")
PUB_PLATFORM_LIST=("osx-arm64" "osx-x64")
CONFIG="Release"
TRIM_ASSEMBLIES="true"
TESTING_MODE_BUILD="false"
CERT_NAME="" # Name of certification to sign the application

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
    rm ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.png
    rm ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.pdb

    # sign application
    echo "Sign package 'Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app'"
    codesign --deep --force --options=runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app"

    # zip .app directory
    ditto -c -k --sequesterRsrc --keepParent "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app" "./Packages/$VERSION/$APP_NAME-$PACKAGE_VERSION-$PUB_PLATFORM.zip"
    if [ "$?" != "0" ]; then
        exit
    fi

done

# Generate package manifest
# dotnet run PackagingTool.cs -- create-package-manifest osx $APP_NAME $VERSION