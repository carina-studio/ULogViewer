APP_NAME="ULogViewer"
APP_BUNDLE_ID="com.carina-studio.ulogviewer"
RID_LIST=("osx-x64" "osx.11.0-arm64")
PUB_PLATFORM_LIST=("osx-x64" "osx-arm64")
USERNAME="" # Apple Developer ID
PASSWORD="" # Application specific password

echo "********** Start notarizing $APP_NAME **********"

# Get application version
VERSION=$(dotnet run --project PackagingTool get-current-version $APP_NAME/$APP_NAME.csproj)
if [ "$?" != "0" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi
echo "Version: $VERSION"

# Notarize
for i in "${!RID_LIST[@]}"; do
    RID=${RID_LIST[$i]}
    PUB_PLATFORM=${PUB_PLATFORM_LIST[$i]}

    echo " " 
    echo "[$PUB_PLATFORM ($RID)]"
    echo " "

    # notarize
    xcrun altool --notarize-app -f "./Packages/$VERSION/$APP_NAME-$VERSION-$PUB_PLATFORM.zip" --primary-bundle-id "$APP_BUNDLE_ID" -u "$USERNAME" -p "$PASSWORD"
    if [ "$?" != "0" ]; then
        exit
    fi

done