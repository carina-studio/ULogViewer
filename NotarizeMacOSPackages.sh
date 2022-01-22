APP_NAME="ULogViewer"
APP_BUNDLE_ID="com.carina-studio.ulogviewer"
RID_LIST=("osx-x64" "osx.11.0-arm64")
PUB_PLATFORM_LIST=("osx-x64" "osx-arm64")
USERNAME="" # Apple Developer ID
PASSWORD="" # Application specific password

# Get application version
VERSION=$(cat ./$APP_NAME/$APP_NAME.csproj | grep "<Version>" | egrep -o "[0-9]+(.[0-9]+)+")
if [ "$VERSION" == "" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi

echo " " 
echo "******************** Notarize $APP_NAME $VERSION ********************"

# Notarize
for i in "${!RID_LIST[@]}"; do
    RID=${RID_LIST[$i]}
    PUB_PLATFORM=${PUB_PLATFORM_LIST[$i]}

    echo " " 
    echo "[$PUB_PLATFORM ($RID)]"
    echo " "

    # notarize
    xcrun altool --notarize-app -f "./Packages/$PUB_PLATFORM/$APP_NAME-$VERSION-$PUB_PLATFORM.zip" --primary-bundle-id "$APP_BUNDLE_ID" -u "$USERNAME" -p "$PASSWORD"
    if [ "$?" != "0" ]; then
        exit
    fi

done