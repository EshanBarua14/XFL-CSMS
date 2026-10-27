# XFL CSMS – gap analysis (27 Oct 2026)

Scope: `main` at `7f86a35` (PRs #1–#6 merged). Areas: security and role access, functional, data validation, user experience, reliability and performance, operations.
Each gap lists what was wrong, what was done, and the state: **Fixed**, **Action for you**, or **Deferred** (with the reason).

Test run for this change: 133 / 133 HTTP checks (every role, every list / report / dashboard, filters, exports, CSRF, lockout, scope) and 27 browser checks (no JavaScript errors on 22 pages in light, dark and phone width; Excel / CSV / PDF downloads; column chooser, column search, row details; service worker active; Chrome reports the app installable). Database: SQLite stand-in, because SQL Server cannot be installed in the test sandbox.

## 1. Security and role access

| # | Gap | What was done | State |
|---|---|---|---|
| S1 | The Gmail app password was committed in `appsettings.json`, and the repository is **public**. Anyone can send mail as xpertfintechltd@gmail.com. | Removed from `appsettings.json`. The password now comes from `XFLCSMS/appsettings.Local.json` (git-ignored) or the `EmailPassword` environment variable. The app logs a warning at start-up when it is missing. | **Fixed in code. Action for you:** revoke that app password in the Google account (Security > App passwords), create a new one, and put the new one in `appsettings.Local.json`. Removing it from the file does not remove it from git history. |
| S2 | No CSRF protection on 34 of the 64 POST / DELETE actions, including Delete Ticket, Delete User, Edit User, Change Password and Delete Attachment. A malicious page could make a signed-in admin delete data. | Global `AutoValidateAntiforgeryToken` filter. Forms built with `asp-action` carry the token already. jQuery AJAX and `fetch()` calls get it as a header from the new `wwwroot/js/xfl-csrf.js`. | Fixed |
| S3 | Unlimited password guessing on sign-in. | 5 wrong passwords for one account lock it for 15 minutes; 20 from one address lock that address. Failed sign-ins are logged with the IP. The verify and reset-token forms are throttled the same way. | Fixed |
| S4 | E-mail verification and password-reset tokens were 6 hex characters (16.7 million values), and a reset token was valid for 24 h and found by token alone. A script could take over accounts. | Tokens are now 16 hex characters (64 random bits), a reset is valid for 2 hours, and wrong tries are throttled. | Fixed |
| S5 | "Forgot password" answered "User not found", which showed who is registered. | It gives the same answer whether or not the account exists. | Fixed |
| S6 | Passwords were hashed with one HMAC-SHA512 round, which is fast to brute-force if the database leaks. | PBKDF2-SHA256 with 210,000 iterations and a 32-byte salt. Old hashes still work and are upgraded the next time each user signs in. No reset is needed. | Fixed |
| S7 | Password rules differed: Reset Password and Create User accepted any 6 characters, while Register and Change Password needed 4+ characters from a short list of symbols (`#` was refused). | One rule everywhere (`Models/Register/PasswordRules.cs`): 8+ characters with upper case, lower case, a digit and any symbol. Existing passwords keep working. | Fixed |
| S8 | Leftover code: a JSON API (`UserController`, register / login / reset without e-mail verification) and admin-only scaffolding controllers (`Home` brokerage CRUD and file upload, `Issue`, `DataTable`, `ServerDT`) with a second, unchecked ticket form and upload path. | Removed, with their views. `Home` now only serves the error, status (404 / 400) and offline pages. | Fixed |
| S9 | No browser security headers. Pages could be framed by other sites (click-jacking), and signed-in pages could be cached. | `X-Content-Type-Options`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy`, `Permissions-Policy`, and `Cache-Control: no-store` on HTML pages (`Infrastructure/SecurityHeadersMiddleware.cs`). | Fixed |
| S10 | The session cookie used defaults. | Named cookie, `HttpOnly`, `SameSite=Lax`, `Secure` when served over HTTPS. The anti-forgery cookie uses `SameSite=Strict`. | Fixed |
| S11 | Attachments had no size limit beyond the server's 30 MB request cap. | 10 MB per file. Larger files are refused with the existing "not attached" message. | Fixed |
| S12 | The session id is not changed at sign-in (session fixation). | Not changed. Exploiting it needs a way to plant a cookie first. The proper fix is moving sign-in to ASP.NET cookie authentication, which touches every controller. | Deferred |
| S13 | Log Out is a GET link, so another site can sign a user out. | Not changed: low impact, and it would change every layout's link. | Deferred |
| S14 | `wwwroot/Uplods` in the public repository holds real-looking uploads (CVs, conveyance bills, meeting notes). | Not deleted, because your `CSMS_Old` tickets may still point at them. Recommendation: keep uploads outside the repository (back them up with the database), delete them from git, and purge the history. | **Action for you** |
| S15 | Attachments are checked by extension only. | Not changed: content sniffing needs a library. The folder is not browsable, and downloads go through a session check. | Deferred |
| R1 | Role scope of lists and reports. | Server-side and tested. A Maker sees only tickets they raised, and adding `HouseIds=` to the URL does not widen that. A Support Engineer's report covers tickets assigned to or closed by them, as the spec says. Admin and Support Manager see all tickets. Cross-role URLs redirect to sign-in. | Fixed / verified |
| R2 | Support Engineers can reassign and close any ticket. | Matches the roles spec, so it is unchanged. Say if engineers should only close their own tickets. | By design |

## 2. Functional

| # | Gap | What was done | State |
|---|---|---|---|
| F1 | Ticket Reports had 6 filters, counts only and no charts. The status filter matched by substring, "Employee" matched only the *closed by* name, and Print replaced the whole page. | One report page for every role (`Views/Shared/TicketExplorer.cshtml`). **Filters:** period presets or from / to dates, applied to created, closed or updated date; houses (multi); branch; products (multi); statuses (multi); priorities (multi); support type; category; sub-category; affected section; engineer (or not assigned); SLA state (met / any breach / response breach / resolution breach / at risk); open longer than N days; free text. Filters live in the URL, so a filtered page can be bookmarked or shared. **KPIs:** tickets, open (split by status), closed %, overdue open, High open, unassigned, SLA met % (response and resolution), median resolution, average first response, average client rating. **Charts:** created vs closed trend (per day, week or month), status, priority with SLA met %, backlog by age, product, top houses, engineer with SLA met %, category, and a weekday × hour heat map. **Breakdown tables** per house, branch, product, engineer, category, support type and priority (total, open, closed, breached, SLA met %, average resolution, average rating, share), each sortable and exportable. | Fixed |
| F2 | Ticket lists had 5 columns and searched only a few fields. 16 copy-pasted list pages each loaded the whole Issues table into memory. | Every list (All, Unassigned, Closed, Assigned to Me, My Tickets) uses the same page. The detailed table has 28 fields: ticket, title, status, priority, product, house, branch, raised by, support type, category, sub-category, affected section, engineer, created, assigned, first response, updated (and by), closed (and by), age, response time, resolution time, response SLA, resolution SLA with target, client rating, attachments, and actions. **Columns** shows and hides fields (remembered per page). A search box or list under each heading filters one column. **+** shows every field of a ticket. Rows past their SLA are marked red and rows at risk amber. Copy / CSV / Excel / PDF / Print export the visible columns and rows. Makers get a ★ button on closed tickets they have not rated. | Fixed |
| F3 | Dashboards showed counts and one bar chart. | Each dashboard has an Insights block for 7 / 30 / 90 days or 12 months: 6 KPIs and 6 charts (trend, status, backlog age, priority + SLA, product, houses or categories), with a link to the full report. It works in light and dark mode. The engineer-performance charts are in the separate ratings work. | Fixed |
| F4 | Priority accepted any text posted by the form. | Only High / Medium / Low, case-insensitive (`TicketService.NormalizePriority`). | Fixed |
| F5 | Two tickets saved at the same moment for one house could get the same number. | Number allocation and save are serialised. | Fixed (single server) |
| F6 | Ticket title had no length limit. | 300 characters. | Fixed |
| F7 | The "Unassigned" lists also showed closed tickets. | Only open tickets without an engineer. | Fixed |
| F8 | "Assigned to me" matched the engineer by name, so a renamed engineer lost their tickets. | Matched by user id, with the name as a fallback for old tickets. | Fixed |
| F9 | A ticket does not store its branch: an unused column, `BranchhBranchId`, was never filled. | Reports use the branch of the person who raised the ticket when they belong to the ticket's house. A ticket raised by XFL staff for a house shows no branch. | Partly fixed; storing the branch on the ticket is deferred |
| F10 | One Comments field is overwritten by every edit, so there is no conversation trail. | Not changed: it needs a new table and a new ticket-page design. | Deferred |
| F11 | No e-mail to the engineer on assignment. Closing already e-mails the client, from the ratings work. | Not changed. | Deferred |
| F12 | No audit log of admin changes (users, master data, deletes). | Not changed. | Deferred |

## 3. Data validation

Covered above: priority (F4), title length (F6), password rule (S7), attachment size (S11). Malformed filter values (bad dates, unknown ids) are ignored and no longer crash a page.

## 4. User experience

| # | Gap | What was done | State |
|---|---|---|---|
| U1 | 404 / 400 / 403 showed an empty browser page. | A friendly page with Back and Start buttons. A rejected form (for example an expired anti-forgery token) explains how to reload. | Fixed |
| U2 | The session ended after 10 idle minutes, which lost a half-written ticket. | 30 minutes (`Session:IdleMinutes`), and an open signed-in page keeps the session alive. | Fixed |
| U3 | Not installable; no app icon on PC or phone. | Installable app (PWA): manifest, icons (including a maskable icon for Android), service worker, **Install app** button in the top bar (or bottom right on the sign-in page), Add-to-Home-Screen hint on iPhone, and an offline page. Pages and data always come from the server; only static files are cached. | Fixed |
| U4 | Charts on new pages must follow light / dark mode. | One chart helper (`wwwroot/js/xfl-charts.js`) with a palette that reads on both backgrounds and fixed colours for status and priority. Charts recolour when the theme changes. | Fixed |

## 5. Reliability and performance

| # | Gap | What was done | State |
|---|---|---|---|
| P1 | Lists and reports loaded every ticket into memory and filtered in C#. | Filters run in the database (`Services/TicketReportService.cs`). New indexes on created date and closed date (migration `TicketDateIndexes`, applied at start-up). A page shows at most 10,000 rows; KPIs and charts count every match. | Fixed |
| P2 | `IStatus` and `TNumber` are `nvarchar(max)`, which SQL Server cannot index. | Not changed: altering columns on live data should be planned. | Deferred |
| P3 | No unique constraint on ticket number. | Not added, because your existing data may already hold duplicates and the migration would then fail at start-up. To check: `SELECT TNumber, COUNT(*) FROM Issues GROUP BY TNumber HAVING COUNT(*) > 1`. If that returns nothing, a unique index can be added safely. | Deferred |

## 6. Operations

| # | Gap | What was done | State |
|---|---|---|---|
| O1 | No health check. | `/healthz` answers 200 "Healthy" when the database answers, 503 otherwise. | Fixed |
| O2 | E-mail links used `http://` behind IIS, a proxy or a tunnel that ends HTTPS. | Forwarded headers are honoured. | Fixed |
| O3 | HTTPS redirection was always on, which caused warnings on `http://127.0.0.1:5200`. | On by default outside Development; setting `HttpsRedirection`. | Fixed |
| O4 | A missing mail setting was only discovered when a mail failed. | Warning at start-up. | Fixed |
| O5 | No log files or retention. | Not changed. On IIS, turn on `stdoutLogEnabled` in `web.config`, or add Serilog. | Deferred |
| O6 | Backups. | Recommendation: back up the database and `wwwroot/Uplods` together on a schedule. | Recommendation |

## Merge note

Rebased on `main` after the ratings work (PR #8) was merged: the reports read the SLA targets in minutes (`TicketReportService.SlaAsync()`).
