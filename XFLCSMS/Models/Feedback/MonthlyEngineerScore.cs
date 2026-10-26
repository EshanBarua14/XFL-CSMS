using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>Frozen scorecard row: one engineer, one month. Written once the month is closed.</summary>
    public class MonthlyEngineerScore
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>First day of the month.</summary>
        public DateTime Month { get; set; }

        public int EngineerUserId { get; set; }

        [MaxLength(200)]
        public string EngineerName { get; set; } = string.Empty;

        public bool Eligible { get; set; }
        public int? Rank { get; set; }
        public double Score { get; set; }

        // Raw measures
        public int TicketsClosed { get; set; }
        public double WeightedVolume { get; set; }
        public int RatingsCount { get; set; }
        public double? AverageStars { get; set; }
        public double? AdjustedRating { get; set; }
        public double? SlaCompliance { get; set; }
        public double? ResolutionRatio { get; set; }
        public double? MedianResolutionHours { get; set; }
        public double? ReopenRate { get; set; }
        public double? MedianFirstResponseMinutes { get; set; }
        public double? BacklogOverdueShare { get; set; }
        public int LowRatingsUnreviewed { get; set; }

        // Measures as 0-100 points
        public double RatingPoints { get; set; }
        public double SlaPoints { get; set; }
        public double ResolutionPoints { get; set; }
        public double VolumePoints { get; set; }
        public double ReopenPoints { get; set; }
        public double FirstResponsePoints { get; set; }
        public double BacklogPoints { get; set; }

        public DateTime FrozenOn { get; set; }
    }
}
