using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Quiz_Web.Services
{
    public class CourseRecommendationService : IHostedService, IDisposable
    {
        private Timer? _timer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CourseRecommendationService> _logger;

        public CourseRecommendationService(
            IServiceProvider serviceProvider,
            ILogger<CourseRecommendationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Course Recommendation Service is starting");

            // Run every day at midnight (you can adjust this)
            // For testing, you might want to set a shorter interval
            _timer = new Timer(
                DoWork,
                null,
                TimeSpan.Zero,  // Start immediately
                TimeSpan.FromHours(24)  // Run every 24 hours
                // For testing: TimeSpan.FromMinutes(5) // Run every 5 minutes
            );

            return Task.CompletedTask;
        }

        private async void DoWork(object? state)
        {
            _logger.LogInformation("Course Recommendation Service is working");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<LearningPlatformContext>();

                // Get all users who have selected interests
                var usersWithInterests = await context.Set<UserInterest>()
                    .Select(ui => ui.UserId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation("Processing recommendations for {Count} users", usersWithInterests.Count);

                foreach (var userId in usersWithInterests)
                {
                    await GenerateRecommendationsForUser(context, userId);
                }

                _logger.LogInformation("Course Recommendation Service completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while generating course recommendations");
            }
        }

        private async Task GenerateRecommendationsForUser(LearningPlatformContext context, int userId)
        {
            try
            {
                // Get user's interested categories
                var userCategoryIds = await context.Set<UserInterest>()
                    .Where(ui => ui.UserId == userId)
                    .Select(ui => ui.CategoryId)
                    .ToListAsync();

                if (!userCategoryIds.Any())
                {
                    return;
                }

                // Get top 3 recommended courses
                var recommendations = await context.Set<Course>()
                    .Where(c => c.CategoryId.HasValue && 
                                userCategoryIds.Contains(c.CategoryId.Value) &&
                                c.IsPublished)
                    // Exclude courses user already purchased
                    .Where(c => !context.Set<CoursePurchase>()
                        .Any(cp => cp.BuyerId == userId && 
                                   cp.CourseId == c.CourseId && 
                                   cp.Status == "Paid"))
                    // Exclude courses already recommended
                    .Where(c => !context.Set<Notification>()
                        .Any(n => n.UserId == userId &&
                                  n.Type == "CourseRecommendation" &&
                                  n.Data != null &&
                                  n.Data.Contains(c.CourseId.ToString())))
                    .Include(c => c.Category)
                    .OrderByDescending(c => c.AverageRating)
                    .ThenByDescending(c => c.CreatedAt)
                    .Take(3)
                    .ToListAsync();

                // Create notifications for each recommended course
                foreach (var course in recommendations)
                {
                    var notification = new Notification
                    {
                        UserId = userId,
                        Type = "CourseRecommendation",
                        Title = "?? xu?t dành riêng cho b?n!",
                        Body = $"D?a trên s? thích \"{course.Category?.Name}\", chúng tôi ngh? b?n s? thích khóa h?c \"{course.Title}\".",
                        Data = JsonSerializer.Serialize(new { CourseId = course.CourseId }),
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    context.Set<Notification>().Add(notification);
                }

                await context.SaveChangesAsync();

                _logger.LogInformation("Generated {Count} recommendations for user {UserId}", 
                    recommendations.Count, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating recommendations for user {UserId}", userId);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Course Recommendation Service is stopping");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
