using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Admin;
using XFLCSMS.Models.Issue;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    /// <summary>
    /// Read-only executive accounts (department "Viewer"): the CEO / MD of a brokerage house sees every ticket of
    /// that house; the XFL MD (an XFL user, UType) sees every house. They can open tickets, lists and reports but
    /// cannot raise, edit, rate, escalate or delete anything.
    /// </summary>
    public class ViewerController : CsmsController
    {
        protected override string SessionKey => SessionAuthorizeAttribute.Viewer;

        // the only posts a viewer may make: their own password and notification settings
        private static readonly string[] AllowedPosts = { nameof(ChangePassword), "NotificationRead", "NotificationPreferences" };

        public ViewerController(DataContext context, TicketService tickets)
            : base(context, tickets)
        {
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var action = (string?)context.RouteData.Values["action"];
            if (!HttpMethods.IsGet(Request.Method) && !HttpMethods.IsHead(Request.Method)
                && !AllowedPosts.Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                context.Result = Forbid();
                return;
            }

            base.OnActionExecuting(context);
        }

        protected override bool CanAccessIssue(IssueTable issue)
        {
            var me = CurrentUser;
            return me != null && (me.UType || issue.BrokerageId == me.BrokerageHouseName);
        }

        public IActionResult Index()
        {
            return RedirectToAction(nameof(Dashbord));
        }

        public Task<IActionResult> Dashbord(TicketFilter filter)
        {
            return Explorer("Dashboard", "report", null, filter);
        }

        public Task<IActionResult> AllTicketList(TicketFilter filter)
        {
            return Explorer("All Tickets", "list", null, filter);
        }

        public Task<IActionResult> OpenTicketList(TicketFilter filter)
        {
            return Explorer("Open Tickets", "list", "open", filter);
        }

        public Task<IActionResult> ClosedTicketList(TicketFilter filter)
        {
            return Explorer("Closed Tickets", "list", "closed", filter);
        }

        public Task<IActionResult> Reports(TicketFilter filter)
        {
            return Explorer("Ticket Reports", "report", null, filter);
        }

        private async Task<IActionResult> Explorer(string title, string mode, string? preset, TicketFilter filter)
        {
            var house = CurrentUser!.UType ? "all brokerage houses" : await Db.Brokerages.Where(b => b.BrokerageId == CurrentUser.BrokerageHouseName)
                .Select(b => b.BrokerageHouseName).FirstOrDefaultAsync() ?? "your house";
            var result = await TicketExplorer(title, "Read-only view of " + house, mode, preset, filter);
            if (result is ViewResult { Model: TicketExplorerView view })
            {
                view.CreateAction = null; // viewers do not raise tickets
            }

            return result;
        }

        public async Task<IActionResult> TicketView(int? id)
        {
            try
            {
                var issue = await Db.Issues.Include(i => i.attachment).FirstOrDefaultAsync(i => i.IssueId == id);
                if (issue == null || !CanAccessIssue(issue))
                {
                    return NotFound();
                }

                var view = new MakerView
                {
                    IssueId = issue.IssueId,
                    CreatedOn = issue.TDate,
                    CreatedBy = await Db.Users.Where(u => u.Id == issue.UserId).Select(u => u.FullName).FirstOrDefaultAsync() ?? string.Empty,
                    AssgnOn = issue.AssignOn,
                    AssgnBy = issue.AssignBy,
                    Product = Tickets.ProductName(issue.ProductId),
                    ProductId = issue.ProductId,
                    Products = Tickets.AllProducts(),
                    SupportType = await Db.SupportTypes.Where(x => x.SupportTypeId == issue.SupportTypeId).Select(x => x.SType).FirstOrDefaultAsync(),
                    SupportCatagory = await Db.SupportCatagories.Where(x => x.SupportCatagoryId == issue.SupportCatagoryId).Select(x => x.SCatagory).FirstOrDefaultAsync(),
                    SupportSubCatagory = await Db.SupportSubCatagories.Where(x => x.SupportSubCatagoryId == issue.SupportSubCatagoryId).Select(x => x.SubCatagory).FirstOrDefaultAsync(),
                    AffectedSection = await Db.AffectedSectionss.Where(x => x.AffectedSectionId == issue.AffectedSectionId).Select(x => x.ASection).FirstOrDefaultAsync(),
                    TicketDetails = issue.Details,
                    Command = issue.Comments,
                    TicketStatus = issue.IStatus,
                    IStatus = issue.IStatus,
                    Priority = issue.Priority,
                    Attachments = issue.attachment,
                    ApproveOn = issue.ApproveOn,
                    ApproveBy = issue.ApproveBy,
                    CloseOn = issue.ClosedOn,
                    ClosedbyName = issue.ClosedBy,
                    IssueTitle = issue.ITitle
                };

                return View(view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
