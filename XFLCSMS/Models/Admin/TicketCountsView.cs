namespace XFLCSMS.Models.Admin
{
    /// <summary>The page "Ticket Counts": how many tickets, for a period, counted every way the team asks for.</summary>
    public class TicketCountsView
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string PeriodText { get; set; } = string.Empty;
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";

        // tickets raised in the period
        public int Raised { get; set; }
        public int Open { get; set; }
        public int InQueue { get; set; }
        public int InProgress { get; set; }
        public int Closed { get; set; }
        public int Unassigned { get; set; }
        public int HighNotClosed { get; set; }
        /// <summary>Closed inside the period, whenever they were raised.</summary>
        public int ClosedInPeriod { get; set; }
        public double? AverageHoursToClose { get; set; }
        public int AllTime { get; set; }
        public int AllTimeNotClosed { get; set; }

        public List<HouseCount> Houses { get; set; } = new();
        public List<NameCount> Engineers { get; set; } = new();
        public List<NameCount> SupportTypes { get; set; } = new();
        public List<NameCount> Priorities { get; set; } = new();
        public List<NameCount> RaisedBy { get; set; } = new();
        public List<NameCount> Timeline { get; set; } = new();
        public string TimelineUnit { get; set; } = "Day";

        public int NotClosed => Raised - Closed;
        public int ClosedPercent => Raised == 0 ? 0 : (int)Math.Round(100.0 * Closed / Raised);
    }

    public class HouseCount
    {
        public string Name { get; set; } = string.Empty;
        public string Acronym { get; set; } = string.Empty;
        public int Raised { get; set; }
        public int Open { get; set; }
        public int InQueue { get; set; }
        public int InProgress { get; set; }
        public int Closed { get; set; }
        public int Unassigned { get; set; }
        public int High { get; set; }
    }

    public class NameCount
    {
        public string Name { get; set; } = string.Empty;
        public int Raised { get; set; }
        public int NotClosed { get; set; }
        public int Closed { get; set; }
    }
}
