using Quiz_Web.Models.Entities;

namespace Quiz_Web.Services.IServices
{
	public interface IUserService
	{
		User Login(string username, string password);
		bool Register(User user);

		bool ExistsUsername(string username);
		bool ExistsEmail(string email);

		//send mail
		User? GetUserByEmail(string email);
		bool GeneratePasswordResetToken(string email, out string token);
		bool GenerateForgotPasswordCode(string email, out string code);
		bool VerifyResetCode(string email, string code, out string secureToken);
		bool ValidatePasswordResetToken(string token);
		bool ResetPassword(string token, string newPassword);

		// Onboarding
		bool HasUserInterests(int userId);
		bool HasUserProfile(int userId);

		// Account Management
		User? GetUserById(int userId);
		bool UpdateEmail(int userId, string newEmail);
		bool UpdatePassword(int userId, string newPasswordHash);
		bool UpdateProfile(int userId, string fullName, string? phone);
	}
}
