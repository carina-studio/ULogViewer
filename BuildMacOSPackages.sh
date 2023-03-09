APP_NAME="ULogViewer"
RID_LIST=("osx.11.0-arm64" "osx-x64")
PUB_PLATFORM_LIST=("osx-arm64" "osx-x64")
CONFIG="Release"
TRIM_ASSEMBLIES="true"
CERT_NAME="" # Name of certification to sign the application

echo "********** Start building $APP_NAME **********"

# Get application version
VERSION=$(dotnet run --project PackagingTool get-current-version $APP_NAME/$APP_NAME.csproj)
if [ "$?" != "0" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi
echo "Version: $VERSION"

# Create output directory
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
    rm -r ./$APP_NAME/bin/$CONFIG/net7.0/$RID
    dotnet clean $APP_NAME
    dotnet restore $APP_NAME
    if [ "$?" != "0" ]; then
        exit
    fi
    
    # build
    dotnet msbuild $APP_NAME -t:BundleApp -property:Configuration=$CONFIG -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=$TRIM_ASSEMBLIES -p:RuntimeIdentifier=$RID
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

    # copy .app directory to output directoty
    mv ./$APP_NAME/bin/$CONFIG/net7.0/$RID/publish/$APP_NAME.app ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app
    if [ "$?" != "0" ]; then
        exit
    fi

    # copy application icon and remove unnecessary files
    cp ./$APP_NAME/$APP_NAME.icns ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/Resources/$APP_NAME.icns
    if [ "$?" != "0" ]; then
        exit
    fi
    rm ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/libMono*.dylib
    rm ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.png
    rm ./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.pdb

    # sign application
    find "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/" | while read FILE_NAME; do
        if [[ -f $FILE_NAME ]]; then
            if [[ "$FILE_NAME" != "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS//$APP_NAME" ]]; then
                echo "Signing $FILE_NAME"
                codesign -f -o runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "$FILE_NAME"
                if [ "$?" != "0" ]; then
                    exit
                fi
            fi
        fi
    done
    codesign -f -o runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/$APP_NAME"
    codesign -f -o runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app"

    # zip .app directory
    ditto -c -k --sequesterRsrc --keepParent "./Packages/$VERSION/$PUB_PLATFORM/$APP_NAME.app" "./Packages/$VERSION/$APP_NAME-$VERSION-$PUB_PLATFORM.zip"
    if [ "$?" != "0" ]; then
        exit
    fi

done

# Generate package manifest
dotnet run --project PackagingTool create-package-manifest osx $APP_NAME $VERSION