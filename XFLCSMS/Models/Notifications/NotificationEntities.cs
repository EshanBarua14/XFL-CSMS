using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace XFLCSMS.Models.Notifications
{
    /// <summary>Delivery channels. Stored as text so the log stays readable in SQL.</summary>
    public static class NotificationChannel
    {
        public const string InApp = "InApp";
        public const string Email = "Email";
        public const string WhatsApp = "WhatsApp";
        public const string Sms = "Sms";

        public static readonly string[] All = { InApp, Email, WhatsApp, Sms };

        public static string Label(string channel) => channel switch
        {
            InApp => "In-app toast",
            Email => "E-mail",
            WhatsApp => "WhatsApp",
            Sms => "SMS",
            _ => channel
        };

        public static string Icon(string channel) => channel switch
        {
            InApp => "fas fa-bell",
            Email => "fas fa-envelope",
            WhatsApp => "fab fa-whatsapp",
            Sms => "fas fa-sms",
            _ => "fas fa-paper-plane"
        };
    }

    /// <summary>The four roles of the application (see RegisterLoginController.Login).</summary>
    public static class NotificationRole
    {
        public const string Admin = "Admin";
        public const string SupportManager = "SupportManager";
        public const string SupportEngineer = "SupportEngineer";
        public const string Maker = "Maker";

        public static readonly string[] All = { Admin, SupportManager, SupportEngineer, Maker };

        public static string Label(string role) => role switch
        {
            Admin => "Administrator",
            SupportManager => "Support Manager",
            SupportEngineer => "Support Engineer",
            Maker => "Brokerage House User",
            _ => role
        };

        /// <summary>Same rules as the sign-in: admin flag first, then XFL staff by department, everybody else is a Maker.</summary>
        public static string Of(XFLCSMS.Models.Register.User user)
        {
            if (user.UCatagory) return Admin;
            if (user.UType && user.Department == "Support Maneger") return SupportManager;
            if (user.UType && user.Department == "Support Engineer") return SupportEngineer;
            return Maker;
        }

        /// <summary>Controller that serves the role's pages (links in messages point there).</summary>
        public static string Controller(string role) => role switch
        {
            Admin => "Admin",
            SupportManager => "SupportManegar",
            SupportEngineer => "SupportEngineer",
            _ => "Maker"
        };
    }

    /// <summary>Who of a role receives an event.</summary>
    public static class NotificationAudience
    {
        /// <summary>Only users the event is about: the ticket owner, the assigned engineer, the edited user...</summary>
        public const string Related = "Related";

        /// <summary>Every active user of the role (Makers: only the users of the ticket's brokerage house).</summary>
        public const string All = "All";
    }

    /// <summary>One switch of the event x channel x role matrix. A missing row means the catalog default.</summary>
    public class NotificationRule
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(80)]
        public string EventKey { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Channel { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Role { get; set; } = string.Empty;

        public bool Enabled { get; set; }

        [MaxLength(20)]
        public string Audience { get; set; } = NotificationAudience.Related;
    }

    /// <summary>A template edited by the administrator. Without a row the built-in default text is used.</summary>
    public class NotificationTemplate
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(80)]
        public string EventKey { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Channel { get; set; } = string.Empty;

        /// <summary>E-mail subject / toast title. Not used by SMS and WhatsApp.</summary>
        [MaxLength(300)]
        public string? Subject { get; set; }

        public string Body { get; set; } = string.Empty;

        public DateTime UpdatedOn { get; set; }

        [MaxLength(200)]
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// Provider settings and switches (key / value). Secrets (passwords, tokens, API keys) are encrypted with
    /// ASP.NET Core Data Protection before they are stored; a value under "Notifications:&lt;key&gt;" in the
    /// configuration (environment variable, user-secrets) wins over the database.
    /// </summary>
    public class NotificationSetting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }

        public bool IsSecret { get; set; }

        public DateTime UpdatedOn { get; set; }
    }

    /// <summary>A user's opt-out: no messages of this event (or "*" = every event) on this channel.</summary>
    public class NotificationOptOut
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int UserId { get; set; }

        [MaxLength(80)]
        public string EventKey { get; set; } = "*";

        [MaxLength(20)]
        public string Channel { get; set; } = string.Empty;
    }

    public static class NotificationStatus
    {
        public const string Pending = "Pending";
        public const string Sent = "Sent";
        public const string Failed = "Failed";
        public const string Skipped = "Skipped";

        public static readonly string[] All = { Pending, Sent, Failed, Skipped };
    }

    /// <summary>
    /// One message to one recipient on one channel: the queue (Pending rows are sent by NotificationWorker,
    /// with retries), the delivery log, and for the in-app channel the user's notification list (bell).
    /// </summary>
    public class NotificationMessage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        [MaxLength(80)]
        public string EventKey { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Channel { get; set; } = string.Empty;

        public int? UserId { get; set; }

        [MaxLength(200)]
        public string? RecipientName { get; set; }

        /// <summary>E-mail address or phone number; empty for the in-app channel.</summary>
        [MaxLength(200)]
        public string? Address { get; set; }

        [MaxLength(300)]
        public string? Subject { get; set; }

        public string Body { get; set; } = string.Empty;

        /// <summary>Page the message is about (opened from the toast / bell).</summary>
        [MaxLength(500)]
        public string? Url { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = NotificationStatus.Pending;

        public int Attempts { get; set; }

        public DateTime CreatedOn { get; set; }

        public DateTime NextAttemptOn { get; set; }

        public DateTime? SentOn { get; set; }

        /// <summary>In-app only: when the user opened / dismissed it.</summary>
        public DateTime? ReadOn { get; set; }

        [MaxLength(2000)]
        public string? LastError { get; set; }

        [MaxLength(200)]
        public string? ProviderMessageId { get; set; }

        public bool IsTest { get; set; }
    }

    /// <summary>Remembers SLA warnings / breaches already announced, so each is sent once per ticket.</summary>
    public class SlaAlert
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public int IssueId { get; set; }

        [MaxLength(80)]
        public string EventKey { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; }
    }
}
