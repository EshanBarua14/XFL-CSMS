using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Services;

namespace XFLCSMS.ViewComponents
{
    /// <summary>
    /// Support level (L1-L5) and development panel of the four Ticket Details pages: where the ticket is, its escalation
    /// history and its development stage, owner, ETA and timeline. Staff also get the escalate and update forms.
    /// </summary>
    public class TicketWorkViewComponent : ViewComponent
    {
        private readonly TierService _tiers;

        public TicketWorkViewComponent(TierService tiers)
        {
            _tiers = tiers;
        }

        public async Task<IViewComponentResult> InvokeAsync(int issueId)
        {
            var controller = (string?)ViewContext.RouteData.Values["controller"] ?? string.Empty;
            var role = controller switch
            {
                "Admin" => "Admin",
                "SupportManegar" => "SupportManager",
                "SupportEngineer" => "SupportEngineer",
                _ => "Maker"
            };

            var view = await _tiers.PanelAsync(issueId, role, controller);
            return view == null ? Content(string.Empty) : View(view);
        }
    }
}
