#!/usr/bin/env bash

SRC="src"
EXTERNAL_APIS_DIR="$SRC/ExternalApis"
#see https://github.com/mono/msbuild/blob/xplat-c9/cibuild.sh
MSBUILD_DOWNLOAD_URL="https://github.com/radical/msbuild/releases/download/v0.03/mono_msbuild_d25dd923839404bd64cc63f420e75acf96fc75c4.zip"
MSBUILD_ZIP="$EXTERNAL_APIS_DIR/msbuild.zip"
MSBUILD_EXE="$EXTERNAL_APIS_DIR/MSBuild/msbuild.exe"

downloadMSBuildForMono()
{
    if [ ! -e "$MSBUILD_EXE" ]
    then
        mkdir -p $EXTERNAL_APIS_DIR

        echo "** Downloading MSBUILD from $MSBUILD_DOWNLOAD_URL"
        curl -sL -o $MSBUILD_ZIP "$MSBUILD_DOWNLOAD_URL"

        unzip -q $MSBUILD_ZIP -d $EXTERNAL_APIS_DIR
        find "$EXTERNAL_APIS_DIR/msbuild" -name "*.exe" -exec chmod "+x" '{}' ';'
        rm $MSBUILD_ZIP
    fi
}

downloadMSBuildForMono
