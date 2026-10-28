using Microsoft.AspNetCore.SignalR;
using XFLCSMS.Models.Chat;
using XFLCSMS.Services.Notifications;

namespace XFLCSMS.Services.Chat
{
    /// <summary>A user as the chat sees them.</summary>
    public sealed class ChatPerson
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Role { get; init; } = ChatRole.Maker;
        public int House { get; init; }
        public string? HouseName { get; init; }
        public string? Designation { get; init; }
        public string? Department { get; init; }
        public bool Active { get; init; }

        /// <summary>"Support Engineer", "Alpha Securities Limited", ...</summary>
        public string Subtitle => Role == ChatRole.Maker
            ? (string.IsNullOrWhiteSpace(HouseName) ? ChatRole.Label(Role) : HouseName!)
            : (string.IsNullOrWhiteSpace(Designation) || string.Equals(Designation.Trim(), ChatRole.Label(Role), StringComparison.OrdinalIgnoreCase) || Designation == Department
                ? ChatRole.Label(Role) : ChatRole.Label(Role) + " · " + Designation);
    }

    /// <summary>
    /// Conversations, messages, files and the rules of who may talk to whom (ChatSettingsStore). Every rule is checked
    /// on the server when a chat is started, a member is added and a message is sent, so a ticket that is reassigned
    /// also ends the client's chat with the previous engineer (after the grace days).
    /// </summary>
    public class ChatService
    {
        public const int MaxBodyLength = 4000;
        public const string FolderName = "chat";

        private readonly DataContext _db;
        private readonly ChatSettingsStore _settings;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly TicketService _tickets;
        private readonly ILogger<ChatService> _logger;
        private readonly Dictionary<int, HashSet<int>> _linked = new();
        private Dictionary<int, ChatPerson>? _people;

        public ChatService(DataContext db, ChatSettingsStore settings, IHubContext<NotificationHub> hub, TicketService tickets, ILogger<ChatService> logger)
        {
            _db = db;
            _settings = settings;
            _hub = hub;
            _tickets = tickets;
            _logger = logger;
        }

        public ChatSettingsStore Settings => _settings;

        public string FileFolder => Path.Combine(_tickets.UploadFolder, FolderName);

        // ---------------------------------------------------------------- people and rules

        /// <summary>Every user (active or not) by id, read once per request.</summary>
        public async Task<Dictionary<int, ChatPerson>> PeopleAsync()
        {
            if (_people != null) return _people;
            var houses = await _db.Brokerages.AsNoTracking().ToDictionaryAsync(b => b.BrokerageId, b => b.BrokerageHouseName);
            var users = await _db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.FullName, u.UserName, u.UCatagory, u.UType, u.Department, u.Designation, u.BrokerageHouseName, u.UStatus })
                .ToListAsync();
            _people = users.ToDictionary(u => u.Id, u => new ChatPerson
            {
                Id = u.Id,
                Name = string.IsNullOrWhiteSpace(u.FullName) ? u.UserName : u.FullName,
                Role = ChatRole.Of(new Models.Register.User { UCatagory = u.UCatagory, UType = u.UType, Department = u.Department }),
                House = u.BrokerageHouseName,
                HouseName = houses.TryGetValue(u.BrokerageHouseName, out var h) ? h : null,
                Designation = u.Designation,
                Department = u.Department,
                Active = u.UStatus
            });
            return _people;
        }

        /// <summary>People linked to the user through a ticket: owner and assigned engineer / developer, while open or within the grace days.</summary>
        private async Task<HashSet<int>> LinkedAsync(int userId)
        {
            if (_linked.TryGetValue(userId, out var set)) return set;

            var grace = Math.Max(0, _settings.GetInt(ChatSettingKeys.TicketGraceDays, 7));
            var since = DateTime.Now.AddDays(-grace);
            var tickets = _db.Issues.AsNoTracking().Where(i => i.IStatus != "Close" || (grace > 0 && i.ClosedOn >= since));

            // older tickets only carry the engineer's name (AssignBy), not AssignedToUserId
            var people = await PeopleAsync();
            var myName = people.TryGetValue(userId, out var me) && ChatRole.IsXfl(me.Role) ? me.Name : null;

            var mine = await tickets.Where(i => i.UserId == userId)
                .Select(i => new { i.AssignedToUserId, i.AssignBy, i.DevOwnerUserId }).ToListAsync();
            var theirs = await tickets
                .Where(i => i.AssignedToUserId == userId || i.DevOwnerUserId == userId || (myName != null && i.AssignedToUserId == null && i.AssignBy == myName))
                .Select(i => i.UserId).ToListAsync();

            set = new HashSet<int>(theirs);
            foreach (var t in mine)
            {
                var engineer = t.AssignedToUserId ?? StaffByName(people, t.AssignBy);
                if (engineer != null) set.Add(engineer.Value);
                if (t.DevOwnerUserId != null) set.Add(t.DevOwnerUserId.Value);
            }

            _linked[userId] = set;
            return set;
        }

        /// <summary>The engineer of a ticket: AssignedToUserId, or for older tickets the staff member named in AssignBy.</summary>
        public async Task<int?> EngineerOfAsync(int? assignedToUserId, string? assignBy) =>
            assignedToUserId ?? StaffByName(await PeopleAsync(), assignBy);

        private static int? StaffByName(Dictionary<int, ChatPerson> people, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return people.Values.Where(p => p.Name == name && ChatRole.IsXfl(p.Role))
                .OrderByDescending(p => p.Role == ChatRole.SupportEngineer).ThenBy(p => p.Id)
                .Select(p => (int?)p.Id).FirstOrDefault();
        }

        /// <summary>May these two write to each other under the current rules?</summary>
        public async Task<bool> CanChatAsync(ChatPerson a, ChatPerson b)
        {
            if (a.Id == b.Id) return true;
            if (!a.Active || !b.Active) return false;

            return _settings.RuleFor(a.Role, b.Role) switch
            {
                ChatRule.Always => true,
                ChatRule.SameHouse => a.House != 0 && a.House == b.House,
                ChatRule.Ticket => (await LinkedAsync(a.Id)).Contains(b.Id),
                _ => false
            };
        }

        /// <summary>Everybody the user may start a chat with (or add to a group).</summary>
        public async Task<List<ChatPerson>> ContactsAsync(int meId)
        {
            var people = await PeopleAsync();
            if (!people.TryGetValue(meId, out var me)) return new List<ChatPerson>();

            var result = new List<ChatPerson>();
            foreach (var p in people.Values.Where(p => p.Id != meId && p.Active))
            {
                if (await CanChatAsync(me, p)) result.Add(p);
            }

            return result.OrderBy(p => ChatRole.IsXfl(p.Role) ? 0 : 1).ThenBy(p => p.Name).ToList();
        }

        public bool MayCreateGroups(ChatPerson me) =>
            _settings.GetBool(ChatSettingKeys.GroupsEnabled) && (ChatRole.IsXfl(me.Role) || _settings.GetBool(ChatSettingKeys.MakersCreateGroups));

        public bool MaySendFiles(ChatPerson me) =>
            _settings.GetBool(ChatSettingKeys.FilesEnabled) && (ChatRole.IsXfl(me.Role) || _settings.GetBool(ChatSettingKeys.MakersSendFiles));

        // ---------------------------------------------------------------- conversations

        private IQueryable<ChatParticipant> Active(int conversationId) =>
            _db.ChatParticipants.Where(p => p.ConversationId == conversationId && p.LeftOn == null);

        /// <summary>The user's membership of a chat, or null when they are not (or no longer) in it.</summary>
        public Task<ChatParticipant?> MembershipAsync(int conversationId, int userId) =>
            Active(conversationId).FirstOrDefaultAsync(p => p.UserId == userId);

        public async Task<List<int>> MemberIdsAsync(int conversationId) =>
            await Active(conversationId).Select(p => p.UserId).ToListAsync();

        /// <summary>Null when the user may write in the chat, otherwise why not.</summary>
        public async Task<string?> CannotSendReasonAsync(int conversationId, int meId)
        {
            var people = await PeopleAsync();
            if (!people.TryGetValue(meId, out var me)) return "Your account was not found.";
            var members = await MemberIdsAsync(conversationId);
            if (!members.Contains(meId)) return "You are not a member of this chat.";

            var others = members.Where(id => id != meId).ToList();
            if (others.Count == 0) return "Nobody else is left in this chat.";
            foreach (var id in others)
            {
                if (!people.TryGetValue(id, out var other) || !await CanChatAsync(me, other))
                {
                    var name = people.TryGetValue(id, out var o) ? o.Name : "a member";
                    return "You can no longer write to " + name + " here (chat rules: Admin > Setup > Chat Rules).";
                }
            }

            return null;
        }

        /// <summary>Opens the one-to-one chat with another user, creating it the first time. Null + error when not allowed.</summary>
        public async Task<(int? Id, string? Error)> StartDirectAsync(int meId, int otherId)
        {
            var people = await PeopleAsync();
            if (meId == otherId || !people.TryGetValue(meId, out var me) || !people.TryGetValue(otherId, out var other))
            {
                return (null, "That user was not found.");
            }

            var key = Math.Min(meId, otherId) + "-" + Math.Max(meId, otherId);
            var existing = await _db.ChatConversations.FirstOrDefaultAsync(c => c.DirectKey == key);
            if (existing != null)
            {
                // an old chat stays readable even when the rules no longer allow writing
                return (existing.Id, null);
            }

            if (!await CanChatAsync(me, other))
            {
                return (null, "You cannot start a chat with " + other.Name + " (chat rules).");
            }

            var conversation = new ChatConversation { IsGroup = false, DirectKey = key, CreatedByUserId = meId };
            conversation.Participants.Add(new ChatParticipant { UserId = meId });
            conversation.Participants.Add(new ChatParticipant { UserId = otherId });
            _db.ChatConversations.Add(conversation);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // both opened it at the same moment: the unique DirectKey kept one
                _db.ChangeTracker.Clear();
                var again = await _db.ChatConversations.FirstOrDefaultAsync(c => c.DirectKey == key);
                return again != null ? (again.Id, null) : throw new InvalidOperationException("Chat could not be created.");
            }

            await PushConversationAsync(conversation.Id, new[] { meId, otherId });
            return (conversation.Id, null);
        }

        /// <summary>Checks that every new member may talk to everybody in the group (and to each other).</summary>
        private async Task<string?> GroupProblemAsync(IReadOnlyCollection<int> existing, IReadOnlyCollection<int> added)
        {
            var people = await PeopleAsync();
            var max = Math.Max(2, _settings.GetInt(ChatSettingKeys.MaxGroupSize, 50));
            if (existing.Count + added.Count > max) return "A group can have at most " + max + " members.";

            var all = existing.Concat(added).Distinct().ToList();
            foreach (var id in added)
            {
                if (!people.TryGetValue(id, out var p) || !p.Active) return "One of the chosen users was not found or is disabled.";
                foreach (var otherId in all.Where(o => o != id))
                {
                    var other = people[otherId];
                    if (!await CanChatAsync(p, other))
                    {
                        return p.Name + " and " + other.Name + " may not chat with each other, so they cannot be in the same group (chat rules).";
                    }
                }
            }

            return null;
        }

        public async Task<(int? Id, string? Error)> CreateGroupAsync(int meId, string? title, IEnumerable<int> memberIds)
        {
            var people = await PeopleAsync();
            if (!people.TryGetValue(meId, out var me)) return (null, "Your account was not found.");
            if (!MayCreateGroups(me)) return (null, "You cannot create group chats.");

            title = (title ?? string.Empty).Trim();
            if (title.Length == 0 || title.Length > 100) return (null, "Give the group a name (up to 100 characters).");

            var members = memberIds.Where(id => id != meId).Distinct().ToList();
            if (members.Count == 0) return (null, "Choose at least one member.");

            var problem = await GroupProblemAsync(new[] { meId }, members);
            if (problem != null) return (null, problem);

            var conversation = new ChatConversation { IsGroup = true, Title = title, CreatedByUserId = meId, LastMessageOn = DateTime.Now };
            conversation.Participants.Add(new ChatParticipant { UserId = meId, IsOwner = true });
            foreach (var id in members) conversation.Participants.Add(new ChatParticipant { UserId = id });
            _db.ChatConversations.Add(conversation);
            await _db.SaveChangesAsync();

            await AddSystemAsync(conversation.Id, me.Name + " created the group \"" + title + "\".");
            await PushConversationAsync(conversation.Id, members.Append(meId));
            return (conversation.Id, null);
        }

        public async Task<string?> AddMembersAsync(int conversationId, int meId, IEnumerable<int> memberIds)
        {
            var conversation = await _db.ChatConversations.FindAsync(conversationId);
            var mine = await MembershipAsync(conversationId, meId);
            if (conversation == null || !conversation.IsGroup || mine == null) return "Group not found.";

            var people = await PeopleAsync();
            var me = people[meId];
            if (!ChatRole.IsXfl(me.Role) && !mine.IsOwner) return "Only the group owner or XFL staff can add members.";

            var existing = await MemberIdsAsync(conversationId);
            var added = memberIds.Where(id => !existing.Contains(id)).Distinct().ToList();
            if (added.Count == 0) return "Choose somebody who is not in the group yet.";

            var problem = await GroupProblemAsync(existing, added);
            if (problem != null) return problem;

            foreach (var id in added)
            {
                var old = await _db.ChatParticipants.FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == id);
                if (old != null)
                {
                    old.LeftOn = null;
                    old.JoinedOn = DateTime.Now;
                }
                else
                {
                    _db.ChatParticipants.Add(new ChatParticipant { ConversationId = conversationId, UserId = id });
                }
            }

            await _db.SaveChangesAsync();
            await AddSystemAsync(conversationId, me.Name + " added " + string.Join(", ", added.Select(id => people[id].Name)) + ".");
            await PushConversationAsync(conversationId, existing.Concat(added));
            return null;
        }

        /// <summary>Removes a member (the owner or XFL staff removing somebody, or anybody leaving: memberId = meId).</summary>
        public async Task<string?> RemoveMemberAsync(int conversationId, int meId, int memberId)
        {
            var conversation = await _db.ChatConversations.FindAsync(conversationId);
            var mine = await MembershipAsync(conversationId, meId);
            if (conversation == null || !conversation.IsGroup || mine == null) return "Group not found.";

            var people = await PeopleAsync();
            if (memberId != meId && !mine.IsOwner && !ChatRole.IsXfl(people[meId].Role)) return "Only the group owner can remove members.";

            var target = await MembershipAsync(conversationId, memberId);
            if (target == null) return "That person is not in the group.";

            var before = await MemberIdsAsync(conversationId);
            target.LeftOn = DateTime.Now;
            if (target.IsOwner)
            {
                // the group keeps an owner: the longest-standing remaining member
                target.IsOwner = false;
                var next = await Active(conversationId).Where(p => p.UserId != memberId).OrderBy(p => p.JoinedOn).FirstOrDefaultAsync();
                if (next != null) next.IsOwner = true;
            }

            await _db.SaveChangesAsync();
            var name = people.TryGetValue(memberId, out var p) ? p.Name : "A member";
            await AddSystemAsync(conversationId, memberId == meId ? name + " left the group." : people[meId].Name + " removed " + name + ".");
            await PushConversationAsync(conversationId, before);
            return null;
        }

        public async Task<string?> RenameAsync(int conversationId, int meId, string? title)
        {
            var conversation = await _db.ChatConversations.FindAsync(conversationId);
            var mine = await MembershipAsync(conversationId, meId);
            if (conversation == null || !conversation.IsGroup || mine == null) return "Group not found.";
            var people = await PeopleAsync();
            if (!mine.IsOwner && !ChatRole.IsXfl(people[meId].Role)) return "Only the group owner can rename it.";

            title = (title ?? string.Empty).Trim();
            if (title.Length == 0 || title.Length > 100) return "Give the group a name (up to 100 characters).";
            if (title == conversation.Title) return null;

            conversation.Title = title;
            await _db.SaveChangesAsync();
            await AddSystemAsync(conversationId, people[meId].Name + " renamed the group to \"" + title + "\".");
            await PushConversationAsync(conversationId, await MemberIdsAsync(conversationId));
            return null;
        }

        /// <summary>The user's chats for the list: newest first, with the last line and the unread count.</summary>
        public async Task<List<object>> ConversationListAsync(int meId)
        {
            var people = await PeopleAsync();
            var memberships = await _db.ChatParticipants.AsNoTracking()
                .Where(p => p.UserId == meId && p.LeftOn == null)
                .Select(p => new { p.ConversationId, p.LastReadMessageId, p.IsOwner })
                .ToListAsync();
            var ids = memberships.Select(m => m.ConversationId).ToList();
            if (ids.Count == 0) return new List<object>();

            var conversations = await _db.ChatConversations.AsNoTracking().Where(c => ids.Contains(c.Id)).ToListAsync();
            var members = (await _db.ChatParticipants.AsNoTracking()
                    .Where(p => ids.Contains(p.ConversationId) && p.LeftOn == null)
                    .Select(p => new { p.ConversationId, p.UserId, p.IsOwner, p.LastReadMessageId })
                    .ToListAsync())
                .GroupBy(p => p.ConversationId).ToDictionary(g => g.Key, g => g.ToList());

            var lastIds = await _db.ChatMessages.AsNoTracking()
                .Where(m => ids.Contains(m.ConversationId))
                .GroupBy(m => m.ConversationId).Select(g => g.Max(m => m.Id)).ToListAsync();
            var last = await _db.ChatMessages.AsNoTracking().Where(m => lastIds.Contains(m.Id))
                .Select(m => new { m.ConversationId, m.SenderUserId, m.Body, m.IsSystem, m.DeletedOn, m.SentOn, Files = m.Attachments.Count })
                .ToDictionaryAsync(m => m.ConversationId);

            var unread = (await UnreadQuery(meId).GroupBy(m => m.ConversationId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Key, x => x.Count);

            var list = new List<(DateTime Sort, object Row)>();
            foreach (var c in conversations)
            {
                var m = members.TryGetValue(c.Id, out var list2) ? list2 : new();
                var other = c.IsGroup ? null : m.Select(x => x.UserId).Where(id => id != meId).Select(id => people.GetValueOrDefault(id)).FirstOrDefault();
                last.TryGetValue(c.Id, out var l);
                string preview = "";
                if (l != null)
                {
                    var who = l.IsSystem ? "" : l.SenderUserId == meId ? "You: " : c.IsGroup && l.SenderUserId != null && people.TryGetValue(l.SenderUserId.Value, out var s) ? s.Name.Split(' ')[0] + ": " : "";
                    preview = l.DeletedOn != null ? who + "Message deleted" : who + (string.IsNullOrWhiteSpace(l.Body) && l.Files > 0 ? "📎 " + l.Files + " file(s)" : l.Body);
                    if (preview.Length > 90) preview = preview.Substring(0, 90) + "…";
                }

                var sort = l?.SentOn ?? c.LastMessageOn ?? c.CreatedOn;
                list.Add((sort, new
                {
                    id = c.Id,
                    isGroup = c.IsGroup,
                    title = c.IsGroup ? c.Title : other?.Name ?? "(deleted user)",
                    subtitle = c.IsGroup ? m.Count + " members" : other?.Subtitle,
                    otherId = other?.Id,
                    otherActive = other?.Active ?? false,
                    preview,
                    time = sort.ToString("o"),
                    unread = unread.GetValueOrDefault(c.Id),
                    isOwner = memberships.First(x => x.ConversationId == c.Id).IsOwner
                }));
            }

            return list.OrderByDescending(x => x.Sort).Select(x => x.Row).ToList();
        }

        private IQueryable<ChatMessage> UnreadQuery(int meId) =>
            from p in _db.ChatParticipants
            where p.UserId == meId && p.LeftOn == null
            from m in _db.ChatMessages
            where m.ConversationId == p.ConversationId && m.Id > (p.LastReadMessageId ?? 0)
                  && !m.IsSystem && m.DeletedOn == null && (m.SenderUserId == null || m.SenderUserId != meId)
            select m;

        public Task<int> UnreadTotalAsync(int meId) => UnreadQuery(meId).CountAsync();

        /// <summary>Details of one chat: title, members (with owner flag) and what the user may do there.</summary>
        public async Task<object?> ConversationInfoAsync(int conversationId, int meId)
        {
            var mine = await MembershipAsync(conversationId, meId);
            var conversation = await _db.ChatConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == conversationId);
            if (mine == null || conversation == null) return null;

            var people = await PeopleAsync();
            var me = people[meId];
            var members = await Active(conversationId).AsNoTracking().Select(p => new { p.UserId, p.IsOwner, p.LastReadMessageId }).ToListAsync();
            var other = conversation.IsGroup ? null : members.Where(x => x.UserId != meId).Select(x => people.GetValueOrDefault(x.UserId)).FirstOrDefault();
            var reason = await CannotSendReasonAsync(conversationId, meId);
            var manage = conversation.IsGroup && (mine.IsOwner || ChatRole.IsXfl(me.Role));

            return new
            {
                id = conversation.Id,
                isGroup = conversation.IsGroup,
                title = conversation.IsGroup ? conversation.Title : other?.Name ?? "(deleted user)",
                subtitle = conversation.IsGroup ? members.Count + " members" : other?.Subtitle,
                canSend = reason == null,
                reason,
                canSendFiles = MaySendFiles(me),
                canManage = manage,
                canAdd = manage && _settings.GetBool(ChatSettingKeys.GroupsEnabled),
                isOwner = mine.IsOwner,
                editMinutes = _settings.GetInt(ChatSettingKeys.EditMinutes, 15),
                members = members.Select(x =>
                {
                    var p = people.GetValueOrDefault(x.UserId);
                    return new { id = x.UserId, name = p?.Name ?? "(deleted user)", subtitle = p?.Subtitle, owner = x.IsOwner, active = p?.Active ?? false, lastRead = x.LastReadMessageId ?? 0 };
                }).OrderByDescending(x => x.owner).ThenBy(x => x.name)
            };
        }

        // ---------------------------------------------------------------- messages

        public async Task<List<object>> MessagesAsync(int conversationId, long? before, long? after, int take = 50)
        {
            var query = _db.ChatMessages.AsNoTracking().Where(m => m.ConversationId == conversationId);
            if (after != null)
            {
                query = query.Where(m => m.Id > after).OrderBy(m => m.Id).Take(200);
            }
            else
            {
                if (before != null) query = query.Where(m => m.Id < before);
                query = query.OrderByDescending(m => m.Id).Take(take);
            }

            var rows = await query.Include(m => m.Attachments).ToListAsync();
            var people = await PeopleAsync();
            return rows.OrderBy(m => m.Id).Select(m => Dto(m, people)).ToList();
        }

        private static object Dto(ChatMessage m, Dictionary<int, ChatPerson> people) => new
        {
            id = m.Id,
            conversationId = m.ConversationId,
            senderId = m.SenderUserId,
            senderName = m.SenderUserId != null && people.TryGetValue(m.SenderUserId.Value, out var p) ? p.Name : m.IsSystem ? "" : "(deleted user)",
            body = m.DeletedOn != null ? "" : m.Body,
            system = m.IsSystem,
            time = m.SentOn.ToString("o"),
            edited = m.EditedOn != null,
            deleted = m.DeletedOn != null,
            files = m.DeletedOn != null ? Array.Empty<object>() : m.Attachments.Select(a => (object)new
            {
                id = a.Id,
                name = a.FileName,
                size = a.Size,
                image = a.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            }).ToArray()
        };

        private async Task AddSystemAsync(int conversationId, string text)
        {
            var message = new ChatMessage { ConversationId = conversationId, IsSystem = true, Body = text.Length > MaxBodyLength ? text.Substring(0, MaxBodyLength) : text };
            _db.ChatMessages.Add(message);
            var conversation = await _db.ChatConversations.FindAsync(conversationId);
            if (conversation != null) conversation.LastMessageOn = message.SentOn;
            await _db.SaveChangesAsync();
            await PushMessageAsync(message, await MemberIdsAsync(conversationId));
        }

        /// <summary>Sends a message with optional files. Returns the error text, or null.</summary>
        public async Task<string?> SendAsync(int conversationId, int meId, string? body, IReadOnlyList<IFormFile>? files)
        {
            var reason = await CannotSendReasonAsync(conversationId, meId);
            if (reason != null) return reason;

            body = (body ?? string.Empty).Trim();
            files ??= Array.Empty<IFormFile>();
            var realFiles = files.Where(f => f != null && f.Length > 0).ToList();
            if (body.Length == 0 && realFiles.Count == 0) return "Write a message or choose a file.";
            if (body.Length > MaxBodyLength) return "A message can be at most " + MaxBodyLength + " characters.";

            var people = await PeopleAsync();
            if (realFiles.Count > 0)
            {
                if (!MaySendFiles(people[meId])) return "You cannot send files in chats.";
                var allowed = _settings.AllowedExtensions;
                var maxBytes = Math.Max(1, _settings.GetInt(ChatSettingKeys.MaxFileMb, 20)) * 1024L * 1024L;
                if (realFiles.Count > 10) return "Send at most 10 files at a time.";
                foreach (var f in realFiles)
                {
                    var name = Path.GetFileName((f.FileName ?? string.Empty).Replace('\\', '/'));
                    if (!allowed.Contains(Path.GetExtension(name).ToLowerInvariant()))
                        return name + ": this file type is not allowed (" + string.Join(" ", allowed) + ").";
                    if (f.Length > maxBytes) return name + " is larger than " + (maxBytes / 1024 / 1024) + " MB.";
                }
            }

            var message = new ChatMessage { ConversationId = conversationId, SenderUserId = meId, Body = body };
            if (realFiles.Count > 0)
            {
                Directory.CreateDirectory(FileFolder);
                foreach (var f in realFiles)
                {
                    var name = Path.GetFileName((f.FileName ?? string.Empty).Replace('\\', '/'));
                    var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(name).ToLowerInvariant();
                    await using (var stream = new FileStream(Path.Combine(FileFolder, stored), FileMode.CreateNew))
                    {
                        await f.CopyToAsync(stream);
                    }

                    message.Attachments.Add(new ChatAttachment
                    {
                        FileName = name.Length > 255 ? name.Substring(name.Length - 255) : name,
                        StoredName = stored,
                        ContentType = TicketService.GetContentType(name),
                        Size = f.Length
                    });
                }
            }

            _db.ChatMessages.Add(message);
            var conversation = await _db.ChatConversations.FindAsync(conversationId);
            if (conversation != null) conversation.LastMessageOn = message.SentOn;
            await _db.SaveChangesAsync();

            // the sender has read their own message
            var mine = await MembershipAsync(conversationId, meId);
            if (mine != null)
            {
                mine.LastReadMessageId = message.Id;
                await _db.SaveChangesAsync();
            }

            await PushMessageAsync(message, await MemberIdsAsync(conversationId));
            return null;
        }

        /// <summary>Edits (body) or deletes (body null) the user's own message within the allowed minutes.</summary>
        public async Task<string?> ChangeAsync(long messageId, int meId, string? body, bool delete)
        {
            var message = await _db.ChatMessages.Include(m => m.Attachments).FirstOrDefaultAsync(m => m.Id == messageId);
            if (message == null || message.SenderUserId != meId || message.IsSystem || message.DeletedOn != null) return "Message not found.";
            if (await MembershipAsync(message.ConversationId, meId) == null) return "Message not found.";

            var minutes = _settings.GetInt(ChatSettingKeys.EditMinutes, 15);
            if (minutes <= 0 || message.SentOn.AddMinutes(minutes) < DateTime.Now)
                return minutes <= 0 ? "Messages cannot be changed once sent." : "Messages can only be changed within " + minutes + " minutes.";

            if (delete)
            {
                message.DeletedOn = DateTime.Now;
                message.Body = string.Empty;
                foreach (var a in message.Attachments.ToList())
                {
                    TryDelete(Path.Combine(FileFolder, a.StoredName));
                    _db.ChatAttachments.Remove(a);
                }
            }
            else
            {
                body = (body ?? string.Empty).Trim();
                if (body.Length == 0 && message.Attachments.Count == 0) return "A message cannot be empty.";
                if (body.Length > MaxBodyLength) return "A message can be at most " + MaxBodyLength + " characters.";
                message.Body = body;
                message.EditedOn = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            await PushMessageAsync(message, await MemberIdsAsync(message.ConversationId));
            return null;
        }

        /// <summary>Marks the chat read up to a message (or the latest one).</summary>
        public async Task MarkReadAsync(int conversationId, int meId, long? upTo)
        {
            var mine = await MembershipAsync(conversationId, meId);
            if (mine == null) return;
            var latest = upTo ?? await _db.ChatMessages.Where(m => m.ConversationId == conversationId).MaxAsync(m => (long?)m.Id) ?? 0;
            if (latest <= (mine.LastReadMessageId ?? 0)) return;

            mine.LastReadMessageId = latest;
            await _db.SaveChangesAsync();

            // the others see "Seen", the user's other tabs update their unread counts
            await PushAsync(await MemberIdsAsync(conversationId), new { type = "read", conversationId, userId = meId, messageId = latest });
        }

        /// <summary>A file of a chat the user is (still) in, or null.</summary>
        public async Task<(ChatAttachment File, string Path)?> FileAsync(long attachmentId, int meId)
        {
            var file = await _db.ChatAttachments.AsNoTracking().Include(a => a.Message).FirstOrDefaultAsync(a => a.Id == attachmentId);
            if (file?.Message == null || file.Message.DeletedOn != null) return null;
            if (await MembershipAsync(file.Message.ConversationId, meId) == null) return null;

            // StoredName is generated (guid + extension); anything else is not served
            if (file.StoredName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || file.StoredName.Contains("..")) return null;
            var path = Path.Combine(FileFolder, file.StoredName);
            return File.Exists(path) ? (file, path) : null;
        }

        private void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _logger.LogWarning(ex, "Chat file {Path} could not be deleted", path); }
        }

        // ---------------------------------------------------------------- real time

        private async Task PushMessageAsync(ChatMessage message, IEnumerable<int> userIds)
        {
            var people = await PeopleAsync();
            await PushAsync(userIds, new { type = "message", conversationId = message.ConversationId, message = Dto(message, people), toast = _settings.GetBool(ChatSettingKeys.Toasts) });
        }

        private Task PushConversationAsync(int conversationId, IEnumerable<int> userIds) =>
            PushAsync(userIds, new { type = "conversation", conversationId });

        private async Task PushAsync(IEnumerable<int> userIds, object payload)
        {
            try
            {
                var groups = userIds.Distinct().Select(NotificationHub.GroupFor).ToList();
                if (groups.Count > 0) await _hub.Clients.Groups(groups).SendAsync("chat", payload);
            }
            catch (Exception ex)
            {
                // the message is saved; the others get it on their next refresh
                _logger.LogWarning(ex, "Chat push failed");
            }
        }
    }
}
