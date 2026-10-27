
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<XFLCSMS.Models.Product.Product>().HasIndex(p => p.ProductName).IsUnique();
            // deleting a product that tickets use is refused by the admin page; the database keeps the same rule
            modelBuilder.Entity<IssueTable>().HasOne(i => i.Product).WithMany(p => p.Issues).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);

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
        }


    }
}
