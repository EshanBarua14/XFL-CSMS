using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Models.Chat;
using XFLCSMS.Services.Chat;

namespace XFLCSMS.Controllers
{
    /// <summary>Admin > Setup > Chat Rules: who may chat with whom, groups, file sharing.</summary>
    public partial class AdminController
    {
        [HttpGet]
        public IActionResult ChatSettings([FromServices] ChatSettingsStore settings)
        {
            var view = new ChatSettingsAdminView();
            foreach (var f in ChatSettingKeys.Fields) view.Values[f.Key] = settings.Get(f.Key);
            foreach (var (a, b) in ChatRule.Pairs()) view.Rules[ChatRule.Key(a, b)] = settings.RuleFor(a, b);
            return View(view);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChatSettings(string? reset, [FromServices] ChatSettingsStore settings)
        {
            try
            {
                var form = Request.Form;
                var save = new Dictionary<string, string?>();

                foreach (var f in ChatSettingKeys.Fields)
                {
                    // a switch posts "false" (hidden field) and, when on, "true" as well
                    string? v = f.Type == "bool" ? (form[f.Key].Contains("true") ? "true" : "false") : form[f.Key].ToString();
                    v = v?.Trim();
                    switch (f.Type)
                    {
                        case "bool":
                            save[f.Key] = reset != null ? null : v;
                            break;
                        case "number":
                            if (reset == null && (!int.TryParse(v, out var n) || n < 0 || n > 100000))
                            {
                                TempData["ErrorMessage"] = f.Label + ": enter a whole number.";
                                return RedirectToAction(nameof(ChatSettings));
                            }
                            save[f.Key] = reset != null ? null : v;
                            break;
                        default:
                            save[f.Key] = reset != null ? null : v;
                            break;
                    }
                }

                foreach (var (a, b) in ChatRule.Pairs())
                {
                    var key = ChatRule.Key(a, b);
                    var v = form[key].ToString();
                    save[key] = reset != null || !ChatRule.IsValid(v) ? null : v;
                }

                await settings.SaveAsync(_context, save);
                TempData["SuccessMessage"] = reset != null ? "Chat rules are back to the defaults." : "Chat rules saved. They apply to new messages straight away.";
                return RedirectToAction(nameof(ChatSettings));
            }
            catch (Exception ex)
            {
                return HandleError(ex);
            }
        }
    }
}
