namespace Quiz_Web.Services.IServices
{
	public interface IEmailService
	{
		Task<bool> SendPasswordResetEmail(string toEmail, string resetLink);
		Task<bool> SendCertificateEmailAsync(string toEmail, string userName, string courseName, byte[] certificateImageBytes, string fileName);
	}
}
