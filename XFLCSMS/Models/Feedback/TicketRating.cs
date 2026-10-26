using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using XFLCSMS.Models.Issue;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>
    /// The client's (Maker's) rating of the support received on one closed ticket.
    /// Only Admin and Support Manager may read these; engineers never see them.
    /// </summary>
    public class TicketRating
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int RatingId { get; set; }

        public int IssueId { get; set; }
        public IssueTable? Issue { get; set; }

        public int RatedByUserId { get; set; }

        /// <summary>Engineer credited with the ticket when it was closed.</summary>
        public int? EngineerUserId { get; set; }

        [MaxLength(200)]
        public string? EngineerName { get; set; }

        [Range(1, 5)]
        public byte Stars { get; set; }

        [Range(1, 5)]
        public byte? SpeedScore { get; set; }

        [Range(1, 5)]
        public byte? CommunicationScore { get; set; }

        [Range(1, 5)]
        public byte? QualityScore { get; set; }

        public bool FullySolved { get; set; } = true;

        [MaxLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }

        // Management follow-up of low ratings (internal, never shown to the client).
        [MaxLength(200)]
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedOn { get; set; }

        [MaxLength(1000)]
        public string? ReviewNote { get; set; }
    }
}
