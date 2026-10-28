using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Tiers
{
    /// <summary>
    /// How tickets reach engineers (Admin > Setup > Ticket Assignment): self-assign, auto-assign of the oldest queued
    /// ticket to an idle engineer, and transfer requests between engineers. One row (Id 1). What each support level
    /// may do is on SupportLevel (Admin > Setup > Support Levels).
    /// </summary>
    public class AssignmentSettings
    {
        public const int SingletonId = 1;

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; } = SingletonId;

        /// <summary>Engineers may take an unassigned ticket themselves (also needs "Self-assign" on their level).</summary>
        public bool SelfAssignEnabled { get; set; } = true;

        /// <summary>Engineers may give a ticket they hold back to the queue.</summary>
        public bool ReleaseEnabled { get; set; } = true;

        /// <summary>Status an Open ticket moves to when an engineer takes it or it is auto-assigned (Inqueue / Inprogress; null = keep it).</summary>
        [MaxLength(20)]
        public string? StatusOnTake { get; set; }

        /// <summary>Idle engineers get the oldest queued ticket automatically.</summary>
        public bool AutoAssignEnabled { get; set; } = true;

        /// <summary>An engineer counts as idle while they hold fewer open tickets than this (1 = only engineers with no ticket).</summary>
        [Range(1, 100)]
        public int AutoAssignIdleBelow { get; set; } = 1;

        [Range(1, 1440)]
        public int AutoAssignIntervalMinutes { get; set; } = 2;

        /// <summary>Which queued tickets an idle engineer may get: see <see cref="AssignmentLevelRules"/>.</summary>
        [MaxLength(20)]
        public string AutoAssignLevelRule { get; set; } = AssignmentLevelRules.OwnLevelFirst;

        /// <summary>Oldest first, or High priority before Medium before Low and the oldest within each.</summary>
        [MaxLength(20)]
        public string AutoAssignOrder { get; set; } = AssignmentOrders.Oldest;

        /// <summary>Only engineers who used CSMS in the last <see cref="OnlineWindowMinutes"/> minutes get tickets automatically.</summary>
        public bool AutoAssignOnlyOnline { get; set; } = true;

        [Range(1, 1440)]
        public int OnlineWindowMinutes { get; set; } = 15;

        /// <summary>Tickets raised less than this many minutes ago stay in the queue for self-assign first (0 = assign at once).</summary>
        [Range(0, 1440)]
        public int AutoAssignDelayMinutes { get; set; }

        /// <summary>Engineers may ask another engineer to take over a ticket they hold.</summary>
        public bool TransferEnabled { get; set; } = true;

        /// <summary>After the other engineer accepts, a support manager must still approve the transfer.</summary>
        public bool TransferNeedsManagerApproval { get; set; }

        /// <summary>Unanswered requests lapse after this many hours (0 = never).</summary>
        [Range(0, 720)]
        public int TransferExpiryHours { get; set; } = 24;

        /// <summary>Levels' "max open tickets" also stops an engineer from accepting a transfer.</summary>
        public bool TransferRespectsCapacity { get; set; } = true;

        public DateTime? UpdatedOn { get; set; }
        [MaxLength(200)]
        public string? UpdatedBy { get; set; }
    }

    public static class AssignmentLevelRules
    {
        /// <summary>Tickets at the engineer's own level first, then lower levels.</summary>
        public const string OwnLevelFirst = "OwnLevelFirst";
        /// <summary>Only tickets at the engineer's own level.</summary>
        public const string OwnLevelOnly = "OwnLevelOnly";
        /// <summary>The oldest ticket at the engineer's level or below, whatever its level.</summary>
        public const string AtOrBelow = "AtOrBelow";

        public static readonly (string Value, string Label)[] All =
        {
            (OwnLevelFirst, "Own level first, then lower levels"),
            (OwnLevelOnly, "Own level only"),
            (AtOrBelow, "Oldest at own level or below")
        };

        public static string Normalize(string? value) => All.Any(a => a.Value == value) ? value! : OwnLevelFirst;
    }

    public static class AssignmentOrders
    {
        public const string Oldest = "Oldest";
        public const string PriorityThenOldest = "Priority";

        public static readonly (string Value, string Label)[] All =
        {
            (Oldest, "Oldest ticket first"),
            (PriorityThenOldest, "High priority first, then oldest")
        };

        public static string Normalize(string? value) => All.Any(a => a.Value == value) ? value! : Oldest;
    }

    /// <summary>One engineer asking another to take over a ticket.</summary>
    public class TicketTransferRequest
    {
        public const string Pending = "Pending";
        /// <summary>Accepted by the engineer, waiting for a support manager (when that is switched on).</summary>
        public const string AwaitingApproval = "AwaitingApproval";
        public const string Accepted = "Accepted";
        public const string Declined = "Declined";
        public const string Cancelled = "Cancelled";
        public const string Rejected = "Rejected";
        public const string Expired = "Expired";

        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int IssueId { get; set; }

        public int FromUserId { get; set; }
        [MaxLength(200)]
        public string FromName { get; set; } = string.Empty;

        public int ToUserId { get; set; }
        [MaxLength(200)]
        public string ToName { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string Note { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Status { get; set; } = Pending;

        public DateTime CreatedOn { get; set; }
        public DateTime? ExpiresOn { get; set; }

        public DateTime? DecidedOn { get; set; }
        public int? DecidedByUserId { get; set; }
        [MaxLength(200)]
        public string? DecidedByName { get; set; }
        [MaxLength(1000)]
        public string? DecisionNote { get; set; }

        [NotMapped]
        public bool IsOpen => Status is Pending or AwaitingApproval;

        public static string Label(string status) => status switch
        {
            Pending => "Waiting for answer",
            AwaitingApproval => "Waiting for manager",
            Accepted => "Accepted",
            Declined => "Declined",
            Cancelled => "Withdrawn",
            Rejected => "Rejected by manager",
            Expired => "Expired",
            _ => status
        };
    }

    /// <summary>A queued (unassigned, open) ticket on the Ticket Queue page.</summary>
    public class QueueTicketRow
    {
        public int IssueId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string House { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Level { get; set; }
        public DateTime Raised { get; set; }
        public bool CanTake { get; set; }
        public string? WhyNot { get; set; }
    }

    /// <summary>A transfer request with its ticket, for the lists.</summary>
    public class TransferRow
    {
        public TicketTransferRequest Request { get; set; } = new();
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Level { get; set; }
    }

    /// <summary>Ticket Queue page (engineers, managers, administrators).</summary>
    public class TicketQueueView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";
        public string Role { get; set; } = "Admin";
        public bool IsEngineer => Role == "SupportEngineer";
        public bool IsManager => Role is "Admin" or "SupportManager";

        public AssignmentSettings Settings { get; set; } = new();
        public List<SupportLevel> Levels { get; set; } = new();

        /// <summary>The signed-in engineer: level, open tickets and what their level allows.</summary>
        public int? MyLevel { get; set; }
        public SupportLevel? MyAuthority { get; set; }
        public int MyOpen { get; set; }
        public bool CanSelfAssign { get; set; }
        public string? SelfAssignBlocked { get; set; }

        public List<QueueTicketRow> Queue { get; set; } = new();
        public List<EngineerOption> Engineers { get; set; } = new();
        public Dictionary<int, DateTime> LastSeen { get; set; } = new();

        public List<TransferRow> Incoming { get; set; } = new();
        public List<TransferRow> Outgoing { get; set; } = new();
        /// <summary>Managers: every open request, and accepted ones waiting for approval.</summary>
        public List<TransferRow> Open { get; set; } = new();
        public List<TransferRow> Recent { get; set; } = new();
    }

    /// <summary>Assignment part of the ticket's level panel.</summary>
    public class TicketAssignmentInfo
    {
        public bool CanTake { get; set; }
        public string? TakeBlocked { get; set; }
        public bool CanRelease { get; set; }
        public bool CanRequestTransfer { get; set; }
        public bool CanAssignOthers { get; set; }
        public TicketTransferRequest? OpenTransfer { get; set; }
        public bool OpenTransferIsForMe { get; set; }
        public bool OpenTransferIsMine { get; set; }
        public bool CanApproveTransfer { get; set; }
        public List<EngineerOption> TransferTargets { get; set; } = new();
        public List<TicketTransferRequest> History { get; set; } = new();
    }
}
