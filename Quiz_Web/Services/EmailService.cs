using Quiz_Web.Services.IServices;
using System.Net;
using System.Net.Mail;

namespace Quiz_Web.Services
{
	public class EmailService : IEmailService
	{
		private readonly IConfiguration _configuration;

		public EmailService(IConfiguration configuration)
		{
			_configuration = configuration;
		}

		public async Task<bool> SendPasswordResetEmail(string toEmail, string resetLink)
		{
			try
			{
				var smtpHost = _configuration["EmailSettings:SmtpHost"];
				var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
				var fromEmail = _configuration["EmailSettings:FromEmail"];
				var fromPassword = _configuration["EmailSettings:FromPassword"];
				var fromName = _configuration["EmailSettings:FromName"];

				var mailMessage = new MailMessage
				{
					From = new MailAddress(fromEmail, fromName),
					Subject = "Đặt lại mật khẩu",
					Body = $@"
						<html>
						<body>
							<h2>Yêu cầu đặt lại mật khẩu</h2>
							<p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình.</p>
							<p>Vui lòng nhấp vào liên kết bên dưới để đặt lại mật khẩu:</p>
							<p><a href='{resetLink}'>Đặt lại mật khẩu</a></p>
							<p>Liên kết này sẽ hết hạn sau 1 giờ.</p>
							<p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.</p>
						</body>
						</html>
					",
					IsBodyHtml = true
				};

				mailMessage.To.Add(toEmail);

				using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
				{
					smtpClient.Credentials = new NetworkCredential(fromEmail, fromPassword);
					smtpClient.EnableSsl = true;
					await smtpClient.SendMailAsync(mailMessage);
				}
				return true;

			}catch(Exception ex)
			{
				Console.WriteLine($"Lỗi gửi mailL: {ex.Message}");
				return false;
			}
		}

		public async Task<bool> SendForgotPasswordCodeEmail(string toEmail, string code)
		{
			try
			{
				var smtpHost = _configuration["EmailSettings:SmtpHost"];
				var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"]);
				var fromEmail = _configuration["EmailSettings:FromEmail"];
				var fromPassword = _configuration["EmailSettings:FromPassword"];
				var fromName = _configuration["EmailSettings:FromName"];

				var mailMessage = new MailMessage
				{
					From = new MailAddress(fromEmail, fromName),
					Subject = "Mã xác thực đặt lại mật khẩu",
					Body = $@"
						<html>
						<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
							<div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;'>
								<h2 style='color: #0d6efd; text-align: center;'>Yêu cầu đặt lại mật khẩu</h2>
								<p>Xin chào,</p>
								<p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình. Vui lòng sử dụng mã xác thực dưới đây để hoàn tất quá trình:</p>
								<div style='text-align: center; margin: 30px 0;'>
									<span style='font-size: 24px; font-weight: bold; letter-spacing: 5px; padding: 10px 20px; background-color: #f8f9fa; border: 1px dashed #0d6efd; border-radius: 4px; color: #0d6efd;'>{code}</span>
								</div>
								<p>Mã xác thực này sẽ hết hạn sau <strong>10 phút</strong>.</p>
								<p>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này và bảo mật tài khoản của mình.</p>
								<hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
								<p style='font-size: 12px; color: #777; text-align: center;'>Đây là email tự động, vui lòng không phản hồi email này.</p>
							</div>
						</body>
						</html>
					",
					IsBodyHtml = true
				};

				mailMessage.To.Add(toEmail);

				using (var smtpClient = new SmtpClient(smtpHost, smtpPort))
				{
					smtpClient.Credentials = new NetworkCredential(fromEmail, fromPassword);
					smtpClient.EnableSsl = true;
					await smtpClient.SendMailAsync(mailMessage);
				}
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Lỗi gửi mã OTP qua mail: {ex.Message}");
				return false;
			}
		}
	}
}
