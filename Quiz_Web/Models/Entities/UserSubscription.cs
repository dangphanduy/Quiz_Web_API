namespace Quiz_Web.Models.Entities;

public class UserSubscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }

    /// <summary>Thời điểm thanh toán thành công, luôn lưu theo UTC.</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Thời điểm hết quyền truy cập, luôn lưu theo UTC.</summary>
    public DateTime EndDate { get; set; }

    public string Status { get; set; } = SubscriptionStatuses.Active;
    public virtual User User { get; set; } = null!;
    public virtual SubscriptionPlan Plan { get; set; } = null!;
}

public static class SubscriptionStatuses
{
    public const string Active = "Active";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
}
