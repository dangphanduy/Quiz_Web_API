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

		public async Task<bool> SendCertificateEmailAsync(string toEmail, string userName, string courseName, byte[] certificateImageBytes, string fileName)
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
					Subject = $"Chúc mừng hoàn thành khóa học: {courseName}",
					Body = $@"
						<html>
						<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
							<div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
								<h2 style='color: #a435f0; text-align: center;'>Chúc mừng bạn đã hoàn thành khóa học!</h2>
								<p>Xin chào <strong>{userName}</strong>,</p>
								<p>Chúc mừng bạn đã hoàn thành xuất sắc khóa học <strong>{courseName}</strong> trên hệ thống học tập <strong>ymedu</strong>!</p>
								<p>Đây là một cột mốc quan trọng chứng minh sự nỗ lực học tập của bạn. Để ghi nhận thành tích này, chúng tôi gửi kèm ảnh chứng chỉ hoàn thành khóa học trong email này.</p>
								<p>Chúc bạn tiếp tục phát triển và gặt hái thêm nhiều thành công trên con đường sắp tới!</p>
								<hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
								<p style='font-size: 12px; color: #777; text-align: center;'>Email này được gửi tự động từ hệ thống ymedu.</p>
							</div>
						</body>
						</html>
					",
					IsBodyHtml = true
				};

				mailMessage.To.Add(toEmail);

				if (certificateImageBytes != null && certificateImageBytes.Length > 0)
				{
					var attachmentStream = new MemoryStream(certificateImageBytes);
					var attachment = new Attachment(attachmentStream, fileName, "image/png");
					mailMessage.Attachments.Add(attachment);
				}

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
				Console.WriteLine($"Lỗi gửi mail chứng chỉ: {ex.Message}");
				return false;
			}
		}
	}
}
