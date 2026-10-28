using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Tiers
{
    /// <summary>
    /// Development stages of a ticket that needs a code change, from analysis to deployment. A ticket without a stage
    /// needs no development. The stage, owner and ETA are on the ticket; every change is a row in TicketDevUpdate.
    /// </summary>
    public static class DevStages
    {
        public const string Analysis = "Analysis";
        public const string Development = "Development";
        public const string CodeReview = "CodeReview";
        public const string Testing = "Testing";
        public const string ReadyToDeploy = "ReadyToDeploy";
        public const string Deployed = "Deployed";
        public const string OnHold = "OnHold";

        /// <summary>The pipeline in order (On hold is outside it).</summary>
        public static readonly (string Value, string Label, string Icon)[] Pipeline =
        {
            (Analysis, "Analysis", "fas fa-search"),
            (Development, "Development", "fas fa-code"),
            (CodeReview, "Code review", "fas fa-code-branch"),
            (Testing, "QA / UAT", "fas fa-vial"),
            (ReadyToDeploy, "Ready to deploy", "fas fa-box"),
            (Deployed, "Deployed", "fas fa-rocket")
        };

        public static readonly (string Value, string Label, string Icon)[] All = Pipeline.Append((OnHold, "On hold", "fas fa-pause-circle")).ToArray();

        public static bool IsValid(string? stage) => All.Any(s => s.Value == stage);

        public static string Label(string? stage) => All.FirstOrDefault(s => s.Value == stage).Label ?? (string.IsNullOrEmpty(stage) ? "No development" : stage);

        public static string Icon(string? stage) => All.FirstOrDefault(s => s.Value == stage).Icon ?? "fas fa-minus";

        /// <summary>0-based place in the pipeline, -1 for On hold / none.</summary>
        public static int Index(string? stage) => Array.FindIndex(Pipeline, s => s.Value == stage);

        public static bool IsActive(string? stage) => !string.IsNullOrEmpty(stage) && stage != Deployed;
    }

    /// <summary>One development update of a ticket: stage, owner, ETA, reference and a note.</summary>
    public class TicketDevUpdate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int IssueId { get; set; }

        [MaxLength(30)]
        public string? FromStage { get; set; }

        [MaxLength(30)]
        public string Stage { get; set; } = string.Empty;

        public int? OwnerUserId { get; set; }
        [MaxLength(200)]
        public string? OwnerName { get; set; }

        public DateTime? Eta { get; set; }

        /// <summary>Change request / branch / pull request / build the work is tracked under.</summary>
        [MaxLength(200)]
        public string? Reference { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        /// <summary>Shown to the client on the ticket page (internal notes are not).</summary>
        public bool VisibleToClient { get; set; } = true;

        public int? ByUserId { get; set; }
        [MaxLength(200)]
        public string? ByName { get; set; }

        public DateTime On { get; set; }
    }
}
