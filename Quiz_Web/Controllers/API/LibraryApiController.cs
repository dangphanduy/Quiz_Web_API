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
    public class LibraryApiController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<LibraryApiController> _logger;

        public LibraryApiController(LearningPlatformContext context, ILogger<LibraryApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/LibraryApi/courses
        [HttpGet("courses")]
        public async Task<IActionResult> GetPurchasedCourses()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                var purchasedCourses = await _context.CoursePurchases
                    .Where(cp => cp.BuyerId == userId && cp.Status == "Paid")
                    .Include(cp => cp.Course)
                        .ThenInclude(c => c.Owner)
                    .Select(cp => cp.Course)
                    .Distinct()
                    .ToListAsync();

                var coursesDto = purchasedCourses.Select(c => new
                {
                    courseId = c.CourseId,
                    title = c.Title,
                    slug = c.Slug,
                    summary = c.Summary,
                    coverUrl = c.CoverUrl,
                    price = c.Price,
                    averageRating = c.AverageRating,
                    totalReviews = c.TotalReviews,
                    ownerName = c.Owner?.FullName
                }).ToList();

                return Ok(new { success = true, courses = coursesDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchased courses for library");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách khóa học đã mua" });
            }
        }

        // GET: api/LibraryApi/flashcards
        [HttpGet("flashcards")]
        public async Task<IActionResult> GetOwnedFlashcardSets()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                var flashcardSets = await _context.FlashcardSets
                    .Where(fs => fs.OwnerId == userId && !fs.IsDeleted)
                    .Include(fs => fs.Flashcards)
                    .OrderByDescending(fs => fs.CreatedAt)
                    .ToListAsync();

                var setsDto = flashcardSets.Select(fs => new
                {
                    setId = fs.SetId,
                    title = fs.Title,
                    description = fs.Description,
                    visibility = fs.Visibility,
                    coverUrl = fs.CoverUrl,
                    tagsText = fs.TagsText,
                    language = fs.Language,
                    createdAt = fs.CreatedAt,
                    cardCount = fs.Flashcards?.Count ?? 0
                }).ToList();

                return Ok(new { success = true, flashcardSets = setsDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting flashcard sets for library");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách bộ thẻ học" });
            }
        }

        // GET: api/LibraryApi/wishlist
        [HttpGet("wishlist")]
        public async Task<IActionResult> GetWishlist()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                var wishlistCourses = await _context.SavedItems
                    .Where(si => si.Library.OwnerId == userId && si.ContentType == "course")
                    .Include(si => si.Library)
                    .Select(si => _context.Courses
                        .Include(c => c.Owner)
                        .FirstOrDefault(c => c.CourseId == si.ContentId))
                    .Where(c => c != null)
                    .ToListAsync();

                var wishlistDto = wishlistCourses.Select(c => new
                {
                    courseId = c.CourseId,
                    title = c.Title,
                    slug = c.Slug,
                    summary = c.Summary,
                    coverUrl = c.CoverUrl,
                    price = c.Price,
                    averageRating = c.AverageRating,
                    totalReviews = c.TotalReviews,
                    ownerName = c.Owner?.FullName
                }).ToList();

                return Ok(new { success = true, wishlist = wishlistDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wishlist for library");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách khóa học yêu thích" });
            }
        }
    }
}
