using Microsoft.AspNetCore.Mvc;

namespace Quiz_Web.Controllers
{
    [Route("upload")]
    public class UploadController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UploadController> _logger;

        public UploadController(IWebHostEnvironment env, ILogger<UploadController> logger)
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
                    return Json(new { uploaded = false, error = new { message = "No file uploaded." } });
                }

                var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(upload.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    return Json(new { uploaded = false, error = new { message = "Unsupported file type." } });
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
                return Json(new { uploaded = true, url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CKEditor image upload failed.");
                return Json(new { uploaded = false, error = new { message = "Upload error." } });
            }
        }
    }
}