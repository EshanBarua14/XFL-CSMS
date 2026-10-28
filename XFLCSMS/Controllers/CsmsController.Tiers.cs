using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Models.Tiers;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    /// <summary>
    /// Support levels (L1-L5) and development tracking for the three staff roles: escalate / hand back a ticket,
    /// update its development stage, the Support Levels overview and the Development Tracker. Clients only see the
    /// level and the development timeline on their ticket page (TicketWork view component).
    /// </summary>
    public abstract partial class CsmsController
    {
        private bool IsStaffRole => RoleName is "Admin" or "SupportManager" or "SupportEngineer";

        private TierService Tiers => HttpContext.RequestServices.GetRequiredService<TierService>();

        private IActionResult BackToTicket(int issueId, string? error, string? success)
        {
            if (error != null) { TempData["ErrorMessage"] = error; }
            if (success != null) { TempData["SuccessMessage"] = success; }
            return RedirectToAction("TicketView", new { id = issueId });
        }

        /// <summary>Moves a ticket to another level and / or engineer, with the reason.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EscalateTicket(int issueId, int toLevel, int? engineerId, string? reason)
        {
            if (!IsStaffRole)
            {
                return NotFound();
            }

            try
            {
                var issue = await Db.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId);
                if (issue == null || !CanAccessIssue(issue))
                {
                    return NotFound();
                }

                var from = SupportLevel.Clamp(issue.SupportLevel);
                var manager = RoleName is "Admin" or "SupportManager";
                string? error = null;
                var canHandBack = manager;
                if (RoleName == "SupportEngineer")
                {
                    // what the engineer's support level may do (Admin > Support Levels > Authority)
                    var assignment = HttpContext.RequestServices.GetRequiredService<AssignmentService>();
                    error = await assignment.ValidateEngineerMoveAsync(issue, toLevel, engineerId, CurrentUser!);
                    canHandBack = toLevel < from && error == null;
                }

                error ??= await Tiers.MoveAsync(issue, toLevel, engineerId, reason, CurrentUser!, canHandBack);
                if (error != null)
                {
                    return BackToTicket(issueId, error, null);
                }

                await Db.SaveChangesAsync();
                if (issue.AssignedToUserId == null)
                {
                    AutoAssignWorker.Wake(); // waiting in the new level's queue: an idle engineer there may get it
                }

                var who = string.IsNullOrWhiteSpace(issue.AssignBy) ? "waiting in the " + SupportLevel.Code(toLevel) + " queue" : "assigned to " + issue.AssignBy;
                var what = toLevel > from ? "Escalated to " : toLevel < from ? "Handed back to " : "Reassigned at ";
                return BackToTicket(issueId, null, what + SupportLevel.Code(toLevel) + ", " + who + ".");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Sets the development stage, owner, ETA and reference, with an optional note.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDevelopment(int issueId, string? stage, int? ownerId, DateTime? eta, string? reference, string? note, bool internalNote)
        {
            if (!IsStaffRole)
            {
                return NotFound();
            }

            try
            {
                var issue = await Db.Issues.FirstOrDefaultAsync(i => i.IssueId == issueId);
                if (issue == null || !CanAccessIssue(issue))
                {
                    return NotFound();
                }

                var error = await Tiers.UpdateDevelopmentAsync(issue, stage, ownerId, eta, reference, note, internalNote, CurrentUser!);
                if (error != null)
                {
                    return BackToTicket(issueId, error, null);
                }

                await Db.SaveChangesAsync();
                return BackToTicket(issueId, null, issue.DevStage == null ? "Development tracking ended for this ticket." : "Development status saved: " + DevStages.Label(issue.DevStage) + ".");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Support Levels overview: workload per level, escalation flow, engineers, development pipeline.</summary>
        [HttpGet]
        public async Task<IActionResult> SupportTiers(int days = 30)
        {
            if (!IsStaffRole)
            {
                return NotFound();
            }

            try
            {
                days = new[] { 7, 30, 90, 365 }.Contains(days) ? days : 30;
                var view = await Tiers.OverviewAsync(days);
                view.Layout = RoleLayout;
                view.Controller = RoleController;
                view.Role = RoleName;
                return View("~/Views/Shared/SupportTiers.cshtml", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Development Tracker: every ticket with development work, as a board by stage.</summary>
        [HttpGet]
        public async Task<IActionResult> DevTracker(bool deployed = false, int? ownerId = null)
        {
            if (!IsStaffRole)
            {
                return NotFound();
            }

            try
            {
                var view = new DevTrackerView
                {
                    Layout = RoleLayout,
                    Controller = RoleController,
                    Role = RoleName,
                    ShowDeployed = deployed,
                    OwnerId = ownerId,
                    Owners = await Tiers.DevOwnersAsync(),
                    Rows = await Tiers.DevTicketsAsync(deployed, ownerId)
                };
                return View("~/Views/Shared/DevTracker.cshtml", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
