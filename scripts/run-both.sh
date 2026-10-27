#!/usr/bin/env bash
# Current version on http://localhost:5200 (CSMS_Old) and the first stable version on
# http://localhost:5201 (CSMS_Original), side by side. Ctrl+C stops both.
set -uo pipefail

DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
"$DIR/run-current.sh" & CURRENT=$!
"$DIR/run-original.sh" v1-original & ORIGINAL=$!
trap 'trap - INT TERM EXIT; kill $CURRENT $ORIGINAL 2>/dev/null; kill 0 2>/dev/null' INT TERM EXIT
wait
