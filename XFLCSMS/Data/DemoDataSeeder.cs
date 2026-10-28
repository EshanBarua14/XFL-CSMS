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
using XFLCSMS.Services;

namespace XFLCSMS.Data
{
    /// <summary>
    /// Sample data for a development / test database. Switched on with "SeedDemoData": true (appsettings.Development.json),
    /// never in Production. All demo accounts use the password in "SeedDemoPassword" (default Demo@12345).
    ///  - Accounts: added on every start-up when missing (matched by user name), so an existing database gets them too:
    ///    2 admins, 2 support managers, 5 support engineers (one per level L1-L5), the XFL MD (viewer of every house)
    ///    and, for every brokerage house, 2 makers and 1 CEO / MD viewer.
    ///  - The ticket flow (every status, level, escalation, hand back, transfer, development stage, ratings, reopen,
    ///    notifications): added once, remembered with the setting "DemoData:FlowSeeded".
    /// </summary>
    public static class DemoDataSeeder
    {
        private const string FlowMarker = "DemoData:FlowSeeded";

        public static void Seed(DataContext db, IConfiguration configuration, ILogger logger)
        {
            if (!configuration.GetValue("SeedDemoData", false))
            {
                return;
            }

            var password = configuration["SeedDemoPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                password = "Demo@12345";
            }

            // Houses and branches. The first house (created with the first administrator) is XFL itself.
            var xfl = db.Brokerages.FirstOrDefault(b => b.BrokerageHouseAcronym == "XFL")
                ?? db.Brokerages.OrderBy(b => b.BrokerageId).FirstOrDefault()
                ?? House(db, "Xpert Fintech Limited", "XFL");
            var alpha = House(db, "Alpha Securities Limited", "ASL");
            var beta = House(db, "Beta Capital Limited", "BCL");

            var xflHead = db.Branchhs.OrderBy(b => b.BranchId).FirstOrDefault(b => b.BrokerageId == xfl.BrokerageId) ?? Branch(db, xfl, "Head Office");
            var alphaHead = Branch(db, alpha, "Motijheel");
            var alphaCtg = Branch(db, alpha, "Chattogram");
            var betaHead = Branch(db, beta, "Gulshan");

            // ---- accounts ------------------------------------------------------------------------------------
            var created = new List<string>();
            User Acc(string fullName, string userName, string department, Branchh branch, bool staff, bool admin = false, int? level = null, string? designation = null)
            {
                var user = Account(db, password, fullName, userName, department, branch, staff, admin, level, designation, out var isNew);
                if (isNew) { created.Add(userName); }
                return user;
            }

            Acc("Second Administrator", "admin2", "Maker", xflHead, staff: true, admin: true, designation: "Administrator");

            var manager = Acc("Support Manager", "manager", "Support Maneger", xflHead, staff: true);
            var manager2 = Acc("Nadia Rahman", "manager2", "Support Maneger", xflHead, staff: true, designation: "Support Manager");

            var engineers = new[]
            {
                Acc("Support Engineer One", "engineer1", "Support Engineer", xflHead, staff: true, level: 1),
                Acc("Support Engineer Two", "engineer2", "Support Engineer", xflHead, staff: true, level: 2),
                Acc("Support Engineer Three", "engineer3", "Support Engineer", xflHead, staff: true, level: 3),
                Acc("Support Engineer Four", "engineer4", "Support Engineer", xflHead, staff: true, level: 4),
                Acc("Support Engineer Five", "engineer5", "Support Engineer", xflHead, staff: true, level: 5)
            };

            Acc("XFL Managing Director", "md.xfl", SessionAuthorizeAttribute.ViewerDepartment, xflHead, staff: true, designation: "Managing Director");

            var makerA = Acc("Alpha Maker", "maker.alpha", "Maker", alphaHead, staff: false);
            var makerA2 = Acc("Alpha Branch Maker", "maker.alpha2", "Maker", alphaCtg, staff: false);
            Acc("Alpha CEO", "md.alpha", SessionAuthorizeAttribute.ViewerDepartment, alphaHead, staff: false, designation: "Chief Executive Officer");
            var makerB = Acc("Beta Maker", "maker.beta", "Maker", betaHead, staff: false);
            var makerB2 = Acc("Beta Second Maker", "maker.beta2", "Maker", betaHead, staff: false);
            Acc("Beta Managing Director", "md.beta", SessionAuthorizeAttribute.ViewerDepartment, betaHead, staff: false, designation: "Managing Director");

            var inactive = Acc("Inactive Maker", "maker.inactive", "Maker", betaHead, staff: false);
            if (created.Contains("maker.inactive")) { inactive.UStatus = false; }

            // every other house of the database: 2 makers and the CEO / MD
            var demoHouses = new[] { xfl.BrokerageId, alpha.BrokerageId, beta.BrokerageId };
            foreach (var house in db.Brokerages.Where(b => !demoHouses.Contains(b.BrokerageId)).OrderBy(b => b.BrokerageId).ToList())
            {
                var branch = db.Branchhs.OrderBy(b => b.BranchId).FirstOrDefault(b => b.BrokerageId == house.BrokerageId) ?? Branch(db, house, "Head Office");
                var tag = new string((house.BrokerageHouseAcronym ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
                if (tag.Length == 0) { tag = "house" + house.BrokerageId; }
                var name = house.BrokerageHouseAcronym ?? house.BrokerageHouseName;
                Acc(name + " Maker", "maker." + tag, "Maker", branch, staff: false);
                Acc(name + " Second Maker", "maker." + tag + "2", "Maker", branch, staff: false);
                Acc(name + " CEO / MD", "md." + tag, SessionAuthorizeAttribute.ViewerDepartment, branch, staff: false, designation: "CEO / Managing Director");
            }

            db.SaveChanges();
            if (created.Count > 0)
            {
                logger.LogWarning("Added demo accounts {Accounts}, password {Password}.", string.Join(", ", created), password);
            }

            if (db.NotificationSettings.Any(s => s.Key == FlowMarker))
            {
                return;
            }

            // ---- lookups (names: letters only, 4 to 100 characters - the rules of the admin forms) -----------
            var types = Lookups(db.SupportTypes, x => x.SType, new[] { "Software", "Hardware", "Network" },
                name => new SupportType { SType = name });
            var categories = Lookups(db.SupportCatagories, x => x.SCatagory, new[] { "Trading", "Reporting", "Accounts" },
                name => new SupportCatagory { SCatagory = name });
            var subCategories = Lookups(db.SupportSubCatagories, x => x.SubCatagory, new[] { "OrderEntry", "Settlement", "Portfolio" },
                name => new SupportSubCatagory { SubCatagory = name });
            var sections = Lookups(db.AffectedSectionss, x => x.ASection, new[] { "Back Office", "Trading Terminal", "Client Portal" },
                name => new AffectedSection { ASection = name });
            db.SaveChanges();
            var products = db.Products.OrderBy(p => p.SortOrder).Select(p => p.ProductId).ToList();

            new Flow(db, manager, manager2, engineers, types, categories, subCategories, sections, products)
                .Run(alpha, beta, makerA, makerA2, makerB, makerB2);

            // To-dos
            var now = DateTime.Now;
            foreach (var (user, house) in new[] { (manager, xfl), (engineers[0], xfl), (engineers[1], xfl), (makerA, alpha), (makerB, beta) })
            {
                db.Todos.Add(new Todo { Todoname = "Check open tickets", Status = "In progress", CreatedOn = now, UserId = user.Id, BrokerageId = house.BrokerageId });
                db.Todos.Add(new Todo { Todoname = "Weekly summary", Status = "Done", CreatedOn = now.AddDays(-3), UserId = user.Id, BrokerageId = house.BrokerageId });
            }

            db.NotificationSettings.Add(new NotificationSetting { Key = FlowMarker, Value = now.ToString("s"), UpdatedOn = now });
            db.SaveChanges();
            logger.LogWarning("Seeded the demo ticket flow (all statuses, levels L1-L5, development stages, ratings, notifications).");
        }

        /// <summary>Tickets that walk through the whole workflow, with the history rows the real pages write.</summary>
        private sealed class Flow
        {
            private readonly DataContext _db;
            private readonly User _manager, _manager2;
            private readonly User[] _eng;
            private readonly List<SupportType> _types;
            private readonly List<SupportCatagory> _categories;
            private readonly List<SupportSubCatagory> _subCategories;
            private readonly List<AffectedSection> _sections;
            private readonly List<int> _products;
            private readonly DateTime _now = DateTime.Now;
            private int _pick;

            public Flow(DataContext db, User manager, User manager2, User[] engineers, List<SupportType> types, List<SupportCatagory> categories,
                List<SupportSubCatagory> subCategories, List<AffectedSection> sections, List<int> products)
            {
                _db = db; _manager = manager; _manager2 = manager2; _eng = engineers;
                _types = types; _categories = categories; _subCategories = subCategories; _sections = sections; _products = products;
            }

            private User L(int level) => _eng[level - 1];

            public void Run(Brokerage alpha, Brokerage beta, User makerA, User makerA2, User makerB, User makerB2)
            {
                // -- waiting at L1 (Open, nobody assigned)
                var t = Ticket(makerA, alpha, "Order entry screen freezes", "High", 0.1);
                Notify(_manager, "TicketCreated", "New ticket " + t.TNumber, makerA.FullName + " raised \"" + t.ITitle + "\".", t, 0.1, read: false);
                Ticket(makerA2, alpha, "Branch report shows no data", "Medium", 1);
                Ticket(makerB, beta, "Client portal login slow", "Low", 2);
                t = Ticket(makerB2, beta, "Margin call alert not sent", "High", 6); // overdue: high priority, unanswered for days
                Notify(_manager, "EscalationDue", "Overdue: " + t.TNumber, "\"" + t.ITitle + "\" has waited 6 days without an engineer.", t, 1, read: false);

                // -- L1: in queue and in progress
                t = Ticket(makerB2, beta, "Printer not working", "Low", 3);
                Assign(t, L(1), 2.9); Status(t, "Inqueue", L(1), 2.8);
                t = Ticket(makerA, alpha, "Settlement file rejected", "High", 2);
                Assign(t, L(1), 1.9); Status(t, "Inprogress", L(1), 1.8);
                Comment(t, L(1), "Checked the file format with the client, fixing the header row.", 1.5);

                // -- escalated L1 -> L2, in progress at L2
                t = Ticket(makerA2, alpha, "Portfolio value mismatch", "Medium", 5);
                Assign(t, L(1), 4.9); Status(t, "Inprogress", L(1), 4.8);
                Escalate(t, 2, L(2), L(1), "Needs a database check of the price feed, beyond L1.", 4);

                // -- escalated to L2 but still waiting in the L2 queue
                t = Ticket(makerA2, alpha, "Bulk BO upload fails at row 500", "Medium", 4);
                Assign(t, L(1), 3.9); Status(t, "Inprogress", L(1), 3.8);
                Escalate(t, 2, null, L(1), "Known L2 issue with large uploads.", 3.5);

                // -- transfer: reassigned at the same level (L2 -> another engineer working at L2+)
                t = Ticket(makerB, beta, "Trade confirmation e-mails delayed", "Medium", 6);
                Assign(t, L(2), 5.8); Status(t, "Inprogress", L(2), 5.7);
                Escalate(t, 2, L(3), _manager, "Transferred: Support Engineer Two is on leave this week.", 5);

                // -- escalated to L3, development analysis started
                t = Ticket(makerB, beta, "Day-end process takes 3 hours", "High", 9);
                Assign(t, L(1), 8.9); Status(t, "Inprogress", L(1), 8.8);
                Escalate(t, 2, L(2), L(1), "Performance issue, not a configuration problem.", 8.5);
                Escalate(t, 3, L(3), L(2), "Needs the product specialist for the day-end job.", 7);
                Dev(t, DevStages.Analysis, L(3), 14, null, "Profiling the day-end job.", 6);

                // -- handed back: L3 -> L2 by the manager
                t = Ticket(makerB, beta, "Cannot export client ledger to Excel", "Low", 7);
                Assign(t, L(2), 6.9); Status(t, "Inprogress", L(2), 6.8);
                Escalate(t, 3, L(3), L(2), "Looks like a product bug.", 6);
                Escalate(t, 2, L(2), _manager, "Handed back: it is the known Excel limit, L2 can give the workaround.", 5);

                // -- L4 development pipeline, one ticket per stage
                var stages = new[]
                {
                    (DevStages.Development, makerA, alpha, "Add T+2 settlement report", 15.0, "DEV-1042"),
                    (DevStages.CodeReview, makerB2, beta, "IPO application export format", 18.0, "DEV-1051"),
                    (DevStages.Testing, makerA2, alpha, "Commission calculation for odd lots", 20.0, "DEV-1060"),
                    (DevStages.ReadyToDeploy, makerB, beta, "New circuit breaker rule from DSE", 22.0, "DEV-1066")
                };
                foreach (var (stage, owner, house, title, days, reference) in stages)
                {
                    t = Ticket(owner, house, title, "Medium", days);
                    Assign(t, L(1), days - 0.1); Status(t, "Inprogress", L(1), days - 0.2);
                    Escalate(t, 2, L(2), L(1), "Change request, not a fault.", days - 1);
                    Escalate(t, 4, L(4), _manager, "Needs a code change by the development team.", days - 2);
                    var walked = 0.0;
                    foreach (var step in DevStages.Pipeline.Select(p => p.Value).TakeWhile(s => s != stage).Append(stage))
                    {
                        Dev(t, step, L(4), 10, reference, step == stage ? "Current stage." : null, days - 3 - walked);
                        walked += (days - 4) / 5;
                    }
                }

                // -- L5 (vendor), development on hold
                t = Ticket(makerA, alpha, "Exchange FIX gateway drops sessions", "High", 25);
                Assign(t, L(2), 24.9); Status(t, "Inprogress", L(2), 24.8);
                Escalate(t, 3, L(3), L(2), "Network and gateway logs point to the exchange side.", 24);
                Escalate(t, 5, L(5), L(3), "Vendor (exchange) involvement needed.", 22);
                Dev(t, DevStages.Analysis, L(5), 30, "VND-77", null, 21);
                Dev(t, DevStages.OnHold, L(5), null, "VND-77", "Waiting for the exchange's fix release.", 15);
                Notify(L(5), "TicketEscalated", t.TNumber + " escalated to L5", "\"" + t.ITitle + "\" was escalated to you.", t, 22, read: true);

                // -- development deployed, ticket closed and rated
                t = Ticket(makerB2, beta, "Tax certificate shows wrong year", "Medium", 30);
                Assign(t, L(1), 29.9); Status(t, "Inprogress", L(1), 29.8);
                Escalate(t, 4, L(4), _manager, "Report template bug.", 29);
                foreach (var (step, i) in DevStages.Pipeline.Select((p, i) => (p.Value, i)))
                {
                    Dev(t, step, L(4), null, "DEV-1012", step == DevStages.Deployed ? "Released in 4.8.2." : null, 28 - i * 3);
                }
                Close(t, L(4), 12);
                Rate(t, makerB2, 5, 11, "Fixed properly, thank you.", tags: new[] { "Knowledgeable", "Kept me updated" }, recommend: 10);

                // -- closed tickets with every kind of rating (this month and last month, for the scorecard)
                t = Closed(makerA, alpha, "Password reset for trader", "Medium", 12, L(1), 0.2);
                Rate(t, makerA, 5, 11.5, "Very quick.", tags: new[] { "Fast response", "Solved first time" }, recommend: 9);
                t = Closed(makerB, beta, "VPN disconnects", "High", 20, L(2), 1);
                Rate(t, makerB, 4, 18.5, null, tags: new[] { "Clear communication" }, recommend: 8);
                t = Closed(makerA2, alpha, "Wrong closing price shown", "High", 16, L(2), 0.5);
                Rate(t, makerA2, 3, 15, "Solved but took a while.", tags: new[] { "Had to follow up" }, recommend: 6);
                t = Closed(makerB2, beta, "Client statement missing trades", "Medium", 10, L(3), 2);
                Rate(t, makerB2, 2, 7.5, "Had to explain the problem twice.", tags: new[] { "Slow response", "Unclear explanation" }, recommend: 4, solved: false);
                Notify(_manager, "TicketRatedLow", "Low rating on " + t.TNumber, makerB2.FullName + " gave 2 stars.", t, 7.5, read: false);
                t = Closed(makerA, alpha, "Cannot add new trader terminal", "Low", 9, L(1), 1);
                var low = Rate(t, makerA, 1, 7, "Problem came back the next day.", tags: new[] { "Problem came back", "Wrong solution" }, recommend: 2, solved: false);
                low.ReviewedBy = _manager.FullName; low.ReviewedOn = _now.AddDays(-6); low.ReviewNote = "Called the client, the terminal licence was not renewed. Fixed.";
                low.ClientReply = "Thanks for calling back, works now."; low.ClientReplyBy = makerA.FullName; low.ClientReplyOn = _now.AddDays(-5);
                t = Closed(makerB, beta, "Network outage at branch (ISP)", "High", 14, L(1), 0.5);
                var excluded = Rate(t, makerB, 1, 13, "Whole day without trading.", tags: new[] { "Slow response" }, recommend: 3, solved: true);
                excluded.ExcludedFromScore = true; excluded.ExcludeReason = "ISP outage, outside XFL control."; excluded.ExcludedBy = _manager2.FullName;
                foreach (var (owner, house, title, days, level) in new[]
                {
                    (makerA, alpha, "Day end report missing", 40.0, 1), (makerB, beta, "Bank reconciliation file error", 45.0, 2),
                    (makerA2, alpha, "User locked out of back office", 35.0, 1), (makerB2, beta, "Dividend posting wrong", 50.0, 3),
                    (makerA, alpha, "Slow trading terminal", 38.0, 2), (makerB, beta, "SMS alerts not delivered", 42.0, 1)
                })
                {
                    t = Closed(owner, house, title, "Medium", days, L(level), 1);
                    Rate(t, owner, (byte)(level == 3 ? 4 : 5), days - 2, null, tags: new[] { "Fast response" }, recommend: 9);
                }

                // -- closed, waiting for the client's rating
                t = Closed(makerA, alpha, "Change printer for contract notes", "Low", 2, L(1), 0.5);
                Notify(makerA, "TicketClosed", t.TNumber + " closed", "Please rate the support you received.", t, 1.5, read: false);
                t = Closed(makerB, beta, "Add new branch to back office", "Low", 3, L(2), 1);

                // -- reopened by the client
                t = Closed(makerA2, alpha, "Corporate action not applied", "Medium", 8, L(1), 1);
                Status(t, "Open", makerA2, 5, "Reopened by the client: not solved.");
                Notify(L(1), "TicketReopened", t.TNumber + " reopened", makerA2.FullName + " says the problem is not solved.", t, 5, read: false);

                // -- raised by XFL staff for a house (phone call)
                t = Ticket(_manager, alpha, "Phone request: add new user", "Low", 4);
                Assign(t, L(2), 3.9); Status(t, "Inprogress", L(2), 3.8);

                // delivery log: a few e-mails, one failed
                foreach (var (user, subject, status, days) in new[] { (makerA, "Your ticket was assigned", NotificationStatus.Sent, 1.9), (makerB, "Your ticket was closed", NotificationStatus.Sent, 3.0), (makerB2, "Your ticket was assigned", NotificationStatus.Failed, 2.8) })
                {
                    _db.NotificationMessages.Add(new NotificationMessage
                    {
                        EventKey = "TicketAssigned", Channel = NotificationChannel.Email, UserId = user.Id, RecipientName = user.FullName, Address = user.Email,
                        Subject = subject, Body = "Sample e-mail.", Status = status, Attempts = status == NotificationStatus.Failed ? 3 : 1,
                        CreatedOn = _now.AddDays(-days), NextAttemptOn = _now.AddDays(-days), SentOn = status == NotificationStatus.Sent ? _now.AddDays(-days) : null,
                        LastError = status == NotificationStatus.Failed ? "SMTP: mailbox unavailable (sample)" : null
                    });
                }

                _db.SaveChanges();
            }

            private IssueTable Ticket(User owner, Brokerage house, string title, string priority, double daysAgo)
            {
                var n = _pick++;
                var issue = new IssueTable
                {
                    UserId = owner.Id,
                    BrokerageId = house.BrokerageId,
                    TDate = _now.AddDays(-daysAgo),
                    TNumber = NextNumber(house),
                    ITitle = title,
                    Priority = priority,
                    Details = "<p>" + title + " (sample ticket).</p>",
                    Comments = "Sample data",
                    ProductId = _products.Count > 0 ? _products[n % _products.Count] : null,
                    SupportTypeId = _types[n % _types.Count].SupportTypeId,
                    SupportCatagoryId = _categories[n % _categories.Count].SupportCatagoryId,
                    SupportSubCatagoryId = _subCategories[n % _subCategories.Count].SupportSubCatagoryId,
                    AffectedSectionId = _sections[n % _sections.Count].AffectedSectionId,
                    IStatus = "Open",
                    AssignBy = null,
                    ApproveBy = null,
                    UpdatedBy = null,
                    SupportLevel = 1
                };
                _db.Issues.Add(issue);
                _db.SaveChanges();
                History(issue, null, "Open", owner, issue.TDate);
                return issue;
            }

            private string NextNumber(Brokerage house)
            {
                var prefix = house.BrokerageHouseAcronym + "_";
                var max = _db.Issues.Where(i => i.TNumber.StartsWith(prefix)).Select(i => i.TNumber).AsEnumerable()
                    .Select(x => int.TryParse(x.Substring(prefix.Length), out var v) ? v : 0).DefaultIfEmpty(0).Max();
                return prefix + (max + 1).ToString("D7");
            }

            private IssueTable Closed(User owner, Brokerage house, string title, string priority, double daysAgo, User engineer, double daysToFix)
            {
                var t = Ticket(owner, house, title, priority, daysAgo);
                if (engineer.SupportLevel > 1) { t.SupportLevel = engineer.SupportLevel ?? 1; t.LevelSince = t.TDate; }
                Assign(t, engineer, daysAgo - 0.05);
                Status(t, "Inprogress", engineer, daysAgo - 0.1);
                Close(t, engineer, Math.Max(0.01, daysAgo - 0.1 - daysToFix));
                return t;
            }

            private void Assign(IssueTable t, User engineer, double daysAgo)
            {
                var at = _now.AddDays(-daysAgo);
                t.AssignedToUserId = engineer.Id;
                t.AssignBy = engineer.FullName;
                t.AssignOn = at;
                t.ApproveBy = _manager.FullName;
                t.ApproveOn = at;
                t.FirstResponseOn ??= at;
                t.UpdatedBy = _manager.FullName;
                t.UpdatedOn = at;
                var owner = _db.Users.Local.FirstOrDefault(u => u.Id == t.UserId);
                Notify(engineer, "TicketAssigned", t.TNumber + " assigned to you", "\"" + t.ITitle + "\"", t, daysAgo, read: daysAgo > 2);
                if (owner != null && !owner.UType)
                {
                    Notify(owner, "TicketAssigned", t.TNumber + " assigned", engineer.FullName + " is working on your ticket.", t, daysAgo, read: daysAgo > 3);
                }
            }

            private void Status(IssueTable t, string to, User by, double daysAgo, string? comment = null)
            {
                var at = _now.AddDays(-daysAgo);
                History(t, t.IStatus, to, by, at);
                t.IStatus = to;
                t.UpdatedBy = by.FullName;
                t.UpdatedOn = at;
                if (to == "Open") { t.ClosedOn = null; t.ClosedBy = null; }
                if (comment != null) { t.Comments = comment; }
                _db.SaveChanges();
            }

            private void Comment(IssueTable t, User by, string text, double daysAgo)
            {
                t.Comments = text;
                t.UpdatedBy = by.FullName;
                t.UpdatedOn = _now.AddDays(-daysAgo);
                _db.SaveChanges();
            }

            private void Close(IssueTable t, User engineer, double daysAgo)
            {
                Status(t, "Close", engineer, daysAgo);
                t.ClosedBy = engineer.FullName;
                t.ClosedOn = _now.AddDays(-daysAgo);
                _db.SaveChanges();
            }

            private void History(IssueTable t, string? from, string to, User by, DateTime at)
            {
                _db.TicketStatusHistories.Add(new TicketStatusHistory
                {
                    IssueId = t.IssueId, FromStatus = from, ToStatus = to, AssignedToUserId = t.AssignedToUserId,
                    ChangedByUserId = by.Id, ChangedByName = by.FullName, ChangedOn = at
                });
            }

            private void Escalate(IssueTable t, int toLevel, User? toEngineer, User by, string reason, double daysAgo)
            {
                var at = _now.AddDays(-daysAgo);
                var fromEngineer = t.AssignedToUserId == null ? null : _db.Users.Local.FirstOrDefault(u => u.Id == t.AssignedToUserId);
                _db.TicketEscalations.Add(new TicketEscalation
                {
                    IssueId = t.IssueId, FromLevel = t.SupportLevel, ToLevel = toLevel,
                    FromEngineerUserId = fromEngineer?.Id, FromEngineerName = fromEngineer?.FullName,
                    ToEngineerUserId = toEngineer?.Id, ToEngineerName = toEngineer?.FullName,
                    Reason = reason, ByUserId = by.Id, ByName = by.FullName, On = at
                });
                t.SupportLevel = toLevel;
                t.LevelSince = at;
                if (toEngineer == null)
                {
                    t.AssignedToUserId = null; t.AssignBy = null; t.AssignOn = null;
                    if (t.IStatus != "Open") { Status(t, "Open", by, daysAgo); }
                }
                else
                {
                    t.AssignedToUserId = toEngineer.Id; t.AssignBy = toEngineer.FullName; t.AssignOn = at;
                    Notify(toEngineer, "TicketEscalated", t.TNumber + " moved to " + SupportLevel.Code(toLevel), reason, t, daysAgo, read: daysAgo > 3);
                }

                _db.SaveChanges();
            }

            private void Dev(IssueTable t, string stage, User owner, int? etaDays, string? reference, string? note, double daysAgo)
            {
                var at = _now.AddDays(-daysAgo);
                var eta = etaDays == null ? (DateTime?)null : _now.AddDays(etaDays.Value).Date;
                _db.TicketDevUpdates.Add(new TicketDevUpdate
                {
                    IssueId = t.IssueId, FromStage = t.DevStage, Stage = stage, OwnerUserId = owner.Id, OwnerName = owner.FullName,
                    Eta = eta, Reference = reference, Note = note, VisibleToClient = true, ByUserId = owner.Id, ByName = owner.FullName, On = at
                });
                t.DevStage = stage;
                t.DevStageSince = at;
                t.DevOwnerUserId = owner.Id;
                t.DevEta = eta;
                t.DevReference = reference;
                _db.SaveChanges();
            }

            private TicketRating Rate(IssueTable t, User maker, byte stars, double daysAgo, string? comment, string[] tags, byte recommend, bool solved = true)
            {
                var score = (byte)Math.Clamp((int)stars, 1, 5);
                var rating = new TicketRating
                {
                    IssueId = t.IssueId, RatedByUserId = maker.Id, EngineerUserId = t.AssignedToUserId, EngineerName = t.AssignBy,
                    Stars = stars, SpeedScore = score, CommunicationScore = score, QualityScore = score, ProfessionalismScore = (byte)Math.Min(5, score + 1),
                    KnowledgeScore = score, EffortScore = score, RecommendScore = recommend, FullySolved = solved, FirstContactResolved = solved && stars >= 4,
                    Tags = string.Join("|", tags), Comment = comment, CreatedOn = _now.AddDays(-daysAgo)
                };
                _db.TicketRatings.Add(rating);
                if (t.AssignedToUserId is int engineerId && _db.Users.Local.FirstOrDefault(u => u.Id == engineerId) is User engineer)
                {
                    Notify(_manager, "TicketRated", t.TNumber + " rated " + stars + " stars", maker.FullName + " rated " + engineer.FullName + ".", t, daysAgo, read: daysAgo > 5);
                }

                _db.SaveChanges();
                return rating;
            }

            private void Notify(User to, string eventKey, string subject, string body, IssueTable t, double daysAgo, bool read)
            {
                var at = _now.AddDays(-daysAgo);
                _db.NotificationMessages.Add(new NotificationMessage
                {
                    EventKey = eventKey, Channel = NotificationChannel.InApp, UserId = to.Id, RecipientName = to.FullName,
                    Subject = subject, Body = body, Url = "/" + NotificationRole.Controller(NotificationRole.Of(to)) + "/TicketView/" + t.IssueId,
                    Status = NotificationStatus.Sent, Attempts = 1, CreatedOn = at, NextAttemptOn = at, SentOn = at, ReadOn = read ? at.AddHours(1) : null
                });
            }
        }

        private static Brokerage House(DataContext db, string name, string acronym)
        {
            var house = db.Brokerages.FirstOrDefault(b => b.BrokerageHouseAcronym == acronym);
            if (house == null)
            {
                house = new Brokerage { BrokerageHouseName = name, BrokerageHouseAcronym = acronym };
                db.Brokerages.Add(house);
                db.SaveChanges();
            }

            return house;
        }

        private static Branchh Branch(DataContext db, Brokerage house, string name)
        {
            var branch = db.Branchhs.FirstOrDefault(b => b.BrokerageId == house.BrokerageId && b.BranchName == name);
            if (branch == null)
            {
                branch = new Branchh { BranchName = name, BrokerageId = house.BrokerageId };
                db.Branchhs.Add(branch);
                db.SaveChanges();
            }

            return branch;
        }

        private static List<T> Lookups<T>(DbSet<T> set, Func<T, string> name, string[] names, Func<string, T> create) where T : class
        {
            var existing = set.ToList();
            var result = new List<T>();
            foreach (var n in names)
            {
                var item = existing.FirstOrDefault(x => string.Equals(name(x), n, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    item = create(n);
                    set.Add(item);
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>The account the way Admin > Create User stores it (see RegisterLoginController.Login). An existing
        /// engineer without a level gets the demo level, so the L1-L5 ladder is complete.</summary>
        private static User Account(DataContext db, string password, string fullName, string userName, string department, Branchh branch,
            bool staff, bool admin, int? level, string? designation, out bool isNew)
        {
            var user = db.Users.FirstOrDefault(u => u.UserName == userName);
            isNew = user == null;
            if (user != null)
            {
                if (level != null && user.SupportLevel == null && user.Department == "Support Engineer") { user.SupportLevel = level; }
                return user;
            }

            PasswordHasher.Create(password, out byte[] hash, out byte[] salt);
            user = new User
            {
                FullName = fullName,
                Email = userName + "@example.com",
                PhonNumber = "01700000000",
                Designation = designation ?? department,
                Department = department,
                EmployeeId = "D-" + userName,
                UserName = userName,
                PasswordHash = hash,
                PasswordSalt = salt,
                VerifiedAt = DateTime.Now,
                BrokerageHouseName = branch.BrokerageId!.Value,
                BrokerageHouseAcronym = branch.BrokerageId!.Value,
                Branch = branch.BranchId,
                UCatagory = admin,
                UType = staff,
                UStatus = true,
                Terms = true,
                SupportLevel = level
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }
    }
}
