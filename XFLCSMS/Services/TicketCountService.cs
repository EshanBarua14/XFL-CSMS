using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using XFLCSMS.Models.Admin;

namespace XFLCSMS.Services
{
    /// <summary>
    /// Counts tickets for the page "Ticket Counts" (administrator and support manager).
    /// A ticket belongs to a period by the day it was raised. Status values are the ones the application stores:
    /// "Close", "Inprogress", "Inqueue"; everything else counts as open. The assigned engineer is IssueTable.AssignBy.
    /// </summary>
    public class TicketCountService
    {
        private readonly DataContext _context;

        public TicketCountService(DataContext context)
        {
            _context = context;
        }

        /// <summary>The period of a request: explicit dates win, then a named period, else the last 30 days.</summary>
        public static (DateTime? From, DateTime? To) Period(string? period, DateTime? from, DateTime? to)
        {
            if (from.HasValue || to.HasValue)
            {
                return (from, to);
            }

            var today = DateTime.Today;
            switch ((period ?? string.Empty).ToLowerInvariant())
            {
                case "today": return (today, today);
                case "week": return (today.AddDays(-6), today);
                case "month": return (new DateTime(today.Year, today.Month, 1), today);
                case "year": return (new DateTime(today.Year, 1, 1), today);
                case "all": return (null, null);
                default: return (today.AddDays(-29), today);
            }
        }

        private sealed class Row
        {
            public int BrokerageId;
            public int UserId;
            public DateTime Raised;
            public string Status = "Open";
            public string Priority = string.Empty;
            public string? Engineer;
            public int? SupportTypeId;
            public DateTime? ClosedOn;
            public bool IsClosed => Status == "Close";
        }

        private static string StatusOf(string? stored)
        {
            switch ((stored ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "close": case "closed": return "Close";
                case "inprogress": case "in progress": return "Inprogress";
                case "inqueue": case "in queue": return "Inqueue";
                default: return "Open";
            }
        }

        public async Task<TicketCountsView> CountAsync(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue && from > to)
            {
                (from, to) = (to, from);
            }

            var start = from?.Date;
            var end = to?.Date.AddDays(1); // the last day counts completely

            var all = (await _context.Issues.AsNoTracking()
                    .Select(i => new { i.BrokerageId, i.UserId, i.TDate, i.IStatus, i.Priority, i.AssignBy, i.SupportTypeId, i.ClosedOn })
                    .ToListAsync())
                .Select(i => new Row
                {
                    BrokerageId = i.BrokerageId,
                    UserId = i.UserId,
                    Raised = i.TDate,
                    Status = StatusOf(i.IStatus),
                    Priority = string.IsNullOrWhiteSpace(i.Priority) ? "(none)" : i.Priority.Trim(),
                    Engineer = string.IsNullOrWhiteSpace(i.AssignBy) ? null : i.AssignBy.Trim(),
                    SupportTypeId = i.SupportTypeId,
                    ClosedOn = i.ClosedOn
                })
                .ToList();

            var rows = all.Where(r => (start == null || r.Raised >= start) && (end == null || r.Raised < end)).ToList();

            var view = new TicketCountsView
            {
                From = start,
                To = to?.Date,
                PeriodText = start == null && to == null ? "All time"
                    : (start?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "the beginning") + " to " + (to?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "today"),
                Raised = rows.Count,
                Open = rows.Count(r => r.Status == "Open"),
                InQueue = rows.Count(r => r.Status == "Inqueue"),
                InProgress = rows.Count(r => r.Status == "Inprogress"),
                Closed = rows.Count(r => r.IsClosed),
                Unassigned = rows.Count(r => !r.IsClosed && r.Engineer == null),
                HighNotClosed = rows.Count(r => !r.IsClosed && string.Equals(r.Priority, "High", StringComparison.OrdinalIgnoreCase)),
                ClosedInPeriod = all.Count(r => r.IsClosed && r.ClosedOn.HasValue && (start == null || r.ClosedOn >= start) && (end == null || r.ClosedOn < end)),
                AllTime = all.Count,
                AllTimeNotClosed = all.Count(r => !r.IsClosed)
            };

            var hours = rows.Where(r => r.IsClosed && r.ClosedOn.HasValue && r.ClosedOn >= r.Raised).Select(r => (r.ClosedOn!.Value - r.Raised).TotalHours).ToList();
            view.AverageHoursToClose = hours.Count == 0 ? null : Math.Round(hours.Average(), 1);

            // every house is listed, also the ones without a ticket in the period: a zero is an answer too
            var houses = await _context.Brokerages.AsNoTracking().OrderBy(b => b.BrokerageHouseName).ToListAsync();
            foreach (var house in houses)
            {
                var mine = rows.Where(r => r.BrokerageId == house.BrokerageId).ToList();
                view.Houses.Add(new HouseCount
                {
                    Name = house.BrokerageHouseName,
                    Acronym = house.BrokerageHouseAcronym,
                    Raised = mine.Count,
                    Open = mine.Count(r => r.Status == "Open"),
                    InQueue = mine.Count(r => r.Status == "Inqueue"),
                    InProgress = mine.Count(r => r.Status == "Inprogress"),
                    Closed = mine.Count(r => r.IsClosed),
                    Unassigned = mine.Count(r => !r.IsClosed && r.Engineer == null),
                    High = mine.Count(r => string.Equals(r.Priority, "High", StringComparison.OrdinalIgnoreCase))
                });
            }

            view.Engineers = Group(rows, r => r.Engineer ?? "(not assigned)");

            var types = await _context.SupportTypes.AsNoTracking().ToDictionaryAsync(t => t.SupportTypeId, t => t.SType);
            view.SupportTypes = Group(rows, r => r.SupportTypeId.HasValue && types.TryGetValue(r.SupportTypeId.Value, out var name) && !string.IsNullOrWhiteSpace(name) ? name : "(not set)");

            view.Priorities = Group(rows, r => r.Priority);

            var users = await _context.Users.AsNoTracking().Select(u => new { u.Id, u.FullName, u.UserName }).ToDictionaryAsync(u => u.Id, u => u.FullName + " (" + u.UserName + ")");
            view.RaisedBy = Group(rows, r => users.TryGetValue(r.UserId, out var name) ? name : "(deleted user)").Take(15).ToList();

            // by day for up to two months, by month for longer periods
            var first = start ?? (rows.Count > 0 ? rows.Min(r => r.Raised).Date : DateTime.Today);
            var last = to?.Date ?? DateTime.Today;
            var byDay = (last - first).TotalDays <= 62;
            view.TimelineUnit = byDay ? "Day" : "Month";
            view.Timeline = rows
                .GroupBy(r => byDay ? r.Raised.Date : new DateTime(r.Raised.Year, r.Raised.Month, 1))
                .OrderByDescending(g => g.Key)
                .Select(g => new NameCount
                {
                    Name = g.Key.ToString(byDay ? "dd MMM yyyy (ddd)" : "MMM yyyy", CultureInfo.InvariantCulture),
                    Raised = g.Count(),
                    Closed = g.Count(r => r.IsClosed),
                    NotClosed = g.Count(r => !r.IsClosed)
                })
                .ToList();

            return view;
        }

        private static List<NameCount> Group(IEnumerable<Row> rows, Func<Row, string> key)
        {
            return rows.GroupBy(key)
                .Select(g => new NameCount { Name = g.Key, Raised = g.Count(), Closed = g.Count(r => r.IsClosed), NotClosed = g.Count(r => !r.IsClosed) })
                .OrderByDescending(c => c.Raised).ThenBy(c => c.Name)
                .ToList();
        }

        /// <summary>The same numbers as a file for a spreadsheet.</summary>
        public static byte[] Csv(TicketCountsView view)
        {
            var text = new StringBuilder();
            void Line(params object?[] cells) => text.AppendLine(string.Join(",", cells.Select(Cell)));

            Line("XFL CSMS - Ticket counts");
            Line("Period", view.PeriodText);
            Line("Made on", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Line();
            Line("Raised", "Open", "In Queue", "In Progress", "Closed", "Not assigned", "Closed in the period", "Average hours to close");
            Line(view.Raised, view.Open, view.InQueue, view.InProgress, view.Closed, view.Unassigned, view.ClosedInPeriod, view.AverageHoursToClose?.ToString(CultureInfo.InvariantCulture) ?? "");
            Line();
            Line("Brokerage house", "Acronym", "Raised", "Open", "In Queue", "In Progress", "Closed", "Not assigned", "High priority");
            foreach (var h in view.Houses)
            {
                Line(h.Name, h.Acronym, h.Raised, h.Open, h.InQueue, h.InProgress, h.Closed, h.Unassigned, h.High);
            }

            Line("Total", "", view.Houses.Sum(h => h.Raised), view.Houses.Sum(h => h.Open), view.Houses.Sum(h => h.InQueue), view.Houses.Sum(h => h.InProgress),
                view.Houses.Sum(h => h.Closed), view.Houses.Sum(h => h.Unassigned), view.Houses.Sum(h => h.High));

            void Block(string title, List<NameCount> counts)
            {
                Line();
                Line(title, "Raised", "Not closed", "Closed");
                foreach (var c in counts)
                {
                    Line(c.Name, c.Raised, c.NotClosed, c.Closed);
                }
            }

            Block("Engineer", view.Engineers);
            Block("Support type", view.SupportTypes);
            Block("Priority", view.Priorities);
            Block("Raised by", view.RaisedBy);
            Block(view.TimelineUnit, view.Timeline);

            // with a byte order mark, so that Excel reads the names right
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        }

        private static string Cell(object? value)
        {
            var s = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            // a cell that starts like a formula must not run as one when the file is opened
            if (s.Length > 0 && "=+-@".IndexOf(s[0]) >= 0 && !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            {
                s = "'" + s;
            }

            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
    }
}
