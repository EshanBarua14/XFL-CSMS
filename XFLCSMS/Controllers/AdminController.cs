using MailKit.Search;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.Security.Cryptography;
using XFLCSMS.Models.Admin;
using XFLCSMS.Models.Affected;
using XFLCSMS.Models.Branch;
using XFLCSMS.Models.Brocarage;
using XFLCSMS.Models.Common;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Support;
using XFLCSMS.Models.Todos;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    public partial class AdminController : CsmsController
    {
        private readonly DataContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        protected override string SessionKey => "AdminData";

        public AdminController(DataContext context, IWebHostEnvironment webHostEnvironment, TicketService tickets)
            : base(context, tickets)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }
        public IActionResult Index()
        {
            return RedirectToAction("Dashbord");
        }

        public async Task<IActionResult> Dashbord()
         {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                // Total Ticket
                int TotalInTicket = _context.Issues.Count();
                int TotalInclosed = _context.Issues.Count(i => i.IStatus == "Close");
                int TotalInQueue = TotalInTicket - TotalInclosed;
                //Today
                int TodayTotal = _context.Issues.Count(i => i.TDate.Date == DateTime.Now.Date);
                int TodayClose = _context.Issues.Count(i => (i.TDate.Date == DateTime.Now.Date) && (i.IStatus == "Close"));
                int TodayQueue = TodayTotal - TodayClose;
                //Week
                DateTime lastWeekStartDate = DateTime.Now.Date.AddDays(-7);
                int LastWeekTotal = _context.Issues.Count(i => i.TDate.Date >= lastWeekStartDate && i.TDate.Date <= DateTime.Now.Date);
                int LastWeekClosed = _context.Issues.Count(i => i.TDate.Date >= lastWeekStartDate && i.TDate.Date <= DateTime.Now.Date && i.IStatus == "Close");
                int lastWeekQueue = LastWeekTotal - LastWeekClosed;

                // Last month's total issues
                DateTime lastMonthStartDate = DateTime.Now.Date.AddMonths(-1);
                int LastMonthTotal = _context.Issues.Count(i => i.TDate.Date >= lastMonthStartDate && i.TDate.Date <= DateTime.Now.Date);
                int LastMonthClosed = _context.Issues.Count(i => i.TDate.Date >= lastMonthStartDate && i.TDate.Date <= DateTime.Now.Date && i.IStatus == "Close");
                int LastMonthQueue = LastMonthTotal - LastMonthClosed;

                // Last year's total issues
                DateTime lastYearStartDate = DateTime.Now.Date.AddYears(-1);
                int LastYearTotal = _context.Issues.Count(i => i.TDate.Date >= lastYearStartDate && i.TDate.Date <= DateTime.Now.Date);
                int LastYearClosed = _context.Issues.Count(i => i.TDate.Date >= lastYearStartDate && i.TDate.Date <= DateTime.Now.Date && i.IStatus == "Close");
                int LastYearQueue = LastYearTotal - LastYearClosed;

                ///for Brocarage Chart
                ///
                var brokerages = _context.Brokerages.Include(b => b.Issues).ToList();

                var chartData = new
                {
                    labels = brokerages.Select(b => b.BrokerageHouseName),
                    datasets = new[]
                    {
                new
                {
                    label = "Total Tickets",
                    data = brokerages.Select(b => b.Issues.Count),
                    backgroundColor = "rgba(75, 192, 192, 0.2)",
                    borderColor = "rgba(75, 192, 192, 1)",
                    borderWidth = 1
                },
                new
                {
                    label = "Close Tickets",
                    data = brokerages.Select(b => b.Issues.Count(I=>I.IStatus=="Close")),
                    backgroundColor = "rgba(0, 255, 0, 0.2)",
                    borderColor = "rgba(0, 255, 0, 1)",
                    borderWidth = 1
                },
                new
                {
                    label = "Inqueue Tickets",
                    data = brokerages.Select(b => b.Issues.Count(I=>I.IStatus!="Close")),
                    backgroundColor = "rgba(75, 192, 192, 0.2)",
                    borderColor = "rgba(255, 206, 86, 1)",
                    borderWidth = 1
                },
                // Repeat the structure for "In Queue" and "Closed Tickets" using relevant data
            }
                };




                var TicketCount = new Dashboard
                {
                    TotalTicket = TotalInTicket,
                    TotalClosed = TotalInclosed,
                    TotalQueue = TotalInQueue,
                    TodayTotalTicket = TodayTotal,
                    TodayTotalClosed = TodayClose,
                    TodayTotalQueue = TodayQueue,
                    WeeklyTotalTicket = LastWeekTotal,
                    WeeklyTotalClosed = LastWeekClosed,
                    WeeklyTotalQueue = lastWeekQueue,
                    MonthlyTotalTicket = LastMonthTotal,
                    MonthlyTotalClosed = LastMonthClosed,
                    MonthlyTotalQueue = LastMonthQueue,
                    YearlyTotalTicket = LastYearTotal,
                    YearlyTotalClosed = LastYearClosed,
                    YearlyTotalQueue = LastYearQueue,
                    ChartData = chartData


                };







                return View(TicketCount);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public Task<IActionResult> AdminView(TicketFilter filter)
        {
            return TicketExplorer("All Tickets", "Every ticket of every brokerage house.", "list", null, filter);
        }

        public async Task<IActionResult> TicketView(int? id)
        {
            var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
            User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
            ViewBag.Profile = LogSesson;
            try
            {
                var issueWithAttachments = _context.Issues
                    .Include(i => i.attachment)  // Include attachments in the query
                    .FirstOrDefault(i => i.IssueId == id);

                if (issueWithAttachments == null || !CanAccessIssue(issueWithAttachments))
                {
                    return NotFound();
                }

                var makerView = new MakerView
                {
                    IssueId = issueWithAttachments.IssueId,
                    CreatedOn = issueWithAttachments.TDate,
                    CreatedBy = UserName(issueWithAttachments.UserId),
                    AssgnOn = issueWithAttachments.AssignOn,
                    AssgnBy = issueWithAttachments.AssignBy,
                    Product = Tickets.ProductName(issueWithAttachments.ProductId),
                    ProductId = issueWithAttachments.ProductId,
                    Products = Tickets.AllProducts(),
                    SupportType = SupportTypeName(issueWithAttachments.SupportTypeId),
                    SupportCatagory = SupportCatagoryName(issueWithAttachments.SupportCatagoryId),
                    SupportSubCatagory = SupportSubCatagoryName(issueWithAttachments.SupportSubCatagoryId),
                    AffectedSection = AffectedSectionName(issueWithAttachments.AffectedSectionId),
                    TicketDetails = issueWithAttachments.Details,
                    Command = issueWithAttachments.Comments,
                    TicketStatus = issueWithAttachments.IStatus,
                    IStatus = issueWithAttachments.IStatus,
                    Priority = issueWithAttachments.Priority,
                    Attachments = issueWithAttachments.attachment,
                    ApproveOn = issueWithAttachments.ApproveOn,
                    ApproveBy = issueWithAttachments.ApproveBy,
                    IssueTitle = issueWithAttachments.ITitle


                };

                return View(makerView);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> EditTicket(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var issueWithAttachments = _context.Issues
                    .Include(i => i.attachment)  // Include attachments in the query
                    .FirstOrDefault(i => i.IssueId == id);
                //var SupportEng = _context.Users.Where(i => i.Designation == "Support Engineer").ToList();
                var SupportEng = await Tickets.AssignableEngineersAsync();

                if (issueWithAttachments == null || !CanAccessIssue(issueWithAttachments))
                {
                    return NotFound();
                }


                var EditView = new MakerView
                {
                    IssueId = issueWithAttachments.IssueId,
                    CreatedOn = issueWithAttachments.TDate,
                    CreatedBy = UserName(issueWithAttachments.UserId),
                    AssgnOn = issueWithAttachments.AssignOn,
                    AssgnBy = issueWithAttachments.AssignBy,
                    Product = Tickets.ProductName(issueWithAttachments.ProductId),
                    ProductId = issueWithAttachments.ProductId,
                    Products = Tickets.AllProducts(),
                    SupportType = SupportTypeName(issueWithAttachments.SupportTypeId),
                    SupportCatagory = SupportCatagoryName(issueWithAttachments.SupportCatagoryId),
                    SupportSubCatagory = SupportSubCatagoryName(issueWithAttachments.SupportSubCatagoryId),
                    AffectedSection = AffectedSectionName(issueWithAttachments.AffectedSectionId),
                    TicketDetails = issueWithAttachments.Details,
                    Command = issueWithAttachments.Comments,
                    TicketStatus = issueWithAttachments.IStatus,
                    IStatus = issueWithAttachments.IStatus,
                    Attachments = issueWithAttachments.attachment,
                    IssueTitle = issueWithAttachments.ITitle,
                    Priority = issueWithAttachments.Priority,
                    SupportEngineers = SupportEng,
                    ApproveOn = issueWithAttachments.ApproveOn,
                    ApproveBy = issueWithAttachments.ApproveBy,

                };
                return View(EditView);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTicketttt(MakerView makerView, List<IFormFile> files)
        {
            try
            {
                var editor = CurrentUser!;
                var issue = await _context.Issues.FirstOrDefaultAsync(a => a.IssueId == makerView.IssueId);
                if (issue == null || !CanAccessIssue(issue))
                {
                    return NotFound("Edit is not done");
                }

                // closing the ticket notifies the client (rating link) through the notification system
                Tickets.ApplyStaffEdit(issue, makerView, editor, canApprove: editor.UType);
                await _context.SaveChangesAsync();

                var rejected = await Tickets.SaveAttachmentsAsync(issue.IssueId, files);
                if (rejected.Count > 0)
                {
                    TempData["ErrorMessage"] = "The ticket was saved, but these files were not attached (file type not allowed or larger than 10 MB): "
                        + string.Join(", ", rejected);
                }

                return RedirectToAction("AdminView", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public Task<IActionResult> ClosedTicketList(TicketFilter filter)
        {
            return TicketExplorer("Closed Tickets", "Resolved tickets with their response and resolution times.", "list", "closed", filter);
        }

        public Task<IActionResult> UnassignedTicketList(TicketFilter filter)
        {
            return TicketExplorer("Unassigned Tickets", "Open tickets nobody is working on yet.", "list", "unassigned", filter);
        }

        public IActionResult CreateBrocarage(int page, int rowperpage, string? searchString = null, string?
            sortField = null, bool sortAscending = true)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                
                return View();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBrocarage([Bind("BrokerageId,BrokerageHouseName,BrokerageHouseAcronym")] Brokerage brokerage)
        {
            try
            {
                brokerage.BrokerageId = 0; // identity column: the database assigns the id
                ValidateBrokerage(brokerage);
                if (!ModelState.IsValid)
                {
                    return View(brokerage);
                }

                await _context.AddAsync(brokerage);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("BrocarageHouseList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        // Ticket numbers are built from the acronym (ABC_0000001), so name and acronym must be unique.
        private void ValidateBrokerage(Brokerage brokerage)
        {
            brokerage.BrokerageHouseName = (brokerage.BrokerageHouseName ?? string.Empty).Trim();
            brokerage.BrokerageHouseAcronym = (brokerage.BrokerageHouseAcronym ?? string.Empty).Trim();

            if (_context.Brokerages.Any(b => b.BrokerageId != brokerage.BrokerageId && b.BrokerageHouseName == brokerage.BrokerageHouseName))
            {
                ModelState.AddModelError(nameof(Brokerage.BrokerageHouseName), "A brokerage house with this name already exists.");
            }

            if (_context.Brokerages.Any(b => b.BrokerageId != brokerage.BrokerageId && b.BrokerageHouseAcronym == brokerage.BrokerageHouseAcronym))
            {
                ModelState.AddModelError(nameof(Brokerage.BrokerageHouseAcronym), "This acronym is already used by another brokerage house.");
            }
        }


        [HttpDelete]
        public async Task<IActionResult> DeleteTicket(int id)
        {
            try
            {
                var issue = await _context.Issues.Include(i => i.attachment).FirstOrDefaultAsync(i => i.IssueId == id);
                if (issue == null)
                {
                    return NotFound("The ticket was not found.");
                }

                // remove the uploaded files too, not only the database rows
                foreach (var attachment in issue.attachment?.ToList() ?? new List<Attachment>())
                {
                    await Tickets.DeleteAttachmentAsync(attachment);
                }

                _context.Issues.Remove(issue);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        // The user list's Delete button used to call DeleteTicket with the USER id and deleted an unrelated ticket.
        [HttpDelete]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                if (id == CurrentUser!.Id)
                {
                    return Conflict("You cannot delete the account you are signed in with.");
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound("The user was not found.");
                }

                // Deleting the user would cascade and delete every ticket he raised.
                if (await _context.Issues.AnyAsync(i => i.UserId == id))
                {
                    return Conflict("This user has raised tickets, so the account cannot be deleted. Disable the user instead (Edit > User Status).");
                }

                // Assignment, ratings and scorecards keep the user's id: deleting would lose that history.
                if (await _context.Issues.AnyAsync(i => i.AssignedToUserId == id)
                    || await _context.TicketRatings.AnyAsync(r => r.EngineerUserId == id || r.RatedByUserId == id))
                {
                    return Conflict("This user has assigned tickets or ratings, so the account cannot be deleted. Disable the user instead (Edit > User Status).");
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> UserList()
        {
            try
            {

                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var users = _context.Users.ToList();
                return View(users);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        public async Task<IActionResult> UserView(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == id);

                if (user == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                var userView = new UserView
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Department = user.Department ?? string.Empty,
                    Email = user.Email,
                    PhonNumber = user.PhonNumber,
                    Designation = user.Designation,
                    BrokerageHouseName = GetBrocarageHouseName(user.BrokerageHouseName),
                    Branch = GetBranchName(user.Branch),
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

        public async Task<IActionResult> EditUser(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;

                var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == id);

                if (user == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                var userView = new UserView
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Department = user.Department ?? string.Empty,
                    Email = user.Email,
                    PhonNumber = user.PhonNumber,
                    Designation = user.Designation,
                    BrokerageHouseName = GetBrocarageHouseName(user.BrokerageHouseName),
                    Branch = GetBranchName(user.Branch),
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

        [HttpPost]
        public async Task<IActionResult> UpdateUser( UserView userView)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                var user = await _context.Users.FirstOrDefaultAsync(a => a.Id == userView.Id);

                if (user != null)
                {
                    user.UStatus = userView.UStatus;
                    user.UCatagory = userView.UCatagory;
                    user.Department = userView.Department;
                    // Sign-in picks the role from UCatagory, UType and Department together. Keep UType in line with the
                    // chosen department (as Create User does): otherwise "Support Engineer" with "Outside User" signs in
                    // as a Maker but is still offered as an engineer to assign tickets to.
                    user.UType = userView.UCatagory || IsStaffDepartment(userView.Department);
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                    return RedirectToAction("UserList");
                }
                return RedirectToAction("UserList");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        private static bool IsStaffDepartment(string? department)
        {
            return department == "Support Maneger" || department == "Support Engineer";
        }

        public async Task<IActionResult> BrocarageHouseList()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var Brocareges = await _context.Brokerages.ToListAsync();
                return View(Brocareges);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }
        public async Task<IActionResult> ViewBrocarageHouse(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var brocarage = await _context.Brokerages.FirstOrDefaultAsync(item => item.BrokerageId == id);

                if (brocarage == null)
                {
                    return NotFound();
                }

                return View(brocarage);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> EditBrocarage(int id)
        {
            try
            {

                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var brocarage = await _context.Brokerages.FirstOrDefaultAsync(item => item.BrokerageId == id);

                if (brocarage == null)
                {
                    return NotFound();
                }

                return View(brocarage);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }
        [HttpPost]
        public async Task<IActionResult> UpdateBrocarage(Brokerage brokeragesss)
        {
            try
            {
                var brocarage = await _context.Brokerages.FirstOrDefaultAsync(item => item.BrokerageId == brokeragesss.BrokerageId);
                if (brocarage == null)
                {
                    return NotFound();
                }

                ValidateBrokerage(brokeragesss);
                if (!ModelState.IsValid)
                {
                    return View("EditBrocarage", brokeragesss);
                }

                brocarage.BrokerageHouseName = brokeragesss.BrokerageHouseName;
                brocarage.BrokerageHouseAcronym = brokeragesss.BrokerageHouseAcronym;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("BrocarageHouseList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteBrocarage(int id)
        {
            try
            {
                var house = await _context.Brokerages.FindAsync(id);
                if (house == null)
                {
                    return NotFound("The brokerage house was not found.");
                }

                // Deleting a house in use either fails on the foreign keys or silently wipes all of its tickets.
                if (await _context.Branchhs.AnyAsync(b => b.BrokerageId == id)
                    || await _context.Users.AnyAsync(u => u.BrokerageHouseName == id)
                    || await _context.Issues.AnyAsync(i => i.BrokerageId == id))
                {
                    return Conflict("This brokerage house still has branches, users or tickets, so it cannot be deleted.");
                }

                _context.Brokerages.Remove(house);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        public async Task<IActionResult> BranchList()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var Branch = await _context.Branchhs.ToListAsync();
                List<BranchView> branches = new List<BranchView>();
                foreach (var branch in Branch)
                {
                    BranchView b = new BranchView();
                    b.BranchId = branch.BranchId;
                    b.BranchName = branch.BranchName;
                    b.BrokerageHouseName = GetBrocarageHouseName(branch.BrokerageId);
                    branches.Add(b);
                }

                return View(branches);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public IActionResult CreateBranch()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var Brocarage = _context.Brokerages.ToList();

                BranchView BB = new BranchView();
                BB.brocarage = Brocarage;

                return View(BB);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBranch(BranchView branchView)
        {
            try
            {
                if (!await _context.Brokerages.AnyAsync(b => b.BrokerageId == branchView.BrokerageId))
                {
                    ModelState.AddModelError(nameof(BranchView.BrokerageId), "Please select a brokerage house.");
                }

                if (!ModelState.IsValid)
                {
                    branchView.brocarage = await _context.Brokerages.ToListAsync(); // the drop-down needs its options again
                    return View(branchView);
                }

                Branchh branchh = new Branchh();
                branchh.BranchName = branchView.BranchName.Trim();
                branchh.BrokerageId = branchView.BrokerageId;
                await _context.Branchhs.AddAsync(branchh);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("BranchList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> ViewBranch(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var branch = await _context.Branchhs.FirstOrDefaultAsync(item => item.BranchId == id);

                if (branch == null)
                {
                    return NotFound();
                }

                BranchView b = new BranchView();

                b.BranchId = branch.BranchId;
                b.BranchName = branch.BranchName;
                b.BrokerageHouseName = GetBrocarageHouseName(branch.BrokerageId);

                return View(b);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        public async Task<IActionResult> EditBranch(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var branch = await _context.Branchhs.FirstOrDefaultAsync(item => item.BranchId == id);
                var Brocarage = await _context.Brokerages.ToListAsync();

                if (branch == null)
                {
                    return NotFound();
                }

                BranchView b = new BranchView();

                b.BranchId = branch.BranchId;
                b.BranchName = branch.BranchName;
                b.BrokerageHouseName = GetBrocarageHouseName(branch.BrokerageId);
                b.BrokerageId = branch.BrokerageId ?? 0;
                b.brocarage = Brocarage;

                return View(b);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }


        }
        [HttpPost]
        public async Task<IActionResult> UpdateBranch(BranchView branchView)
        {
            try
            {
                var branch = await _context.Branchhs.FirstOrDefaultAsync(item => item.BranchId == branchView.BranchId);
                if (branch == null)
                {
                    return NotFound();
                }

                if (!await _context.Brokerages.AnyAsync(b => b.BrokerageId == branchView.BrokerageId))
                {
                    ModelState.AddModelError(nameof(BranchView.BrokerageId), "Please select a brokerage house.");
                }

                if (!ModelState.IsValid)
                {
                    branchView.BrokerageHouseName = GetBrocarageHouseName(branch.BrokerageId) ?? string.Empty;
                    branchView.brocarage = await _context.Brokerages.ToListAsync();
                    return View("EditBranch", branchView);
                }

                branch.BranchName = branchView.BranchName.Trim();
                branch.BrokerageId = branchView.BrokerageId;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("BranchList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteBranch(int id)
        {
            try
            {
                var branch = await _context.Branchhs.FirstOrDefaultAsync(i => i.BranchId == id);
                if (branch == null)
                {
                    return NotFound("The branch was not found.");
                }

                if (await _context.Users.AnyAsync(u => u.Branch == id))
                {
                    return Conflict("Users are registered under this branch, so it cannot be deleted.");
                }

                _context.Branchhs.Remove(branch);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        // ---- Products (Setup > Products): what a ticket is about. OST, Smart Trade, ... ----

        public async Task<IActionResult> ProductList()
        {
            try
            {
                var products = await _context.Products.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.ProductName).ToListAsync();
                ViewBag.TicketCounts = await _context.Issues.Where(i => i.ProductId != null)
                    .GroupBy(i => i.ProductId!.Value).Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(g => g.Key, g => g.Count);
                return View(products);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> ViewProduct(int id)
        {
            try
            {
                var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
                if (product == null)
                {
                    return NotFound();
                }

                ViewBag.TicketCount = await _context.Issues.CountAsync(i => i.ProductId == id);
                return View(product);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> CreateProduct()
        {
            var next = await _context.Products.Select(p => (int?)p.SortOrder).MaxAsync() ?? 0;
            return View(new XFLCSMS.Models.Product.Product { IsActive = true, SortOrder = next + 10 });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct([Bind("ProductName,Description,IsActive,SortOrder")] XFLCSMS.Models.Product.Product product)
        {
            try
            {
                product.ProductId = 0; // identity column: the database assigns the id
                product.ProductName = (product.ProductName ?? string.Empty).Trim();
                if (await _context.Products.AnyAsync(p => p.ProductName == product.ProductName))
                {
                    ModelState.AddModelError(nameof(product.ProductName), "A product with this name already exists.");
                }

                if (!ModelState.IsValid)
                {
                    return View(product);
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Product \"" + product.ProductName + "\" was added.";
                return RedirectToAction("ProductList");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> EditProduct(int id)
        {
            try
            {
                var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);
                return product == null ? NotFound() : View(product);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct([Bind("ProductId,ProductName,Description,IsActive,SortOrder")] XFLCSMS.Models.Product.Product product)
        {
            try
            {
                var existing = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == product.ProductId);
                if (existing == null)
                {
                    return NotFound();
                }

                product.ProductName = (product.ProductName ?? string.Empty).Trim();
                if (await _context.Products.AnyAsync(p => p.ProductName == product.ProductName && p.ProductId != product.ProductId))
                {
                    ModelState.AddModelError(nameof(product.ProductName), "A product with this name already exists.");
                }

                if (!ModelState.IsValid)
                {
                    return View(product);
                }

                existing.ProductName = product.ProductName;
                existing.Description = product.Description;
                existing.IsActive = product.IsActive;
                existing.SortOrder = product.SortOrder;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Product \"" + existing.ProductName + "\" was saved.";
                return RedirectToAction("ProductList");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                var item = await _context.Products.FindAsync(id);
                if (item == null)
                {
                    return NotFound("The product was not found.");
                }

                if (await _context.Issues.AnyAsync(i => i.ProductId == id))
                {
                    return Conflict("This product has tickets, so it cannot be deleted. Edit it and untick \"Active\" to stop offering it for new tickets.");
                }

                _context.Products.Remove(item);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> SupportTypeList()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportType = await _context.SupportTypes.ToListAsync();
                return View(supportType);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public async Task<IActionResult> ViewSupportType(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var brocarage = await _context.SupportTypes.FirstOrDefaultAsync(item => item.SupportTypeId == id);

                if (brocarage == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(brocarage);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public IActionResult CreateSupportType()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                return View();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupportType([Bind("SupportTypeId,SType")] SupportType supportType)
        {
            try
            {
                supportType.SupportTypeId = 0; // identity column: the database assigns the id
                if (!ModelState.IsValid)
                {
                    return View(supportType);
                }

                await _context.AddAsync(supportType);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("SupportTypeList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        public async Task<IActionResult> EditSupportType(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var brocarage = await _context.SupportTypes.FirstOrDefaultAsync(item => item.SupportTypeId == id);

                if (brocarage == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(brocarage);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }
        [HttpPost]
        public async Task<IActionResult> UpdateSupportType(SupportType brokeragesss)
        {
            try
            {
                var existing = await _context.SupportTypes.FirstOrDefaultAsync(item => item.SupportTypeId == brokeragesss.SupportTypeId);
                if (existing == null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    return View("EditSupportType", brokeragesss);
                }

                existing.SType = brokeragesss.SType;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("SupportTypeList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSupportType(int id)
        {
            try
            {
                var item = await _context.SupportTypes.FindAsync(id);
                if (item == null)
                {
                    return NotFound("The support type was not found.");
                }

                // Tickets point at this row; deleting it would fail on the foreign key.
                if (await _context.Issues.AnyAsync(i => i.SupportTypeId == id))
                {
                    return Conflict("This support type is used by existing tickets, so it cannot be deleted.");
                }

                _context.SupportTypes.Remove(item);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        public async Task<IActionResult> SupportCatagoryList()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportCatagory = await _context.SupportCatagories.ToListAsync();
                return View(supportCatagory);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public async Task<IActionResult> ViewSupportCatagory(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportCatagory = await _context.SupportCatagories.FirstOrDefaultAsync(item => item.SupportCatagoryId == id);

                if (supportCatagory == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(supportCatagory);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public IActionResult CreateSupportCatagory()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                return View();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupportCatagory([Bind("SupportCatagoryId,SCatagory")] SupportCatagory supportCatagory)
        {
            try
            {
                supportCatagory.SupportCatagoryId = 0; // identity column: the database assigns the id
                if (!ModelState.IsValid)
                {
                    return View(supportCatagory);
                }

                await _context.AddAsync(supportCatagory);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("SupportCatagoryList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> EditSupportCatagory(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportCatagory = await _context.SupportCatagories.FirstOrDefaultAsync(item => item.SupportCatagoryId == id);

                if (supportCatagory == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(supportCatagory);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }
        [HttpPost]
        public async Task<IActionResult> UpdateSupportCatagory(SupportCatagory supportCatagory)
        {
            try
            {
                var existing = await _context.SupportCatagories.FirstOrDefaultAsync(item => item.SupportCatagoryId == supportCatagory.SupportCatagoryId);
                if (existing == null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    return View("EditSupportCatagory", supportCatagory);
                }

                existing.SCatagory = supportCatagory.SCatagory;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("SupportCatagoryList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSupportCatagory(int id)
        {
            try
            {
                var item = await _context.SupportCatagories.FindAsync(id);
                if (item == null)
                {
                    return NotFound("The support category was not found.");
                }

                // Tickets point at this row; deleting it would fail on the foreign key.
                if (await _context.Issues.AnyAsync(i => i.SupportCatagoryId == id))
                {
                    return Conflict("This support category is used by existing tickets, so it cannot be deleted.");
                }

                _context.SupportCatagories.Remove(item);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> SupportSubCatagoryList()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportSubCatagory = await _context.SupportSubCatagories.ToListAsync();
                return View(supportSubCatagory);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public async Task<IActionResult> ViewSupportSubCatagory(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportSubCatagory = await _context.SupportSubCatagories.FirstOrDefaultAsync(item => item.SupportSubCatagoryId == id);

                if (supportSubCatagory == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(supportSubCatagory);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public IActionResult CreateSupportSubCatagory()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                return View();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupportSubCatagory([Bind("SupportSubCatagoryId,SubCatagory")] SupportSubCatagory supportSubCatagory)
        {
            try
            {
                supportSubCatagory.SupportSubCatagoryId = 0; // identity column: the database assigns the id
                if (!ModelState.IsValid)
                {
                    return View(supportSubCatagory);
                }

                await _context.AddAsync(supportSubCatagory);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("SupportSubCatagoryList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> EditSupportSubCatagory(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var supportSubCatagory = await _context.SupportSubCatagories.FirstOrDefaultAsync(item => item.SupportSubCatagoryId == id);

                if (supportSubCatagory == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(supportSubCatagory);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }
        [HttpPost]
        public async Task<IActionResult> UpdateSupportSubCatagory(SupportSubCatagory supportSubCatagory)
        {
            try
            {
                var existing = await _context.SupportSubCatagories.FirstOrDefaultAsync(item => item.SupportSubCatagoryId == supportSubCatagory.SupportSubCatagoryId);
                if (existing == null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    return View("EditSupportSubCatagory", supportSubCatagory);
                }

                existing.SubCatagory = supportSubCatagory.SubCatagory;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("SupportSubCatagoryList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSupportSubCatagory(int id)
        {
            try
            {
                var item = await _context.SupportSubCatagories.FindAsync(id);
                if (item == null)
                {
                    return NotFound("The support sub-category was not found.");
                }

                // Tickets point at this row; deleting it would fail on the foreign key.
                if (await _context.Issues.AnyAsync(i => i.SupportSubCatagoryId == id))
                {
                    return Conflict("This support sub-category is used by existing tickets, so it cannot be deleted.");
                }

                _context.SupportSubCatagories.Remove(item);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }



        public async Task<IActionResult> AffectedSectionList()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var affectedSectios = await _context.AffectedSectionss.ToListAsync();
                return View(affectedSectios);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public async Task<IActionResult> ViewAffectedSection(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var affectedsection = await _context.AffectedSectionss.FirstOrDefaultAsync(item => item.AffectedSectionId == id);

                if (affectedsection == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(affectedsection);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
        public IActionResult CreateAffectedSection()
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                return View();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAffectedSection([Bind("AffectedSectionId,ASection")] AffectedSection affectedSection)
        {
            try
            {
                affectedSection.AffectedSectionId = 0; // identity column: the database assigns the id
                if (!ModelState.IsValid)
                {
                    return View(affectedSection);
                }

                await _context.AddAsync(affectedSection);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("AffectedSectionList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public async Task<IActionResult> EditAffectedSection(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var affectedSection = await _context.AffectedSectionss.FirstOrDefaultAsync(item => item.AffectedSectionId == id);

                if (affectedSection == null)
                {
                    return NotFound(); // Or handle the case where the user is not found
                }

                return View(affectedSection);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }
        [HttpPost]
        public async Task<IActionResult> UpdateAffectedSection(AffectedSection affectedSection)
        {
            try
            {
                var existing = await _context.AffectedSectionss.FirstOrDefaultAsync(item => item.AffectedSectionId == affectedSection.AffectedSectionId);
                if (existing == null)
                {
                    return NotFound();
                }

                if (!ModelState.IsValid)
                {
                    return View("EditAffectedSection", affectedSection);
                }

                existing.ASection = affectedSection.ASection;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "The changes were saved.";
                return RedirectToAction("AffectedSectionList", "Admin");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteAffectedSection(int id)
        {
            try
            {
                var item = await _context.AffectedSectionss.FindAsync(id);
                if (item == null)
                {
                    return NotFound("The affected section was not found.");
                }

                // Tickets point at this row; deleting it would fail on the foreign key.
                if (await _context.Issues.AnyAsync(i => i.AffectedSectionId == id))
                {
                    return Conflict("This affected section is used by existing tickets, so it cannot be deleted.");
                }

                _context.AffectedSectionss.Remove(item);
                await _context.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public Task<IActionResult> Reports(TicketFilter filter)
        {
            return TicketExplorer("Ticket Reports", "Filter by period, house, branch, product, status, priority, category, engineer and SLA; charts, breakdowns and exports.", "report", null, filter);
        }




        [HttpGet]
        public async Task<IActionResult> ViewTodo(int page, int rowperpage, string? searchString = null, string? sortField = null, bool sortAscending = true)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                if (page <= 0) { page = 1; }

                ViewBag.CurrentSortField = sortField;
                ViewBag.CurrentSortAscending = sortAscending;
                // Fetch all Todo items from the database
                //var todos = await _context.Todos.Where(item => item.UserId == LogSesson.Id && item.Status == "In progress").ToListAsync();
                var todos = await _context.Todos
                            .Where(item => item.UserId == LogSesson.Id && item.Status == "In progress")
                            .OrderByDescending(item => item.Id)
                            .ToListAsync();
                //List<Todo> todoss = todos;
                if (!string.IsNullOrEmpty(searchString))
                {
                    todos = todos.Where(todo =>
                                            todo.Todoname.ToLower().Contains(searchString.ToLower()) ||
                                            todo.Status?.ToLower().Contains(searchString.ToLower()) == true ||
                                            todo.CreatedOn.ToString().ToLower().Contains(searchString.ToLower())
                                     ).ToList();
                }

                switch (sortField)
                {
                    case "Task":
                        todos = sortAscending ? todos.OrderBy(item => item.Todoname).ToList() :
                            todos.OrderByDescending(item => item.Todoname).ToList();
                        break;
                    case "Status":
                        todos = sortAscending ? todos.OrderBy(item => item.Status).ToList() :
                            todos.OrderByDescending(item => item.Status).ToList();
                        break;
                    case "CreatedOn":
                        todos = sortAscending ? todos.OrderBy(item => item.CreatedOn).ToList() :
                            todos.OrderByDescending(item => item.CreatedOn).ToList();
                        break;
                    default:
                        // Default sorting if no valid sort field provided (sort by Id by default)
                        todos = todos.OrderByDescending(item => item.Id).ToList();
                        break;
                }


                int tot_records = todos.Count;
                int pagesize = rowperpage > 0 ? rowperpage : 10;
                int number_of_button = 4;

                Pager P = new Pager(tot_records, page, pagesize, number_of_button, searchString);
                ViewBag.pager = P;
                int skip_records = (Math.Max(P.CurrentPage, 1) - 1) * pagesize;
                int take_records = pagesize;
                List<Todo> todoss = todos.Skip(skip_records).Take(take_records).OrderByDescending(item => item.Id).ToList();



                TodoviewModel todoviewModel = new TodoviewModel()
                {
                    Todos = todoss,
                    Todo = null,

                };

                // Return the view with the list of Todo items
                return View(todoviewModel);

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }

        [HttpPost]
        public async Task<IActionResult> AddTodo(TodoviewModel newTodo)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                if (string.IsNullOrWhiteSpace(newTodo?.Todo?.Todoname))
                {
                    TempData["ErrorMessage"] = "Please enter a to-do before adding it.";
                    return RedirectToAction("ViewTodo");
                }

                Todo todo = new Todo()
                {
                    CreatedOn = DateTime.Now,
                    Todoname = newTodo.Todo.Todoname,
                    Status = "In progress",
                    UserId = LogSesson.Id,
                    BrokerageId = LogSesson.BrokerageHouseName,
                };

                await _context.Todos.AddAsync(todo);
                await _context.SaveChangesAsync();
                return RedirectToAction("ViewTodo");

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }

        [HttpGet]
        public async Task<IActionResult> GetTodo(int id)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                var todo = await _context.Todos.FindAsync(id);
                if (todo == null)
                {
                    return NotFound();
                }
                var statusOptions = new List<SelectListItem>
                {
                    new SelectListItem("In progress", "In progress"),
                    new SelectListItem("Done", "Done"),
                    new SelectListItem("Canceled", "Canceled")
                };

                ViewBag.StatusOptions = statusOptions;

                return View(todo);

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }


        }

        [HttpPost]
        public async Task<IActionResult> Updatetodo(Todo model)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);

                var existingTodo = await _context.Todos.FindAsync(model.Id);
                if (existingTodo == null)
                {
                    return NotFound();
                }

                if (!string.IsNullOrWhiteSpace(model.Todoname))
                {
                    existingTodo.Todoname = model.Todoname;
                }

                if (!string.IsNullOrWhiteSpace(model.Status))
                {
                    existingTodo.Status = model.Status;
                }

                _context.Todos.Update(existingTodo);
                await _context.SaveChangesAsync();

                return RedirectToAction("ViewTodo");

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }


        }

        [HttpGet]
        public async Task<IActionResult> AllTodo(int page, int rowperpage, string? searchString = null, string? sortField = null, bool sortAscending = true)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                if (page <= 0) { page = 1; }

                ViewBag.CurrentSortField = sortField;
                ViewBag.CurrentSortAscending = sortAscending;
                // Fetch all Todo items from the database
                var todos = await _context.Todos.Where(item => item.UserId == LogSesson.Id).ToListAsync();
                //List<Todo> todoss = todos;

                if (!string.IsNullOrEmpty(searchString))
                {
                    todos = todos.Where(todo =>
                                            todo.Todoname.ToLower().Contains(searchString.ToLower()) ||
                                            todo.Status?.ToLower().Contains(searchString.ToLower()) == true ||
                                            todo.CreatedOn.ToString().ToLower().Contains(searchString.ToLower())
                                     ).ToList();
                }

                switch (sortField)
                {
                    case "Task":
                        todos = sortAscending ? todos.OrderBy(item => item.Todoname).ToList() :
                            todos.OrderByDescending(item => item.Todoname).ToList();
                        break;
                    case "Status":
                        todos = sortAscending ? todos.OrderBy(item => item.Status).ToList() :
                            todos.OrderByDescending(item => item.Status).ToList();
                        break;
                    case "CreatedOn":
                        todos = sortAscending ? todos.OrderBy(item => item.CreatedOn).ToList() :
                            todos.OrderByDescending(item => item.CreatedOn).ToList();
                        break;
                    default:
                        // Default sorting if no valid sort field provided (sort by Id by default)
                        todos = todos.OrderByDescending(item => item.Id).ToList();
                        break;
                }


                int tot_records = todos.Count;
                int pagesize = rowperpage > 0 ? rowperpage : 10;
                int number_of_button = 4;

                Pager P = new Pager(tot_records, page, pagesize, number_of_button, searchString);
                ViewBag.pager = P;
                int skip_records = (Math.Max(P.CurrentPage, 1) - 1) * pagesize;
                int take_records = pagesize;
                List<Todo> todoss = todos.Skip(skip_records).Take(take_records).ToList();



                TodoviewModel todoviewModel = new TodoviewModel()
                {
                    Todos = todoss,
                    Todo = null,

                };

                // Return the view with the list of Todo items
                return View(todoviewModel);

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }

        [HttpGet]
        public async Task<IActionResult> CompletedTodo(int page, int rowperpage, string? searchString = null, string? sortField = null, bool sortAscending = true)
        {

            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                if (page <= 0) { page = 1; }

                ViewBag.CurrentSortField = sortField;
                ViewBag.CurrentSortAscending = sortAscending;
                // Fetch all Todo items from the database
                var todos = await _context.Todos.Where(item => item.UserId == LogSesson.Id && item.Status == "Done").ToListAsync();
                //List<Todo> todoss = todos;
                if (!string.IsNullOrEmpty(searchString))
                {
                    todos = todos.Where(todo =>
                                            todo.Todoname.ToLower().Contains(searchString.ToLower()) ||
                                            todo.Status?.ToLower().Contains(searchString.ToLower()) == true ||
                                            todo.CreatedOn.ToString().ToLower().Contains(searchString.ToLower())
                                     ).ToList();
                }

                switch (sortField)
                {
                    case "Task":
                        todos = sortAscending ? todos.OrderBy(item => item.Todoname).ToList() :
                            todos.OrderByDescending(item => item.Todoname).ToList();
                        break;
                    case "Status":
                        todos = sortAscending ? todos.OrderBy(item => item.Status).ToList() :
                            todos.OrderByDescending(item => item.Status).ToList();
                        break;
                    case "CreatedOn":
                        todos = sortAscending ? todos.OrderBy(item => item.CreatedOn).ToList() :
                            todos.OrderByDescending(item => item.CreatedOn).ToList();
                        break;
                    default:
                        // Default sorting if no valid sort field provided (sort by Id by default)
                        todos = todos.OrderByDescending(item => item.Id).ToList();
                        break;
                }


                int tot_records = todos.Count;
                int pagesize = rowperpage > 0 ? rowperpage : 10;
                int number_of_button = 4;

                Pager P = new Pager(tot_records, page, pagesize, number_of_button, searchString);
                ViewBag.pager = P;
                int skip_records = (Math.Max(P.CurrentPage, 1) - 1) * pagesize;
                int take_records = pagesize;
                List<Todo> todoss = todos.Skip(skip_records).Take(take_records).ToList();



                TodoviewModel todoviewModel = new TodoviewModel()
                {
                    Todos = todoss,
                    Todo = null,

                };

                // Return the view with the list of Todo items
                return View(todoviewModel);

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }


        }


        [HttpGet]
        public async Task<IActionResult> TodoReports()
        {
            try
            {


                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;

                var supportEngineer = _context.Users.Where(i => i.Department == "Support Engineer").ToList();

                var reportView = new TodoReportView
                {
                    Todos = null,
                    ListofEmployee = supportEngineer,
                    search = null,
                    TodoHederInfo = null,
                    brokerages = null,

                };


                return View(reportView);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpGet]
        public async Task<IActionResult> TodoSearch(TodoReportView reportView)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                reportView.search ??= new XFLCSMS.Models.Todos.TodoSearch();
                if (reportView.search != null)
                {
                    // Check if FromDate is not null and is greater than or equal to the current date
                    if (reportView.search.FromDate.HasValue && reportView.search.FromDate >= DateTime.Now)
                    {
                        reportView.search.FromDate = DateTime.Now;
                    }

                    // Check if ToDate is not null and is greater than or equal to the current date
                    if (reportView.search.ToDate.HasValue && reportView.search.ToDate >= DateTime.Now)
                    {
                        reportView.search.ToDate = DateTime.Now;
                    }
                }

                var searchResults = _context.Todos
                    .Where(item => item.UserId == LogSesson.Id)
                    .OrderByDescending(item => item.Id)
                    .ToList();


                if (!String.IsNullOrEmpty(reportView.search.Status))
                    searchResults = searchResults.Where(x => x.Status != null && x.Status.Contains(reportView.search.Status)).ToList();

                // both ends of the range are inclusive, and either end may be left empty
                if (reportView.search.FromDate != null)
                    searchResults = searchResults.Where(x => x.CreatedOn.Date >= reportView.search.FromDate.Value.Date).ToList();
                if (reportView.search.ToDate != null)
                    searchResults = searchResults.Where(x => x.CreatedOn.Date <= reportView.search.ToDate.Value.Date).ToList();

                string Brocaragename = GetBrocarageHouseName(LogSesson.BrokerageHouseName);
                string EmployeeNamee = LogSesson.FullName;



                int TotalTodo = searchResults.Count();
                int TotalInprogressTodo = searchResults.Where(x => x.Status == "In progress").Count();
                int TotalCompletedTodoo = searchResults.Where(x => x.Status == "Done").Count();
                int TotalCancledTodo = TotalTodo - TotalInprogressTodo - TotalCompletedTodoo;


                TodoHederInfo SectionInfo = new TodoHederInfo
                {
                    BrokerageHouseName = Brocaragename,
                    EmployeeName = EmployeeNamee,
                    TotalTodo = TotalTodo,
                    TotalInprogress = TotalInprogressTodo,
                    TotalCanseled = TotalCancledTodo,
                    TotalCompleteTodo = TotalCompletedTodoo,
                    ReportName = "Admin"


                };

                // Populate the Issues property of the ReportView model with search results
                var reportViewWithSearchResults = new TodoReportView
                {
                    Todos = searchResults,
                    TodoHederInfo = SectionInfo,

                };

                // Return the partial view with the populated reportView model
                return PartialView("_todoSearchResult", reportViewWithSearchResults);

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }


        }


        [HttpGet]
        public async Task<IActionResult> ViewAllTodo(int page, int rowperpage, string? searchString = null, string? sortField = null, bool sortAscending = true)
        {
            try
            {
                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;
                if (page <= 0) { page = 1; }

                ViewBag.CurrentSortField = sortField;
                ViewBag.CurrentSortAscending = sortAscending;
                // Fetch all Todo items from the database
                //var todos = await _context.Todos.Where(item => item.UserId == LogSesson.Id && item.Status == "In progress").ToListAsync();
                var todos = await _context.Todos
                            .OrderByDescending(item => item.Id)
                            .ToListAsync();
                //List<Todo> todoss = todos;
                if (!string.IsNullOrEmpty(searchString))
                {
                    todos = todos.Where(todo =>
                                            todo.Todoname.ToLower().Contains(searchString.ToLower()) ||
                                            todo.Status?.ToLower().Contains(searchString.ToLower()) == true ||
                                            todo.CreatedOn.ToString().ToLower().Contains(searchString.ToLower())
                                     ).ToList();
                }

                switch (sortField)
                {
                    case "Task":
                        todos = sortAscending ? todos.OrderBy(item => item.Todoname).ToList() :
                            todos.OrderByDescending(item => item.Todoname).ToList();
                        break;
                    case "Status":
                        todos = sortAscending ? todos.OrderBy(item => item.Status).ToList() :
                            todos.OrderByDescending(item => item.Status).ToList();
                        break;
                    case "CreatedOn":
                        todos = sortAscending ? todos.OrderBy(item => item.CreatedOn).ToList() :
                            todos.OrderByDescending(item => item.CreatedOn).ToList();
                        break;
                    default:
                        // Default sorting if no valid sort field provided (sort by Id by default)
                        todos = todos.OrderByDescending(item => item.Id).ToList();
                        break;
                }


                int tot_records = todos.Count;
                int pagesize = rowperpage > 0 ? rowperpage : 10;
                int number_of_button = 4;

                Pager P = new Pager(tot_records, page, pagesize, number_of_button, searchString);
                ViewBag.pager = P;
                int skip_records = (Math.Max(P.CurrentPage, 1) - 1) * pagesize;
                int take_records = pagesize;
                List<Todo> todoss = todos.Skip(skip_records).Take(take_records).OrderByDescending(item => item.Id).ToList();



                TodoviewModel todoviewModel = new TodoviewModel()
                {
                    Todos = todoss,
                    Todo = null,

                };

                // Return the view with the list of Todo items
                return View(todoviewModel);

            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }

        }


        [HttpGet]
        public async Task<IActionResult> AllTodoReports()
        {
            try
            {


                var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
                User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);
                ViewBag.Profile = LogSesson;

                var supportEngineer = _context.Users.ToList();
                var BrocarageHouse = _context.Brokerages.ToList();

                var reportView = new TodoReportView
                {
                    Todos = null,
                    ListofEmployee = supportEngineer,
                    search = null,
                    TodoHederInfo = null,
                    brokerages = BrocarageHouse,

                };


                return View(reportView);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


        [HttpGet]
        public async Task<IActionResult> AllTodoSearch(TodoReportView reportView)
        {
            try { 
            var jsonStringFromSession = HttpContext.Session.GetString("AdminData");
            User LogSesson = JsonConvert.DeserializeObject<User>(jsonStringFromSession);


            reportView.search ??= new XFLCSMS.Models.Todos.TodoSearch();
            if (reportView.search != null)
            {
                // Check if FromDate is not null and is greater than or equal to the current date
                if (reportView.search.FromDate.HasValue && reportView.search.FromDate >= DateTime.Now)
                {
                    reportView.search.FromDate = DateTime.Now;
                }

                // Check if ToDate is not null and is greater than or equal to the current date
                if (reportView.search.ToDate.HasValue && reportView.search.ToDate >= DateTime.Now)
                {
                    reportView.search.ToDate = DateTime.Now;
                }
            }

            var searchResults = await _context.Todos.ToListAsync();

            // both ends of the range are inclusive, and either end may be left empty
            if (reportView.search.FromDate != null)
                searchResults = searchResults.Where(x => x.CreatedOn.Date >= reportView.search.FromDate.Value.Date).ToList();
            if (reportView.search.ToDate != null)
                searchResults = searchResults.Where(x => x.CreatedOn.Date <= reportView.search.ToDate.Value.Date).ToList();

            if (!String.IsNullOrEmpty(reportView.search.Status))
                searchResults = searchResults.Where(x => x.Status != null && x.Status.Contains(reportView.search.Status)).ToList();

            if (reportView.search.BrocarageHouseName.HasValue)
                searchResults = searchResults.Where(x => x.BrokerageId == reportView.search.BrocarageHouseName).ToList();
            if (reportView.search.EmployeeName.HasValue)
                searchResults = searchResults.Where(x => x.UserId==reportView.search.EmployeeName).ToList();



            string Brocaragename = reportView.search.BrocarageHouseName !=null ?GetBrocarageHouseName(reportView.search.BrocarageHouseName) :"All";
            string EmployeeNamee = (reportView.search.EmployeeName != null) ? GetEmployeeName(reportView.search.EmployeeName) : "All";




            int TotalTodo = searchResults.Count();
            int TotalInprogressTodo = searchResults.Where(x => x.Status == "In progress").Count();
            int TotalCompletedTodoo = searchResults.Where(x => x.Status == "Done").Count();
            int TotalCancledTodo = TotalTodo - TotalInprogressTodo - TotalCompletedTodoo;


            TodoHederInfo SectionInfo = new TodoHederInfo
            {
                BrokerageHouseName = Brocaragename,
                EmployeeName = EmployeeNamee,
                TotalTodo = TotalTodo,
                TotalInprogress = TotalInprogressTodo,
                TotalCanseled = TotalCancledTodo,
                TotalCompleteTodo = TotalCompletedTodoo,
                ReportName = "Admin"


            };

            // Populate the Issues property of the ReportView model with search results
            var reportViewWithSearchResults = new TodoReportView
            {
                Todos = searchResults,
                TodoHederInfo = SectionInfo,

            };

            // Return the partial view with the populated reportView model
            return PartialView("_todoSearchResult", reportViewWithSearchResults);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }


















        private string GetBrocarageHouseName(int? id)
        {
            var Brocarage = _context.Brokerages.ToList();
            foreach (var item in Brocarage)
            {
                if(item.BrokerageId== id)
                {
                    return item.BrokerageHouseName;
                }
            }
            return null;
        }

        private string GetEmployeeName(int? id)
        {
            var user = _context.Users.ToList();
            foreach (var item in user)
            {
                if (item.Id == id)
                {
                    return item.FullName;
                }
            }
            return null;
        }

        private string GetBranchName(int id)
        {
            var Brocarage = _context.Branchhs.ToList();
            foreach (var item in Brocarage)
            {
                if (item.BranchId == id)
                {
                    return item.BranchName;
                }
            }
            return null;
        }


        private string UserName(int id)
        {
            var user = _context.Users.Where(i => i.Id == id).FirstOrDefault();
            return user?.FullName ?? string.Empty;
        }
        private string SupportTypeName(int? id)
        {
            var user = _context.SupportTypes.Where(i => i.SupportTypeId == id).FirstOrDefault();
            if (user == null)
            {
                return null;
            }
            return user.SType;
        }



        private string SupportCatagoryName(int? id)
        {
            var user = _context.SupportCatagories.Where(i => i.SupportCatagoryId == id).FirstOrDefault();
            if (user == null)
            {
                return null;
            }

            return user.SCatagory;
        }

        private string SupportSubCatagoryName(int? id)
        {
            var user = _context.SupportSubCatagories.Where(i => i.SupportSubCatagoryId == id).FirstOrDefault();
            if (user == null)
            {
                return null;
            }
            return user.SubCatagory;
        }

        private string AffectedSectionName(int? id)
        {
            var user = _context.AffectedSectionss.Where(i => i.AffectedSectionId == id).FirstOrDefault();
            if (user == null)
            {
                return null;
            }
            return user.ASection;
        }

        // ---- The administrator creates users and raises tickets ------------------------------------------------

        private static readonly string[] NewUserRoles = { "Admin", "Support Maneger", "Support Engineer", "Maker" };

        private NewUserView FillNewUser(NewUserView form)
        {
            var houses = _context.Brokerages.ToList().ToDictionary(b => b.BrokerageId, b => b.BrokerageHouseName);
            form.Branches = _context.Branchhs.ToList()
                .Select(b => (b.BranchId, Label: (houses.TryGetValue(b.BrokerageId ?? 0, out var house) ? house : "?") + " - " + b.BranchName))
                .OrderBy(b => b.Label)
                .ToList();
            return form;
        }

        public IActionResult CreateUser()
        {
            try
            {
                return View(FillNewUser(new NewUserView()));
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(NewUserView form)
        {
            try
            {
                form.UserName = (form.UserName ?? string.Empty).Trim();
                form.Email = (form.Email ?? string.Empty).Trim();

                var branch = await _context.Branchhs.FirstOrDefaultAsync(b => b.BranchId == form.Branch);
                if (ModelState.IsValid)
                {
                    if (branch?.BrokerageId == null)
                    {
                        ModelState.AddModelError(nameof(form.Branch), "Please select the brokerage house and branch.");
                    }

                    if (!NewUserRoles.Contains(form.Role))
                    {
                        ModelState.AddModelError(nameof(form.Role), "Please select a role.");
                    }

                    if (await _context.Users.AnyAsync(u => u.Email == form.Email))
                    {
                        ModelState.AddModelError(nameof(form.Email), "This email is already registered.");
                    }

                    if (await _context.Users.AnyAsync(u => u.UserName == form.UserName))
                    {
                        ModelState.AddModelError(nameof(form.UserName), "This user name is already taken.");
                    }
                }

                if (!ModelState.IsValid || branch?.BrokerageId == null)
                {
                    form.Password = string.Empty;
                    form.ConfirmPassword = string.Empty;
                    return View(FillNewUser(form));
                }

                PasswordHasher.Create(form.Password, out byte[] passwordHash, out byte[] passwordSalt);

                var isAdmin = form.Role == "Admin";
                var isStaff = isAdmin || form.Role == "Support Maneger" || form.Role == "Support Engineer";
                var user = new User
                {
                    FullName = form.FullName.Trim(),
                    Email = form.Email,
                    PhonNumber = form.PhonNumber.Trim(),
                    Designation = form.Designation?.Trim() ?? string.Empty,
                    BrokerageHouseName = branch.BrokerageId.Value,
                    BrokerageHouseAcronym = branch.BrokerageId.Value,
                    Branch = branch.BranchId,
                    EmployeeId = form.EmployeeId.Trim(),
                    UserName = form.UserName,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt,
                    VerifiedAt = DateTime.Now, // created by the administrator: no token by e-mail needed
                    Department = isAdmin ? "Maker" : form.Role,
                    UCatagory = isAdmin,
                    UType = isStaff,
                    UStatus = true,
                    Terms = true
                };

                await _context.Users.AddAsync(user);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "User " + user.UserName + " was created and can sign in now.";
                return RedirectToAction("UserList");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        public IActionResult IssueRaiseFrom()
        {
            try
            {
                var me = CurrentUser!;
                var viewModel = new IssueViewModel
                {
                    Products = Tickets.ActiveProducts(),
                    SupportTypes = _context.SupportTypes.ToList(),
                    SupportCatagories = _context.SupportCatagories.ToList(),
                    SupportSubCatagories = _context.SupportSubCatagories.ToList(),
                    AffectedSections = _context.AffectedSectionss.ToList(),
                    Brokerages = _context.Brokerages.ToList(),
                    Branchhs = _context.Branchhs.ToList(),
                    LoginInfo = new IssueLoginInfo
                    {
                        UserId = me.Id,
                        BrocarageHouseName = GetBrocarageHouseName(me.BrokerageHouseName) ?? string.Empty,
                        BranchName = GetBranchName(me.Branch) ?? string.Empty,
                        // shown on the form as a preview only; the real number is taken when the ticket is saved
                        TicketID = Tickets.NextTicketNumber(me.BrokerageHouseName) ?? string.Empty
                    }
                };
                return View(viewModel);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueRaiseFrom(IssueViewModel issueViewModel, List<IFormFile> files)
        {
            try
            {
                // Owner, brokerage house, ticket number and date are decided on the server (see TicketService).
                var result = await Tickets.CreateAsync(CurrentUser!, issueViewModel?.issueFrom, files);
                if (result.Issue == null)
                {
                    TempData["ErrorMessage"] = result.Error;
                    return RedirectToAction("IssueRaiseFrom");
                }

                if (result.RejectedFiles.Count > 0)
                {
                    TempData["ErrorMessage"] = "Ticket " + result.Issue.TNumber + " was created, but these files were not attached (file type not allowed or larger than 10 MB): "
                        + string.Join(", ", result.RejectedFiles);
                }
                else
                {
                    TempData["SuccessMessage"] = "Ticket " + result.Issue.TNumber + " was created.";
                }

                return RedirectToAction("AdminView");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Ticket counts for a period: by house, engineer, support type, priority, user and day or month.</summary>
        public async Task<IActionResult> TicketCounts(DateTime? from, DateTime? to, string? period, string? export, [FromServices] TicketCountService counts)
        {
            try
            {
                (from, to) = TicketCountService.Period(period, from, to);
                var view = await counts.CountAsync(from, to);
                view.Layout = "_Layout";
                view.Controller = "Admin";

                if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    var name = "ticket-counts-" + (view.From?.ToString("yyyyMMdd") ?? "all") + "-" + (view.To?.ToString("yyyyMMdd") ?? "now") + ".csv";
                    return File(TicketCountService.Csv(view), "text/csv", name);
                }

                return View("TicketCounts", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Client ratings and feedback (management only).</summary>
        public async Task<IActionResult> ServiceFeedback(DateTime? from, DateTime? to, int? engineerId, int? stars, int? houseId, bool followUp,
            string? export, [FromServices] FeedbackService feedback)
        {
            try
            {
                if (from == null && to == null && !followUp && Request.Query.Count == 0)
                {
                    from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
                }

                var view = await feedback.ListAsync(from, to, engineerId, stars, houseId, followUp);
                view.Layout = "_Layout";
                view.Controller = "Admin";

                if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    return File(FeedbackService.Csv(view), "text/csv", "service-feedback-" + DateTime.Now.ToString("yyyyMMdd") + ".csv");
                }

                return View("ServiceFeedback", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Marks a (low) rating as followed up, with an internal note.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewRating(int ratingId, string? note, string? returnUrl, [FromServices] FeedbackService feedback)
        {
            try
            {
                if (!await feedback.MarkReviewedAsync(ratingId, CurrentUser!, note))
                {
                    return NotFound();
                }

                TempData["SuccessMessage"] = "The rating was marked as reviewed.";
                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("ServiceFeedback");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>One Support Engineer's performance: KPIs, six-month trend and client feedback in depth.</summary>
        public async Task<IActionResult> EngineerProfile(int id, string? month, [FromServices] ScorecardService scorecard)
        {
            try
            {
                DateTime? selected = DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
                var view = await scorecard.ProfileAsync(id, selected);
                if (view == null)
                {
                    return NotFound();
                }

                view.Layout = "_Layout";
                view.Controller = "Admin";
                return View("EngineerProfile", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Answers the client's feedback; the reply is shown on the client's ticket page and e-mailed to him.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyToClient(int ratingId, string? reply, string? returnUrl, [FromServices] FeedbackService feedback)
        {
            try
            {
                var rating = await feedback.ReplyAsync(ratingId, CurrentUser!, reply);
                if (rating == null)
                {
                    TempData["ErrorMessage"] = "Please write the reply.";
                }
                else
                {
                    // the client is told through the notification system (step "Reply to client feedback")
                    TempData["SuccessMessage"] = "Your reply was sent to the client.";
                }

                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("ServiceFeedback");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Leaves a rating out of the engineer scorecard (with a reason), or counts it again.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcludeRating(int ratingId, bool exclude, string? reason, string? returnUrl, [FromServices] FeedbackService feedback)
        {
            try
            {
                var error = await feedback.SetExcludedAsync(ratingId, CurrentUser!, exclude, reason);
                TempData[error == null ? "SuccessMessage" : "ErrorMessage"] = error ?? (exclude ? "The rating is no longer counted in the scorecard." : "The rating is counted in the scorecard again.");
                return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : RedirectToAction("ServiceFeedback");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Monthly Support Engineer ranking and Best Support Engineer of the Month.</summary>
        public async Task<IActionResult> Scorecard(string? month, string? export, [FromServices] ScorecardService scorecard)
        {
            try
            {
                DateTime? selected = DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsed) ? parsed : null;
                var view = await scorecard.ViewAsync(selected);
                view.Layout = "_Layout";
                view.Controller = "Admin";
                view.CanDecide = true;

                if (string.Equals(export, "csv", StringComparison.OrdinalIgnoreCase))
                {
                    return File(ScorecardService.Csv(view), "text/csv", "engineer-scorecard-" + view.Month.ToString("yyyy-MM") + ".csv");
                }

                return View("Scorecard", view);
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Confirms the nominated Best Support Engineer of the Month, or names another engineer with a reason.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DecideAward(string month, int? engineerUserId, string? reason, [FromServices] ScorecardService scorecard)
        {
            try
            {
                if (!DateTime.TryParseExact(month, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var parsed))
                {
                    return BadRequest();
                }

                var error = await scorecard.DecideAsync(parsed, CurrentUser!, engineerUserId, reason);
                if (error != null)
                {
                    TempData["ErrorMessage"] = error;
                }
                else
                {
                    TempData["SuccessMessage"] = "Best Support Engineer of the Month saved.";
                }

                return RedirectToAction("Scorecard", new { month });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>SLA targets per priority, used by the engineer scorecard.</summary>
        [HttpGet]
        public async Task<IActionResult> SlaTargets([FromServices] ScorecardService scorecard)
        {
            try
            {
                return View(await scorecard.SlaTargetsAsync());
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SlaTargets(List<XFLCSMS.Models.Feedback.SlaTarget> targets)
        {
            try
            {
                var known = XFLCSMS.Models.Feedback.SlaTarget.Defaults.Select(d => d.Priority).ToList();
                targets = targets.Where(t => known.Contains(t.Priority)).ToList();
                if (!ModelState.IsValid || targets.Count == 0)
                {
                    ViewBag.message = "Please enter whole minutes (at least 1) for every priority.";
                    return View(targets);
                }

                foreach (var target in targets)
                {
                    var row = await _context.SlaTargets.FirstOrDefaultAsync(t => t.Priority == target.Priority);
                    if (row == null)
                    {
                        _context.SlaTargets.Add(new XFLCSMS.Models.Feedback.SlaTarget
                        {
                            Priority = target.Priority, FirstResponseMinutes = target.FirstResponseMinutes, ResolutionMinutes = target.ResolutionMinutes
                        });
                    }
                    else
                    {
                        row.FirstResponseMinutes = target.FirstResponseMinutes;
                        row.ResolutionMinutes = target.ResolutionMinutes;
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "SLA targets saved. Months that are already closed keep their frozen scores.";
                return RedirectToAction("SlaTargets");
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
