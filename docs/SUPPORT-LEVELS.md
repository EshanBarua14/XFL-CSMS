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
