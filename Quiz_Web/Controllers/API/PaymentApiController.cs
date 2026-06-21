using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Services.IServices;
using System.Security.Claims;
using PayOS.Models.V2.PaymentRequests;

namespace Quiz_Web.Controllers.API
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentApiController : ControllerBase
    {
        private readonly IPayOSService _payOSService;
        private readonly ICartService _cartService;
        private readonly IPurchaseService _purchaseService;
        private readonly LearningPlatformContext _context;
        private readonly ILogger<PaymentApiController> _logger;

        public PaymentApiController(
            IPayOSService payOSService,
            ICartService cartService,
            IPurchaseService purchaseService,
            LearningPlatformContext context,
            ILogger<PaymentApiController> logger)
        {
            _payOSService = payOSService;
            _cartService = cartService;
            _purchaseService = purchaseService;
            _context = context;
            _logger = logger;
        }

        // POST: api/PaymentApi/create
        [HttpPost("create")]
        public async Task<IActionResult> CreatePayment()
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var cartItems = await _cartService.GetCartItemsAsync(userId);

                if (!cartItems.Any())
                    return BadRequest(new { success = false, message = "Giỏ hàng trống" });

                var total = cartItems.Sum(x => x.Course.Price);

                // 1) Tạo Order
                var order = new Order
                {
                    BuyerId = userId,
                    TotalAmount = total,
                    Status = "Pending"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // 2) Tạo OrderItems
                foreach (var item in cartItems)
                {
                    _context.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.OrderId,
                        CourseId = item.CourseId,
                        Price = item.Course.Price
                    });
                }
                await _context.SaveChangesAsync();

                // 3) Tạo Purchase (Pending)
                foreach (var item in cartItems)
                {
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
                await _context.SaveChangesAsync();

                // 4) Tạo Payment
                var orderIdStr = order.OrderId.ToString();
                var payment = new Payment
                {
                    OrderId = order.OrderId,
                    Provider = "PayOS",
                    Amount = total,
                    Currency = "VND",
                    Status = "Pending",
                    RawPayload = orderIdStr
                };

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // 5) Gọi PayOS
                var payosResult = await _payOSService.CreatePaymentLinkAsync(total, "Thanh toan gio hang", order.OrderId);

                if (payosResult != null && !string.IsNullOrWhiteSpace(payosResult.CheckoutUrl))
                {
                    payment.ProviderRef = orderIdStr;
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        payUrl = payosResult.CheckoutUrl,
                        orderId = orderIdStr
                    });
                }

                return BadRequest(new { success = false, message = "Không tạo được liên kết thanh toán PayOS" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error creating payment");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tạo giao dịch thanh toán" });
            }
        }

        // GET: api/PaymentApi/status/{orderId}
        [HttpGet("status/{orderId}")]
        public async Task<IActionResult> CheckStatus(string orderId)
        {
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Order)
                    .FirstOrDefaultAsync(p => p.RawPayload == orderId);

                if (payment == null)
                    return NotFound(new { success = false, message = "Không tìm thấy giao dịch" });

                return Ok(new
                {
                    success = true,
                    status = payment.Status.ToUpper(),
                    orderStatus = payment.Order?.Status,
                    message = payment.Status == "Paid" ? "Thanh toán thành công" : "Đang chờ thanh toán"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error checking payment status");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi kiểm tra trạng thái thanh toán" });
            }
        }

        // GET: api/PaymentApi/access/{courseId}
        [HttpGet("access/{courseId:int}")]
        public async Task<IActionResult> CheckAccess(int courseId)
        {
            try
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");
                var hasAccess = await _purchaseService.HasUserPurchasedCourseAsync(userId, courseId);
                return Ok(new { success = true, hasAccess });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error checking course access");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi kiểm tra quyền truy cập khóa học" });
            }
        }
    }
}
