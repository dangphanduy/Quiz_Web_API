using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;

namespace Quiz_Web.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly LearningPlatformContext _context;
    private readonly TimeProvider _timeProvider;

    public SubscriptionService(LearningPlatformContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetActivePlansAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.DurationInMonths)
            .ToListAsync(cancellationToken);
    }

    public Task<SubscriptionPlan?> GetActivePlanAsync(
        int planId,
        CancellationToken cancellationToken = default)
    {
        return _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(plan => plan.Id == planId && plan.IsActive, cancellationToken);
    }

    public DateTime CalculateEndDate(DateTime paidAtUtc, int durationInMonths)
    {
        if (durationInMonths <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationInMonths));

        return DateTime.SpecifyKind(paidAtUtc, DateTimeKind.Utc).AddMonths(durationInMonths);
    }

    public async Task<UserSubscription> ActivateOrRenewAsync(
        int userId,
        int planId,
        DateTime paidAtUtc,
        CancellationToken cancellationToken = default)
    {
        var plan = await _context.SubscriptionPlans
            .FirstOrDefaultAsync(x => x.Id == planId && x.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Gói thuê bao không tồn tại hoặc đã ngừng bán.");

        var paidAt = DateTime.SpecifyKind(paidAtUtc, DateTimeKind.Utc);
        var current = await _context.UserSubscriptions
            .Where(x => x.UserId == userId &&
                        x.Status == SubscriptionStatuses.Active &&
                        x.EndDate >= paidAt)
            .OrderByDescending(x => x.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        // Còn hạn: cộng tiếp thời lượng vào EndDate hiện tại để user không mất ngày đã trả tiền.
        if (current is not null)
        {
            current.PlanId = planId;
            current.EndDate = CalculateEndDate(current.EndDate, plan.DurationInMonths);
            return current;
        }

        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanId = planId,
            StartDate = paidAt,
            EndDate = CalculateEndDate(paidAt, plan.DurationInMonths),
            Status = SubscriptionStatuses.Active,
        };

        _context.UserSubscriptions.Add(subscription);
        return subscription;
    }

    public Task<UserSubscription?> GetCurrentAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeHelper.Now;

        return _context.UserSubscriptions
            .AsNoTracking()
            .Include(x => x.Plan)
            .Where(x => x.UserId == userId &&
                        x.Status == SubscriptionStatuses.Active &&
                        x.EndDate >= now)
            .OrderByDescending(x => x.EndDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task EnsureExpiryNotificationAsync(
        int userId,
        int warningDays = 3,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeHelper.Now;
        var warningLimit = now.AddDays(warningDays);

        var subscription = await _context.UserSubscriptions
            .AsNoTracking()
            .Where(x => x.UserId == userId &&
                        x.Status == SubscriptionStatuses.Active &&
                        x.EndDate >= now &&
                        x.EndDate <= warningLimit)
            .OrderBy(x => x.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return;

        var notificationKey = $"subscription-expiring:{subscription.Id}:{subscription.EndDate:yyyyMMdd}";
        var alreadyCreated = await _context.Notifications
            .AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.Data == notificationKey, cancellationToken);

        if (alreadyCreated)
            return;

        var daysRemaining = Math.Max(0, (int)Math.Ceiling((subscription.EndDate - now).TotalDays));
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = "SubscriptionExpiring",
            Title = "Gói thuê bao sắp hết hạn",
            Body = $"Gói của bạn còn {daysRemaining} ngày. Gia hạn để tiếp tục xem toàn bộ khóa học.",
            Data = notificationKey,
            IsRead = false,
            CreatedAt = now
        });
        await _context.SaveChangesAsync(cancellationToken);
    }
}
