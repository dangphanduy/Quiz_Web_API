using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.PayOSPayment;
using PayOS.Models.Webhooks;
using PayOS.Models.V2.PaymentRequests;
using Quiz_Web.Services.IServices;
using System.Data;
using System.Security.Claims;

namespace Quiz_Web.Controllers;

public class PaymentController : Controller
{
    private readonly IPayOSService _payOSService;
    private readonly ICartService _cartService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICourseAccessService _courseAccessService;
    private readonly LearningPlatformContext _context;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IPayOSService payOSService,
        ICartService cartService,
        ISubscriptionService subscriptionService,
        ICourseAccessService courseAccessService,
        LearningPlatformContext context,
        ILogger<PaymentController> logger)
    {
        _payOSService = payOSService;
        _cartService = cartService;
        _subscriptionService = subscriptionService;
        _courseAccessService = courseAccessService;
        _context = context;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreatePayOSPayment(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var cartItems = await _cartService.GetCartItemsAsync(userId);
        if (!cartItems.Any())
            return Json(new { success = false, message = "Giỏ hàng trống." });

        var total = cartItems.Sum(x => x.Course.Price);
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

            foreach (var item in cartItems)
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

            var payment = new Payment
            {
                OrderId = order.OrderId,
                Provider = "PayOS",
                Amount = total,
                Currency = "VND",
                Status = "Pending",
                Purpose = PaymentPurposes.Course,
                RawPayload = order.OrderId.ToString(),
                ProviderRef = order.OrderId.ToString()
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            var payosResult = await _payOSService.CreatePaymentLinkAsync(
                total,
                "Thanh toan gio hang",
                order.OrderId);

            if (payosResult == null || string.IsNullOrWhiteSpace(payosResult.CheckoutUrl))
            {
                payment.Status = "Failed";
                order.Status = "Failed";
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Json(new { success = false, message = "Không tạo được liên kết thanh toán PayOS." });
            }

            await transaction.CommitAsync(cancellationToken);

            return Json(new { 
                success = true, 
                payUrl = payosResult.CheckoutUrl, 
                orderId = order.OrderId.ToString(),
                qrCode = payosResult.QrCode,
                bin = payosResult.Bin,
                accountNumber = payosResult.AccountNumber,
                accountName = payosResult.AccountName,
                amount = payosResult.Amount,
                description = payosResult.Description
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating course payment for user {UserId}", userId);
            return Json(new { success = false, message = "Không thể khởi tạo thanh toán." });
        }
    }

    /// <summary>
    /// Dùng chung cổng PayOS; Purpose và SubscriptionPlanId giúp callback phân loại giao dịch.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateSubscriptionPayOSPayment(
        [FromBody] SubscriptionPaymentRequest req,
        CancellationToken cancellationToken)
    {
        var planId = req.PlanId;
        var userId = GetCurrentUserId();
        var plan = await _subscriptionService.GetActivePlanAsync(planId, cancellationToken);
        if (plan is null)
        {
            return Json(new { success = false, message = "Gói thuê bao không tồn tại hoặc đã ngừng bán." });
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

            var payment = new Payment
            {
                OrderId = order.OrderId,
                Provider = "PayOS",
                Amount = plan.Price,
                Currency = "VND",
                Status = "Pending",
                Purpose = PaymentPurposes.Subscription,
                SubscriptionPlanId = plan.Id,
                RawPayload = order.OrderId.ToString(),
                ProviderRef = order.OrderId.ToString()
            };
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            var payosResult = await _payOSService.CreatePaymentLinkAsync(
                plan.Price,
                $"Dang ky goi {plan.Name}",
                order.OrderId);

            if (payosResult == null || string.IsNullOrWhiteSpace(payosResult.CheckoutUrl))
            {
                payment.Status = "Failed";
                order.Status = "Failed";
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Json(new { success = false, message = "Không thể tạo giao dịch PayOS." });
            }

            await transaction.CommitAsync(cancellationToken);
            return Json(new { 
                success = true, 
                payUrl = payosResult.CheckoutUrl, 
                orderId = order.OrderId.ToString(),
                qrCode = payosResult.QrCode,
                bin = payosResult.Bin,
                accountNumber = payosResult.AccountNumber,
                accountName = payosResult.AccountName,
                amount = payosResult.Amount,
                description = payosResult.Description
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Error creating subscription payment for plan {PlanId}", planId);
            return Json(new { success = false, message = "Không thể khởi tạo thanh toán gói thuê bao." });
        }
    }

    public class SubscriptionPaymentRequest
    {
        public int PlanId { get; set; }
    }

    [HttpPost("Payment/PayOSCallback")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSCallback(
        [FromBody] Webhook callbackRequest,
        CancellationToken cancellationToken)
    {
        try
        {
            var verifiedData = await _payOSService.VerifyWebhookDataAsync(callbackRequest);
            if (verifiedData == null)
            {
                return BadRequest("Invalid signature");
            }

            var payment = await FindPaymentAsync(verifiedData.OrderCode.ToString(), cancellationToken);
            if (payment is null)
                return NotFound("Payment not found");

            if (decimal.ToInt64(decimal.Round(payment.Amount, 0)) != verifiedData.Amount)
            {
                _logger.LogWarning("Amount mismatch for payment {PaymentId}", payment.PaymentId);
                return BadRequest("Amount mismatch");
            }

            if (payment.Status != "Pending")
                return Ok("Already processed");

            if (callbackRequest.Success)
            {
                await CompletePaymentAsync(payment.PaymentId, verifiedData.Reference, cancellationToken);
            }
            else
            {
                await FailPaymentAsync(payment.PaymentId, cancellationToken);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS callback");
            return StatusCode(500, "Internal error");
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSReturn(
        [FromQuery] int orderCode,
        [FromQuery] string status,
        [FromQuery] string id,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await FindPaymentAsync(orderCode.ToString(), cancellationToken);
            if (payment is null)
                return PaymentResult(false, "Không tìm thấy giao dịch.");

            if (payment.Status == "Paid")
                return PaymentResult(true, "Thanh toán thành công!", payment);
            if (payment.Status == "Failed")
                return PaymentResult(false, "Thanh toán thất bại.");

            if (status == "PAID")
            {
                var info = await _payOSService.GetPaymentLinkInformationAsync(orderCode);
                if (info != null && info.Status == PaymentLinkStatus.Paid)
                {
                    var reference = info.Transactions?.FirstOrDefault()?.Reference ?? id;
                    await CompletePaymentAsync(payment.PaymentId, reference, cancellationToken);
                    return PaymentResult(true, "Thanh toán thành công!", payment);
                }
            }

            return PaymentResult(false, "Thanh toán không thành công hoặc đang chờ xử lý.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS return");
            return PaymentResult(false, "Có lỗi xảy ra khi xử lý thanh toán.");
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSCancel(
        [FromQuery] int orderCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await FindPaymentAsync(orderCode.ToString(), cancellationToken);
            if (payment is null)
                return PaymentResult(false, "Không tìm thấy giao dịch.");

            if (payment.Status == "Pending")
            {
                await FailPaymentAsync(payment.PaymentId, cancellationToken);
            }

            return PaymentResult(false, "Bạn đã hủy giao dịch thanh toán.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS cancel");
            return PaymentResult(false, "Có lỗi xảy ra khi hủy thanh toán.");
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

                await _cartService.ClearCartAsync(payment.Order.BuyerId);
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

    [HttpPost("Payment/SimulateSuccess")]
    public async Task<IActionResult> SimulateSuccess([FromBody] SimulateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var payment = await _context.Payments
                .Include(x => x.Order)
                .FirstOrDefaultAsync(x => x.OrderId == request.OrderId, cancellationToken);

            if (payment is null)
                return NotFound(new { success = false, message = "Không tìm thấy giao dịch." });

            if (payment.Status == "Paid")
                return Ok(new { success = true, message = "Giao dịch đã được thanh toán trước đó." });

            // Giả lập hoàn thành thanh toán
            var reference = "SIMULATED_" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            await CompletePaymentAsync(payment.PaymentId, reference, cancellationToken);

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error simulating payment success");
            return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi giả lập thanh toán." });
        }
    }

    public class SimulateRequest
    {
        public int OrderId { get; set; }
    }
}
