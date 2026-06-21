using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.MoMoPayment;
using Quiz_Web.Services.IServices;
using System.Data;
using System.Security.Claims;

namespace Quiz_Web.Controllers;

public class PaymentController : Controller
{
    private readonly IMoMoPaymentService _momoService;
    private readonly ICartService _cartService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICourseAccessService _courseAccessService;
    private readonly LearningPlatformContext _context;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IMoMoPaymentService momoService,
        ICartService cartService,
        ISubscriptionService subscriptionService,
        ICourseAccessService courseAccessService,
        LearningPlatformContext context,
        ILogger<PaymentController> logger)
    {
        _momoService = momoService;
        _cartService = cartService;
        _subscriptionService = subscriptionService;
        _courseAccessService = courseAccessService;
        _context = context;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateMoMoPayment(
        [FromBody] CoursePaymentRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var cartItems = await _cartService.GetCartItemsAsync(userId);
        if (!cartItems.Any())
            return Json(new { success = false, message = "Giỏ hàng trống." });

        var selectedCourseIds = request?.CourseIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList() ?? new List<int>();

        if (!selectedCourseIds.Any())
            return Json(new { success = false, message = "Vui lòng chọn ít nhất một khóa học để thanh toán." });

        var selectedItems = cartItems
            .Where(x => selectedCourseIds.Contains(x.CourseId))
            .ToList();

        if (selectedItems.Count != selectedCourseIds.Count)
            return Json(new { success = false, message = "Một số khóa học đã chọn không còn trong giỏ hàng." });

        var total = selectedItems.Sum(x => x.Course.Price);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var order = new Order
            {
                BuyerId = userId,
                TotalAmount = total,
                Currency = "VND",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var item in selectedItems)
            {
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    CourseId = item.CourseId,
                    Price = item.Course.Price
                });
                _context.CoursePurchases.Add(new CoursePurchase
                {
                    BuyerId = userId,
                    CourseId = item.CourseId,
                    PricePaid = item.Course.Price,
                    Currency = "VND",
                    Status = "Pending",
                    PurchasedAt = DateTime.UtcNow
                });
            }

            var merchantOrderId = $"ORDER_{order.OrderId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var payment = new Payment
            {
                OrderId = order.OrderId,
                Provider = "MoMo",
                Amount = total,
                Currency = "VND",
                Status = "Pending",
                Purpose = PaymentPurposes.Course,
                RawPayload = merchantOrderId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            var momo = await _momoService.CreatePaymentAsync(
                total,
                "Thanh toán giỏ hàng",
                merchantOrderId);

            if (momo.resultCode != 0 || string.IsNullOrWhiteSpace(momo.payUrl))
            {
                payment.Status = "Failed";
                order.Status = "Failed";
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Json(new { success = false, message = momo.message });
            }

            // ProviderRef giữ merchant order id. TransactionId được lưu ở cột riêng.
            payment.ProviderRef = momo.orderId;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Json(new { success = true, payUrl = momo.payUrl, orderId = merchantOrderId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating course payment for user {UserId}", userId);
            return Json(new { success = false, message = "Không thể khởi tạo thanh toán." });
        }
    }

    /// <summary>
    /// Dùng chung cổng MoMo; Purpose và SubscriptionPlanId giúp callback phân loại giao dịch.
    /// </summary>
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSubscriptionMoMoPayment(
        int planId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var plan = await _subscriptionService.GetActivePlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            TempData["Error"] = "Gói thuê bao không tồn tại hoặc đã ngừng bán.";
            return RedirectToAction("Pricing", "Subscription");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var order = new Order
            {
                BuyerId = userId,
                TotalAmount = plan.Price,
                Currency = "VND",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(cancellationToken);

            // Không tạo OrderItem giả vì bảng hiện tại bắt buộc CourseId.
            var merchantOrderId = $"SUB_{order.OrderId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
            var payment = new Payment
            {
                OrderId = order.OrderId,
                Provider = "MoMo",
                Amount = plan.Price,
                Currency = "VND",
                Status = "Pending",
                Purpose = PaymentPurposes.Subscription,
                SubscriptionPlanId = plan.Id,
                RawPayload = merchantOrderId
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            var momo = await _momoService.CreatePaymentAsync(
                plan.Price,
                $"Đăng ký gói {plan.Name}",
                merchantOrderId);

            if (momo.resultCode != 0 || string.IsNullOrWhiteSpace(momo.payUrl))
            {
                payment.Status = "Failed";
                order.Status = "Failed";
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                TempData["Error"] = momo.message ?? "Không thể tạo giao dịch MoMo.";
                return RedirectToAction("Pricing", "Subscription");
            }

            payment.ProviderRef = momo.orderId;
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Redirect(momo.payUrl);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating subscription payment for plan {PlanId}", planId);
            TempData["Error"] = "Không thể khởi tạo thanh toán gói thuê bao.";
            return RedirectToAction("Pricing", "Subscription");
        }
    }

    [HttpPost("Payment/MoMoCallback")]
    [AllowAnonymous]
    public async Task<IActionResult> MoMoCallback(
        [FromBody] MoMoIpnRequest ipn,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_momoService.ValidateSignature(ipn))
                return BadRequest("Invalid signature");

            var payment = await FindPaymentAsync(ipn.orderId, cancellationToken);
            if (payment is null)
                return NotFound("Payment not found");

            // Không tin amount từ client/callback nếu lệch với giao dịch đã tạo.
            if (decimal.ToInt64(decimal.Round(payment.Amount, 0)) != ipn.amount)
            {
                _logger.LogWarning("Amount mismatch for payment {PaymentId}", payment.PaymentId);
                return BadRequest("Amount mismatch");
            }

            if (payment.Status != "Pending")
                return Ok("Already processed");

            if (ipn.resultCode == 0)
                await CompletePaymentAsync(payment.PaymentId, ipn.transId.ToString(), cancellationToken);
            else
                await FailPaymentAsync(payment.PaymentId, cancellationToken);

            return Ok("IPN Processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo IPN");
            return StatusCode(500, "Internal error");
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> MoMoReturn(
        string orderId,
        int resultCode,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await FindPaymentAsync(orderId, cancellationToken);
            if (payment is null)
                return PaymentResult(false, "Không tìm thấy giao dịch.");

            if (payment.Status == "Paid")
                return PaymentResult(true, "Thanh toán thành công!", payment);
            if (payment.Status == "Failed")
                return PaymentResult(false, $"Thanh toán thất bại: {message}");

            // Return URL chỉ hoàn tất sau khi query server-to-server với MoMo.
            if (resultCode == 0)
            {
                var queryResult = await _momoService.QueryTransactionAsync(orderId);
                if (queryResult is not null &&
                    queryResult.resultCode == 0 &&
                    queryResult.amount == decimal.ToInt64(decimal.Round(payment.Amount, 0)))
                {
                    await CompletePaymentAsync(
                        payment.PaymentId,
                        queryResult.transId.ToString(),
                        cancellationToken);
                    return PaymentResult(true, "Thanh toán thành công!", payment);
                }
            }

            return PaymentResult(
                resultCode == 0,
                resultCode == 0
                    ? "Thanh toán đang được xử lý. Vui lòng đợi."
                    : $"Thanh toán thất bại: {message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MoMo return");
            return PaymentResult(false, "Có lỗi xảy ra khi xử lý thanh toán.");
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> CheckPaymentStatus(
        string orderId,
        CancellationToken cancellationToken)
    {
        var payment = await FindPaymentAsync(orderId, cancellationToken);
        if (payment is null)
            return Json(new { status = "NOT_FOUND" });

        return Json(new
        {
            status = payment.Status.ToUpperInvariant(),
            orderStatus = payment.Order.Status,
            purpose = payment.Purpose,
            message = payment.Status == "Paid" ? "Thanh toán thành công" : string.Empty
        });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> CheckCourseAccess(
        int courseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var hasAccess = await _courseAccessService.CheckCourseAccessAsync(
                GetCurrentUserId(),
                courseId,
                cancellationToken);
            return Json(new { hasAccess });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking course access");
            return Json(new { hasAccess = false });
        }
    }

    private async Task<Payment?> FindPaymentAsync(
        string merchantOrderId,
        CancellationToken cancellationToken)
    {
        return await _context.Payments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(
                x => x.ProviderRef == merchantOrderId || x.RawPayload == merchantOrderId,
                cancellationToken);
    }

    private async Task CompletePaymentAsync(
        int paymentId,
        string transactionId,
        CancellationToken cancellationToken)
    {
        // IPN và Return URL có thể chạy đồng thời. Serializable + đọc lại bản ghi
        // bảo đảm một payment chỉ kích hoạt hoặc gia hạn đúng một lần.
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken)
                ?? throw new InvalidOperationException("Payment not found.");

            if (payment.Status != "Pending")
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var paidAt = DateTime.UtcNow;
            payment.Status = "Paid";
            payment.PaidAt = paidAt;
            payment.TransactionId = transactionId;
            payment.Order.Status = "Paid";
            payment.Order.PaidAt = paidAt;

            if (payment.Purpose == PaymentPurposes.Subscription)
            {
                if (!payment.SubscriptionPlanId.HasValue)
                    throw new InvalidOperationException("Subscription payment is missing PlanId.");

                await _subscriptionService.ActivateOrRenewAsync(
                    payment.Order.BuyerId,
                    payment.SubscriptionPlanId.Value,
                    paidAt,
                    cancellationToken);
            }
            else
            {
                var courseIds = await _context.OrderItems
                    .Where(x => x.OrderId == payment.OrderId)
                    .Select(x => x.CourseId)
                    .ToListAsync(cancellationToken);

                var purchases = await _context.CoursePurchases
                    .Where(x => x.BuyerId == payment.Order.BuyerId &&
                                courseIds.Contains(x.CourseId) &&
                                x.Status == "Pending")
                    .ToListAsync(cancellationToken);

                foreach (var purchase in purchases)
                {
                    purchase.Status = "Paid";
                    purchase.PurchasedAt = paidAt;
                }

                await RemovePurchasedCartItemsAsync(
                    payment.Order.BuyerId,
                    courseIds,
                    cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task FailPaymentAsync(int paymentId, CancellationToken cancellationToken)
    {
        var payment = await _context.Payments
            .Include(x => x.Order)
            .FirstOrDefaultAsync(x => x.PaymentId == paymentId, cancellationToken)
            ?? throw new InvalidOperationException("Payment not found.");

        if (payment.Status != "Pending")
            return;

        payment.Status = "Failed";
        payment.Order.Status = "Failed";

        if (payment.Purpose == PaymentPurposes.Course)
        {
            var courseIds = await _context.OrderItems
                .Where(x => x.OrderId == payment.OrderId)
                .Select(x => x.CourseId)
                .ToListAsync(cancellationToken);
            var purchases = await _context.CoursePurchases
                .Where(x => x.BuyerId == payment.Order.BuyerId &&
                            courseIds.Contains(x.CourseId) &&
                            x.Status == "Pending")
                .ToListAsync(cancellationToken);

            foreach (var purchase in purchases)
                purchase.Status = "Failed";
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private IActionResult PaymentResult(bool success, string message, Payment? payment = null)
    {
        ViewBag.Success = success;
        ViewBag.Message = message;
        ViewBag.OrderId = payment?.OrderId;
        ViewBag.PaymentPurpose = payment?.Purpose;
        return View("PaymentResult");
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("User not authenticated.");
    }

    private async Task RemovePurchasedCartItemsAsync(
        int userId,
        List<int> courseIds,
        CancellationToken cancellationToken)
    {
        if (!courseIds.Any())
            return;

        var cart = await _context.ShoppingCarts
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (cart is null)
            return;

        var cartItems = await _context.CartItems
            .Where(x => x.CartId == cart.CartId && courseIds.Contains(x.CourseId))
            .ToListAsync(cancellationToken);

        if (!cartItems.Any())
            return;

        _context.CartItems.RemoveRange(cartItems);
        cart.UpdatedAt = DateTime.UtcNow;
    }

    public sealed class CoursePaymentRequest
    {
        public List<int> CourseIds { get; set; } = new();
    }
}
