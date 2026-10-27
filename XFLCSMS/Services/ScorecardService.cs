using System.Globalization;
using System.Text;
using XFLCSMS.Models.Feedback;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;

namespace XFLCSMS.Services
{
    /// <summary>
    /// Monthly Support Engineer scorecard and the Best Support Engineer of the Month award.
    ///
    /// Seven measures per engineer and month, each turned into 0-100 points by percentile rank among the
    /// eligible engineers (best = 100), then weighted: client rating 30, SLA compliance 20, resolution time 15,
    /// weighted volume 15, reopen rate 10, first response 5, backlog health 5 (appsettings "Scorecard").
    /// A month is frozen on the 3rd of the next month; the top eligible engineer who passes the award gates
    /// is nominated and a manager confirms or overrides (confirmed automatically after 5 days).
    /// There is no background job in this application, so freezing happens the first time a management page
    /// needs it after the freeze date.
    /// </summary>
    public class ScorecardService
    {
        private static readonly Dictionary<string, double> PriorityPoints = new(StringComparer.OrdinalIgnoreCase)
        {
            ["High"] = 3, ["Medium"] = 2, ["Low"] = 1
        };

        private readonly DataContext _context;
        private readonly ILogger<ScorecardService> _logger;

        public ScorecardService(DataContext context, IConfiguration configuration, ILogger<ScorecardService> logger)
        {
            _context = context;
            _logger = logger;
            configuration.GetSection("Scorecard").Bind(Settings);
        }

        public ScorecardSettings Settings { get; } = new();

        public static DateTime MonthStart(DateTime date) => new(date.Year, date.Month, 1);

        public DateTime FreezeDate(DateTime month) => MonthStart(month).AddMonths(1).AddDays(Math.Clamp(Settings.FreezeDay, 1, 28) - 1);

        public async Task<List<SlaTarget>> SlaTargetsAsync()
        {
            var targets = await _context.SlaTargets.ToListAsync();
            foreach (var d in SlaTarget.Defaults.Where(d => targets.All(t => !string.Equals(t.Priority, d.Priority, StringComparison.OrdinalIgnoreCase))))
            {
                targets.Add(new SlaTarget { Priority = d.Priority, FirstResponseHours = d.FirstResponseHours, ResolutionHours = d.ResolutionHours });
            }

            return targets.OrderBy(t => PriorityPoints.TryGetValue(t.Priority, out var p) ? -p : 0).ToList();
        }

        /// <summary>First day of every month in which at least one ticket was closed, newest first.</summary>
        public async Task<List<DateTime>> MonthsWithClosedTicketsAsync()
        {
            var months = await _context.Issues
                .Where(i => i.IStatus == "Close" && i.ClosedOn != null)
                .Select(i => new { i.ClosedOn!.Value.Year, i.ClosedOn.Value.Month })
                .Distinct()
                .ToListAsync();
            return months.Select(m => new DateTime(m.Year, m.Month, 1)).OrderByDescending(m => m).ToList();
        }

        /// <summary>Freezes every finished month with closed tickets that is due and not frozen yet, and auto-confirms old nominations.</summary>
        public async Task CatchUpAsync()
        {
            var now = DateTime.Now;
            var lastDue = MonthStart(now).AddMonths(now >= FreezeDate(MonthStart(now).AddMonths(-1)) ? -1 : -2);

            var frozen = await _context.EngineerAwards.Select(a => a.Month).ToListAsync();
            foreach (var month in (await MonthsWithClosedTicketsAsync()).Where(m => m <= lastDue && !frozen.Contains(m)).OrderBy(m => m))
            {
                await FreezeAsync(month);
            }

            var limit = now.AddDays(-Settings.AutoConfirmDays);
            var stale = await _context.EngineerAwards.Where(a => a.Status == EngineerAward.Nominated && a.NominatedOn <= limit).ToListAsync();
            foreach (var award in stale)
            {
                award.Status = EngineerAward.Confirmed;
                award.DecidedBy = "Automatic (no decision within " + Settings.AutoConfirmDays + " days)";
                award.DecidedOn = now;
            }

            if (stale.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        /// <summary>Computes and stores the month's scores and nominates the award winner. Does nothing when already frozen.</summary>
        public async Task FreezeAsync(DateTime month)
        {
            month = MonthStart(month);
            if (await _context.EngineerAwards.AnyAsync(a => a.Month == month))
            {
                return;
            }

            var now = DateTime.Now;
            var rows = await ComputeAsync(month);
            foreach (var row in rows)
            {
                row.FrozenOn = now;
            }

            var ranked = rows.Where(r => r.Rank != null).OrderBy(r => r.Rank).ToList();
            var winner = ranked.FirstOrDefault(PassesGates);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.MonthlyEngineerScores.RemoveRange(_context.MonthlyEngineerScores.Where(s => s.Month == month));
                _context.MonthlyEngineerScores.AddRange(rows);
                _context.EngineerAwards.Add(new EngineerAward
                {
                    Month = month,
                    EngineerUserId = winner?.EngineerUserId,
                    EngineerName = winner?.EngineerName,
                    Score = winner?.Score,
                    TopRankedName = ranked.FirstOrDefault()?.EngineerName,
                    Status = winner == null ? EngineerAward.NoWinner : EngineerAward.Nominated,
                    NominatedOn = now,
                    Reason = winner == null
                        ? (ranked.Count == 0 ? "No engineer closed the minimum of " + Settings.MinClosedTickets + " tickets." : "No ranked engineer passed the award rules (at least " + Settings.MinRatings + " client ratings, adjusted rating " + Settings.AwardMinRating.ToString("0.0") + ", SLA met " + (Settings.AwardMinSla * 100).ToString("0") + "%, no unreviewed 1-star rating).")
                        : winner.Rank > 1 ? "Higher ranked engineers did not pass the award rules (number of ratings, rating, SLA or an unreviewed 1-star rating)." : null
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException ex)
            {
                // Two requests froze the same month at once: the unique index kept one copy.
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                _logger.LogInformation(ex, "Scorecard for {Month:yyyy-MM} was frozen by another request.", month);
            }
        }

        public bool PassesGates(MonthlyEngineerScore row)
        {
            return row.Rank != null
                && row.RatingsCount >= Settings.MinRatings
                && (row.AdjustedRating ?? 0) >= Settings.AwardMinRating
                && (row.SlaCompliance ?? 0) >= Settings.AwardMinSla
                && row.LowRatingsUnreviewed == 0;
        }

        public async Task<ScorecardView> ViewAsync(DateTime? month)
        {
            await CatchUpAsync();

            var now = DateTime.Now;
            var withData = await MonthsWithClosedTicketsAsync();

            // Without a month in the address: last month, or the latest month that has closed tickets when last month has none.
            var lastMonth = MonthStart(now).AddMonths(-1);
            var latestWithData = withData.FirstOrDefault(m => m < MonthStart(now));
            var selected = MonthStart(month
                ?? (withData.Contains(lastMonth) || latestWithData == default ? lastMonth : latestWithData));
            var award = await _context.EngineerAwards.FirstOrDefaultAsync(a => a.Month == selected);

            var view = new ScorecardView
            {
                Month = selected,
                IsFrozen = award != null,
                FreezesOn = FreezeDate(selected),
                Award = award,
                Settings = Settings,
                Sla = await SlaTargetsAsync(),
                AwardHistory = await _context.EngineerAwards.OrderByDescending(a => a.Month).Take(12).ToListAsync()
            };

            view.Rows = award != null
                ? await _context.MonthlyEngineerScores.Where(s => s.Month == selected).ToListAsync()
                : await ComputeAsync(selected);
            view.Rows = view.Rows.OrderBy(r => r.Rank ?? int.MaxValue).ThenByDescending(r => r.TicketsClosed).ThenBy(r => r.EngineerName).ToList();

            var previous = selected.AddMonths(-1);
            var prevRows = await _context.MonthlyEngineerScores.Where(s => s.Month == previous).ToListAsync();
            if (prevRows.Count == 0)
            {
                prevRows = await ComputeAsync(previous);
            }

            view.PreviousRanks = prevRows.GroupBy(r => r.EngineerUserId).ToDictionary(g => g.Key, g => g.First().Rank);

            // The current month, the last 12 months and every older month that has closed tickets.
            view.Months = Enumerable.Range(0, 13).Select(i => MonthStart(now).AddMonths(-i))
                .Concat(withData).Append(selected).Distinct().OrderByDescending(m => m).ToList();

            return view;
        }

        public async Task<AwardCardView> CardAsync(string controller)
        {
            await CatchUpAsync();
            var award = await _context.EngineerAwards.Where(a => a.Status != EngineerAward.NoWinner)
                .OrderByDescending(a => a.Month).FirstOrDefaultAsync();
            var score = award?.EngineerUserId == null
                ? null
                : await _context.MonthlyEngineerScores.FirstOrDefaultAsync(s => s.Month == award.Month && s.EngineerUserId == award.EngineerUserId);

            return new AwardCardView
            {
                Controller = controller,
                Award = award,
                Score = score,
                UnreviewedLowRatings = await _context.TicketRatings.CountAsync(r => r.Stars <= 2 && r.ReviewedOn == null)
            };
        }

        /// <summary>Confirms the nominee, or names another engineer of that month's scorecard (reason required).</summary>
        public async Task<string?> DecideAsync(DateTime month, User manager, int? engineerUserId, string? reason)
        {
            month = MonthStart(month);
            var award = await _context.EngineerAwards.FirstOrDefaultAsync(a => a.Month == month);
            if (award == null)
            {
                return "This month is not closed yet.";
            }

            var now = DateTime.Now;
            if (engineerUserId == null || engineerUserId == award.EngineerUserId)
            {
                if (award.EngineerUserId == null)
                {
                    return "There is no nominee to confirm. Choose an engineer and give a reason.";
                }

                award.Status = EngineerAward.Confirmed;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(reason))
                {
                    return "Please give the reason for choosing another engineer.";
                }

                var row = await _context.MonthlyEngineerScores.FirstOrDefaultAsync(s => s.Month == month && s.EngineerUserId == engineerUserId);
                if (row == null)
                {
                    return "That engineer has no score for this month.";
                }

                award.EngineerUserId = row.EngineerUserId;
                award.EngineerName = row.EngineerName;
                award.Score = row.Score;
                award.Status = EngineerAward.Overridden;
            }

            award.DecidedBy = manager.FullName;
            award.DecidedOn = now;
            if (!string.IsNullOrWhiteSpace(reason))
            {
                award.Reason = reason.Trim().Length > 1000 ? reason.Trim().Substring(0, 1000) : reason.Trim();
            }

            await _context.SaveChangesAsync();
            return null;
        }

        /// <summary>The scorecard of one month as it stands now (not stored).</summary>
        public async Task<List<MonthlyEngineerScore>> ComputeAsync(DateTime month)
        {
            month = MonthStart(month);
            var start = month;
            var end = month.AddMonths(1);
            var s = Settings;

            var sla = (await SlaTargetsAsync()).ToDictionary(t => t.Priority, StringComparer.OrdinalIgnoreCase);
            SlaTarget Sla(string? priority) => priority != null && sla.TryGetValue(priority, out var t) ? t : sla["Medium"];

            var engineers = await _context.Users.Where(u => u.UType && u.Department == "Support Engineer")
                .Select(u => new { u.Id, u.FullName }).ToListAsync();
            var engineerIds = engineers.Select(e => e.Id).ToList();

            var closed = await _context.Issues
                .Where(i => i.IStatus == "Close" && i.ClosedOn >= start && i.ClosedOn < end
                    && i.AssignedToUserId != null && engineerIds.Contains(i.AssignedToUserId.Value))
                .ToListAsync();

            // A ticket the engineer raised himself and closed within 5 minutes is not real support work.
            closed = closed.Where(i => !(i.UserId == i.AssignedToUserId && (i.ClosedOn!.Value - i.TDate).TotalMinutes < 5)).ToList();

            var ratings = await _context.TicketRatings
                .Where(r => r.CreatedOn >= start && r.CreatedOn < end && r.EngineerUserId != null && engineerIds.Contains(r.EngineerUserId.Value))
                .ToListAsync();
            var teamAverage = ratings.Count == 0 ? 0 : ratings.Average(r => (double)r.Stars);

            var reopens = (await _context.TicketStatusHistories
                    .Where(h => h.FromStatus == "Close" && h.ToStatus != "Close" && h.ChangedOn >= start && h.ChangedOn < end && h.AssignedToUserId != null)
                    .Select(h => new { h.AssignedToUserId, h.IssueId })
                    .ToListAsync())
                .GroupBy(h => h.AssignedToUserId!.Value)
                .Select(g => new { Id = g.Key, Count = g.Select(h => h.IssueId).Distinct().Count() })
                .ToList();

            // Open at the end of the month: raised before the end and not closed by then.
            var openAtEnd = await _context.Issues
                .Where(i => i.TDate < end && i.AssignedToUserId != null && engineerIds.Contains(i.AssignedToUserId.Value)
                    && (i.IStatus != "Close" || i.ClosedOn >= end))
                .Select(i => new { i.AssignedToUserId, i.TDate, i.Priority })
                .ToListAsync();
            var backlogAt = end <= DateTime.Now ? end : DateTime.Now;

            var rows = new List<MonthlyEngineerScore>();
            foreach (var engineer in engineers)
            {
                var mine = closed.Where(i => i.AssignedToUserId == engineer.Id).ToList();
                var myRatings = ratings.Where(r => r.EngineerUserId == engineer.Id).ToList();
                var myOpen = openAtEnd.Where(i => i.AssignedToUserId == engineer.Id).ToList();
                var reopened = reopens.FirstOrDefault(r => r.Id == engineer.Id)?.Count ?? 0;

                if (mine.Count == 0 && myRatings.Count == 0 && myOpen.Count == 0 && reopened == 0)
                {
                    continue;
                }

                var row = new MonthlyEngineerScore
                {
                    Month = month,
                    EngineerUserId = engineer.Id,
                    EngineerName = engineer.FullName,
                    TicketsClosed = mine.Count,
                    WeightedVolume = mine.Sum(i => PriorityPoints.TryGetValue(i.Priority ?? string.Empty, out var p) ? p : 1),
                    RatingsCount = myRatings.Count,
                    AverageStars = myRatings.Count == 0 ? null : myRatings.Average(r => (double)r.Stars),
                    LowRatingsUnreviewed = myRatings.Count(r => r.Stars == 1 && r.ReviewedOn == null)
                };

                if (myRatings.Count > 0)
                {
                    row.AdjustedRating = (s.RatingConfidence * teamAverage + myRatings.Sum(r => (double)r.Stars)) / (s.RatingConfidence + myRatings.Count);
                }

                if (mine.Count > 0)
                {
                    row.SlaCompliance = mine.Count(i => (i.ClosedOn!.Value - i.TDate).TotalHours <= Sla(i.Priority).ResolutionHours) / (double)mine.Count;
                    row.MedianResolutionHours = Median(mine.Select(i => (i.ClosedOn!.Value - (i.AssignOn ?? i.TDate)).TotalHours));
                    row.ResolutionRatio = Median(mine.Select(i => Math.Max(0, (i.ClosedOn!.Value - (i.AssignOn ?? i.TDate)).TotalHours) / Sla(i.Priority).ResolutionHours));
                    row.ReopenRate = reopened / (double)mine.Count;

                    var responses = mine.Where(i => i.FirstResponseOn != null)
                        .Select(i => Math.Max(0, (i.FirstResponseOn!.Value - (i.AssignOn ?? i.TDate)).TotalMinutes)).ToList();
                    row.MedianFirstResponseMinutes = responses.Count == 0 ? null : Median(responses);
                }
                else if (reopened > 0)
                {
                    row.ReopenRate = 1;
                }

                row.BacklogOverdueShare = myOpen.Count == 0
                    ? 0
                    : myOpen.Count(i => (backlogAt - i.TDate).TotalHours > 2 * Sla(i.Priority).ResolutionHours) / (double)myOpen.Count;

                // Ranked on closed tickets alone: an engineer without client ratings gets neutral rating points (50).
                // The award additionally needs MinRatings ratings (PassesGates).
                row.Eligible = row.TicketsClosed >= s.MinClosedTickets;
                rows.Add(row);
            }

            var eligible = rows.Where(r => r.Eligible).ToList();
            Points(eligible, r => r.AdjustedRating, true, (r, p) => r.RatingPoints = p);
            Points(eligible, r => r.SlaCompliance, true, (r, p) => r.SlaPoints = p);
            Points(eligible, r => r.ResolutionRatio, false, (r, p) => r.ResolutionPoints = p);
            Points(eligible, r => r.WeightedVolume, true, (r, p) => r.VolumePoints = p);
            Points(eligible, r => r.ReopenRate, false, (r, p) => r.ReopenPoints = p);
            Points(eligible, r => r.MedianFirstResponseMinutes, false, (r, p) => r.FirstResponsePoints = p);
            Points(eligible, r => r.BacklogOverdueShare, false, (r, p) => r.BacklogPoints = p);

            var total = s.TotalWeight <= 0 ? 1 : s.TotalWeight;
            foreach (var r in eligible)
            {
                r.Score = Math.Round((s.RatingWeight * r.RatingPoints + s.SlaWeight * r.SlaPoints + s.ResolutionWeight * r.ResolutionPoints
                    + s.VolumeWeight * r.VolumePoints + s.ReopenWeight * r.ReopenPoints + s.FirstResponseWeight * r.FirstResponsePoints
                    + s.BacklogWeight * r.BacklogPoints) / total, 1);
            }

            var rank = 1;
            foreach (var r in eligible
                .OrderByDescending(r => r.Score)
                .ThenByDescending(r => r.AdjustedRating ?? 0)
                .ThenByDescending(r => r.SlaCompliance ?? 0)
                .ThenByDescending(r => r.WeightedVolume)
                .ThenBy(r => r.EngineerName))
            {
                r.Rank = rank++;
            }

            return rows;
        }

        /// <summary>
        /// Percentile points: 100 for the best engineer, 0 for the worst, ties share the average. An engineer
        /// without a value for the measure (e.g. no first response recorded) gets a neutral 50.
        /// </summary>
        public static void Points(List<MonthlyEngineerScore> rows, Func<MonthlyEngineerScore, double?> value, bool higherIsBetter, Action<MonthlyEngineerScore, double> set)
        {
            var withValue = rows.Where(r => value(r) != null).ToList();
            foreach (var r in rows.Where(r => value(r) == null))
            {
                set(r, 50);
            }

            if (withValue.Count == 1)
            {
                set(withValue[0], 100);
                return;
            }

            foreach (var r in withValue)
            {
                var v = value(r)!.Value;
                var worse = withValue.Count(o => o != r && (higherIsBetter ? value(o) < v : value(o) > v));
                var ties = withValue.Count(o => o != r && Math.Abs(value(o)!.Value - v) < 1e-9);
                set(r, Math.Round(100.0 * (worse + 0.5 * ties) / (withValue.Count - 1), 1));
            }
        }

        public static double? Median(IEnumerable<double> values)
        {
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 0)
            {
                return null;
            }

            var mid = list.Count / 2;
            return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2;
        }

        public static byte[] Csv(ScorecardView view)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Month,Rank,Engineer,Eligible,Score,Tickets Closed,Weighted Volume,Ratings,Average Stars,Adjusted Rating,SLA %,Median Resolution h,Resolution vs SLA,Reopen %,Median First Response min,Backlog Overdue %,Rating pts,SLA pts,Resolution pts,Volume pts,Reopen pts,First Response pts,Backlog pts");
            string N(double? v, string f = "0.##") => v?.ToString(f, CultureInfo.InvariantCulture) ?? string.Empty;
            foreach (var r in view.Rows)
            {
                csv.AppendLine(string.Join(",", new[]
                {
                    view.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture), r.Rank?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    FeedbackService.CsvField(r.EngineerName), r.Eligible ? "Yes" : "No", N(r.Score, "0.0"), r.TicketsClosed.ToString(CultureInfo.InvariantCulture),
                    N(r.WeightedVolume), r.RatingsCount.ToString(CultureInfo.InvariantCulture), N(r.AverageStars), N(r.AdjustedRating),
                    N(r.SlaCompliance * 100, "0.#"), N(r.MedianResolutionHours, "0.#"), N(r.ResolutionRatio), N(r.ReopenRate * 100, "0.#"),
                    N(r.MedianFirstResponseMinutes, "0"), N(r.BacklogOverdueShare * 100, "0.#"),
                    N(r.RatingPoints, "0.#"), N(r.SlaPoints, "0.#"), N(r.ResolutionPoints, "0.#"), N(r.VolumePoints, "0.#"),
                    N(r.ReopenPoints, "0.#"), N(r.FirstResponsePoints, "0.#"), N(r.BacklogPoints, "0.#")
                }));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        }
    }
}
