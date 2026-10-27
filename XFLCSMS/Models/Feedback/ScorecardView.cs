namespace XFLCSMS.Models.Feedback
{
    public class ScorecardView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";
        public bool CanDecide { get; set; }

        public DateTime Month { get; set; }
        public bool IsFrozen { get; set; }
        public DateTime FreezesOn { get; set; }
        public List<DateTime> Months { get; set; } = new();

        public List<MonthlyEngineerScore> Rows { get; set; } = new();
        public EngineerAward? Award { get; set; }
        public List<EngineerAward> AwardHistory { get; set; } = new();

        /// <summary>Score and rank of each engineer last month, for the movement column.</summary>
        public Dictionary<int, int?> PreviousRanks { get; set; } = new();

        public ScorecardSettings Settings { get; set; } = new();
        public List<SlaTarget> Sla { get; set; } = new();

        /// <summary>Team KPIs of the month (ratings not excluded by management).</summary>
        public RatingKpis TeamKpis { get; set; } = new();
        public double? TeamSla { get; set; }
        public double? TeamReopenRate { get; set; }
        public int TeamClosed { get; set; }

        /// <summary>Team KPIs of the six months up to the selected one.</summary>
        public List<KpiPoint> Trend { get; set; } = new();
    }

    /// <summary>One engineer's performance: month KPIs, six-month trend, client feedback in depth.</summary>
    public class EngineerProfileView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";
        public int EngineerUserId { get; set; }
        public string EngineerName { get; set; } = string.Empty;
        public DateTime Month { get; set; }
        public bool IsFrozen { get; set; }
        public List<DateTime> Months { get; set; } = new();

        public MonthlyEngineerScore? Row { get; set; }
        public MonthlyEngineerScore? PreviousRow { get; set; }
        public int RankedCount { get; set; }

        /// <summary>Average points of the ranked engineers per measure, for comparison.</summary>
        public Dictionary<string, double> TeamPoints { get; set; } = new();
        public List<(string Measure, double Points)> Strengths { get; set; } = new();
        public List<(string Measure, double Points)> ToImprove { get; set; } = new();

        public List<KpiPoint> Trend { get; set; } = new();

        /// <summary>Client feedback of the six months up to the selected one.</summary>
        public RatingKpis Kpis { get; set; } = new();
        public RatingKpis TeamKpis { get; set; } = new();
        public List<FeedbackRow> RecentFeedback { get; set; } = new();
        public List<EngineerAward> Awards { get; set; } = new();
        public ScorecardSettings Settings { get; set; } = new();
    }

    /// <summary>Weights and rules of the monthly scorecard (appsettings.json "Scorecard" section).</summary>
    public class ScorecardSettings
    {
        public double RatingWeight { get; set; } = 25;
        public double SlaWeight { get; set; } = 20;
        public double ResolutionWeight { get; set; } = 15;
        public double VolumeWeight { get; set; } = 10;
        public double ReopenWeight { get; set; } = 10;
        public double FcrWeight { get; set; } = 10;
        public double FirstResponseWeight { get; set; } = 5;
        public double BacklogWeight { get; set; } = 5;

        public int MinClosedTickets { get; set; } = 5;
        public int MinRatings { get; set; } = 3;

        /// <summary>Confidence constant of the adjusted rating: this many "team average" ratings are mixed in.</summary>
        public double RatingConfidence { get; set; } = 5;

        public double AwardMinRating { get; set; } = 4.0;
        public double AwardMinSla { get; set; } = 0.85;

        /// <summary>Award rule: at least this share of the engineer's ratings are 4 or 5 stars (CSAT).</summary>
        public double AwardMinCsat { get; set; } = 0.80;

        /// <summary>Day of the next month on which a month's scores are frozen and the award nominated.</summary>
        public int FreezeDay { get; set; } = 3;

        /// <summary>A nomination nobody confirmed or overrode is confirmed after this many days.</summary>
        public int AutoConfirmDays { get; set; } = 5;

        public double TotalWeight => RatingWeight + SlaWeight + ResolutionWeight + VolumeWeight + ReopenWeight + FcrWeight + FirstResponseWeight + BacklogWeight;
    }

    /// <summary>Winner card on the management dashboards.</summary>
    public class AwardCardView
    {
        public string Controller { get; set; } = "Admin";
        public EngineerAward? Award { get; set; }
        public MonthlyEngineerScore? Score { get; set; }
        public int UnreviewedLowRatings { get; set; }
    }
}
