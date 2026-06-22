using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services
{
	public class ReviewService : IReviewService
	{
		private readonly LearningPlatformContext _context;
		private readonly ILogger<ReviewService> _logger;

		public ReviewService(LearningPlatformContext context, ILogger<ReviewService> logger)
		{
			_context = context;
			_logger = logger;
		}

		// Get reviews
		public List<CourseReview> GetReviewsByCourse(int courseId)
		{
			try
			{
				return _context.CourseReviews
					.Include(r => r.User)
					.Where(r => r.CourseId == courseId && r.IsApproved)
					.OrderByDescending(r => r.CreatedAt)
					.ToList();
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving reviews for course {CourseId}", courseId);
				return new List<CourseReview>();
			}
		}

		public CourseReview? GetReviewById(int reviewId)
		{
			try
			{
				return _context.CourseReviews
					.Include(r => r.User)
					.Include(r => r.Course)
					.FirstOrDefault(r => r.ReviewId == reviewId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving review {ReviewId}", reviewId);
				return null;
			}
		}

		public CourseReview? GetUserReviewForCourse(int courseId, int userId)
		{
			try
			{
				return _context.CourseReviews
					.Include(r => r.User)
					.FirstOrDefault(r => r.CourseId == courseId && r.UserId == userId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error retrieving user review for course {CourseId}, user {UserId}", courseId, userId);
				return null;
			}
		}

		// Check permissions
		public bool HasUserPurchasedCourse(int courseId, int userId)
		{
			try
			{
				var hasPurchased = _context.CoursePurchases
					.Any(p => p.CourseId == courseId && p.BuyerId == userId && p.Status == "Paid");

				if (hasPurchased)
					return true;

				var now = DateTimeHelper.Now;
				return _context.UserSubscriptions.Any(subscription =>
					subscription.UserId == userId &&
					subscription.Status == SubscriptionStatuses.Active &&
					subscription.EndDate >= now);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking purchase status for course {CourseId}, user {UserId}", courseId, userId);
				return false;
			}
		}

		public bool CanUserReview(int courseId, int userId)
		{
			try
			{
				var isOwner = _context.Courses
					.Any(course => course.CourseId == courseId && course.OwnerId == userId);

				if (isOwner)
					return false;

				// User can review if:
				// 1. They purchased the course, AND
				// 2. They haven't reviewed it yet
				var hasPurchased = HasUserPurchasedCourse(courseId, userId);
				if (!hasPurchased) return false;

				var hasReviewed = _context.CourseReviews
					.Any(r => r.CourseId == courseId && r.UserId == userId);

				return !hasReviewed;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error checking review permission for course {CourseId}, user {UserId}", courseId, userId);
				return false;
			}
		}

		// CRUD operations
		public CourseReview? CreateReview(CreateReviewViewModel model, int userId)
		{
			try
			{
				// Verify user can review
				if (!CanUserReview(model.CourseId, userId))
				{
					_logger.LogWarning("User {UserId} cannot review course {CourseId}", userId, model.CourseId);
					return null;
				}

				var review = new CourseReview
				{
					CourseId = model.CourseId,
					UserId = userId,
					Rating = model.Rating,
					Comment = model.Comment?.Trim(),
					CreatedAt = DateTimeHelper.Now,
					IsApproved = true // Auto-approve reviews (can be changed to false for moderation)
				};

				_context.CourseReviews.Add(review);
				_context.SaveChanges();

				_logger.LogInformation("User {UserId} created review {ReviewId} for course {CourseId}", 
					userId, review.ReviewId, model.CourseId);

				// Reload to include User navigation property
				return GetReviewById(review.ReviewId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error creating review for course {CourseId}, user {UserId}", model.CourseId, userId);
				return null;
			}
		}

		public CourseReview? UpdateReview(EditReviewViewModel model, int userId)
		{
			try
			{
				var review = _context.CourseReviews
					.FirstOrDefault(r => r.ReviewId == model.ReviewId && r.UserId == userId);

				if (review == null)
				{
					_logger.LogWarning("Review {ReviewId} not found or user {UserId} is not the owner", 
						model.ReviewId, userId);
					return null;
				}

				review.Rating = model.Rating;
				review.Comment = model.Comment?.Trim();
				review.UpdatedAt = DateTimeHelper.Now;

				_context.SaveChanges();

				_logger.LogInformation("User {UserId} updated review {ReviewId}", userId, model.ReviewId);

				// Reload to include User navigation property
				return GetReviewById(review.ReviewId);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating review {ReviewId}, user {UserId}", model.ReviewId, userId);
				return null;
			}
		}

		public bool DeleteReview(int reviewId, int userId)
		{
			try
			{
				var review = _context.CourseReviews
					.FirstOrDefault(r => r.ReviewId == reviewId && r.UserId == userId);

				if (review == null)
				{
					_logger.LogWarning("Review {ReviewId} not found or user {UserId} is not the owner", 
						reviewId, userId);
					return false;
				}

				_context.CourseReviews.Remove(review);
				_context.SaveChanges();

				_logger.LogInformation("User {UserId} deleted review {ReviewId}", userId, reviewId);

				return true;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error deleting review {ReviewId}, user {UserId}", reviewId, userId);
				return false;
			}
		}

		// Statistics
		public Dictionary<int, int> GetRatingDistribution(int courseId)
		{
			try
			{
				var distribution = new Dictionary<int, int>
				{
					{ 5, 0 },
					{ 4, 0 },
					{ 3, 0 },
					{ 2, 0 },
					{ 1, 0 }
				};

				var reviews = _context.CourseReviews
					.Where(r => r.CourseId == courseId && r.IsApproved)
					.ToList();

				foreach (var review in reviews)
				{
					int ratingValue = (int)Math.Round(review.Rating);
					if (distribution.ContainsKey(ratingValue))
					{
						distribution[ratingValue]++;
					}
				}

				return distribution;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error getting rating distribution for course {CourseId}", courseId);
				return new Dictionary<int, int>();
			}
		}
	}
}
