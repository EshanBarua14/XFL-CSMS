using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Models.Chat;
using XFLCSMS.Services.Chat;

namespace XFLCSMS.Controllers
{
    /// <summary>Messages: one-to-one and group chats with files, for every role. Rules: Admin > Setup > Chat Rules.</summary>
    public abstract partial class CsmsController
    {
        private ChatService ChatSvc => HttpContext.RequestServices.GetRequiredService<ChatService>();

        // read-only Viewer accounts have no chat
        private bool ChatOff => SessionKey == Infrastructure.SessionAuthorizeAttribute.Viewer
            || !HttpContext.RequestServices.GetRequiredService<ChatSettingsStore>().Enabled;

        private IActionResult ChatDisabled() => NotFound(new { error = "Chat is switched off." });

        private IActionResult ChatResult(string? error) => error == null ? Json(new { ok = true }) : BadRequest(new { error });

        /// <summary>The Messages page. ?with=userId opens (or starts) the chat with that user, ?c=id a chat.</summary>
        [HttpGet]
        public async Task<IActionResult> Chat(int? with, int? c)
        {
            if (ChatOff) return ChatDisabled();
            var people = await ChatSvc.PeopleAsync();
            var me = people[CurrentUser!.Id];
            return View("~/Views/Shared/Chat/Chat.cshtml", new ChatPageView
            {
                Layout = RoleLayout,
                Controller = (string)RouteData.Values["controller"]!,
                MeId = me.Id,
                MeName = me.Name,
                CanCreateGroups = ChatSvc.MayCreateGroups(me),
                OpenWith = with,
                OpenConversation = c
            });
        }

        [HttpGet]
        public async Task<IActionResult> ChatUnread()
        {
            if (ChatOff) return Json(new { unread = 0, enabled = false });
            return Json(new { unread = await ChatSvc.UnreadTotalAsync(CurrentUser!.Id), enabled = true });
        }

        [HttpGet]
        public async Task<IActionResult> ChatConversations()
        {
            if (ChatOff) return ChatDisabled();
            return Json(await ChatSvc.ConversationListAsync(CurrentUser!.Id));
        }

        [HttpGet]
        public async Task<IActionResult> ChatContacts()
        {
            if (ChatOff) return ChatDisabled();
            var contacts = await ChatSvc.ContactsAsync(CurrentUser!.Id);
            return Json(contacts.Select(p => new { id = p.Id, name = p.Name, subtitle = p.Subtitle, xfl = ChatRole.IsXfl(p.Role) }));
        }

        [HttpGet]
        public async Task<IActionResult> ChatInfo(int id)
        {
            if (ChatOff) return ChatDisabled();
            var info = await ChatSvc.ConversationInfoAsync(id, CurrentUser!.Id);
            return info == null ? NotFound(new { error = "Chat not found." }) : Json(info);
        }

        [HttpGet]
        public async Task<IActionResult> ChatMessages(int id, long? before, long? after)
        {
            if (ChatOff) return ChatDisabled();
            if (await ChatSvc.MembershipAsync(id, CurrentUser!.Id) == null) return NotFound(new { error = "Chat not found." });
            return Json(await ChatSvc.MessagesAsync(id, before, after));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatStart(int userId)
        {
            if (ChatOff) return ChatDisabled();
            var (id, error) = await ChatSvc.StartDirectAsync(CurrentUser!.Id, userId);
            return id != null ? Json(new { id }) : BadRequest(new { error });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatCreateGroup(string? title, List<int>? members)
        {
            if (ChatOff) return ChatDisabled();
            var (id, error) = await ChatSvc.CreateGroupAsync(CurrentUser!.Id, title, members ?? new List<int>());
            return id != null ? Json(new { id }) : BadRequest(new { error });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatAddMembers(int id, List<int>? members)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.AddMembersAsync(id, CurrentUser!.Id, members ?? new List<int>()));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatRemoveMember(int id, int userId)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.RemoveMemberAsync(id, CurrentUser!.Id, userId));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatLeave(int id)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.RemoveMemberAsync(id, CurrentUser!.Id, CurrentUser!.Id));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatRename(int id, string? title)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.RenameAsync(id, CurrentUser!.Id, title));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(105 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 105 * 1024 * 1024)]
        public async Task<IActionResult> ChatSend(int id, string? body, List<IFormFile>? files)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.SendAsync(id, CurrentUser!.Id, body, files));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatEdit(long messageId, string? body)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.ChangeAsync(messageId, CurrentUser!.Id, body, delete: false));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatDelete(long messageId)
        {
            if (ChatOff) return ChatDisabled();
            return ChatResult(await ChatSvc.ChangeAsync(messageId, CurrentUser!.Id, null, delete: true));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatRead(int id, long? upTo)
        {
            if (ChatOff) return ChatDisabled();
            await ChatSvc.MarkReadAsync(id, CurrentUser!.Id, upTo);
            return Json(new { ok = true });
        }

        /// <summary>A shared file, for members of the chat only. Images open in the page (inline=true), everything else downloads.</summary>
        [HttpGet]
        public async Task<IActionResult> ChatFile(long id, bool inline = false)
        {
            if (ChatOff) return ChatDisabled();
            var found = await ChatSvc.FileAsync(id, CurrentUser!.Id);
            if (found == null) return NotFound();

            var (file, path) = found.Value;
            var image = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && !file.ContentType.Contains("svg");
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return inline && image
                ? File(stream, file.ContentType)
                : File(stream, file.ContentType, file.FileName);
        }
    }
}
