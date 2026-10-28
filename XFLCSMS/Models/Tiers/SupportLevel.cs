using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Tiers
{
    /// <summary>
    /// One support tier, L1 to L5 (ITIL style functional escalation). Every Support Engineer works at a level, every
    /// ticket sits at a level, and a ticket moves up (escalation) or down (handed back) with a recorded reason.
    /// The administrator renames the levels and sets how long a ticket may stay at a level (Admin > Support Levels).
    /// </summary>
    public class SupportLevel
    {
        public const int Min = 1;
        public const int Max = 5;

        /// <summary>1 to 5 (shown as L1 to L5).</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Range(Min, Max)]
        public int Level { get; set; }

        [Required]
        [MaxLength(60)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(400)]
        public string? Description { get; set; }

        /// <summary>
        /// Hours an open ticket may stay at this level before it is flagged "escalation due" (and the managers are told).
        /// 0 = no limit.
        /// </summary>
        [Range(0, 8760, ErrorMessage = "Enter between 0 and 8760 hours (0 = no limit).")]
        public int MaxHoursAtLevel { get; set; }

        public static string Code(int? level) => level is >= Min and <= Max ? "L" + level : "–";

        public static int Clamp(int? level) => Math.Clamp(level ?? Min, Min, Max);

        public static readonly SupportLevel[] Defaults =
        {
            new SupportLevel { Level = 1, Name = "Service Desk", MaxHoursAtLevel = 4,
                Description = "First contact: logs and classifies the ticket, answers how-to questions, applies known fixes and workarounds." },
            new SupportLevel { Level = 2, Name = "Technical Support", MaxHoursAtLevel = 24,
                Description = "Deeper troubleshooting: configuration, accounts and access, logs, reproducing the problem." },
            new SupportLevel { Level = 3, Name = "Product Specialist", MaxHoursAtLevel = 72,
                Description = "Expert analysis of the product: data fixes, integrations, root cause of recurring problems." },
            new SupportLevel { Level = 4, Name = "Development", MaxHoursAtLevel = 240,
                Description = "Engineering: bug fixes and changes to the code, followed through the development stages to deployment." },
            new SupportLevel { Level = 5, Name = "Architecture & Vendors", MaxHoursAtLevel = 0,
                Description = "Architects, infrastructure and third parties (exchange, depository, vendors) for problems outside XFL's code." }
        };
    }

    /// <summary>One move of a ticket between levels (or to another engineer at the same level): who, when, why.</summary>
    public class TicketEscalation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int IssueId { get; set; }

        public int FromLevel { get; set; }
        public int ToLevel { get; set; }

        public int? FromEngineerUserId { get; set; }
        [MaxLength(200)]
        public string? FromEngineerName { get; set; }

        public int? ToEngineerUserId { get; set; }
        [MaxLength(200)]
        public string? ToEngineerName { get; set; }

        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        public int? ByUserId { get; set; }
        [MaxLength(200)]
        public string? ByName { get; set; }

        public DateTime On { get; set; }

        /// <summary>Escalated / Handed back / Reassigned.</summary>
        [NotMapped]
        public string Kind => ToLevel > FromLevel ? "Escalated" : ToLevel < FromLevel ? "Handed back" : "Reassigned";
    }
}
