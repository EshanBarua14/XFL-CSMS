using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Tiers;

namespace XFLCSMS.Services
{
    /// <summary>
    /// Support levels L1 to L5 and development tracking: who works at which level, moving a ticket between levels
    /// (escalation with a reason, hand back, reassignment) and the development stage, owner and ETA of tickets that
    /// need a code change. Also builds the Support Levels overview and the Development Tracker.
    /// </summary>
    public class TierService
    {
        public const string EngineerDepartment = "Support Engineer";

        private readonly DataContext _db;

        public TierService(DataContext db)
        {
            _db = db;
        }

        /// <summary>The five levels in order. Rows missing from the database (deleted by hand) come back with their defaults.</summary>
        public async Task<List<SupportLevel>> LevelsAsync()
        {
            var rows = await _db.SupportLevels.AsNoTracking().ToListAsync();
            return SupportLevel.Defaults
                .Select(d => rows.FirstOrDefault(r => r.Level == d.Level) ?? new SupportLevel { Level = d.Level, Name = d.Name, Description = d.Description, MaxHoursAtLevel = d.MaxHoursAtLevel })
                .ToList();
        }

        private IQueryable<User> EngineerQuery() => _db.Users.AsNoTracking().Where(u => u.Department == EngineerDepartment && u.UType);

        /// <summary>Support Engineers with their level and open tickets, highest level first within each name.</summary>
        public async Task<List<EngineerOption>> EngineersAsync(bool activeOnly = true)
        {
            var query = EngineerQuery();
            if (activeOnly) { query = query.Where(u => u.UStatus); }
            var users = await query.Select(u => new { u.Id, u.FullName, u.SupportLevel, u.UStatus }).ToListAsync();
            var load = await OpenLoadAsync();
            return users
                .Select(u => new EngineerOption { Id = u.Id, Name = u.FullName, Level = SupportLevel.Clamp(u.SupportLevel), OpenTickets = load.GetValueOrDefault(u.Id), Active = u.UStatus })
                .OrderBy(e => e.Level).ThenBy(e => e.Name)
                .ToList();
        }

        /// <summary>XFL staff who can own development work (engineers, managers, administrators).</summary>
        public async Task<List<EngineerOption>> DevOwnersAsync()
        {
            var users = await _db.Users.AsNoTracking().Where(u => u.UType && u.UStatus)
                .Select(u => new { u.Id, u.FullName, u.SupportLevel, u.Department }).ToListAsync();
            var load = await _db.Issues.AsNoTracking()
                .Where(i => i.DevOwnerUserId != null && i.DevStage != null && i.DevStage != DevStages.Deployed)
                .GroupBy(i => i.DevOwnerUserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
            return users
                .Select(u => new EngineerOption { Id = u.Id, Name = u.FullName, Level = u.Department == EngineerDepartment ? SupportLevel.Clamp(u.SupportLevel) : 0, OpenTickets = load.GetValueOrDefault(u.Id) })
                .OrderBy(e => e.Name)
                .ToList();
        }

        /// <summary>Open tickets per assigned engineer.</summary>
        private Task<Dictionary<int, int>> OpenLoadAsync()
        {
            return _db.Issues.AsNoTracking()
                .Where(i => i.IStatus != "Close" && i.AssignedToUserId != null)
                .GroupBy(i => i.AssignedToUserId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
        }

        private async Task<User?> AssigneeAsync(IssueTable issue)
        {
            if (issue.AssignedToUserId != null)
            {
                return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == issue.AssignedToUserId);
            }

            if (string.IsNullOrWhiteSpace(issue.AssignBy))
            {
                return null;
            }

            return await _db.Users.AsNoTracking().Where(u => u.FullName == issue.AssignBy)
                .OrderByDescending(u => u.Department == EngineerDepartment).ThenBy(u => u.Id).FirstOrDefaultAsync();
        }

        /// <summary>
        /// Moves a ticket to another level and / or engineer and records why. Without a chosen engineer the current one
        /// keeps the ticket when they work at the new level or above; otherwise the ticket waits unassigned in the new
        /// level's queue. Engineers may only escalate upward; handing back needs a manager or an administrator.
        /// Returns an error text, or null when the ticket was moved (the caller saves).
        /// </summary>
        public async Task<string?> MoveAsync(IssueTable issue, int toLevel, int? engineerId, string? reason, User actor, bool canHandBack)
        {
            if (issue.IStatus == "Close")
            {
                return "A closed ticket cannot be escalated. Reopen it first.";
            }

            if (toLevel < SupportLevel.Min || toLevel > SupportLevel.Max)
            {
                return "Choose a level from L1 to L5.";
            }

            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 5)
            {
                return "Write why the ticket moves: the next engineer reads it first.";
            }

            if (reason.Length > 1000)
            {
                reason = reason.Substring(0, 1000);
            }

            var from = SupportLevel.Clamp(issue.SupportLevel);
            if (toLevel < from && !canHandBack)
            {
                return "Only a support manager or an administrator can hand a ticket back to a lower level.";
            }

            var current = await AssigneeAsync(issue);
            User? target = null;
            if (engineerId.HasValue)
            {
                target = await EngineerQuery().FirstOrDefaultAsync(u => u.Id == engineerId.Value && u.UStatus);
                if (target == null)
                {
                    return "That person is not an active Support Engineer.";
                }

                if (SupportLevel.Clamp(target.SupportLevel) < toLevel)
                {
                    return target.FullName + " works at " + SupportLevel.Code(SupportLevel.Clamp(target.SupportLevel)) + ". Choose an engineer at " + SupportLevel.Code(toLevel) + " or above.";
                }
            }
            else if (current != null && current.Department == EngineerDepartment && SupportLevel.Clamp(current.SupportLevel) >= toLevel)
            {
                target = current; // the current engineer can work at the new level and keeps it
            }

            if (toLevel == from && target?.Id == current?.Id)
            {
                return "Nothing changes: choose another level or another engineer.";
            }

            var now = DateTime.Now;
            _db.TicketEscalations.Add(new TicketEscalation
            {
                IssueId = issue.IssueId,
                FromLevel = from,
                ToLevel = toLevel,
                FromEngineerUserId = current?.Id,
                FromEngineerName = current?.FullName ?? (string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy),
                ToEngineerUserId = target?.Id,
                ToEngineerName = target?.FullName,
                Reason = reason,
                ByUserId = actor.Id,
                ByName = actor.FullName,
                On = now
            });

            issue.SupportLevel = toLevel;
            if (toLevel != from)
            {
                issue.LevelSince = now;
            }

            if (target?.Id != current?.Id)
            {
                if (target == null)
                {
                    issue.AssignBy = null;
                    issue.AssignOn = null;
                    issue.AssignedToUserId = null;
                    issue.ApproveBy = null;
                    issue.ApproveOn = null;
                }
                else
                {
                    issue.AssignBy = target.FullName;
                    issue.AssignedToUserId = target.Id;
                    issue.AssignOn = now;
                    if (canHandBack)
                    {
                        issue.ApproveOn = now;
                        issue.ApproveBy = actor.FullName;
                    }
                }
            }

            // moving a ticket on is staff acting on it
            issue.FirstResponseOn ??= now;
            issue.UpdatedOn = now;
            issue.UpdatedBy = actor.FullName;
            return null;
        }

        /// <summary>
        /// Sets the development stage, owner, ETA and reference of a ticket and records the update (with an optional note,
        /// internal or for the client). Stage "None" ends development tracking. Returns an error text or null (the caller saves).
        /// </summary>
        public async Task<string?> UpdateDevelopmentAsync(IssueTable issue, string? stage, int? ownerId, DateTime? eta, string? reference,
            string? note, bool internalNote, User actor)
        {
            stage = string.IsNullOrWhiteSpace(stage) || stage == "None" ? null : stage.Trim();
            if (stage != null && !DevStages.IsValid(stage))
            {
                return "Unknown development stage.";
            }

            if (stage == null && issue.DevStage == null)
            {
                return "Choose a development stage.";
            }

            string? ownerName = null;
            if (stage == null)
            {
                ownerId = null;
                eta = null;
                reference = null;
            }
            else if (ownerId.HasValue)
            {
                ownerName = await _db.Users.AsNoTracking().Where(u => u.Id == ownerId.Value && u.UType && u.UStatus).Select(u => u.FullName).FirstOrDefaultAsync();
                if (ownerName == null)
                {
                    return "The owner must be an active XFL staff member.";
                }
            }

            if (eta.HasValue && eta.Value.Date < issue.TDate.Date)
            {
                return "The ETA cannot be before the ticket was raised.";
            }

            reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
            if (reference?.Length > 200) { reference = reference.Substring(0, 200); }
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > 1000) { note = note.Substring(0, 1000); }

            var changed = stage != issue.DevStage || ownerId != issue.DevOwnerUserId || eta?.Date != issue.DevEta?.Date || reference != issue.DevReference;
            if (!changed && note == null)
            {
                return "Nothing changed. Change the stage, owner, ETA or reference, or write a note.";
            }

            var now = DateTime.Now;
            _db.TicketDevUpdates.Add(new TicketDevUpdate
            {
                IssueId = issue.IssueId,
                FromStage = issue.DevStage,
                Stage = stage ?? "None",
                OwnerUserId = ownerId,
                OwnerName = ownerName,
                Eta = eta?.Date,
                Reference = reference,
                Note = note,
                VisibleToClient = !internalNote,
                ByUserId = actor.Id,
                ByName = actor.FullName,
                On = now
            });

            if (stage != issue.DevStage)
            {
                issue.DevStageSince = stage == null ? null : now;
            }

            issue.DevStage = stage;
            issue.DevOwnerUserId = ownerId;
            issue.DevEta = eta?.Date;
            issue.DevReference = reference;
            issue.UpdatedOn = now;
            issue.UpdatedBy = actor.FullName;
            return null;
        }

        /// <summary>The level and development panel of one ticket. Clients see the development timeline without internal notes.</summary>
        public async Task<TicketWorkView?> PanelAsync(int issueId, string role, string controller)
        {
            var issue = await _db.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == issueId);
            if (issue == null)
            {
                return null;
            }

            var levels = await LevelsAsync();
            var level = SupportLevel.Clamp(issue.SupportLevel);
            var info = levels.First(l => l.Level == level);
            var since = issue.LevelSince ?? issue.TDate;
            var closed = issue.IStatus == "Close";
            var staff = role != "Maker";
            var assignee = await AssigneeAsync(issue);

            var view = new TicketWorkView
            {
                IssueId = issue.IssueId,
                TicketNumber = issue.TNumber,
                Controller = controller,
                Role = role,
                IsClosed = closed,
                Level = level,
                LevelInfo = info,
                LevelSince = since,
                Levels = levels,
                EscalationDueAt = info.MaxHoursAtLevel > 0 ? since.AddHours(info.MaxHoursAtLevel) : null,
                CanHandBack = role == "Admin" || role == "SupportManager",
                CurrentEngineer = assignee?.FullName ?? (string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy),
                CurrentEngineerId = assignee?.Id,
                DevStage = issue.DevStage,
                DevStageSince = issue.DevStageSince,
                DevOwnerUserId = issue.DevOwnerUserId,
                DevEta = issue.DevEta,
                DevReference = issue.DevReference
            };
            view.EscalationOverdue = !closed && view.EscalationDueAt.HasValue && view.EscalationDueAt.Value < DateTime.Now;

            if (issue.DevOwnerUserId != null)
            {
                view.DevOwner = await _db.Users.AsNoTracking().Where(u => u.Id == issue.DevOwnerUserId).Select(u => u.FullName).FirstOrDefaultAsync();
            }

            view.Escalations = await _db.TicketEscalations.AsNoTracking().Where(e => e.IssueId == issueId).OrderByDescending(e => e.On).ThenByDescending(e => e.Id).ToListAsync();
            var updates = _db.TicketDevUpdates.AsNoTracking().Where(u => u.IssueId == issueId);
            if (!staff)
            {
                updates = updates.Where(u => u.VisibleToClient);
            }

            view.DevUpdates = await updates.OrderByDescending(u => u.On).ThenByDescending(u => u.Id).ToListAsync();

            if (staff && !closed)
            {
                view.Engineers = await EngineersAsync();
                view.DevOwners = await DevOwnersAsync();
            }

            return view;
        }

        /// <summary>Support Levels overview: workload and flow per level for the last <paramref name="days"/> days, and the development pipeline.</summary>
        public async Task<TiersOverviewView> OverviewAsync(int days)
        {
            var now = DateTime.Now;
            var from = DateTime.Today.AddDays(-(days - 1));
            var levels = await LevelsAsync();
            var engineers = await EngineersAsync(activeOnly: false);

            var open = await _db.Issues.AsNoTracking().Where(i => i.IStatus != "Close")
                .Select(i => new { i.IssueId, i.SupportLevel, i.LevelSince, i.TDate, i.Priority, i.AssignedToUserId, i.AssignBy }).ToListAsync();
            var closed = await _db.Issues.AsNoTracking().Where(i => i.IStatus == "Close" && i.ClosedOn >= from)
                .Select(i => new { i.IssueId, i.SupportLevel, i.AssignedToUserId }).ToListAsync();
            var moves = await _db.TicketEscalations.AsNoTracking().Where(e => e.On >= from).ToListAsync();

            // every move of the tickets that moved in the period, to know when each stay at a level began
            var movedIds = moves.Select(m => m.IssueId).Distinct().ToList();
            var history = new List<TicketEscalation>();
            var raised = new Dictionary<int, DateTime>();
            foreach (var chunk in movedIds.Chunk(1500))
            {
                history.AddRange(await _db.TicketEscalations.AsNoTracking().Where(e => chunk.Contains(e.IssueId)).ToListAsync());
                foreach (var r in await _db.Issues.AsNoTracking().Where(i => chunk.Contains(i.IssueId)).Select(i => new { i.IssueId, i.TDate }).ToListAsync())
                {
                    raised[r.IssueId] = r.TDate;
                }
            }

            var stays = new List<(int Level, double Hours)>();
            foreach (var group in history.GroupBy(h => h.IssueId))
            {
                var start = raised.TryGetValue(group.Key, out var t) ? t : (DateTime?)null;
                foreach (var move in group.OrderBy(m => m.On).ThenBy(m => m.Id))
                {
                    if (move.FromLevel != move.ToLevel && start.HasValue && move.On >= from)
                    {
                        stays.Add((move.FromLevel, Math.Max(0, (move.On - start.Value).TotalHours)));
                    }

                    if (move.FromLevel != move.ToLevel)
                    {
                        start = move.On;
                    }
                }
            }

            var escalatedIds = new HashSet<int>();
            foreach (var chunk in closed.Select(c => c.IssueId).Chunk(1500))
            {
                foreach (var id in await _db.TicketEscalations.AsNoTracking().Where(e => chunk.Contains(e.IssueId) && e.ToLevel > e.FromLevel).Select(e => e.IssueId).Distinct().ToListAsync())
                {
                    escalatedIds.Add(id);
                }
            }

            var view = new TiersOverviewView { Days = days };
            foreach (var l in levels)
            {
                var atLevel = open.Where(o => SupportLevel.Clamp(o.SupportLevel) == l.Level).ToList();
                var levelStays = stays.Where(s => s.Level == l.Level).Select(s => s.Hours).ToList();
                view.Levels.Add(new LevelOverviewRow
                {
                    Level = l,
                    Engineers = engineers.Count(e => e.Level == l.Level),
                    ActiveEngineers = engineers.Count(e => e.Level == l.Level && e.Active),
                    Open = atLevel.Count,
                    Unassigned = atLevel.Count(o => o.AssignedToUserId == null && string.IsNullOrWhiteSpace(o.AssignBy)),
                    HighOpen = atLevel.Count(o => o.Priority == "High"),
                    EscalationDue = l.MaxHoursAtLevel > 0 ? atLevel.Count(o => (o.LevelSince ?? o.TDate).AddHours(l.MaxHoursAtLevel) < now) : 0,
                    ClosedInPeriod = closed.Count(c => SupportLevel.Clamp(c.SupportLevel) == l.Level),
                    EscalatedIn = moves.Count(m => m.ToLevel == l.Level && m.ToLevel > m.FromLevel),
                    EscalatedOut = moves.Count(m => m.FromLevel == l.Level && m.ToLevel > m.FromLevel),
                    HandedBack = moves.Count(m => m.FromLevel == l.Level && m.ToLevel < m.FromLevel),
                    AvgHoursAtLevel = levelStays.Count > 0 ? levelStays.Average() : null
                });
            }

            var closedByEngineer = closed.Where(c => c.AssignedToUserId != null).GroupBy(c => c.AssignedToUserId!.Value).ToDictionary(g => g.Key, g => g.Count());
            view.Engineers = engineers.Where(e => e.Active).Select(e => (e, closedByEngineer.GetValueOrDefault(e.Id))).ToList();
            view.EscalationsInPeriod = moves.Count(m => m.ToLevel > m.FromLevel);
            view.HandBacksInPeriod = moves.Count(m => m.ToLevel < m.FromLevel);
            if (closed.Count > 0)
            {
                view.FirstLevelResolution = (double)closed.Count(c => SupportLevel.Clamp(c.SupportLevel) == 1 && !escalatedIds.Contains(c.IssueId)) / closed.Count;
                view.EscalationRate = (double)closed.Count(c => escalatedIds.Contains(c.IssueId)) / closed.Count;
            }

            var recent = await _db.TicketEscalations.AsNoTracking().OrderByDescending(e => e.On).ThenByDescending(e => e.Id).Take(15).ToListAsync();
            var recentIds = recent.Select(r => r.IssueId).Distinct().ToList();
            var tickets = await _db.Issues.AsNoTracking().Where(i => recentIds.Contains(i.IssueId)).Select(i => new { i.IssueId, i.TNumber, i.ITitle }).ToDictionaryAsync(i => i.IssueId);
            view.RecentEscalations = recent.Select(r => new EscalationRow
            {
                Escalation = r,
                TicketNumber = tickets.TryGetValue(r.IssueId, out var t) ? t.TNumber : "#" + r.IssueId,
                Title = tickets.TryGetValue(r.IssueId, out var t2) ? t2.ITitle ?? string.Empty : string.Empty
            }).ToList();

            var dev = await _db.Issues.AsNoTracking().Where(i => i.DevStage != null && i.DevStage != DevStages.Deployed)
                .Select(i => new { i.DevStage, i.DevEta }).ToListAsync();
            view.DevPipeline = DevStages.All.Where(s => s.Value != DevStages.Deployed).ToDictionary(s => s.Value, s => dev.Count(d => d.DevStage == s.Value));
            view.DevOverdue = dev.Count(d => d.DevEta.HasValue && d.DevEta.Value.Date < DateTime.Today);

            var deployed = await _db.TicketDevUpdates.AsNoTracking().Where(u => u.Stage == DevStages.Deployed && u.On >= from)
                .GroupBy(u => u.IssueId).Select(g => new { g.Key, On = g.Max(x => x.On) }).ToListAsync();
            view.DeployedInPeriod = deployed.Count;
            if (deployed.Count > 0)
            {
                var ids = deployed.Select(d => d.Key).ToList();
                var starts = await _db.TicketDevUpdates.AsNoTracking().Where(u => ids.Contains(u.IssueId)).GroupBy(u => u.IssueId)
                    .Select(g => new { g.Key, On = g.Min(x => x.On) }).ToDictionaryAsync(x => x.Key, x => x.On);
                view.AvgDaysToDeploy = deployed.Average(d => (d.On - starts.GetValueOrDefault(d.Key, d.On)).TotalDays);
            }

            return view;
        }

        /// <summary>Tickets with development work: active ones, and (optionally) the deployed ones; deployed in the last 14 days always show.</summary>
        public async Task<List<DevTicketRow>> DevTicketsAsync(bool includeDeployed, int? ownerId)
        {
            var recent = DateTime.Today.AddDays(-14);
            var query = _db.Issues.AsNoTracking().Where(i => i.DevStage != null);
            if (!includeDeployed)
            {
                query = query.Where(i => i.DevStage != DevStages.Deployed || i.DevStageSince >= recent);
            }

            if (ownerId.HasValue)
            {
                query = query.Where(i => i.DevOwnerUserId == ownerId);
            }

            var rows = await query.Select(i => new DevTicketRow
            {
                IssueId = i.IssueId,
                Number = i.TNumber,
                Title = i.ITitle ?? string.Empty,
                House = i.Brokerages.BrokerageHouseAcronym,
                Priority = i.Priority,
                Status = i.IStatus ?? "Open",
                Level = i.SupportLevel,
                Stage = i.DevStage!,
                StageSince = i.DevStageSince,
                OwnerId = i.DevOwnerUserId,
                Eta = i.DevEta,
                Reference = i.DevReference
            }).ToListAsync();

            var ownerIds = rows.Where(r => r.OwnerId != null).Select(r => r.OwnerId!.Value).Distinct().ToList();
            var owners = await _db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
            var ids = rows.Select(r => r.IssueId).ToList();
            var notes = new Dictionary<int, string>();
            foreach (var chunk in ids.Chunk(1500))
            {
                var all = await _db.TicketDevUpdates.AsNoTracking().Where(u => chunk.Contains(u.IssueId) && u.Note != null)
                    .OrderBy(u => u.On).ThenBy(u => u.Id).Select(u => new { u.IssueId, u.Note }).ToListAsync();
                foreach (var n in all) { notes[n.IssueId] = n.Note!; } // the latest wins
            }

            foreach (var r in rows)
            {
                r.Owner = r.OwnerId.HasValue && owners.TryGetValue(r.OwnerId.Value, out var name) ? name : null;
                r.LastNote = notes.GetValueOrDefault(r.IssueId);
            }

            return rows.OrderBy(r => r.Eta ?? DateTime.MaxValue).ThenBy(r => r.Number).ToList();
        }

        /// <summary>Scorecard by level: every engineer's level, and with a level chosen only the engineers of that level.</summary>
        public async Task ApplyToScorecardAsync(XFLCSMS.Models.Feedback.ScorecardView view, int? level)
        {
            view.Levels = await LevelsAsync();
            view.EngineerLevels = await EngineerQuery().ToDictionaryAsync(u => u.Id, u => u.SupportLevel ?? SupportLevel.Min);
            if (level is >= SupportLevel.Min and <= SupportLevel.Max)
            {
                view.Level = level;
                view.Rows = view.Rows.Where(r => view.EngineerLevels.GetValueOrDefault(r.EngineerUserId, SupportLevel.Min) == level).ToList();
            }
        }

        /// <summary>Admin: saves the level texts and limits and every engineer's level. Returns an error text or null.</summary>
        public async Task<string?> SaveAdminAsync(List<SupportLevel> levels, List<EngineerLevelForm> engineers)
        {
            foreach (var form in levels ?? new())
            {
                if (form.Level < SupportLevel.Min || form.Level > SupportLevel.Max) { continue; }
                var name = form.Name?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    return "Every level needs a name.";
                }

                if (form.MaxHoursAtLevel < 0 || form.MaxHoursAtLevel > 8760)
                {
                    return "Time at a level must be between 0 and 8760 hours (0 = no limit).";
                }

                var row = await _db.SupportLevels.FirstOrDefaultAsync(l => l.Level == form.Level);
                if (row == null)
                {
                    row = new SupportLevel { Level = form.Level };
                    _db.SupportLevels.Add(row);
                }

                row.Name = name.Length > 60 ? name.Substring(0, 60) : name;
                row.Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim().Length > 400 ? form.Description.Trim().Substring(0, 400) : form.Description.Trim();
                row.MaxHoursAtLevel = form.MaxHoursAtLevel;
            }

            var ids = (engineers ?? new()).Select(e => e.UserId).ToList();
            var users = await _db.Users.Where(u => ids.Contains(u.Id) && u.Department == EngineerDepartment).ToListAsync();
            foreach (var form in engineers ?? new())
            {
                var user = users.FirstOrDefault(u => u.Id == form.UserId);
                if (user != null && form.Level >= SupportLevel.Min && form.Level <= SupportLevel.Max)
                {
                    user.SupportLevel = form.Level;
                }
            }

            await _db.SaveChangesAsync();
            return null;
        }
    }
}
