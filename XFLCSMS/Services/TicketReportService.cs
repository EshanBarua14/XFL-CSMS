using System.Globalization;
using XFLCSMS.Models.Admin;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Tiers;

namespace XFLCSMS.Services
{
    /// <summary>
    /// One query for every ticket list, the ticket report and the dashboard charts of all four roles:
    /// filters (date range, house, branch, product, status, priority, category, engineer, SLA, age, text),
    /// the detailed rows (names resolved, response / resolution times, SLA state, rating) and the summary
    /// (KPIs, trend, breakdowns, aging, busy hours). Replaces the copy-pasted list and report code that
    /// loaded the whole Issues table into memory for every page.
    /// </summary>
    public class TicketReportService
    {
        /// <summary>Most rows sent to one page; the summary always covers every matching ticket.</summary>
        public const int RowLimit = 10000;

        private readonly DataContext _context;
        private readonly ScorecardService _scorecard;

        public TicketReportService(DataContext context, ScorecardService scorecard)
        {
            _context = context;
            _scorecard = scorecard;
        }

        /// <summary>
        /// Which tickets a role may see at all. Makers: only the tickets they raised (same rule as their ticket pages).
        /// Viewers: every ticket of their house (the XFL viewer, an XFL user, every ticket). XFL staff: every ticket.
        /// </summary>
        public IQueryable<IssueTable> Scope(User user, string role)
        {
            var issues = _context.Issues.AsNoTracking();
            if (role == "Maker") { return issues.Where(i => i.UserId == user.Id); }
            if (role == "Viewer" && !user.UType) { return issues.Where(i => i.BrokerageId == user.BrokerageHouseName); }
            return issues;
        }

        /// <summary>Fixed filters of the menu lists (All / Unassigned / Closed / Assigned to me / Open).</summary>
        public static IQueryable<IssueTable> ApplyPreset(IQueryable<IssueTable> issues, string? preset, User user)
        {
            switch (preset)
            {
                case "unassigned":
                    return issues.Where(i => i.AssignedToUserId == null && (i.AssignBy == null || i.AssignBy == "") && i.IStatus != "Close");
                case "closed":
                    return issues.Where(i => i.IStatus == "Close");
                case "open":
                    return issues.Where(i => i.IStatus != "Close");
                case "engineer":
                    // an engineer's report: tickets assigned to or closed by him, any status
                    return issues.Where(i => i.AssignedToUserId == user.Id || (i.AssignedToUserId == null && i.AssignBy == user.FullName) || i.ClosedBy == user.FullName);
                case "levelqueue":
                    // a Support Engineer's queue: nobody took these yet and they are at the engineer's level or below
                    var level = SupportLevel.Clamp(user.SupportLevel);
                    return issues.Where(i => i.AssignedToUserId == null && (i.AssignBy == null || i.AssignBy == "") && i.IStatus != "Close" && i.SupportLevel <= level);
                case "mine":
                    return issues.Where(i => (i.AssignedToUserId == user.Id || (i.AssignedToUserId == null && i.AssignBy == user.FullName)) && i.IStatus != "Close");
                default:
                    return issues;
            }
        }

        public static (DateTime? From, DateTime? To) ResolvePeriod(TicketFilter filter)
        {
            if (filter.From.HasValue || filter.To.HasValue || string.IsNullOrEmpty(filter.Period))
            {
                return (filter.From?.Date, filter.To?.Date);
            }

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            return filter.Period.ToLowerInvariant() switch
            {
                "today" => (today, today),
                "7d" => (today.AddDays(-6), today),
                "30d" => (today.AddDays(-29), today),
                "90d" => (today.AddDays(-89), today),
                "month" => (monthStart, today),
                "lastmonth" => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
                "quarter" => (new DateTime(today.Year, (today.Month - 1) / 3 * 3 + 1, 1), today),
                "year" => (new DateTime(today.Year, 1, 1), today),
                _ => (null, null)
            };
        }

        /// <summary>Applies the filters that the database can evaluate; SLA and age filters run on the rows.</summary>
        public IQueryable<IssueTable> ApplyFilter(IQueryable<IssueTable> issues, TicketFilter f)
        {
            var (from, to) = ResolvePeriod(f);
            var toExclusive = to?.AddDays(1);
            switch ((f.DateField ?? "created").ToLowerInvariant())
            {
                case "closed":
                    if (from.HasValue) { issues = issues.Where(i => i.ClosedOn >= from.Value); }
                    if (toExclusive.HasValue) { issues = issues.Where(i => i.ClosedOn < toExclusive.Value); }
                    break;
                case "updated":
                    if (from.HasValue) { issues = issues.Where(i => i.UpdatedOn >= from.Value); }
                    if (toExclusive.HasValue) { issues = issues.Where(i => i.UpdatedOn < toExclusive.Value); }
                    break;
                default:
                    if (from.HasValue) { issues = issues.Where(i => i.TDate >= from.Value); }
                    if (toExclusive.HasValue) { issues = issues.Where(i => i.TDate < toExclusive.Value); }
                    break;
            }

            if (f.HouseIds.Count > 0) { issues = issues.Where(i => f.HouseIds.Contains(i.BrokerageId)); }
            if (f.BranchId.HasValue) { issues = issues.Where(i => i.Users.Branch == f.BranchId.Value && i.Users.BrokerageHouseName == i.BrokerageId); }
            if (f.ProductIds.Count > 0) { issues = issues.Where(i => i.ProductId.HasValue && f.ProductIds.Contains(i.ProductId.Value)); }

            var statuses = f.Statuses.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (statuses.Count > 0)
            {
                // tickets saved without a status are "Open"
                var withOpen = statuses.Contains("Open");
                issues = issues.Where(i => statuses.Contains(i.IStatus!) || (withOpen && (i.IStatus == null || i.IStatus == "")));
            }

            var priorities = f.Priorities.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
            if (priorities.Count > 0) { issues = issues.Where(i => priorities.Contains(i.Priority)); }
            if (f.SupportTypeId.HasValue) { issues = issues.Where(i => i.SupportTypeId == f.SupportTypeId); }
            if (f.CategoryId.HasValue) { issues = issues.Where(i => i.SupportCatagoryId == f.CategoryId); }
            if (f.SubCategoryId.HasValue) { issues = issues.Where(i => i.SupportSubCatagoryId == f.SubCategoryId); }
            if (f.SectionId.HasValue) { issues = issues.Where(i => i.AffectedSectionId == f.SectionId); }

            if (f.EngineerId == -1)
            {
                issues = issues.Where(i => i.AssignedToUserId == null && (i.AssignBy == null || i.AssignBy == ""));
            }
            else if (f.EngineerId.HasValue)
            {
                var id = f.EngineerId.Value;
                var name = _context.Users.Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefault() ?? "\u0000";
                issues = issues.Where(i => i.AssignedToUserId == id || (i.AssignedToUserId == null && i.AssignBy == name));
            }

            var levels = f.Levels.Where(l => l >= SupportLevel.Min && l <= SupportLevel.Max).Distinct().ToList();
            if (levels.Count > 0) { issues = issues.Where(i => levels.Contains(i.SupportLevel)); }

            var dev = f.Dev.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            if (dev.Count > 0)
            {
                var stages = dev.Where(DevStages.IsValid).ToList();
                var any = dev.Contains("any");
                var active = dev.Contains("active");
                var none = dev.Contains("none");
                issues = issues.Where(i => (any && i.DevStage != null) || (active && i.DevStage != null && i.DevStage != DevStages.Deployed)
                    || (none && i.DevStage == null) || (i.DevStage != null && stages.Contains(i.DevStage)));
            }

            if (!string.IsNullOrWhiteSpace(f.Q))
            {
                var q = f.Q.Trim();
                issues = issues.Where(i => i.TNumber.Contains(q) || (i.ITitle != null && i.ITitle.Contains(q))
                    || (i.AssignBy != null && i.AssignBy.Contains(q)) || i.Users.FullName.Contains(q));
            }

            return issues;
        }

        /// <summary>SLA hours per priority (Medium when a ticket has an unknown priority).</summary>
        private async Task<Func<string?, (double Response, double Resolution)>> SlaAsync()
        {
            // the only place that reads the SLA target fields
            var targets = (await _scorecard.SlaTargetsAsync())
                .ToDictionary(t => t.Priority, t => (t.FirstResponseMinutes / 60.0, t.ResolutionMinutes / 60.0), StringComparer.OrdinalIgnoreCase);
            var fallback = targets.TryGetValue("Medium", out var m) ? m : (4d, 48d);
            return p => p != null && targets.TryGetValue(p, out var t) ? t : fallback;
        }

        public async Task<List<TicketRow>> RowsAsync(IQueryable<IssueTable> issues, TicketFilter? filter = null)
        {
            var data = await issues
                .Select(i => new
                {
                    i.IssueId, i.TNumber, i.ITitle, i.IStatus, i.Priority, i.ProductId, i.BrokerageId,
                    OwnerName = i.Users.FullName, OwnerBranch = i.Users.Branch, OwnerHouse = i.Users.BrokerageHouseName,
                    i.SupportTypeId, i.SupportCatagoryId, i.SupportSubCatagoryId, i.AffectedSectionId,
                    i.AssignedToUserId, i.AssignBy, i.TDate, i.AssignOn, i.FirstResponseOn, i.UpdatedOn, i.UpdatedBy, i.ClosedOn, i.ClosedBy,
                    i.SupportLevel, i.DevStage, i.DevOwnerUserId, i.DevEta
                })
                .ToListAsync();

            var ids = data.Select(d => d.IssueId).ToList();
            var houses = await _context.Brokerages.AsNoTracking().ToDictionaryAsync(b => b.BrokerageId, b => (b.BrokerageHouseName, b.BrokerageHouseAcronym));
            var branches = await _context.Branchhs.AsNoTracking().ToDictionaryAsync(b => b.BranchId, b => b.BranchName);
            var products = await _context.Products.AsNoTracking().ToDictionaryAsync(p => p.ProductId, p => p.ProductName);
            var types = await _context.SupportTypes.AsNoTracking().ToDictionaryAsync(x => x.SupportTypeId, x => x.SType);
            var categories = await _context.SupportCatagories.AsNoTracking().ToDictionaryAsync(x => x.SupportCatagoryId, x => x.SCatagory);
            var subCategories = await _context.SupportSubCatagories.AsNoTracking().ToDictionaryAsync(x => x.SupportSubCatagoryId, x => x.SubCatagory);
            var sections = await _context.AffectedSectionss.AsNoTracking().ToDictionaryAsync(x => x.AffectedSectionId, x => x.ASection);
            var staff = await _context.Users.AsNoTracking().Where(u => u.UType || u.UCatagory).ToDictionaryAsync(u => u.Id, u => u.FullName);
            var staffByName = staff.GroupBy(s => s.Value).ToDictionary(g => g.Key, g => g.First().Key);

            // ratings and attachment counts for these tickets only (in chunks: SQL Server allows ~2100 parameters)
            var ratings = new Dictionary<int, int>();
            var attachments = new Dictionary<int, int>();
            var escalations = new Dictionary<int, int>();
            foreach (var chunk in ids.Chunk(1500))
            {
                foreach (var e in await _context.TicketEscalations.AsNoTracking().Where(e => chunk.Contains(e.IssueId) && e.ToLevel > e.FromLevel).GroupBy(e => e.IssueId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                {
                    escalations[e.Key] = e.Count;
                }

                foreach (var r in await _context.TicketRatings.AsNoTracking().Where(r => chunk.Contains(r.IssueId)).Select(r => new { r.IssueId, r.Stars }).ToListAsync())
                {
                    ratings[r.IssueId] = r.Stars;
                }

                foreach (var a in await _context.Attachments.AsNoTracking().Where(a => chunk.Contains(a.IssueId)).GroupBy(a => a.IssueId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                {
                    attachments[a.Key] = a.Count;
                }
            }

            var sla = await SlaAsync();
            var now = DateTime.Now;
            var rows = new List<TicketRow>(data.Count);

            foreach (var d in data)
            {
                var status = string.IsNullOrWhiteSpace(d.IStatus) ? "Open" : d.IStatus!;
                var closed = status == "Close";
                var (responseTarget, resolutionTarget) = sla(d.Priority);
                var end = closed ? (d.ClosedOn ?? d.UpdatedOn ?? now) : now;
                var age = Math.Max(0, (end - d.TDate).TotalHours);
                var responded = d.FirstResponseOn ?? (closed ? d.ClosedOn : null);
                double? responseHours = responded.HasValue ? Math.Max(0, (responded.Value - d.TDate).TotalHours) : null;

                int? engineerId = d.AssignedToUserId;
                if (engineerId == null && !string.IsNullOrWhiteSpace(d.AssignBy) && staffByName.TryGetValue(d.AssignBy!, out var byName))
                {
                    engineerId = byName;
                }

                var house = houses.TryGetValue(d.BrokerageId, out var h) ? h : (string.Empty, string.Empty);
                rows.Add(new TicketRow
                {
                    Id = d.IssueId,
                    Number = d.TNumber,
                    Title = d.ITitle ?? string.Empty,
                    Status = status,
                    StatusLabel = TicketService.StatusLabel(status),
                    Priority = d.Priority ?? string.Empty,
                    Product = d.ProductId.HasValue && products.TryGetValue(d.ProductId.Value, out var p) ? p : string.Empty,
                    HouseId = d.BrokerageId,
                    House = house.Item1,
                    HouseCode = house.Item2,
                    // the raiser's branch, when the raiser belongs to the ticket's house (staff raise tickets for houses)
                    Branch = d.OwnerHouse == d.BrokerageId && branches.TryGetValue(d.OwnerBranch, out var br) ? br : string.Empty,
                    RaisedBy = d.OwnerName ?? string.Empty,
                    SupportType = d.SupportTypeId.HasValue && types.TryGetValue(d.SupportTypeId.Value, out var t) ? t : string.Empty,
                    Category = d.SupportCatagoryId.HasValue && categories.TryGetValue(d.SupportCatagoryId.Value, out var c) ? c : string.Empty,
                    SubCategory = d.SupportSubCatagoryId.HasValue && subCategories.TryGetValue(d.SupportSubCatagoryId.Value, out var sc) ? sc : string.Empty,
                    Section = d.AffectedSectionId.HasValue && sections.TryGetValue(d.AffectedSectionId.Value, out var se) ? se : string.Empty,
                    EngineerId = engineerId,
                    Engineer = engineerId.HasValue && staff.TryGetValue(engineerId.Value, out var en) ? en : (d.AssignBy ?? string.Empty),
                    Created = d.TDate,
                    Assigned = d.AssignOn,
                    FirstResponse = d.FirstResponseOn,
                    Updated = d.UpdatedOn,
                    UpdatedBy = d.UpdatedBy ?? string.Empty,
                    Closed = closed ? d.ClosedOn : null,
                    ClosedBy = closed ? d.ClosedBy ?? string.Empty : string.Empty,
                    IsClosed = closed,
                    AgeHours = Math.Round(age, 2),
                    ResponseHours = responseHours.HasValue ? Math.Round(responseHours.Value, 2) : null,
                    ResolutionHours = closed ? Math.Round(age, 2) : null,
                    ResponseTargetHours = responseTarget,
                    ResolutionTargetHours = resolutionTarget,
                    ResponseSla = responseHours.HasValue
                        ? (responseHours <= responseTarget ? "Met" : "Breached")
                        : ((now - d.TDate).TotalHours > responseTarget ? "Breached" : "Pending"),
                    ResolutionSla = closed
                        ? (age <= resolutionTarget ? "Met" : "Breached")
                        : (age > resolutionTarget ? "Breached" : age > resolutionTarget * 0.75 ? "At risk" : "On track"),
                    Rating = ratings.TryGetValue(d.IssueId, out var stars) ? stars : null,
                    Attachments = attachments.TryGetValue(d.IssueId, out var count) ? count : 0,
                    Level = SupportLevel.Clamp(d.SupportLevel),
                    LevelCode = SupportLevel.Code(SupportLevel.Clamp(d.SupportLevel)),
                    Escalations = escalations.TryGetValue(d.IssueId, out var esc) ? esc : 0,
                    DevStage = d.DevStage == null ? string.Empty : DevStages.Label(d.DevStage),
                    DevOwner = d.DevOwnerUserId.HasValue && staff.TryGetValue(d.DevOwnerUserId.Value, out var owner) ? owner : string.Empty,
                    DevEta = d.DevEta,
                    DevOverdue = d.DevEta.HasValue && DevStages.IsActive(d.DevStage) && d.DevEta.Value.Date < now.Date
                });
            }

            if (filter != null)
            {
                switch ((filter.Sla ?? string.Empty).ToLowerInvariant())
                {
                    case "met": rows = rows.Where(r => r.ResolutionSla == "Met").ToList(); break;
                    case "breached": rows = rows.Where(r => r.ResolutionSla == "Breached" || r.ResponseSla == "Breached").ToList(); break;
                    case "response-breached": rows = rows.Where(r => r.ResponseSla == "Breached").ToList(); break;
                    case "resolution-breached": rows = rows.Where(r => r.ResolutionSla == "Breached").ToList(); break;
                    case "at-risk": rows = rows.Where(r => r.ResolutionSla == "At risk").ToList(); break;
                }

                if (filter.OlderThanDays.HasValue)
                {
                    rows = rows.Where(r => !r.IsClosed && r.AgeHours >= filter.OlderThanDays.Value * 24).ToList();
                }
            }

            return rows.OrderByDescending(r => r.Created).ThenByDescending(r => r.Id).ToList();
        }

        public static TicketSummary Summarize(List<TicketRow> rows, DateTime? from, DateTime? to)
        {
            var s = new TicketSummary
            {
                Total = rows.Count,
                Open = rows.Count(r => r.Status == "Open"),
                InQueue = rows.Count(r => r.Status == "Inqueue"),
                InProgress = rows.Count(r => r.Status == "Inprogress"),
                Closed = rows.Count(r => r.IsClosed),
                Unassigned = rows.Count(r => !r.IsClosed && string.IsNullOrEmpty(r.Engineer)),
                HighOpen = rows.Count(r => !r.IsClosed && r.Priority == "High"),
                OverdueOpen = rows.Count(r => !r.IsClosed && r.ResolutionSla == "Breached")
            };

            var responded = rows.Where(r => r.ResponseHours.HasValue).Select(r => r.ResponseHours!.Value).ToList();
            s.AvgFirstResponseHours = responded.Count > 0 ? responded.Average() : null;
            s.MedianResolutionHours = Median(rows.Where(r => r.ResolutionHours.HasValue).Select(r => r.ResolutionHours!.Value));
            var responseDecided = rows.Where(r => r.ResponseSla != "Pending").ToList();
            s.SlaResponseMetPercent = responseDecided.Count > 0 ? 100.0 * responseDecided.Count(r => r.ResponseSla == "Met") / responseDecided.Count : null;
            var closed = rows.Where(r => r.IsClosed).ToList();
            s.SlaResolutionMetPercent = closed.Count > 0 ? 100.0 * closed.Count(r => r.ResolutionSla == "Met") / closed.Count : null;
            var rated = rows.Where(r => r.Rating.HasValue).ToList();
            s.Rated = rated.Count;
            s.AvgRating = rated.Count > 0 ? rated.Average(r => r.Rating!.Value) : null;

            s.ByStatus = TicketService.Statuses
                .Select(st => new BreakdownRow { Name = st.Label, Total = rows.Count(r => r.Status == st.Value) })
                .Concat(rows.Where(r => TicketService.Statuses.All(st => st.Value != r.Status)).GroupBy(r => r.Status).Select(g => new BreakdownRow { Name = g.Key, Total = g.Count() }))
                .ToList();
            s.ByPriority = TicketService.Priorities.Select(p => Group(p, rows.Where(r => r.Priority == p))).ToList();
            s.ByProduct = GroupBy(rows, r => r.Product, "(no product)");
            s.ByHouse = GroupBy(rows, r => r.House, "(unknown house)");
            s.ByBranch = GroupBy(rows, r => string.IsNullOrEmpty(r.Branch) ? string.Empty : r.HouseCode + " / " + r.Branch, "(raised by XFL staff)");
            s.ByEngineer = GroupBy(rows, r => r.Engineer, "(unassigned)");
            s.ByCategory = GroupBy(rows, r => r.Category, "(no category)");
            s.BySupportType = GroupBy(rows, r => r.SupportType, "(no support type)");
            s.ByLevel = Enumerable.Range(SupportLevel.Min, SupportLevel.Max).Select(l => Group(SupportLevel.Code(l), rows.Where(r => r.Level == l))).ToList();
            s.ByDevStage = DevStages.All.Select(st => Group(st.Label, rows.Where(r => r.DevStage == st.Label))).Where(b => b.Total > 0).ToList();
            s.Escalated = rows.Count(r => r.Escalations > 0);
            s.InDevelopment = rows.Count(r => r.DevStage != string.Empty && r.DevStage != DevStages.Label(DevStages.Deployed));

            var open = rows.Where(r => !r.IsClosed).ToList();
            s.Aging = new List<(string, int)>
            {
                ("< 1 day", open.Count(r => r.AgeHours < 24)),
                ("1-3 days", open.Count(r => r.AgeHours >= 24 && r.AgeHours < 72)),
                ("3-7 days", open.Count(r => r.AgeHours >= 72 && r.AgeHours < 168)),
                ("7-30 days", open.Count(r => r.AgeHours >= 168 && r.AgeHours < 720)),
                ("> 30 days", open.Count(r => r.AgeHours >= 720))
            };

            foreach (var r in rows)
            {
                var day = ((int)r.Created.DayOfWeek + 6) % 7; // Monday first
                s.Heat[day][r.Created.Hour]++;
            }

            s.Trend = Trend(rows, from, to, out var unit);
            s.TrendUnit = unit;
            return s;
        }

        /// <summary>Created and closed tickets per day (up to ~2 months), week (up to ~1 year) or month.</summary>
        private static List<(string, int, int)> Trend(List<TicketRow> rows, DateTime? from, DateTime? to, out string unit)
        {
            var result = new List<(string, int, int)>();
            var end = (to ?? DateTime.Today).Date;
            var start = (from ?? (rows.Count > 0 ? rows.Min(r => r.Created) : end.AddDays(-29))).Date;
            if (start > end) { start = end; }
            var days = (end - start).TotalDays + 1;

            if (days <= 62)
            {
                unit = "day";
                for (var d = start; d <= end; d = d.AddDays(1))
                {
                    var next = d.AddDays(1);
                    result.Add((d.ToString("dd MMM", CultureInfo.InvariantCulture), rows.Count(r => r.Created >= d && r.Created < next), rows.Count(r => r.Closed >= d && r.Closed < next)));
                }
            }
            else if (days <= 400)
            {
                unit = "week";
                var monday = start.AddDays(-(((int)start.DayOfWeek + 6) % 7));
                for (var d = monday; d <= end; d = d.AddDays(7))
                {
                    var next = d.AddDays(7);
                    result.Add(("w/c " + d.ToString("dd MMM", CultureInfo.InvariantCulture), rows.Count(r => r.Created >= d && r.Created < next), rows.Count(r => r.Closed >= d && r.Closed < next)));
                }
            }
            else
            {
                unit = "month";
                for (var d = new DateTime(start.Year, start.Month, 1); d <= end; d = d.AddMonths(1))
                {
                    var next = d.AddMonths(1);
                    result.Add((d.ToString("MMM yyyy", CultureInfo.InvariantCulture), rows.Count(r => r.Created >= d && r.Created < next), rows.Count(r => r.Closed >= d && r.Closed < next)));
                }
            }

            return result;
        }

        private static List<BreakdownRow> GroupBy(List<TicketRow> rows, Func<TicketRow, string> key, string empty)
        {
            return rows
                .GroupBy(r => string.IsNullOrWhiteSpace(key(r)) ? empty : key(r))
                .Select(g => Group(g.Key, g))
                .OrderByDescending(b => b.Total)
                .ThenBy(b => b.Name)
                .ToList();
        }

        private static BreakdownRow Group(string name, IEnumerable<TicketRow> items)
        {
            var list = items.ToList();
            var closed = list.Where(r => r.IsClosed).ToList();
            var rated = list.Where(r => r.Rating.HasValue).ToList();
            return new BreakdownRow
            {
                Name = name,
                Total = list.Count,
                Open = list.Count - closed.Count,
                Closed = closed.Count,
                Breached = list.Count(r => r.ResolutionSla == "Breached"),
                SlaMetPercent = closed.Count > 0 ? 100.0 * closed.Count(r => r.ResolutionSla == "Met") / closed.Count : null,
                AvgResolutionHours = closed.Count > 0 ? closed.Average(r => r.ResolutionHours ?? 0) : null,
                AvgRating = rated.Count > 0 ? rated.Average(r => r.Rating!.Value) : null
            };
        }

        private static double? Median(IEnumerable<double> values)
        {
            var list = values.OrderBy(v => v).ToList();
            if (list.Count == 0) { return null; }
            var mid = list.Count / 2;
            return list.Count % 2 == 1 ? list[mid] : (list[mid - 1] + list[mid]) / 2;
        }

        /// <summary>Builds the whole page model: lookups, rows (capped) and the summary of all matching tickets.</summary>
        public async Task<TicketExplorerView> BuildAsync(User user, string role, string controller, string action, string mode,
            string? preset, TicketFilter filter)
        {
            if (preset == "levelqueue")
            {
                // the level may have changed since the engineer signed in
                user.SupportLevel = await _context.Users.AsNoTracking().Where(u => u.Id == user.Id).Select(u => u.SupportLevel).FirstOrDefaultAsync();
            }

            var issues = ApplyPreset(Scope(user, role), preset, user);
            issues = ApplyFilter(issues, filter);
            var rows = await RowsAsync(issues, filter);
            var (from, to) = ResolvePeriod(filter);

            var view = new TicketExplorerView
            {
                Mode = mode,
                Role = role,
                Controller = controller,
                Action = action,
                Preset = preset,
                Filter = filter,
                TotalMatching = rows.Count,
                RowLimit = RowLimit,
                Rows = rows.Count > RowLimit ? rows.Take(RowLimit).ToList() : rows,
                Summary = Summarize(rows, from, to),
                ShowHouseFilter = role != "Maker" && !(role == "Viewer" && !user.UType),
                ShowEngineerFilter = true,
                CanEdit = role != "Viewer",
                CanDelete = role == "Admin"
            };

            await FillLookupsAsync(view, user, role);
            view.FilterDescription = Describe(view, from, to);
            return view;
        }

        /// <summary>Summary only (dashboards): no lookups, no rows sent to the page.</summary>
        public async Task<TicketSummary> SummaryAsync(User user, string role, string? preset, DateTime from, DateTime to)
        {
            var filter = new TicketFilter { From = from, To = to };
            var issues = ApplyFilter(ApplyPreset(Scope(user, role), preset, user), filter);
            var rows = await RowsAsync(issues, filter);
            var summary = Summarize(rows, from, to);

            // the backlog charts (status, aging) describe every open ticket, not only the ones raised in the period
            var openRows = await RowsAsync(ApplyPreset(Scope(user, role), preset ?? "open", user).Where(i => i.IStatus != "Close"));
            var open = Summarize(openRows, from, to);
            summary.Aging = open.Aging;
            summary.OverdueOpen = open.OverdueOpen;
            summary.Unassigned = open.Unassigned;
            summary.HighOpen = open.HighOpen;
            return summary;
        }

        private async Task FillLookupsAsync(TicketExplorerView view, User user, string role)
        {
            view.Houses = await _context.Brokerages.AsNoTracking().OrderBy(b => b.BrokerageHouseName)
                .Select(b => new Option(b.BrokerageId.ToString(), b.BrokerageHouseName + " (" + b.BrokerageHouseAcronym + ")")).ToListAsync();

            var branches = _context.Branchhs.AsNoTracking();
            if (role == "Maker" || (role == "Viewer" && !user.UType)) { branches = branches.Where(b => b.BrokerageId == user.BrokerageHouseName); }
            view.Branches = await branches.OrderBy(b => b.Brokerage!.BrokerageHouseAcronym).ThenBy(b => b.BranchName)
                .Select(b => new Option(b.BranchId.ToString(), (b.Brokerage != null ? b.Brokerage.BrokerageHouseAcronym + " / " : "") + b.BranchName)).ToListAsync();

            view.Products = await _context.Products.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.ProductName)
                .Select(p => new Option(p.ProductId.ToString(), p.ProductName + (p.IsActive ? "" : " (inactive)"))).ToListAsync();
            view.SupportTypes = await _context.SupportTypes.AsNoTracking().OrderBy(x => x.SType).Select(x => new Option(x.SupportTypeId.ToString(), x.SType)).ToListAsync();
            view.Categories = await _context.SupportCatagories.AsNoTracking().OrderBy(x => x.SCatagory).Select(x => new Option(x.SupportCatagoryId.ToString(), x.SCatagory)).ToListAsync();
            view.SubCategories = await _context.SupportSubCatagories.AsNoTracking().OrderBy(x => x.SubCatagory).Select(x => new Option(x.SupportSubCatagoryId.ToString(), x.SubCatagory)).ToListAsync();
            view.Sections = await _context.AffectedSectionss.AsNoTracking().OrderBy(x => x.ASection).Select(x => new Option(x.AffectedSectionId.ToString(), x.ASection)).ToListAsync();
            view.Engineers = await _context.Users.AsNoTracking()
                .Where(u => u.UType && (u.Department == "Support Engineer" || u.Department == "Support Maneger"))
                .OrderBy(u => u.FullName)
                .Select(u => new Option(u.Id.ToString(), u.FullName + (u.UStatus ? "" : " (disabled)"))).ToListAsync();
            view.Levels = await _context.SupportLevels.AsNoTracking().OrderBy(l => l.Level)
                .Select(l => new Option(l.Level.ToString(), "L" + l.Level + " · " + l.Name)).ToListAsync();
        }

        private static List<string> Describe(TicketExplorerView v, DateTime? from, DateTime? to)
        {
            var f = v.Filter;
            var list = new List<string>();
            string Names(IEnumerable<string> values, List<Option> options) =>
                string.Join(", ", values.Select(x => options.FirstOrDefault(o => o.Value == x)?.Text ?? x));

            var dateName = (f.DateField ?? "created").ToLowerInvariant() switch { "closed" => "Closed", "updated" => "Updated", _ => "Created" };
            if (from.HasValue || to.HasValue)
            {
                list.Add(dateName + ": " + (from?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "start") + " to " + (to?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "today"));
            }

            if (f.HouseIds.Count > 0) { list.Add("House: " + Names(f.HouseIds.Select(x => x.ToString()), v.Houses)); }
            if (f.BranchId.HasValue) { list.Add("Branch: " + Names(new[] { f.BranchId.Value.ToString() }, v.Branches)); }
            if (f.ProductIds.Count > 0) { list.Add("Product: " + Names(f.ProductIds.Select(x => x.ToString()), v.Products)); }
            if (f.Statuses.Count > 0) { list.Add("Status: " + string.Join(", ", f.Statuses.Select(TicketService.StatusLabel))); }
            if (f.Priorities.Count > 0) { list.Add("Priority: " + string.Join(", ", f.Priorities)); }
            if (f.SupportTypeId.HasValue) { list.Add("Support type: " + Names(new[] { f.SupportTypeId.Value.ToString() }, v.SupportTypes)); }
            if (f.CategoryId.HasValue) { list.Add("Category: " + Names(new[] { f.CategoryId.Value.ToString() }, v.Categories)); }
            if (f.SubCategoryId.HasValue) { list.Add("Sub-category: " + Names(new[] { f.SubCategoryId.Value.ToString() }, v.SubCategories)); }
            if (f.SectionId.HasValue) { list.Add("Affected section: " + Names(new[] { f.SectionId.Value.ToString() }, v.Sections)); }
            if (f.EngineerId == -1) { list.Add("Engineer: not assigned"); }
            else if (f.EngineerId.HasValue) { list.Add("Engineer: " + Names(new[] { f.EngineerId.Value.ToString() }, v.Engineers)); }
            if (!string.IsNullOrEmpty(f.Sla)) { list.Add("SLA: " + f.Sla.Replace('-', ' ')); }
            if (f.OlderThanDays.HasValue) { list.Add("Open longer than " + f.OlderThanDays + " days"); }
            if (f.Levels.Count > 0) { list.Add("Level: " + string.Join(", ", f.Levels.OrderBy(l => l).Select(l => SupportLevel.Code(l)))); }
            if (f.Dev.Count > 0)
            {
                list.Add("Development: " + string.Join(", ", f.Dev.Select(d => d switch { "any" => "tracked", "active" => "in progress", "none" => "none", _ => DevStages.Label(d) })));
            }
            if (!string.IsNullOrWhiteSpace(f.Q)) { list.Add("Search: \"" + f.Q.Trim() + "\""); }
            return list;
        }
    }
}
