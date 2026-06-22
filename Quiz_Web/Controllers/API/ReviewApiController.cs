using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReviewApiController : ControllerBase
    {
        private readonly IReviewService _reviewService;
        private readonly ICourseService _courseService;
        private readonly ILogger<ReviewApiController> _logger;

        public ReviewApiController(
            IReviewService reviewService,
            ICourseService courseService,
            ILogger<ReviewApiController> logger)
        {
            _reviewService = reviewService;
            _courseService = courseService;
            _logger = logger;
        }

        // GET: api/ReviewApi/course/{courseId}
        [HttpGet("course/{courseId:int}")]
        public IActionResult GetCourseReviews(int courseId)
        {
            try
            {
                var reviews = _reviewService.GetReviewsByCourse(courseId);
                var reviewsDto = reviews.Select(r => new
                {
                    reviewId = r.ReviewId,
                    rating = r.Rating,
                    comment = r.Comment,
                    createdAt = r.CreatedAt,
                    reviewerName = r.User?.FullName
                }).ToList();

                return Ok(new { success = true, reviews = reviewsDto });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reviews for course {CourseId}", courseId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách đánh giá" });
            }
        }

        // GET: api/ReviewApi/stats/{courseId}
        [HttpGet("stats/{courseId:int}")]
        public IActionResult GetRatingStats(int courseId)
        {
            try
            {
                var distribution = _reviewService.GetRatingDistribution(courseId);
                var totalReviews = distribution.Values.Sum();
                
                var percentages = distribution.ToDictionary(
                    kvp => kvp.Key.ToString(),
                    kvp => totalReviews > 0 ? Math.Round((double)kvp.Value / totalReviews * 100, 1) : 0
                );

                return Ok(new
                {
                    success = true,
                    totalReviews,
                    distribution = distribution.ToDictionary(k => k.Key.ToString(), v => v.Value),
                    percentages
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting review stats for course {CourseId}", courseId);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải thống kê đánh giá" });
            }
        }

        // POST: api/ReviewApi
        [Authorize]
        [HttpPost]
        public IActionResult CreateReview([FromBody] CreateReviewViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Thông tin đánh giá không hợp lệ" });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

                if (!_reviewService.HasUserPurchasedCourse(model.CourseId, userId))
                {
                    return StatusCode(403, new { success = false, message = "Bạn cần mua khóa học này trước khi đánh giá" });
                }

                if (!_reviewService.CanUserReview(model.CourseId, userId))
                {
                    return BadRequest(new { success = false, message = "Bạn đã đánh giá khóa học này rồi" });
                }

                var review = _reviewService.CreateReview(model, userId);
                if (review == null)
                {
                    return StatusCode(500, new { success = false, message = "Không thể tạo đánh giá lúc này" });
                }

                return Ok(new { success = true, reviewId = review.ReviewId, message = "Đánh giá khóa học thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi gửi đánh giá" });
            }
        }

        // PUT: api/ReviewApi/{id}
        [Authorize]
        [HttpPut("{id:int}")]
        public IActionResult EditReview(int id, [FromBody] EditReviewViewModel model)
        {
            try
            {
                if (id != model.ReviewId || !ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Dữ liệu cập nhật không hợp lệ" });
                }

                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var review = _reviewService.GetReviewById(id);

                if (review == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đánh giá" });
                }

                if (review.UserId != userId)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền sửa đánh giá của người khác" });
                }

                var updatedReview = _reviewService.UpdateReview(model, userId);
                if (updatedReview == null)
                {
                    return StatusCode(500, new { success = false, message = "Không thể cập nhật đánh giá lúc này" });
                }

                return Ok(new { success = true, message = "Cập nhật đánh giá thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating review {ReviewId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi cập nhật đánh giá" });
            }
        }

        // DELETE: api/ReviewApi/{id}
        [Authorize]
        [HttpDelete("{id:int}")]
        public IActionResult DeleteReview(int id)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var review = _reviewService.GetReviewById(id);

                if (review == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đánh giá" });
                }

                if (review.UserId != userId)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền xóa đánh giá của người khác" });
                }

                var success = _reviewService.DeleteReview(id, userId);
                if (!success)
                {
                    return StatusCode(500, new { success = false, message = "Không thể xóa đánh giá lúc này" });
                }

                return Ok(new { success = true, message = "Xóa đánh giá thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting review {ReviewId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xóa đánh giá" });
            }
        }
    }
}
