#!/bin/bash
set -u

LABEL="com.aerolithe.metashape-helper"
QUEUE_DIR="$HOME/Documents/Aerolithe/metashape-queue"
LOG_DIR="$HOME/Documents/Aerolithe/metashape-helper-logs"

echo "Aerolithe Metashape helper status"
echo "Label: $LABEL"
echo "Command folder: $QUEUE_DIR"
echo "Logs: $LOG_DIR"
echo

if launchctl print "gui/$(id -u)/$LABEL" >/tmp/aerolithe-metashape-helper-status.txt 2>&1; then
  echo "LaunchAgent: loaded"
  sed -n '1,35p' /tmp/aerolithe-metashape-helper-status.txt
else
  echo "LaunchAgent: not loaded"
  cat /tmp/aerolithe-metashape-helper-status.txt
fi

echo
echo "Pending command:"
find "$QUEUE_DIR" -maxdepth 1 -name 'metashape-current.json' -print 2>/dev/null || true

echo
echo "Running command:"
find "$QUEUE_DIR" -maxdepth 1 -name 'metashape-current.json.running' -print 2>/dev/null || true

echo
echo "Failed requests:"
find "$QUEUE_DIR/failed" -maxdepth 1 -name '*.json' -print 2>/dev/null || true

echo
echo "Recent helper log:"
tail -n 40 "$LOG_DIR/helper.log" 2>/dev/null || echo "No helper.log yet."
