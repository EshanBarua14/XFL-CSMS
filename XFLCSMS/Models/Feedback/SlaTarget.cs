using System.ComponentModel.DataAnnotations;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>Service level target per ticket priority (High / Medium / Low), in calendar minutes.</summary>
    public class SlaTarget
    {
        [Key]
        [MaxLength(20)]
        public string Priority { get; set; } = string.Empty;

        /// <summary>Minutes from raising the ticket to the first response.</summary>
        [Range(1, 129600, ErrorMessage = "Enter between 1 and 129600 minutes (90 days).")]
        public int FirstResponseMinutes { get; set; }

        /// <summary>Minutes from raising the ticket to closing it.</summary>
        [Range(1, 525600, ErrorMessage = "Enter between 1 and 525600 minutes (365 days).")]
        public int ResolutionMinutes { get; set; }

        public static readonly SlaTarget[] Defaults =
        {
            new SlaTarget { Priority = "High", FirstResponseMinutes = 60, ResolutionMinutes = 480 },
            new SlaTarget { Priority = "Medium", FirstResponseMinutes = 240, ResolutionMinutes = 2880 },
            new SlaTarget { Priority = "Low", FirstResponseMinutes = 1440, ResolutionMinutes = 7200 }
        };
    }

    public static class Duration
    {
        /// <summary>Minutes as "45m", "1h 30m" or "2d 3h".</summary>
        public static string Format(double? minutes)
        {
            if (minutes == null)
            {
                return "-";
            }

            var m = (int)Math.Round(minutes.Value);
            if (m < 60)
            {
                return m + "m";
            }

            if (m < 1440)
            {
                return m / 60 + "h" + (m % 60 == 0 ? "" : " " + m % 60 + "m");
            }

            var h = m % 1440 / 60;
            return m / 1440 + "d" + (h == 0 ? "" : " " + h + "h");
        }
    }
}
