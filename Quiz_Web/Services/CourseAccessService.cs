using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services;

public class CourseAccessService : ICourseAccessService
{
    private readonly LearningPlatformContext _context;
    private readonly TimeProvider _timeProvider;

    public CourseAccessService(LearningPlatformContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<bool> CheckCourseAccessAsync(
        int userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        // Bước 1: SQL EXISTS trên index BuyerId/CourseId/Status và short-circuit ngay khi đã mua lẻ.
        var ownsCourse = await _context.CoursePurchases
            .AsNoTracking()
            .AnyAsync(x => x.BuyerId == userId &&
                           x.CourseId == courseId &&
                           x.Status == "Paid", cancellationToken);

        if (ownsCourse)
            return true;

        // Bước 2: chỉ chạy khi chưa mua lẻ. Không Include/ToList nên phù hợp với request xem video.
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return await _context.UserSubscriptions
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId &&
                           x.Status == SubscriptionStatuses.Active &&
                           x.EndDate >= now, cancellationToken);
    }

    public async Task<bool> CanAccessCourseAsync(
        int userId,
        int courseId,
        CancellationToken cancellationToken = default)
    {
        var isOwner = await _context.Courses
            .AsNoTracking()
            .AnyAsync(x => x.CourseId == courseId && x.OwnerId == userId, cancellationToken);

        return isOwner || await CheckCourseAccessAsync(userId, courseId, cancellationToken);
    }
}
