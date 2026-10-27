namespace XFLCSMS.Models.Tiers
{
    /// <summary>An engineer as the level pickers show them: level and current open workload.</summary>
    public class EngineerOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int OpenTickets { get; set; }
        public bool Active { get; set; } = true;
    }

    /// <summary>Support level and development panel of the Ticket Details page (Views/Shared/Components/TicketWork).</summary>
    public class TicketWorkView
    {
        public int IssueId { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsStaff => Role != "Maker";
        public bool IsClosed { get; set; }

        public int Level { get; set; }
        public SupportLevel? LevelInfo { get; set; }
        public DateTime LevelSince { get; set; }
        public DateTime? EscalationDueAt { get; set; }
        public bool EscalationOverdue { get; set; }
        public List<SupportLevel> Levels { get; set; } = new();

        /// <summary>Engineers may only escalate upward; managers and administrators may also hand a ticket back.</summary>
        public bool CanHandBack { get; set; }
        public string? CurrentEngineer { get; set; }
        public int? CurrentEngineerId { get; set; }
        public List<EngineerOption> Engineers { get; set; } = new();
        public List<TicketEscalation> Escalations { get; set; } = new();

        public string? DevStage { get; set; }
        public DateTime? DevStageSince { get; set; }
        public int? DevOwnerUserId { get; set; }
        public string? DevOwner { get; set; }
        public DateTime? DevEta { get; set; }
        public string? DevReference { get; set; }
        public bool DevOverdue => DevEta.HasValue && DevStages.IsActive(DevStage) && DevEta.Value.Date < DateTime.Today;
        public List<EngineerOption> DevOwners { get; set; } = new();
        public List<TicketDevUpdate> DevUpdates { get; set; } = new();
    }

    /// <summary>One level on the Support Levels overview.</summary>
    public class LevelOverviewRow
    {
        public SupportLevel Level { get; set; } = new();
        public int Engineers { get; set; }
        public int ActiveEngineers { get; set; }
        public int Open { get; set; }
        public int Unassigned { get; set; }
        public int HighOpen { get; set; }
        public int EscalationDue { get; set; }
        public int ClosedInPeriod { get; set; }

        /// <summary>Tickets closed at this level that never left it (solved where they were raised / escalated to).</summary>
        public int EscalatedIn { get; set; }
        public int EscalatedOut { get; set; }
        public int HandedBack { get; set; }
        public double? AvgHoursAtLevel { get; set; }
        public double OpenPerEngineer => ActiveEngineers > 0 ? (double)Open / ActiveEngineers : Open;
    }

    public class EscalationRow
    {
        public TicketEscalation Escalation { get; set; } = new();
        public string TicketNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public class DevTicketRow
    {
        public int IssueId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string House { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Stage { get; set; } = string.Empty;
        public DateTime? StageSince { get; set; }
        public int? OwnerId { get; set; }
        public string? Owner { get; set; }
        public DateTime? Eta { get; set; }
        public string? Reference { get; set; }
        public string? LastNote { get; set; }
        public bool Overdue => Eta.HasValue && DevStages.IsActive(Stage) && Eta.Value.Date < DateTime.Today;
        public double DaysInStage => StageSince.HasValue ? Math.Max(0, (DateTime.Now - StageSince.Value).TotalDays) : 0;
    }

    /// <summary>Support Levels page (staff): workload, escalation flow and development pipeline.</summary>
    public class TiersOverviewView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";
        public string Role { get; set; } = "Admin";
        public int Days { get; set; } = 30;

        public List<LevelOverviewRow> Levels { get; set; } = new();
        public List<(EngineerOption Engineer, int Closed)> Engineers { get; set; } = new();
        public List<EscalationRow> RecentEscalations { get; set; } = new();
        public int EscalationsInPeriod { get; set; }
        public int HandBacksInPeriod { get; set; }

        /// <summary>Share of tickets closed in the period that were solved at L1 without escalation (first-level resolution).</summary>
        public double? FirstLevelResolution { get; set; }

        /// <summary>Share of tickets closed in the period that were escalated at least once.</summary>
        public double? EscalationRate { get; set; }

        public Dictionary<string, int> DevPipeline { get; set; } = new();
        public int DevOverdue { get; set; }
        public int DeployedInPeriod { get; set; }
        public double? AvgDaysToDeploy { get; set; }
    }

    /// <summary>Development Tracker board.</summary>
    public class DevTrackerView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";
        public string Role { get; set; } = "Admin";
        public bool ShowDeployed { get; set; }
        public int? OwnerId { get; set; }
        public List<EngineerOption> Owners { get; set; } = new();
        public List<DevTicketRow> Rows { get; set; } = new();
    }

    /// <summary>Admin > Support Levels: the five levels and every engineer's level.</summary>
    public class SupportLevelsAdminView
    {
        public List<SupportLevel> Levels { get; set; } = new();
        public List<EngineerOption> Engineers { get; set; } = new();
    }

    public class EngineerLevelForm
    {
        public int UserId { get; set; }
        public int Level { get; set; }
    }

    /// <summary>Support level strip of the staff dashboards (ViewComponents/TierStripViewComponent).</summary>
    public class TierStripView
    {
        public string Controller { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<(SupportLevel Level, int Open, int Unassigned, int Due)> Levels { get; set; } = new();
        public int InDevelopment { get; set; }
        public int DevOverdue { get; set; }

        /// <summary>For a Support Engineer: their level and the unassigned tickets they may pick up.</summary>
        public int? MyLevel { get; set; }
        public int MyQueue { get; set; }
    }
}
