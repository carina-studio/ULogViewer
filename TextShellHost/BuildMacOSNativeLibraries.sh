LIB_NAME="libTextShellHost.dylib"
TARGET_LIST=("x86_64-apple-macos10.12" "arm64-apple-macos11")
RID_LIST=("osx-x64" "osx.11.0-arm64")

# Build packages
for i in "${!TARGET_LIST[@]}"; do
    TARGET=${TARGET_LIST[$i]}
    RID=${RID_LIST[$i]}

    echo " " 
    echo "[$RID ($TARGET)]"
    echo " "

    if [[ ! -d "bin/Native/$RID" ]]; then
        mkdir -pv bin/Native/$RID
        if [ "$?" != "0" ]; then
            exit
        fi
    fi

    gcc TextShellHost.cpp -dynamiclib -target $TARGET -o bin/Native/$RID/$LIB_NAME
    if [ "$?" != "0" ]; then
        exit
    fi

done