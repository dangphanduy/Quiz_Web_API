using Quiz_Web.Models.Entities;

namespace Quiz_Web.Models.ViewModels;

public class SubscriptionPricingViewModel
{
    public IReadOnlyList<SubscriptionPlan> Plans { get; init; } = Array.Empty<SubscriptionPlan>();
    public UserSubscription? CurrentSubscription { get; init; }
    public int? DaysRemaining { get; init; }
}
