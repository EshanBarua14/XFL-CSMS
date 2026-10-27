using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;
using XFLCSMS.Models.Notifications;

namespace XFLCSMS.Services.Notifications
{
    public sealed record SettingField(string Group, string Key, string Label, string Help, string Type = "text",
        string? Default = null, bool Secret = false, string[]? Options = null);

    /// <summary>
    /// Every provider setting and switch shown on Admin > Notifications > Providers & settings.
    /// </summary>
    public static class SettingKeys
    {
        public const string InAppEnabled = "Channel.InApp.Enabled";
        public const string EmailEnabled = "Channel.Email.Enabled";
        public const string WhatsAppEnabled = "Channel.WhatsApp.Enabled";
        public const string SmsEnabled = "Channel.Sms.Enabled";
        public const string PublicBaseUrl = "General.PublicBaseUrl";
        public const string AppName = "General.AppName";
        public const string ExcludeActor = "General.ExcludeActor";
        public const string MaxAttempts = "General.MaxAttempts";
        public const string CountryCode = "General.CountryCode";
        public const string RetentionDays = "General.RetentionDays";
        public const string SlaWarningPercent = "Sla.WarningPercent";
        public const string SlaScanMinutes = "Sla.ScanMinutes";
        public const string SlaLookbackHours = "Sla.LookbackHours";

        public const string SmtpHost = "Email.Host";
        public const string SmtpPort = "Email.Port";
        public const string SmtpSecurity = "Email.Security";
        public const string SmtpUser = "Email.UserName";
        public const string SmtpPassword = "Email.Password";
        public const string SmtpFrom = "Email.FromAddress";
        public const string SmtpFromName = "Email.FromName";

        public const string WaProvider = "WhatsApp.Provider";
        public const string WaMetaPhoneId = "WhatsApp.Meta.PhoneNumberId";
        public const string WaMetaToken = "WhatsApp.Meta.AccessToken";
        public const string WaMetaVersion = "WhatsApp.Meta.ApiVersion";
        public const string WaMetaMode = "WhatsApp.Meta.Mode";
        public const string WaMetaTemplate = "WhatsApp.Meta.TemplateName";
        public const string WaMetaLanguage = "WhatsApp.Meta.TemplateLanguage";
        public const string WaTwilioSid = "WhatsApp.Twilio.AccountSid";
        public const string WaTwilioToken = "WhatsApp.Twilio.AuthToken";
        public const string WaTwilioFrom = "WhatsApp.Twilio.From";
        public const string WaTwilioContentSid = "WhatsApp.Twilio.ContentSid";

        public const string SmsProvider = "Sms.Provider";
        public const string SmsTwilioSid = "Sms.Twilio.AccountSid";
        public const string SmsTwilioToken = "Sms.Twilio.AuthToken";
        public const string SmsTwilioFrom = "Sms.Twilio.From";
        public const string SmsHttpMethod = "Sms.Http.Method";
        public const string SmsHttpUrl = "Sms.Http.Url";
        public const string SmsHttpBody = "Sms.Http.Body";
        public const string SmsHttpHeaders = "Sms.Http.Headers";
        public const string SmsHttpApiKey = "Sms.Http.ApiKey";
        public const string SmsHttpSenderId = "Sms.Http.SenderId";
        public const string SmsHttpUser = "Sms.Http.UserName";
        public const string SmsHttpPassword = "Sms.Http.Password";
        public const string SmsHttpSuccess = "Sms.Http.SuccessContains";
        public const string SmsHttpNumberFormat = "Sms.Http.NumberFormat";

        public const string General = "General";
        public const string Email = "E-mail (SMTP)";
        public const string WhatsApp = "WhatsApp";
        public const string Sms = "SMS";

        public static readonly SettingField[] Fields =
        {
            new(General, InAppEnabled, "In-app toasts on", "Real-time toasts and the bell in the top bar.", "bool", "true"),
            new(General, EmailEnabled, "E-mail on", "Master switch for every e-mail notification.", "bool", "true"),
            new(General, WhatsAppEnabled, "WhatsApp on", "Master switch; also needs a provider below.", "bool", "false"),
            new(General, SmsEnabled, "SMS on", "Master switch; also needs a provider below.", "bool", "false"),
            new(General, PublicBaseUrl, "Public address of CSMS", "Used for links in e-mail / SMS / WhatsApp, e.g. https://csms.xpertfintech.com. Empty: the address the user had open.", "text"),
            new(General, AppName, "Application name", "Printed as {{AppName}} in the texts.", "text", "XFL CSMS"),
            new(General, ExcludeActor, "Do not notify the person who did it", "Somebody who closes a ticket does not get their own 'ticket closed' message (security alerts always go).", "bool", "true"),
            new(General, MaxAttempts, "Delivery attempts", "Failed messages are retried after 1, 5, 15, 60 and 240 minutes up to this many attempts.", "number", "5"),
            new(General, CountryCode, "Default country code", "Local phone numbers (01XXXXXXXXX) get this code, e.g. 880 for Bangladesh.", "text", "880"),
            new(General, RetentionDays, "Keep the log for (days)", "Older log rows and read in-app notifications are deleted.", "number", "90"),
            new(General, SlaWarningPercent, "SLA warning at (% of target used)", "e.g. 80: warn when 80% of the first-response / resolution time is used.", "number", "80"),
            new(General, SlaScanMinutes, "SLA check every (minutes)", "How often open tickets are checked against the SLA targets.", "number", "5"),
            new(General, SlaLookbackHours, "Announce breaches up to (hours) late", "After the server was down, older breaches are not announced any more (no flood of messages).", "number", "24"),

            new(Email, SmtpHost, "SMTP server", "e.g. smtp.gmail.com, smtp.office365.com. Empty: EmailHost from appsettings.json.", "text"),
            new(Email, SmtpPort, "Port", "587 (STARTTLS) or 465 (SSL).", "number", "587"),
            new(Email, SmtpSecurity, "Security", "", "select", "StartTls", Options: new[] { "StartTls", "SslOnConnect", "Auto", "None" }),
            new(Email, SmtpUser, "User name", "Empty: EmailUsername from appsettings.json.", "text"),
            new(Email, SmtpPassword, "Password / app password", "Stored encrypted. Empty: EmailPassword from appsettings.json.", "password", Secret: true),
            new(Email, SmtpFrom, "From address", "Empty: the user name.", "text"),
            new(Email, SmtpFromName, "From name", "", "text", "XFL CSMS"),

            new(WhatsApp, WaProvider, "Provider", "Meta WhatsApp Cloud API or Twilio.", "select", "None", Options: new[] { "None", "Meta", "Twilio" }),
            new(WhatsApp, WaMetaPhoneId, "Meta: phone number ID", "WhatsApp Manager > API setup > Phone number ID (not the phone number).", "text"),
            new(WhatsApp, WaMetaToken, "Meta: access token", "A permanent System User token with whatsapp_business_messaging. Stored encrypted.", "password", Secret: true),
            new(WhatsApp, WaMetaVersion, "Meta: Graph API version", "", "text", "v21.0"),
            new(WhatsApp, WaMetaMode, "Meta: message type", "Template: an approved template with ONE body variable {{1}} carries the text (required for messages outside the 24-hour window). Text: free text, only delivered inside an open 24-hour chat.", "select", "Template", Options: new[] { "Template", "Text" }),
            new(WhatsApp, WaMetaTemplate, "Meta: template name", "e.g. csms_notification (body: \"{{1}}\").", "text", "csms_notification"),
            new(WhatsApp, WaMetaLanguage, "Meta: template language", "e.g. en, en_US, bn.", "text", "en"),
            new(WhatsApp, WaTwilioSid, "Twilio: Account SID", "", "text"),
            new(WhatsApp, WaTwilioToken, "Twilio: Auth token", "Stored encrypted.", "password", Secret: true),
            new(WhatsApp, WaTwilioFrom, "Twilio: WhatsApp sender", "e.g. +14155238886 (sandbox) or your approved number.", "text"),
            new(WhatsApp, WaTwilioContentSid, "Twilio: Content SID (optional)", "HX... of an approved content template with variable {{1}}; empty sends free text.", "text"),

            new(Sms, SmsProvider, "Provider", "Twilio, or any HTTP gateway (BulkSMSBD, SSL Wireless, Alpha SMS, Infobip, ...).", "select", "None", Options: new[] { "None", "Twilio", "Http" }),
            new(Sms, SmsTwilioSid, "Twilio: Account SID", "", "text"),
            new(Sms, SmsTwilioToken, "Twilio: Auth token", "Stored encrypted.", "password", Secret: true),
            new(Sms, SmsTwilioFrom, "Twilio: From number / sender", "", "text"),
            new(Sms, SmsHttpMethod, "HTTP: method", "GET (parameters in the URL), POST form, or POST JSON.", "select", "GET", Options: new[] { "GET", "POST_FORM", "POST_JSON" }),
            new(Sms, SmsHttpUrl, "HTTP: URL", "Placeholders: {to} {message} {apikey} {senderid} {username} {password} {id}. Values are URL-encoded in the URL.", "textarea"),
            new(Sms, SmsHttpBody, "HTTP: body (POST)", "Form: a=1&b={message}. JSON: {\"to\":\"{to}\",\"msg\":\"{message}\"} (values are JSON-escaped).", "textarea"),
            new(Sms, SmsHttpHeaders, "HTTP: extra headers", "One per line, e.g. Authorization: Bearer {apikey}", "textarea"),
            new(Sms, SmsHttpApiKey, "HTTP: API key / token", "Stored encrypted.", "password", Secret: true),
            new(Sms, SmsHttpSenderId, "HTTP: sender ID / masking", "", "text"),
            new(Sms, SmsHttpUser, "HTTP: user name", "", "text"),
            new(Sms, SmsHttpPassword, "HTTP: password", "Stored encrypted.", "password", Secret: true),
            new(Sms, SmsHttpSuccess, "HTTP: success text (optional)", "The answer must contain this text to count as sent (e.g. 202 or SUCCESS). Empty: any 2xx answer.", "text"),
            new(Sms, SmsHttpNumberFormat, "HTTP: number format", "How {to} is written.", "select", "880XXXXXXXXXX", Options: new[] { "880XXXXXXXXXX", "+880XXXXXXXXXX", "01XXXXXXXXX" }),
        };

        public static SettingField? Find(string key) => Fields.FirstOrDefault(f => f.Key == key);
    }

    /// <summary>Reads and writes NotificationSetting rows (cached, secrets encrypted). Registered as a singleton.</summary>
    public class NotificationSettingsStore
    {
        private const string CacheKey = "notification-settings";
        private readonly IServiceScopeFactory _scopes;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly IDataProtector _protector;
        private readonly ILogger<NotificationSettingsStore> _logger;

        public NotificationSettingsStore(IServiceScopeFactory scopes, IMemoryCache cache, IConfiguration configuration,
            IDataProtectionProvider protection, ILogger<NotificationSettingsStore> logger)
        {
            _scopes = scopes;
            _cache = cache;
            _configuration = configuration;
            _protector = protection.CreateProtector("XFLCSMS.Notifications.Settings");
            _logger = logger;
        }

        private Dictionary<string, NotificationSetting> Rows()
        {
            return _cache.GetOrCreate(CacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                return db.NotificationSettings.AsNoTracking().ToList().ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
            })!;
        }

        /// <summary>Configuration ("Notifications:Email.Password" or env Notifications__Email.Password) first, then the database, then the default.</summary>
        public string? Get(string key)
        {
            var configured = _configuration["Notifications:" + key];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            if (Rows().TryGetValue(key, out var row) && !string.IsNullOrEmpty(row.Value))
            {
                if (!row.IsSecret)
                {
                    return row.Value;
                }

                try
                {
                    return _protector.Unprotect(row.Value);
                }
                catch (Exception ex)
                {
                    // e.g. the database was restored on another server: its data protection keys are not here
                    _logger.LogWarning(ex, "Notification setting {Key} cannot be decrypted on this server; enter it again.", key);
                    return null;
                }
            }

            return SettingKeys.Find(key)?.Default;
        }

        public bool IsFromConfiguration(string key) => !string.IsNullOrWhiteSpace(_configuration["Notifications:" + key]);

        public bool HasStoredValue(string key) => Rows().TryGetValue(key, out var row) && !string.IsNullOrEmpty(row.Value);

        public bool GetBool(string key) => bool.TryParse(Get(key), out var b) && b;

        public int GetInt(string key, int fallback) => int.TryParse(Get(key), out var i) ? i : fallback;

        /// <summary>Saves settings. A secret posted empty keeps the stored value; "clear" removes it.</summary>
        public async Task SaveAsync(DataContext db, IDictionary<string, string?> values, ISet<string> clearSecrets)
        {
            var existing = await db.NotificationSettings.ToDictionaryAsync(s => s.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var field in SettingKeys.Fields)
            {
                existing.TryGetValue(field.Key, out var row);
                values.TryGetValue(field.Key, out var value);
                value = value?.Trim();

                if (field.Secret)
                {
                    if (clearSecrets.Contains(field.Key))
                    {
                        value = null;
                    }
                    else if (string.IsNullOrEmpty(value))
                    {
                        continue; // keep what is stored
                    }
                    else
                    {
                        value = _protector.Protect(value);
                    }
                }
                else if (field.Type == "bool")
                {
                    value = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "on" ? "true" : "false";
                }

                if (row == null)
                {
                    row = new NotificationSetting { Key = field.Key };
                    db.NotificationSettings.Add(row);
                }

                row.Value = value;
                row.IsSecret = field.Secret;
                row.UpdatedOn = DateTime.Now;
            }

            await db.SaveChangesAsync();
            Invalidate();
        }

        public void Invalidate() => _cache.Remove(CacheKey);
    }
}
