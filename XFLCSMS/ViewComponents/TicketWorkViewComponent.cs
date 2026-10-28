using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Services;
using XFLCSMS.Services.Notifications;

namespace XFLCSMS.ViewComponents
{
    /// <summary>
    /// Support level (L1-L5) and development panel of the four Ticket Details pages: where the ticket is, its escalation
    /// history and its development stage, owner, ETA and timeline. Staff also get the escalate and update forms.
    /// </summary>
    public class TicketWorkViewComponent : ViewComponent
    {
        private readonly TierService _tiers;
        private readonly AssignmentService _assignment;
        private readonly DataContext _db;

        public TicketWorkViewComponent(TierService tiers, AssignmentService assignment, DataContext db)
        {
            _tiers = tiers;
            _assignment = assignment;
            _db = db;
        }

        public async Task<IViewComponentResult> InvokeAsync(int issueId)
        {
            var controller = (string?)ViewContext.RouteData.Values["controller"] ?? string.Empty;
            var role = controller switch
            {
                "Admin" => "Admin",
                "SupportManegar" => "SupportManager",
                "SupportEngineer" => "SupportEngineer",
                _ => "Maker"
            };

            var view = await _tiers.PanelAsync(issueId, role, controller);
            if (view == null)
            {
                return Content(string.Empty);
            }

            var (userId, _) = NotificationSaveInterceptor.Actor(HttpContext);
            if (view.IsStaff && userId.HasValue)
            {
                var issue = await _db.Issues.AsNoTracking().FirstAsync(i => i.IssueId == issueId);
                view.Assignment = await _assignment.PanelAsync(issue, userId.Value, role, view.Engineers);
                if (role == "SupportEngineer")
                {
                    var engineer = await _assignment.EngineerAsync(userId.Value);
                    var authority = engineer?.Authority;
                    view.CanHandBack = authority?.CanHandBack == true;
                    view.CanEscalate = authority?.CanEscalate == true;
                    view.EscalateUpTo = authority?.EscalateUpTo ?? view.Level;
                    view.CanReassign = authority?.CanAssignOthers == true;
                }
            }

            return View(view);
        }
    }
}
