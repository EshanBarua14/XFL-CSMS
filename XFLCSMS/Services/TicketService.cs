using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using XFLCSMS.Models.Feedback;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;

namespace XFLCSMS.Services
{
    /// <summary>
    /// Ticket logic that used to be copy-pasted (with different bugs) into every role controller:
    /// ticket numbering, ticket creation, attachment storage/lookup and the staff "edit ticket" rules.
    /// </summary>
    public class TicketService
    {
        public const string UploadFolderName = "Uplods";

        /// <summary>File types that may be attached to a ticket (the folder is served from wwwroot).</summary>
        public static readonly string[] AllowedExtensions =
        {
            ".txt", ".doc", ".docx", ".pdf", ".jpg", ".jpeg", ".png", ".xls", ".xlsx", ".csv"
        };

        /// <summary>Largest file accepted per attachment.</summary>
        public const long MaxAttachmentBytes = 10 * 1024 * 1024;

        /// <summary>The priorities the forms offer; anything else posted is refused.</summary>
        public static readonly string[] Priorities = { "High", "Medium", "Low" };

        /// <summary>Ticket statuses as stored, with the text shown to people.</summary>
        public static readonly (string Value, string Label)[] Statuses =
        {
            ("Open", "Open"), ("Inqueue", "In Queue"), ("Inprogress", "In Progress"), ("Close", "Closed")
        };

        public static string StatusLabel(string? status)
        {
            foreach (var s in Statuses)
            {
                if (string.Equals(s.Value, status, StringComparison.OrdinalIgnoreCase)) { return s.Label; }
            }

            return string.IsNullOrWhiteSpace(status) ? "Open" : status;
        }

        public static string? NormalizePriority(string? priority)
        {
            return Priorities.FirstOrDefault(p => string.Equals(p, priority?.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // Two people saving a ticket for the same house at the same moment used to get the same number
        // (both read the highest number, both add one). Creation is serialised in this web server.
        private static readonly SemaphoreSlim NumberLock = new(1, 1);

        private static readonly FileExtensionContentTypeProvider ContentTypes = new();

        private readonly DataContext _context;
        private readonly IWebHostEnvironment _environment;

        public TicketService(DataContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public string UploadFolder =>
            Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), UploadFolderName);

        /// <summary>Next free ticket number for a brokerage house, e.g. ABC_0000042. Null when the house does not exist.</summary>
        public string? NextTicketNumber(int brokerageId)
        {
            var house = _context.Brokerages.FirstOrDefault(b => b.BrokerageId == brokerageId);
            if (house == null)
            {
                return null;
            }

            var prefix = house.BrokerageHouseAcronym + "_";
            var max = 0;
            var numbers = _context.Issues
                .Where(i => i.TNumber.StartsWith(prefix))
                .Select(i => i.TNumber)
                .ToList();

            foreach (var number in numbers)
            {
                // TryParse: one malformed ticket number must not break ticket creation for everybody.
                if (int.TryParse(number.Substring(prefix.Length), out var value) && value > max)
                {
                    max = value;
                }
            }

            return prefix + (max + 1).ToString("D7");
        }

        /// <summary>
        /// Creates a ticket for the logged-in user. Owner, brokerage house, ticket number and creation time
        /// always come from the server, never from the (editable) form fields.
        /// </summary>
        public async Task<(IssueTable? Issue, string? Error, List<string> RejectedFiles)> CreateAsync(
            User user, IssueFrom? form, IEnumerable<IFormFile>? files)
        {
            var rejected = new List<string>();

            if (form == null)
            {
                return (null, "The ticket form was empty. Please fill it in again.", rejected);
            }

            if (string.IsNullOrWhiteSpace(form.ITitle))
            {
                return (null, "Please enter an issue title.", rejected);
            }

            var priority = NormalizePriority(form.Priority);
            if (priority == null)
            {
                return (null, "Please select a priority (High, Medium or Low).", rejected);
            }

            if (form.ITitle.Trim().Length > 300)
            {
                return (null, "The issue title is too long (300 characters at most).", rejected);
            }

            if (!form.ProductId.HasValue || !_context.Products.Any(p => p.ProductId == form.ProductId.Value && p.IsActive))
            {
                return (null, "Please select the product the ticket is about.", rejected);
            }

            // XFL staff may raise a ticket for a brokerage house (a call, an e-mail): it gets that house's number and
            // is counted for that house. Everybody else raises for the own house, whatever the form says.
            var houseId = user.BrokerageHouseName;
            if ((user.UCatagory || user.UType) && form.ForBrokerageId.HasValue
                && _context.Brokerages.Any(b => b.BrokerageId == form.ForBrokerageId.Value))
            {
                houseId = form.ForBrokerageId.Value;
            }

            await NumberLock.WaitAsync();
            IssueTable issue;
            try
            {
                var ticketNumber = NextTicketNumber(houseId);
                if (ticketNumber == null)
                {
                    return (null, "Your account is not linked to a valid brokerage house. Please contact the XFL team.", rejected);
                }

                issue = new IssueTable
                {
                    TDate = DateTime.Now,
                    TNumber = ticketNumber,
                    Priority = priority,
                    ProductId = form.ProductId,
                    ITitle = form.ITitle.Trim(),
                    Details = form.IssueDetails,
                    Comments = form.Commands,
                    UserId = user.Id,
                    BrokerageId = houseId,
                    SupportTypeId = Existing(form.SupportTypeId, id => _context.SupportTypes.Any(x => x.SupportTypeId == id)),
                    SupportCatagoryId = Existing(form.SupportCatagoryId, id => _context.SupportCatagories.Any(x => x.SupportCatagoryId == id)),
                    SupportSubCatagoryId = Existing(form.SupportSubCatagoryID, id => _context.SupportSubCatagories.Any(x => x.SupportSubCatagoryId == id)),
                    AffectedSectionId = Existing(form.AffectedSectionId, id => _context.AffectedSectionss.Any(x => x.AffectedSectionId == id)),
                    IStatus = "Open",
                    AssignOn = null,
                    AssignBy = null
                };

                _context.Issues.Add(issue);
                await _context.SaveChangesAsync();
            }
            finally
            {
                NumberLock.Release();
            }

            _context.TicketStatusHistories.Add(new TicketStatusHistory
            {
                IssueId = issue.IssueId,
                FromStatus = null,
                ToStatus = issue.IStatus,
                ChangedByUserId = user.Id,
                ChangedByName = user.FullName,
                ChangedOn = issue.TDate
            });
            await _context.SaveChangesAsync();

            rejected = await SaveAttachmentsAsync(issue.IssueId, files);
            return (issue, null, rejected);
        }

        private static int? Existing(int? id, Func<int, bool> exists)
        {
            return id.HasValue && exists(id.Value) ? id : null;
        }

        /// <summary>Stores uploaded files for a ticket. Returns the names of files that were refused.</summary>
        public async Task<List<string>> SaveAttachmentsAsync(int issueId, IEnumerable<IFormFile>? files)
        {
            var rejected = new List<string>();
            if (files == null)
            {
                return rejected;
            }

            Directory.CreateDirectory(UploadFolder);
            var added = false;

            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    continue;
                }

                // Keep only the file name: browsers may send a full client path and "..\\" must never reach the disk.
                var originalName = Path.GetFileName((file.FileName ?? string.Empty).Replace('\\', '/'));
                var extension = Path.GetExtension(originalName).ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(originalName) || !AllowedExtensions.Contains(extension) || file.Length > MaxAttachmentBytes)
                {
                    rejected.Add(string.IsNullOrWhiteSpace(originalName) ? "(unnamed file)" : originalName);
                    continue;
                }

                // Unique name on disk so two tickets with "screenshot.png" no longer overwrite each other.
                var storedName = Guid.NewGuid().ToString("N") + extension;
                var filePath = Path.Combine(UploadFolder, storedName);

                await using (var stream = new FileStream(filePath, FileMode.CreateNew))
                {
                    await file.CopyToAsync(stream);
                }

                _context.Attachments.Add(new Attachment
                {
                    FileName = originalName,
                    AttachmentLoc = filePath,
                    IssueId = issueId
                });
                added = true;
            }

            if (added)
            {
                await _context.SaveChangesAsync();
            }

            return rejected;
        }

        /// <summary>
        /// Finds the attachment on disk. AttachmentLoc holds an absolute path from the machine that saved it, so after
        /// restoring the database on another PC/server we fall back to this installation's upload folder.
        /// </summary>
        public string? ResolveAttachmentPath(Attachment attachment)
        {
            if (!string.IsNullOrWhiteSpace(attachment.AttachmentLoc) && File.Exists(attachment.AttachmentLoc))
            {
                return attachment.AttachmentLoc;
            }

            foreach (var name in new[] { LastSegment(attachment.AttachmentLoc), LastSegment(attachment.FileName) })
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var candidate = Path.Combine(UploadFolder, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static string GetContentType(string fileName)
        {
            return ContentTypes.TryGetContentType(fileName, out var contentType) ? contentType : "application/octet-stream";
        }

        /// <summary>Removes the attachment row and, when no other ticket uses the same file, the file itself.</summary>
        public async Task DeleteAttachmentAsync(Attachment attachment)
        {
            var path = ResolveAttachmentPath(attachment);
            var location = attachment.AttachmentLoc;

            _context.Attachments.Remove(attachment);
            await _context.SaveChangesAsync();

            if (path == null)
            {
                return;
            }

            var stillUsed = _context.Attachments.Any(a => a.AttachmentLoc == location);
            var insideUploadFolder = Path.GetFullPath(path).StartsWith(Path.GetFullPath(UploadFolder), StringComparison.OrdinalIgnoreCase);
            if (!stillUsed && insideUploadFolder)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // The row is gone; a locked file can be cleaned up later.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        /// <summary>Products offered on the Create Ticket form: the active ones, in the admin's order.</summary>
        public List<XFLCSMS.Models.Product.Product> ActiveProducts()
        {
            return _context.Products.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ThenBy(p => p.ProductName).ToList();
        }

        /// <summary>Every product (for Edit Ticket: a ticket may sit on a product that was switched off since).</summary>
        public List<XFLCSMS.Models.Product.Product> AllProducts()
        {
            return _context.Products.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.ProductName).ToList();
        }

        public string? ProductName(int? productId)
        {
            return productId.HasValue ? _context.Products.AsNoTracking().Where(p => p.ProductId == productId.Value).Select(p => p.ProductName).FirstOrDefault() : null;
        }

        /// <summary>Engineers a ticket can be assigned to: active XFL support engineers (not disabled accounts), by level then name.</summary>
        public Task<List<User>> AssignableEngineersAsync()
        {
            return _context.Users
                .Where(u => u.Department == "Support Engineer" && u.UType && u.UStatus)
                .OrderBy(u => u.SupportLevel ?? 1)
                .ThenBy(u => u.FullName)
                .ToListAsync();
        }

        /// <summary>Edit rules for the ticket owner (Maker): text fields and priority only.</summary>
        public void ApplyOwnerEdit(IssueTable issue, MakerView form, User editor)
        {
            ApplyCommonFields(issue, form, editor);
        }

        /// <summary>
        /// Edit rules for XFL staff (Admin / Support Manager / Support Engineer): also assignment and status.
        /// Records status changes in TicketStatusHistory. Returns true when this edit closed the ticket.
        /// </summary>
        public bool ApplyStaffEdit(IssueTable issue, MakerView form, User editor, bool canApprove)
        {
            var oldStatus = issue.IStatus;
            var oldComments = issue.Comments;
            ApplyCommonFields(issue, form, editor);

            // staff may move a ticket to another product (an existing one; inactive products stay valid on old tickets)
            if (form.ProductId.HasValue && form.ProductId != issue.ProductId
                && _context.Products.Any(p => p.ProductId == form.ProductId.Value))
            {
                issue.ProductId = form.ProductId;
            }

            var now = DateTime.Now;
            var newAssignee = string.IsNullOrWhiteSpace(form.AssgnBy) ? null : form.AssgnBy;
            var assigneeChanged = !string.Equals(newAssignee, string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy, StringComparison.Ordinal);

            // an engineer below the ticket's support level cannot take it (the form does not offer them): it has to be handed back first
            if (assigneeChanged && newAssignee != null)
            {
                var id = FindStaffId(newAssignee);
                var engineerLevel = _context.Users.Where(u => u.Id == id && u.Department == "Support Engineer").Select(u => (int?)(u.SupportLevel ?? 1)).FirstOrDefault();
                if (engineerLevel != null && engineerLevel < issue.SupportLevel)
                {
                    newAssignee = string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy;
                    assigneeChanged = false;
                }
            }

            if (newAssignee == null)
            {
                issue.AssignBy = null;
                issue.AssignOn = null;
                issue.AssignedToUserId = null;
                issue.ApproveBy = null;
                issue.ApproveOn = null;
            }
            else
            {
                if (assigneeChanged || issue.AssignOn == null)
                {
                    // Stamp the assignment time once, when the engineer is actually (re)assigned.
                    issue.AssignBy = newAssignee;
                    issue.AssignOn = now;
                }

                if (assigneeChanged || issue.AssignedToUserId == null)
                {
                    issue.AssignedToUserId = FindStaffId(newAssignee);
                }

                if (canApprove && (assigneeChanged || issue.ApproveOn == null))
                {
                    issue.ApproveOn = now;
                    issue.ApproveBy = editor.FullName;
                }
            }

            // A missing status (field not posted) keeps the current one instead of blanking it.
            var newStatus = string.IsNullOrWhiteSpace(form.IStatus) ? issue.IStatus : form.IStatus;
            var wasClosed = issue.IStatus == "Close";
            issue.IStatus = newStatus;

            if (newStatus == "Close")
            {
                if (!wasClosed || issue.ClosedOn == null)
                {
                    issue.ClosedOn = now;
                    issue.ClosedBy = editor.FullName;
                }
            }
            else
            {
                issue.ClosedOn = null;
                issue.ClosedBy = null;
            }

            var statusChanged = !string.Equals(oldStatus ?? string.Empty, newStatus ?? string.Empty, StringComparison.Ordinal);
            var answered = !string.Equals(oldComments ?? string.Empty, issue.Comments ?? string.Empty, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(issue.Comments);

            // First response: the first time staff move the ticket on from Open, or write an answer in Comments.
            if (issue.FirstResponseOn == null && ((statusChanged && newStatus != "Open") || answered))
            {
                issue.FirstResponseOn = now;
            }

            if (statusChanged)
            {
                _context.TicketStatusHistories.Add(new TicketStatusHistory
                {
                    IssueId = issue.IssueId,
                    FromStatus = oldStatus,
                    ToStatus = newStatus,
                    AssignedToUserId = issue.AssignedToUserId,
                    ChangedByUserId = editor.Id,
                    ChangedByName = editor.FullName,
                    ChangedOn = now
                });
            }

            return statusChanged && newStatus == "Close";
        }

        /// <summary>
        /// The assignee drop-down posts the engineer's full name. Resolve it to a user id, preferring XFL staff
        /// (Support Engineers first) when two users share the name.
        /// </summary>
        public int? FindStaffId(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            return _context.Users
                .Where(u => u.FullName == fullName)
                .OrderByDescending(u => u.Department == "Support Engineer")
                .ThenByDescending(u => u.UType)
                .ThenBy(u => u.Id)
                .Select(u => (int?)u.Id)
                .FirstOrDefault();
        }

        private static void ApplyCommonFields(IssueTable issue, MakerView form, User editor)
        {
            if (!string.IsNullOrWhiteSpace(form.IssueTitle))
            {
                issue.ITitle = form.IssueTitle.Trim();
            }

            issue.Details = form.TicketDetails;
            issue.Comments = form.Command;

            var priority = NormalizePriority(form.Priority);
            if (priority != null)
            {
                issue.Priority = priority;
            }

            issue.UpdatedOn = DateTime.Now;
            issue.UpdatedBy = editor.FullName;
        }

        private static string LastSegment(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Substring(path.LastIndexOfAny(new[] { '/', '\\' }) + 1);
        }
    }
}
