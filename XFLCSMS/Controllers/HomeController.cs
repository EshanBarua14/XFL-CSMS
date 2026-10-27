using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace XFLCSMS.Controllers
{
    /// <summary>
    /// Error pages only. The old scaffolding pages that lived here (brokerage CRUD, file upload, a second
    /// ticket form) duplicated the Admin pages and were removed.
    /// </summary>
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Login", "RegisterLogin");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>Friendly page for 404 / 403 / ... (UseStatusCodePagesWithReExecute in Program.cs).</summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Status(int id)
        {
            ViewBag.Code = id;
            return View();
        }

        /// <summary>Shown by the service worker when the app is opened without a network connection.</summary>
        public IActionResult Offline()
        {
            return View();
        }
    }
}
