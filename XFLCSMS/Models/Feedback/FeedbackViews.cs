using System.ComponentModel.DataAnnotations;

namespace XFLCSMS.Models.Feedback
{
    /// <summary>The client's rating form.</summary>
    public class RatingForm
    {
        public int IssueId { get; set; }
        public string? TicketNumber { get; set; }
        public string? TicketTitle { get; set; }
        public string? EngineerName { get; set; }
        public DateTime? ClosedOn { get; set; }

        [Range(1, 5, ErrorMessage = "Please choose from 1 to 5 stars.")]
        public int Stars { get; set; }

        [Range(1, 5)]
        public int? SpeedScore { get; set; }

        [Range(1, 5)]
        public int? CommunicationScore { get; set; }

        [Range(1, 5)]
        public int? QualityScore { get; set; }

        [Range(1, 5)]
        public int? ProfessionalismScore { get; set; }

        [Range(1, 5)]
        public int? KnowledgeScore { get; set; }

        [Range(1, 5)]
        public int? EffortScore { get; set; }

        [Range(0, 10)]
        public int? RecommendScore { get; set; }

        public bool FullySolved { get; set; } = true;

        public bool? FirstContactResolved { get; set; }

        public List<string> Tags { get; set; } = new();

        [MaxLength(1000, ErrorMessage = "Please keep the feedback under 1000 characters.")]
        public string? Comment { get; set; }

        public bool IsEdit { get; set; }
    }

    /// <summary>Quick feedback tags on the rating form. Only these values are stored.</summary>
    public static class FeedbackTags
    {
        public static readonly string[] Positive =
        {
            "Fast response", "Clear communication", "Knowledgeable", "Polite and professional", "Solved first time", "Kept me updated"
        };

        public static readonly string[] Negative =
        {
            "Slow response", "Had to follow up", "Unclear explanation", "Problem came back", "Wrong solution", "Not professional"
        };

        public static bool IsPositive(string tag) => Positive.Contains(tag);

        /// <summary>The known tags among the posted ones, joined for storage.</summary>
        public static string? Join(IEnumerable<string>? tags)
        {
            var known = (tags ?? Enumerable.Empty<string>()).Where(t => Positive.Contains(t) || Negative.Contains(t)).Distinct().ToList();
            return known.Count == 0 ? null : string.Join("|", known);
        }
    }

    /// <summary>
    /// Customer experience KPIs over a set of ratings, as used in ITIL / HDI service desk reporting:
    /// CSAT (share of 4-5 stars), NPS (promoters 9-10 minus detractors 0-6), CES (customer effort, 1-5),
    /// FCR (first-contact resolution as reported by the client) and the average of each rated dimension.
    /// </summary>
    public class RatingKpis
    {
        public int Count { get; set; }
        public double? AverageStars { get; set; }
        public double? Csat { get; set; }
        public double? Nps { get; set; }
        public int NpsAnswers { get; set; }
        public int Promoters { get; set; }
        public int Passives { get; set; }
        public int Detractors { get; set; }
        public double? Ces { get; set; }
        public double? Fcr { get; set; }
        public double? SolvedRate { get; set; }
        public int[] StarCounts { get; set; } = new int[5];
        public double? Speed { get; set; }
        public double? Communication { get; set; }
        public double? Quality { get; set; }
        public double? Professionalism { get; set; }
        public double? Knowledge { get; set; }
        public List<(string Tag, int Count, bool Positive)> Tags { get; set; } = new();

        public static RatingKpis From(IEnumerable<TicketRating> source)
        {
            var list = source.ToList();
            double? Avg(Func<TicketRating, byte?> f)
            {
                var v = list.Where(r => f(r) != null).Select(r => (double)f(r)!.Value).ToList();
                return v.Count == 0 ? null : v.Average();
            }

            var k = new RatingKpis { Count = list.Count };
            if (list.Count == 0)
            {
                return k;
            }

            k.AverageStars = list.Average(r => (double)r.Stars);
            k.Csat = list.Count(r => r.Stars >= 4) / (double)list.Count;
            k.SolvedRate = list.Count(r => r.FullySolved) / (double)list.Count;
            foreach (var r in list)
            {
                k.StarCounts[Math.Clamp((int)r.Stars, 1, 5) - 1]++;
            }

            var nps = list.Where(r => r.RecommendScore != null).Select(r => (int)r.RecommendScore!.Value).ToList();
            k.NpsAnswers = nps.Count;
            k.Promoters = nps.Count(v => v >= 9);
            k.Detractors = nps.Count(v => v <= 6);
            k.Passives = nps.Count - k.Promoters - k.Detractors;
            k.Nps = nps.Count == 0 ? null : Math.Round(100.0 * (k.Promoters - k.Detractors) / nps.Count, 1);

            var fcr = list.Where(r => r.FirstContactResolved != null).ToList();
            k.Fcr = fcr.Count == 0 ? null : fcr.Count(r => r.FirstContactResolved == true) / (double)fcr.Count;

            k.Ces = Avg(r => r.EffortScore);
            k.Speed = Avg(r => r.SpeedScore);
            k.Communication = Avg(r => r.CommunicationScore);
            k.Quality = Avg(r => r.QualityScore);
            k.Professionalism = Avg(r => r.ProfessionalismScore);
            k.Knowledge = Avg(r => r.KnowledgeScore);
            k.Tags = list.SelectMany(r => r.TagList).GroupBy(t => t)
                .Select(g => (g.Key, g.Count(), FeedbackTags.IsPositive(g.Key)))
                .OrderByDescending(t => t.Item2).ToList();
            return k;
        }
    }

    /// <summary>One month of a KPI trend.</summary>
    public class KpiPoint
    {
        public DateTime Month { get; set; }
        public int Ratings { get; set; }
        public double? AverageStars { get; set; }
        public double? Csat { get; set; }
        public double? Nps { get; set; }
        public double? Ces { get; set; }
        public double? Fcr { get; set; }
        public double? Sla { get; set; }
        public int Closed { get; set; }
        public double? Score { get; set; }
        public int? Rank { get; set; }
        public double? TeamMedianScore { get; set; }
    }

    /// <summary>What the client may do with the rating of one ticket.</summary>
    public class RatingState
    {
        public bool IsClosed { get; set; }
        public TicketRating? Rating { get; set; }
        public bool CanRate { get; set; }
        public bool CanEdit { get; set; }
        public bool CanReopen { get; set; }
        public DateTime? RateUntil { get; set; }
    }

    public class FeedbackRow
    {
        public TicketRating Rating { get; set; } = new();
        public string TicketNumber { get; set; } = string.Empty;
        public string? TicketTitle { get; set; }
        public string? Priority { get; set; }
        public string HouseName { get; set; } = string.Empty;
        public string RatedByName { get; set; } = string.Empty;
    }

    public class EngineerFeedbackSummary
    {
        public int? EngineerUserId { get; set; }
        public string EngineerName { get; set; } = string.Empty;
        public int Ratings { get; set; }
        public double AverageStars { get; set; }
        public int FourOrFive { get; set; }
        public int OneOrTwo { get; set; }
        public int ClosedTickets { get; set; }
        public RatingKpis Kpis { get; set; } = new();
        public int ResponseRate => ClosedTickets == 0 ? 0 : (int)Math.Round(100.0 * Ratings / ClosedTickets);
    }

    public class ServiceFeedbackView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = "Admin";

        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? EngineerId { get; set; }
        public int? Stars { get; set; }
        public int? HouseId { get; set; }
        public bool FollowUp { get; set; }

        public List<(int Id, string Name)> Engineers { get; set; } = new();
        public List<(int Id, string Name)> Houses { get; set; } = new();

        public List<FeedbackRow> Rows { get; set; } = new();
        public List<EngineerFeedbackSummary> Summary { get; set; } = new();
        public int UnreviewedLow { get; set; }

        /// <summary>KPIs of all ratings matching the period, engineer and house filters.</summary>
        public RatingKpis Kpis { get; set; } = new();
        public int ClosedInPeriod { get; set; }
        public List<KpiPoint> Trend { get; set; } = new();
    }

    /// <summary>Rating panel on the staff ticket page (management only).</summary>
    public class RatingPanelView
    {
        public string Controller { get; set; } = "Admin";
        public bool IsClosed { get; set; }
        public TicketRating? Rating { get; set; }
        public DateTime? RateUntil { get; set; }
        public List<TicketStatusHistory> History { get; set; } = new();
    }
}
