using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Quiz_Web.Controllers.API
{
    [Authorize]
    [Route("api/upload")]
    [ApiController]
    public class UploadApiController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UploadApiController> _logger;

        public UploadApiController(IWebHostEnvironment env, ILogger<UploadApiController> logger)
        {
            _env = env;
            _logger = logger;
        }

        // CKEditor 5 "ckfinder" upload adapter compatible
        [HttpPost("ck-editor")]
        [RequestSizeLimit(20_000_000)] // ~20MB
        public async Task<IActionResult> CkEditorImage(IFormFile upload)
        {
            try
            {
                if (upload == null || upload.Length == 0)
                {
                    return BadRequest(new { uploaded = false, error = new { message = "Không có file nào được tải lên." } });
                }

                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(upload.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    return BadRequest(new { uploaded = false, error = new { message = "Định dạng file không được hỗ trợ." } });
                }

                var folder = $"uploads/ck/{DateTimeHelper.Now:yyyy/MM}";
                var physical = Path.Combine(_env.WebRootPath, folder);
                Directory.CreateDirectory(physical);

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(physical, fileName);

                await using (var stream = System.IO.File.Create(fullPath))
                {
                    await upload.CopyToAsync(stream);
                }

                var url = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
                return Ok(new { uploaded = true, url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CKEditor image upload failed via API.");
                return StatusCode(500, new { uploaded = false, error = new { message = "Lỗi khi tải file lên hệ thống." } });
            }
        }
    }
}
