#!/usr/bin/env bash

build_pht() {
    xcodebuild -project pht.xcodeproj -target phtctl -configuration Release CODE_SIGN_IDENTITY="$DEVELOPER" build
    
    mkdir -p "$BUNDLE_PATH/Contents/Library/LaunchDaemons"

    cp build/Release/phtctl "$BUNDLE_PATH/Contents/MacOS/"
    cp build/Release/pht "$BUNDLE_PATH/Contents/Library/LaunchDaemons/com.marmaladeengine.launcher.pht"
    cp pht/com.marmaladeengine.launcher.pht.plist "$BUNDLE_PATH/Contents/Library/LaunchDaemons/"

    codesign --force --options runtime --timestamp --sign "$DEVELOPER" --entitlements phtctl/phtctl.entitlements --identifier com.marmaladeengine.launcher.phtctl "$BUNDLE_PATH/Contents/MacOS/phtctl"
    codesign --force --options runtime --timestamp --sign "$DEVELOPER" --entitlements pht/pht.entitlements --identifier com.marmaladeengine.launcher.pht "$BUNDLE_PATH/Contents/Library/LaunchDaemons/com.marmaladeengine.launcher.pht"
}
