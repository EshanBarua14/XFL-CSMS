using XFLCSMS.Models.Register;
using XFLCSMS.Models.Brocarage;

namespace XFLCSMS.Models.Admin
{
    public class ReportView
    {
        public ICollection<string>? EmployeeNames { get; set; }
        public ICollection<Brokerage>? brocarages { get; set; }
        public List<XFLCSMS.Models.Product.Product> Products { get; set; } = new();
        public Search? search { get; set; }
        public HeaderInfo? HeaderInfo { get; set; }
        public SESearch? SESearch { get; set; }

        public MakerSearch? MakerSearch { get; set; }

        public IEnumerable<XFLCSMS.Models.Issue.IssueTable>? Issues { get; set; }
    }
}
