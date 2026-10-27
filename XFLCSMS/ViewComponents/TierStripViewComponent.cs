using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Register;
using XFLCSMS.Models.Tiers;
using XFLCSMS.Services;

namespace XFLCSMS.ViewComponents
{
    /// <summary>
    /// Support level strip of the Admin, Support Manager and Support Engineer dashboards: open tickets, queue and
    /// "escalation due" per level (L1-L5) and the development work in progress. An engineer also sees their level queue.
    /// </summary>
    public class TierStripViewComponent : ViewComponent
    {
        private readonly DataContext _db;
        private readonly TierService _tiers;

        public TierStripViewComponent(DataContext db, TierService tiers)
        {
            _db = db;
            _tiers = tiers;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var controller = (string?)ViewContext.RouteData.Values["controller"] ?? string.Empty;
            var role = controller switch
            {
                "Admin" => "Admin",
                "SupportManegar" => "SupportManager",
                "SupportEngineer" => "SupportEngineer",
                _ => "Maker"
            };
            if (role == "Maker")
            {
                return Content(string.Empty);
            }

            var now = DateTime.Now;
            var levels = await _tiers.LevelsAsync();
            var open = await _db.Issues.AsNoTracking().Where(i => i.IStatus != "Close")
                .Select(i => new { i.SupportLevel, i.LevelSince, i.TDate, Unassigned = i.AssignedToUserId == null && (i.AssignBy == null || i.AssignBy == "") })
                .ToListAsync();
            var view = new TierStripView { Controller = controller, Role = role };
            foreach (var l in levels)
            {
                var at = open.Where(o => SupportLevel.Clamp(o.SupportLevel) == l.Level).ToList();
                var due = l.MaxHoursAtLevel > 0 ? at.Count(o => (o.LevelSince ?? o.TDate).AddHours(l.MaxHoursAtLevel) < now) : 0;
                view.Levels.Add((l, at.Count, at.Count(o => o.Unassigned), due));
            }

            var dev = await _db.Issues.AsNoTracking().Where(i => i.DevStage != null && i.DevStage != DevStages.Deployed).Select(i => i.DevEta).ToListAsync();
            view.InDevelopment = dev.Count;
            view.DevOverdue = dev.Count(e => e.HasValue && e.Value.Date < DateTime.Today);

            if (role == "SupportEngineer")
            {
                var json = HttpContext.Session.GetString(SessionAuthorizeAttribute.SupportEngineer);
                var me = string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<User>(json);
                if (me != null)
                {
                    var level = SupportLevel.Clamp(await _db.Users.AsNoTracking().Where(u => u.Id == me.Id).Select(u => u.SupportLevel).FirstOrDefaultAsync());
                    view.MyLevel = level;
                    view.MyQueue = open.Count(o => o.Unassigned && SupportLevel.Clamp(o.SupportLevel) <= level);
                }
            }

            return View(view);
        }
    }
}
