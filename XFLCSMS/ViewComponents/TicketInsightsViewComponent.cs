using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Register;
using XFLCSMS.Services;

namespace XFLCSMS.ViewComponents
{
    /// <summary>
    /// Charts block of the four dashboards: KPIs, created / closed trend, backlog by status and age, priority with
    /// SLA, products and houses for the chosen period (?days=7|30|90|365). Scope follows the role: Admin and
    /// Support Manager see every ticket, a Support Engineer his own work, a Maker the tickets he raised.
    /// </summary>
    public class TicketInsightsViewComponent : ViewComponent
    {
        private readonly TicketReportService _reports;

        public TicketInsightsViewComponent(TicketReportService reports)
        {
            _reports = reports;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var controller = (string?)ViewContext.RouteData.Values["controller"] ?? string.Empty;
            var (key, role, preset) = controller switch
            {
                "Admin" => (SessionAuthorizeAttribute.Admin, "Admin", (string?)null),
                "SupportManegar" => (SessionAuthorizeAttribute.SupportManager, "SupportManager", null),
                "SupportEngineer" => (SessionAuthorizeAttribute.SupportEngineer, "SupportEngineer", "engineer"),
                _ => (SessionAuthorizeAttribute.Maker, "Maker", null)
            };

            var json = HttpContext.Session.GetString(key);
            if (string.IsNullOrEmpty(json))
            {
                return Content(string.Empty);
            }

            var user = JsonConvert.DeserializeObject<User>(json)!;
            var days = int.TryParse(Request.Query["days"], out var d) && new[] { 7, 30, 90, 365 }.Contains(d) ? d : 30;
            var to = DateTime.Today;
            var from = to.AddDays(-(days - 1));
            var summary = await _reports.SummaryAsync(user, role, preset, from, to);

            ViewBag.Days = days;
            ViewBag.Role = role;
            ViewBag.Controller = controller;
            ViewBag.Scope = role switch
            {
                "SupportEngineer" => "Tickets assigned to or closed by you",
                "Maker" => "Tickets you raised",
                _ => "All tickets"
            };
            return View(summary);
        }
    }
}
