using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Models.Tiers;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    /// <summary>Admin > Setup > Support Levels: names, time limits and authority of L1-L5 and the level of every Support Engineer.</summary>
    public partial class AdminController
    {
        [HttpGet]
        public async Task<IActionResult> SupportLevels([FromServices] TierService tiers)
        {
            try
            {
                return View(new SupportLevelsAdminView
                {
                    Levels = await tiers.LevelsAsync(),
                    Engineers = await tiers.EngineersAsync(activeOnly: false)
                });
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupportLevels(List<SupportLevel> levels, List<EngineerLevelForm> engineers, [FromServices] TierService tiers)
        {
            try
            {
                var error = await tiers.SaveAdminAsync(levels, engineers);
                if (error != null)
                {
                    TempData["ErrorMessage"] = error;
                }
                else
                {
                    TempData["SuccessMessage"] = "Support levels saved.";
                }

                return RedirectToAction(nameof(SupportLevels));
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        /// <summary>Admin > Setup > Ticket Assignment: self-assign, auto-assign and transfer requests.</summary>
        [HttpGet]
        public async Task<IActionResult> TicketAssignment([FromServices] AssignmentService assignment)
        {
            try
            {
                return View(await assignment.SettingsAsync());
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TicketAssignment(AssignmentSettings form, [FromServices] AssignmentService assignment)
        {
            try
            {
                var error = await assignment.SaveSettingsAsync(form, CurrentUser!);
                if (error != null)
                {
                    TempData["ErrorMessage"] = error;
                    return View(form);
                }

                AutoAssignWorker.Wake();
                TempData["SuccessMessage"] = "Ticket assignment saved.";
                return RedirectToAction(nameof(TicketAssignment));
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
