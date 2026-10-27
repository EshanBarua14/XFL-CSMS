namespace XFLCSMS.Models.Admin
{
    public class Search
    {
        public int? BrokerageId { get; set; }
        public int? ProductId { get; set; }
        public string? Priority { get; set; }
        public string? AStatus { get; set; }
        
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set;}

        public string? EmployeeName { get; set; }



    }
}
