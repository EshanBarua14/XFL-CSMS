using XFLCSMS.Models.Feedback;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Tiers;
using XFLCSMS.Services.Notifications;

namespace XFLCSMS.Services
{
    /// <summary>
    /// How tickets reach engineers: an engineer takes a queued ticket (self-assign) or gives it back (release), idle
    /// engineers get the oldest queued ticket automatically (auto-assign), an engineer asks another to take a ticket over
    /// (transfer request, optionally approved by a manager), and managers assign from the queue. What is allowed comes
    /// from Admin > Ticket Assignment (AssignmentSettings) and, per level, Admin > Support Levels (SupportLevel authority).
    /// </summary>
    public class AssignmentService
    {
        public const string AutoAssignName = "Auto-assign";

        // Two engineers taking the same ticket at the same moment, or auto-assign and an engineer: only one may win.
        // Assignment is serialised in this web server (like ticket numbering).
        private static readonly SemaphoreSlim AssignLock = new(1, 1);

        private static readonly string[] StatusOrder = { "Open", "Inqueue", "Inprogress" };

        private readonly DataContext _db;
        private readonly TierService _tiers;
        private readonly EngineerPresence _presence;
        private readonly NotificationQueue _notifications;
        private readonly IHttpContextAccessor _http;

        public AssignmentService(DataContext db, TierService tiers, EngineerPresence presence, NotificationQueue notifications, IHttpContextAccessor http)
        {
            _db = db;
            _tiers = tiers;
            _presence = presence;
            _notifications = notifications;
            _http = http;
        }

        public async Task<AssignmentSettings> SettingsAsync()
        {
            return await _db.AssignmentSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == AssignmentSettings.SingletonId) ?? new AssignmentSettings();
        }

        private IQueryable<IssueTable> Queued() =>
            _db.Issues.Where(i => i.IStatus != "Close" && i.AssignedToUserId == null && (i.AssignBy == null || i.AssignBy == ""));

        private static bool IsQueued(IssueTable i) => i.IStatus != "Close" && i.AssignedToUserId == null && string.IsNullOrWhiteSpace(i.AssignBy);

        private Task<int> OpenCountAsync(int userId) => _db.Issues.CountAsync(i => i.IStatus != "Close" && i.AssignedToUserId == userId);

        private Task<User?> ActiveEngineerAsync(int userId) =>
            _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.Department == TierService.EngineerDepartment && u.UType && u.UStatus);

        /// <summary>What the engineer's level allows (null: not an active Support Engineer). Read fresh: the session copy may be old.</summary>
        public async Task<(User User, SupportLevel Authority)?> EngineerAsync(int userId)
        {
            var user = await ActiveEngineerAsync(userId);
            if (user == null)
            {
                return null;
            }

            var levels = await _tiers.LevelsAsync();
            return (user, levels.First(l => l.Level == SupportLevel.Clamp(user.SupportLevel)));
        }

        private static string? CapacityError(SupportLevel authority, int open, string who)
        {
            return authority.MaxOpenTickets > 0 && open >= authority.MaxOpenTickets
                ? who + " already hold" + (who == "You" ? "" : "s") + " " + open + " open tickets, the most an " + SupportLevel.Code(authority.Level) + " engineer may hold (" + authority.MaxOpenTickets + ")."
                : null;
        }

        private static string? SelfAssignError(AssignmentSettings settings, SupportLevel authority)
        {
            if (!settings.SelfAssignEnabled) return "Taking tickets yourself is switched off. Ask a support manager to assign it.";
            if (!authority.CanSelfAssign) return SupportLevel.Code(authority.Level) + " engineers cannot take tickets themselves. Ask a support manager to assign it.";
            return null;
        }

        private static string? TakeError(IssueTable issue, SupportLevel authority)
        {
            if (issue.IStatus == "Close") return "The ticket is closed.";
            if (!IsQueued(issue)) return "The ticket is already assigned to " + issue.AssignBy + ".";
            var level = SupportLevel.Clamp(issue.SupportLevel);
            if (level > authority.Level) return "The ticket is at " + SupportLevel.Code(level) + " and you work at " + SupportLevel.Code(authority.Level) + ".";
            return null;
        }

        /// <summary>Puts the engineer on the ticket and moves an Open ticket on to the "status on take" (with its history row).</summary>
        private void Assign(IssueTable issue, User engineer, int? byUserId, string byName, AssignmentSettings settings, DateTime now, bool staffActed)
        {
            issue.AssignBy = engineer.FullName;
            issue.AssignedToUserId = engineer.Id;
            issue.AssignOn = now;
            issue.ApproveOn = now;
            issue.ApproveBy = byName;
            issue.UpdatedOn = now;
            issue.UpdatedBy = byName;

            // only forward: an In Progress ticket is not put back to In Queue
            var target = settings.StatusOnTake;
            var from = Math.Max(0, Array.IndexOf(StatusOrder, string.IsNullOrWhiteSpace(issue.IStatus) ? "Open" : issue.IStatus));
            var to = Array.IndexOf(StatusOrder, target ?? string.Empty);
            if (to > from)
            {
                _db.TicketStatusHistories.Add(new TicketStatusHistory
                {
                    IssueId = issue.IssueId,
                    FromStatus = issue.IStatus,
                    ToStatus = target,
                    AssignedToUserId = engineer.Id,
                    ChangedByUserId = byUserId,
                    ChangedByName = byName,
                    ChangedOn = now
                });
                issue.IStatus = target;

                // the same rule as the edit form: staff moving a ticket on from Open is the first response (auto-assign is not)
                if (staffActed)
                {
                    issue.FirstResponseOn ??= now;
                }
            }
        }

        private void Unassign(IssueTable issue, string byName, DateTime now)
        {
            issue.AssignBy = null;
            issue.AssignOn = null;
            issue.AssignedToUserId = null;
            issue.ApproveBy = null;
            issue.ApproveOn = null;
            issue.UpdatedOn = now;
            issue.UpdatedBy = byName;
        }

        /// <summary>A move at the same level, so the ticket's escalation history shows who had it and why it moved.</summary>
        private void RecordHandover(IssueTable issue, User? from, User? to, string reason, User actor, DateTime now)
        {
            var level = SupportLevel.Clamp(issue.SupportLevel);
            _db.TicketEscalations.Add(new TicketEscalation
            {
                IssueId = issue.IssueId,
                FromLevel = level,
                ToLevel = level,
                FromEngineerUserId = from?.Id,
                FromEngineerName = from?.FullName,
                ToEngineerUserId = to?.Id,
                ToEngineerName = to?.FullName,
                Reason = reason.Length > 1000 ? reason.Substring(0, 1000) : reason,
                ByUserId = actor.Id,
                ByName = actor.FullName,
                On = now
            });
        }

        private async Task CloseOpenTransfersAsync(int issueId, string status, string byName, DateTime now)
        {
            foreach (var r in await _db.TicketTransferRequests.Where(t => t.IssueId == issueId && (t.Status == TicketTransferRequest.Pending || t.Status == TicketTransferRequest.AwaitingApproval)).ToListAsync())
            {
                r.Status = status;
                r.DecidedOn = now;
                r.DecidedByName = byName;
            }
        }

        // ---------------------------------------------------------------- self-assign / release / manager assign

        /// <summary>The engineer takes a queued ticket. Returns an error text or null.</summary>
        public async Task<string?> TakeAsync(int issueId, User me)
        {
            var engineer = await EngineerAsync(me.Id);
            if (engineer == null) return "Only an active Support Engineer can take a ticket.";
            var (user, authority) = engineer.Value;
            var settings = await SettingsAsync();
            var blocked = SelfAssignError(settings, authority) ?? CapacityError(authority, await OpenCountAsync(user.Id), "You");
            if (blocked != null) return blocked;

            await AssignLock.WaitAsync();
            try
            {
                var issue = await _db.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId);
                if (issue == null) return "Ticket not found.";
                var error = TakeError(issue, authority);
                if (error != null) return error;

                Assign(issue, user, user.Id, user.FullName, settings, DateTime.Now, staffActed: true);
                await _db.SaveChangesAsync();
                return null;
            }
            finally
            {
                AssignLock.Release();
            }
        }

        /// <summary>The engineer takes the next queued ticket (the one auto-assign would give them). Returns the ticket id, or an error.</summary>
        public async Task<(int? IssueId, string? Error)> TakeNextAsync(User me)
        {
            var engineer = await EngineerAsync(me.Id);
            if (engineer == null) return (null, "Only an active Support Engineer can take a ticket.");
            var settings = await SettingsAsync();
            var blocked = SelfAssignError(settings, engineer.Value.Authority) ?? CapacityError(engineer.Value.Authority, await OpenCountAsync(me.Id), "You");
            if (blocked != null) return (null, blocked);

            var queue = await QueueRowsAsync(delayMinutes: 0);
            var next = Pick(queue, engineer.Value.Authority.Level, settings, me.Id, new HashSet<(int, int)>());
            if (next == null) return (null, "No ticket in the queue that you can take.");
            var error = await TakeAsync(next.Value, me);
            return error == null ? (next.Value, null) : (null, error);
        }

        /// <summary>The assigned engineer gives the ticket back to the queue of its level, with a reason.</summary>
        public async Task<string?> ReleaseAsync(int issueId, User me, string? reason)
        {
            var settings = await SettingsAsync();
            if (!settings.ReleaseEnabled) return "Giving tickets back to the queue is switched off. Ask a support manager, or request a transfer.";
            reason = reason?.Trim();
            if (string.IsNullOrEmpty(reason) || reason.Length < 5) return "Write why you give the ticket back: the next engineer reads it.";

            await AssignLock.WaitAsync();
            try
            {
                var issue = await _db.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId);
                if (issue == null) return "Ticket not found.";
                if (issue.IStatus == "Close") return "The ticket is closed.";
                if (issue.AssignedToUserId != me.Id) return "Only the engineer the ticket is assigned to can give it back.";

                var now = DateTime.Now;
                var user = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id);
                RecordHandover(issue, user, null, "Given back to the queue: " + reason, user, now);
                Unassign(issue, user.FullName, now);
                await CloseOpenTransfersAsync(issue.IssueId, TicketTransferRequest.Cancelled, user.FullName, now);
                await _db.SaveChangesAsync();
                return null;
            }
            finally
            {
                AssignLock.Release();
            }
        }

        /// <summary>A manager / administrator (or an engineer whose level may assign others) puts an engineer on the ticket.</summary>
        public async Task<string?> AssignAsync(int issueId, int engineerId, User actor)
        {
            var target = await ActiveEngineerAsync(engineerId);
            if (target == null) return "That person is not an active Support Engineer.";
            var settings = await SettingsAsync();

            await AssignLock.WaitAsync();
            try
            {
                var issue = await _db.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId);
                if (issue == null) return "Ticket not found.";
                if (issue.IStatus == "Close") return "The ticket is closed.";
                if (issue.AssignedToUserId == target.Id) return "The ticket is already assigned to " + target.FullName + ".";
                var level = SupportLevel.Clamp(issue.SupportLevel);
                if (SupportLevel.Clamp(target.SupportLevel) < level)
                {
                    return target.FullName + " works at " + SupportLevel.Code(SupportLevel.Clamp(target.SupportLevel)) + " and the ticket is at " + SupportLevel.Code(level) + ".";
                }

                var now = DateTime.Now;
                if (!IsQueued(issue))
                {
                    var previous = issue.AssignedToUserId == null ? null : await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == issue.AssignedToUserId);
                    RecordHandover(issue, previous, target, "Reassigned by " + actor.FullName + ".", actor, now);
                    await CloseOpenTransfersAsync(issue.IssueId, TicketTransferRequest.Cancelled, actor.FullName, now);
                }

                Assign(issue, target, actor.Id, actor.FullName, settings, now, staffActed: true);
                await _db.SaveChangesAsync();
                return null;
            }
            finally
            {
                AssignLock.Release();
            }
        }

        // ---------------------------------------------------------------- transfer requests

        /// <summary>The assigned engineer asks another engineer to take the ticket over.</summary>
        public async Task<string?> RequestTransferAsync(int issueId, User me, int toUserId, string? note)
        {
            var settings = await SettingsAsync();
            if (!settings.TransferEnabled) return "Transfer requests are switched off. Ask a support manager to reassign the ticket.";
            var engineer = await EngineerAsync(me.Id);
            if (engineer == null) return "Only an active Support Engineer can request a transfer.";
            var (user, authority) = engineer.Value;
            if (!authority.CanRequestTransfer) return SupportLevel.Code(authority.Level) + " engineers cannot request transfers. Ask a support manager.";

            note = note?.Trim();
            if (string.IsNullOrEmpty(note) || note.Length < 5) return "Write why: the other engineer reads it before answering.";
            if (note.Length > 1000) note = note.Substring(0, 1000);

            var issue = await _db.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == issueId);
            if (issue == null) return "Ticket not found.";
            if (issue.IStatus == "Close") return "The ticket is closed.";
            if (issue.AssignedToUserId != user.Id) return "Only the engineer the ticket is assigned to can ask to transfer it.";
            if (toUserId == user.Id) return "Choose another engineer.";
            if (await _db.TicketTransferRequests.AnyAsync(t => t.IssueId == issueId && (t.Status == TicketTransferRequest.Pending || t.Status == TicketTransferRequest.AwaitingApproval)))
            {
                return "A transfer of this ticket is already waiting for an answer. Withdraw it first.";
            }

            var target = await EngineerAsync(toUserId);
            if (target == null) return "That person is not an active Support Engineer.";
            var error = await TargetErrorAsync(issue, target.Value.User, target.Value.Authority, settings);
            if (error != null) return error;

            var now = DateTime.Now;
            var request = new TicketTransferRequest
            {
                IssueId = issueId,
                FromUserId = user.Id,
                FromName = user.FullName,
                ToUserId = target.Value.User.Id,
                ToName = target.Value.User.FullName,
                Note = note,
                Status = TicketTransferRequest.Pending,
                CreatedOn = now,
                ExpiresOn = settings.TransferExpiryHours > 0 ? now.AddHours(settings.TransferExpiryHours) : null
            };
            _db.TicketTransferRequests.Add(request);
            await _db.SaveChangesAsync();
            Announce("TransferRequested", issue, request, user, new[] { request.ToUserId });
            return null;
        }

        private async Task<string?> TargetErrorAsync(IssueTable issue, User target, SupportLevel authority, AssignmentSettings settings)
        {
            var level = SupportLevel.Clamp(issue.SupportLevel);
            if (authority.Level < level)
            {
                return target.FullName + " works at " + SupportLevel.Code(authority.Level) + " and the ticket is at " + SupportLevel.Code(level) + ".";
            }

            return settings.TransferRespectsCapacity ? CapacityError(authority, await OpenCountAsync(target.Id), target.FullName) : null;
        }

        /// <summary>The asked engineer accepts or declines.</summary>
        public async Task<string?> AnswerTransferAsync(int requestId, User me, bool accept, string? note)
        {
            var request = await _db.TicketTransferRequests.FirstOrDefaultAsync(t => t.Id == requestId);
            if (request == null || request.ToUserId != me.Id) return "Transfer request not found.";
            if (request.Status != TicketTransferRequest.Pending) return "This request was already answered (" + TicketTransferRequest.Label(request.Status) + ").";
            var now = DateTime.Now;
            if (request.ExpiresOn.HasValue && request.ExpiresOn < now)
            {
                request.Status = TicketTransferRequest.Expired;
                await _db.SaveChangesAsync();
                return "This request has expired.";
            }

            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > 1000) note = note.Substring(0, 1000);
            var actor = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id);
            var issue = await _db.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == request.IssueId);
            if (issue == null) return "Ticket not found.";

            if (!accept)
            {
                Decide(request, TicketTransferRequest.Declined, actor, note, now);
                await _db.SaveChangesAsync();
                Announce("TransferAnswered", issue, request, actor, new[] { request.FromUserId });
                return null;
            }

            var settings = await SettingsAsync();
            var target = await EngineerAsync(me.Id);
            if (target == null) return "Only an active Support Engineer can take a ticket.";
            var error = StillTransferable(issue, request) ?? await TargetErrorAsync(issue, target.Value.User, target.Value.Authority, settings);
            if (error != null) return error;

            if (settings.TransferNeedsManagerApproval)
            {
                request.Status = TicketTransferRequest.AwaitingApproval;
                request.DecisionNote = note;
                await _db.SaveChangesAsync();
                Announce("TransferAwaitingApproval", issue, request, actor, Array.Empty<int>());
                return null;
            }

            return await CompleteAsync(request, actor, note, settings);
        }

        private static string? StillTransferable(IssueTable issue, TicketTransferRequest request)
        {
            if (issue.IStatus == "Close") return "The ticket is closed.";
            if (issue.AssignedToUserId != request.FromUserId) return "The ticket is no longer with " + request.FromName + ".";
            return null;
        }

        private async Task<string?> CompleteAsync(TicketTransferRequest request, User actor, string? note, AssignmentSettings settings)
        {
            await AssignLock.WaitAsync();
            try
            {
                var issue = await _db.Issues.FirstAsync(i => i.IssueId == request.IssueId);
                var error = StillTransferable(issue, request);
                if (error != null) return error;
                var from = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.FromUserId);
                var to = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == request.ToUserId);
                var now = DateTime.Now;

                RecordHandover(issue, from, to, "Transfer: " + request.Note, actor, now);
                Assign(issue, to, actor.Id, actor.FullName, settings, now, staffActed: true);
                Decide(request, TicketTransferRequest.Accepted, actor, note ?? request.DecisionNote, now);
                await _db.SaveChangesAsync();
                Announce("TransferAnswered", issue, request, actor, new[] { request.FromUserId, request.ToUserId });
                return null;
            }
            finally
            {
                AssignLock.Release();
            }
        }

        private static void Decide(TicketTransferRequest request, string status, User actor, string? note, DateTime now)
        {
            request.Status = status;
            request.DecidedOn = now;
            request.DecidedByUserId = actor.Id;
            request.DecidedByName = actor.FullName;
            request.DecisionNote = note;
        }

        /// <summary>The requesting engineer withdraws an unanswered request.</summary>
        public async Task<string?> CancelTransferAsync(int requestId, User me)
        {
            var request = await _db.TicketTransferRequests.FirstOrDefaultAsync(t => t.Id == requestId);
            if (request == null || request.FromUserId != me.Id) return "Transfer request not found.";
            if (!request.IsOpen) return "This request was already answered (" + TicketTransferRequest.Label(request.Status) + ").";
            Decide(request, TicketTransferRequest.Cancelled, await _db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id), null, DateTime.Now);
            await _db.SaveChangesAsync();
            return null;
        }

        /// <summary>A manager / administrator approves or rejects an open transfer (accepted ones waiting for approval, or unanswered ones).</summary>
        public async Task<string?> DecideTransferAsync(int requestId, User manager, bool approve, string? note)
        {
            var request = await _db.TicketTransferRequests.FirstOrDefaultAsync(t => t.Id == requestId);
            if (request == null) return "Transfer request not found.";
            if (!request.IsOpen) return "This request was already answered (" + TicketTransferRequest.Label(request.Status) + ").";
            note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (note?.Length > 1000) note = note.Substring(0, 1000);
            var actor = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == manager.Id);
            var issue = await _db.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.IssueId == request.IssueId);
            if (issue == null) return "Ticket not found.";

            if (!approve)
            {
                Decide(request, TicketTransferRequest.Rejected, actor, note, DateTime.Now);
                await _db.SaveChangesAsync();
                Announce("TransferAnswered", issue, request, actor, new[] { request.FromUserId, request.ToUserId });
                return null;
            }

            var settings = await SettingsAsync();
            var target = await EngineerAsync(request.ToUserId);
            if (target == null) return request.ToName + " is no longer an active Support Engineer.";
            var level = SupportLevel.Clamp(issue.SupportLevel);
            if (target.Value.Authority.Level < level) return request.ToName + " works at " + SupportLevel.Code(target.Value.Authority.Level) + " and the ticket is at " + SupportLevel.Code(level) + ".";
            return await CompleteAsync(request, actor, note, settings);
        }

        /// <summary>Unanswered requests past their time lapse (background).</summary>
        public async Task<int> ExpireTransfersAsync(CancellationToken token)
        {
            var now = DateTime.Now;
            var old = await _db.TicketTransferRequests.Where(t => t.Status == TicketTransferRequest.Pending && t.ExpiresOn != null && t.ExpiresOn < now).ToListAsync(token);
            foreach (var r in old)
            {
                r.Status = TicketTransferRequest.Expired;
                r.DecidedOn = now;
            }

            if (old.Count > 0) await _db.SaveChangesAsync(token);
            return old.Count;
        }

        private void Announce(string key, IssueTable issue, TicketTransferRequest request, User actor, IEnumerable<int> to)
        {
            var http = _http.HttpContext;
            var data = NotificationSaveInterceptor.ForTicket(new NotificationEventData
            {
                EventKey = key,
                ActorUserId = actor.Id,
                ActorName = actor.FullName,
                BaseUrl = NotificationSaveInterceptor.BaseUrl(http)
            }, NotificationSaveInterceptor.Snapshot(issue));
            data.StaffOnly = true;
            data.RelatedUserIds.Clear(); // only the engineers of the request (and managers by their own switches), never the client
            foreach (var id in to) data.RelatedUserIds.Add(id);
            data.Values["FromName"] = request.FromName;
            data.Values["ToName"] = request.ToName;
            data.Values["TransferNote"] = request.Note;
            data.Values["Decision"] = TicketTransferRequest.Label(request.Status);
            data.Values["DecisionNote"] = request.DecisionNote;
            _notifications.Publish(data);
        }

        // ---------------------------------------------------------------- auto-assign

        private sealed record QueueItem(int IssueId, int Level, string Priority, DateTime Raised);

        private async Task<List<QueueItem>> QueueRowsAsync(int delayMinutes)
        {
            var cutoff = DateTime.Now.AddMinutes(-Math.Max(0, delayMinutes));
            var rows = await Queued().AsNoTracking().Where(i => i.TDate <= cutoff)
                .Select(i => new { i.IssueId, i.SupportLevel, i.Priority, i.TDate }).ToListAsync();
            return rows.Select(r => new QueueItem(r.IssueId, SupportLevel.Clamp(r.SupportLevel), r.Priority ?? string.Empty, r.TDate)).ToList();
        }

        private static int PriorityRank(string priority) => priority switch { "High" => 0, "Medium" => 1, "Low" => 2, _ => 3 };

        /// <summary>The ticket an engineer of <paramref name="level"/> gets next under the level rule and order of the settings.</summary>
        private static int? Pick(List<QueueItem> queue, int level, AssignmentSettings settings, int engineerId, HashSet<(int IssueId, int UserId)> givenAway)
        {
            var rule = AssignmentLevelRules.Normalize(settings.AutoAssignLevelRule);
            var byPriority = AssignmentOrders.Normalize(settings.AutoAssignOrder) == AssignmentOrders.PriorityThenOldest;
            var allowed = queue.Where(q => (rule == AssignmentLevelRules.OwnLevelOnly ? q.Level == level : q.Level <= level) && !givenAway.Contains((q.IssueId, engineerId)));
            var ordered = rule == AssignmentLevelRules.OwnLevelFirst ? allowed.OrderBy(q => q.Level == level ? 0 : 1) : allowed.OrderBy(q => 0);
            ordered = byPriority ? ordered.ThenBy(q => PriorityRank(q.Priority)) : ordered;
            return ordered.ThenBy(q => q.Raised).ThenBy(q => q.IssueId).Select(q => (int?)q.IssueId).FirstOrDefault();
        }

        /// <summary>
        /// Gives queued tickets to idle engineers: active engineers whose level receives auto-assign, who hold fewer open
        /// tickets than the idle limit (and their level's maximum) and, when required, used CSMS recently. The least busy
        /// engineer (then the one who waited longest for a ticket) goes first; each gets the oldest ticket their level
        /// allows. A ticket an engineer gave back or transferred in the last 24 hours is not given to them again.
        /// Returns how many tickets were assigned.
        /// </summary>
        public async Task<int> AutoAssignAsync(bool ignoreOnline, CancellationToken token)
        {
            var settings = await SettingsAsync();
            if (!settings.AutoAssignEnabled) return 0;

            var queue = await QueueRowsAsync(settings.AutoAssignDelayMinutes);
            if (queue.Count == 0) return 0;

            var levels = (await _tiers.LevelsAsync()).ToDictionary(l => l.Level);
            var engineers = await _db.Users.AsNoTracking()
                .Where(u => u.Department == TierService.EngineerDepartment && u.UType && u.UStatus).ToListAsync(token);
            var load = await _db.Issues.AsNoTracking().Where(i => i.IStatus != "Close" && i.AssignedToUserId != null)
                .GroupBy(i => i.AssignedToUserId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, token);
            var lastAssigned = await _db.Issues.AsNoTracking().Where(i => i.AssignedToUserId != null && i.AssignOn != null)
                .GroupBy(i => i.AssignedToUserId!.Value).Select(g => new { g.Key, On = g.Max(i => i.AssignOn) }).ToDictionaryAsync(x => x.Key, x => x.On, token);
            var since = DateTime.Now.AddHours(-24);
            var givenAway = (await _db.TicketEscalations.AsNoTracking()
                    .Where(e => e.On >= since && e.FromEngineerUserId != null && (e.ToEngineerUserId == null || e.ToEngineerUserId != e.FromEngineerUserId))
                    .Select(e => new { e.IssueId, UserId = e.FromEngineerUserId!.Value }).ToListAsync(token))
                .Select(x => (x.IssueId, x.UserId)).ToHashSet();

            bool Idle(User u)
            {
                var authority = levels[SupportLevel.Clamp(u.SupportLevel)];
                var open = load.GetValueOrDefault(u.Id);
                return authority.ReceivesAutoAssign
                    && open < Math.Max(1, settings.AutoAssignIdleBelow)
                    && (authority.MaxOpenTickets == 0 || open < authority.MaxOpenTickets)
                    && (ignoreOnline || !settings.AutoAssignOnlyOnline || _presence.IsOnline(u.Id, settings.OnlineWindowMinutes));
            }

            var assigned = 0;
            while (queue.Count > 0 && !token.IsCancellationRequested)
            {
                var candidates = engineers.Where(Idle)
                    .OrderBy(u => load.GetValueOrDefault(u.Id))
                    .ThenBy(u => lastAssigned.GetValueOrDefault(u.Id) ?? DateTime.MinValue)
                    .ThenBy(u => u.Id)
                    .ToList();
                var progress = false;
                foreach (var engineer in candidates)
                {
                    var pick = Pick(queue, SupportLevel.Clamp(engineer.SupportLevel), settings, engineer.Id, givenAway);
                    if (pick == null) continue;
                    queue.RemoveAll(q => q.IssueId == pick.Value);
                    if (await AutoAssignOneAsync(pick.Value, engineer, settings, token))
                    {
                        assigned++;
                        progress = true;
                        load[engineer.Id] = load.GetValueOrDefault(engineer.Id) + 1;
                        lastAssigned[engineer.Id] = DateTime.Now;
                        break; // the least busy engineer is now someone else: start the round again
                    }
                }

                if (!progress) break;
            }

            return assigned;
        }

        private async Task<bool> AutoAssignOneAsync(int issueId, User engineer, AssignmentSettings settings, CancellationToken token)
        {
            await AssignLock.WaitAsync(token);
            try
            {
                var issue = await _db.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId, token);
                if (issue == null || !IsQueued(issue) || SupportLevel.Clamp(issue.SupportLevel) > SupportLevel.Clamp(engineer.SupportLevel)) return false;
                Assign(issue, engineer, null, AutoAssignName, settings, DateTime.Now, staffActed: false);
                await _db.SaveChangesAsync(token);

                // saves outside a web request (the background run) are not seen by NotificationSaveInterceptor: announce it here
                if (_http.HttpContext != null) return true;
                var data = NotificationSaveInterceptor.ForTicket(new NotificationEventData { EventKey = "TicketAssigned", ActorName = AutoAssignName }, NotificationSaveInterceptor.Snapshot(issue));
                _notifications.Publish(data);
                return true;
            }
            finally
            {
                AssignLock.Release();
            }
        }

        // ---------------------------------------------------------------- authority checks used by the existing pages

        /// <summary>
        /// Checks an engineer's Edit Ticket save against their level's authority: assigning somebody else needs "assign others",
        /// taking an unassigned ticket needs self-assign, unassigning yourself needs "give back", closing needs "close".
        /// </summary>
        public async Task<string?> ValidateEngineerEditAsync(IssueTable issue, MakerView form, User me)
        {
            var engineer = await EngineerAsync(me.Id);
            if (engineer == null) return null;
            var (user, authority) = engineer.Value;
            var settings = await SettingsAsync();

            var oldName = string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy;
            var newName = string.IsNullOrWhiteSpace(form.AssgnBy) ? null : form.AssgnBy.Trim();
            if (!string.Equals(oldName, newName, StringComparison.Ordinal) && !authority.CanAssignOthers)
            {
                var mine = issue.AssignedToUserId == user.Id || (issue.AssignedToUserId == null && oldName == user.FullName);
                if (newName == user.FullName && oldName == null)
                {
                    var blocked = SelfAssignError(settings, authority) ?? CapacityError(authority, await OpenCountAsync(user.Id), "You");
                    if (blocked != null) return blocked;
                }
                else if (newName == null && mine)
                {
                    if (!settings.ReleaseEnabled) return "Giving tickets back to the queue is switched off. Ask a support manager.";
                }
                else
                {
                    return SupportLevel.Code(authority.Level) + " engineers cannot assign tickets to other engineers. Request a transfer on the ticket page, or ask a support manager.";
                }
            }

            var closing = form.IStatus?.Trim() == "Close" && issue.IStatus != "Close";
            if (closing && !authority.CanClose)
            {
                return SupportLevel.Code(authority.Level) + " engineers cannot close tickets. Escalate it, or ask a support manager to close it.";
            }

            return null;
        }

        /// <summary>
        /// Checks an engineer's escalate / hand back / reassign against their level's authority. Returns an error or null.
        /// </summary>
        public async Task<string?> ValidateEngineerMoveAsync(IssueTable issue, int toLevel, int? engineerId, User me)
        {
            var engineer = await EngineerAsync(me.Id);
            if (engineer == null) return null;
            var authority = engineer.Value.Authority;
            var from = SupportLevel.Clamp(issue.SupportLevel);
            if (toLevel > from)
            {
                if (!authority.CanEscalate) return SupportLevel.Code(authority.Level) + " engineers cannot escalate tickets. Ask a support manager.";
                if (toLevel > authority.EscalateUpTo) return SupportLevel.Code(authority.Level) + " engineers may escalate up to " + SupportLevel.Code(authority.EscalateUpTo) + ".";
            }
            else if (toLevel < from)
            {
                if (!authority.CanHandBack) return SupportLevel.Code(authority.Level) + " engineers cannot hand tickets back to a lower level. Ask a support manager.";
            }
            else if (engineerId.HasValue && engineerId != issue.AssignedToUserId && engineerId != me.Id && !authority.CanAssignOthers)
            {
                return SupportLevel.Code(authority.Level) + " engineers cannot reassign tickets. Request a transfer instead.";
            }

            return null;
        }

        // ---------------------------------------------------------------- pages

        /// <summary>The assignment part of the ticket's level panel for the signed-in user.</summary>
        public async Task<TicketAssignmentInfo> PanelAsync(IssueTable issue, int userId, string role, List<EngineerOption> engineers)
        {
            var settings = await SettingsAsync();
            var info = new TicketAssignmentInfo();
            var closed = issue.IStatus == "Close";
            info.History = await _db.TicketTransferRequests.AsNoTracking().Where(t => t.IssueId == issue.IssueId)
                .OrderByDescending(t => t.CreatedOn).ThenByDescending(t => t.Id).Take(10).ToListAsync();
            info.OpenTransfer = info.History.FirstOrDefault(t => t.IsOpen);
            info.OpenTransferIsForMe = info.OpenTransfer?.ToUserId == userId && info.OpenTransfer.Status == TicketTransferRequest.Pending;
            info.OpenTransferIsMine = info.OpenTransfer?.FromUserId == userId;
            var manager = role is "Admin" or "SupportManager";
            info.CanApproveTransfer = manager && info.OpenTransfer != null;
            info.CanAssignOthers = manager;
            if (closed) return info;

            if (role == "SupportEngineer")
            {
                var engineer = await EngineerAsync(userId);
                if (engineer == null) return info;
                var (user, authority) = engineer.Value;
                info.CanAssignOthers = authority.CanAssignOthers;
                if (IsQueued(issue))
                {
                    info.TakeBlocked = SelfAssignError(settings, authority) ?? TakeError(issue, authority) ?? CapacityError(authority, await OpenCountAsync(userId), "You");
                    info.CanTake = info.TakeBlocked == null;
                }

                var mine = issue.AssignedToUserId == user.Id;
                info.CanRelease = mine && settings.ReleaseEnabled;
                info.CanRequestTransfer = mine && settings.TransferEnabled && authority.CanRequestTransfer && info.OpenTransfer == null;
                var level = SupportLevel.Clamp(issue.SupportLevel);
                info.TransferTargets = engineers.Where(e => e.Id != user.Id && e.Level >= level).OrderBy(e => e.OpenTickets).ThenBy(e => e.Name).ToList();
            }

            return info;
        }

        /// <summary>Ticket Queue page: the queued tickets, the engineers' workload and the transfer requests.</summary>
        public async Task<TicketQueueView> QueueAsync(User me, string role)
        {
            var view = new TicketQueueView { Role = role, Settings = await SettingsAsync(), Levels = await _tiers.LevelsAsync() };
            view.Engineers = await _tiers.EngineersAsync();
            view.LastSeen = _presence.Snapshot();

            if (role == "SupportEngineer")
            {
                var engineer = await EngineerAsync(me.Id);
                if (engineer != null)
                {
                    view.MyLevel = engineer.Value.Authority.Level;
                    view.MyAuthority = engineer.Value.Authority;
                    view.MyOpen = await OpenCountAsync(me.Id);
                    view.SelfAssignBlocked = SelfAssignError(view.Settings, engineer.Value.Authority) ?? CapacityError(engineer.Value.Authority, view.MyOpen, "You");
                    view.CanSelfAssign = view.SelfAssignBlocked == null;
                }
            }

            var rows = await Queued().AsNoTracking()
                .Select(i => new QueueTicketRow
                {
                    IssueId = i.IssueId,
                    Number = i.TNumber,
                    Title = i.ITitle ?? string.Empty,
                    House = i.Brokerages.BrokerageHouseAcronym,
                    Priority = i.Priority,
                    Status = i.IStatus ?? "Open",
                    Level = i.SupportLevel,
                    Raised = i.TDate
                }).ToListAsync();
            foreach (var r in rows)
            {
                r.Level = SupportLevel.Clamp(r.Level);
                if (view.MyLevel.HasValue)
                {
                    r.WhyNot = view.SelfAssignBlocked ?? (r.Level > view.MyLevel ? "At " + SupportLevel.Code(r.Level) + ", above your level" : null);
                    r.CanTake = r.WhyNot == null;
                }
            }

            // engineers see what they may take first; everybody sees the oldest first
            view.Queue = rows.OrderBy(r => view.MyLevel.HasValue && r.Level > view.MyLevel ? 1 : 0).ThenBy(r => r.Raised).ThenBy(r => r.IssueId).ToList();

            var requests = _db.TicketTransferRequests.AsNoTracking();
            if (role == "SupportEngineer")
            {
                view.Incoming = await TransferRowsAsync(requests.Where(t => t.ToUserId == me.Id && t.Status == TicketTransferRequest.Pending));
                view.Outgoing = await TransferRowsAsync(requests.Where(t => t.FromUserId == me.Id && (t.Status == TicketTransferRequest.Pending || t.Status == TicketTransferRequest.AwaitingApproval)));
            }
            else
            {
                view.Open = await TransferRowsAsync(requests.Where(t => t.Status == TicketTransferRequest.Pending || t.Status == TicketTransferRequest.AwaitingApproval));
            }

            var recentFrom = DateTime.Now.AddDays(-7);
            var recent = requests.Where(t => t.Status != TicketTransferRequest.Pending && t.Status != TicketTransferRequest.AwaitingApproval && t.CreatedOn >= recentFrom);
            if (role == "SupportEngineer")
            {
                recent = recent.Where(t => t.FromUserId == me.Id || t.ToUserId == me.Id);
            }

            view.Recent = await TransferRowsAsync(recent, 20);
            return view;
        }

        private async Task<List<TransferRow>> TransferRowsAsync(IQueryable<TicketTransferRequest> query, int take = 200)
        {
            var list = await query.OrderByDescending(t => t.CreatedOn).ThenByDescending(t => t.Id).Take(take).ToListAsync();
            var ids = list.Select(t => t.IssueId).Distinct().ToList();
            var tickets = await _db.Issues.AsNoTracking().Where(i => ids.Contains(i.IssueId))
                .Select(i => new { i.IssueId, i.TNumber, i.ITitle, i.SupportLevel }).ToDictionaryAsync(i => i.IssueId);
            return list.Select(t => new TransferRow
            {
                Request = t,
                Number = tickets.TryGetValue(t.IssueId, out var i) ? i.TNumber : "#" + t.IssueId,
                Title = tickets.TryGetValue(t.IssueId, out var i2) ? i2.ITitle ?? string.Empty : string.Empty,
                Level = tickets.TryGetValue(t.IssueId, out var i3) ? SupportLevel.Clamp(i3.SupportLevel) : 1
            }).ToList();
        }

        /// <summary>Admin: saves Ticket Assignment. Returns an error text or null.</summary>
        public async Task<string?> SaveSettingsAsync(AssignmentSettings form, User admin)
        {
            if (form.AutoAssignIdleBelow < 1 || form.AutoAssignIdleBelow > 100) return "\"Idle while holding fewer than\" must be between 1 and 100 tickets.";
            if (form.AutoAssignIntervalMinutes < 1 || form.AutoAssignIntervalMinutes > 1440) return "Run auto-assign every 1 to 1440 minutes.";
            if (form.OnlineWindowMinutes < 1 || form.OnlineWindowMinutes > 1440) return "\"Online\" must mean active in the last 1 to 1440 minutes.";
            if (form.AutoAssignDelayMinutes < 0 || form.AutoAssignDelayMinutes > 1440) return "The wait before auto-assign must be 0 to 1440 minutes.";
            if (form.TransferExpiryHours < 0 || form.TransferExpiryHours > 720) return "Transfer requests lapse after 0 to 720 hours (0 = never).";

            var row = await _db.AssignmentSettings.FirstOrDefaultAsync(s => s.Id == AssignmentSettings.SingletonId);
            if (row == null)
            {
                row = new AssignmentSettings();
                _db.AssignmentSettings.Add(row);
            }

            row.SelfAssignEnabled = form.SelfAssignEnabled;
            row.ReleaseEnabled = form.ReleaseEnabled;
            row.StatusOnTake = form.StatusOnTake is "Inqueue" or "Inprogress" ? form.StatusOnTake : null;
            row.AutoAssignEnabled = form.AutoAssignEnabled;
            row.AutoAssignIdleBelow = form.AutoAssignIdleBelow;
            row.AutoAssignIntervalMinutes = form.AutoAssignIntervalMinutes;
            row.AutoAssignLevelRule = AssignmentLevelRules.Normalize(form.AutoAssignLevelRule);
            row.AutoAssignOrder = AssignmentOrders.Normalize(form.AutoAssignOrder);
            row.AutoAssignOnlyOnline = form.AutoAssignOnlyOnline;
            row.OnlineWindowMinutes = form.OnlineWindowMinutes;
            row.AutoAssignDelayMinutes = form.AutoAssignDelayMinutes;
            row.TransferEnabled = form.TransferEnabled;
            row.TransferNeedsManagerApproval = form.TransferNeedsManagerApproval;
            row.TransferExpiryHours = form.TransferExpiryHours;
            row.TransferRespectsCapacity = form.TransferRespectsCapacity;
            row.UpdatedOn = DateTime.Now;
            row.UpdatedBy = admin.FullName;
            await _db.SaveChangesAsync();
            return null;
        }
    }
}
