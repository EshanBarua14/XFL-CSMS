using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Models.Tiers;
using XFLCSMS.Services;

namespace XFLCSMS.Controllers
{
    /// <summary>Admin > Setup > Support Levels: names and time limits of L1-L5 and the level of every Support Engineer.</summary>
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
    }
}
