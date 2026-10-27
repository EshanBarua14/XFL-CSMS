using XFLCSMS.Models.Notifications;
using XFLCSMS.Models.Tiers;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>
    /// Checks open tickets against the SLA targets (Admin > SLA Targets) and raises the SLA warning / breach
    /// events once per ticket. Breaches older than the look-back window (e.g. after the server was off) are
    /// remembered without being announced, so a restart never floods everybody with old alerts.
    /// </summary>
    public class SlaMonitor
    {
        private readonly DataContext _db;
        private readonly ScorecardService _scorecard;
        private readonly NotificationSettingsStore _settings;
        private readonly NotificationDispatcher _dispatcher;
        private readonly NotificationQueue _queue;
        private readonly ILogger<SlaMonitor> _logger;

        public SlaMonitor(DataContext db, ScorecardService scorecard, NotificationSettingsStore settings, NotificationDispatcher dispatcher, NotificationQueue queue,
            ILogger<SlaMonitor> logger)
        {
            _logger = logger;
            _db = db;
            _scorecard = scorecard;
            _settings = settings;
            _dispatcher = dispatcher;
            _queue = queue;
        }

        public async Task ScanAsync(CancellationToken token)
        {
            var targets = (await _scorecard.SlaTargetsAsync()).ToDictionary(t => t.Priority, StringComparer.OrdinalIgnoreCase);
            var warnShare = Math.Clamp(_settings.GetInt(SettingKeys.SlaWarningPercent, 80), 10, 99) / 100.0;
            var lookback = TimeSpan.FromHours(Math.Max(1, _settings.GetInt(SettingKeys.SlaLookbackHours, 24)));
            var now = DateTime.Now;

            var open = await _db.Issues.AsNoTracking()
                .Where(i => i.IStatus != "Close")
                .Select(i => new
                {
                    i.IssueId, i.TNumber, i.ITitle, i.Priority, i.IStatus, i.UserId, i.BrokerageId, i.ProductId, i.AssignedToUserId, i.AssignBy, i.TDate, i.FirstResponseOn,
                    i.SupportLevel, i.LevelSince
                })
                .ToListAsync(token);
            if (open.Count == 0) return;

            var ids = open.Select(i => i.IssueId).ToList();
            var done = (await _db.SlaAlerts.AsNoTracking().Where(a => ids.Contains(a.IssueId)).Select(a => new { a.IssueId, a.EventKey }).ToListAsync(token))
                .Select(a => (a.IssueId, a.EventKey)).ToHashSet();

            var levels = await _db.SupportLevels.AsNoTracking().ToDictionaryAsync(l => l.Level, token);
            var dispatched = 0;
            foreach (var i in open)
            {
                // time allowed at the support level (Admin > Support Levels): one "escalation due" per stay at a level
                if (levels.TryGetValue(i.SupportLevel, out var level) && level.MaxHoursAtLevel > 0)
                {
                    var since = i.LevelSince ?? i.TDate;
                    var due = since.AddHours(level.MaxHoursAtLevel);
                    var alertKey = "EscalationDue:L" + i.SupportLevel + ":" + since.ToString("yyyyMMddHHmmss");
                    if (now >= due && !done.Contains((i.IssueId, alertKey)))
                    {
                        _db.SlaAlerts.Add(new SlaAlert { IssueId = i.IssueId, EventKey = alertKey, CreatedOn = now });
                        await _db.SaveChangesAsync(token);
                        if (now - due <= lookback)
                        {
                            var data = NotificationSaveInterceptor.ForTicket(new NotificationEventData { EventKey = "EscalationDue", ActorName = "SLA monitor" }, new TicketSnapshot
                            {
                                IssueId = i.IssueId, TNumber = i.TNumber, Title = i.ITitle, Priority = i.Priority, Status = i.IStatus, OwnerUserId = i.UserId,
                                BrokerageId = i.BrokerageId, ProductId = i.ProductId, EngineerUserId = i.AssignedToUserId,
                                EngineerName = string.IsNullOrWhiteSpace(i.AssignBy) ? null : i.AssignBy
                            });
                            data.StaffOnly = true;
                            data.Values["Level"] = SupportLevel.Code(i.SupportLevel);
                            data.Values["LevelName"] = level.Name;
                            data.Values["DueAt"] = due.ToString("dd MMM yyyy HH:mm");
                            data.Values["Overdue"] = Duration(now - due);
                            try
                            {
                                dispatched += await _dispatcher.DispatchAsync(data, token);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                _logger.LogError(ex, "Could not send EscalationDue for ticket {Ticket}.", i.TNumber);
                            }
                        }
                    }
                }

                if (!targets.TryGetValue(i.Priority ?? string.Empty, out var target)) continue;

                var checks = new List<(string Kind, string Warning, string Breach, DateTime Due)>();
                if (i.FirstResponseOn == null)
                {
                    checks.Add(("first response", "SlaFirstResponseWarning", "SlaFirstResponseBreach", i.TDate.AddMinutes(target.FirstResponseMinutes)));
                }

                checks.Add(("resolution", "SlaResolutionWarning", "SlaResolutionBreach", i.TDate.AddMinutes(target.ResolutionMinutes)));

                foreach (var (kind, warning, breach, due) in checks)
                {
                    string? key = null;
                    var announce = true;
                    if (now >= due)
                    {
                        if (!done.Contains((i.IssueId, breach)))
                        {
                            key = breach;
                            announce = now - due <= lookback;
                        }
                    }
                    else if (now >= i.TDate + TimeSpan.FromTicks((long)((due - i.TDate).Ticks * warnShare)) && !done.Contains((i.IssueId, warning)))
                    {
                        key = warning;
                    }

                    if (key == null) continue;

                    _db.SlaAlerts.Add(new SlaAlert { IssueId = i.IssueId, EventKey = key, CreatedOn = now });
                    await _db.SaveChangesAsync(token);
                    if (!announce) continue;

                    var data = NotificationSaveInterceptor.ForTicket(new NotificationEventData { EventKey = key, ActorName = "SLA monitor" }, new TicketSnapshot
                    {
                        IssueId = i.IssueId, TNumber = i.TNumber, Title = i.ITitle, Priority = i.Priority, Status = i.IStatus, OwnerUserId = i.UserId,
                        BrokerageId = i.BrokerageId, ProductId = i.ProductId, EngineerUserId = i.AssignedToUserId,
                        EngineerName = string.IsNullOrWhiteSpace(i.AssignBy) ? null : i.AssignBy
                    });
                    data.Values["SlaKind"] = kind;
                    data.Values["DueAt"] = due.ToString("dd MMM yyyy HH:mm");
                    data.Values["Overdue"] = now > due ? Duration(now - due) : string.Empty;
                    try
                    {
                        dispatched += await _dispatcher.DispatchAsync(data, token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Could not send {Event} for ticket {Ticket}.", key, i.TNumber);
                    }
                }
            }

            if (dispatched > 0) _queue.Wake();
        }

        public static string Duration(TimeSpan span)
        {
            if (span.TotalDays >= 1) return (int)span.TotalDays + "d " + span.Hours + "h";
            if (span.TotalHours >= 1) return (int)span.TotalHours + "h " + span.Minutes + "m";
            return Math.Max(1, (int)span.TotalMinutes) + "m";
        }
    }
}
