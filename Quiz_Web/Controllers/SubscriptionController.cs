using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;
using System.Security.Claims;

namespace Quiz_Web.Controllers;

public class SubscriptionController : Controller
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpGet("/pricing")]
    public async Task<IActionResult> Pricing(CancellationToken cancellationToken)
    {
        var plans = await _subscriptionService.GetActivePlansAsync(cancellationToken);
        var current = User.Identity?.IsAuthenticated == true
            ? await _subscriptionService.GetCurrentAsync(GetCurrentUserId(), cancellationToken)
            : null;

        if (current is not null)
        {
            await _subscriptionService.EnsureExpiryNotificationAsync(
                current.UserId,
                warningDays: 3,
                cancellationToken);
        }

        var model = new SubscriptionPricingViewModel
        {
            Plans = plans,
            CurrentSubscription = current,
            DaysRemaining = current is null
                ? null
                : Math.Max(0, (int)Math.Ceiling((current.EndDate - DateTimeHelper.Now).TotalDays))
        };

        return View(model);
    }

    [Authorize]
    [HttpGet("/api/subscription/status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var subscription = await _subscriptionService.GetCurrentAsync(
            GetCurrentUserId(),
            cancellationToken);

        return Json(new
        {
            isActive = subscription is not null,
            planName = subscription?.Plan.Name,
            endDate = subscription?.EndDate,
            daysRemaining = subscription is null
                ? 0
                : Math.Max(0, (int)Math.Ceiling((subscription.EndDate - DateTimeHelper.Now).TotalDays)),
            shouldWarn = subscription is not null &&
                         subscription.EndDate <= DateTimeHelper.Now.AddDays(3)
        });
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User not authenticated.");
    }
}
