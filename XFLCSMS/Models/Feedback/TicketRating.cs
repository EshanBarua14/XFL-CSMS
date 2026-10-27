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

        [Range(1, 5)]
        public byte? ProfessionalismScore { get; set; }

        [Range(1, 5)]
        public byte? KnowledgeScore { get; set; }

        /// <summary>Customer Effort Score: how easy it was to get the issue solved, 1 very difficult to 5 very easy.</summary>
        [Range(1, 5)]
        public byte? EffortScore { get; set; }

        /// <summary>Net Promoter question: how likely the client is to recommend XFL support, 0 to 10.</summary>
        [Range(0, 10)]
        public byte? RecommendScore { get; set; }

        public bool FullySolved { get; set; } = true;

        /// <summary>Client's view of first-contact resolution: solved the first time, without chasing or explaining again.</summary>
        public bool? FirstContactResolved { get; set; }

        /// <summary>Quick feedback tags picked by the client (FeedbackTags), separated by "|".</summary>
        [MaxLength(400)]
        public string? Tags { get; set; }

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

        // Management's answer to the client, shown on the client's ticket page.
        [MaxLength(1000)]
        public string? ClientReply { get; set; }

        [MaxLength(200)]
        public string? ClientReplyBy { get; set; }
        public DateTime? ClientReplyOn { get; set; }

        // Fairness: a rating that is not about the engineer's work (e.g. about a product limitation or pricing)
        // can be left out of the scorecard by management, with a reason that stays on record.
        public bool ExcludedFromScore { get; set; }

        [MaxLength(500)]
        public string? ExcludeReason { get; set; }

        [MaxLength(200)]
        public string? ExcludedBy { get; set; }

        public IEnumerable<string> TagList => string.IsNullOrWhiteSpace(Tags) ? Enumerable.Empty<string>() : Tags.Split('|', StringSplitOptions.RemoveEmptyEntries);

        /// <summary>Needs a management follow-up: 1-2 stars, or the client says the problem is not solved.</summary>
        public bool NeedsFollowUp => Stars <= 2 || !FullySolved;
    }
}
