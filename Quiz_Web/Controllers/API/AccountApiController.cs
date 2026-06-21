using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Helper;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using Quiz_Web.Utils;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountApiController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;
        private readonly IPurchaseService _purchaseService;
        private readonly ILogger<AccountApiController> _logger;

        public AccountApiController(
            IUserService userService,
            IEmailService emailService,
            IPurchaseService purchaseService,
            ILogger<AccountApiController> logger)
        {
            _userService = userService;
            _emailService = emailService;
            _purchaseService = purchaseService;
            _logger = logger;
        }

        // GET: api/AccountApi/profile
        [Authorize]
        [HttpGet("profile")]
        public IActionResult GetProfile()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var user = _userService.GetUserById(userId);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "Người dùng không tồn tại" });
                }

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        userId = user.UserId,
                        username = user.Username,
                        email = user.Email,
                        fullName = user.FullName,
                        phone = user.Phone,
                        role = user.Role,
                        createdAt = user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile via API");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải thông tin cá nhân" });
            }
        }

        // POST: api/AccountApi/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                {
                    return BadRequest(new { success = false, message = "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu" });
                }

                var user = _userService.Login(request.Username, HashHelper.ComputeHash(request.Password));
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "Tên đăng nhập hoặc mật khẩu không chính xác" });
                }

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Role, user.Role?.Name ?? "Student")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var authProperties = new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(3)
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties);

                return Ok(new
                {
                    success = true,
                    message = "Đăng nhập thành công",
                    user = new
                    {
                        userId = user.UserId,
                        fullName = user.FullName,
                        email = user.Email,
                        role = user.Role
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API login error");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi đăng nhập" });
            }
        }

        // POST: api/AccountApi/register
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ApiRegisterRequest request)
        {
            try
            {
                if (!Validation.IsValidEmail(request.Email))
                    return BadRequest(new { success = false, message = "Email không đúng định dạng" });

                if (!Validation.IsValidUsername(request.Username))
                    return BadRequest(new { success = false, message = "Tên đăng nhập phải có ít nhất 6 ký tự" });

                if (!Validation.IsValidPassword(request.Password))
                    return BadRequest(new { success = false, message = "Mật khẩu phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt" });

                if (_userService.ExistsEmail(request.Email))
                    return BadRequest(new { success = false, message = "Email đã được sử dụng" });

                if (_userService.ExistsUsername(request.Username))
                    return BadRequest(new { success = false, message = "Tên đăng nhập đã được sử dụng" });

                if (request.ConfirmPassword != request.Password)
                    return BadRequest(new { success = false, message = "Mật khẩu xác nhận không khớp" });

                var user = new User
                {
                    FullName = request.FullName,
                    Email = request.Email,
                    Username = request.Username.Trim(),
                    PasswordHash = HashHelper.ComputeHash(request.Password.Trim()),
                    RoleId = 2, // 2 = Student
                    Status = 1, // 1 = Active
                    CreatedAt = DateTime.UtcNow
                };

                var success = _userService.Register(user);
                if (!success)
                {
                    return StatusCode(500, new { success = false, message = "Không thể đăng ký tài khoản lúc này" });
                }

                return Ok(new { success = true, message = "Đăng ký tài khoản thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API register error");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi đăng ký" });
            }
        }

        // POST: api/AccountApi/logout
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Ok(new { success = true, message = "Đăng xuất thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API logout error");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi đăng xuất" });
            }
        }

        // POST: api/AccountApi/update-profile
        [Authorize]
        [HttpPost("update-profile")]
        public IActionResult UpdateProfile([FromBody] ApiUpdateProfileRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                if (string.IsNullOrWhiteSpace(request.FullName))
                {
                    return BadRequest(new { success = false, message = "Họ và tên không được để trống" });
                }

                if (!string.IsNullOrEmpty(request.Phone) && !Validation.IsValidPhone(request.Phone))
                {
                    return BadRequest(new { success = false, message = "Số điện thoại không đúng định dạng" });
                }

                var success = _userService.UpdateProfile(userId, request.FullName, request.Phone);
                if (!success)
                {
                    return StatusCode(500, new { success = false, message = "Không thể cập nhật hồ sơ lúc này" });
                }

                return Ok(new { success = true, message = "Cập nhật thông tin cá nhân thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API update-profile error");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi cập nhật hồ sơ" });
            }
        }

        // POST: api/AccountApi/change-password
        [Authorize]
        [HttpPost("change-password")]
        public IActionResult ChangePassword([FromBody] ApiChangePasswordRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var user = _userService.GetUserById(userId);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "Người dùng không tồn tại" });
                }

                if (user.PasswordHash != HashHelper.ComputeHash(request.CurrentPassword))
                {
                    return BadRequest(new { success = false, message = "Mật khẩu hiện tại không chính xác" });
                }

                if (!Validation.IsValidPassword(request.NewPassword))
                {
                    return BadRequest(new { success = false, message = "Mật khẩu mới phải có ít nhất 8 ký tự, bao gồm chữ hoa, chữ thường, số và ký tự đặc biệt" });
                }

                if (request.NewPassword != request.ConfirmPassword)
                {
                    return BadRequest(new { success = false, message = "Mật khẩu mới xác nhận không khớp" });
                }

                var success = _userService.UpdatePassword(userId, HashHelper.ComputeHash(request.NewPassword));
                if (!success)
                {
                    return StatusCode(500, new { success = false, message = "Không thể đổi mật khẩu lúc này" });
                }

                return Ok(new { success = true, message = "Thay đổi mật khẩu thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API change-password error");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi đổi mật khẩu" });
            }
        }

        // GET: api/AccountApi/purchase-history
        [Authorize]
        [HttpGet("purchase-history")]
        public async Task<IActionResult> GetPurchaseHistory()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var purchases = await _purchaseService.GetUserPurchasesAsync(userId);

                var purchasesDto = purchases.Select(p => new
                {
                    purchaseId = p.PurchaseId,
                    courseId = p.CourseId,
                    courseTitle = p.Course?.Title,
                    courseSlug = p.Course?.Slug,
                    price = p.PricePaid,
                    status = p.Status,
                    purchaseDate = p.PurchasedAt
                }).ToList();

                return Ok(new { success = true, purchases = purchasesDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API purchase-history error");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải lịch sử giao dịch" });
            }
        }
    }

    public class ApiLoginRequest
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class ApiRegisterRequest
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }

    public class ApiUpdateProfileRequest
    {
        public string FullName { get; set; } = null!;
        public string? Phone { get; set; }
    }

    public class ApiChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }
}
