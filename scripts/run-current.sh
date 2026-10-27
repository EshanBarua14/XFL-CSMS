#!/usr/bin/env bash
# Runs the current version (this checkout) on http://localhost:5200 against CSMS_Old.
# Overrides: CSMS_DB=<database>  CSMS_SERVER=<sql server>  CSMS_PORT=<port>
set -euo pipefail
# The app targets .NET 6; DOTNET_ROLL_FORWARD=Major lets it run on a newer runtime (8, 10) when 6 is not installed.

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${CSMS_SERVER:-.}"
DB="${CSMS_DB:-CSMS_Old}"
PORT="${CSMS_PORT:-5200}"

echo "Running current version on http://localhost:$PORT  (database $DB on $SERVER). Ctrl+C to stop."
cd "$ROOT/XFLCSMS"
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://localhost:$PORT" \
ConnectionStrings__DefaultConnection="Server=$SERVER;Database=$DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True" \
DOTNET_ROLL_FORWARD="${DOTNET_ROLL_FORWARD:-Major}" \
  dotnet run --no-launch-profile
