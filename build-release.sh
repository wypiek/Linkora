#!/usr/bin/env bash
#
# Builds Linkora releases: a self-contained client for each operating system, and one portable
# server that runs on all of them.
#
# The clients need no .NET runtime installed on the target machine. The server needs one (.NET 6
# or newer), which is what lets a single build serve every operating system. The Windows clients
# run on Windows 7 SP1 and every later version. Results land in dist/.
#
# Usage:  ./build-release.sh [target ...]
#         ./build-release.sh                 # every client and the server
#         ./build-release.sh osx-arm64       # just one client
#         ./build-release.sh server          # just the server
#
# Only the targets being built are replaced; anything else already in dist/ is left alone.
#
set -euo pipefail

cd "$(dirname "$0")"

DEFAULT_TARGETS=(osx-arm64 osx-x64 linux-x64 linux-arm64 win-x64 win-x86 server)
TARGETS=("${@:-}")

if [ -z "${TARGETS[0]:-}" ]; then
    TARGETS=("${DEFAULT_TARGETS[@]}")
fi

DIST="dist"

# Finder can recreate .DS_Store while the tree is being deleted, which makes a single
# rm -rf fail with "Directory not empty"; sweep those out and retry before giving up.
clear_dir() {
    local dir="$1"

    rm -rf "$dir" 2>/dev/null || true

    if [ -d "$dir" ]; then
        find "$dir" -name '.DS_Store' -delete 2>/dev/null || true
        rm -rf "$dir" 2>/dev/null || true
    fi

    if [ -d "$dir" ]; then
        echo "Could not clear $dir - close anything using it and try again." >&2
        exit 1
    fi
}

mkdir -p "$DIST"

APP_NAME="Linkora"
BUNDLE_ID="net.linkora.client"
VERSION="1.1.0"

# Stamped into the binaries and shown at the foot of the client's window.
BUILD_STAMP="$(date +%Y%m%d)"
INFORMATIONAL_VERSION="${VERSION}+build.${BUILD_STAMP}"

publish_client() {
    local rid="$1" out="$2"
    local extra=()

    # .NET 8 does not run on Windows 7, so Windows builds use .NET 6 - the last release that does.
    # See Directory.Build.props.
    case "$rid" in
        win-*) extra+=(-p:Windows7=true) ;;
    esac

    dotnet publish WLMClient/WLMClient.csproj \
        --configuration Release \
        --runtime "$rid" \
        --self-contained true \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -p:InformationalVersion="$INFORMATIONAL_VERSION" \
        ${extra[@]+"${extra[@]}"} \
        --output "$out" \
        --nologo --verbosity quiet
}

# The one server, for every operating system. There is no such thing as a single native
# executable for several operating systems, so this is framework-dependent: it needs a .NET
# runtime installed and is started with "dotnet WLMServer.dll".
#
# Built against .NET 6 and allowed to roll forward to any later major version, so it starts on
# whatever runtime is installed - .NET 6 on Windows 7, or .NET 8, 9, 10 anywhere else. No native
# launcher is made, since it would only work on the OS doing the building.
publish_server() {
    local out="$1"

    dotnet publish WLMServer/WLMServer.csproj \
        --configuration Release \
        -p:Windows7=true \
        -p:RollForward=Major \
        -p:UseAppHost=false \
        -p:DebugType=none \
        -p:DebugSymbols=false \
        -p:InformationalVersion="$INFORMATIONAL_VERSION" \
        --output "$out" \
        --nologo --verbosity quiet
}

# Wraps a published client folder into a double-clickable macOS .app bundle.
make_app_bundle() {
    local publish_dir="$1" bundle="$2" icon_source="$3"

    rm -rf "$bundle"
    mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"

    cp -R "$publish_dir"/* "$bundle/Contents/MacOS/"

    if [ -f "$icon_source" ]; then
        cp "$icon_source" "$bundle/Contents/Resources/AppIcon.png"
    fi

    cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>${APP_NAME}</string>
    <key>CFBundleDisplayName</key><string>${APP_NAME}</string>
    <key>CFBundleIdentifier</key><string>${BUNDLE_ID}</string>
    <key>CFBundleVersion</key><string>${VERSION}</string>
    <key>CFBundleShortVersionString</key><string>${VERSION}</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleExecutable</key><string>WLMClient</string>
    <key>CFBundleIconFile</key><string>AppIcon.png</string>
    <key>LSMinimumSystemVersion</key><string>11.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

    chmod +x "$bundle/Contents/MacOS/WLMClient"
}

for name in "${TARGETS[@]}"; do
    target="$DIST/$name"

    clear_dir "$target"
    mkdir -p "$target"

    if [ "$name" = "server" ]; then
        echo "==> Building the server (every OS)"
        publish_server "$target"

        cat > "$target/README-server.txt" <<'TXT'
The Linkora server. One build for every operating system.

The same files run on macOS, Linux and Windows (7 SP1 and later), on Intel/AMD
and ARM alike. They need a .NET runtime installed - .NET 6 or any newer one:

    https://dotnet.microsoft.com/download

    Windows 7 and 8.1 can only run .NET 6, so install the .NET 6 runtime there.
    Windows 7 also needs update KB3063858 first:
        https://www.microsoft.com/download/details.aspx?id=47409  (32-bit)
        https://www.microsoft.com/download/details.aspx?id=47442  (64-bit)

Edit Messenger.config in this folder, then start the server from here:

    dotnet WLMServer.dll

On Linux the runtime also needs OpenSSL for the MySQL connection:

    Debian/Ubuntu:  sudo apt install libssl3
    Fedora:         sudo dnf install openssl-libs

A single running server serves macOS, Linux and Windows clients at the same
time - they all speak the same protocol, so you never need more than one.
TXT

        echo "    done: $target"
        continue
    fi

    echo "==> Building the client for $name"
    publish_client "$name" "$target/client"

    case "$name" in
        osx-*)
            echo "    packaging .app bundle..."
            make_app_bundle "$target/client" "$target/${APP_NAME}.app" \
                "WLMClient/Content/Icons/33.png"
            rm -rf "$target/client"

            # macOS quarantines unsigned downloads; note the workaround next to the build.
            cat > "$target/README-macos.txt" <<'TXT'
The app is not code signed, so macOS Gatekeeper will refuse to open it on first run.

Clear the quarantine flag once, then open it normally:

    xattr -dr com.apple.quarantine "Linkora.app"

Alternatively: right click the app, choose Open, then confirm.

Settings live in Contents/MacOS/Messenger.config inside the bundle.

The server is a separate download that runs on every OS; see dist/server.
TXT
            ;;
        linux-*)
            chmod +x "$target/client/WLMClient"

            cat > "$target/README-linux.txt" <<'TXT'
Run the client with ./client/WLMClient.

It needs a desktop session and the usual X11/Wayland client libraries plus a
font:

    Debian/Ubuntu:  sudo apt install libx11-6 libice6 libsm6 libfontconfig1 fonts-dejavu-core
    Fedora:         sudo dnf install libX11 libICE libSM fontconfig dejavu-sans-fonts

Notification sounds use paplay, aplay, pw-play or ffplay, whichever is present.

The server is a separate download that runs on every OS; see dist/server.
TXT
            ;;
        win-*)
            cat > "$target/README-windows.txt" <<'TXT'
Run the client with client\WLMClient.exe.

It runs on Windows 7 SP1, 8, 8.1, 10 and 11. win-x86 is the 32-bit build and
runs on both 32-bit and 64-bit Windows; win-x64 needs 64-bit Windows.

Windows 7 and 8.1 need one update before the first run, otherwise the program
closes straight away or complains about a missing DLL:

    Windows 7 SP1:  KB3063858  (or the older KB2533623)
                    https://www.microsoft.com/download/details.aspx?id=47409  (32-bit)
                    https://www.microsoft.com/download/details.aspx?id=47442  (64-bit)

If it still reports api-ms-win-crt-runtime-l1-1-0.dll as missing, also install
the Microsoft Visual C++ 2015-2019 Redistributable:

    https://aka.ms/vs/16/release/vc_redist.x86.exe  (32-bit)
    https://aka.ms/vs/16/release/vc_redist.x64.exe  (64-bit)

Windows 10 and 11 need nothing extra.

This build uses .NET 6, the last .NET that runs on Windows 7. Microsoft no
longer ships security fixes for it, which is the price of Windows 7 support.

Settings live in client\Messenger.config.

The server is a separate download that runs on every OS; see dist/server.
TXT
            ;;
    esac

    echo "    done: $target"
done

echo
echo "Build complete. Output:"
du -sh "$DIST"/* 2>/dev/null || true
