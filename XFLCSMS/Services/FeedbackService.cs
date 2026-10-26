using System.Globalization;
using System.Text;
using XFLCSMS.Models.Feedback;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;

namespace XFLCSMS.Services
{
    /// <summary>
    /// Client ratings of closed tickets. The client (Maker) who raised a ticket rates it once it is closed;
    /// only Admin and Support Manager can read the ratings.
    /// </summary>
    public class FeedbackService
    {
        /// <summary>Days after closing during which the client can rate (and reopen) a ticket.</summary>
        public const int RatingWindowDays = 14;

        /// <summary>Hours after submitting during which the client can still change the rating.</summary>
        public const int EditHours = 48;

        private readonly DataContext _context;
        private readonly TicketService _tickets;

        public FeedbackService(DataContext context, TicketService tickets)
        {
            _context = context;
            _tickets = tickets;
        }

        public async Task<RatingState> GetStateAsync(IssueTable issue)
        {
            var now = DateTime.Now;
            var rating = await _context.TicketRatings.FirstOrDefaultAsync(r => r.IssueId == issue.IssueId);
            var closed = issue.IStatus == "Close" && issue.ClosedOn != null;
            var until = closed ? issue.ClosedOn!.Value.AddDays(RatingWindowDays) : (DateTime?)null;
            var inWindow = closed && now <= until;

            return new RatingState
            {
                IsClosed = closed,
                Rating = rating,
                RateUntil = until,
                CanRate = inWindow && rating == null,
                CanEdit = rating != null && closed && now <= rating.CreatedOn.AddHours(EditHours),
                CanReopen = inWindow
            };
        }

        /// <summary>Engineer credited with a closed ticket: the assignee, else whoever closed it if he is staff.</summary>
        public (int? Id, string? Name) EngineerFor(IssueTable issue)
        {
            var id = issue.AssignedToUserId ?? _tickets.FindStaffId(issue.AssignBy);
            var name = issue.AssignBy;

            if (id == null && !string.IsNullOrWhiteSpace(issue.ClosedBy))
            {
                id = _context.Users.Where(u => u.FullName == issue.ClosedBy && u.UType).Select(u => (int?)u.Id).FirstOrDefault();
                name = id == null ? null : issue.ClosedBy;
            }

            if (id != null)
            {
                name = _context.Users.Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefault() ?? name;
            }

            return (id, name);
        }

        /// <summary>Saves a new rating or changes one inside the edit window. Returns an error text or null.</summary>
        public async Task<string?> SaveAsync(IssueTable issue, User maker, RatingForm form)
        {
            var state = await GetStateAsync(issue);
            if (!state.CanRate && !state.CanEdit)
            {
                return state.Rating != null
                    ? "This rating can no longer be changed."
                    : "This ticket cannot be rated: it is not closed, or it was closed more than " + RatingWindowDays + " days ago.";
            }

            var comment = string.IsNullOrWhiteSpace(form.Comment) ? null : form.Comment.Trim();
            if (comment != null && comment.Length > 1000)
            {
                comment = comment.Substring(0, 1000);
            }

            var rating = state.Rating;
            if (rating == null)
            {
                var (engineerId, engineerName) = EngineerFor(issue);
                rating = new TicketRating
                {
                    IssueId = issue.IssueId,
                    RatedByUserId = maker.Id,
                    EngineerUserId = engineerId,
                    EngineerName = engineerName,
                    CreatedOn = DateTime.Now
                };
                _context.TicketRatings.Add(rating);
            }
            else
            {
                rating.UpdatedOn = DateTime.Now;
            }

            rating.Stars = (byte)Math.Clamp(form.Stars, 1, 5);
            rating.SpeedScore = Score(form.SpeedScore);
            rating.CommunicationScore = Score(form.CommunicationScore);
            rating.QualityScore = Score(form.QualityScore);
            rating.FullySolved = form.FullySolved;
            rating.Comment = comment;

            await _context.SaveChangesAsync();
            return null;
        }

        private static byte? Score(int? value)
        {
            return value is >= 1 and <= 5 ? (byte)value.Value : null;
        }

        /// <summary>The client says the problem is not solved: the ticket goes back to Open for the same engineer.</summary>
        public async Task<string?> ReopenAsync(IssueTable issue, User maker)
        {
            var state = await GetStateAsync(issue);
            if (!state.CanReopen)
            {
                return "This ticket can no longer be reopened. Please raise a new ticket.";
            }

            var now = DateTime.Now;
            _context.TicketStatusHistories.Add(new TicketStatusHistory
            {
                IssueId = issue.IssueId,
                FromStatus = issue.IStatus,
                ToStatus = "Open",
                AssignedToUserId = issue.AssignedToUserId,
                ChangedByUserId = maker.Id,
                ChangedByName = maker.FullName,
                ChangedOn = now
            });

            issue.IStatus = "Open";
            issue.ClosedOn = null;
            issue.ClosedBy = null;
            issue.UpdatedOn = now;
            issue.UpdatedBy = maker.FullName;

            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<RatingPanelView> PanelAsync(IssueTable issue, string controller)
        {
            var state = await GetStateAsync(issue);
            return new RatingPanelView
            {
                Controller = controller,
                IsClosed = state.IsClosed,
                Rating = state.Rating,
                RateUntil = state.RateUntil,
                History = await _context.TicketStatusHistories.Where(h => h.IssueId == issue.IssueId)
                    .OrderBy(h => h.ChangedOn).ThenBy(h => h.Id).ToListAsync()
            };
        }

        public async Task<ServiceFeedbackView> ListAsync(DateTime? from, DateTime? to, int? engineerId, int? stars, int? houseId, bool followUp)
        {
            var view = new ServiceFeedbackView
            {
                From = from,
                To = to,
                EngineerId = engineerId,
                Stars = stars,
                HouseId = houseId,
                FollowUp = followUp
            };

            view.Engineers = (await _context.Users.Where(u => u.UType && u.Department == "Support Engineer")
                    .OrderBy(u => u.FullName).Select(u => new { u.Id, u.FullName }).ToListAsync())
                .Select(u => (u.Id, u.FullName)).ToList();
            view.Houses = (await _context.Brokerages.OrderBy(b => b.BrokerageHouseName)
                    .Select(b => new { b.BrokerageId, b.BrokerageHouseName }).ToListAsync())
                .Select(b => (b.BrokerageId, b.BrokerageHouseName)).ToList();

            var query = _context.TicketRatings.Include(r => r.Issue).AsQueryable();
            if (from != null)
            {
                query = query.Where(r => r.CreatedOn >= from.Value.Date);
            }

            if (to != null)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(r => r.CreatedOn < end);
            }

            if (engineerId != null)
            {
                query = query.Where(r => r.EngineerUserId == engineerId);
            }

            if (houseId != null)
            {
                query = query.Where(r => r.Issue!.BrokerageId == houseId);
            }

            var all = await query.OrderByDescending(r => r.CreatedOn).ToListAsync();
            view.UnreviewedLow = await _context.TicketRatings.CountAsync(r => r.Stars <= 2 && r.ReviewedOn == null);

            // Summary per engineer over the same filters (except stars / follow-up, which narrow the list only).
            var closedQuery = _context.Issues.Where(i => i.IStatus == "Close" && i.ClosedOn != null);
            if (from != null)
            {
                closedQuery = closedQuery.Where(i => i.ClosedOn >= from.Value.Date);
            }

            if (to != null)
            {
                var end = to.Value.Date.AddDays(1);
                closedQuery = closedQuery.Where(i => i.ClosedOn < end);
            }

            if (houseId != null)
            {
                closedQuery = closedQuery.Where(i => i.BrokerageId == houseId);
            }

            var closedPerEngineer = await closedQuery.Where(i => i.AssignedToUserId != null)
                .GroupBy(i => i.AssignedToUserId).Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync();

            view.Summary = all.GroupBy(r => r.EngineerUserId)
                .Select(g => new EngineerFeedbackSummary
                {
                    EngineerUserId = g.Key,
                    EngineerName = g.Select(r => r.EngineerName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "(not assigned)",
                    Ratings = g.Count(),
                    AverageStars = g.Average(r => (double)r.Stars),
                    FourOrFive = g.Count(r => r.Stars >= 4),
                    OneOrTwo = g.Count(r => r.Stars <= 2),
                    ClosedTickets = closedPerEngineer.FirstOrDefault(c => c.Id == g.Key)?.Count ?? 0
                })
                .Where(s => engineerId == null || s.EngineerUserId == engineerId)
                .OrderByDescending(s => s.AverageStars).ThenByDescending(s => s.Ratings)
                .ToList();

            if (stars != null)
            {
                all = all.Where(r => r.Stars == stars).ToList();
            }

            if (followUp)
            {
                all = all.Where(r => r.Stars <= 2 && r.ReviewedOn == null).ToList();
            }

            var houseNames = view.Houses.ToDictionary(h => h.Id, h => h.Name);
            var raterIds = all.Select(r => r.RatedByUserId).Distinct().ToList();
            var raters = await _context.Users.Where(u => raterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);

            view.Rows = all.Select(r => new FeedbackRow
            {
                Rating = r,
                TicketNumber = r.Issue?.TNumber ?? string.Empty,
                TicketTitle = r.Issue?.ITitle,
                Priority = r.Issue?.Priority,
                HouseName = r.Issue != null && houseNames.TryGetValue(r.Issue.BrokerageId, out var h) ? h : string.Empty,
                RatedByName = raters.TryGetValue(r.RatedByUserId, out var n) ? n : string.Empty
            }).ToList();

            return view;
        }

        public async Task<bool> MarkReviewedAsync(int ratingId, User reviewer, string? note)
        {
            var rating = await _context.TicketRatings.FirstOrDefaultAsync(r => r.RatingId == ratingId);
            if (rating == null)
            {
                return false;
            }

            rating.ReviewedBy = reviewer.FullName;
            rating.ReviewedOn = DateTime.Now;
            rating.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim().Length > 1000 ? note.Trim().Substring(0, 1000) : note.Trim();
            await _context.SaveChangesAsync();
            return true;
        }

        public static byte[] Csv(ServiceFeedbackView view)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Date,Ticket,Title,Priority,Brokerage House,Rated By,Engineer,Stars,Speed,Communication,Solution Quality,Fully Solved,Comment,Reviewed By,Reviewed On,Review Note");
            foreach (var row in view.Rows)
            {
                var r = row.Rating;
                csv.AppendLine(string.Join(",", new[]
                {
                    r.CreatedOn.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), row.TicketNumber, row.TicketTitle, row.Priority, row.HouseName,
                    row.RatedByName, r.EngineerName, r.Stars.ToString(CultureInfo.InvariantCulture), r.SpeedScore?.ToString(CultureInfo.InvariantCulture),
                    r.CommunicationScore?.ToString(CultureInfo.InvariantCulture), r.QualityScore?.ToString(CultureInfo.InvariantCulture),
                    r.FullySolved ? "Yes" : "No", r.Comment, r.ReviewedBy, r.ReviewedOn?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), r.ReviewNote
                }.Select(CsvField)));
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        }

        /// <summary>Quotes a CSV field and neutralises spreadsheet formulas typed by a client.</summary>
        public static string CsvField(string? value)
        {
            value ??= string.Empty;
            if (value.Length > 0 && "=+-@".Contains(value[0]))
            {
                value = "'" + value;
            }

            return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }
    }
}
