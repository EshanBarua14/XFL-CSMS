namespace XFLCSMS.Models.Admin
{
    /// <summary>
    /// Filters of the ticket lists and the ticket report (query string, so a filtered page can be bookmarked
    /// or shared). Every field is optional; the lists set some of them themselves (e.g. Closed = status Close).
    /// </summary>
    public class TicketFilter
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>Which date From / To apply to: created (default), closed or updated.</summary>
        public string? DateField { get; set; }

        /// <summary>Quick ranges: today, 7d, 30d, month, lastmonth, quarter, year.</summary>
        public string? Period { get; set; }

        public List<int> HouseIds { get; set; } = new();
        public int? BranchId { get; set; }
        public List<int> ProductIds { get; set; } = new();
        public List<string> Statuses { get; set; } = new();
        public List<string> Priorities { get; set; } = new();
        public int? SupportTypeId { get; set; }
        public int? CategoryId { get; set; }
        public int? SubCategoryId { get; set; }
        public int? SectionId { get; set; }

        /// <summary>Assigned engineer (user id); -1 = not assigned.</summary>
        public int? EngineerId { get; set; }

        /// <summary>met, breached, response-breached, resolution-breached, at-risk.</summary>
        public string? Sla { get; set; }

        /// <summary>Only tickets open longer than this many days.</summary>
        public int? OlderThanDays { get; set; }

        /// <summary>Free text: ticket number, title, raised by, engineer.</summary>
        public string? Q { get; set; }

        public bool IsEmpty =>
            From == null && To == null && string.IsNullOrEmpty(Period) && HouseIds.Count == 0 && BranchId == null
            && ProductIds.Count == 0 && Statuses.Count == 0 && Priorities.Count == 0 && SupportTypeId == null
            && CategoryId == null && SubCategoryId == null && SectionId == null && EngineerId == null
            && string.IsNullOrEmpty(Sla) && OlderThanDays == null && string.IsNullOrWhiteSpace(Q);
    }

    /// <summary>One ticket as the detailed tables and the exports show it (names resolved, times computed).</summary>
    public class TicketRow
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string StatusLabel { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public int HouseId { get; set; }
        public string House { get; set; } = string.Empty;
        public string HouseCode { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public string RaisedBy { get; set; } = string.Empty;
        public string SupportType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SubCategory { get; set; } = string.Empty;
        public string Section { get; set; } = string.Empty;
        public int? EngineerId { get; set; }
        public string Engineer { get; set; } = string.Empty;
        public DateTime Created { get; set; }
        public DateTime? Assigned { get; set; }
        public DateTime? FirstResponse { get; set; }
        public DateTime? Updated { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public DateTime? Closed { get; set; }
        public string ClosedBy { get; set; } = string.Empty;
        public bool IsClosed { get; set; }

        /// <summary>Hours from creation to now (open) or to closing (closed).</summary>
        public double AgeHours { get; set; }
        public double? ResponseHours { get; set; }
        public double? ResolutionHours { get; set; }
        public double ResponseTargetHours { get; set; }
        public double ResolutionTargetHours { get; set; }

        /// <summary>Met / Breached / Pending.</summary>
        public string ResponseSla { get; set; } = string.Empty;

        /// <summary>Met / Breached / At risk / On track.</summary>
        public string ResolutionSla { get; set; } = string.Empty;
        public int? Rating { get; set; }
        public int Attachments { get; set; }
    }

    /// <summary>A count per group (house, product, engineer, ...) for the breakdown tables and charts.</summary>
    public class BreakdownRow
    {
        public string Name { get; set; } = string.Empty;
        public int Total { get; set; }
        public int Open { get; set; }
        public int Closed { get; set; }
        public int Breached { get; set; }
        public double? SlaMetPercent { get; set; }
        public double? AvgResolutionHours { get; set; }
        public double? AvgRating { get; set; }
    }

    public class TicketSummary
    {
        public int Total { get; set; }
        public int Open { get; set; }
        public int InQueue { get; set; }
        public int InProgress { get; set; }
        public int Closed { get; set; }
        public int Unassigned { get; set; }
        public int HighOpen { get; set; }
        public int OverdueOpen { get; set; }
        public double? AvgFirstResponseHours { get; set; }
        public double? MedianResolutionHours { get; set; }
        public double? SlaResponseMetPercent { get; set; }
        public double? SlaResolutionMetPercent { get; set; }
        public double? AvgRating { get; set; }
        public int Rated { get; set; }

        /// <summary>Trend: label, created, closed (per day, week or month depending on the range).</summary>
        public List<(string Label, int Created, int Closed)> Trend { get; set; } = new();
        public string TrendUnit { get; set; } = "day";

        public List<BreakdownRow> ByStatus { get; set; } = new();
        public List<BreakdownRow> ByPriority { get; set; } = new();
        public List<BreakdownRow> ByProduct { get; set; } = new();
        public List<BreakdownRow> ByHouse { get; set; } = new();
        public List<BreakdownRow> ByBranch { get; set; } = new();
        public List<BreakdownRow> ByEngineer { get; set; } = new();
        public List<BreakdownRow> ByCategory { get; set; } = new();
        public List<BreakdownRow> BySupportType { get; set; } = new();

        /// <summary>Open tickets by age: &lt;1 day, 1-3, 3-7, 7-30, &gt;30 days.</summary>
        public List<(string Label, int Count)> Aging { get; set; } = new();

        /// <summary>Created per weekday and hour (busy times heat map), [weekday 0=Mon][hour].</summary>
        public int[][] Heat { get; set; } = Enumerable.Range(0, 7).Select(_ => new int[24]).ToArray();
    }

    public record Option(string Value, string Text);

    /// <summary>Everything the shared TicketExplorer page needs.</summary>
    public class TicketExplorerView
    {
        public string Title { get; set; } = "Tickets";
        public string? Subtitle { get; set; }

        /// <summary>"list" = a ticket list with the detailed table; "report" = KPIs, charts, breakdowns and the table.</summary>
        public string Mode { get; set; } = "list";
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        /// <summary>Filters fixed by the page (e.g. Closed list) and not shown as editable.</summary>
        public string? Preset { get; set; }

        public TicketFilter Filter { get; set; } = new();
        public List<TicketRow> Rows { get; set; } = new();
        public int TotalMatching { get; set; }
        public int RowLimit { get; set; }
        public TicketSummary Summary { get; set; } = new();
        public DateTime GeneratedOn { get; set; } = DateTime.Now;

        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool ShowHouseFilter { get; set; } = true;
        public bool ShowEngineerFilter { get; set; } = true;
        public string? CreateAction { get; set; }

        public List<Option> Houses { get; set; } = new();
        public List<Option> Branches { get; set; } = new();
        public List<Option> Products { get; set; } = new();
        public List<Option> SupportTypes { get; set; } = new();
        public List<Option> Categories { get; set; } = new();
        public List<Option> SubCategories { get; set; } = new();
        public List<Option> Sections { get; set; } = new();
        public List<Option> Engineers { get; set; } = new();

        /// <summary>Readable list of the active filters, for the page and the printed / exported report.</summary>
        public List<string> FilterDescription { get; set; } = new();
    }
}
