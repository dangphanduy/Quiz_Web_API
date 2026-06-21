namespace Quiz_Web.Models.Entities;

/// <summary>Ánh xạ bảng SubscriptionPlans đã có sẵn trong database.</summary>
public class SubscriptionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int DurationInMonths { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }

    public virtual ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
