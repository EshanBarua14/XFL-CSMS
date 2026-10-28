using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Notifications;
using XFLCSMS.Services.Notifications;

namespace XFLCSMS.Controllers
{
    /// <summary>The bell, "My notifications" and the user's own notification switches, for every role.</summary>
    public abstract partial class CsmsController
    {
        /// <summary>Layout of this role's pages (shared views pick it up from their model).</summary>
        protected string RoleLayout => SessionKey switch
        {
            SessionAuthorizeAttribute.Admin => "_Layout",
            SessionAuthorizeAttribute.SupportManager => "_SupportManegerLayout",
            SessionAuthorizeAttribute.SupportEngineer => "_SupportEngineerLayout",
            SessionAuthorizeAttribute.Viewer => "_ViewerLayout",
            _ => "_MakerLayout"
        };

        private string RoleController => (string)RouteData.Values["controller"]!;

        private IQueryable<NotificationMessage> MyInApp(int userId) =>
            Db.NotificationMessages.Where(m => m.UserId == userId && m.Channel == NotificationChannel.InApp && m.Status == NotificationStatus.Sent);

        /// <summary>Unread count and the latest in-app notifications for the bell.</summary>
        [HttpGet]
        public async Task<IActionResult> NotificationFeed()
        {
            var me = CurrentUser!.Id;
            var unread = await MyInApp(me).CountAsync(m => m.ReadOn == null);
            var items = await MyInApp(me).OrderByDescending(m => m.Id).Take(10)
                .Select(m => new { id = m.Id, title = m.Subject, body = m.Body, url = m.Url, read = m.ReadOn != null, created = m.CreatedOn })
                .ToListAsync();
            return Json(new { unread, items = items.Select(i => new { i.id, i.title, i.body, i.url, i.read, time = i.created.ToString("dd MMM HH:mm") }) });
        }

        /// <summary>Marks one notification (id) or all of them as read.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificationRead(long? id)
        {
            var me = CurrentUser!.Id;
            var query = MyInApp(me).Where(m => m.ReadOn == null);
            if (id != null) query = query.Where(m => m.Id == id);
            var rows = await query.ToListAsync();
            foreach (var row in rows) row.ReadOn = DateTime.Now;
            await Db.SaveChangesAsync();
            return Json(new { marked = rows.Count });
        }

        /// <summary>Opens the page of a notification and marks it read.</summary>
        [HttpGet]
        public async Task<IActionResult> OpenNotification(long id)
        {
            var me = CurrentUser!.Id;
            var row = await MyInApp(me).FirstOrDefaultAsync(m => m.Id == id);
            if (row == null) return RedirectToAction(nameof(MyNotifications));
            row.ReadOn ??= DateTime.Now;
            await Db.SaveChangesAsync();
            return !string.IsNullOrEmpty(row.Url) && Url.IsLocalUrl(row.Url) ? LocalRedirect(row.Url) : RedirectToAction(nameof(MyNotifications));
        }

        [HttpGet]
        public async Task<IActionResult> MyNotifications(int page = 1)
        {
            const int size = 30;
            page = Math.Max(1, page);
            var me = CurrentUser!.Id;
            var items = await MyInApp(me).OrderByDescending(m => m.Id).Skip((page - 1) * size).Take(size + 1).ToListAsync();
            return View("~/Views/Shared/Notifications/MyNotifications.cshtml", new MyNotificationsView
            {
                Layout = RoleLayout,
                Controller = RoleController,
                Items = items.Take(size).ToList(),
                HasMore = items.Count > size,
                Page = page,
                Unread = await MyInApp(me).CountAsync(m => m.ReadOn == null)
            });
        }

        [HttpGet]
        public async Task<IActionResult> NotificationPreferences([FromServices] NotificationSettingsStore settings)
        {
            return View("~/Views/Shared/Notifications/Preferences.cshtml", await PreferencesAsync(settings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificationPreferences(List<string>? on, [FromServices] NotificationSettingsStore settings)
        {
            var view = await PreferencesAsync(settings);
            var me = CurrentUser!.Id;
            var wanted = new HashSet<string>(on ?? new List<string>());
            var existing = await Db.NotificationOptOuts.Where(o => o.UserId == me).ToListAsync();

            // only switches shown on the page are changed; "*" rows (whole channel muted) come from the mute boxes
            foreach (var row in view.Rows)
            {
                foreach (var channel in row.Channels)
                {
                    var key = row.EventKey + "|" + channel;
                    var current = existing.FirstOrDefault(o => o.EventKey == row.EventKey && o.Channel == channel);
                    if (wanted.Contains(key) && current != null) Db.NotificationOptOuts.Remove(current);
                    if (!wanted.Contains(key) && current == null) Db.NotificationOptOuts.Add(new NotificationOptOut { UserId = me, EventKey = row.EventKey, Channel = channel });
                }
            }

            foreach (var channel in NotificationChannel.All)
            {
                var muted = wanted.Contains("*|" + channel);
                var current = existing.FirstOrDefault(o => o.EventKey == "*" && o.Channel == channel);
                if (!muted && current != null) Db.NotificationOptOuts.Remove(current);
                if (muted && current == null) Db.NotificationOptOuts.Add(new NotificationOptOut { UserId = me, EventKey = "*", Channel = channel });
            }

            await Db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Your notification settings are saved.";
            return RedirectToAction(nameof(NotificationPreferences));
        }

        private async Task<PreferencesView> PreferencesAsync(NotificationSettingsStore settings)
        {
            var me = CurrentUser!;
            var user = await Db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id);
            var role = NotificationRole.Of(user);
            var rules = await Db.NotificationRules.AsNoTracking().Where(r => r.Role == role).ToListAsync();
            var channels = NotificationChannel.All.Where(c => NotificationDispatcher.ChannelOn(settings, c)).ToList();

            var view = new PreferencesView
            {
                Layout = RoleLayout,
                Controller = RoleController,
                Role = role,
                ActiveChannels = channels,
                HasEmail = !string.IsNullOrWhiteSpace(user.Email),
                HasPhone = PhoneNumbers.ToE164(user.PhonNumber, settings.Get(SettingKeys.CountryCode) ?? "880") != null,
                OptedOut = (await Db.NotificationOptOuts.AsNoTracking().Where(o => o.UserId == me.Id).ToListAsync())
                    .Select(o => o.EventKey + "|" + o.Channel).ToHashSet()
            };

            foreach (var e in NotificationCatalog.Events)
            {
                var on = channels.Where(c => NotificationDispatcher.Rule(e, c, role, rules).Enabled).ToList();
                if (on.Count > 0)
                {
                    view.Rows.Add(new PreferenceRow { EventKey = e.Key, Group = e.Group, Name = e.Name, Description = e.Description, Channels = on });
                }
            }

            return view;
        }
    }
}
