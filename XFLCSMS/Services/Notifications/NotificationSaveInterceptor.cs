using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Affected;
using XFLCSMS.Models.Branch;
using XFLCSMS.Models.Brocarage;
using XFLCSMS.Models.Feedback;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Notifications;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Support;
using XFLCSMS.Models.Tiers;
using XFLCSMS.Models.Todos;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>
    /// Watches every SaveChanges of a web request and turns the changes into workflow events (ticket raised,
    /// assigned, closed, user disabled, house added...). This way every page that changes data notifies, without
    /// notification code in each of the ~100 controller actions. Saves outside a web request (start-up seeding,
    /// background jobs) are ignored.
    /// </summary>
    public class NotificationSaveInterceptor : SaveChangesInterceptor
    {
        private const string CreatedIssuesKey = "notifications.created-issues";

        private readonly IHttpContextAccessor _http;
        private readonly NotificationQueue _queue;
        private readonly ConditionalWeakTable<DbContext, List<Func<NotificationEventData?>>> _pending = new();

        public NotificationSaveInterceptor(IHttpContextAccessor http, NotificationQueue queue)
        {
            _http = http;
            _queue = queue;
        }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Capture(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Capture(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            Flush(eventData.Context);
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Flush(eventData.Context);
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        public override void SaveChangesFailed(DbContextErrorEventData eventData)
        {
            if (eventData.Context != null) _pending.Remove(eventData.Context);
            base.SaveChangesFailed(eventData);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null) _pending.Remove(eventData.Context);
            return base.SaveChangesFailedAsync(eventData, cancellationToken);
        }

        private void Flush(DbContext? context)
        {
            if (context == null || !_pending.TryGetValue(context, out var list))
            {
                return;
            }

            _pending.Remove(context);
            foreach (var build in list)
            {
                try
                {
                    var data = build();
                    if (data != null)
                    {
                        _queue.Publish(data);
                    }
                }
                catch (Exception)
                {
                    // a notification must never break the save that triggered it
                }
            }
        }

        /// <summary>The signed-in user of the request (any role), or null for anonymous pages such as registration.</summary>
        public static (int? Id, string? Name) Actor(HttpContext? http)
        {
            if (http == null)
            {
                return (null, null);
            }

            try
            {
                foreach (var key in SessionAuthorizeAttribute.AllKeys)
                {
                    var json = http.Session.GetString(key);
                    if (!string.IsNullOrEmpty(json))
                    {
                        var user = JsonConvert.DeserializeObject<User>(json);
                        if (user != null) return (user.Id, user.FullName);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // session not configured for this request
            }

            return (null, null);
        }

        public static string? BaseUrl(HttpContext? http)
        {
            return http == null ? null : http.Request.Scheme + "://" + http.Request.Host + http.Request.PathBase;
        }

        private void Capture(DbContext? context)
        {
            var http = _http.HttpContext;
            if (context == null || http == null)
            {
                return;
            }

            var (actorId, actorName) = Actor(http);
            var baseUrl = BaseUrl(http);
            var list = new List<Func<NotificationEventData?>>();

            NotificationEventData New(string key)
            {
                return new NotificationEventData { EventKey = key, ActorUserId = actorId, ActorName = actorName ?? "Somebody", BaseUrl = baseUrl };
            }

            var createdIssues = http.Items[CreatedIssuesKey] as HashSet<int> ?? new HashSet<int>();
            http.Items[CreatedIssuesKey] = createdIssues;

            context.ChangeTracker.DetectChanges();
            var entries = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();

            foreach (var entry in entries)
            {
                switch (entry.Entity)
                {
                    case IssueTable issue:
                        CaptureIssue(entry, issue, New, list, createdIssues);
                        break;
                    case User user:
                        CaptureUser(entry, user, New, list, actorId);
                        break;
                    case TicketRating rating:
                        CaptureRating(entry, rating, context, New, list);
                        break;
                    case EngineerAward award:
                        CaptureAward(entry, award, New, list);
                        break;
                    case Todo todo:
                        CaptureTodo(entry, todo, New, list);
                        break;
                    case TicketEscalation escalation when entry.State == EntityState.Added:
                        CaptureEscalation(escalation, context, New, list);
                        break;
                    case TicketDevUpdate update when entry.State == EntityState.Added:
                        CaptureDevUpdate(update, context, New, list);
                        break;
                    case Brokerage b:
                        CaptureSetup(entry, "BrokerageHouse", "brokerage house", nameof(Brokerage.BrokerageHouseName), New, list);
                        break;
                    case Branchh:
                        CaptureSetup(entry, "Branch", "branch", nameof(Branchh.BranchName), New, list);
                        break;
                    case XFLCSMS.Models.Product.Product:
                        CaptureSetup(entry, "Product", "product", nameof(XFLCSMS.Models.Product.Product.ProductName), New, list);
                        break;
                    case SupportType:
                        CaptureSetup(entry, "SupportType", "support type", nameof(SupportType.SType), New, list);
                        break;
                    case SupportCatagory:
                        CaptureSetup(entry, "SupportCategory", "support category", nameof(SupportCatagory.SCatagory), New, list);
                        break;
                    case SupportSubCatagory:
                        CaptureSetup(entry, "SupportSubCategory", "support sub-category", nameof(SupportSubCatagory.SubCatagory), New, list);
                        break;
                    case AffectedSection:
                        CaptureSetup(entry, "AffectedSection", "affected section", nameof(AffectedSection.ASection), New, list);
                        break;
                }
            }

            // one message per save for things that come in batches
            var attachments = entries.Where(e => e.State == EntityState.Added && e.Entity is Attachment).Select(e => (Attachment)e.Entity)
                .GroupBy(a => a.IssueId).ToList();
            foreach (var group in attachments)
            {
                var issueId = group.Key;
                if (createdIssues.Contains(issueId))
                {
                    continue; // files sent with a new ticket belong to "ticket raised"
                }

                var names = string.Join(", ", group.Select(a => a.FileName));
                list.Add(() =>
                {
                    var issue = context.Set<IssueTable>().Local.FirstOrDefault(i => i.IssueId == issueId)
                                ?? context.Set<IssueTable>().AsNoTracking().FirstOrDefault(i => i.IssueId == issueId);
                    if (issue == null) return null;
                    var data = ForTicket(New("AttachmentAdded"), Snapshot(issue));
                    data.Values["FileNames"] = names;
                    return data;
                });
            }

            if (entries.Any(e => e.Entity is SlaTarget && e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue))))
            {
                list.Add(() => New("SlaTargetsUpdated"));
            }

            var frozenMonths = entries.Where(e => e.State == EntityState.Added && e.Entity is MonthlyEngineerScore)
                .Select(e => ((MonthlyEngineerScore)e.Entity).Month).Distinct().ToList();
            foreach (var month in frozenMonths)
            {
                list.Add(() =>
                {
                    var data = New("ScorecardFrozen");
                    data.Values["Month"] = month.ToString("MMMM yyyy");
                    data.LinkAction = "Scorecard";
                    return data;
                });
            }

            if (list.Count > 0)
            {
                if (_pending.TryGetValue(context, out var existing))
                {
                    existing.AddRange(list);
                }
                else
                {
                    _pending.Add(context, list);
                }
            }
        }

        private static T? Original<T>(EntityEntry entry, string property) => (T?)entry.Property(property).OriginalValue;

        private static bool Changed(EntityEntry entry, string property)
        {
            var p = entry.Property(property);
            return !Equals(p.OriginalValue, p.CurrentValue);
        }

        public static TicketSnapshot Snapshot(IssueTable issue) => new()
        {
            IssueId = issue.IssueId,
            TNumber = issue.TNumber,
            Title = issue.ITitle,
            Priority = issue.Priority,
            Status = issue.IStatus,
            OwnerUserId = issue.UserId,
            BrokerageId = issue.BrokerageId,
            ProductId = issue.ProductId,
            EngineerUserId = issue.AssignedToUserId,
            EngineerName = string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy
        };

        public static NotificationEventData ForTicket(NotificationEventData data, TicketSnapshot ticket)
        {
            data.Ticket = ticket;
            data.IssueId = ticket.IssueId;
            data.HouseId = ticket.BrokerageId;
            data.LinkAction = "TicketView";
            data.LinkId = ticket.IssueId;
            data.RelatedUserIds.Add(ticket.OwnerUserId);
            if (ticket.EngineerUserId != null) data.RelatedUserIds.Add(ticket.EngineerUserId.Value);
            return data;
        }

        private static void CaptureIssue(EntityEntry entry, IssueTable issue, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list, HashSet<int> createdIssues)
        {
            if (entry.State == EntityState.Added)
            {
                list.Add(() =>
                {
                    createdIssues.Add(issue.IssueId);
                    return ForTicket(make("TicketCreated"), Snapshot(issue));
                });
                return;
            }

            if (entry.State == EntityState.Deleted)
            {
                var snap = Snapshot(issue);
                list.Add(() =>
                {
                    var data = ForTicket(make("TicketDeleted"), snap);
                    data.LinkAction = null; // the ticket page is gone
                    return data;
                });
                return;
            }

            var any = false;
            var oldStatus = Original<string>(entry, nameof(IssueTable.IStatus));
            if (Changed(entry, nameof(IssueTable.IStatus)))
            {
                var newStatus = issue.IStatus;
                string key = newStatus == "Close" ? "TicketClosed" : oldStatus == "Close" ? "TicketReopened" : "TicketStatusChanged";
                list.Add(() =>
                {
                    var data = ForTicket(make(key), Snapshot(issue));
                    data.Values["OldStatus"] = oldStatus;
                    if (key == "TicketClosed") data.MakerLinkAction = "RateTicket";
                    return data;
                });
                any = true;
            }

            var oldEngineerId = Original<int?>(entry, nameof(IssueTable.AssignedToUserId));
            var oldEngineerName = Original<string>(entry, nameof(IssueTable.AssignBy));
            if (string.IsNullOrWhiteSpace(oldEngineerName)) oldEngineerName = null;
            var newEngineerName = string.IsNullOrWhiteSpace(issue.AssignBy) ? null : issue.AssignBy;
            if (oldEngineerId != issue.AssignedToUserId || !string.Equals(oldEngineerName, newEngineerName, StringComparison.Ordinal))
            {
                // a name typed differently with the same user id is not a new assignment
                var sameUser = oldEngineerId != null && oldEngineerId == issue.AssignedToUserId;
                if (!sameUser)
                {
                    if (issue.AssignedToUserId != null || newEngineerName != null)
                    {
                        list.Add(() => ForTicket(make("TicketAssigned"), Snapshot(issue)));
                        any = true;
                    }

                    if (oldEngineerId != null)
                    {
                        list.Add(() =>
                        {
                            var data = ForTicket(make("TicketUnassigned"), Snapshot(issue));
                            data.RelatedUserIds.Clear();
                            data.RelatedUserIds.Add(oldEngineerId.Value);
                            return data;
                        });
                    }
                }
            }

            if (Changed(entry, nameof(IssueTable.Comments)) && !string.IsNullOrWhiteSpace(issue.Comments))
            {
                var comment = HtmlToText(issue.Comments, 300);
                list.Add(() =>
                {
                    var data = ForTicket(make("TicketCommented"), Snapshot(issue));
                    data.Values["Comment"] = comment;
                    return data;
                });
                any = true;
            }

            var changes = new List<string>();
            if (Changed(entry, nameof(IssueTable.ITitle))) changes.Add("title");
            if (Changed(entry, nameof(IssueTable.Details))) changes.Add("details");
            if (Changed(entry, nameof(IssueTable.Priority))) changes.Add("priority " + Original<string>(entry, nameof(IssueTable.Priority)) + " -> " + issue.Priority);
            if (Changed(entry, nameof(IssueTable.ProductId))) changes.Add("product");
            if (changes.Count > 0 && !any)
            {
                var text = string.Join(", ", changes);
                list.Add(() =>
                {
                    var data = ForTicket(make("TicketUpdated"), Snapshot(issue));
                    data.Values["Changes"] = text;
                    return data;
                });
            }
        }

        private static IssueTable? FindIssue(DbContext context, int issueId) =>
            context.Set<IssueTable>().Local.FirstOrDefault(i => i.IssueId == issueId)
            ?? context.Set<IssueTable>().AsNoTracking().FirstOrDefault(i => i.IssueId == issueId);

        private static void CaptureEscalation(TicketEscalation escalation, DbContext context, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list)
        {
            if (escalation.FromLevel == escalation.ToLevel)
            {
                return; // a reassignment at the same level is announced as "ticket assigned"
            }

            list.Add(() =>
            {
                var issue = FindIssue(context, escalation.IssueId);
                if (issue == null) return null;
                var data = ForTicket(make("TicketEscalated"), Snapshot(issue));
                data.Values["Level"] = SupportLevel.Code(escalation.ToLevel);
                data.Values["OldLevel"] = SupportLevel.Code(escalation.FromLevel);
                data.Values["LevelName"] = context.Set<SupportLevel>().AsNoTracking().Where(l => l.Level == escalation.ToLevel).Select(l => l.Name).FirstOrDefault();
                data.Values["Reason"] = escalation.Reason;
                if (issue.AssignedToUserId == null)
                {
                    // nobody picked it up yet: the engineers of the new level are told it is in their queue
                    foreach (var id in context.Set<User>().AsNoTracking()
                                 .Where(u => u.Department == "Support Engineer" && u.UType && u.UStatus && (u.SupportLevel ?? 1) >= escalation.ToLevel)
                                 .Select(u => u.Id).ToList())
                    {
                        data.RelatedUserIds.Add(id);
                    }
                }

                return data;
            });
        }

        private static void CaptureDevUpdate(TicketDevUpdate update, DbContext context, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list)
        {
            list.Add(() =>
            {
                var issue = FindIssue(context, update.IssueId);
                if (issue == null) return null;
                var data = ForTicket(make("DevelopmentUpdated"), Snapshot(issue));
                data.Values["DevStage"] = DevStages.Label(update.Stage == "None" ? null : update.Stage);
                data.Values["OldDevStage"] = DevStages.Label(update.FromStage);
                data.Values["DevOwner"] = update.OwnerName ?? "not set";
                data.Values["DevEta"] = update.Eta?.ToString("dd MMM yyyy") ?? "not set";
                data.Values["DevReference"] = update.Reference;
                data.Values["DevNote"] = update.VisibleToClient ? update.Note : null;
                if (update.OwnerUserId != null) data.RelatedUserIds.Add(update.OwnerUserId.Value);
                data.StaffOnly = !update.VisibleToClient; // internal: the client is not told
                return data;
            });
        }

        private static readonly string[] ProfileFields =
        {
            nameof(User.FullName), nameof(User.Email), nameof(User.PhonNumber), nameof(User.Designation), nameof(User.Branch),
            nameof(User.BrokerageHouseName), nameof(User.EmployeeId), nameof(User.UserName), nameof(User.SupportLevel)
        };

        private static void CaptureUser(EntityEntry entry, User user, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list, int? actorId)
        {
            NotificationEventData ForUser(string key)
            {
                var data = make(key);
                data.SubjectUserId = user.Id;
                data.HouseId = user.BrokerageHouseName;
                data.RelatedUserIds.Add(user.Id);
                data.LinkPath = "/RegisterLogin/Login";
                return data;
            }

            if (entry.State == EntityState.Added)
            {
                var key = actorId == null ? "UserRegistered" : "UserCreated";
                list.Add(() => ForUser(key));
                return;
            }

            if (entry.State == EntityState.Deleted)
            {
                if (actorId == null) return; // a registration rolled back because its mail could not be sent
                var data = make("UserDeleted");
                data.Values["UserFullName"] = user.FullName;
                data.Values["UserName"] = user.UserName;
                data.Values["UserEmail"] = user.Email;
                list.Add(() => data);
                return;
            }

            if (Original<DateTime?>(entry, nameof(User.VerifiedAt)) == null && user.VerifiedAt != null)
            {
                list.Add(() => ForUser("UserVerified"));
            }

            if (Changed(entry, nameof(User.UStatus)))
            {
                var key = user.UStatus ? "UserActivated" : "UserDeactivated";
                list.Add(() => ForUser(key));
            }

            if (Changed(entry, nameof(User.Department)) || Changed(entry, nameof(User.UType)) || Changed(entry, nameof(User.UCatagory)))
            {
                var oldRole = NotificationRole.Of(new User
                {
                    UCatagory = Original<bool>(entry, nameof(User.UCatagory)),
                    UType = Original<bool>(entry, nameof(User.UType)),
                    Department = Original<string>(entry, nameof(User.Department))
                });
                if (oldRole != NotificationRole.Of(user))
                {
                    list.Add(() => ForUser("UserRoleChanged"));
                }
            }

            var oldHash = Original<byte[]>(entry, nameof(User.PasswordHash));
            if (oldHash != null && user.PasswordHash != null && !oldHash.SequenceEqual(user.PasswordHash))
            {
                list.Add(() => ForUser("PasswordChanged"));
            }

            if (Changed(entry, nameof(User.PasswordResetToken)) && !string.IsNullOrEmpty(user.PasswordResetToken))
            {
                list.Add(() => ForUser("PasswordResetRequested"));
            }

            var changes = ProfileFields.Where(f => Changed(entry, f)).Select(f => f switch
            {
                nameof(User.PhonNumber) => "phone",
                nameof(User.FullName) => "name",
                nameof(User.BrokerageHouseName) => "brokerage house",
                nameof(User.EmployeeId) => "employee ID",
                nameof(User.UserName) => "user name",
                nameof(User.SupportLevel) => "support level " + SupportLevel.Code(Original<int?>(entry, nameof(User.SupportLevel))) + " -> " + SupportLevel.Code(user.SupportLevel),
                _ => f.ToLowerInvariant()
            }).ToList();
            if (changes.Count > 0)
            {
                var text = string.Join(", ", changes);
                list.Add(() =>
                {
                    var data = ForUser("UserUpdated");
                    data.Values["Changes"] = text;
                    return data;
                });
            }
        }

        private static void CaptureRating(EntityEntry entry, TicketRating rating, DbContext context, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list)
        {
            if (entry.State == EntityState.Deleted) return;

            var keys = new List<string>();
            if (entry.State == EntityState.Added)
            {
                keys.Add("TicketRated");
                if (rating.NeedsFollowUp) keys.Add("TicketRatedLow");
            }
            else
            {
                var oldStars = Original<byte>(entry, nameof(TicketRating.Stars));
                var oldSolved = Original<bool>(entry, nameof(TicketRating.FullySolved));
                if (oldStars != rating.Stars || oldSolved != rating.FullySolved)
                {
                    keys.Add("TicketRated");
                    if (rating.NeedsFollowUp && !(oldStars <= 2 || !oldSolved)) keys.Add("TicketRatedLow");
                }

                if (Original<DateTime?>(entry, nameof(TicketRating.ReviewedOn)) == null && rating.ReviewedOn != null)
                {
                    keys.Add("RatingReviewed");
                }

                if (Changed(entry, nameof(TicketRating.ClientReply)) && !string.IsNullOrWhiteSpace(rating.ClientReply))
                {
                    keys.Add("FeedbackReplied");
                }
            }

            foreach (var key in keys)
            {
                list.Add(() =>
                {
                    var issue = rating.Issue ?? context.Set<IssueTable>().Local.FirstOrDefault(i => i.IssueId == rating.IssueId)
                                ?? context.Set<IssueTable>().AsNoTracking().FirstOrDefault(i => i.IssueId == rating.IssueId);
                    if (issue == null) return null;
                    var data = ForTicket(make(key), Snapshot(issue));
                    if (rating.EngineerUserId != null) data.RelatedUserIds.Add(rating.EngineerUserId.Value);
                    data.Values["Stars"] = rating.Stars.ToString();
                    data.Values["Solved"] = rating.FullySolved ? "yes" : "no";
                    data.Values["RatingComment"] = rating.Comment;
                    data.Values["Reply"] = rating.ClientReply;
                    if (key == "FeedbackReplied")
                    {
                        // the answer is for the client who rated, not for the engineer
                        data.RelatedUserIds.Clear();
                        data.RelatedUserIds.Add(rating.RatedByUserId);
                    }
                    return data;
                });
            }
        }

        private static void CaptureAward(EntityEntry entry, EngineerAward award, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list)
        {
            string? key = null;
            if (entry.State == EntityState.Added && award.EngineerUserId != null)
            {
                key = "AwardNominated";
            }
            else if (entry.State == EntityState.Modified && Changed(entry, nameof(EngineerAward.Status))
                     && award.EngineerUserId != null && award.Status is EngineerAward.Confirmed or EngineerAward.Overridden)
            {
                key = "AwardDecided";
            }

            if (key == null) return;
            list.Add(() =>
            {
                var data = make(key);
                data.RelatedUserIds.Add(award.EngineerUserId!.Value);
                data.Values["Month"] = award.Month.ToString("MMMM yyyy");
                data.Values["EngineerName"] = award.EngineerName;
                data.Values["AwardStatus"] = award.Status;
                data.Values["Reason"] = award.Reason;
                data.Values["Score"] = award.Score?.ToString("0.#");
                data.LinkAction = "Scorecard";
                return data;
            });
        }

        private static void CaptureTodo(EntityEntry entry, Todo todo, Func<string, NotificationEventData> make,
            List<Func<NotificationEventData?>> list)
        {
            string? key = entry.State switch
            {
                EntityState.Added => "TodoCreated",
                EntityState.Modified when Changed(entry, nameof(Todo.Status)) && todo.Status == "Done" => "TodoCompleted",
                EntityState.Modified when entry.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)) => "TodoUpdated",
                _ => null
            };
            if (key == null) return;
            list.Add(() =>
            {
                var data = make(key);
                data.RelatedUserIds.Add(todo.UserId);
                data.HouseId = todo.BrokerageId;
                data.Values["TodoTitle"] = todo.Todoname;
                data.Values["TodoStatus"] = todo.Status;
                data.Values["OwnerUserId"] = todo.UserId.ToString();
                data.LinkAction = "ViewTodo";
                return data;
            });
        }

        private static void CaptureSetup(EntityEntry entry, string item, string label, string nameProperty,
            Func<string, NotificationEventData> make, List<Func<NotificationEventData?>> list)
        {
            if (entry.State == EntityState.Modified && !entry.Properties.Any(p => p.IsModified && !Equals(p.OriginalValue, p.CurrentValue)))
            {
                return;
            }

            var action = entry.State switch { EntityState.Added => "Created", EntityState.Deleted => "Deleted", _ => "Updated" };
            var verb = entry.State switch { EntityState.Added => "added", EntityState.Deleted => "deleted", _ => "changed" };
            var name = entry.Property(nameProperty).CurrentValue?.ToString() ?? entry.Property(nameProperty).OriginalValue?.ToString();
            list.Add(() =>
            {
                var data = make(item + action);
                data.Values["ItemType"] = label;
                data.Values["ItemName"] = name;
                data.Values["Action"] = verb;
                return data;
            });
        }

        /// <summary>SMS / WhatsApp: plain text, but keep the line breaks of the template.</summary>
        public static string HtmlToTextKeepLines(string? html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            if (!html.Contains('<')) return System.Net.WebUtility.HtmlDecode(html).Trim();
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<(br|/p|/div|/li)[^>]*>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text).Trim();
        }

        /// <summary>Ticket comments are HTML from the editor; messages need plain text.</summary>
        public static string HtmlToText(string? html, int max)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<(br|/p|/div|/li)[^>]*>", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, "\\s+", " ").Trim();
            return text.Length > max ? text.Substring(0, max - 1) + "…" : text;
        }
    }
}
