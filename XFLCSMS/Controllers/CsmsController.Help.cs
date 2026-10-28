using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Common;

namespace XFLCSMS.Controllers
{
    /// <summary>"How Xpert CSMS works": the roles, the ticket workflow and what the signed-in role does in it.</summary>
    public abstract partial class CsmsController
    {
        [HttpGet]
        public async Task<IActionResult> Help()
        {
            return View("~/Views/Shared/Help.cshtml", new HelpView
            {
                Layout = RoleLayout,
                Controller = RoleController,
                Role = SessionKey switch
                {
                    SessionAuthorizeAttribute.Admin => "Admin",
                    SessionAuthorizeAttribute.SupportManager => "SupportManager",
                    SessionAuthorizeAttribute.SupportEngineer => "SupportEngineer",
                    SessionAuthorizeAttribute.Viewer => "Viewer",
                    _ => "Maker"
                },
                Levels = await Tiers.LevelsAsync()
            });
        }
    }
}
