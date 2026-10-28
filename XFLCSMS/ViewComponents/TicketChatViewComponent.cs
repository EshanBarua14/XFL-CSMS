using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Register;
using XFLCSMS.Services.Chat;

namespace XFLCSMS.ViewComponents
{
    /// <summary>
    /// "Chat with ..." buttons on the ticket pages: the client, the assigned engineer and the developer of the ticket,
    /// each only when the chat rules allow the signed-in user to write to them.
    /// </summary>
    public class TicketChatViewComponent : ViewComponent
    {
        private readonly DataContext _context;
        private readonly ChatService _chat;

        public TicketChatViewComponent(DataContext context, ChatService chat)
        {
            _context = context;
            _chat = chat;
        }

        public async Task<IViewComponentResult> InvokeAsync(int issueId)
        {
            var controller = (string?)ViewContext.RouteData.Values["controller"];
            var json = SessionAuthorizeAttribute.AllKeys.Select(k => HttpContext.Session.GetString(k)).FirstOrDefault(s => !string.IsNullOrEmpty(s));
            var me = json == null ? null : JsonConvert.DeserializeObject<User>(json);
            if (me == null || controller == null || !_chat.Settings.Enabled) return Content(string.Empty);

            var issue = await _context.Issues.AsNoTracking().Where(i => i.IssueId == issueId)
                .Select(i => new { i.UserId, i.AssignedToUserId, i.AssignBy, i.DevOwnerUserId }).FirstOrDefaultAsync();
            if (issue == null) return Content(string.Empty);

            var people = await _chat.PeopleAsync();
            if (!people.TryGetValue(me.Id, out var self)) return Content(string.Empty);

            var buttons = new List<(int Id, string Name, string Role)>();
            foreach (var (id, role) in new[] { ((int?)issue.UserId, "Client"), (await _chat.EngineerOfAsync(issue.AssignedToUserId, issue.AssignBy), "Engineer"), (issue.DevOwnerUserId, "Developer") })
            {
                if (id == null || id == me.Id || buttons.Any(b => b.Id == id) || !people.TryGetValue(id.Value, out var p)) continue;
                if (await _chat.CanChatAsync(self, p)) buttons.Add((p.Id, p.Name, role));
            }

            if (buttons.Count == 0) return Content(string.Empty);
            ViewBag.Controller = controller;
            return View(buttons);
        }
    }
}
