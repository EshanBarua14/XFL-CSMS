#!/usr/bin/env bash
# Builds a release for the server (IIS on Windows): the published site, an idempotent SQL script of the database
# migrations, and a zip of both. Run from Git Bash:  ./scripts/publish.sh            -> artifacts/xflcsms-<version>-<commit>/
# Then follow docs/DEPLOYMENT.md (copy the folder to the server, keep appsettings.Local.json, App_Data and logs).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"
export DOTNET_ROLL_FORWARD=Major   # builds with the .NET 6 SDK or any newer one

VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' XFLCSMS/XFLCSMS.csproj | head -1)"
COMMIT="$(git rev-parse --short HEAD 2>/dev/null || echo local)"
NAME="xflcsms-${VERSION:-0.0.0}-$COMMIT"
OUT="$ROOT/artifacts/$NAME"

rm -rf "$OUT"
mkdir -p "$OUT"

echo "== Publishing $NAME"
dotnet publish XFLCSMS/XFLCSMS.csproj -c Release -o "$OUT/site" -p:InformationalVersion="$VERSION+$COMMIT"

echo "== Database migration script (safe to run more than once)"
dotnet tool restore >/dev/null
dotnet ef migrations script --idempotent --project XFLCSMS/XFLCSMS.csproj --configuration Release \
  --output "$OUT/migrations.sql"

git log -1 --format='%H%n%ad%n%s' > "$OUT/COMMIT.txt" 2>/dev/null || true

if command -v powershell.exe >/dev/null 2>&1; then
  powershell.exe -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$OUT")\\*' -DestinationPath '$(cygpath -w "$OUT").zip' -Force"
elif command -v zip >/dev/null 2>&1; then
  (cd "$OUT" && zip -qr "../$NAME.zip" .)
fi

echo
echo "Done: $OUT"
ls "$ROOT/artifacts" | grep "$NAME"
