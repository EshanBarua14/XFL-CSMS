using System.ComponentModel.DataAnnotations;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>The client's rating form.</summary>
    public class RatingForm
    {
        public int IssueId { get; set; }
        public string? TicketNumber { get; set; }
        public string? TicketTitle { get; set; }
        public string? EngineerName { get; set; }
        public DateTime? ClosedOn { get; set; }

        [Range(1, 5, ErrorMessage = "Please choose from 1 to 5 stars.")]
        public int Stars { get; set; }

        [Range(1, 5)]
        public int? SpeedScore { get; set; }

        [Range(1, 5)]
        public int? CommunicationScore { get; set; }

        [Range(1, 5)]
        public int? QualityScore { get; set; }

        public bool FullySolved { get; set; } = true;

        [MaxLength(1000, ErrorMessage = "Please keep the feedback under 1000 characters.")]
        public string? Comment { get; set; }

        public bool IsEdit { get; set; }
    }

    /// <summary>What the client may do with the rating of one ticket.</summary>
    public class RatingState
    {
        public bool IsClosed { get; set; }
        public TicketRating? Rating { get; set; }
        public bool CanRate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanReopen { get; set; }
        public DateTime? RateUntil { get; set; }
    }

    public class FeedbackRow
    {
        public TicketRating Rating { get; set; } = new();
        public string TicketNumber { get; set; } = string.Empty;
        public string? TicketTitle { get; set; }
        public string? Priority { get; set; }
        public string HouseName { get; set; } = string.Empty;
        public string RatedByName { get; set; } = string.Empty;
    }

    public class EngineerFeedbackSummary
    {
        public int? EngineerUserId { get; set; }
        public string EngineerName { get; set; } = string.Empty;
        public int Ratings { get; set; }
        public double AverageStars { get; set; }
        public int FourOrFive { get; set; }
        public int OneOrTwo { get; set; }
        public int ClosedTickets { get; set; }
        public int ResponseRate => ClosedTickets == 0 ? 0 : (int)Math.Round(100.0 * Ratings / ClosedTickets);
    }

    public class ServiceFeedbackView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? EngineerId { get; set; }
        public int? Stars { get; set; }
        public int? HouseId { get; set; }
        public bool FollowUp { get; set; }

        public List<(int Id, string Name)> Engineers { get; set; } = new();
        public List<(int Id, string Name)> Houses { get; set; } = new();

        public List<FeedbackRow> Rows { get; set; } = new();
        public List<EngineerFeedbackSummary> Summary { get; set; } = new();
        public int UnreviewedLow { get; set; }
    }

    /// <summary>Rating panel on the staff ticket page (management only).</summary>
    public class RatingPanelView
    {
        public string Controller { get; set; } = "Admin";
        public bool IsClosed { get; set; }
        public TicketRating? Rating { get; set; }
        public DateTime? RateUntil { get; set; }
        public List<TicketStatusHistory> History { get; set; } = new();
    }
}
