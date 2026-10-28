#!/usr/bin/env bash
# Adds demo users and tickets to the database of the old version (scripts/run-original.sh, http://localhost:5201).
#
#   scripts/seed-original.sh            # database CSMS_Original on server .
#
# Overrides: CSMS_DB=<database>  CSMS_SERVER=<sql server>
# Needs sqlcmd (installed with SQL Server / SSMS; or "winget install sqlcmd"). Without it, open
# scripts/seed-original.sql in SSMS and Execute it against the database.
# Run the old version once first so its tables exist. Safe to run again.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
SERVER="${CSMS_SERVER:-.}"
DB="${CSMS_DB:-CSMS_Original}"

command -v sqlcmd >/dev/null || { echo "sqlcmd not found. Open scripts/seed-original.sql in SSMS and Execute it against $DB."; exit 1; }

echo "Seeding $DB on $SERVER ..."
# -C: trust the server certificate (newer sqlcmd encrypts by default); -b: stop with an error code on the first error
sqlcmd -S "$SERVER" -E -C -b -d "$DB" -i "$ROOT/scripts/seed-original.sql"
echo "Done. Sign in on http://localhost:5201 with e.g. manager / Demo@12345."
