# Support levels (L1-L5) and development tracking

## Levels
| Level | Default name | Handles | Time allowed |
|---|---|---|---|
| L1 | Service Desk | first contact, how-to questions, known fixes | 4 h |
| L2 | Technical Support | configuration, access, logs, reproducing | 24 h |
| L3 | Product Specialist | data fixes, integrations, root cause | 72 h |
| L4 | Development | code changes through the development stages | 240 h |
| L5 | Architecture & Vendors | infrastructure, exchange / depository / vendors | no limit |

- **Admin > Setup > Support Levels**: rename the levels, change the time allowed, set each Support Engineer's level. Engineers without a level work at L1; the upgrade sets every existing engineer to L1.
- New tickets start at L1. On the ticket page (**Support level & development** panel) staff move a ticket with a reason.
  Engineers escalate upward only; managers and administrators can also hand back or reassign. Without a chosen engineer the
  current one keeps the ticket when their level is high enough, otherwise it waits in the level's queue.
- Every move is kept: who, when, from / to level and engineer, reason. Clients see the level and the moves, not the reasons.
- A ticket that stays at a level longer than allowed is flagged **escalation due** (ticket page, overview, dashboards) and the
  "Escalation due" notification goes out (Admin > Notifications).
- Edit Ticket's "Assigned To" list is grouped by level, least busy first. Engineers below the ticket's level cannot be chosen (also checked on the server).
- Support Engineers get **My Level Queue**: unassigned open tickets at their level or below.

## Development tracking
Stages: Analysis, Development, Code review, QA / UAT, Ready to deploy, Deployed, plus On hold. A ticket that needs a code
change gets a stage, owner, ETA and reference (CR / branch / PR / build), and every update is kept with an optional note.
Internal notes are hidden from the client and never sent to them.

## Where it shows
- **Support Levels > Levels Overview**: open work per level, queue, escalation due, solved at L1, escalation rate, flow in / out / handed back, average stay, engineers per level, development pipeline, latest moves.
- **Support Levels > Development Tracker**: board by stage with owner, ETA (late in red), level, reference and last note.
- Dashboards (Admin, Support Manager, Support Engineer): level strip with links.
- Ticket lists and reports: Level and Development filters, columns (Level, Escalations, Development, Dev owner, Dev ETA), charts and breakdowns by level and stage.
- Engineer Scorecard: level filter (ranking within the level) and level badges.
- Notifications: "Ticket escalated / handed back", "Development status changed", "Escalation due".

## Ticket assignment
- **Tickets > Ticket Queue** (engineers, managers, administrators): open tickets nobody is assigned to, oldest first.
  Engineers **Take** a ticket (or **Take the next ticket**), managers **Assign** one to an engineer at the ticket's level or above,
  see each engineer's load and who is online, approve transfers and **Run auto-assign now**.
- On the ticket page: **Assign to me**, **Give back to the queue** (with a reason), **Ask another engineer to take it**
  (transfer request: the other engineer accepts or declines, optionally a manager approves). Managers assign / reassign from the same panel.
- **Auto-assign**: every few minutes (and when a ticket is raised, given back, closed or escalated to a queue) each idle engineer
  gets the oldest queued ticket their level may work on. Idle = fewer open tickets than the limit; only engineers online
  (used CSMS recently) when that is switched on. A ticket an engineer gave back or transferred is not given back to them for 24 hours.
  Background assignments are stamped "Auto-assign" in "Approved by".
- **Admin > Setup > Ticket Assignment**: switches and numbers for all of the above (self-assign, give back, status when taken,
  auto-assign on/off, idle limit, interval, which tickets (own level first / own level only / oldest at or below), order
  (oldest / priority first), online only, delay for new tickets, transfers on/off, manager approval, lapse time, capacity check).
- **Admin > Setup > Support Levels > Authority**: per level, what its engineers may do: take tickets, get auto-assigned,
  request transfers, assign others, escalate (and up to which level), hand back, close, most open tickets (0 = no limit).
  Support managers and administrators may always do everything. Checked on the server for the ticket panel, Edit Ticket and the queue.
- Defaults after the upgrade: every level may take, get auto-assigned, request transfers, escalate (up to L5) and close;
  no level may hand back or assign others (as before, engineers moving a ticket to a colleague now use a transfer request).
  Auto-assign is on (idle = no open ticket, online only, every 2 minutes); switch it off under Ticket Assignment if not wanted.
- Notifications: "Transfer requested", "Transfer accepted / declined", "Transfer waiting for approval" (Admin > Notifications); auto-assigned tickets send "Ticket assigned".
