using Quiz_Web.Models.Entities;

namespace Quiz_Web.Services.IServices;

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionPlan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
    Task<SubscriptionPlan?> GetActivePlanAsync(int planId, CancellationToken cancellationToken = default);
    DateTime CalculateEndDate(DateTime paidAtUtc, int durationInMonths);
    Task<UserSubscription> ActivateOrRenewAsync(
        int userId,
        int planId,
        DateTime paidAtUtc,
        CancellationToken cancellationToken = default);
    Task<UserSubscription?> GetCurrentAsync(int userId, CancellationToken cancellationToken = default);
    Task EnsureExpiryNotificationAsync(
        int userId,
        int warningDays = 3,
        CancellationToken cancellationToken = default);
}
