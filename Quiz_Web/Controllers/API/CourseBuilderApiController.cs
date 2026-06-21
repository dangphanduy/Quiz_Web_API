using Ganss.Xss;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Authorize]
    [Route("api/courses/builder")]
    [ApiController]
    public class CourseBuilderApiController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<CourseBuilderApiController> _logger;
        private readonly IStorageService _storageService;

        public CourseBuilderApiController(
            ICourseService courseService,
            IWebHostEnvironment env,
            ILogger<CourseBuilderApiController> logger,
            IStorageService storageService)
        {
            _courseService = courseService;
            _env = env;
            _logger = logger;
            _storageService = storageService;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }
            return userId;
        }

        // GET: api/courses/builder/categories
        [HttpGet("categories")]
        public IActionResult GetCategories()
        {
            try
            {
                var categories = _courseService.GetAllCategories();
                return Ok(new { success = true, categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting course builder categories");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh mục" });
            }
        }

        // GET: api/courses/builder/data/{courseId}
        [HttpGet("data/{courseId:int}")]
        public IActionResult GetBuilderData(int courseId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var data = _courseService.GetCourseBuilderData(courseId, userId);
                if (data == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khóa học hoặc bạn không có quyền sở hữu" });
                }
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting course builder data for {CourseId}", courseId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải dữ liệu khóa học" });
            }
        }

        // POST: api/courses/builder/autosave
        [HttpPost("autosave")]
        public IActionResult Autosave([FromBody] CourseAutosaveViewModel model)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (!_courseService.IsSlugUnique(model.Slug, model.CourseId))
                {
                    return StatusCode(409, new { success = false, code = "DuplicateSlug", message = "Slug này đã tồn tại." });
                }

                var success = _courseService.AutosaveCourse(model.CourseId, model, userId);
                return Ok(new CourseBuilderResponse
                {
                    Success = success,
                    Message = success ? "Đã lưu tự động" : "Lỗi lưu tự động"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error on course builder autosave");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi lưu tự động" });
            }
        }

        // POST: api/courses/builder/save
        [HttpPost("save")]
        public async Task<IActionResult> SaveBuilder(
            [FromForm] string jsonData,
            IFormFile? coverFile,
            [FromServices] HtmlSanitizer sanitizer)
        {
            try
            {
                var userId = GetCurrentUserId();
                var model = System.Text.Json.JsonSerializer.Deserialize<CourseBuilderViewModel>(
                    jsonData,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

                if (model == null)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                // Upload cover image to GCS
                if (coverFile is { Length: > 0 })
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        return BadRequest(new { success = false, message = "Định dạng ảnh không hợp lệ (jpg, jpeg, png, gif, webp)." });
                    }

                    model.CoverUrl = await _storageService.UploadFileAsync(coverFile, "uploads/courses");
                }

                // Sanitize HTML content
                if (!string.IsNullOrEmpty(model.Summary))
                    model.Summary = sanitizer.Sanitize(model.Summary);

                foreach (var chapter in model.Chapters)
                {
                    if (!string.IsNullOrEmpty(chapter.Description))
                        chapter.Description = sanitizer.Sanitize(chapter.Description);

                    foreach (var lesson in chapter.Lessons)
                    {
                        foreach (var content in lesson.Contents)
                        {
                            if (!string.IsNullOrEmpty(content.Body))
                                content.Body = sanitizer.Sanitize(content.Body);
                        }
                    }
                }

                // Check slug uniqueness
                if (!_courseService.IsSlugUnique(model.Slug))
                {
                    return StatusCode(409, new { success = false, message = "Slug này đã tồn tại. Vui lòng chọn slug khác." });
                }

                // Create course with full structure
                var course = _courseService.CreateCourseWithStructure(model, userId);

                if (course == null)
                {
                    return BadRequest(new { success = false, message = "Có lỗi xảy ra khi tạo khóa học" });
                }

                return Ok(new { success = true, message = "Tạo khóa học thành công!", courseId = course.CourseId, slug = course.Slug });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error saving course builder");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // PUT: api/courses/builder/update/{id}
        [HttpPut("update/{id:int}")]
        public async Task<IActionResult> UpdateBuilder(
            int id,
            [FromForm] string jsonData,
            IFormFile? coverFile,
            [FromServices] HtmlSanitizer sanitizer)
        {
            try
            {
                var userId = GetCurrentUserId();
                var model = System.Text.Json.JsonSerializer.Deserialize<CourseBuilderViewModel>(
                    jsonData,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }
                );

                if (model == null)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ" });
                }

                // Upload cover image to GCS
                if (coverFile is { Length: > 0 })
                {
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        return BadRequest(new { success = false, message = "Định dạng ảnh không hợp lệ (jpg, jpeg, png, gif, webp)." });
                    }

                    model.CoverUrl = await _storageService.UploadFileAsync(coverFile, "uploads/courses");
                }

                // Sanitize HTML content
                if (!string.IsNullOrEmpty(model.Summary))
                    model.Summary = sanitizer.Sanitize(model.Summary);

                foreach (var chapter in model.Chapters)
                {
                    if (!string.IsNullOrEmpty(chapter.Description))
                        chapter.Description = sanitizer.Sanitize(chapter.Description);

                    foreach (var lesson in chapter.Lessons)
                    {
                        foreach (var content in lesson.Contents)
                        {
                            if (!string.IsNullOrEmpty(content.Body))
                                content.Body = sanitizer.Sanitize(content.Body);
                        }
                    }
                }

                // Update course structure
                var course = _courseService.UpdateCourseStructure(id, model, userId);

                if (course == null)
                {
                    return BadRequest(new { success = false, message = "Không thể cập nhật khóa học hoặc bạn không có quyền sở hữu." });
                }

                return Ok(new { success = true, message = "Cập nhật khóa học thành công!", courseId = course.CourseId, slug = course.Slug });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error updating course builder for {CourseId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // GET: api/courses/builder/check-slug
        [HttpGet("check-slug")]
        public IActionResult CheckSlug([FromQuery] string slug, [FromQuery] int? excludeId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(slug))
                    return BadRequest(new { success = false, available = false, message = "Slug không hợp lệ" });

                var available = _courseService.IsSlugUnique(slug, excludeId);
                return Ok(new { success = true, available });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error checking slug");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi kiểm tra slug" });
            }
        }

        // POST: api/courses/builder/upload-video
        [HttpPost("upload-video")]
        [RequestSizeLimit(104_857_600)] // 100MB limit
        [RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
        public async Task<IActionResult> UploadVideo(IFormFile video)
        {
            try
            {
                if (video == null || video.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Không có file video được tải lên." });
                }

                // Validate file type
                var allowed = new[] { ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv" };
                var ext = Path.GetExtension(video.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    return BadRequest(new { success = false, message = $"Định dạng video không hợp lệ. Chỉ chấp nhận: {string.Join(", ", allowed)}" });
                }

                // Validate file size (100MB)
                const long maxSize = 104_857_600;
                if (video.Length > maxSize)
                {
                    return BadRequest(new { success = false, message = "Kích thước video không được vượt quá 100MB." });
                }

                // Upload to GCS
                var videoUrl = await _storageService.UploadFileAsync(video, "uploads/videos");
                return Ok(new { success = true, videoUrl = videoUrl, url = videoUrl, message = "Tải lên video thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error uploading course builder video");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải video lên: " + ex.Message });
            }
        }

        // POST: api/courses/builder/upload-pdf
        [HttpPost("upload-pdf")]
        [RequestSizeLimit(52_428_800)] // 50MB limit
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> UploadPdf(IFormFile pdf)
        {
            try
            {
                if (pdf == null || pdf.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Không có file tài liệu được tải lên." });
                }

                // Validate file type
                var allowed = new[] { ".pdf" };
                var ext = Path.GetExtension(pdf.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    return BadRequest(new { success = false, message = "Định dạng tài liệu không hợp lệ. Chỉ chấp nhận file PDF." });
                }

                // Validate file size (50MB)
                const long maxSize = 52_428_800;
                if (pdf.Length > maxSize)
                {
                    return BadRequest(new { success = false, message = "Kích thước tài liệu không được vượt quá 50MB." });
                }

                // Upload to GCS
                var pdfUrl = await _storageService.UploadFileAsync(pdf, "uploads/documents");
                return Ok(new { success = true, pdfUrl = pdfUrl, url = pdfUrl, message = "Tải lên tài liệu thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error uploading course builder pdf");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải tài liệu lên: " + ex.Message });
            }
        }
    }
}
