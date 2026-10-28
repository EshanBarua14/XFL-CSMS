# End-to-end workflow check (28 Oct 2026)

Scope: `main` after PR #15. Run over HTTP against SQL Server 2022 (database `CSMS_Old`, demo data), signed in as each
role, with real form posts and anti-forgery tokens.

## Result

| Area | Checks | Result |
|---|---|---|
| Every page | All links crawled per role: Admin 306, Support Manager 63, Support Engineer 54, Maker 44 pages, plus ticket / edit / report pages by id | 0 errors, no server exceptions logged |
| Raise & access | Ticket with attachment, download, other house gets 404, `/Uplods` blocked, anonymous redirect, Maker kept out of Admin | Pass |
| Assign & levels | Assign, escalate L1->L2 to the queue, L3 queue visibility, manager hand-back, below-level assignment | Pass (after fix 2) |
| Development | Stage, ETA, reference, internal note hidden from the client, tracker | Pass |
| Close, rate, reopen | Close without resolution refused, close, rating, review, reply shown to the client, exclude / include, reopen | Pass |
| Administration | Create / view / edit / delete products, support types, categories, sub-categories, sections, houses; branches; users; support levels; SLA targets; notification settings | Pass |
| Accounts | Create user, sign in, edit details, duplicate e-mail, disable (signed out), change password | Pass (after fixes 3, 4) |
| Everyday | To-dos and reports for all roles, notification feed / open / preferences, PWA files, health checks | Pass |
| Registration e-mail | Refused cleanly without `EmailPassword`; sending not testable without the mail account | n/a |

## Fixed

1. **Staff-logged tickets were invisible to the house.** A ticket a Support Manager / Engineer / Admin raised for a house
   was owned by the staff member, so nobody at the house could see, follow, be notified about or rate it. The staff
   Create Ticket form now has **Requested by (house user)**: that user owns the ticket. The status history keeps who logged it.
2. **Below-level assignment was silently ignored.** Edit Ticket saved everything else and kept the old engineer. It is now
   refused with a message (engineer's level, ticket's level, what to do).
3. **Edit User could not change name, e-mail, phone, designation or employee id.** Editable now, with e-mail format and
   duplicate checks.
4. **A disabled / re-roled user kept access for up to 30 s** (account check cache). The cache entry is dropped on save / delete.
5. **The ticket progress bar never showed the closed date and closer** (not passed to the view).

## Added

- **How CSMS works** (`/{Role}/Help`, the ? in the top bar and the user menu): roles, the ticket steps with the signed-in
  role's steps marked, direct links to that role's pages, support levels (live names and hours), development stages,
  ratings and scorecard rules.
- Ticket forms (Create / Edit Ticket) keep an unsent draft in the browser and put it back after a closed tab, an expired
  session or a form the server sent back with an error (`data-xfl-draft`, `wwwroot/js/xfl-ux.js`).
- POST forms show a spinner and cannot be sent twice; read-only fields look read-only; visible keyboard focus; skip link;
  `/` focuses the menu search; one pager style; footer year (`wwwroot/css/xfl-ux.css`).

No database change.
