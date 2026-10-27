using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using XFLCSMS.Models.Issue;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>
    /// One row per status change of a ticket (and one for its creation). Gives reopens, first response and
    /// time in status, which the ticket row alone cannot: closing and reopening just overwrites ClosedOn.
    /// </summary>
    public class TicketStatusHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int IssueId { get; set; }
        public IssueTable? Issue { get; set; }

        [MaxLength(30)]
        public string? FromStatus { get; set; }

        [MaxLength(30)]
        public string? ToStatus { get; set; }

        /// <summary>Engineer the ticket was assigned to right after the change.</summary>
        public int? AssignedToUserId { get; set; }

        public int? ChangedByUserId { get; set; }

        [MaxLength(200)]
        public string? ChangedByName { get; set; }

        public DateTime ChangedOn { get; set; }
    }
}
