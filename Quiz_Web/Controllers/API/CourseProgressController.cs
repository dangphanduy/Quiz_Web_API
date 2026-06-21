using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Quiz_Web.Controllers.API
{
	[ApiController]
	[Route("api/course-progress")]
	[Authorize]
	public class CourseProgressController : ControllerBase
	{
		private readonly LearningPlatformContext _context;
		private readonly ILogger<CourseProgressController> _logger;

		public CourseProgressController(
			LearningPlatformContext context,
			ILogger<CourseProgressController> logger)
		{
			_context = context;
			_logger = logger;
		}

		// GET: /api/course-progress/get-progress?courseSlug={slug}
		[HttpGet("get-progress")]
		public async Task<IActionResult> GetProgress([FromQuery] string courseSlug)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				// Get course by slug with all lesson contents
				var course = await _context.Courses
					.Include(c => c.CourseChapters)
						.ThenInclude(ch => ch.Lessons)
							.ThenInclude(l => l.LessonContents)
					.FirstOrDefaultAsync(c => c.Slug == courseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// ? Get all valid ContentIds in this course (t? LessonContents)
				var allCourseContentIds = course.CourseChapters
					.SelectMany(ch => ch.Lessons)
					.SelectMany(l => l.LessonContents)
					.Select(c => c.ContentId)
					.Distinct()
					.ToList();

				var totalContents = allCourseContentIds.Count;

				if (totalContents == 0)
				{
					return Ok(new
					{
						success = true,
						completionPercentage = 0.0,
						completedContents = 0,
						totalContents = 0,
						completedLessons = new List<int>(),
						completedContentIds = new List<int>()
					});
				}

				// ? Get completed ContentIds for this user (ch? l?y ContentId ?ă complete)
				var completedContentIds = await _context.CourseProgresses
					.Where(p => p.CourseId == course.CourseId && 
					           p.UserId == userId && 
					           p.IsCompleted && 
					           allCourseContentIds.Contains(p.ContentId)) // ? Ch? l?y ContentId thu?c course này
					.Select(p => p.ContentId)
					.Distinct()
					.ToListAsync();

				var completedContentsCount = completedContentIds.Count;

				// ? Get unique lesson IDs that have at least one completed content
				var completedLessons = await _context.CourseProgresses
					.Where(p => p.CourseId == course.CourseId && 
					           p.UserId == userId && 
					           p.IsCompleted && 
					           p.LessonId.HasValue)
					.Select(p => p.LessonId.Value)
					.Distinct()
					.ToListAsync();

				// ? Calculate completion percentage (??m b?o không v??t quá 100%)
				var completionPercentage = (double)completedContentsCount / totalContents * 100;
				completionPercentage = Math.Min(completionPercentage, 100); // Cap at 100%

				return Ok(new
				{
					success = true,
					completionPercentage = Math.Round(completionPercentage, 2),
					completedContents = completedContentsCount,
					totalContents = totalContents,
					completedLessons = completedLessons,
					completedContentIds = completedContentIds
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting course progress for slug: {Slug}", courseSlug);
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}

		// POST: /api/course-progress/save-progress
		[HttpPost("save-progress")]
		public async Task<IActionResult> SaveProgress([FromBody] SaveProgressRequest request)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				// Get course by slug
				var course = await _context.Courses
					.FirstOrDefaultAsync(c => c.Slug == request.CourseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// Check if lesson exists
				var lesson = await _context.Lessons
					.Include(l => l.LessonContents)
					.FirstOrDefaultAsync(l => l.LessonId == request.LessonId);

				if (lesson == null)
				{
					return NotFound(new { success = false, message = "Lesson not found" });
				}

				// ? Find the video content in this lesson
				var videoContent = lesson.LessonContents
					.FirstOrDefault(c => c.ContentType == "Video");

				if (videoContent == null)
				{
					return NotFound(new { success = false, message = "Video content not found in this lesson" });
				}

				// Find or create progress record for video content
				var progress = await _context.CourseProgresses
					.FirstOrDefaultAsync(p => 
						p.CourseId == course.CourseId && 
						p.UserId == userId && 
						p.LessonId == request.LessonId &&
						p.ContentType == "Video" &&
						p.ContentId == videoContent.ContentId);

				if (progress == null)
				{
					// Create new progress
					progress = new CourseProgress
					{
						UserId = userId,
						CourseId = course.CourseId,
						LessonId = request.LessonId,
						ContentType = "Video",
						ContentId = videoContent.ContentId,
						IsCompleted = false,
						LastViewedAt = DateTime.UtcNow,
						DurationSec = request.WatchedDuration
					};
					_context.CourseProgresses.Add(progress);
				}
				else
				{
					// Update existing progress
					progress.LastViewedAt = DateTime.UtcNow;
					progress.DurationSec = request.WatchedDuration;
				}

				await _context.SaveChangesAsync();

				return Ok(new { success = true, message = "Progress saved" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error saving progress for lesson: {LessonId}", request.LessonId);
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}

		// POST: /api/course-progress/mark-complete
		[HttpPost("mark-complete")]
		public async Task<IActionResult> MarkComplete([FromBody] MarkCompleteRequest request)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				// Get course by slug
				var course = await _context.Courses
					.FirstOrDefaultAsync(c => c.Slug == request.CourseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// Check if lesson exists
				var lesson = await _context.Lessons
					.Include(l => l.LessonContents)
					.FirstOrDefaultAsync(l => l.LessonId == request.LessonId);

				if (lesson == null)
				{
					return NotFound(new { success = false, message = "Lesson not found" });
				}

				// ? Find the video content in this lesson
				var videoContent = lesson.LessonContents
					.FirstOrDefault(c => c.ContentType == "Video");

				if (videoContent == null)
				{
					return NotFound(new { success = false, message = "Video content not found in this lesson" });
				}

				// Find or create progress record for video content
				var progress = await _context.CourseProgresses
					.FirstOrDefaultAsync(p => 
						p.CourseId == course.CourseId && 
						p.UserId == userId && 
						p.LessonId == request.LessonId &&
						p.ContentType == "Video" &&
						p.ContentId == videoContent.ContentId);

				if (progress == null)
				{
					// Create new progress
					progress = new CourseProgress
					{
						UserId = userId,
						CourseId = course.CourseId,
						LessonId = request.LessonId,
						ContentType = "Video",
						ContentId = videoContent.ContentId,
						IsCompleted = true,
						CompletionAt = DateTime.UtcNow,
						LastViewedAt = DateTime.UtcNow,
						DurationSec = request.WatchedDuration
					};
					_context.CourseProgresses.Add(progress);
				}
				else
				{
					// Update existing progress
					progress.IsCompleted = true;
					progress.CompletionAt = DateTime.UtcNow;
					progress.LastViewedAt = DateTime.UtcNow;
					progress.DurationSec = request.WatchedDuration;
				}

				await _context.SaveChangesAsync();

				_logger.LogInformation("User {UserId} completed video in lesson {LessonId}", userId, request.LessonId);

				return Ok(new { success = true, message = "Video marked as complete" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error marking lesson complete: {LessonId}", request.LessonId);
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}

		// POST: /api/course-progress/mark-content-complete
		[HttpPost("mark-content-complete")]
		public async Task<IActionResult> MarkContentComplete([FromBody] MarkContentCompleteRequest request)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				// Get course by slug
				var course = await _context.Courses
					.FirstOrDefaultAsync(c => c.Slug == request.CourseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// Check if lesson exists
				var lesson = await _context.Lessons
					.FirstOrDefaultAsync(l => l.LessonId == request.LessonId);

				if (lesson == null)
				{
					return NotFound(new { success = false, message = "Lesson not found" });
				}

				// ? Verify content exists in lesson
				var content = await _context.LessonContents
					.FirstOrDefaultAsync(c => c.ContentId == request.ContentId && c.LessonId == request.LessonId);

				if (content == null)
				{
					return NotFound(new { success = false, message = "Content not found in this lesson" });
				}

				// Find or create progress record for this specific content
				var progress = await _context.CourseProgresses
					.FirstOrDefaultAsync(p => 
						p.CourseId == course.CourseId && 
						p.UserId == userId && 
						p.LessonId == request.LessonId &&
						p.ContentType == request.ContentType &&
						p.ContentId == request.ContentId);

				if (progress == null)
				{
					// Create new progress
					progress = new CourseProgress
					{
						UserId = userId,
						CourseId = course.CourseId,
						LessonId = request.LessonId,
						ContentType = request.ContentType,
						ContentId = request.ContentId,
						IsCompleted = true,
						CompletionAt = DateTime.UtcNow,
						LastViewedAt = DateTime.UtcNow
					};
					_context.CourseProgresses.Add(progress);
				}
				else
				{
					// Update existing progress
					progress.IsCompleted = true;
					progress.CompletionAt = DateTime.UtcNow;
					progress.LastViewedAt = DateTime.UtcNow;
				}

				await _context.SaveChangesAsync();

				_logger.LogInformation("User {UserId} completed content {ContentId} ({ContentType}) in lesson {LessonId}", 
					userId, request.ContentId, request.ContentType, request.LessonId);

				return Ok(new { success = true, message = $"{request.ContentType} content marked as complete" });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error marking content complete: {ContentId}", request.ContentId);
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}

		// ? DEBUG ENDPOINT - Get detailed progress info
		[HttpGet("debug-progress")]
		public async Task<IActionResult> DebugProgress([FromQuery] string courseSlug)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				var course = await _context.Courses
					.Include(c => c.CourseChapters)
						.ThenInclude(ch => ch.Lessons)
							.ThenInclude(l => l.LessonContents)
					.FirstOrDefaultAsync(c => c.Slug == courseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// Get all contents in course
				var allContents = course.CourseChapters
					.SelectMany(ch => ch.Lessons)
					.SelectMany(l => l.LessonContents)
					.Select(c => new
					{
						c.ContentId,
						c.ContentType,
						c.LessonId,
						c.Title,
						LessonTitle = c.Lesson.Title
					})
					.ToList();

				// Get all progress records for this user and course
				var progressRecords = await _context.CourseProgresses
					.Where(p => p.CourseId == course.CourseId && p.UserId == userId)
					.Select(p => new
					{
						p.ProgressId,
						p.ContentId,
						p.ContentType,
						p.LessonId,
						p.IsCompleted,
						p.CompletionAt
					})
					.ToListAsync();

				return Ok(new
				{
					success = true,
					courseId = course.CourseId,
					courseName = course.Title,
					allContents = allContents,
					totalContents = allContents.Count,
					progressRecords = progressRecords,
					totalProgressRecords = progressRecords.Count,
					completedRecords = progressRecords.Count(p => p.IsCompleted),
					// ? Find duplicate or invalid records
					duplicateContentIds = progressRecords
						.GroupBy(p => p.ContentId)
						.Where(g => g.Count() > 1)
						.Select(g => new { ContentId = g.Key, Count = g.Count() })
						.ToList(),
					invalidContentIds = progressRecords
						.Where(p => !allContents.Any(c => c.ContentId == p.ContentId))
						.Select(p => p.ContentId)
						.Distinct()
						.ToList()
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error debugging progress");
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}

		// ? CLEANUP ENDPOINT - Remove invalid progress records
		[HttpPost("cleanup-progress")]
		public async Task<IActionResult> CleanupProgress([FromQuery] string courseSlug)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				var course = await _context.Courses
					.Include(c => c.CourseChapters)
						.ThenInclude(ch => ch.Lessons)
							.ThenInclude(l => l.LessonContents)
					.FirstOrDefaultAsync(c => c.Slug == courseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// Get valid ContentIds
				var validContentIds = course.CourseChapters
					.SelectMany(ch => ch.Lessons)
					.SelectMany(l => l.LessonContents)
					.Select(c => c.ContentId)
					.ToList();

				// Find invalid progress records (ContentId not in course anymore)
				var invalidRecords = await _context.CourseProgresses
					.Where(p => p.CourseId == course.CourseId && 
					           p.UserId == userId && 
					           !validContentIds.Contains(p.ContentId))
					.ToListAsync();

				if (invalidRecords.Any())
				{
					_context.CourseProgresses.RemoveRange(invalidRecords);
					await _context.SaveChangesAsync();

					_logger.LogInformation("Cleaned up {Count} invalid progress records for user {UserId} in course {CourseId}", 
						invalidRecords.Count, userId, course.CourseId);

					return Ok(new
					{
						success = true,
						message = $"Cleaned up {invalidRecords.Count} invalid records",
						removedRecords = invalidRecords.Select(r => new
						{
							r.ProgressId,
							r.ContentId,
							r.ContentType
						})
					});
				}

				return Ok(new
				{
					success = true,
					message = "No invalid records found"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error cleaning up progress");
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}

		// GET: /api/course-progress/check-content-completion
		[HttpGet("check-content-completion")]
		public async Task<IActionResult> CheckContentCompletion(
			[FromQuery] string courseSlug, 
			[FromQuery] int lessonId, 
			[FromQuery] int contentId)
		{
			try
			{
				var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
				if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				{
					return Unauthorized(new { success = false, message = "Unauthorized" });
				}

				// Get course by slug
				var course = await _context.Courses
					.FirstOrDefaultAsync(c => c.Slug == courseSlug);

				if (course == null)
				{
					return NotFound(new { success = false, message = "Course not found" });
				}

				// Check if content completion exists for this user
				var isCompleted = await _context.CourseProgresses
					.AnyAsync(p => 
						p.CourseId == course.CourseId && 
						p.UserId == userId && 
						p.LessonId == lessonId &&
						p.ContentId == contentId &&
						p.IsCompleted);

				return Ok(new 
				{ 
					success = true, 
					isCompleted = isCompleted 
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking content completion for contentId: {ContentId}", contentId);
				return StatusCode(500, new { success = false, message = "Internal server error" });
			}
		}
	}

	// Request models
	public class SaveProgressRequest
	{
		public string CourseSlug { get; set; } = string.Empty;
		public int LessonId { get; set; }
		public int WatchedDuration { get; set; }
		public int TotalDuration { get; set; }
	}

	public class MarkCompleteRequest
	{
		public string CourseSlug { get; set; } = string.Empty;
		public int LessonId { get; set; }
		public int WatchedDuration { get; set; }
	}

	public class MarkContentCompleteRequest
	{
		public string CourseSlug { get; set; } = string.Empty;
		public int LessonId { get; set; }
		public int ContentId { get; set; }
		public string ContentType { get; set; } = string.Empty;
	}
}
