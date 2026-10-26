using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>Best Support Engineer of the Month.</summary>
    public class EngineerAward
    {
        public const string Nominated = "Nominated";
        public const string Confirmed = "Confirmed";
        public const string Overridden = "Overridden";
        public const string NoWinner = "No winner";

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>First day of the month.</summary>
        public DateTime Month { get; set; }

        public int? EngineerUserId { get; set; }

        [MaxLength(200)]
        public string? EngineerName { get; set; }

        public double? Score { get; set; }

        /// <summary>Top-ranked engineer, even when he did not pass the award gates.</summary>
        [MaxLength(200)]
        public string? TopRankedName { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = Nominated;

        public DateTime NominatedOn { get; set; }

        [MaxLength(200)]
        public string? DecidedBy { get; set; }
        public DateTime? DecidedOn { get; set; }

        [MaxLength(1000)]
        public string? Reason { get; set; }
    }
}
