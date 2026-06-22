namespace Quiz_Web.Services.IServices
{
	public interface IEmailService
	{
		Task<bool> SendPasswordResetEmail(string toEmail, string resetLink);
		Task<bool> SendForgotPasswordCodeEmail(string toEmail, string code);
	}
}
