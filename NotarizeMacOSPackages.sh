APP_NAME="ULogViewer"
RID_LIST=("osx-x64" "osx-arm64")
PUB_PLATFORM_LIST=("osx-x64" "osx-arm64")
KEY_ID="" # App Store Connect API Key ID
USERNAME="" # Apple Developer ID
PASSWORD="" # Application specific password
TEAM_ID=""

echo "********** Start notarizing $APP_NAME **********"

# Get application version
VERSION=$(dotnet run PackagingTool.cs -- get-current-version $APP_NAME/$APP_NAME.csproj)
if [ "$?" != "0" ]; then
    echo "Unable to get version of $APP_NAME"
    exit
fi
INFORMATIONAL_VERSION=$(dotnet run PackagingTool.cs -- get-current-informational-version $APP_NAME/$APP_NAME.csproj)
if [ "$?" != "0" ]; then
    echo "Unable to get informational version of $APP_NAME"
    exit
fi
PACKAGE_VERSION=$VERSION
if [ ! -z "$INFORMATIONAL_VERSION" ]; then
    PACKAGE_VERSION=$INFORMATIONAL_VERSION
fi
echo "Version: $VERSION ($PACKAGE_VERSION)"

# Notarize
for i in "${!RID_LIST[@]}"; do
    RID=${RID_LIST[$i]}
    PUB_PLATFORM=${PUB_PLATFORM_LIST[$i]}

    echo " " 
    echo "[$PUB_PLATFORM ($RID)]"
    echo " "

    # notarize
    xcrun notarytool submit "./Packages/$VERSION/$APP_NAME-$PACKAGE_VERSION-$PUB_PLATFORM.zip" --key-id "$KEY_ID" --apple-id "$USERNAME" --password "$PASSWORD" --team-id "$TEAM_ID" --wait
    if [ "$?" != "0" ]; then
        exit
    fi

done