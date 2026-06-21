using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LessonApiController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<LessonApiController> _logger;

        public LessonApiController(LearningPlatformContext context, ILogger<LessonApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/LessonApi/{lessonId}
        [HttpGet("{lessonId:int}")]
        public async Task<IActionResult> GetLessonContent(int lessonId)
        {
            try
            {
                var lesson = await _context.Lessons
                    .Include(l => l.Chapter)
                        .ThenInclude(ch => ch.Course)
                            .ThenInclude(c => c.CoursePurchases)
                    .Include(l => l.LessonContents)
                    .FirstOrDefaultAsync(l => l.LessonId == lessonId);

                if (lesson == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy bài học" });
                }

                var course = lesson.Chapter.Course;
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var isOwner = course.OwnerId == userId;
                var hasPurchased = course.CoursePurchases?.Any(p => p.BuyerId == userId && p.Status == "Paid") ?? false;

                if (!isOwner && !hasPurchased)
                {
                    return StatusCode(403, new { success = false, message = "Bạn cần mua khóa học chứa bài học này để xem nội dung" });
                }

                var response = new
                {
                    success = true,
                    lessonId = lesson.LessonId,
                    chapterId = lesson.ChapterId,
                    title = lesson.Title,
                    description = lesson.Description,
                    orderIndex = lesson.OrderIndex,
                    contents = lesson.LessonContents.OrderBy(lc => lc.OrderIndex).Select(lc => new
                    {
                        contentId = lc.ContentId,
                        contentType = lc.ContentType,
                        refId = lc.RefId,
                        title = lc.Title,
                        body = lc.Body,
                        videoUrl = lc.VideoUrl,
                        documentUrl = lc.DocumentUrl,
                        orderIndex = lc.OrderIndex
                    }).ToList()
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading lesson {LessonId} content via API", lessonId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải nội dung bài học" });
            }
        }
    }
}
