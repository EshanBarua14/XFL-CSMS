# Deploying XFL CSMS (IIS on Windows Server)

Commands are for **Git Bash**. Server paths below use `D:\XFLCSMS` (site) and `D:\XFLCSMS-Data` (data that must survive
every new version). Change them to suit the server.

## 1. One-time server setup

1. **Windows features:** IIS with *WebSocket Protocol* (for live notifications; without it they fall back to polling).
2. **.NET hosting bundle:** install the *ASP.NET Core Hosting Bundle* 6.0 (or 8.0; the app rolls forward to it).
   Restart IIS afterwards: `iisreset` in an admin prompt.
3. **SQL Server:** 2019 or 2022. Create the database (e.g. `CSMS`) or restore the current one.
   The app pool account needs `db_datareader`, `db_datawriter` and, if the app applies migrations itself, `db_ddladmin`.
   With Windows authentication that account is `IIS APPPOOL\XFLCSMS`.
4. **Folders** (give `IIS APPPOOL\XFLCSMS` *Modify*):
   ```
   D:\XFLCSMS              site files (replaced on every release)
   D:\XFLCSMS-Data\Uploads ticket attachments   -> Storage:UploadPath
   D:\XFLCSMS-Data\Keys    data protection keys -> DataProtection:KeysPath
   D:\XFLCSMS-Data\Logs    log files            -> Serilog file path
   ```
   Copy the existing attachments (`wwwroot\Uplods` of the current installation) into `D:\XFLCSMS-Data\Uploads`.
   Old tickets are still found there: the app looks up stored file names in the upload folder.
5. **IIS site / app pool:**
   - App pool `XFLCSMS`: .NET CLR version *No Managed Code*, pipeline *Integrated*.
   - App pool > Advanced: **Start Mode = AlwaysRunning**, **Idle Time-out = 0**, Load User Profile = True.
     The notification worker (e-mails, SLA alerts, escalation due) runs inside the site and stops when the pool sleeps.
   - Site > Advanced: **Preload Enabled = True**.
   - Bindings: HTTPS with the company certificate. Keep HTTP only to redirect.
6. **Secrets:** copy `appsettings.Local.json.example` (in the published folder) to `D:\XFLCSMS\appsettings.Local.json` and fill in:
   connection string, `EmailPassword` (a **new** Gmail app password: the old one is in the git history and must be revoked),
   the three paths above. Restrict the file to Administrators and the app pool account.
7. **First start on an empty database only:** add `SeedAdmin:UserName` / `SeedAdmin:Password` to `appsettings.Local.json`,
   start the site, sign in, change the password, then remove those two lines.

## 2. Build a release (developer PC)

```bash
cd /e/XFL-CSMS-main/XFL-CSMS-main && git pull
./scripts/publish.sh
```
Result in `artifacts/xflcsms-<version>-<commit>/`: `site/` (the files for IIS), `migrations.sql`
(idempotent, safe to run more than once), `COMMIT.txt`, and a zip of all three.
GitHub Actions builds the same thing for every push to `main` (Actions > CI > artifact).

## 3. Release (on the server)

1. **Back up first:** the database and `D:\XFLCSMS-Data`.
2. **Database:** either let the app migrate at start-up (`Database:AutoMigrate`, default `true`), or set it to `false` in
   `appsettings.Local.json` and have the DBA run `migrations.sql` before step 4.
3. Stop the site: put `app_offline.htm` in `D:\XFLCSMS` (IIS then shows it and releases the files).
4. Copy the new files, keeping the server's own ones (Git Bash on the server, or from a share):
   ```bash
   robocopy "/c/release/site" "/d/XFLCSMS" //MIR //XF appsettings.Local.json app_offline.htm //XD logs App_Data
   ```
   (`//` because Git Bash would otherwise turn `/MIR` into a path.) Never use *delete existing files* without these exclusions.
5. Delete `app_offline.htm`. Check:
   ```bash
   curl -s https://csms.example.com/healthz        # Healthy (app + database)
   curl -s https://csms.example.com/healthz/live   # Alive (app only)
   ```
   and the first lines of today's log: `XFL CSMS <version> starting in Production. Data protection keys: ... Attachments: ...`.

**Roll back:** put the previous release's `site/` back the same way. Migrations only add tables / columns, so the older
version keeps working on the newer database; restore the database backup only if a release notes says otherwise.

## 4. Settings reference

| Setting | Default | Notes |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `.\SQLEXPRESS`, `CSMS` | Put the real one in `appsettings.Local.json`. |
| `EmailPassword` | empty | Or set it under Admin > Notifications (stored encrypted). |
| `Storage:UploadPath` | `wwwroot\Uplods` | Set it outside the site folder in production. |
| `DataProtection:KeysPath` | `%ProgramData%\XFLCSMS\keys` (Production) | Encrypts sign-in cookies and the notification secrets. Lose it and every user is signed out and the secrets under Admin > Notifications must be entered again. Back it up. |
| `Serilog:WriteTo:1:Args:path` | `logs/xflcsms-.log` | One file per day, 31 days kept, 50 MB per file. |
| `ReverseProxy:KnownProxies` / `KnownNetworks` | this machine only | Only needed when a proxy / load balancer on *another* machine ends HTTPS. |
| `HttpsRedirection` | `true` outside Development | |
| `Session:IdleMinutes` | 30 | |
| `Database:AutoMigrate` | `true` | `false` when the DBA runs `migrations.sql`. |
| `AllowedHosts` | `*` | Set to the site's host name, e.g. `csms.example.com`. |

Environment variables override files, with `__` for `:` (e.g. `ConnectionStrings__DefaultConnection`).
`ASPNETCORE_ENVIRONMENT` is `Production` in `web.config`; demo data is never created in Production.

## 5. Operations

- **Monitoring:** poll `/healthz` (503 when the database is down). Logs: `D:\XFLCSMS-Data\Logs`. If the site does not
  start at all, set `stdoutLogEnabled="true"` in `web.config`, reproduce, read `logs\stdout*`, and set it back.
- **Backups (daily):** database (full + log as per policy), `D:\XFLCSMS-Data\Uploads`, `D:\XFLCSMS-Data\Keys`.
- **Accounts:** disabling a user or changing their role takes effect within 30 seconds, also for open sessions.
