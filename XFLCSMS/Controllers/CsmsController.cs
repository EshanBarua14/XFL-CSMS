using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
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
    public abstract partial class CsmsController : Controller
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
            if (user == null || !AccountStillAllowed(user))
            {
                if (user != null)
                {
                    HttpContext.Session.Clear();
                }

                context.Result = SessionAuthorizeAttribute.Challenge(Request);
                return;
            }

            // Every layout prints ViewBag.Profile.FullName, so it must be set for every view.
            ViewBag.Profile = user;
            base.OnActionExecuting(context);
        }

        /// <summary>
        /// The session holds a copy of the user taken at sign-in. An administrator who disables an account or changes
        /// its role must not have to wait for that session to expire: the account is re-read (cached for 30 seconds)
        /// and a disabled, deleted or re-roled account is signed out.
        /// </summary>
        private bool AccountStillAllowed(User user)
        {
            var cache = HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            var key = cache.GetOrCreate("account-check:" + user.Id, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
                var account = Db.Users.AsNoTracking().Where(u => u.Id == user.Id)
                    .Select(u => new { u.UStatus, u.UCatagory, u.UType, u.Department })
                    .FirstOrDefault();
                return account == null || !account.UStatus
                    ? string.Empty
                    : SessionAuthorizeAttribute.KeyFor(account.UCatagory, account.UType, account.Department);
            });

            return key == SessionKey;
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

        /// <summary>Admin, SupportManager, SupportEngineer or Maker (from the session key of this controller).</summary>
        protected string RoleName => SessionKey switch
        {
            SessionAuthorizeAttribute.Admin => "Admin",
            SessionAuthorizeAttribute.SupportManager => "SupportManager",
            SessionAuthorizeAttribute.SupportEngineer => "SupportEngineer",
            _ => "Maker"
        };

        /// <summary>
        /// The ticket lists and the ticket report of every role: one page (Views/Shared/TicketExplorer) with filters,
        /// a detailed table (column choice, sorting, per-column search, row details, Excel / PDF / CSV / print)
        /// and, for the report, KPIs, charts and breakdowns.
        /// </summary>
        protected async Task<IActionResult> TicketExplorer(string title, string? subtitle, string mode, string? preset, TicketFilter? filter)
        {
            try
            {
                var reports = HttpContext.RequestServices.GetRequiredService<TicketReportService>();
                filter ??= new TicketFilter();

                // the report opens on the last 30 days; the lists show everything until filtered
                if (mode == "report" && filter.IsEmpty && !Request.Query.ContainsKey("filtered"))
                {
                    filter.Period = "30d";
                }

                var action = (string)RouteData.Values["action"]!;
                var view = await reports.BuildAsync(CurrentUser!, RoleName, (string)RouteData.Values["controller"]!, action, mode, preset, filter);
                view.Title = title;
                view.Subtitle = subtitle;
                view.CreateAction = "IssueRaiseFrom";
                return View("TicketExplorer", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
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
                    ViewBag.message = PasswordRules.Message;
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
