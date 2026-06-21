using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class OnboardingApiController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<OnboardingApiController> _logger;

        public OnboardingApiController(LearningPlatformContext context, ILogger<OnboardingApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/OnboardingApi/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.CourseCategories
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .Select(c => new
                    {
                        categoryId = c.CategoryId,
                        name = c.Name,
                        slug = c.Slug,
                        description = c.Description,
                        iconUrl = c.IconUrl
                    })
                    .ToListAsync();

                return Ok(new { success = true, categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting onboarding categories");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh mục" });
            }
        }

        // POST: api/OnboardingApi/submit
        [HttpPost("submit")]
        public async Task<IActionResult> Submit([FromBody] ApiOnboardingRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Người dùng chưa xác thực" });
                }

                if (request.SelectedCategoryIds == null || !request.SelectedCategoryIds.Any())
                {
                    return BadRequest(new { success = false, message = "Vui lòng chọn ít nhất một chủ đề quan tâm" });
                }

                var existingProfile = await _context.UserProfiles
                    .FirstOrDefaultAsync(up => up.UserId == userId);

                var existingInterests = await _context.UserInterests
                    .Where(ui => ui.UserId == userId)
                    .ToListAsync();

                if (existingProfile != null && existingInterests.Any())
                {
                    return BadRequest(new { success = false, message = "Bạn đã hoàn thành thiết lập hồ sơ trước đó" });
                }

                if (existingProfile == null)
                {
                    var userProfile = new UserProfile
                    {
                        UserId = userId,
                        DoB = request.DoB,
                        Gender = request.Gender,
                        Bio = request.Bio,
                        SchoolName = request.SchoolName,
                        GradeLevel = request.GradeLevel,
                        Locale = "vi-VN",
                        TimeZone = "SE Asia Standard Time"
                    };
                    _context.UserProfiles.Add(userProfile);
                }
                else
                {
                    existingProfile.DoB = request.DoB;
                    existingProfile.Gender = request.Gender;
                    existingProfile.Bio = request.Bio;
                    existingProfile.SchoolName = request.SchoolName;
                    existingProfile.GradeLevel = request.GradeLevel;
                }

                if (existingInterests.Any())
                {
                    _context.UserInterests.RemoveRange(existingInterests);
                }

                var userInterests = request.SelectedCategoryIds.Select(categoryId => new UserInterest
                {
                    UserId = userId,
                    CategoryId = categoryId,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

                await _context.UserInterests.AddRangeAsync(userInterests);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Hoàn thành thiết lập hồ sơ thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error submitting onboarding data");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi lưu dữ liệu thiết lập" });
            }
        }

        // POST: api/OnboardingApi/skip
        [HttpPost("skip")]
        public async Task<IActionResult> Skip()
        {
            try
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Người dùng chưa xác thực" });
                }

                var existingProfile = await _context.UserProfiles
                    .FirstOrDefaultAsync(up => up.UserId == userId);

                if (existingProfile == null)
                {
                    var minimalProfile = new UserProfile
                    {
                        UserId = userId,
                        Locale = "vi-VN",
                        TimeZone = "SE Asia Standard Time"
                    };
                    _context.UserProfiles.Add(minimalProfile);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, message = "Đã bỏ qua thiết lập hồ sơ" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error skipping onboarding");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi bỏ qua thiết lập" });
            }
        }
    }

    public class ApiOnboardingRequest
    {
        public DateOnly? DoB { get; set; }
        public string? Gender { get; set; }
        public string? Bio { get; set; }
        public string? SchoolName { get; set; }
        public string? GradeLevel { get; set; }
        public List<int> SelectedCategoryIds { get; set; } = new();
    }
}
