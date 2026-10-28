using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    /// <summary>
    /// Ticket assignment for the three staff roles: the Ticket Queue page, taking a ticket (self-assign), giving it back,
    /// transfer requests between engineers and manager assignment. The rules are in AssignmentService.
    /// </summary>
    public abstract partial class CsmsController
    {
        private AssignmentService Assignment => HttpContext.RequestServices.GetRequiredService<AssignmentService>();

        private bool IsManagerRole => RoleName is "Admin" or "SupportManager";

        /// <summary>Back to where the form was: "#..." = the ticket page at that anchor, a local URL, or else the ticket / queue.</summary>
        private IActionResult BackTo(string? returnUrl, int? issueId, string? error, string? success)
        {
            if (error != null) { TempData["ErrorMessage"] = error; }
            if (success != null) { TempData["SuccessMessage"] = success; }
            if (issueId.HasValue && returnUrl != null && returnUrl.StartsWith("#"))
            {
                return Redirect(Url.Action("TicketView", new { id = issueId })! + returnUrl);
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return issueId.HasValue ? RedirectToAction("TicketView", new { id = issueId }) : RedirectToAction(nameof(TicketQueue));
        }

        /// <summary>Queued tickets (open, nobody assigned), engineers' workload and transfer requests.</summary>
        [HttpGet]
        public async Task<IActionResult> TicketQueue()
        {
            if (!IsStaffRole)
            {
                return NotFound();
            }

            try
            {
                var view = await Assignment.QueueAsync(CurrentUser!, RoleName);
                view.Layout = RoleLayout;
                view.Controller = RoleController;
                return View("~/Views/Shared/TicketQueue.cshtml", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeTicket(int issueId, string? returnUrl)
        {
            if (RoleName != "SupportEngineer")
            {
                return NotFound();
            }

            try
            {
                var error = await Assignment.TakeAsync(issueId, CurrentUser!);
                return BackTo(returnUrl, issueId, error, error == null ? "The ticket is assigned to you." : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Takes the ticket auto-assign would give the engineer next.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TakeNextTicket()
        {
            if (RoleName != "SupportEngineer")
            {
                return NotFound();
            }

            try
            {
                var (issueId, error) = await Assignment.TakeNextAsync(CurrentUser!);
                return error != null ? BackTo(null, null, error, null) : BackTo(null, issueId, null, "The next ticket in the queue is assigned to you.");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReleaseTicket(int issueId, string? reason, string? returnUrl)
        {
            if (RoleName != "SupportEngineer")
            {
                return NotFound();
            }

            try
            {
                var error = await Assignment.ReleaseAsync(issueId, CurrentUser!, reason);
                if (error == null)
                {
                    AutoAssignWorker.Wake();
                }

                return BackTo(returnUrl, issueId, error, error == null ? "The ticket is back in the queue." : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Managers and administrators (and engineers whose level may assign others) assign from the queue or the ticket.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTicket(int issueId, int? engineerId, string? returnUrl)
        {
            if (!IsStaffRole)
            {
                return NotFound();
            }

            try
            {
                if (!IsManagerRole)
                {
                    var me = await Assignment.EngineerAsync(CurrentUser!.Id);
                    if (me?.Authority.CanAssignOthers != true)
                    {
                        return BackTo(returnUrl, issueId, "Your support level cannot assign tickets to other engineers. Request a transfer instead.", null);
                    }
                }

                if (!engineerId.HasValue)
                {
                    return BackTo(returnUrl, issueId, "Choose an engineer.", null);
                }

                var error = await Assignment.AssignAsync(issueId, engineerId.Value, CurrentUser!);
                return BackTo(returnUrl, issueId, error, error == null ? "Ticket assigned." : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTransfer(int issueId, int? toUserId, string? note, string? returnUrl)
        {
            if (RoleName != "SupportEngineer")
            {
                return NotFound();
            }

            try
            {
                var error = toUserId.HasValue ? await Assignment.RequestTransferAsync(issueId, CurrentUser!, toUserId.Value, note) : "Choose an engineer.";
                return BackTo(returnUrl, issueId, error, error == null ? "Transfer requested. The ticket stays with you until it is accepted." : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AnswerTransfer(int requestId, bool accept, string? note, string? returnUrl)
        {
            if (RoleName != "SupportEngineer")
            {
                return NotFound();
            }

            try
            {
                var issueId = await Db.TicketTransferRequests.Where(t => t.Id == requestId).Select(t => (int?)t.IssueId).FirstOrDefaultAsync();
                var error = await Assignment.AnswerTransferAsync(requestId, CurrentUser!, accept, note);
                var settings = await Assignment.SettingsAsync();
                var done = !accept ? "Transfer declined." : settings.TransferNeedsManagerApproval ? "Accepted. A support manager approves the transfer." : "Transfer accepted: the ticket is yours.";
                return BackTo(returnUrl, issueId, error, error == null ? done : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTransfer(int requestId, string? returnUrl)
        {
            if (RoleName != "SupportEngineer")
            {
                return NotFound();
            }

            try
            {
                var issueId = await Db.TicketTransferRequests.Where(t => t.Id == requestId).Select(t => (int?)t.IssueId).FirstOrDefaultAsync();
                var error = await Assignment.CancelTransferAsync(requestId, CurrentUser!);
                return BackTo(returnUrl, issueId, error, error == null ? "Transfer request withdrawn." : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DecideTransfer(int requestId, bool approve, string? note, string? returnUrl)
        {
            if (!IsManagerRole)
            {
                return NotFound();
            }

            try
            {
                var issueId = await Db.TicketTransferRequests.Where(t => t.Id == requestId).Select(t => (int?)t.IssueId).FirstOrDefaultAsync();
                var error = await Assignment.DecideTransferAsync(requestId, CurrentUser!, approve, note);
                return BackTo(returnUrl, issueId, error, error == null ? (approve ? "Transfer approved." : "Transfer rejected.") : null);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Managers: run auto-assign now instead of waiting for the next run.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunAutoAssign()
        {
            if (!IsManagerRole)
            {
                return NotFound();
            }

            try
            {
                var settings = await Assignment.SettingsAsync();
                if (!settings.AutoAssignEnabled)
                {
                    return BackTo(null, null, "Auto-assign is switched off (Admin > Setup > Ticket Assignment).", null);
                }

                var count = await Assignment.AutoAssignAsync(ignoreOnline: false, HttpContext.RequestAborted);
                return BackTo(null, null, null, count == 0
                    ? "Nothing to assign: no idle engineer" + (settings.AutoAssignOnlyOnline ? " online" : "") + " can take a queued ticket."
                    : count + " ticket" + (count == 1 ? "" : "s") + " auto-assigned.");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
