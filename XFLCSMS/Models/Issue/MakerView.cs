using XFLCSMS.Models.Register;

namespace XFLCSMS.Models.Issue
{
    public class MakerView
    {
        public int IssueId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? AssgnOn { get; set; }
        public string? AssgnBy { get; set; }
        public DateTime? ApproveOn { get; set; }
        public string? ApproveBy { get; set; }
        public DateTime? CloseOn { get; set; }
        public int? ClosedBy { get; set; }
        public string? ClosedbyName { get; set; }
        /// <summary>Product of the ticket (name, for display) and its id (staff may change it on Edit Ticket).</summary>
        public string? Product { get; set; }
        public int? ProductId { get; set; }
        public List<XFLCSMS.Models.Product.Product>? Products { get; set; }
        public string? SupportType { get; set; }
        public string? SupportCatagory { get; set; }
        public string? SupportSubCatagory { get; set; }
        public string? AffectedSection { get; set; }

        public string? Priority { get; set; }
        public string? IssueTitle { get; set; }
        public string? TicketStatus { get; set; }
        public string? TicketDetails { get; set; }

        public string? IStatus { get; set; }

        public string? Command { get; set; } 
        public int? attachmentId { get; set; }

        public ICollection<Attachment>? Attachments { get; set; }

        public ICollection<User>? SupportEngineers { get; set; }

        



    }
}
