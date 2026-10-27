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
    }

    /// <summary>Weights and rules of the monthly scorecard (appsettings.json "Scorecard" section).</summary>
    public class ScorecardSettings
    {
        public double RatingWeight { get; set; } = 30;
        public double SlaWeight { get; set; } = 20;
        public double ResolutionWeight { get; set; } = 15;
        public double VolumeWeight { get; set; } = 15;
        public double ReopenWeight { get; set; } = 10;
        public double FirstResponseWeight { get; set; } = 5;
        public double BacklogWeight { get; set; } = 5;

        public int MinClosedTickets { get; set; } = 5;
        public int MinRatings { get; set; } = 3;

        /// <summary>Confidence constant of the adjusted rating: this many "team average" ratings are mixed in.</summary>
        public double RatingConfidence { get; set; } = 5;

        public double AwardMinRating { get; set; } = 4.0;
        public double AwardMinSla { get; set; } = 0.85;

        /// <summary>Day of the next month on which a month's scores are frozen and the award nominated.</summary>
        public int FreezeDay { get; set; } = 3;

        /// <summary>A nomination nobody confirmed or overrode is confirmed after this many days.</summary>
        public int AutoConfirmDays { get; set; } = 5;

        public double TotalWeight => RatingWeight + SlaWeight + ResolutionWeight + VolumeWeight + ReopenWeight + FirstResponseWeight + BacklogWeight;
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
