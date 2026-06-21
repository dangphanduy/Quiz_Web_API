using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Helper;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using System;
using System.Threading.Tasks;

namespace Quiz_Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ITokenService _tokenService;
        private readonly LearningPlatformContext _context;

        public AuthController(IUserService userService, ITokenService tokenService, LearningPlatformContext context)
        {
            _userService = userService;
            _tokenService = tokenService;
            _context = context;
        }

        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "Thông tin đăng nhập không hợp lệ" });
            }

            var passwordHash = HashHelper.ComputeHash(request.Password);
            var user = _userService.Login(request.Username, passwordHash);

            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Tài khoản hoặc mật khẩu không chính xác" });
            }

            var token = _tokenService.GenerateJwtToken(user);

            // Update last login
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                token,
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.Email,
                    user.FullName,
                    user.AvatarUrl,
                    Role = user.Role?.Name ?? "User"
                }
            });
        }

        // POST: api/auth/external-login
        [HttpPost("external-login")]
        public async Task<IActionResult> ExternalLogin([FromBody] ExternalLoginRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Provider) || string.IsNullOrEmpty(request.Token))
            {
                return BadRequest(new { success = false, message = "Thông tin xác thực không hợp lệ" });
            }

            var externalUser = await _tokenService.VerifyExternalTokenAsync(request.Provider, request.Token);
            if (externalUser == null)
            {
                return Unauthorized(new { success = false, message = "Xác thực tài khoản bên thứ ba thất bại" });
            }

            // Find existing user by email
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == externalUser.Email.ToLower().Trim());

            if (user == null)
            {
                // Register a new user automatically
                var username = externalUser.Email.Split('@')[0].ToLower().Trim();
                
                // Ensure username uniqueness
                int count = 1;
                var baseUsername = username;
                while (await _context.Users.AnyAsync(u => u.Username == username))
                {
                    username = $"{baseUsername}{count++}";
                }

                user = new User
                {
                    Email = externalUser.Email.ToLower().Trim(),
                    Username = username,
                    PasswordHash = HashHelper.ComputeHash(Guid.NewGuid().ToString()), // Random password since they login with OAuth
                    FullName = externalUser.FullName,
                    AvatarUrl = externalUser.AvatarUrl,
                    RoleId = 2, // Default Role (Student/User)
                    Status = 1,
                    CreatedAt = DateTime.UtcNow,
                    LastLoginAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Reload user to populate relationships
                user = await _context.Users
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == user.UserId);
            }
            else
            {
                // Update login details
                user.LastLoginAt = DateTime.UtcNow;
                if (string.IsNullOrEmpty(user.AvatarUrl) && !string.IsNullOrEmpty(externalUser.AvatarUrl))
                {
                    user.AvatarUrl = externalUser.AvatarUrl;
                }
                await _context.SaveChangesAsync();
            }

            if (user == null)
            {
                return StatusCode(500, new { success = false, message = "Không thể tạo hoặc tải thông tin người dùng" });
            }

            var localToken = _tokenService.GenerateJwtToken(user);

            return Ok(new
            {
                success = true,
                token = localToken,
                user = new
                {
                    user.UserId,
                    user.Username,
                    user.Email,
                    user.FullName,
                    user.AvatarUrl,
                    Role = user.Role?.Name ?? "User"
                }
            });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class ExternalLoginRequest
    {
        public string Provider { get; set; } = string.Empty; // "google", "microsoft", or "facebook"
        public string Token { get; set; } = string.Empty; // ID Token or Access Token from provider
    }
}
