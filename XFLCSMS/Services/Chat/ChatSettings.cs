using Microsoft.Extensions.Caching.Memory;
using XFLCSMS.Models.Chat;
using XFLCSMS.Models.Register;

namespace XFLCSMS.Services.Chat
{
    /// <summary>Who a user is for the chat rules. XFL staff outside support (other departments) are a role of their own.</summary>
    public static class ChatRole
    {
        public const string Admin = "Admin";
        public const string SupportManager = "SupportManager";
        public const string SupportEngineer = "SupportEngineer";
        public const string XflStaff = "XflStaff";
        public const string Maker = "Maker";

        /// <summary>Read-only executive accounts: never in chats (they cannot post anything in the application).</summary>
        public const string Viewer = "Viewer";

        /// <summary>The roles of the rules matrix (Viewer is always "not allowed").</summary>
        public static readonly string[] All = { Admin, SupportManager, SupportEngineer, XflStaff, Maker };

        public static string Of(User user)
        {
            if (user.UCatagory) return Admin;
            if (user.Department == XFLCSMS.Infrastructure.SessionAuthorizeAttribute.ViewerDepartment) return Viewer;
            if (!user.UType) return Maker;
            return user.Department switch
            {
                "Support Maneger" => SupportManager,
                "Support Engineer" => SupportEngineer,
                _ => XflStaff
            };
        }

        public static bool IsXfl(string role) => role is Admin or SupportManager or SupportEngineer or XflStaff;

        public static string Label(string role) => role switch
        {
            Admin => "Administrator",
            SupportManager => "Support Manager",
            SupportEngineer => "Support Engineer",
            XflStaff => "Other XFL staff",
            Maker => "Brokerage House User",
            Viewer => "Viewer (read-only)",
            _ => role
        };
    }

    /// <summary>What a pair of roles may do. The rule of a pair works both ways (A with B = B with A).</summary>
    public static class ChatRule
    {
        /// <summary>Always allowed.</summary>
        public const string Always = "Always";

        /// <summary>Only when one of them raised a ticket that is assigned to the other (or the other is its developer).</summary>
        public const string Ticket = "Ticket";

        /// <summary>Only users of the same brokerage house.</summary>
        public const string SameHouse = "SameHouse";

        public const string Never = "Never";

        public static readonly (string Value, string Label)[] Options =
        {
            (Always, "Always"), (Ticket, "Only through a ticket"), (SameHouse, "Same brokerage house"), (Never, "Not allowed")
        };

        public static bool IsValid(string? value) => Options.Any(o => o.Value == value);

        /// <summary>Key of the pair, independent of the order: "Rule.Maker|SupportEngineer".</summary>
        public static string Key(string a, string b)
        {
            var ia = Array.IndexOf(ChatRole.All, a);
            var ib = Array.IndexOf(ChatRole.All, b);
            return ia <= ib ? "Rule." + a + "|" + b : "Rule." + b + "|" + a;
        }

        /// <summary>
        /// Out of the box: XFL employees talk to each other freely; a brokerage house user (Maker) may talk to the
        /// Support Managers and to the engineer assigned to one of their tickets, nobody else.
        /// </summary>
        public static string Default(string a, string b)
        {
            if (ChatRole.IsXfl(a) && ChatRole.IsXfl(b)) return Always;
            var other = a == ChatRole.Maker ? b : a;
            return other switch
            {
                ChatRole.SupportManager => Always,
                ChatRole.SupportEngineer => Ticket,
                _ => Never
            };
        }

        /// <summary>Every pair once, in the order of the matrix on the settings page.</summary>
        public static IEnumerable<(string A, string B)> Pairs()
        {
            for (var i = 0; i < ChatRole.All.Length; i++)
                for (var j = i; j < ChatRole.All.Length; j++)
                    yield return (ChatRole.All[i], ChatRole.All[j]);
        }
    }

    public sealed record ChatSettingField(string Key, string Label, string Help, string Type, string Default);

    public static class ChatSettingKeys
    {
        public const string Enabled = "Chat.Enabled";
        public const string GroupsEnabled = "Chat.GroupsEnabled";
        public const string MakersCreateGroups = "Chat.MakersCreateGroups";
        public const string FilesEnabled = "Chat.FilesEnabled";
        public const string MakersSendFiles = "Chat.MakersSendFiles";
        public const string MaxFileMb = "Chat.MaxFileMb";
        public const string AllowedExtensions = "Chat.AllowedExtensions";
        public const string EditMinutes = "Chat.EditMinutes";
        public const string TicketGraceDays = "Chat.TicketGraceDays";
        public const string MaxGroupSize = "Chat.MaxGroupSize";
        public const string Toasts = "Chat.Toasts";

        public static readonly ChatSettingField[] Fields =
        {
            new(Enabled, "Chat on", "Master switch. Off: the Messages page and the chat icon disappear for everybody.", "bool", "true"),
            new(GroupsEnabled, "Group chats", "Off: only one-to-one chats can be started (existing groups stay readable and usable).", "bool", "true"),
            new(MakersCreateGroups, "Brokerage house users may create groups", "Off: only XFL staff create groups (they can still add a client to one, within the rules below).", "bool", "false"),
            new(FilesEnabled, "File sharing", "Documents and images can be sent in chats.", "bool", "true"),
            new(MakersSendFiles, "Brokerage house users may send files", "", "bool", "true"),
            new(MaxFileMb, "Largest file (MB)", "Per file. The web server also limits a whole request to 30 MB (web.config).", "number", "20"),
            new(AllowedExtensions, "Allowed file types", "Comma separated, e.g. .pdf,.docx,.png", "text", ".pdf,.doc,.docx,.xls,.xlsx,.csv,.txt,.ppt,.pptx,.png,.jpg,.jpeg,.gif,.zip"),
            new(EditMinutes, "Edit / delete own messages within (minutes)", "0 = messages cannot be changed once sent.", "number", "15"),
            new(TicketGraceDays, "Ticket chats stay open after closing (days)", "For the rule 'Only through a ticket': how long after the ticket is closed the two may still write. 0 = only while it is open.", "number", "7"),
            new(MaxGroupSize, "Largest group (members)", "", "number", "50"),
            new(Toasts, "Pop-up for new messages", "A toast on other pages when a message arrives (the unread count is always shown).", "bool", "true"),
        };

        public static ChatSettingField? Find(string key) => Fields.FirstOrDefault(f => f.Key == key);
    }

    /// <summary>Reads and writes the ChatSettings rows (cached). Missing rows fall back to the defaults above.</summary>
    public class ChatSettingsStore
    {
        private const string CacheKey = "chat-settings";
        private readonly IServiceScopeFactory _scopes;
        private readonly IMemoryCache _cache;

        public ChatSettingsStore(IServiceScopeFactory scopes, IMemoryCache cache)
        {
            _scopes = scopes;
            _cache = cache;
        }

        private Dictionary<string, string?> Rows()
        {
            return _cache.GetOrCreate(CacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DataContext>();
                return db.ChatSettings.AsNoTracking().ToList().ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
            })!;
        }

        public string? Get(string key)
        {
            if (Rows().TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return ChatSettingKeys.Find(key)?.Default;
        }

        public bool GetBool(string key) => bool.TryParse(Get(key), out var b) && b;

        public int GetInt(string key, int fallback) => int.TryParse(Get(key), out var i) ? i : fallback;

        public bool Enabled => GetBool(ChatSettingKeys.Enabled);

        public string RuleFor(string roleA, string roleB)
        {
            if (roleA == ChatRole.Viewer || roleB == ChatRole.Viewer) return ChatRule.Never;
            var value = Rows().TryGetValue(ChatRule.Key(roleA, roleB), out var v) ? v : null;
            return ChatRule.IsValid(value) ? value! : ChatRule.Default(roleA, roleB);
        }

        /// <summary>Types that run in a browser or on Windows are never accepted, whatever the setting says.</summary>
        public static readonly string[] BlockedExtensions =
        {
            ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".scr", ".ps1", ".vbs", ".js", ".jse", ".wsf", ".hta",
            ".html", ".htm", ".xhtml", ".svg", ".xml", ".jar", ".lnk", ".reg", ".cshtml", ".aspx", ".config"
        };

        public string[] AllowedExtensions =>
            (Get(ChatSettingKeys.AllowedExtensions) ?? string.Empty)
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(e => (e.StartsWith('.') ? e : "." + e).ToLowerInvariant())
                .Where(e => !BlockedExtensions.Contains(e))
                .Distinct().ToArray();

        public async Task SaveAsync(DataContext db, IDictionary<string, string?> values)
        {
            var existing = await db.ChatSettings.ToDictionaryAsync(s => s.Key, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in values)
            {
                if (existing.TryGetValue(key, out var row))
                {
                    row.Value = value;
                }
                else
                {
                    db.ChatSettings.Add(new ChatSetting { Key = key, Value = value });
                }
            }

            await db.SaveChangesAsync();
            _cache.Remove(CacheKey);
        }
    }
}
