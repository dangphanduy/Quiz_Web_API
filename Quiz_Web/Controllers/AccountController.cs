using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Authorization;
using Quiz_Web.Helper;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using Quiz_Web.Utils;
using System.Security.Claims;

namespace Quiz_Web.Controllers
{
	public class AccountController : Controller
	{
		private readonly IUserService _userService;
		private readonly IEmailService _emailService;
		private readonly IPurchaseService _purchaseService;
		public AccountController(IUserService userService, IEmailService emailService, IPurchaseService purchaseService)
		{
			_userService = userService;
			_emailService = emailService;
			_purchaseService = purchaseService;
		}

		public IActionResult Index()
		{
			return View();
		}

		[Route("/login")]
		public IActionResult Login(string? returnUrl = null)
		{
			if (User.Identities != null && User.Identity.IsAuthenticated)
			{
				if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
				{
					return LocalRedirect(returnUrl);
				}
				return Redirect("/");
			}
			ViewBag.ReturnUrl = returnUrl;
			return View();
		}

		// POST: /Account/Login
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> LoginToSystem(string username, string password, string? returnUrl)
		{
			try
			{
				var user = _userService.Login(username, HashHelper.ComputeHash(password));
				if (user != null)
				{
					var claims = new List<Claim>();
					claims.Add(new Claim(ClaimTypes.Name, user.FullName));
					claims.Add(new Claim(ClaimTypes.Email, user.Email));
					claims.Add(new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()));
					claims.Add(new Claim(ClaimTypes.Role, user.Role?.Name ?? string.Empty));

					var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
					await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

					// Admin goes directly to admin dashboard
					if (user.RoleId == 1) // Admin role
					{
						return Json(new { status = WebConstants.SUCCESS, redirectUrl = "/admin" });
					}

					// Check if user needs onboarding (first-time login) for non-admin users
					var hasProfile = _userService.HasUserProfile(user.UserId);
					var hasInterests = _userService.HasUserInterests(user.UserId);
					
					if (!hasProfile || !hasInterests)
					{
						return Json(new { status = WebConstants.SUCCESS, redirectUrl = "/Onboarding" });
					}

					if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
					{
						return Json(new { status = WebConstants.SUCCESS, redirectUrl = returnUrl });
					}

					// Nếu không có returnUrl → điều hướng theo role
					var role = user.Role?.Name?.ToLower() ?? "";

					string redirectUrl = role == "admin" ? "/admin" : "/";
					return Json(new { status = WebConstants.SUCCESS, redirectUrl = redirectUrl });
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Tài khoản hoặc mật khẩu không chính xác." });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi đăng nhập", error = ex.ToString() });
			}
		}

		[Route("/register")]
		public IActionResult Register()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> RegisterToSystem(string fullname, string email, string username, string password, string confirmPassword)
		{
			try
			{
				if (!Validation.IsValidEmail(email)) return Json(new { status = WebConstants.ERROR, message = "Email không đúng định dạng" });
				if (!Validation.IsValidUsername(username)) return Json(new { status = WebConstants.ERROR, message = "Tên đăng nhập phải có ít nhất 6 ký tự" });
				if (!Validation.IsValidPassword(password)) return Json(new { status = WebConstants.ERROR, message = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt" });
				if (_userService.ExistsEmail(email)) return Json(new { status = WebConstants.ERROR, message = "Email đã được sử dụng" });
				if (_userService.ExistsUsername(username)) return Json(new { status = WebConstants.ERROR, message = "Tên đăng nhập đã được sử dụng" });
				if (confirmPassword.Equals(password) == false) return Json(new { status = WebConstants.ERROR, message = "Mật khẩu xác nhận không khớp" });

				var user = new User
				{
					Email = email.ToLower().Trim(),
					Username = username.ToLower().Trim(),
					PasswordHash = HashHelper.ComputeHash(password.Trim()),
					FullName = fullname.Trim(),
					RoleId = 2
				};

				if (_userService.Register(user))
				{
					return Json(new { status = WebConstants.SUCCESS });
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Lỗi khi tạo tài khoản" });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi khi đăng ký", error = ex.ToString() });
			}
		}

		[Route("forgotPassword")]
		public IActionResult ForgotPassword()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> ForgotPasswordSubmit(string email)
		{
			try
			{
				if (!Validation.IsValidEmail(email))
					return Json(new { status = WebConstants.ERROR, message = "Email không đúng định dạng" });

				var user = _userService.GetUserByEmail(email);
				if (user == null)
					return Json(new { status = WebConstants.ERROR, message = "Email không tồn tại trong hệ thống" });

				if (_userService.GenerateForgotPasswordCode(email, out string code))
				{
					if (await _emailService.SendForgotPasswordCodeEmail(email, code))
					{
						return Json(new { status = WebConstants.SUCCESS, message = "Mã xác thực đã được gửi về email của bạn. Vui lòng kiểm tra hộp thư.", email = email });
					}
					else
					{
						return Json(new { status = WebConstants.ERROR, message = "Không thể gửi email chứa mã xác thực. Vui lòng thử lại sau." });
					}
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Có lỗi xảy ra khi tạo mã xác thực. Vui lòng thử lại." });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi hệ thống", error = ex.ToString() });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult VerifyResetCode(string email, string code)
		{
			try
			{
				if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
				{
					return Json(new { status = WebConstants.ERROR, message = "Thông tin không đầy đủ." });
				}

				if (_userService.VerifyResetCode(email, code, out string secureToken))
				{
					return Json(new { status = WebConstants.SUCCESS, token = secureToken });
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Mã xác thực không chính xác hoặc đã hết hạn." });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi hệ thống", error = ex.ToString() });
			}
		}

		[Route("/resetPassword")]
		public IActionResult ResetPassword(string token)
		{
			if (string.IsNullOrEmpty(token) || !_userService.ValidatePasswordResetToken(token))
			{
				ViewBag.ErrorMessage = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
				return View("ResetPasswordInvalid");
			}

			ViewBag.Token = token;
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public Task<JsonResult> ResetPasswordSubmit(string token, string password, string confirmPassword)
		{
			try
			{
				if (string.IsNullOrEmpty(token))
					return Task.FromResult(Json(new { status = WebConstants.ERROR, message = "Liên kết đặt lại mật khẩu không hợp lệ." }));

				if (!Validation.IsValidPassword(password))
					return Task.FromResult(Json(new { status = WebConstants.ERROR, message = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt" }));
				if (!password.Equals(confirmPassword))
					return Task.FromResult(Json(new { status = WebConstants.ERROR, message = "Mật khẩu xác nhận không khớp." }));

				if (!_userService.ValidatePasswordResetToken(token))
					return Task.FromResult(Json(new { status = WebConstants.ERROR, message = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn." }));

				if (_userService.ResetPassword(token, HashHelper.ComputeHash(password)))
				{
					return Task.FromResult(Json(new { status = WebConstants.SUCCESS, message = "Đặt lại mật khẩu thành công. Bạn có thể đăng nhập với mật khẩu mới." }));
				}
				else
				{
					return Task.FromResult(Json(new { status = WebConstants.ERROR, message = "Đặt lại mật khẩu thất bại. Vui lòng thử lại." }));
				}
			}
			catch (Exception ex)
			{
				return Task.FromResult(Json(new { status = WebConstants.ERROR, message = "Lỗi hệ thống", error = ex.ToString() }));
			}
		}

		[Route("/logout")]
		public async Task<IActionResult> Logout()
		{
			await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return RedirectToAction("Login", "Account");
		}

		[Authorize]
		[Route("/account/settings")]
		public IActionResult Settings()
		{
			var userId = GetCurrentUserId();
			var user = _userService.GetUserById(userId);
			if (user == null)
			{
				return RedirectToAction("Login");
			}
			return View(user);
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> UpdateEmail(string newEmail)
		{
			try
			{
				var userId = GetCurrentUserId();
				if (!Validation.IsValidEmail(newEmail))
				{
					return Json(new { status = WebConstants.ERROR, message = "Email không đúng định dạng" });
				}

				if (_userService.ExistsEmail(newEmail))
				{
					return Json(new { status = WebConstants.ERROR, message = "Email đã được sử dụng" });
				}

				if (_userService.UpdateEmail(userId, newEmail))
				{
					// Update Claims with new Email
					var user = _userService.GetUserById(userId);
					if (user != null)
					{
						var claims = new List<Claim>
						{
							new Claim(ClaimTypes.Name, user.FullName),
							new Claim(ClaimTypes.Email, user.Email),
							new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
							new Claim(ClaimTypes.Role, user.Role?.Name ?? string.Empty)
						};

						var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
						await HttpContext.SignInAsync(
							CookieAuthenticationDefaults.AuthenticationScheme, 
							new ClaimsPrincipal(claimsIdentity)
						);
					}

					return Json(new { status = WebConstants.SUCCESS, message = "Cập nhật email thành công", email = newEmail });
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Không thể cập nhật email" });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi hệ thống", error = ex.ToString() });
			}
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> UpdatePassword(string currentPassword, string newPassword, string confirmPassword)
		{
			try
			{
				var userId = GetCurrentUserId();
				var user = _userService.GetUserById(userId);

				if (user == null)
				{
					return Json(new { status = WebConstants.ERROR, message = "Người dùng không tồn tại" });
				}

				// Verify current password
				if (user.PasswordHash != HashHelper.ComputeHash(currentPassword))
				{
					return Json(new { status = WebConstants.ERROR, message = "Mật khẩu hiện tại không đúng" });
				}

				if (!Validation.IsValidPassword(newPassword))
				{
					return Json(new { status = WebConstants.ERROR, message = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt" });
				}

				if (newPassword != confirmPassword)
				{
					return Json(new { status = WebConstants.ERROR, message = "Mật khẩu xác nhận không khớp" });
				}

				if (_userService.UpdatePassword(userId, HashHelper.ComputeHash(newPassword)))
				{
					return Json(new { status = WebConstants.SUCCESS, message = "Cập nhật mật khẩu thành công" });
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Không thể cập nhật mật khẩu" });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi hệ thống", error = ex.ToString() });
			}
		}

		[Authorize]
		[Route("/account/profile")]
		public IActionResult Profile()
		{
			var userId = GetCurrentUserId();
			var user = _userService.GetUserById(userId);
			if (user == null)
			{
				return RedirectToAction("Login");
			}
			return View(user);
		}

		[Authorize]
		[Route("/account/purchase-history")]
		public async Task<IActionResult> PurchaseHistory()
		{
			var userId = GetCurrentUserId();
			var user = _userService.GetUserById(userId);
			if (user == null)
			{
				return RedirectToAction("Login");
			}
			
			var purchases = await _purchaseService.GetUserPurchasesAsync(userId);
			ViewBag.Purchases = purchases;
			ViewBag.PurchaseCount = purchases.Count;
			return View(user);
		}

		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<JsonResult> UpdateProfile(string fullName, string? phone)
		{
			try
			{
				var userId = GetCurrentUserId();
				
				if (string.IsNullOrWhiteSpace(fullName))
				{
					return Json(new { status = WebConstants.ERROR, message = "Họ và tên không được để trống" });
				}

				if (!string.IsNullOrEmpty(phone) && !Validation.IsValidPhone(phone))
				{
					return Json(new { status = WebConstants.ERROR, message = "Số điện thoại không đúng định dạng" });
				}

				if (_userService.UpdateProfile(userId, fullName, phone))
				{
                    var user = _userService.GetUserById(userId);
                    if (user != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.Name, user.FullName),
                            new Claim(ClaimTypes.Email, user.Email),
                            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                            new Claim(ClaimTypes.Role, user.Role?.Name ?? string.Empty)
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity)
                        );
                    }
                    return Json(new { status = WebConstants.SUCCESS, message = "Cập nhật hồ sơ thành công" });
				}
				else
				{
					return Json(new { status = WebConstants.ERROR, message = "Không thể cập nhật hồ sơ" });
				}
			}
			catch (Exception ex)
			{
				return Json(new { status = WebConstants.ERROR, message = "Lỗi hệ thống", error = ex.ToString() });
			}
		}

		private int GetCurrentUserId()
		{
			var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
			if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
			{
				return userId;
			}
			return 0;
		}
	}
}
