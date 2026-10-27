using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.SignalR;
using MimeKit;
using MimeKit.Text;
using XFLCSMS.Models.Notifications;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>A failure that retrying will not fix (provider not set up, number refused...).</summary>
    public class PermanentNotificationException : Exception
    {
        public PermanentNotificationException(string message) : base(message) { }
    }

    /// <summary>Delivers one message on its channel. Throws on failure; returns the provider's message id.</summary>
    public class NotificationSender
    {
        private readonly NotificationSettingsStore _settings;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IConfiguration _configuration;

        public NotificationSender(NotificationSettingsStore settings, IHttpClientFactory httpFactory, IHubContext<NotificationHub> hub, IConfiguration configuration)
        {
            _settings = settings;
            _httpFactory = httpFactory;
            _hub = hub;
            _configuration = configuration;
        }

        public Task<string?> SendAsync(NotificationMessage m, CancellationToken token) => m.Channel switch
        {
            NotificationChannel.InApp => SendInAppAsync(m, token),
            NotificationChannel.Email => SendEmailAsync(m, token),
            NotificationChannel.WhatsApp => SendWhatsAppAsync(m, token),
            NotificationChannel.Sms => SendSmsAsync(m, token),
            _ => throw new PermanentNotificationException("Unknown channel " + m.Channel)
        };

        private async Task<string?> SendInAppAsync(NotificationMessage m, CancellationToken token)
        {
            if (m.UserId == null) throw new PermanentNotificationException("In-app messages need a user.");
            await _hub.Clients.Group(NotificationHub.GroupFor(m.UserId.Value)).SendAsync("notify", new
            {
                id = m.Id,
                title = m.Subject,
                body = m.Body,
                url = m.Url,
                eventKey = m.EventKey,
                time = m.CreatedOn.ToString("dd MMM HH:mm")
            }, token);
            return null;
        }

        // ---- e-mail -----------------------------------------------------------------------------------------

        private string? Smtp(string key, string legacy) => _settings.Get(key) is { Length: > 0 } v ? v : _configuration[legacy];

        public bool EmailConfigured => !string.IsNullOrWhiteSpace(Smtp(SettingKeys.SmtpHost, "EmailHost"));

        private async Task<string?> SendEmailAsync(NotificationMessage m, CancellationToken token)
        {
            var host = Smtp(SettingKeys.SmtpHost, "EmailHost");
            if (string.IsNullOrWhiteSpace(host)) throw new PermanentNotificationException("No SMTP server is set up.");
            if (string.IsNullOrWhiteSpace(m.Address)) throw new PermanentNotificationException("No e-mail address.");

            var user = Smtp(SettingKeys.SmtpUser, "EmailUsername");
            var password = Smtp(SettingKeys.SmtpPassword, "EmailPassword");
            var from = _settings.Get(SettingKeys.SmtpFrom) is { Length: > 0 } f ? f : user;
            if (string.IsNullOrWhiteSpace(from)) throw new PermanentNotificationException("No sender (From address) is set up.");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_settings.Get(SettingKeys.SmtpFromName) ?? "XFL CSMS", from));
            try
            {
                email.To.Add(MailboxAddress.Parse(m.Address));
            }
            catch (ParseException)
            {
                throw new PermanentNotificationException("Invalid e-mail address " + m.Address);
            }

            email.Subject = m.Subject ?? string.Empty;
            email.Body = new TextPart(TextFormat.Html) { Text = m.Body };

            var security = (_settings.Get(SettingKeys.SmtpSecurity) ?? "StartTls") switch
            {
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                "None" => SecureSocketOptions.None,
                "Auto" => SecureSocketOptions.Auto,
                _ => SecureSocketOptions.StartTls
            };

            using var smtp = new SmtpClient { Timeout = 20000 };
            await smtp.ConnectAsync(host, _settings.GetInt(SettingKeys.SmtpPort, 587), security, token);
            if (!string.IsNullOrWhiteSpace(user))
            {
                try
                {
                    await smtp.AuthenticateAsync(user, password ?? string.Empty, token);
                }
                catch (AuthenticationException ex)
                {
                    throw new PermanentNotificationException("SMTP sign-in refused: " + ex.Message);
                }
            }

            var answer = await smtp.SendAsync(email, token);
            await smtp.DisconnectAsync(true, token);
            return Cut(answer);
        }

        // ---- WhatsApp ---------------------------------------------------------------------------------------

        private async Task<string?> SendWhatsAppAsync(NotificationMessage m, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(m.Address)) throw new PermanentNotificationException("No mobile number.");
            var text = Trim(PlainText(m.Body), 1000);
            switch (_settings.Get(SettingKeys.WaProvider))
            {
                case "Meta":
                {
                    var phoneId = Need(SettingKeys.WaMetaPhoneId, "Meta phone number ID");
                    var tokenValue = Need(SettingKeys.WaMetaToken, "Meta access token");
                    var version = _settings.Get(SettingKeys.WaMetaVersion) ?? "v21.0";
                    object payload = _settings.Get(SettingKeys.WaMetaMode) == "Text"
                        ? new { messaging_product = "whatsapp", to = m.Address, type = "text", text = new { preview_url = true, body = text } }
                        : new
                        {
                            messaging_product = "whatsapp", to = m.Address, type = "template",
                            template = new
                            {
                                name = Need(SettingKeys.WaMetaTemplate, "template name"),
                                language = new { code = _settings.Get(SettingKeys.WaMetaLanguage) ?? "en" },
                                // template parameters may not contain new lines or more than 4 spaces in a row
                                components = new[] { new { type = "body", parameters = new[] { new { type = "text", text = OneLine(text) } } } }
                            }
                        };

                    using var request = new HttpRequestMessage(HttpMethod.Post, "https://graph.facebook.com/" + version + "/" + phoneId + "/messages")
                    {
                        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                    };
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenValue);
                    var body = await SendHttpAsync(request, token);
                    using var json = JsonDocument.Parse(body);
                    return json.RootElement.TryGetProperty("messages", out var msgs) && msgs.GetArrayLength() > 0
                        ? msgs[0].GetProperty("id").GetString() : null;
                }
                case "Twilio":
                {
                    var from = Need(SettingKeys.WaTwilioFrom, "Twilio WhatsApp sender").Replace("whatsapp:", string.Empty);
                    var form = new Dictionary<string, string> { ["To"] = "whatsapp:+" + m.Address, ["From"] = "whatsapp:" + (from.StartsWith("+") ? from : "+" + from) };
                    var contentSid = _settings.Get(SettingKeys.WaTwilioContentSid);
                    if (!string.IsNullOrWhiteSpace(contentSid))
                    {
                        form["ContentSid"] = contentSid;
                        form["ContentVariables"] = JsonSerializer.Serialize(new Dictionary<string, string> { ["1"] = OneLine(text) });
                    }
                    else
                    {
                        form["Body"] = text;
                    }

                    return await TwilioAsync(Need(SettingKeys.WaTwilioSid, "Twilio Account SID"), Need(SettingKeys.WaTwilioToken, "Twilio Auth token"), form, token);
                }
                default:
                    throw new PermanentNotificationException("No WhatsApp provider is set up.");
            }
        }

        // ---- SMS --------------------------------------------------------------------------------------------

        private async Task<string?> SendSmsAsync(NotificationMessage m, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(m.Address)) throw new PermanentNotificationException("No mobile number.");
            var text = Trim(PlainText(m.Body), 480);
            switch (_settings.Get(SettingKeys.SmsProvider))
            {
                case "Twilio":
                {
                    var from = Need(SettingKeys.SmsTwilioFrom, "Twilio From number");
                    var form = new Dictionary<string, string> { ["To"] = "+" + m.Address, ["From"] = from, ["Body"] = text };
                    return await TwilioAsync(Need(SettingKeys.SmsTwilioSid, "Twilio Account SID"), Need(SettingKeys.SmsTwilioToken, "Twilio Auth token"), form, token);
                }
                case "Http":
                    return await SendHttpSmsAsync(m, text, token);
                default:
                    throw new PermanentNotificationException("No SMS provider is set up.");
            }
        }

        private async Task<string?> SendHttpSmsAsync(NotificationMessage m, string text, CancellationToken token)
        {
            var url = Need(SettingKeys.SmsHttpUrl, "gateway URL");
            var to = PhoneNumbers.Format(m.Address!, _settings.Get(SettingKeys.SmsHttpNumberFormat) ?? "880XXXXXXXXXX", _settings.Get(SettingKeys.CountryCode) ?? "880");
            var values = new Dictionary<string, string>
            {
                ["to"] = to, ["message"] = text, ["apikey"] = _settings.Get(SettingKeys.SmsHttpApiKey) ?? string.Empty,
                ["senderid"] = _settings.Get(SettingKeys.SmsHttpSenderId) ?? string.Empty, ["username"] = _settings.Get(SettingKeys.SmsHttpUser) ?? string.Empty,
                ["password"] = _settings.Get(SettingKeys.SmsHttpPassword) ?? string.Empty, ["id"] = "csms" + m.Id
            };

            string Fill(string template, Func<string, string> encode) =>
                System.Text.RegularExpressions.Regex.Replace(template, @"\{(\w+)\}", x => values.TryGetValue(x.Groups[1].Value.ToLowerInvariant(), out var v) ? encode(v) : x.Value);

            var method = _settings.Get(SettingKeys.SmsHttpMethod) ?? "GET";
            using var request = new HttpRequestMessage(method == "GET" ? HttpMethod.Get : HttpMethod.Post, Fill(url, Uri.EscapeDataString));
            var bodyTemplate = _settings.Get(SettingKeys.SmsHttpBody);
            if (method == "POST_FORM")
            {
                request.Content = new StringContent(Fill(bodyTemplate ?? string.Empty, Uri.EscapeDataString), Encoding.UTF8, "application/x-www-form-urlencoded");
            }
            else if (method == "POST_JSON")
            {
                request.Content = new StringContent(Fill(bodyTemplate ?? "{}", v => JsonSerializer.Serialize(v).Trim('"')), Encoding.UTF8, "application/json");
            }

            foreach (var line in (_settings.Get(SettingKeys.SmsHttpHeaders) ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var colon = line.IndexOf(':');
                if (colon > 0)
                {
                    request.Headers.TryAddWithoutValidation(line.Substring(0, colon).Trim(), Fill(line.Substring(colon + 1).Trim(), v => v));
                }
            }

            var answer = await SendHttpAsync(request, token);
            var success = _settings.Get(SettingKeys.SmsHttpSuccess);
            if (!string.IsNullOrWhiteSpace(success) && !answer.Contains(success, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Gateway answer does not contain \"" + success + "\": " + Trim(answer, 500));
            }

            return Cut(answer);
        }

        // ---- helpers ----------------------------------------------------------------------------------------

        private async Task<string?> TwilioAsync(string sid, string authToken, Dictionary<string, string> form, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twilio.com/2010-04-01/Accounts/" + Uri.EscapeDataString(sid) + "/Messages.json")
            {
                Content = new FormUrlEncodedContent(form)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(sid + ":" + authToken)));
            var body = await SendHttpAsync(request, token);
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("sid", out var s) ? s.GetString() : null;
        }

        private async Task<string> SendHttpAsync(HttpRequestMessage request, CancellationToken token)
        {
            var client = _httpFactory.CreateClient("notifications");
            using var response = await client.SendAsync(request, token);
            var body = await response.Content.ReadAsStringAsync(token);
            if (!response.IsSuccessStatusCode)
            {
                var message = "HTTP " + (int)response.StatusCode + ": " + Trim(body, 800);
                // 4xx (bad token, refused number, unknown template) will fail again; 429 and 5xx may pass later
                if ((int)response.StatusCode is >= 400 and < 500 and not 408 and not 429)
                {
                    throw new PermanentNotificationException(message);
                }

                throw new HttpRequestException(message);
            }

            return body;
        }

        private string Need(string key, string label)
        {
            var value = _settings.Get(key);
            if (string.IsNullOrWhiteSpace(value)) throw new PermanentNotificationException("Missing setting: " + label + ".");
            return value;
        }

        private static string PlainText(string body) => NotificationSaveInterceptor.HtmlToTextKeepLines(body);

        private static string OneLine(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();

        private static string Trim(string text, int max) => text.Length <= max ? text : text.Substring(0, max - 1) + "…";

        private static string? Cut(string? text) => text == null ? null : Trim(text, 200);
    }
}
