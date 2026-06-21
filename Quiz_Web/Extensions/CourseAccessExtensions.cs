using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using System.Security.Claims;

namespace Quiz_Web.Extensions
{
    public static class CourseAccessExtensions
    {
        public static async Task<bool> HasCourseAccessAsync(this ClaimsPrincipal user, int courseId, LearningPlatformContext context)
        {
            if (!user.Identity?.IsAuthenticated ?? false)
                return false;

            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return false;

            return await context.CoursePurchases
                .AnyAsync(cp => cp.BuyerId == userId && 
                               cp.CourseId == courseId && 
                               cp.Status == "Paid");
        }

        public static async Task<bool> IsCourseOwnerAsync(this ClaimsPrincipal user, int courseId, LearningPlatformContext context)
        {
            if (!user.Identity?.IsAuthenticated ?? false)
                return false;

            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return false;

            return await context.Courses
                .AnyAsync(c => c.CourseId == courseId && c.OwnerId == userId);
        }

        public static async Task<bool> CanAccessCourseAsync(this ClaimsPrincipal user, int courseId, LearningPlatformContext context)
        {
            if (await user.IsCourseOwnerAsync(courseId, context) ||
                await user.HasCourseAccessAsync(courseId, context))
                return true;

            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return false;

            var now = DateTime.UtcNow;
            return await context.UserSubscriptions.AnyAsync(x =>
                x.UserId == userId &&
                x.Status == SubscriptionStatuses.Active &&
                x.EndDate >= now);
        }
    }
}
