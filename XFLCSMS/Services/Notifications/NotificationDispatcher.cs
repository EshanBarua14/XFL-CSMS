using XFLCSMS.Models.Notifications;
using XFLCSMS.Models.Register;

namespace XFLCSMS.Services.Notifications
{
    /// <summary>
    /// Turns one event into messages: applies the event x channel x role switches, finds the recipients,
    /// drops opt-outs, renders the templates and stores the messages as Pending for NotificationWorker.
    /// </summary>
    public class NotificationDispatcher
    {
        private readonly DataContext _db;
        private readonly NotificationSettingsStore _settings;
        private readonly ILogger<NotificationDispatcher> _logger;

        public NotificationDispatcher(DataContext db, NotificationSettingsStore settings, ILogger<NotificationDispatcher> logger)
        {
            _db = db;
            _settings = settings;
            _logger = logger;
        }

        public static bool ChannelOn(NotificationSettingsStore settings, string channel) => channel switch
        {
            NotificationChannel.InApp => settings.GetBool(SettingKeys.InAppEnabled),
            NotificationChannel.Email => settings.GetBool(SettingKeys.EmailEnabled),
            NotificationChannel.WhatsApp => settings.GetBool(SettingKeys.WhatsAppEnabled),
            NotificationChannel.Sms => settings.GetBool(SettingKeys.SmsEnabled),
            _ => false
        };

        /// <summary>Effective switch for one cell of the matrix: the stored rule, else the catalog default.</summary>
        public static (bool Enabled, string Audience) Rule(NotificationEventDefinition e, string channel, string role, IEnumerable<NotificationRule> rules)
        {
            var row = rules.FirstOrDefault(r => r.EventKey == e.Key && r.Channel == channel && r.Role == role);
            if (row != null) return (row.Enabled, row.Audience);
            var d = e.DefaultFor(role);
            return (d != null && d.Channels.Contains(channel), d?.Audience ?? NotificationAudience.Related);
        }

        public async Task<int> DispatchAsync(NotificationEventData data, CancellationToken token = default)
        {
            var definition = NotificationCatalog.Find(data.EventKey);
            if (definition == null)
            {
                _logger.LogWarning("Unknown notification event {Event}.", data.EventKey);
                return 0;
            }

            var channels = NotificationChannel.All.Where(c => ChannelOn(_settings, c)).ToList();
            if (channels.Count == 0) return 0;

            var rules = await _db.NotificationRules.AsNoTracking().Where(r => r.EventKey == definition.Key).ToListAsync(token);
            var cells = (from c in channels
                         from r in NotificationRole.All
                         let rule = Rule(definition, c, r, rules)
                         where rule.Enabled
                         select (Channel: c, Role: r, rule.Audience)).ToList();
            if (cells.Count == 0) return 0;

            // the user table is small (staff + house users): one read is cheaper than a query per role
            var users = await _db.Users.AsNoTracking()
                .Select(u => new User
                {
                    Id = u.Id, FullName = u.FullName, Email = u.Email, PhonNumber = u.PhonNumber, UserName = u.UserName, Department = u.Department,
                    UCatagory = u.UCatagory, UType = u.UType, UStatus = u.UStatus, BrokerageHouseName = u.BrokerageHouseName
                })
                .ToListAsync(token);
            var byId = users.ToDictionary(u => u.Id);

            await FillValuesAsync(data, byId, token);

            var excludeActor = _settings.GetBool(SettingKeys.ExcludeActor) && !definition.NotifyActor;
            var recipients = new List<(User User, string Channel, string Role)>();
            foreach (var cell in cells)
            {
                if (data.StaffOnly && cell.Role == NotificationRole.Maker) continue;
                IEnumerable<User> candidates;
                if (cell.Audience == NotificationAudience.All)
                {
                    candidates = users.Where(u => NotificationRole.Of(u) == cell.Role);
                    if (cell.Role == NotificationRole.Maker && data.HouseId != null)
                    {
                        // never tell one brokerage house about another house's tickets
                        candidates = candidates.Where(u => u.BrokerageHouseName == data.HouseId);
                    }
                }
                else
                {
                    candidates = data.RelatedUserIds.Where(byId.ContainsKey).Select(id => byId[id]).Where(u => NotificationRole.Of(u) == cell.Role);
                }

                foreach (var user in candidates)
                {
                    if (!user.UStatus && user.Id != data.SubjectUserId) continue;
                    if (excludeActor && user.Id == data.ActorUserId) continue;
                    recipients.Add((user, cell.Channel, cell.Role));
                }
            }

            recipients = recipients.GroupBy(r => (r.User.Id, r.Channel)).Select(g => g.First()).ToList();
            if (recipients.Count == 0) return 0;

            var ids = recipients.Select(r => r.User.Id).Distinct().ToList();
            var optOuts = await _db.NotificationOptOuts.AsNoTracking()
                .Where(o => ids.Contains(o.UserId) && (o.EventKey == definition.Key || o.EventKey == "*"))
                .ToListAsync(token);
            recipients = recipients.Where(r => !optOuts.Any(o => o.UserId == r.User.Id && o.Channel == r.Channel)).ToList();
            if (recipients.Count == 0) return 0;

            var templates = await _db.NotificationTemplates.AsNoTracking().Where(t => t.EventKey == definition.Key).ToListAsync(token);
            var baseUrl = (_settings.Get(SettingKeys.PublicBaseUrl) ?? data.BaseUrl ?? string.Empty).TrimEnd('/');
            var countryCode = _settings.Get(SettingKeys.CountryCode) ?? "880";
            var now = DateTime.Now;

            foreach (var (user, channel, role) in recipients)
            {
                var path = LinkFor(data, role);
                var values = new Dictionary<string, string?>(data.Values, StringComparer.OrdinalIgnoreCase)
                {
                    ["RecipientName"] = user.FullName,
                    ["Link"] = path == null ? baseUrl : baseUrl + path,
                    ["EventName"] = definition.Name,
                    ["AppName"] = _settings.Get(SettingKeys.AppName) ?? "Xpert CSMS",
                    ["Time"] = data.OccurredOn.ToString("dd MMM yyyy HH:mm")
                };

                var template = NotificationTexts.Resolve(definition, channel, templates);
                var html = channel == NotificationChannel.Email;
                var message = new NotificationMessage
                {
                    EventKey = definition.Key,
                    Channel = channel,
                    UserId = user.Id,
                    RecipientName = Cut(user.FullName, 200),
                    Subject = Cut(NotificationTexts.Render(template.Subject, values, false), 300),
                    Body = NotificationTexts.Render(template.Body, values, html),
                    Url = path,
                    CreatedOn = now,
                    NextAttemptOn = now,
                    Status = NotificationStatus.Pending
                };

                switch (channel)
                {
                    case NotificationChannel.Email:
                        message.Address = Cut(user.Email?.Trim(), 200);
                        if (string.IsNullOrWhiteSpace(message.Address)) Skip(message, "The user has no e-mail address.");
                        break;
                    case NotificationChannel.WhatsApp:
                    case NotificationChannel.Sms:
                        message.Address = PhoneNumbers.ToE164(user.PhonNumber, countryCode);
                        if (message.Address == null) Skip(message, "The user has no valid mobile number (" + user.PhonNumber + ").");
                        break;
                }

                _db.NotificationMessages.Add(message);
            }

            await _db.SaveChangesAsync(token);
            return recipients.Count;
        }

        private static void Skip(NotificationMessage message, string reason)
        {
            message.Status = NotificationStatus.Skipped;
            message.LastError = reason;
        }

        private static string? Cut(string? value, int max) => value == null || value.Length <= max ? value : value.Substring(0, max);

        public static string? LinkFor(NotificationEventData data, string role)
        {
            if (data.LinkPath != null) return data.LinkPath;
            var action = role == NotificationRole.Maker && data.MakerLinkAction != null ? data.MakerLinkAction : data.LinkAction;
            if (action == null) return null;
            if (action == "Scorecard" && role is not (NotificationRole.Admin or NotificationRole.SupportManager)) return null;
            return "/" + NotificationRole.Controller(role) + "/" + action + (data.LinkId != null ? "/" + data.LinkId : string.Empty);
        }

        /// <summary>Names for the ids captured with the event (house, product, owner, engineer).</summary>
        private async Task FillValuesAsync(NotificationEventData data, Dictionary<int, User> users, CancellationToken token)
        {
            var v = data.Values;
            v.TryAdd("ActorName", data.ActorName ?? "Somebody");

            var t = data.Ticket;
            if (t != null)
            {
                v.TryAdd("TicketNumber", t.TNumber);
                v.TryAdd("TicketTitle", t.Title);
                v.TryAdd("Priority", t.Priority);
                v.TryAdd("Status", t.Status);
                v.TryAdd("OwnerName", users.TryGetValue(t.OwnerUserId, out var owner) ? owner.FullName : null);
                v.TryAdd("EngineerName", t.EngineerUserId != null && users.TryGetValue(t.EngineerUserId.Value, out var eng) ? eng.FullName : t.EngineerName ?? "(not assigned)");
                v.TryAdd("HouseName", await _db.Brokerages.Where(b => b.BrokerageId == t.BrokerageId).Select(b => b.BrokerageHouseName).FirstOrDefaultAsync(token));
                if (t.ProductId != null)
                {
                    v.TryAdd("ProductName", await _db.Products.Where(p => p.ProductId == t.ProductId).Select(p => p.ProductName).FirstOrDefaultAsync(token));
                }
            }

            if (data.SubjectUserId != null && users.TryGetValue(data.SubjectUserId.Value, out var subject))
            {
                v.TryAdd("UserFullName", subject.FullName);
                v.TryAdd("UserName", subject.UserName);
                v.TryAdd("UserEmail", subject.Email);
                v.TryAdd("UserPhone", subject.PhonNumber);
                v.TryAdd("UserRole", NotificationRole.Label(NotificationRole.Of(subject)));
                v.TryAdd("HouseName", await _db.Brokerages.Where(b => b.BrokerageId == subject.BrokerageHouseName).Select(b => b.BrokerageHouseName).FirstOrDefaultAsync(token));
            }

            if (v.TryGetValue("OwnerUserId", out var ownerId) && int.TryParse(ownerId, out var oid) && users.TryGetValue(oid, out var todoOwner))
            {
                v.TryAdd("OwnerName", todoOwner.FullName);
            }
        }
    }

    public static class PhoneNumbers
    {
        /// <summary>
        /// Digits with country code, no "+", e.g. 8801712345678. "01712345678" gets the default country code,
        /// "+880..." / "00880..." are kept. Null when it does not look like a phone number.
        /// </summary>
        public static string? ToE164(string? phone, string countryCode)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            var cc = new string((countryCode ?? string.Empty).Where(char.IsDigit).ToArray());
            if (phone.TrimStart().StartsWith("+")) { }
            else if (digits.StartsWith("00")) digits = digits.Substring(2);
            else if (cc.Length > 0 && digits.StartsWith("0")) digits = cc + digits.Substring(1);
            else if (cc.Length > 0 && !digits.StartsWith(cc) && digits.Length <= 10) digits = cc + digits;
            return digits.Length is >= 8 and <= 15 ? digits : null;
        }

        /// <summary>Writes a number the way an HTTP SMS gateway wants it.</summary>
        public static string Format(string e164, string format, string countryCode)
        {
            var cc = new string((countryCode ?? string.Empty).Where(char.IsDigit).ToArray());
            return format switch
            {
                "+880XXXXXXXXXX" => "+" + e164,
                "01XXXXXXXXX" => cc.Length > 0 && e164.StartsWith(cc) ? "0" + e164.Substring(cc.Length) : e164,
                _ => e164
            };
        }
    }
}
