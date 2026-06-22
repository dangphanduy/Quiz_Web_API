using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Quiz_Web.Services
{
	public class UserService : IUserService
	{
		private readonly LearningPlatformContext _context;
		private readonly ILogger<UserService> _logger;
		public UserService(LearningPlatformContext context, ILogger<UserService> logger)
		{
			_context = context;
			_logger = logger;
		}

		public User? Login(string username, string password)
		{
			try
			{
				var user = _context.Users.
					Include(u => u.Role).
					FirstOrDefault(u => u.Username == username.ToLower().Trim() && u.PasswordHash == password);
				return user;
			}
			catch (Exception ex)
			{
				return null;
			}
		}

		public bool Register(User user)
		{
			try
			{
				_context.Users.Add(user);
				_context.SaveChanges();
				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}
		public bool ExistsEmail(string email)
		{
			try
			{
				return _context.Users.Any(u => u.Email == email.ToLower().Trim());
			}
			catch (Exception ex)
			{
				return false;
			}
		}
		public bool ExistsUsername(string username)
		{
			try
			{
				return _context.Users.Any(u => u.Username == username.ToLower().Trim());
			}
			catch (Exception ex)
			{
				return false;
			}
		}

		public User? GetUserByEmail(string email)
		{
			try
			{
				return _context.Users.FirstOrDefault(u => u.Email == email.ToLower().Trim());
			}
			catch (Exception ex)
			{
				return null;
			}
		}

		public bool GeneratePasswordResetToken(string email, out string token)
		{
			try
			{
				var user = GetUserByEmail(email);
				if (user == null)
				{
					token = null;
					return false;
				}

				//random token
				token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

				user.PasswordResetToken = token;
				user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1);

				_context.SaveChanges();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"GeneratePasswordResetToken error: {ex.Message}");

				token = null;
				return false;
			}
		}

		public bool GenerateForgotPasswordCode(string email, out string code)
		{
			try
			{
				var user = GetUserByEmail(email);
				if (user == null)
				{
					code = null;
					return false;
				}

				// Generate a 6-digit code
				code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");

				// Generate a secure long token
				var secureToken = Guid.NewGuid().ToString("N");

				// Store formatted as OTP|SecureToken
				user.PasswordResetToken = $"{code}|{secureToken}";
				user.PasswordResetTokenExpiry = DateTime.UtcNow.AddMinutes(10);

				_context.SaveChanges();
				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError($"GenerateForgotPasswordCode error: {ex.Message}");
				code = null;
				return false;
			}
		}

		public bool VerifyResetCode(string email, string code, out string secureToken)
		{
			secureToken = null;
			try
			{
				var user = GetUserByEmail(email);
				if (user == null || string.IsNullOrEmpty(user.PasswordResetToken) || user.PasswordResetTokenExpiry == null || user.PasswordResetTokenExpiry < DateTime.UtcNow)
				{
					return false;
				}

				var parts = user.PasswordResetToken.Split('|');
				if (parts.Length == 2 && parts[0] == code.Trim())
				{
					secureToken = parts[1];
					return true;
				}
				return false;
			}
			catch (Exception ex)
			{
				_logger.LogError($"VerifyResetCode error: {ex.Message}");
				return false;
			}
		}

		public bool ValidatePasswordResetToken(string token)
		{
			try
			{
				_logger.LogInformation($"ValidatePasswordResetToken called with token: {token}");
				_logger.LogInformation($"Current UTC time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

				var user = _context.Users.FirstOrDefault(u => u.PasswordResetToken == token || (u.PasswordResetToken != null && u.PasswordResetToken.EndsWith("|" + token)));

				if (user == null)
				{
					_logger.LogWarning("ValidatePasswordResetToken: User not found for token");
					return false;
				}

				_logger.LogInformation($"Found user: {user.Email}");
				_logger.LogInformation($"Token expiry: {user.PasswordResetTokenExpiry:yyyy-MM-dd HH:mm:ss}");

				if (user.PasswordResetTokenExpiry == null)
				{
					_logger.LogWarning("ValidatePasswordResetToken: Token expiry is null");
					return false;
				}

				bool isValid = user.PasswordResetTokenExpiry > DateTime.UtcNow;
				_logger.LogInformation($"Token is valid: {isValid}");

				return isValid;
			}
			catch (Exception ex)
			{
				_logger.LogError($"ValidatePasswordResetToken error: {ex.Message}");
				_logger.LogError($"Stack trace: {ex.StackTrace}");
				return false;
			}

		}

		public bool ResetPassword(string token, string newPassword)
		{
			try
			{
				var user = _context.Users.FirstOrDefault(u => 
					(u.PasswordResetToken == token || (u.PasswordResetToken != null && u.PasswordResetToken.EndsWith("|" + token)))
					&& u.PasswordResetTokenExpiry.HasValue
					&& u.PasswordResetTokenExpiry > DateTime.UtcNow);

				if (user == null) return false;

				user.PasswordHash = newPassword;
				user.PasswordResetToken = null;
				user.PasswordResetTokenExpiry = null;
				_context.SaveChanges();

				return true;
			}
			catch (Exception ex)
			{
				return false;
			}
		}

		public bool HasUserInterests(int userId)
		{
			try
			{
				return _context.UserInterests.Any(ui => ui.UserId == userId);
			}
			catch
			{
				return false;
			}
		}

		public bool HasUserProfile(int userId)
		{
			try
			{
				return _context.UserProfiles.Any(up => up.UserId == userId);
			}
			catch
			{
				return false;
			}
		}
		public User? GetUserById(int userId)
		{
			return _context.Users
				.Include(u => u.Role)
				.FirstOrDefault(u => u.UserId == userId);
		}

		public bool UpdateEmail(int userId, string newEmail)
		{
			try
			{
				var user = _context.Users.Find(userId);
				if (user == null) return false;

				user.Email = newEmail.ToLower().Trim();
				_context.SaveChanges();
				return true;
			}
			catch
			{
				return false;
			}
		}

		public bool UpdatePassword(int userId, string newPasswordHash)
		{
			try
			{
				var user = _context.Users.Find(userId);
				if (user == null) return false;

				user.PasswordHash = newPasswordHash;
				_context.SaveChanges();
				return true;
			}
			catch
			{
				return false;
			}
		}
		public bool UpdateProfile(int userId, string fullName, string? phone)
		{
			try
			{
				var user = _context.Users.Find(userId);
				if (user == null) return false;

				user.FullName = fullName.Trim();
				user.Phone = phone?.Trim();
				_context.SaveChanges();
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
