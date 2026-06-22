#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
LAUNCH_AGENTS="$HOME/Library/LaunchAgents"
DOCUMENTS_AEROLITHE="$HOME/Documents/Aerolithe"
QUEUE_DIR="$DOCUMENTS_AEROLITHE/metashape-queue"
LOG_DIR="$DOCUMENTS_AEROLITHE/metashape-helper-logs"
PLIST_NAME="com.aerolithe.metashape-helper.plist"

mkdir -p "$DOCUMENTS_AEROLITHE" "$LAUNCH_AGENTS" "$QUEUE_DIR" "$LOG_DIR"

cp "$SCRIPT_DIR/aerolithe-metashape-helper.sh" "$DOCUMENTS_AEROLITHE/aerolithe-metashape-helper.sh"
chmod 755 "$DOCUMENTS_AEROLITHE/aerolithe-metashape-helper.sh"

cat > "$LAUNCH_AGENTS/$PLIST_NAME" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>com.aerolithe.metashape-helper</string>
    <key>ProgramArguments</key>
    <array>
        <string>/bin/bash</string>
        <string>$DOCUMENTS_AEROLITHE/aerolithe-metashape-helper.sh</string>
    </array>
    <key>WatchPaths</key>
    <array>
        <string>$QUEUE_DIR</string>
    </array>
    <key>StartInterval</key>
    <integer>5</integer>
    <key>RunAtLoad</key>
    <true/>
    <key>StandardOutPath</key>
    <string>$LOG_DIR/launchd.out.log</string>
    <key>StandardErrorPath</key>
    <string>$LOG_DIR/launchd.err.log</string>
</dict>
</plist>
PLIST

launchctl bootout "gui/$(id -u)" "$LAUNCH_AGENTS/$PLIST_NAME" 2>/dev/null || true
launchctl bootstrap "gui/$(id -u)" "$LAUNCH_AGENTS/$PLIST_NAME"
launchctl kickstart -k "gui/$(id -u)/com.aerolithe.metashape-helper"

echo "Aerolithe Metashape helper installed."
echo "Command folder: $QUEUE_DIR"
echo "Logs: $LOG_DIR"
echo
launchctl print "gui/$(id -u)/com.aerolithe.metashape-helper" | sed -n '1,35p'
