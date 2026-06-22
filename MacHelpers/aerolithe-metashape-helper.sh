#!/bin/bash
set -u

QUEUE_DIR="$HOME/Documents/Aerolithe/metashape-queue"
LOG_DIR="$HOME/Documents/Aerolithe/metashape-helper-logs"
DONE_DIR="$QUEUE_DIR/done"
FAILED_DIR="$QUEUE_DIR/failed"
LOCK_DIR="$QUEUE_DIR/.helper.lock"

mkdir -p "$QUEUE_DIR" "$LOG_DIR" "$DONE_DIR" "$FAILED_DIR"

if ! mkdir "$LOCK_DIR" 2>/dev/null; then
  exit 0
fi
trap 'rmdir "$LOCK_DIR" 2>/dev/null || true' EXIT

log_helper() {
  printf '%s %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*" >> "$LOG_DIR/helper.log"
}

read_json_value() {
  /usr/bin/python3 - "$1" "$2" <<'PY'
import json
import sys

path = sys.argv[1]
key = sys.argv[2]
with open(path, "r", encoding="utf-8") as f:
    data = json.load(f)
print(data.get(key, ""))
PY
}

shopt -s nullglob
for stale_request in "$QUEUE_DIR"/metashape-*.json; do
  if [ "$(basename "$stale_request")" != "metashape-current.json" ]; then
    log_helper "Removing stale pending request $(basename "$stale_request")"
    rm -f "$stale_request"
  fi
done

request_path="$QUEUE_DIR/metashape-current.json"
if [ -f "$request_path" ]; then
  request_name="$(basename "$request_path")"
  running_path="$QUEUE_DIR/metashape-current.json.running"

  if ! mv "$request_path" "$running_path" 2>/dev/null; then
    exit 0
  fi

  request_id="$(read_json_value "$running_path" "request_id" 2>> "$LOG_DIR/helper.log" || true)"
  if [ -z "$request_id" ]; then
    request_id="${request_name%.json}"
  fi
  command_path="$(read_json_value "$running_path" "command_path" 2>> "$LOG_DIR/helper.log" || true)"
  project_name="$(read_json_value "$running_path" "project_name" 2>> "$LOG_DIR/helper.log" || true)"
  log_path="$LOG_DIR/${request_id}.log"

  log_helper "Request $request_id received for $project_name"

  if [ -z "$command_path" ] || [ ! -f "$command_path" ]; then
    log_helper "Request $request_id failed: command_path missing or not found: $command_path"
    mv "$running_path" "$FAILED_DIR/$request_name"
    continue
  fi

  {
    echo "Aerolithe Metashape helper"
    echo "Request: $request_id"
    echo "Project: $project_name"
    echo "Command: $command_path"
    echo "Started: $(date '+%Y-%m-%d %H:%M:%S')"
    echo
    /bin/bash "$command_path"
    status=$?
    echo
    echo "Finished: $(date '+%Y-%m-%d %H:%M:%S')"
    echo "Exit code: $status"
    exit "$status"
  } > "$log_path" 2>&1

  status=$?
  if [ "$status" -eq 0 ]; then
    log_helper "Request $request_id completed"
    mv "$running_path" "$DONE_DIR/metashape-$request_id.json"
  else
    log_helper "Request $request_id failed with exit code $status"
    mv "$running_path" "$FAILED_DIR/metashape-$request_id.json"
  fi
fi
