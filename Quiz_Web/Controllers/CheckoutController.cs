using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;
using System.Security.Claims;

namespace Quiz_Web.Controllers
{
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(ICartService cartService, ILogger<CheckoutController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = await _cartService.GetCartItemsAsync(userId);
                
                var viewModel = new CheckoutViewModel
                {
                    CartItems = cartItems.Select(ci => new CartItemViewModel
                    {
                        CourseId = ci.CourseId,
                        Title = ci.Course.Title,
                        CoverUrl = ci.Course.CoverUrl,
                        Price = ci.Course.Price,
                        InstructorName = ci.Course.Owner.FullName,
                        AddedAt = ci.AddedAt
                    }).ToList(),
                    Total = cartItems.Sum(ci => ci.Course.Price)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading checkout page");
                TempData["Error"] = "Có lỗi xảy ra khi tải trang thanh toán";
                return RedirectToAction("Index", "Home");
            }
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                throw new UnauthorizedAccessException("User not authenticated");
            }
            return userId;
        }
    }
}
