using System.ComponentModel.DataAnnotations;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>Service level target per ticket priority (High / Medium / Low). Hours are calendar hours.</summary>
    public class SlaTarget
    {
        [Key]
        [MaxLength(20)]
        public string Priority { get; set; } = string.Empty;

        [Range(0.25, 2000, ErrorMessage = "Enter between 0.25 and 2000 hours.")]
        public double FirstResponseHours { get; set; }

        [Range(0.25, 5000, ErrorMessage = "Enter between 0.25 and 5000 hours.")]
        public double ResolutionHours { get; set; }

        public static readonly SlaTarget[] Defaults =
        {
            new SlaTarget { Priority = "High", FirstResponseHours = 1, ResolutionHours = 8 },
            new SlaTarget { Priority = "Medium", FirstResponseHours = 4, ResolutionHours = 48 },
            new SlaTarget { Priority = "Low", FirstResponseHours = 24, ResolutionHours = 120 }
        };
    }
}
