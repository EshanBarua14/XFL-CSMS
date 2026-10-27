namespace XFLCSMS.Models.Notifications
{
    public class MyNotificationsView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = string.Empty;
        public List<NotificationMessage> Items { get; set; } = new();
        public int Page { get; set; } = 1;
        public bool HasMore { get; set; }
        public int Unread { get; set; }
    }

    public class PreferenceRow
    {
        public string EventKey { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>Channels the administrator turned on for this user's role.</summary>
        public List<string> Channels { get; set; } = new();
    }

    public class PreferencesView
    {
        public string Layout { get; set; } = "_Layout";
        public string Controller { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<PreferenceRow> Rows { get; set; } = new();
        public HashSet<string> OptedOut { get; set; } = new();
        public List<string> ActiveChannels { get; set; } = new();
        public bool HasEmail { get; set; }
        public bool HasPhone { get; set; }
    }

    public class NotificationLogView
    {
        public List<NotificationMessage> Items { get; set; } = new();
        public string? Channel { get; set; }
        public string? Status { get; set; }
        public string? EventKey { get; set; }
        public string? Search { get; set; }
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int Page { get; set; } = 1;
        public int Total { get; set; }
        public int PageSize { get; set; } = 50;
        public Dictionary<string, int> Counts { get; set; } = new();
    }

    public class TemplateEditView
    {
        public string EventKey { get; set; } = string.Empty;
        public string Channel { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string Body { get; set; } = string.Empty;
        public bool IsCustom { get; set; }
        public string? PreviewSubject { get; set; }
        public string? PreviewBody { get; set; }
    }
}
