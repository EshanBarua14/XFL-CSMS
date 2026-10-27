using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Register;
using XFLCSMS.Services;

namespace XFLCSMS.ViewComponents
{
    /// <summary>
    /// Ratings are management-only. These components check the session themselves, so even when one is placed on
    /// a page another role can open, it renders nothing for that role.
    /// </summary>
    internal static class FeedbackAccess
    {
        /// <summary>"Admin" or "SupportManegar" for management sessions, otherwise null.</summary>
        public static string? ManagementController(HttpContext context)
        {
            if (!string.IsNullOrEmpty(context.Session.GetString(SessionAuthorizeAttribute.Admin)))
            {
                return "Admin";
            }

            return string.IsNullOrEmpty(context.Session.GetString(SessionAuthorizeAttribute.SupportManager)) ? null : "SupportManegar";
        }

        public static User? Maker(HttpContext context)
        {
            var json = context.Session.GetString(SessionAuthorizeAttribute.Maker);
            return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<User>(json);
        }
    }

    /// <summary>Client rating and status history on the staff ticket page (Admin / Support Manager only).</summary>
    public class RatingPanelViewComponent : ViewComponent
    {
        private readonly DataContext _context;
        private readonly FeedbackService _feedback;

        public RatingPanelViewComponent(DataContext context, FeedbackService feedback)
        {
            _context = context;
            _feedback = feedback;
        }

        public async Task<IViewComponentResult> InvokeAsync(int issueId)
        {
            var controller = FeedbackAccess.ManagementController(HttpContext);
            var issue = controller == null ? null : await _context.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId);
            if (issue == null)
            {
                return Content(string.Empty);
            }

            return View(await _feedback.PanelAsync(issue, controller!));
        }
    }

    /// <summary>Best Support Engineer of the Month card on the Admin / Support Manager dashboards.</summary>
    public class AwardCardViewComponent : ViewComponent
    {
        private readonly ScorecardService _scorecard;

        public AwardCardViewComponent(ScorecardService scorecard)
        {
            _scorecard = scorecard;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var controller = FeedbackAccess.ManagementController(HttpContext);
            if (controller == null)
            {
                return Content(string.Empty);
            }

            return View(await _scorecard.CardAsync(controller));
        }
    }

    /// <summary>Rate / reopen box on the client's own ticket page.</summary>
    public class MakerRatingViewComponent : ViewComponent
    {
        private readonly DataContext _context;
        private readonly FeedbackService _feedback;

        public MakerRatingViewComponent(DataContext context, FeedbackService feedback)
        {
            _context = context;
            _feedback = feedback;
        }

        public async Task<IViewComponentResult> InvokeAsync(int issueId)
        {
            var maker = FeedbackAccess.Maker(HttpContext);
            var issue = maker == null ? null : await _context.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId && i.UserId == maker.Id);
            if (issue == null)
            {
                return Content(string.Empty);
            }

            var state = await _feedback.GetStateAsync(issue);
            if (!state.IsClosed)
            {
                return Content(string.Empty);
            }

            ViewBag.IssueId = issue.IssueId;
            return View(state);
        }
    }

    /// <summary>Closed tickets the client can still rate (Maker dashboard).</summary>
    public class PendingRatingsViewComponent : ViewComponent
    {
        private readonly DataContext _context;

        public PendingRatingsViewComponent(DataContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var maker = FeedbackAccess.Maker(HttpContext);
            if (maker == null)
            {
                return Content(string.Empty);
            }

            var since = DateTime.Now.AddDays(-FeedbackService.RatingWindowDays);
            var pending = await _context.Issues
                .Where(i => i.UserId == maker.Id && i.IStatus == "Close" && i.ClosedOn >= since
                    && !_context.TicketRatings.Any(r => r.IssueId == i.IssueId))
                .OrderByDescending(i => i.ClosedOn)
                .Take(5)
                .ToListAsync();

            return pending.Count == 0 ? Content(string.Empty) : View(pending);
        }
    }
}
