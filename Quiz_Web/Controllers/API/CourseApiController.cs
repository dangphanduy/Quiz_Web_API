using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Services.IServices;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourseApiController : ControllerBase
    {
        private readonly ICourseService _courseService;
        private readonly ILogger<CourseApiController> _logger;
        private readonly ICourseAccessService _courseAccessService;

        public CourseApiController(
            ICourseService courseService,
            ILogger<CourseApiController> logger,
            ICourseAccessService courseAccessService)
        {
            _courseService = courseService;
            _logger = logger;
            _courseAccessService = courseAccessService;
        }

        // GET: api/CourseApi
        [HttpGet]
        public IActionResult GetCourses(
            [FromQuery] string? search,
            [FromQuery] string? category,
            [FromQuery] decimal? minRating,
            [FromQuery] decimal? maxRating,
            [FromQuery] bool? isFree,
            [FromQuery] string? sortBy,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12)
        {
            try
            {
                var courses = _courseService.GetFilteredAndSortedCourses(
                    searchKeyword: search,
                    categorySlug: category,
                    minRating: minRating,
                    maxRating: maxRating,
                    isFree: isFree,
                    sortBy: sortBy);

                var totalCount = courses.Count;
                var paginatedCourses = courses
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new
                    {
                        courseId = c.CourseId,
                        title = c.Title,
                        slug = c.Slug,
                        summary = c.Summary,
                        coverUrl = c.CoverUrl,
                        price = c.Price,
                        averageRating = c.AverageRating,
                        totalReviews = c.TotalReviews,
                        categoryName = c.Category?.Name,
                        categorySlug = c.Category?.Slug
                    }).ToList();

                return Ok(new
                {
                    success = true,
                    totalCount,
                    page,
                    pageSize,
                    courses = paginatedCourses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting courses via API");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách khóa học" });
            }
        }

        // GET: api/CourseApi/{slug}
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetCourseDetail(string slug)
        {
            try
            {
                var course = _courseService.GetCourseBySlugWithFullDetails(slug);
                if (course == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khóa học" });
                }

                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var isOwner = false;
                var hasAccess = false;
                if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
                {
                    isOwner = course.OwnerId == userId;
                    hasAccess = isOwner ||
                        await _courseAccessService.CheckCourseAccessAsync(userId, course.CourseId, HttpContext.RequestAborted);
                }

                var courseDetail = new
                {
                    courseId = course.CourseId,
                    title = course.Title,
                    slug = course.Slug,
                    summary = course.Summary,
                    coverUrl = course.CoverUrl,
                    price = course.Price,
                    averageRating = course.AverageRating,
                    totalReviews = course.TotalReviews,
                    createdAt = course.CreatedAt,
                    categoryName = course.Category?.Name,
                    ownerName = course.Owner?.FullName,
                    hasAccess,
                    isOwner,
                    chapters = course.CourseChapters?.OrderBy(ch => ch.OrderIndex).Select(ch => new
                    {
                        chapterId = ch.ChapterId,
                        title = ch.Title,
                        description = ch.Description,
                        orderIndex = ch.OrderIndex,
                        lessons = ch.Lessons?.OrderBy(l => l.OrderIndex).Select(l => new
                        {
                            lessonId = l.LessonId,
                            title = l.Title,
                            orderIndex = l.OrderIndex
                        }).ToList()
                    }).ToList()
                };

                return Ok(new { success = true, course = courseDetail });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course details for {Slug}", slug);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải chi tiết khóa học" });
            }
        }

        // GET: api/CourseApi/{slug}/learn
        [Authorize]
        [HttpGet("{slug}/learn")]
        public async Task<IActionResult> GetCourseLearning(string slug)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var course = _courseService.GetCourseBySlugWithFullDetails(slug);
                if (course == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy khóa học" });
                }

                var isOwner = course.OwnerId == userId;
                var hasAccess = isOwner ||
                    await _courseAccessService.CheckCourseAccessAsync(userId, course.CourseId, HttpContext.RequestAborted);

                if (!hasAccess)
                {
                    return StatusCode(403, new { success = false, message = "Bạn cần mua khóa học này để học nội dung" });
                }

                var learnData = new
                {
                    courseId = course.CourseId,
                    title = course.Title,
                    slug = course.Slug,
                    coverUrl = course.CoverUrl,
                    chapters = course.CourseChapters?.OrderBy(ch => ch.OrderIndex).Select(ch => new
                    {
                        chapterId = ch.ChapterId,
                        title = ch.Title,
                        description = ch.Description,
                        orderIndex = ch.OrderIndex,
                        lessons = ch.Lessons?.OrderBy(l => l.OrderIndex).Select(l => new
                        {
                            lessonId = l.LessonId,
                            title = l.Title,
                            orderIndex = l.OrderIndex
                        }).ToList()
                    }).ToList()
                };

                return Ok(new { success = true, course = learnData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting course learning data for {Slug}", slug);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải nội dung học" });
            }
        }
    }
}
