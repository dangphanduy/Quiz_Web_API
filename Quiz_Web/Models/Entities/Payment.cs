using System;
using System.Collections.Generic;

namespace Quiz_Web.Models.Entities;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int OrderId { get; set; }

    public string Provider { get; set; } = null!;

    public string? ProviderRef { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public string Status { get; set; } = null!;

    /// <summary>Course hoặc Subscription.</summary>
    public string Purpose { get; set; } = PaymentPurposes.Course;

    public int? SubscriptionPlanId { get; set; }

    /// <summary>Mã giao dịch do cổng thanh toán trả về.</summary>
    public string? TransactionId { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? RawPayload { get; set; }

    public virtual Order Order { get; set; } = null!;

    public virtual SubscriptionPlan? SubscriptionPlan { get; set; }
}

public static class PaymentPurposes
{
    public const string Course = "Course";
    public const string Subscription = "Subscription";
}
