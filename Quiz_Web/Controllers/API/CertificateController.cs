using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Services.IServices;
using System;
using System.Threading.Tasks;

namespace Quiz_Web.Controllers.API
{
    [ApiController]
    [Route("api/certificates")]
    public class CertificateController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly ICertificateService _certificateService;
        private readonly ILogger<CertificateController> _logger;

        public CertificateController(
            LearningPlatformContext context,
            ICertificateService certificateService,
            ILogger<CertificateController> logger)
        {
            _context = context;
            _certificateService = certificateService;
            _logger = logger;
        }

        // GET: /api/certificates/{verifyCode}/image
        [HttpGet("{verifyCode}/image")]
        public async Task<IActionResult> GetCertificateImage(string verifyCode, [FromQuery] bool download = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(verifyCode))
                {
                    return BadRequest(new { success = false, message = "Verify code is required." });
                }

                // Load certificate with Course, Course.Owner and User
                var certificate = await _context.Certificates
                    .Include(c => c.Course)
                        .ThenInclude(co => co.Owner)
                    .Include(c => c.User)
                    .FirstOrDefaultAsync(c => c.VerifyCode == verifyCode);

                if (certificate == null)
                {
                    return NotFound(new { success = false, message = "Certificate not found." });
                }

                // Generate image bytes using SkiaSharp
                var imageBytes = await _certificateService.GenerateCertificateImageAsync(
                    certificate.User.FullName,
                    certificate.Course.Title,
                    certificate.Course.Owner?.FullName ?? "ymedu Instructor",
                    certificate.IssuedAt,
                    certificate.VerifyCode,
                    certificate.Serial ?? certificate.CertId.ToString("D4")
                );

                var contentType = "image/png";
                var fileName = $"Certificate_{certificate.Course.Slug}.png";

                if (download)
                {
                    return File(imageBytes, contentType, fileName);
                }

                return File(imageBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating certificate image for verify code: {VerifyCode}", verifyCode);
                return StatusCode(500, new { success = false, message = "Internal server error occurred while rendering the certificate." });
            }
        }
    }
}
