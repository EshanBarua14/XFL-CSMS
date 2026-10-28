using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using XFLCSMS.Infrastructure;
using XFLCSMS.Models.Email;
using XFLCSMS.Models.Login;
using XFLCSMS.Models.Register;
using XFLCSMS.Services;
using XFLCSMS.Services.Notifications;
using Newtonsoft.Json;

namespace XFLCSMS.Controllers
{
    public class RegisterLoginController : Controller
    {
        private readonly DataContext _context;
        private readonly IEmailServices _emailServices;
        private readonly ILogger<RegisterLoginController> _logger;
        private readonly LoginThrottle _throttle;

        public RegisterLoginController(DataContext context, IEmailServices emailServices, ILogger<RegisterLoginController> logger, LoginThrottle throttle)
        {
            _context = context;
            _emailServices = emailServices;
            _logger = logger;
            _throttle = throttle;
        }

        // Keeps the session alive while a long form is open (called by xfl-ui.js every few minutes).
        [HttpGet]
        public IActionResult KeepAlive()
        {
            var signedIn = SessionAuthorizeAttribute.AllKeys.Any(k => !string.IsNullOrEmpty(HttpContext.Session.GetString(k)));
            return signedIn ? NoContent() : Unauthorized();
        }

        public IActionResult Index()
        {
            return RedirectToAction("Login");
        }

        public IActionResult Register()
        {
            var viewModel = new RegisterViewModel
            {
                Brokerages = _context.Brokerages.ToList(),
                userRegisterRequest = new UserRegisterRequest()
            };
            return View(viewModel);
        }

        public IActionResult Verify()
        {
            return View();
        }

        public IActionResult Login()
        {
            return View();
        }

        public IActionResult ForgotPassword()
        {
            return View();
        }

        public IActionResult ResetPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel registerView)
        {
            var request = registerView.userRegisterRequest ??= new UserRegisterRequest();

            if (ModelState.IsValid)
            {
                // The drop-downs post ids; make sure they point at real rows that belong together.
                if (!_context.Brokerages.Any(b => b.BrokerageId == request.BrokerageHouseName))
                {
                    ModelState.AddModelError("userRegisterRequest.BrokerageHouseName", "Please select your organization.");
                }
                else if (!_context.Branchhs.Any(b => b.BranchId == request.Branch && b.BrokerageId == request.BrokerageHouseName))
                {
                    ModelState.AddModelError("userRegisterRequest.Branch", "Please select your branch.");
                }

                if (_context.Users.Any(u => u.Email == request.Email))
                {
                    ModelState.AddModelError("userRegisterRequest.Email", "This email is already registered.");
                }

                if (_context.Users.Any(u => u.UserName == request.UserName))
                {
                    ModelState.AddModelError("userRegisterRequest.UserName", "This user name is already taken.");
                }
            }

            if (!ModelState.IsValid)
            {
                return RegisterForm(registerView);
            }

            PasswordHasher.Create(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                VerificationToken = CreateRandomToken(),
                FullName = request.FullName,
                PhonNumber = request.PhonNumber,
                Designation = request.Designation,
                BrokerageHouseName = request.BrokerageHouseName,
                Branch = request.Branch,
                EmployeeId = request.EmployeeId,
                Terms = request.Terms,
                UserName = request.UserName,
                BrokerageHouseAcronym = request.BrokerageHouseName,
                UStatus = true,
                UCatagory = false,
                UType = false,
            };

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            try
            {
                _emailServices.SendEmail(new EmailDto
                {
                    To = request.Email,
                    Subject = "Registration Token for XFL Support System Software",
                    Body = "Dear Concern,<br/>" +
                           "Thank you for registering in XFLCSMS.<br/><br/>" +
                           "Your Registration Token is: <b>" + user.VerificationToken + "</b><br/><br/>" +
                           "Please use this token to complete your registration process. If you did not request this token, " +
                           "please ignore this email.<br/><br/>" +
                           "If you encounter any issues or need assistance, feel free to contact us at " +
                           "info@xpertfintech.com.<br/><br/>Thank you,<br/>Xpert Fintech Limited"
                });
            }
            catch (Exception ex)
            {
                // Without the email the user can never get the token, so do not leave a dead account behind.
                _logger.LogError(ex, "Could not send the registration email to {Email}", request.Email);
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                ModelState.AddModelError(string.Empty, "We could not send the verification email. Please check the email address and try again later.");
                return RegisterForm(registerView);
            }

            TempData["Message"] = "Registration successful. Enter the token we emailed to you to activate your account.";
            return RedirectToAction("Verify");
        }

        // Re-display the registration form with its drop-downs filled in again.
        private IActionResult RegisterForm(RegisterViewModel registerView)
        {
            var request = registerView.userRegisterRequest;
            registerView.Brokerages = _context.Brokerages.ToList();
            registerView.Acronyme = _context.Brokerages
                .Where(b => b.BrokerageId == request.BrokerageHouseName)
                .Select(b => b.BrokerageHouseAcronym)
                .FirstOrDefault();
            registerView.Branchhs = _context.Branchhs
                .Where(b => b.BrokerageId == request.BrokerageHouseName)
                .ToList();
            return View("Register", registerView);
        }

        [HttpPost]
        public async Task<IActionResult> Verify(Verify verify)
        {
            if (string.IsNullOrWhiteSpace(verify.Email) || string.IsNullOrWhiteSpace(verify.Token))
            {
                ViewBag.Message = "Please enter your email (or user name) and the token.";
                return View(verify);
            }

            var token = verify.Token.Trim();
            var login = verify.Email.Trim();
            var ip = LoginThrottle.Ip(HttpContext);
            var wait = _throttle.BlockedMinutes("verify:" + login, ip);
            if (wait > 0)
            {
                ViewBag.Message = "Too many wrong attempts. Please try again in " + wait + " minute(s).";
                return View(verify);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => (u.VerificationToken == token) &&
                (u.Email == login || u.UserName == login));
            if (user == null)
            {
                _throttle.Fail("verify:" + login, ip);
                ViewBag.Message = "Invalid token or Email";
                return View(verify);
            }

            if (user.VerifiedAt == null)
            {
                user.VerifiedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            TempData["Message"] = "Your account is verified. You can sign in now.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Login(UserLoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrEmpty(request.Password))
            {
                return LoginFailed(request, "Please enter your user name (or email) and password.");
            }

            var login = request.UserId.Trim();
            var ip = LoginThrottle.Ip(HttpContext);

            // Password guessing: 5 wrong passwords for one account (or 20 from one address) lock it for 15 minutes.
            var wait = _throttle.BlockedMinutes("login:" + login, ip);
            if (wait > 0)
            {
                _logger.LogWarning("Sign-in for {Login} from {Ip} refused: too many failed attempts.", login, ip);
                return LoginFailed(request, "Too many failed sign-in attempts. Please try again in " + wait + " minute(s) or reset your password.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => (u.Email == login || u.UserName == login));

            // Same message for "no such user" and "wrong password" so the form does not reveal who is registered.
            if (user == null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
            {
                _throttle.Fail("login:" + login, ip);
                _logger.LogWarning("Failed sign-in for {Login} from {Ip}.", login, ip);
                return LoginFailed(request, "Invalid user name or password.");
            }

            _throttle.Reset("login:" + login);

            // accounts from before the PBKDF2 change get the stronger hash now, while the password is known
            if (PasswordHasher.NeedsUpgrade(user.PasswordSalt))
            {
                PasswordHasher.Create(request.Password, out var upgradedHash, out var upgradedSalt);
                user.PasswordHash = upgradedHash;
                user.PasswordSalt = upgradedSalt;
                await _context.SaveChangesAsync();
            }

            if (user.VerifiedAt == null)
            {
                return LoginFailed(request, "Your account is not verified yet. Use the token from the registration email on the Verify page.");
            }

            if (!user.UStatus)
            {
                return LoginFailed(request, "You are currently inactive. Please contact the XFL team.");
            }

            // Start from an empty session: a previous sign-in in the same browser must not leave another role's key behind.
            HttpContext.Session.Clear();

            // The session copy is only used to identify the user; keep the credentials out of it.
            var jsonString = JsonConvert.SerializeObject(new User
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                PhonNumber = user.PhonNumber,
                Designation = user.Designation,
                BrokerageHouseName = user.BrokerageHouseName,
                BrokerageHouseAcronym = user.BrokerageHouseAcronym,
                Branch = user.Branch,
                Department = user.Department,
                EmployeeId = user.EmployeeId,
                UserName = user.UserName,
                VerifiedAt = user.VerifiedAt,
                UCatagory = user.UCatagory,
                UType = user.UType,
                UStatus = user.UStatus,
                Terms = user.Terms,
                PasswordHash = Array.Empty<byte>(),
                PasswordSalt = Array.Empty<byte>()
            });

            // security alert "signed in" (off unless the administrator turns it on)
            var signedIn = new NotificationEventData
            {
                EventKey = "UserSignedIn", ActorUserId = user.Id, ActorName = user.FullName, SubjectUserId = user.Id,
                HouseId = user.BrokerageHouseName, BaseUrl = NotificationSaveInterceptor.BaseUrl(HttpContext), LinkPath = "/RegisterLogin/Login"
            };
            signedIn.RelatedUserIds.Add(user.Id);
            HttpContext.RequestServices.GetRequiredService<NotificationQueue>().Publish(signedIn);

            var sessionKey = SessionAuthorizeAttribute.KeyFor(user.UCatagory, user.UType, user.Department);
            HttpContext.Session.SetString(sessionKey, jsonString);
            return sessionKey switch
            {
                SessionAuthorizeAttribute.Admin => RedirectToAction("Dashbord", "Admin"),
                SessionAuthorizeAttribute.SupportManager => RedirectToAction("Dashbord", "SupportManegar"),
                SessionAuthorizeAttribute.SupportEngineer => RedirectToAction("Dashbord", "SupportEngineer"),
                SessionAuthorizeAttribute.Viewer => RedirectToAction("Dashbord", "Viewer"),
                _ => RedirectToAction("Dashbord", "Maker")
            };
        }

        private IActionResult LoginFailed(UserLoginRequest request, string message)
        {
            ViewBag.Message = message;
            request.Password = string.Empty;
            return View("Login", request);
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(Verify verify)
        {
            if (string.IsNullOrWhiteSpace(verify.Email))
            {
                ViewBag.Message = "Please enter your email or user name.";
                return View(verify);
            }

            var login = verify.Email.Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => (u.Email == login) || (u.UserName == login));
            if (user == null)
            {
                // Same answer as for a real account, so this form cannot be used to find out who is registered.
                _logger.LogInformation("Password reset asked for unknown login {Login}.", login);
                TempData["Message"] = "If that account exists, we emailed it a reset token. Enter it below together with your new password.";
                return RedirectToAction("ResetPassword");
            }

            user.PasswordResetToken = CreateRandomToken();
            user.ResetTokenExpires = DateTime.Now.AddHours(2);
            await _context.SaveChangesAsync();

            try
            {
                _emailServices.SendEmail(new EmailDto
                {
                    // Always the address on file: the form also accepts a user name, which is not an email address.
                    To = user.Email,
                    Subject = "Password Reset Token",
                    Body = "Your Password Reset Token is: <b>" + user.PasswordResetToken + "</b><br/>It is valid for 2 hours."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the password reset email to {Email}", user.Email);
                ViewBag.Message = "We could not send the reset email. Please try again later.";
                return View(verify);
            }

            TempData["Message"] = "If that account exists, we emailed it a reset token. Enter it below together with your new password.";
            return RedirectToAction("ResetPassword");
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Message = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return View(request);
            }

            var token = request.Token.Trim();
            var ip = LoginThrottle.Ip(HttpContext);
            var wait = _throttle.BlockedMinutes(ip);
            if (wait > 0)
            {
                ViewBag.Message = "Too many wrong attempts. Please try again in " + wait + " minute(s).";
                return View(request);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.PasswordResetToken == token);
            if (user == null || user.ResetTokenExpires < DateTime.Now)
            {
                _throttle.Fail(ip);
                ViewBag.Message = "Invalid or expired token.";
                return View(request);
            }

            PasswordHasher.Create(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

            user.PasswordHash = passwordHash;
            user.PasswordSalt = passwordSalt;
            user.PasswordResetToken = null;
            user.ResetTokenExpires = null;

            await _context.SaveChangesAsync();
            _throttle.Reset("login:" + user.UserName, "login:" + user.Email);

            TempData["Message"] = "Your password was changed. You can sign in now.";
            return RedirectToAction("Login");
        }

        // Signed-in users change their password from their own menu (Admin/Maker/... ChangePassword).
        public IActionResult ChangePassword()
        {
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches(int brokerageId)
        {
            // Only the two fields the registration page needs (no navigation properties).
            var branches = await _context.Branchhs
                .Where(b => b.BrokerageId == brokerageId)
                .Select(b => new { branchId = b.BranchId, branchName = b.BranchName })
                .ToListAsync();

            return Json(branches);
        }

        // 6 hex characters (16 million values) could be guessed by a script; 8 random bytes cannot.
        private string CreateRandomToken()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
        }
    }
}
