
using Microsoft.EntityFrameworkCore;
using XFLCSMS.Models.Affected;
using XFLCSMS.Models.Branch;
using XFLCSMS.Models.Brocarage;
using XFLCSMS.Models.Issue;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Support;
using XFLCSMS.Models.Login;
using XFLCSMS.Models.DataTable;
using XFLCSMS.Models.Todos;
using XFLCSMS.Models.Feedback;
using XFLCSMS.Models.Notifications;
using XFLCSMS.Models.Tiers;

namespace XFLCSMS.Data
{
    public class DataContext:DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {

        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Branchh> Branchhs {  get; set; }
        public DbSet<Brokerage> Brokerages { get; set; }
        public DbSet<SupportType> SupportTypes { get; set; }
        public DbSet<SupportCatagory> SupportCatagories { get; set; }
        public DbSet<SupportSubCatagory> SupportSubCatagories { get; set; }
        public DbSet<AffectedSection> AffectedSectionss { get; set; }

        public DbSet<IssueTable> Issues { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Todo> Todos { get; set; }
        public DbSet<XFLCSMS.Models.Product.Product> Products { get; set; }

        public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();
        public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
        public DbSet<TicketRating> TicketRatings => Set<TicketRating>();
        public DbSet<MonthlyEngineerScore> MonthlyEngineerScores => Set<MonthlyEngineerScore>();
        public DbSet<EngineerAward> EngineerAwards => Set<EngineerAward>();

        public DbSet<NotificationRule> NotificationRules => Set<NotificationRule>();
        public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
        public DbSet<NotificationSetting> NotificationSettings => Set<NotificationSetting>();
        public DbSet<NotificationOptOut> NotificationOptOuts => Set<NotificationOptOut>();
        public DbSet<NotificationMessage> NotificationMessages => Set<NotificationMessage>();
        public DbSet<SlaAlert> SlaAlerts => Set<SlaAlert>();

        public DbSet<SupportLevel> SupportLevels => Set<SupportLevel>();
        public DbSet<TicketEscalation> TicketEscalations => Set<TicketEscalation>();
        public DbSet<TicketDevUpdate> TicketDevUpdates => Set<TicketDevUpdate>();
        public DbSet<AssignmentSettings> AssignmentSettings => Set<AssignmentSettings>();
        public DbSet<TicketTransferRequest> TicketTransferRequests => Set<TicketTransferRequest>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<XFLCSMS.Models.Product.Product>().HasIndex(p => p.ProductName).IsUnique();
            // deleting a product that tickets use is refused by the admin page; the database keeps the same rule
            modelBuilder.Entity<IssueTable>().HasOne(i => i.Product).WithMany(p => p.Issues).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

            // the lists, reports and dashboards filter tickets by these dates
            modelBuilder.Entity<IssueTable>().HasIndex(i => i.TDate);
            modelBuilder.Entity<IssueTable>().HasIndex(i => i.ClosedOn);

            // Users are never deleted through these links: DeleteUser refuses users that are referenced.
            modelBuilder.Entity<IssueTable>()
                .HasOne<User>().WithMany().HasForeignKey(i => i.AssignedToUserId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TicketStatusHistory>(e =>
            {
                e.HasOne(h => h.Issue).WithMany().HasForeignKey(h => h.IssueId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(h => new { h.IssueId, h.ChangedOn });
            });

            modelBuilder.Entity<TicketRating>(e =>
            {
                e.HasOne(r => r.Issue).WithMany().HasForeignKey(r => r.IssueId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.IssueId).IsUnique();
                e.HasOne<User>().WithMany().HasForeignKey(r => r.RatedByUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<User>().WithMany().HasForeignKey(r => r.EngineerUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(r => new { r.EngineerUserId, r.CreatedOn });
            });

            modelBuilder.Entity<MonthlyEngineerScore>()
                .HasIndex(s => new { s.Month, s.EngineerUserId }).IsUnique();

            modelBuilder.Entity<EngineerAward>()
                .HasIndex(a => a.Month).IsUnique();

            modelBuilder.Entity<SlaTarget>().HasData(SlaTarget.Defaults);

            modelBuilder.Entity<NotificationRule>().HasIndex(r => new { r.EventKey, r.Channel, r.Role }).IsUnique();
            modelBuilder.Entity<NotificationTemplate>().HasIndex(t => new { t.EventKey, t.Channel }).IsUnique();
            modelBuilder.Entity<NotificationOptOut>().HasIndex(o => new { o.UserId, o.EventKey, o.Channel }).IsUnique();
            modelBuilder.Entity<NotificationOptOut>().HasOne<User>().WithMany().HasForeignKey(o => o.UserId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<NotificationMessage>(e =>
            {
                e.HasIndex(m => new { m.Status, m.NextAttemptOn });
                e.HasIndex(m => new { m.UserId, m.Channel, m.ReadOn });
                e.HasIndex(m => m.CreatedOn);
                // the log keeps messages of deleted users
                e.HasOne<User>().WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<SlaAlert>().HasIndex(a => new { a.IssueId, a.EventKey }).IsUnique();
            modelBuilder.Entity<SlaAlert>().HasOne<IssueTable>().WithMany().HasForeignKey(a => a.IssueId).OnDelete(DeleteBehavior.Cascade);

            // support levels L1-L5 and development tracking
            modelBuilder.Entity<SupportLevel>().HasData(SupportLevel.Defaults);
            modelBuilder.Entity<IssueTable>().Property(i => i.SupportLevel).HasDefaultValue(1);
            modelBuilder.Entity<IssueTable>().HasIndex(i => i.SupportLevel);
            modelBuilder.Entity<IssueTable>().HasIndex(i => i.DevStage);
            modelBuilder.Entity<IssueTable>().HasOne<User>().WithMany().HasForeignKey(i => i.DevOwnerUserId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<TicketEscalation>(e =>
            {
                e.HasOne<IssueTable>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.IssueId, x.On });
                e.HasIndex(x => x.On);
            });
            modelBuilder.Entity<TicketDevUpdate>(e =>
            {
                e.HasOne<IssueTable>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.IssueId, x.On });
            });

            // ticket assignment: self-assign, auto-assign and transfer requests between engineers
            modelBuilder.Entity<AssignmentSettings>().HasData(new AssignmentSettings());
            modelBuilder.Entity<TicketTransferRequest>(e =>
            {
                e.HasOne<IssueTable>().WithMany().HasForeignKey(x => x.IssueId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne<User>().WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.IssueId, x.Status });
                e.HasIndex(x => new { x.ToUserId, x.Status });
                e.HasIndex(x => new { x.FromUserId, x.Status });
            });
        }


    }
}
