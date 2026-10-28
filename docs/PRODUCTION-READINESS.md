# Production readiness review (28 Oct 2026)

Scope: `main` at `1576fb4` (PRs #1-#14). Earlier findings are in `docs/GAP-ANALYSIS.md`; this review covers the
end-to-end ticket workflow and what is needed to run the app on a server. Deployment steps: `docs/DEPLOYMENT.md`.

**Tested** on SQL Server 2022 (container) with the demo data: migrations from an empty database at start-up; the
idempotent `migrations.sql` on an empty database, on an already migrated one, and twice in a row; the whole ticket
flow over HTTP (16 checks, below); start-up in Production (demo data refused, keys persisted, health checks).

## Ticket workflow

Maker raises -> manager assigns -> engineer works, escalates L1-L5 -> closes -> Maker rates or reopens.

| # | Gap | Fix | Tested |
|---|---|---|---|
| W1 | A ticket could be closed with an empty Comments field, so the client's rating e-mail and ticket page had no resolution. | Closing needs a resolution in Comments (checked in the browser and on the server; nothing is saved otherwise). | ✓ |
| W2 | Staff Edit Ticket stored whatever status was posted (e.g. a crafted `IStatus=Hacked`), which drops the ticket out of every list and report. | Only Open / In Queue / In Progress / Closed, or the ticket's current (legacy) status. | ✓ |
| W3 | An engineer closing an **unassigned** ticket left it without an engineer: the rating, scorecard and "closed by me" counted nobody. | The closing engineer is assigned. | ✓ |
| W4 | A Maker could edit a **closed** ticket (title, details, comments, priority), changing the record the rating refers to, and could overwrite the staff resolution. | Closed tickets are read-only for the Maker (page and POST); the message points to Reopen / new ticket. | ✓ |
| W5 | A disabled user, or one whose role changed, kept full access until the session expired (the role was read once at sign-in). | Each request re-checks the account (cached 30 s); disabled, deleted or re-roled accounts are signed out. | ✓ |
| — | Assign, escalate L1->L2 to the queue, manager hand-back to L1 with an engineer, rating saved for the right engineer, other Maker gets 404. | Already correct. | ✓ |

## Deployment and operations

| # | Gap | Fix |
|---|---|---|
| D1 | Data protection keys were not stored anywhere fixed. On IIS (no user profile) every app-pool recycle signed everybody out and made the notification secrets saved under Admin > Notifications unreadable. | Kept in `DataProtection:KeysPath` (default `%ProgramData%\XFLCSMS\keys`, DPAPI-encrypted on Windows) outside Development. |
| D2 | Attachments lived in `wwwroot\Uplods` inside the site folder: a new release could delete them, and the 62 files in the repository were published to the server. | `Storage:UploadPath` (outside the site); old paths still resolve. `wwwroot\Uplods`, `appsettings.Development.json` (seed passwords) and `appsettings.Local.json` are no longer published. |
| D3 | Logs only went to the console, which IIS discards. | Serilog: daily files, 31 days, 50 MB cap, one line per request, start-up line with version / environment / folders. |
| D4 | Forwarded headers were accepted from **any** client, so anyone could fake their IP (bypassing the sign-in lock-out) or the scheme. | Only proxies on the same machine are trusted by default; others via `ReverseProxy:KnownProxies` / `KnownNetworks`. |
| D5 | No IIS configuration: 30 MB request limit, server / X-Powered-By headers. | `web.config` (in-process, 30 MB, headers removed, Production); Kestrel `Server` header off. |
| D6 | The app needs .NET 6, which is out of support and often missing on new servers. | `RollForward=Major`: runs on .NET 6 if present, otherwise on .NET 8 (no `DOTNET_ROLL_FORWARD` needed). |
| D7 | No release process or SQL script for a DBA. The idempotent script also **failed** on an already migrated database (a migration referenced dropped columns). | `scripts/publish.sh` (site + `migrations.sql` + zip); the migration's update runs through `EXEC`, so the script now runs cleanly any number of times. CI publishes the same artifact. |
| D8 | `SeedDemoData` would create demo accounts with a known password if switched on in production. | Ignored in Production. |
| D9 | Only one health check, which needs the database. | `/healthz` (app + database) and `/healthz/live` (app only). |
| D10 | Unused Swagger package. | Removed. |

## Action for you

1. **Revoke the old Gmail app password** (it is in the git history of a public repository) and put a new one in `appsettings.Local.json` on the server.
2. Tell us the hosting target (assumed: IIS on Windows Server) and the host name, so `AllowedHosts` and HTTPS can be set.
3. Move `wwwroot/Uplods` out of the repository and purge it from history (real-looking client files are public).
4. A server behind a load balancer / proxy on another machine: add its address to `ReverseProxy:KnownProxies`.

## Still open (not changed here)

| Item | Why |
|---|---|
| Single Comments field overwritten by each edit (no conversation trail); no optimistic concurrency, so two people saving the same ticket at once: last save wins. | Needs a comments table and a new ticket page. |
| Session fixation, GET logout (S12, S13). | Needs a move to cookie authentication across every controller. |
| Ticket number uniqueness is per web server (one IIS site is fine; a web farm needs a unique index, see P3). | Existing data may hold duplicates. |
| .NET 6 / EF Core 6 end of support. | Upgrade to .NET 8 is a separate change with its own test pass. |
| Automated tests. | No test project yet; the HTTP flow check above is a script, not part of CI. |
