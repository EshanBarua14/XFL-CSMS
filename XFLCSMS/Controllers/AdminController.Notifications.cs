using Microsoft.AspNetCore.Mvc;
using XFLCSMS.Models.Notifications;
using XFLCSMS.Services.Notifications;

namespace XFLCSMS.Controllers
{
    /// <summary>Admin > Notifications: who gets what on which channel, texts, providers, test messages and the log.</summary>
    public partial class AdminController
    {
        // ---- event x channel x role switches --------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> Notifications([FromServices] NotificationSettingsStore settings)
        {
            ViewBag.Rules = await _context.NotificationRules.AsNoTracking().ToListAsync();
            ViewBag.ActiveChannels = NotificationChannel.All.Where(c => NotificationDispatcher.ChannelOn(settings, c)).ToList();
            return View("~/Views/Admin/Notifications/Rules.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Notifications(string? reset)
        {
            var existing = await _context.NotificationRules.ToListAsync();
            if (reset == "all")
            {
                _context.NotificationRules.RemoveRange(existing);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "All switches are back to the defaults.";
                return RedirectToAction(nameof(Notifications));
            }

            var form = Request.Form;
            foreach (var e in NotificationCatalog.Events)
            {
                foreach (var role in NotificationRole.All)
                {
                    var audience = form["a|" + e.Key + "|" + role].ToString();
                    if (audience != NotificationAudience.All) audience = NotificationAudience.Related;
                    foreach (var channel in NotificationChannel.All)
                    {
                        var enabled = form.ContainsKey("r|" + e.Key + "|" + channel + "|" + role);
                        var row = existing.FirstOrDefault(r => r.EventKey == e.Key && r.Channel == channel && r.Role == role);
                        if (row == null)
                        {
                            var (defEnabled, defAudience) = NotificationDispatcher.Rule(e, channel, role, Array.Empty<NotificationRule>());
                            if (enabled == defEnabled && (!enabled || audience == defAudience)) continue; // still the default
                            row = new NotificationRule { EventKey = e.Key, Channel = channel, Role = role };
                            _context.NotificationRules.Add(row);
                        }

                        row.Enabled = enabled;
                        row.Audience = audience;
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Notification switches are saved.";
            return RedirectToAction(nameof(Notifications));
        }

        // ---- templates --------------------------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> NotificationTemplates()
        {
            ViewBag.Custom = await _context.NotificationTemplates.AsNoTracking().Select(t => t.EventKey + "|" + t.Channel).ToListAsync();
            return View("~/Views/Admin/Notifications/Templates.cshtml");
        }

        [HttpGet]
        public async Task<IActionResult> EditNotificationTemplate(string id, string channel)
        {
            var e = NotificationCatalog.Find(id);
            if (e == null || !NotificationChannel.All.Contains(channel)) return NotFound();
            var custom = await _context.NotificationTemplates.AsNoTracking().Where(t => t.EventKey == e.Key).ToListAsync();
            var t = NotificationTexts.Resolve(e, channel, custom);
            return View("~/Views/Admin/Notifications/EditTemplate.cshtml", Preview(new TemplateEditView
            {
                EventKey = e.Key, Channel = channel, Subject = t.Subject, Body = t.Body, IsCustom = t.IsCustom
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditNotificationTemplate(TemplateEditView form, string? command)
        {
            var e = NotificationCatalog.Find(form.EventKey);
            if (e == null || !NotificationChannel.All.Contains(form.Channel)) return NotFound();
            var row = await _context.NotificationTemplates.FirstOrDefaultAsync(t => t.EventKey == e.Key && t.Channel == form.Channel);

            if (command == "reset")
            {
                if (row != null)
                {
                    _context.NotificationTemplates.Remove(row);
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = "The built-in text is used again.";
                return RedirectToAction(nameof(EditNotificationTemplate), new { id = e.Key, channel = form.Channel });
            }

            if (command == "preview")
            {
                form.IsCustom = row != null;
                return View("~/Views/Admin/Notifications/EditTemplate.cshtml", Preview(form));
            }

            if (string.IsNullOrWhiteSpace(form.Body))
            {
                ModelState.AddModelError(nameof(form.Body), "The text cannot be empty.");
                form.IsCustom = row != null;
                return View("~/Views/Admin/Notifications/EditTemplate.cshtml", Preview(form));
            }

            if (row == null)
            {
                row = new NotificationTemplate { EventKey = e.Key, Channel = form.Channel };
                _context.NotificationTemplates.Add(row);
            }

            row.Subject = form.Subject?.Trim();
            // only administrators edit templates; values put into them are HTML-encoded when sent
            row.Body = form.Body.Trim();
            row.UpdatedOn = DateTime.Now;
            row.UpdatedBy = CurrentUser!.FullName;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Template saved.";
            return RedirectToAction(nameof(EditNotificationTemplate), new { id = e.Key, channel = form.Channel });
        }

        private TemplateEditView Preview(TemplateEditView view)
        {
            var values = NotificationTexts.SampleValues(CurrentUser!.FullName);
            view.PreviewSubject = NotificationTexts.Render(view.Subject, values, false);
            view.PreviewBody = NotificationTexts.Render(view.Body, values, view.Channel == NotificationChannel.Email);
            return view;
        }

        // ---- providers, switches and test messages ----------------------------------------------------------

        [HttpGet]
        public IActionResult NotificationSettings()
        {
            return View("~/Views/Admin/Notifications/Settings.cshtml");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificationSettings([FromServices] NotificationSettingsStore settings)
        {
            // a switch posts "false" (hidden field) and, when ticked, also "true"
            var values = SettingKeys.Fields.ToDictionary(f => f.Key, f => f.Type == "bool"
                ? (Request.Form[f.Key].Contains("true") ? "true" : "false")
                : (string?)Request.Form[f.Key].ToString());
            var clear = Request.Form["clear"].Select(v => v ?? string.Empty).ToHashSet();
            await settings.SaveAsync(_context, values, clear);
            TempData["SuccessMessage"] = "Settings saved.";
            return RedirectToAction(nameof(NotificationSettings));
        }

        /// <summary>Sends one message right now (not through the queue) and shows the provider's answer.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificationTest(string channel, string? address, string? eventKey,
            [FromServices] NotificationSender sender, [FromServices] NotificationSettingsStore settings)
        {
            if (!NotificationChannel.All.Contains(channel)) return BadRequest();
            var me = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == CurrentUser!.Id);
            var e = NotificationCatalog.Find(eventKey ?? string.Empty);
            var values = NotificationTexts.SampleValues(me.FullName);

            string? subject;
            string body;
            if (e != null)
            {
                var custom = await _context.NotificationTemplates.AsNoTracking().Where(t => t.EventKey == e.Key).ToListAsync();
                var t = NotificationTexts.Resolve(e, channel, custom);
                subject = NotificationTexts.Render(t.Subject, values, false);
                body = NotificationTexts.Render(t.Body, values, channel == NotificationChannel.Email);
            }
            else
            {
                subject = "XFL CSMS test message";
                body = channel == NotificationChannel.Email
                    ? "<p>Hello " + System.Net.WebUtility.HtmlEncode(me.FullName) + ",</p><p>This is a test message from XFL CSMS. E-mail notifications work.</p>"
                    : "XFL CSMS test message: " + NotificationChannel.Label(channel) + " notifications work.";
            }

            address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
            var message = new NotificationMessage
            {
                EventKey = e?.Key ?? "Test",
                Channel = channel,
                UserId = me.Id,
                RecipientName = me.FullName,
                Subject = subject,
                Body = body,
                CreatedOn = DateTime.Now,
                NextAttemptOn = DateTime.Now,
                IsTest = true,
                Address = channel switch
                {
                    NotificationChannel.Email => address ?? me.Email,
                    NotificationChannel.InApp => null,
                    _ => PhoneNumbers.ToE164(address ?? me.PhonNumber, settings.Get(SettingKeys.CountryCode) ?? "880")
                }
            };
            _context.NotificationMessages.Add(message);
            await _context.SaveChangesAsync(); // the id is part of the toast

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await NotificationWorker.DeliverAsync(message, sender, 1, timeout.Token);
            await _context.SaveChangesAsync();

            if (message.Status == NotificationStatus.Sent)
            {
                TempData["SuccessMessage"] = NotificationChannel.Label(channel) + " test sent" + (message.Address != null ? " to " + message.Address : string.Empty)
                    + (message.ProviderMessageId != null ? " (provider id " + message.ProviderMessageId + ")" : string.Empty) + ".";
            }
            else
            {
                TempData["ErrorMessage"] = NotificationChannel.Label(channel) + " test failed: " + message.LastError;
            }

            return Redirect(Request.Headers["Referer"].ToString() is { Length: > 0 } back && Url.IsLocalUrl(new Uri(back).PathAndQuery)
                ? new Uri(back).PathAndQuery : Url.Action(nameof(NotificationSettings))!);
        }

        // ---- delivery log -----------------------------------------------------------------------------------

        [HttpGet]
        public async Task<IActionResult> NotificationLog(string? channel, string? status, string? eventKey, string? search, DateTime? from, DateTime? to, int page = 1)
        {
            var view = new NotificationLogView { Channel = channel, Status = status, EventKey = eventKey, Search = search, From = from, To = to, Page = Math.Max(1, page) };
            var query = _context.NotificationMessages.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(channel)) query = query.Where(m => m.Channel == channel);
            if (!string.IsNullOrEmpty(eventKey)) query = query.Where(m => m.EventKey == eventKey);
            if (from != null) query = query.Where(m => m.CreatedOn >= from.Value.Date);
            if (to != null)
            {
                var end = to.Value.Date.AddDays(1);
                query = query.Where(m => m.CreatedOn < end);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(m => m.RecipientName!.Contains(s) || m.Address!.Contains(s) || m.Subject!.Contains(s) || m.Body.Contains(s));
            }

            view.Counts = (await query.GroupBy(m => m.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Key, x => x.Count);
            if (!string.IsNullOrEmpty(status)) query = query.Where(m => m.Status == status);

            view.Total = await query.CountAsync();
            view.Items = await query.OrderByDescending(m => m.Id).Skip((view.Page - 1) * view.PageSize).Take(view.PageSize).ToListAsync();
            return View("~/Views/Admin/Notifications/Log.cshtml", view);
        }

        /// <summary>Puts a failed message (or every failed one) back in the queue.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotificationRetry(long? id, string? returnUrl, [FromServices] NotificationQueue queue)
        {
            var query = _context.NotificationMessages.Where(m => m.Status == NotificationStatus.Failed && !m.IsTest);
            if (id != null) query = query.Where(m => m.Id == id);
            var rows = await query.Take(1000).ToListAsync();
            foreach (var row in rows)
            {
                row.Status = NotificationStatus.Pending;
                row.Attempts = 0;
                row.NextAttemptOn = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            queue.Wake();
            TempData["SuccessMessage"] = rows.Count + " message(s) queued again.";
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(NotificationLog));
        }
    }
}
