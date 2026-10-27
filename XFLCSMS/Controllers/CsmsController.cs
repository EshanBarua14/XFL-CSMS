using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Admin;
using XFLCSMS.Models.Email;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    /// <summary>
    /// Shared behaviour of the four role controllers (Admin, SupportManegar, SupportEngineer, Maker):
    /// the session check, profile, change password, attachments, logout and error handling.
    /// </summary>
    public abstract class CsmsController : Controller
    {
        protected readonly DataContext Db;
        protected readonly TicketService Tickets;
        private User? _currentUser;

        protected CsmsController(DataContext context, TicketService tickets)
        {
            Db = context;
            Tickets = tickets;
        }

        /// <summary>Session key of the role this controller serves, e.g. "AdminData".</summary>
        protected abstract string SessionKey { get; }

        /// <summary>The signed-in user of this role, or null when the session is missing or expired.</summary>
        protected User? CurrentUser
        {
            get
            {
                if (_currentUser == null)
                {
                    var json = HttpContext.Session.GetString(SessionKey);
                    if (!string.IsNullOrEmpty(json))
                    {
                        _currentUser = JsonConvert.DeserializeObject<User>(json);
                    }
                }

                return _currentUser;
            }
        }

        /// <summary>Runs before every action: no valid session means back to the login page.</summary>
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var user = CurrentUser;
            if (user == null)
            {
                context.Result = SessionAuthorizeAttribute.Challenge(Request);
                return;
            }

            // Every layout prints ViewBag.Profile.FullName, so it must be set for every view.
            ViewBag.Profile = user;
            base.OnActionExecuting(context);
        }

        /// <summary>Tickets this role may open. Makers are limited to their own tickets.</summary>
        protected virtual bool CanAccessIssue(IssueTable issue)
        {
            return true;
        }

        /// <summary>
        /// Used by the catch blocks. They used to sign the user out on ANY exception, which hid the real
        /// error and looked like a random logout. Now the error is logged and an error page is shown.
        /// </summary>
        protected IActionResult HandleError(Exception exception)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            logger.LogError(exception, "Unhandled error in {Path}", Request.Path);

            Response.StatusCode = StatusCodes.Status500InternalServerError;
            if (SessionAuthorizeAttribute.IsAjax(Request))
            {
                return Content("Something went wrong while processing the request.");
            }

            return View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Tells the client who raised the ticket that it was closed and asks for a rating. A mail failure is logged
        /// and never stops the edit.
        /// </summary>
        protected void NotifyTicketClosed(IssueTable issue)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            try
            {
                var owner = Db.Users.FirstOrDefault(u => u.Id == issue.UserId);
                if (owner == null || string.IsNullOrWhiteSpace(owner.Email) || owner.UType || owner.UCatagory)
                {
                    return; // tickets raised by XFL staff on behalf of a house have no client account to ask
                }

                var link = Url.Action("RateTicket", "Maker", new { id = issue.IssueId }, Request.Scheme);
                var title = System.Net.WebUtility.HtmlEncode(issue.ITitle ?? string.Empty);
                HttpContext.RequestServices.GetRequiredService<IEmailServices>().SendEmail(new EmailDto
                {
                    To = owner.Email,
                    Subject = "Ticket " + issue.TNumber + " is closed - how did we do?",
                    Body = "Dear " + System.Net.WebUtility.HtmlEncode(owner.FullName) + ",<br/><br/>" +
                           "Your ticket <b>" + issue.TNumber + "</b> (" + title + ") has been closed by the XFL support team.<br/><br/>" +
                           "Please take a minute to rate the support you received: <a href=\"" + link + "\">rate this ticket</a>. " +
                           "Your feedback is seen only by XFL support management.<br/><br/>" +
                           "If the problem is not solved, you can reopen the ticket from the same page within " + FeedbackService.RatingWindowDays + " days.<br/><br/>" +
                           "Thank you,<br/>Xpert Fintech Limited"
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send the rating e-mail for ticket {Ticket}.", issue.TNumber);
            }
        }

        /// <summary>
        /// Escalates a rating that needs follow-up (1-2 stars or "not solved") to every active Admin and Support Manager.
        /// A mail failure is logged and never stops the rating.
        /// </summary>
        protected void NotifyLowRating(IssueTable issue, XFLCSMS.Models.Feedback.TicketRating rating)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            try
            {
                var managers = Db.Users
                    .Where(u => u.UStatus && u.Email != null && u.Email != "" && (u.UCatagory || (u.UType && u.Department == "Support Maneger")))
                    .Select(u => new { u.Email, u.FullName, u.UCatagory })
                    .ToList();
                var mail = HttpContext.RequestServices.GetRequiredService<IEmailServices>();
                var title = System.Net.WebUtility.HtmlEncode(issue.ITitle ?? string.Empty);
                var comment = string.IsNullOrWhiteSpace(rating.Comment) ? "(no comment)" : System.Net.WebUtility.HtmlEncode(rating.Comment);
                foreach (var m in managers)
                {
                    var link = Url.Action("TicketView", m.UCatagory ? "Admin" : "SupportManegar", new { id = issue.IssueId }, Request.Scheme);
                    mail.SendEmail(new EmailDto
                    {
                        To = m.Email!,
                        Subject = "Low client rating on ticket " + issue.TNumber + " (" + rating.Stars + "/5)",
                        Body = "Dear " + System.Net.WebUtility.HtmlEncode(m.FullName) + ",<br/><br/>" +
                               "The client rated ticket <b>" + issue.TNumber + "</b> (" + title + ") <b>" + rating.Stars + " of 5</b>" +
                               (rating.FullySolved ? "" : " and says the problem is <b>not solved</b>") + ".<br/>" +
                               "Engineer: " + System.Net.WebUtility.HtmlEncode(rating.EngineerName ?? "-") + "<br/>" +
                               "Comment: " + comment + "<br/><br/>" +
                               "Please follow up within " + FeedbackService.FollowUpHours + " hours: <a href=\"" + link + "\">open the ticket</a>.<br/><br/>" +
                               "XFL CSMS"
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send the low rating e-mail for ticket {Ticket}.", issue.TNumber);
            }
        }

        /// <summary>Tells the client that XFL management answered his feedback.</summary>
        protected void NotifyClientReply(XFLCSMS.Models.Feedback.TicketRating rating)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
            try
            {
                var owner = Db.Users.FirstOrDefault(u => u.Id == rating.RatedByUserId);
                if (owner == null || string.IsNullOrWhiteSpace(owner.Email) || rating.Issue == null)
                {
                    return;
                }

                var link = Url.Action("TicketView", "Maker", new { id = rating.IssueId }, Request.Scheme);
                HttpContext.RequestServices.GetRequiredService<IEmailServices>().SendEmail(new EmailDto
                {
                    To = owner.Email,
                    Subject = "Reply to your feedback on ticket " + rating.Issue.TNumber,
                    Body = "Dear " + System.Net.WebUtility.HtmlEncode(owner.FullName) + ",<br/><br/>" +
                           "Thank you for your feedback on ticket <b>" + rating.Issue.TNumber + "</b>. XFL support management replied:<br/><br/>" +
                           "<i>" + System.Net.WebUtility.HtmlEncode(rating.ClientReply ?? string.Empty) + "</i><br/><br/>" +
                           "<a href=\"" + link + "\">Open the ticket</a><br/><br/>Xpert Fintech Limited"
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not send the feedback reply e-mail for rating {Rating}.", rating.RatingId);
            }
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "RegisterLogin");
        }

        /// <summary>"My Profile" always shows the signed-in user; the id in the URL is ignored on purpose.</summary>
        public async Task<IActionResult> Profile(int id)
        {
            try
            {
                var me = CurrentUser!;
                var user = await Db.Users.FirstOrDefaultAsync(item => item.Id == me.Id);
                if (user == null)
                {
                    return NotFound();
                }

                var house = await Db.Brokerages.FirstOrDefaultAsync(b => b.BrokerageId == user.BrokerageHouseName);
                var branch = await Db.Branchhs.FirstOrDefaultAsync(b => b.BranchId == user.Branch);

                var userView = new UserView
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhonNumber = user.PhonNumber,
                    Designation = user.Designation ?? string.Empty,
                    Department = user.Department ?? string.Empty,
                    BrokerageHouseName = house?.BrokerageHouseName ?? string.Empty,
                    Branch = branch?.BranchName ?? string.Empty,
                    EmployeeId = user.EmployeeId,
                    UserName = user.UserName,
                    UCatagory = user.UCatagory,
                    UType = user.UType,
                    UStatus = user.UStatus
                };

                return View(userView);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new Password { UserId = CurrentUser!.Id });
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(Password password)
        {
            try
            {
                // Always the signed-in user: the posted UserId is ignored so nobody can target another account.
                var me = CurrentUser!;
                password.UserId = me.Id;

                var user = await Db.Users.FirstOrDefaultAsync(i => i.Id == me.Id);
                if (user == null)
                {
                    return Logout();
                }

                if (!PasswordHasher.Verify(password.CurrentPassword, user.PasswordHash, user.PasswordSalt))
                {
                    ViewBag.message = "Current Password is not Correct";
                    return View(password);
                }

                if (string.IsNullOrEmpty(password.NewPassword) || ModelState[nameof(Password.NewPassword)]?.Errors.Count > 0)
                {
                    ViewBag.message = "Password must contain at least one lowercase letter, one uppercase letter, one digit, and one special character";
                    return View(password);
                }

                if (password.NewPassword != password.ConNewPassword)
                {
                    ViewBag.message = "New Password and Confirm Password are not the same";
                    return View(password);
                }

                PasswordHasher.Create(password.NewPassword, out byte[] passwordHash, out byte[] passwordSalt);
                user.PasswordHash = passwordHash;
                user.PasswordSalt = passwordSalt;
                await Db.SaveChangesAsync();

                return Logout();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> DownloadAttachment(int? att)
        {
            if (att == null)
            {
                return BadRequest("Attachment ID is missing in the query parameters.");
            }

            var attachment = await Db.Attachments.Include(a => a.issue).FirstOrDefaultAsync(i => i.AttachmentId == att);
            if (attachment == null || attachment.issue == null || !CanAccessIssue(attachment.issue))
            {
                return NotFound("Attachment not found.");
            }

            var filePath = Tickets.ResolveAttachmentPath(attachment);
            if (filePath == null)
            {
                return NotFound("File not found.");
            }

            return PhysicalFile(filePath, TicketService.GetContentType(attachment.FileName), attachment.FileName);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAttachment(int attachmentId)
        {
            var attachment = await Db.Attachments.Include(a => a.issue).FirstOrDefaultAsync(a => a.AttachmentId == attachmentId);
            if (attachment == null || attachment.issue == null || !CanAccessIssue(attachment.issue))
            {
                return NotFound();
            }

            await Tickets.DeleteAttachmentAsync(attachment);
            return Ok();
        }
    }
}
