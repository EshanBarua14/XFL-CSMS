using System.Net;
using System.Text.RegularExpressions;
using XFLCSMS.Models.Notifications;

namespace XFLCSMS.Services.Notifications
{
    public sealed record TemplateText(string? Subject, string Body, bool IsCustom);

    /// <summary>Default texts per channel and the {{Placeholder}} renderer.</summary>
    public static class NotificationTexts
    {
        private static readonly Regex Placeholder = new(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

        public static TemplateText Default(NotificationEventDefinition e, string channel) => channel switch
        {
            NotificationChannel.Email => new TemplateText("[{{AppName}}] " + e.Title,
                "<p>Dear {{RecipientName}},</p>\n<p>" + e.Text + "</p>\n<p><a href=\"{{Link}}\">Open in {{AppName}}</a></p>\n<p>Thank you,<br/>Xpert Fintech Limited</p>", false),
            NotificationChannel.WhatsApp => new TemplateText(null, "*" + e.Title + "*\n" + e.Text + "\n{{Link}}", false),
            NotificationChannel.Sms => new TemplateText(null, e.Text + " - {{AppName}}", false),
            _ => new TemplateText(e.Title, e.Text, false)
        };

        public static TemplateText Resolve(NotificationEventDefinition e, string channel, IEnumerable<NotificationTemplate> custom)
        {
            var row = custom.FirstOrDefault(t => t.EventKey == e.Key && t.Channel == channel);
            return row == null ? Default(e, channel) : new TemplateText(row.Subject, row.Body, true);
        }

        /// <summary>Replaces {{Name}} with its value. For HTML e-mail the values are encoded, the template is not.</summary>
        public static string Render(string? template, IReadOnlyDictionary<string, string?> values, bool html)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            return Placeholder.Replace(template, m =>
            {
                if (!values.TryGetValue(m.Groups[1].Value, out var value) || value == null) return string.Empty;
                return html ? WebUtility.HtmlEncode(value) : value;
            });
        }

        /// <summary>Sample values for the template preview and test messages.</summary>
        public static Dictionary<string, string?> SampleValues(string recipient) => new(StringComparer.OrdinalIgnoreCase)
        {
            ["RecipientName"] = recipient, ["ActorName"] = "Rahim Uddin", ["EventName"] = "Ticket closed", ["Time"] = DateTime.Now.ToString("dd MMM yyyy HH:mm"),
            ["Link"] = "https://csms.example.com/Maker/TicketView/42", ["AppName"] = "Xpert CSMS",
            ["TicketNumber"] = "ABC_0000042", ["TicketTitle"] = "Cannot print the portfolio statement", ["Priority"] = "High", ["Status"] = "Close",
            ["OldStatus"] = "In Progress", ["HouseName"] = "ABC Securities Ltd.", ["ProductName"] = "Back Office", ["EngineerName"] = "Karim Ahmed",
            ["OwnerName"] = "Nusrat Jahan", ["Comment"] = "Please update to version 4.2 and try again.", ["Changes"] = "priority Medium -> High",
            ["FileNames"] = "screenshot.png", ["Stars"] = "5", ["Solved"] = "yes", ["Reply"] = "Thank you, we have improved the response time.", ["RatingComment"] = "Very quick, thanks!", ["SlaKind"] = "resolution",
            ["DueAt"] = DateTime.Now.AddHours(2).ToString("dd MMM HH:mm"), ["Overdue"] = "1h 20m", ["UserFullName"] = "Nusrat Jahan", ["UserName"] = "nusrat",
            ["UserRole"] = "Brokerage House User", ["UserEmail"] = "nusrat@example.com", ["UserPhone"] = "01700000000", ["ItemType"] = "product",
            ["ItemName"] = "Back Office", ["Action"] = "added", ["Month"] = DateTime.Now.ToString("MMMM yyyy"), ["AwardStatus"] = "Confirmed",
            ["Reason"] = "Best SLA and ratings", ["Score"] = "87.5", ["TodoTitle"] = "Call ABC Securities", ["TodoStatus"] = "Done"
        };
    }
}
