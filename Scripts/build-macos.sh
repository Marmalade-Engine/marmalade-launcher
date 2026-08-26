#!/usr/bin/env bash

if [ -f .env ]; then
    source .env
else
    echo "[ERR] Failed to load .env file"
    exit
fi

SKIP_NOTARIZE=false

if [ "$1" == "skipnotarize" ]; then
    echo "[INFO] Skip flag detected. Skipping notarization step..."
    SKIP_NOTARIZE=true
fi

echo "[INFO] Building for arm64"
dotnet publish ../MarmaladeLauncher/MarmaladeLauncher.csproj -c Release -r osx-arm64 --self-contained true

echo "[INFO] Building for x64"
dotnet publish ../MarmaladeLauncher/MarmaladeLauncher.csproj -c Release -r osx-x64 --self-contained true

echo "[INFO] Cleaning up output directory"
rm -rf macos_distribution/

mkdir -p "macos_distribution/$APP_NAME.app/Contents/"{MacOS,Resources,Frameworks}

echo "[INFO] Creating universal binary"
lipo -create \
    "$RELEASE_PATH/osx-arm64/publish/MarmaladeLauncher" \
    "$RELEASE_PATH/osx-x64/publish/MarmaladeLauncher" \
    -output "macos_distribution/$APP_NAME.app/Contents/MacOS/MarmaladeLauncher"

echo "[INFO] Copying frameworks"
for lib in "$RELEASE_PATH/osx-arm64/publish/"*.dylib; do
    # Frameworks have already been lipo'd
    name=${lib##*/}
    cp "$lib" "macos_distribution/$APP_NAME.app/Contents/Frameworks/$name"
done

echo "[INFO] Setting rpath for frameworks"
install_name_tool -add_rpath "@executable_path/../Frameworks" "macos_distribution/$APP_NAME.app/Contents/MacOS/MarmaladeLauncher"

echo "[INFO] Copying assets"
cp -r "$RELEASE_PATH/osx-arm64/publish/Assets" "macos_distribution/$APP_NAME.app/Contents/Resources/"

echo "[INFO] Copying app icon"
cp -r MacOS/AppIcon.icns "macos_distribution/$APP_NAME.app/Contents/Resources/"

echo "[INFO] Copying Info.plist"
cp -r MacOS/Info.plist "macos_distribution/$APP_NAME.app/Contents/"

echo "[INFO] Copying provisioning profile"
cp -r MacOS/Marmalade_Launcher.provisionprofile "macos_distribution/$APP_NAME.app/Contents/embedded.provisionprofile"

BUNDLE_PATH="$(pwd)/macos_distribution/$APP_NAME.app"
pushd ../PrivilegedHelperTool

echo "[INFO] Building PHT"
source build.sh
build_pht

popd

echo "[INFO] Signing executable"
codesign --force --options runtime --timestamp --sign "$DEVELOPER" --entitlements MacOS/MarmaladeLauncher.entitlements "macos_distribution/$APP_NAME.app/Contents/MacOS/MarmaladeLauncher"

echo "[INFO] Signing frameworks"
for lib in "macos_distribution/$APP_NAME.app/Contents/Frameworks/"*.dylib; do
    codesign --force --options runtime --timestamp --sign "$DEVELOPER" "$lib"
done

echo "[INFO] Signing app bundle"
codesign --force --options runtime --timestamp --sign "$DEVELOPER" --entitlements MacOS/MarmaladeLauncher.entitlements "macos_distribution/$APP_NAME.app"

if [ "$SKIP_NOTARIZE" = false ]; then
    echo "[INFO] Notarising app"
    ditto -c -k --keepParent "macos_distribution/$APP_NAME.app" "macos_distribution/$APP_NAME.zip"

    xcrun notarytool submit "macos_distribution/$APP_NAME.zip" --apple-id "$APPLE_EMAIL" --team-id "$APPLE_TEAM" --password "$APPLE_PASSWORD" --wait
    xcrun stapler staple "macos_distribution/$APP_NAME.app"
fi

echo "[INFO] Building pkg"
STAGING_DIR="${TMPDIR}/marmalade-launcher-staging"
INSTALL_LOCATION="/Applications"
IDENTIFIER="com.marmaladeengine.launcher"

rm -rf "${STAGING_DIR}"
mkdir -p "${STAGING_DIR}/${INSTALL_LOCATION}"
cp -r "macos_distribution/$APP_NAME.app" "${STAGING_DIR}/${INSTALL_LOCATION}"

pkgbuild --analyze --root "${STAGING_DIR}" macos_distribution/component.plist
pkgbuild --root "${STAGING_DIR}" --component-plist macos_distribution/component.plist --identifier "${IDENTIFIER}" --version "${VERSION}" "${STAGING_DIR}/tmp-package.pkg"

echo "[INFO] Signing pkg"
productsign --sign "$INSTALLER" "${STAGING_DIR}/tmp-package.pkg" "macos_distribution/$APP_NAME.pkg"

if [ "$SKIP_NOTARIZE" = false ]; then
    echo "[INFO] Notarising pkg"
    xcrun notarytool submit "macos_distribution/$APP_NAME.pkg" --apple-id "$APPLE_EMAIL" --team-id "$APPLE_TEAM" --password "$APPLE_PASSWORD" --wait
    xcrun stapler staple "macos_distribution/$APP_NAME.pkg"
fi
