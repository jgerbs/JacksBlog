// Jack Gerber - A01266976
// Production AccountController with Email Confirmation (EmailJS)

using BlogApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using BlogApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace BlogApp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        // ---------------- REGISTER (GET) ----------------
        [HttpGet]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        // ---------------- REGISTER (POST) ----------------
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                IsApproved = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                bool duplicate = result.Errors.Any(e => e.Code == "DuplicateUserName" || e.Code == "DuplicateEmail");

                if (duplicate)
                    ViewBag.DuplicateEmail = true;

                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Contributor");

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var confirmUrl = Url.Action(
                "ConfirmEmail",
                "Account",
                new { userId = user.Id, token = token },
                protocol: HttpContext.Request.Scheme
            );

            // Load email template
            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "email", "confirm.html");
            var html = await System.IO.File.ReadAllTextAsync(templatePath);
            html = html.Replace("{{confirm_link}}", confirmUrl);

            await _emailSender.SendEmailAsync(user.Email, "Confirm your BlogApp account", html);

            TempData["RegisterSuccess"] = user.Email;

            return RedirectToAction("Register");
        }


        // ---------------- RESEND CONFIRMATION EMAIL ----------------
        [HttpPost]
        public async Task<IActionResult> ResendConfirmationEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return RedirectToAction("CheckEmail", new { email = "", error = "Invalid email." });
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                return RedirectToAction("CheckEmail", new { email = email, error = "No account found with that email." });
            }

            if (user.EmailConfirmed)
            {
                return RedirectToAction("Login");
            }

            // Generate token
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var confirmUrl = Url.Action(
                "ConfirmEmail",
                "Account",
                new { userId = user.Id, token = token },
                protocol: HttpContext.Request.Scheme);

            var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "email", "confirm.html");
            var html = await System.IO.File.ReadAllTextAsync(templatePath);
            html = html.Replace("{{confirm_link}}", confirmUrl);

            await _emailSender.SendEmailAsync(user.Email, "Confirm your BlogApp account", html);

            // Redirect BACK to CheckEmail with success flag
            return RedirectToAction("CheckEmail", new { email = email, resent = true });
        }



        // ---------------- CHECK EMAIL PAGE ----------------
        public IActionResult CheckEmail(string email, bool? resent, string error)
        {
            ViewBag.Email = email;
            ViewBag.Error = error;
            ViewBag.Resent = resent ?? false;

            return View();
        }


        // ---------------- CONFIRM EMAIL ----------------
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (userId == null || token == null)
                return BadRequest("Invalid confirmation link.");

            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound("User not found.");

            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                user.IsApproved = true;
                await _userManager.UpdateAsync(user);

                return View("ConfirmSuccess");
            }

            return View("ConfirmFailed");
        }

        // ---------------- LOGIN (GET) ----------------
        public IActionResult Login() => View(new LoginViewModel());

        // ---------------- LOGIN (POST) ----------------
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByNameAsync(model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "No account found with that email.");
                return View(model);
            }

            if (!user.EmailConfirmed)
            {
                ModelState.AddModelError("", "Please confirm your email before signing in.");
                return View(model);
            }

            if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow)
            {
                ModelState.AddModelError("", "Your account has been suspended. Contact the administrator.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName,
                model.Password,
                model.RememberMe,
                false
            );

            if (result.Succeeded)
                return RedirectToAction("Index", "Home");

            ModelState.AddModelError("", "Invalid password.");
            return View(model);
        }

        // ---------------- LOGOUT ----------------
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ---------------- MANAGE USERS (Admin Only) ----------------
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManageUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        // ---------------- SUSPEND USER ----------------
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> SuspendUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "User suspended.";
            return RedirectToAction("ManageUsers");
        }

        // ---------------- UNSUSPEND USER ----------------
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UnsuspendUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.LockoutEnd = null;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "User unsuspended.";
            return RedirectToAction("ManageUsers");
        }

        // ---------------- DELETE USER ----------------
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["Success"] = "User not found.";
                return RedirectToAction("ManageUsers");
            }

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["Success"] = "User deleted successfully.";
                return RedirectToAction("ManageUsers");
            }

            TempData["Success"] = "Could not delete user.";
            return RedirectToAction("ManageUsers");
        }


        // ---------------- FORGOT PASSWORD GET ----------------
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        // ---------------- FORGOT PASSWORD POST ----------------
        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] = "Please enter your email.";
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                TempData["Error"] = "No account found with that email.";
                return View();
            }

            if (!user.EmailConfirmed)
            {
                TempData["Error"] = "You must confirm your email before resetting your password.";
                return View();
            }

            // Generate reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { email = user.Email, token = token },
                protocol: HttpContext.Request.Scheme
            );

            // Load template
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "email", "resetpassword.html");
            var html = await System.IO.File.ReadAllTextAsync(path);
            html = html.Replace("{{reset_link}}", resetLink);

            await _emailSender.SendEmailAsync(
                user.Email,
                "Reset your password",
                html
            );

            TempData["ResetSuccess"] = "Password reset instructions have been sent to your email.";
            return RedirectToAction("Login");
        }

        // ---------------- RESET PASSWORD GET ----------------
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            if (email == null || token == null)
                return BadRequest("Invalid password reset link.");

            ViewBag.Email = email;
            ViewBag.Token = token;

            return View();
        }

        // ---------------- RESET PASSWORD POST ----------------
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string email, string token, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid password reset request.";
                return RedirectToAction("Login");
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                TempData["Error"] = "No user found.";
                return RedirectToAction("Login");
            }

            var result = await _userManager.ResetPasswordAsync(user, token, password);

            if (result.Succeeded)
            {
                TempData["Success"] = "Your password has been reset! You may now log in.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            ViewBag.Email = email;
            ViewBag.Token = token;

            return View();
        }

    }
}
