# XFL CSMS - fixes

What was wrong and what was changed. File names are relative to `XFLCSMS/`.

## 1. Could not build / start on another machine

| Problem | Fix |
|---|---|
| `Controllers/IssueController.cs` had `using NuGet.Protocol;` (unused). It only compiled when a scaffolding package happened to bring NuGet DLLs along. | Removed. |
| The connection string was hard-coded in `Data/DataContext.cs` to the original developer PC (`DESKTOP-21UHJJD\SQLEXPRESS`). | Moved to `appsettings.json` > `ConnectionStrings:DefaultConnection`, wired up in `Program.cs`. |
| A fresh database could not be used: no administrator exists, and registration needs a brokerage house + branch that only an administrator can create. | `Data/DbInitializer.cs`: applies migrations at start-up and, when the Users table is empty, creates the first admin from `SeedAdmin` (see `appsettings.Development.json`). |
| `CSMS.bak` comes from SQL Server 2022; it cannot be restored on SQL Server 2019. | Documented in `read me.txt` (use 2022, or start with an empty database). |

## 2. Bugs that damaged data or gave wrong results

- **Edit Ticket (Admin / Support Manager / Support Engineer) reset the ticket on every save.** The status drop-down was bound to a field the controller never filled, so it always showed "Open": saving any edit re-opened closed tickets and wiped Closed On / Closed By. Assigned On was erased (Admin, Engineer) or re-stamped (Manager) and Approved On re-stamped on every save. Now the stored status and assignee are pre-selected and the dates only change when the assignment or status really changes (`Services/TicketService.cs`).
- **Admin > Edit User demoted staff.** The department drop-down always showed "Maker", so saving a Support Engineer/Manager (e.g. to disable him) turned him into a Maker. "Full Name" showed the user name.
- **Admin > User List > Delete deleted a ticket.** The button called `DeleteTicket` with the *user* id. There is now a real `DeleteUser` (refuses your own account and users who have tickets).
- **Deleting something that is in use logged the admin out.** The foreign-key error went into a catch block that cleared the session; the row also disappeared from the page although nothing was deleted. Deletes now answer "409 - cannot be deleted because ..." and the page shows that message.
- **Every `catch` signed the user out.** About 120 catch blocks removed the session and redirected to the login page on *any* exception, which hid the real error. They now log the exception and show an error page (`CsmsController.HandleError`).
- **Raising a ticket as Support Engineer / Support Manager did nothing.** Their forms posted to the Maker controller (-> login page). Their own actions also crashed on empty details/comments and saved the ticket without a status.
- **Ticket date, number, owner and brokerage house came from editable form fields.** The date was text formatted by the browser's locale (wrong or `0001-01-01` on non-US PCs); the number was reserved when the form was *opened*, so two users could get the same number. All four are now set on the server when the ticket is saved.
- **Attachments.** Two uploads with the same file name overwrote each other; the client file name was used as the path; any file type was accepted; downloads were always sent as `application/pdf`; and the absolute path stored in the database made every download fail after the database was restored on another machine. Files are now stored under a generated name, limited to document/image types, served with the right content type, and looked up in this installation's `wwwroot/Uplods` when the stored path does not exist.
- **Reports.** Maker search threw a NullReferenceException (wrote to `SESearch`); the Support Manager page called a misspelt URL (`/SupportManeger/Search`) and posted to the Admin controller; the Support Engineer Search button was dead (`$ is not defined`); an empty filter crashed; the "To" date excluded that day and a single date was ignored; to-do reports counted status "Completed" although the application stores "Done".
- **To-dos** took the owner from a hidden field and let a user open/update another user's to-do.
- **View Branch / profile branch name** were looked up by brokerage id instead of branch id.
- **Missing pages:** "My Profile" for Support Engineer and Support Manager (404), Maker "Change Password" menu link (empty action), `EditSupportType` was a 400-line copy of the AdminLTE demo page with dead links.
- **Paging:** an empty list showed page 0 of 0 with serial -9; a page number past the end showed an empty table.
- **Forgot password** with a user name tried to send the mail *to the user name*. **Registration** with a failing mail server ended in an error page and left an account that could never be verified; it is now rolled back with a message.

- **Admin > Edit User: choosing Support Manager / Support Engineer did not change the role** unless "User Type" was also set to "XFL User"; the user kept signing in as a Maker but was offered as an engineer on Edit Ticket. User Type now follows the department (as in Create User).
- **Edit Ticket offered disabled engineers** (and non-XFL accounts with department "Support Engineer") in the assign list. Only active XFL support engineers are listed now (`TicketService.AssignableEngineersAsync`).

## 3. Security

- Many actions had no session check at all (report searches, attachment download/delete, and the old scaffolding controllers `Home`, `Issue`, `DataTable`, `ServerDT` - including file upload and ticket delete). All role controllers now inherit `CsmsController`, which requires the role's session; the old controllers are admin-only (`Infrastructure/SessionAuthorizeAttribute.cs`).
- Signing in as a second user in the same browser kept the first user's role. The session is cleared on sign-in and sign-out, and no longer contains the password hash/salt.
- A Maker could open, edit and download any ticket by changing the id in the URL, view any profile, and Change Password trusted a posted user id.
- Ticket details are printed as raw HTML -> stored XSS. They now go through an allow-list sanitizer (`Services/HtmlSanitizer.cs`).
- `wwwroot/Uplods` was readable by URL without signing in; direct access is blocked, downloads go through `DownloadAttachment`.
- Sign-in answered "User not found" / "Password is incorrect" as bare error pages; it now shows one neutral message on the login page.

## 4. Validation

- Registration: confirm password and the terms checkbox were only checked in JavaScript; branch must belong to the chosen organisation; duplicate e-mail / user name shown next to the field.
- Reset password: length and confirmation are enforced.
- Admin create/update forms (brokerage house, branch, support type/category/sub-category, affected section) never checked `ModelState`; empty values went to the database and failed. They now re-display the form with the messages. Brokerage name and acronym must be unique (ticket numbers are built from the acronym).
  Note: the existing rules on those models (letters only; 4-100 characters for support types/categories) are now really enforced on the server.

## 5. Pages / scripts

- Register page loaded a stylesheet from `C:\Users\Lenovo\...`; dashboards used `~plugins/...` (missing slash).
- Links to AdminLTE demo pages (`index2.html`, `examples/...`) removed.
- Script errors: copied validation script on the Edit Ticket pages, `DataTable`/`CKEDITOR`/`bsCustomFileInput` used on pages that do not load them, AdminLTE demo `dashboard.js`, stray `">` printed on the Maker dashboard, Show/Hide buttons on Change Password.
- `Views/Shared/Error.cshtml` used the admin layout and crashed for everybody who was not an admin.

## 6. Not changed - please review

- **`appsettings.json` contains a Gmail app password and it is in the git history.** Revoke it and keep the new one in user-secrets / an environment variable.
- .NET 6 is out of support (since Nov 2024). The project still targets `net6.0` with the same packages.
- Only the Google font still comes from the internet; without it the pages fall back to the system font. Everything else is served from `wwwroot` (section 7).
- Passwords are a single salted HMAC-SHA512 (kept so existing accounts keep working).
- The AJAX delete calls and some POST forms carry no anti-forgery token.
- The old scaffolding controllers/views were kept (admin-only now); `api/UserControllers/*` is still public.

## 7. Look and feel, editor, dark mode

- **Rich-text editor: CKEditor 4.16.2 replaced by Summernote 0.9.1.** CKEditor 4.16.2 is out of support and has published security fixes after it (the warning on every ticket page). Its only maintained 4.x release, 4.25.x-lts, is a commercial build that needs a paid licence key; CKEditor 5 is free only under GPL (or a paid licence) and needs its own build. Summernote is MIT-licensed, maintained, built for Bootstrap 4 / jQuery like the rest of the app, and is served from `wwwroot/lib/summernote` (no CDN). Existing ticket text (HTML) opens and saves unchanged. The server-side sanitizer still cleans everything that is saved. Pasted pictures are limited to 1 MB each (larger files go in as attachments).
- **Ticket View** shows the details as formatted text instead of a read-only editor.
- **Light / dark mode.** A Dark / Light switch is in the top bar of every page (top right corner on the sign-in pages). The choice is remembered in the browser; with no choice yet the Windows / browser setting is used. Charts, tables, forms, dialogs and the editor follow the theme.
- **One set of scripts per page.** Pages used to load jQuery up to three times (3.3.1 slim, 3.5.1, 3.6.0, 3.6.4), Bootstrap 4.3.1 and 5.3.0 JavaScript together, Bootstrap 5 CSS on top of AdminLTE (Bootstrap 4), and DataTables / CKEditor / Chart.js from CDNs. Each reload of jQuery dropped the plugins loaded before it. Every page now loads the same set from `Views/Shared/_XflHead.cshtml`, all from `wwwroot`: jQuery 3.6, Bootstrap 4.6, AdminLTE 3.2, DataTables 1.11 (Bootstrap 4 style), Summernote, Font Awesome; Chart.js 4.6 (`wwwroot/lib/chart.js`) on the dashboards. Unused AdminLTE demo scripts (jqvmap, knob, daterangepicker, tempusdominus, overlayScrollbars, `demo.js`) removed from the dashboards.
- To-do edit dialog used Bootstrap 5 markup (`btn-close`, `data-bs-dismiss`); now Bootstrap 4.
- "Sowing 1 to 10 form 25 items" -> "Showing 1 to 10 of 25 items"; "Shows:" -> "Show:".
- On phones, wide tables scroll sideways inside their card. The registration page scrolls instead of being cut off at the top.
- Removed `Controllers/_SupportEngineerLayout.cshtml` (+ `.css`): an unused old copy of the engineer layout that still referenced CKEditor 4.16.2.

## 8. Page layout, side menu, products

- **One page shell for every signed-in page** (`Views/Shared/_AppLayout.cshtml`). The four role layouts and the four dashboards each had their own copy of the top bar and menu; they now all use the shell. Every page has the same header (title, short description, buttons on the right such as *Create Ticket*, *Add Product*) and the same 24 px margin (14 px on phones), with its content in cards. A page sets `ViewData["Title"]` (and optionally `ViewData["Subtitle"]`) and puts buttons in `@section PageActions { ... }`. Change Password (all roles) now opens inside the app instead of as a separate sign-in-style page.
- **Side menu redesigned** (`Views/Shared/_Sidebar.cshtml`): user card with name, role and a menu (profile, change password, log out), menu search, and real groups that open and close (Tickets, Reports, Users, Setup, To-Do, Team To-Dos) instead of one hidden "PAGES" list. The open page is marked on the server (also on its view / edit pages) and its group starts open. Collapsing the menu (☰) leaves the icons.
- **Sign-in logo**: the logo picture itself had the globe lower than the middle (and cut at the bottom). `wwwroot/img/xfl-logo-centered.png` is the same logo re-centred on a square; the sign-in, registration and password pages and the menu use it, and the logo block is centred.
- **Products.** New table `Products` (migration `AddProducts`, applied at start-up). The 6 products are added when the table is empty: OST, Smart Trade, Xpert Trading OMS, Xpert BO Account Opening Portal, Xpert eKYC, Xpert DMS.
  - Admin > Setup > Products: list (with ticket count), add, view, edit (name, description, display order, active), delete. A product with tickets cannot be deleted; untick *Active* to stop offering it.
  - Create Ticket (all roles): *Product* is required (active products only).
  - Ticket view shows the product; staff can change it on Edit Ticket; the brokerage house user sees it read-only.
  - Ticket lists show a product badge; Ticket Reports can filter by product; Ticket Counts has a *By Product* table (and CSV block).
  - Tickets raised before this change show "(not set)".
- **Setup pages** (brokerage houses, branches, support types, categories, sub-categories, affected sections) rebuilt the same way as Products: list with View / Edit / Delete, add and edit forms in one card with labels, validation messages and Save / Cancel, a details page with Edit / Delete. A branch's brokerage house can now be changed. Saving shows a confirmation.
- Names of support types, categories, sub-categories, branches and brokerage houses may now contain spaces, digits and `. , & ( ) / + - _` (before: letters only, so "Bug Fix" or "Back Office 2" were refused); 2 characters minimum instead of 4.
- Dashboards: Ionicons (from a CDN) replaced by Font Awesome icons served locally.

## New files

`Controllers/CsmsController.cs`, `Infrastructure/SessionAuthorizeAttribute.cs`, `Services/TicketService.cs`,
`Services/PasswordHasher.cs`, `Services/HtmlSanitizer.cs`, `Data/DbInitializer.cs`,
`Views/SupportEngineer/Profile.cshtml`, `Views/SupportManegar/Profile.cshtml`, `Data/DemoDataSeeder.cs` (sample data, Development only),
`Views/Shared/_XflHead.cshtml`, `wwwroot/js/xfl-theme.js`, `wwwroot/lib/summernote/*` (MIT), `wwwroot/lib/chart.js/*` (MIT),
`Views/Shared/_AppLayout.cshtml`, `Views/Shared/_Sidebar.cshtml`, `Views/Shared/_ProductBadge.cshtml`, `Models/Product/Product.cs`,
`Views/Admin/ProductList|CreateProduct|EditProduct|ViewProduct|_ProductFields.cshtml`, `Migrations/20261027092109_AddProducts*.cs`, `wwwroot/img/xfl-logo-centered.png`

## 9. Gap analysis, detailed reports and tables, charts, installable app

Full list of gaps with what was done or why it was deferred: [`docs/GAP-ANALYSIS.md`](docs/GAP-ANALYSIS.md). In short:

- **Security:** anti-forgery check on every POST / DELETE (`wwwroot/js/xfl-csrf.js` adds the token to AJAX / fetch); sign-in lockout after 5 wrong passwords; 64-bit reset / verify tokens valid 2 h; "forgot password" no longer reveals accounts; PBKDF2 password hashing with automatic upgrade at sign-in; one password rule (8+, upper, lower, digit, symbol); browser security headers; 10 MB attachment limit; mail password moved out of `appsettings.json` (now `appsettings.Local.json`, git-ignored); legacy API and scaffolding controllers removed.
- **Tickets and reports:** one page for every ticket list and report of every role (`Views/Shared/TicketExplorer.cshtml`, `Services/TicketReportService.cs`, `wwwroot/js/xfl-explorer.js`) with 15 filters, KPIs, charts, breakdowns and a 28-column table (column chooser, per-column search, row details, Copy / CSV / Excel / PDF / Print).
- **Dashboards:** Insights block with KPIs and charts for 7 / 30 / 90 days / 12 months (`ViewComponents/TicketInsightsViewComponent.cs`).
- **Installable app (PWA):** `wwwroot/manifest.webmanifest`, `wwwroot/sw.js`, `wwwroot/icons/*`, `wwwroot/js/xfl-pwa.js`, offline page. See `read me.txt` section 6 for HTTPS on phones.
- **Other:** priority validated, ticket numbers allocated one at a time, Unassigned lists show open tickets only, "Assigned to me" by user id, friendly 404 page, 30-minute session with keep-alive, `/healthz`, indexes on ticket dates (migration `TicketDateIndexes`).
