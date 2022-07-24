APP_NAME="ULogViewer"
RID_LIST=("osx-x64" "osx.11.0-arm64")
PUB_PLATFORM_LIST=("osx-x64" "osx-arm64")
CONFIG="Release"
CERT_NAME="" # Name of certification to sign the application

# Reset output directory
rm -r ./Packages
mkdir ./Packages
if [ "$?" != "0" ]; then
    exit
fi

# Get application version
VERSION=$(cat ./$APP_NAME/$APP_NAME.csproj | grep "<Version>" | egrep -o "[0-9]+(.[0-9]+)+")
if [ "$VERSION" == "" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi
echo " " 
echo "******************** Build $APP_NAME $VERSION ********************"

# Build packages
for i in "${!RID_LIST[@]}"; do
    RID=${RID_LIST[$i]}
    PUB_PLATFORM=${PUB_PLATFORM_LIST[$i]}

    echo " " 
    echo "[$PUB_PLATFORM ($RID)]"
    echo " "

    # clean and restore
    rm -r ./$APP_NAME/bin/$CONFIG/net6.0/$RID
    dotnet clean $APP_NAME
    dotnet restore $APP_NAME
    if [ "$?" != "0" ]; then
        exit
    fi
    
    # build
    dotnet msbuild $APP_NAME -t:BundleApp -property:Configuration=$CONFIG -p:SelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=true -p:RuntimeIdentifier=$RID
    if [ "$?" != "0" ]; then
        exit
    fi

    # create output directory
    mkdir ./Packages/$PUB_PLATFORM
    if [ "$?" != "0" ]; then
        exit
    fi

    # copy .app directory to output directoty
    mv ./$APP_NAME/bin/$CONFIG/net6.0/$RID/publish/$APP_NAME.app ./Packages/$PUB_PLATFORM/$APP_NAME.app
    if [ "$?" != "0" ]; then
        exit
    fi

    # copy application icon and remove unnecessary files
    cp ./$APP_NAME/$APP_NAME.icns ./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/Resources/$APP_NAME.icns
    if [ "$?" != "0" ]; then
        exit
    fi
    rm ./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/libMono*.dylib
    rm ./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.png
    rm ./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/*.pdb

    # sign application
    find "./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/" | while read FILE_NAME; do
        if [[ -f $FILE_NAME ]]; then
            if [[ "$FILE_NAME" != "./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS//$APP_NAME" ]]; then
                echo "Signing $FILE_NAME"
                codesign -f -o runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "$FILE_NAME"
                if [ "$?" != "0" ]; then
                    exit
                fi
            fi
        fi
    done
    codesign -f -o runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "./Packages/$PUB_PLATFORM/$APP_NAME.app/Contents/MacOS/$APP_NAME"
    codesign -f -o runtime --timestamp --entitlements "./$APP_NAME/$APP_NAME.entitlements" -s "$CERT_NAME" "./Packages/$PUB_PLATFORM/$APP_NAME.app"

    # zip .app directory
    ditto -c -k --sequesterRsrc --keepParent "./Packages/$PUB_PLATFORM/$APP_NAME.app" "./Packages/$PUB_PLATFORM/$APP_NAME-$VERSION-$PUB_PLATFORM.zip"

done

