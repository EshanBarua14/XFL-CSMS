#!/usr/bin/env bash
# Runs a frozen earlier version of XFL-CSMS next to the current one, without touching the main checkout.
#
#   scripts/run-original.sh                 # v1-original (first stable version) on http://localhost:5201, DB CSMS_Original
#   scripts/run-original.sh vendor-original # code as handed over by the vendor,  on http://localhost:5202, DB CSMS_Original
#
# Overrides: CSMS_DB=<database>  CSMS_SERVER=<sql server>  CSMS_PORT=<port>
#
# The version is exported (git archive, no second clone) into .original/<tag>/ (git-ignored) and
# built from there, so main keeps working as it is. The old code never alters the schema of a database
# the new code created, but whatever you click in it is written to the database it points at.
set -euo pipefail

TAG="${1:-v1-original}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${CSMS_SERVER:-.}"
DB="${CSMS_DB:-CSMS_Original}"
case "$TAG" in
  vendor-original) PORT="${CSMS_PORT:-5202}" ;;
  *)               PORT="${CSMS_PORT:-5201}" ;;
esac
CONN="Server=$SERVER;Database=$DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"

cd "$ROOT"
if ! git rev-parse -q --verify "refs/tags/$TAG" >/dev/null; then
  git fetch origin "refs/tags/$TAG:refs/tags/$TAG"
fi

DEST="$ROOT/.original/$TAG"
if [ ! -f "$DEST/XFLCSMS/XFLCSMS.csproj" ]; then
  echo "Exporting $TAG to .original/$TAG ..."
  mkdir -p "$DEST"
  git archive "$TAG" | tar -x -C "$DEST"
fi

if [ "$TAG" = "vendor-original" ]; then
  # The vendor's DataContext has the connection string hard-coded to their PC and never creates tables:
  # point it at our database (run v1-original once first on an empty database to create the tables).
  CSCONN="${CONN//\\/\\\\}"   # C# string literal: \ for each backslash (e.g. .\SQLEXPRESS)
  sed -i -E "s/UseSqlServer\(\"[^\"]*\"\)/UseSqlServer(\"$(printf '%s' "$CSCONN" | sed 's/[&/\]/\\&/g')\")/" \
    "$DEST/XFLCSMS/Data/DataContext.cs"
fi

echo "Running $TAG on http://localhost:$PORT  (database $DB on $SERVER). Ctrl+C to stop."
cd "$DEST/XFLCSMS"
# The old appsettings.json holds a live Gmail password: point SMTP at nowhere so the old copy cannot send e-mail.
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://localhost:$PORT" \
ConnectionStrings__DefaultConnection="$CONN" \
EmailHost=127.0.0.1 \
EmailPassword=disabled \
  dotnet run --no-launch-profile
