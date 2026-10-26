using XFLCSMS.Models.Affected;
using XFLCSMS.Models.Branch;
using XFLCSMS.Models.Brocarage;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Support;
using XFLCSMS.Models.Todos;
using XFLCSMS.Services;

namespace XFLCSMS.Data
{
    /// <summary>
    /// Sample data for a fresh development / test database: brokerage houses and branches, the support lookups,
    /// one account per role and tickets in every status. Switched on with "SeedDemoData": true
    /// (appsettings.Development.json) and only runs while the database has no tickets, so it never touches real data.
    /// All demo accounts use the password in "SeedDemoPassword" (default Demo@12345).
    /// </summary>
    public static class DemoDataSeeder
    {
        public static void Seed(DataContext db, IConfiguration configuration, ILogger logger)
        {
            if (!configuration.GetValue("SeedDemoData", false) || db.Issues.Any())
            {
                return;
            }

            var password = configuration["SeedDemoPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                password = "Demo@12345";
            }

            // Houses and branches. The first house (created with the first administrator) is XFL itself.
            var xfl = House(db, "Xpert Fintech Limited", "XFL");
            var alpha = House(db, "Alpha Securities Limited", "ASL");
            var beta = House(db, "Beta Capital Limited", "BCL");

            var xflHead = Branch(db, xfl, "Head Office");
            var alphaHead = Branch(db, alpha, "Motijheel");
            var alphaCtg = Branch(db, alpha, "Chattogram");
            var betaHead = Branch(db, beta, "Gulshan");

            // Support lookups (names: letters only, 4 to 100 characters - the rules of the admin forms).
            var types = Lookups(db.SupportTypes, x => x.SType, new[] { "Software", "Hardware", "Network" },
                name => new SupportType { SType = name });
            var categories = Lookups(db.SupportCatagories, x => x.SCatagory, new[] { "Trading", "Reporting", "Accounts" },
                name => new SupportCatagory { SCatagory = name });
            var subCategories = Lookups(db.SupportSubCatagories, x => x.SubCatagory, new[] { "OrderEntry", "Settlement", "Portfolio" },
                name => new SupportSubCatagory { SubCatagory = name });
            var sections = Lookups(db.AffectedSectionss, x => x.ASection, new[] { "Back Office", "Trading Terminal", "Client Portal" },
                name => new AffectedSection { ASection = name });
            db.SaveChanges();

            // One account per role, the way Admin > Create User stores them (see RegisterLoginController.Login).
            var manager = Account(db, password, "Support Manager", "manager", "Support Maneger", xflHead, staff: true);
            var engineer1 = Account(db, password, "Support Engineer One", "engineer1", "Support Engineer", xflHead, staff: true);
            var engineer2 = Account(db, password, "Support Engineer Two", "engineer2", "Support Engineer", xflHead, staff: true);
            var makerA = Account(db, password, "Alpha Maker", "maker.alpha", "Maker", alphaHead, staff: false);
            var makerA2 = Account(db, password, "Alpha Branch Maker", "maker.alpha2", "Maker", alphaCtg, staff: false);
            var makerB = Account(db, password, "Beta Maker", "maker.beta", "Maker", betaHead, staff: false);
            var inactive = Account(db, password, "Inactive Maker", "maker.inactive", "Maker", betaHead, staff: false);
            inactive.UStatus = false;
            db.SaveChanges();

            var now = DateTime.Now;
            var number = new Dictionary<int, int>();
            string Next(Brokerage house)
            {
                number[house.BrokerageId] = number.TryGetValue(house.BrokerageId, out var n) ? n + 1 : 1;
                return house.BrokerageHouseAcronym + "_" + number[house.BrokerageId].ToString("D7");
            }

            IssueTable Ticket(User owner, Brokerage house, string title, string priority, int daysAgo, int t, int c, int s, int a)
            {
                var issue = new IssueTable
                {
                    UserId = owner.Id,
                    BrokerageId = house.BrokerageId,
                    TDate = now.AddDays(-daysAgo),
                    TNumber = Next(house),
                    ITitle = title,
                    Priority = priority,
                    Details = "<p>" + title + " (sample ticket).</p>",
                    Comments = "Sample data",
                    SupportTypeId = types[t].SupportTypeId,
                    SupportCatagoryId = categories[c].SupportCatagoryId,
                    SupportSubCatagoryId = subCategories[s].SupportSubCatagoryId,
                    AffectedSectionId = sections[a].AffectedSectionId,
                    IStatus = "Open",
                    AssignBy = null,
                    AssignOn = null,
                    ApproveBy = null,
                    ApproveOn = null,
                    UpdatedBy = null
                };
                db.Issues.Add(issue);
                return issue;
            }

            void Assign(IssueTable issue, User engineer, string status, int daysAfter)
            {
                issue.AssignBy = engineer.FullName;
                issue.AssignOn = issue.TDate.AddDays(daysAfter);
                issue.ApproveBy = manager.FullName;
                issue.ApproveOn = issue.AssignOn;
                issue.IStatus = status;
                issue.UpdatedBy = engineer.FullName;
                issue.UpdatedOn = issue.AssignOn;
                if (status == "Close")
                {
                    issue.ClosedBy = engineer.FullName;
                    issue.ClosedOn = issue.TDate.AddDays(daysAfter + 1);
                    issue.UpdatedOn = issue.ClosedOn;
                }
            }

            // Unassigned (Open)
            Ticket(makerA, alpha, "Order entry screen freezes", "High", 0, 0, 0, 0, 1);
            Ticket(makerA2, alpha, "Branch report shows no data", "Medium", 1, 0, 1, 2, 0);
            Ticket(makerB, beta, "Client portal login slow", "Low", 2, 2, 0, 2, 2);
            // Assigned, in work
            Assign(Ticket(makerA, alpha, "Settlement file rejected", "High", 3, 0, 2, 1, 0), engineer1, "Inprogress", 0);
            Assign(Ticket(makerB, beta, "Printer not working", "Low", 5, 1, 2, 2, 0), engineer2, "Inqueue", 1);
            Assign(Ticket(makerA2, alpha, "Portfolio value mismatch", "Medium", 8, 0, 1, 2, 2), engineer1, "Open", 1);
            // Closed
            Assign(Ticket(makerA, alpha, "Password reset for trader", "Medium", 12, 0, 0, 0, 1), engineer1, "Close", 0);
            Assign(Ticket(makerB, beta, "VPN disconnects", "High", 20, 2, 0, 0, 1), engineer2, "Close", 1);
            Assign(Ticket(makerB, beta, "Day end report missing", "Medium", 40, 0, 1, 1, 0), engineer1, "Close", 2);
            // Raised by XFL staff for a house (phone call)
            Assign(Ticket(manager, alpha, "Phone request: add new user", "Low", 4, 0, 2, 2, 0), engineer2, "Inprogress", 0);

            // To-dos
            foreach (var (user, house) in new[] { (manager, xfl), (engineer1, xfl), (engineer2, xfl), (makerA, alpha), (makerB, beta) })
            {
                db.Todos.Add(new Todo { Todoname = "Check open tickets", Status = "In progress", CreatedOn = now, UserId = user.Id, BrokerageId = house.BrokerageId });
                db.Todos.Add(new Todo { Todoname = "Weekly summary", Status = "Done", CreatedOn = now.AddDays(-3), UserId = user.Id, BrokerageId = house.BrokerageId });
            }

            db.SaveChanges();
            logger.LogWarning("Seeded demo data. Demo accounts: manager, engineer1, engineer2, maker.alpha, maker.alpha2, maker.beta (inactive: maker.inactive), password {Password}.", password);
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

        private static User Account(DataContext db, string password, string fullName, string userName, string department, Branchh branch, bool staff)
        {
            var user = db.Users.FirstOrDefault(u => u.UserName == userName);
            if (user != null)
            {
                return user;
            }

            PasswordHasher.Create(password, out byte[] hash, out byte[] salt);
            user = new User
            {
                FullName = fullName,
                Email = userName + "@example.com",
                PhonNumber = "01700000000",
                Designation = department,
                Department = department,
                EmployeeId = "D-" + userName,
                UserName = userName,
                PasswordHash = hash,
                PasswordSalt = salt,
                VerifiedAt = DateTime.Now,
                BrokerageHouseName = branch.BrokerageId!.Value,
                BrokerageHouseAcronym = branch.BrokerageId!.Value,
                Branch = branch.BranchId,
                UCatagory = false,
                UType = staff,
                UStatus = true,
                Terms = true
            };
            db.Users.Add(user);
            return user;
        }
    }
}
