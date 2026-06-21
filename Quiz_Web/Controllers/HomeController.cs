using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services;
using Quiz_Web.Services.IServices;
using System.Security.Claims;

namespace Quiz_Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ICourseService _courseService;
        private readonly ICartService _cartService;
        private readonly IFlashcardService _flashcardService;
        private readonly LearningPlatformContext _context;

        public HomeController(
            ILogger<HomeController> logger, 
            ICourseService courseService,
            ICartService cartService,
            IFlashcardService flashcardService,
            LearningPlatformContext context)
        {
            _logger = logger;
            _courseService = courseService;
            _cartService = cartService;
            _flashcardService = flashcardService;
            _context = context;
        }

        // Action mặc định để xử lý routing dựa trên authentication
        public IActionResult Welcome()
        {
            // Kiểm tra nếu user đã đăng nhập (có cookie authentication)
            if (User.Identity?.IsAuthenticated == true)
            {
                // Redirect đến trang Home/Index
                return RedirectToAction("Index");
            }
            else
            {
                // Redirect đến trang Introduce cho người dùng mới
                return RedirectToAction("Index", "Introduce");
            }
        }

        public async Task<IActionResult> Index()
        {
            // Nếu người dùng chưa đăng nhập, redirect về Introduce
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToAction("Index", "Introduce");
            }
            
            // Lấy danh sách categories cho navigation
            var categories = await _context.CourseCategories
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(); 
            
            // Lấy recommended courses dựa trên user interests
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var recommendedCourses = new List<Quiz_Web.Models.Entities.Course>();
            
            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out int userIdInt))
            {
                // Lấy user interests
                var userInterests = await _context.UserInterests
                    .Where(ui => ui.UserId == userIdInt)
                    .Select(ui => ui.CategoryId)
                    .ToListAsync();
                
                if (userInterests.Any())
                {
                    // Lấy courses từ các categories user quan tâm
                    recommendedCourses = await _context.Courses
                        .Include(c => c.Owner)
                        .Include(c => c.Category)
                        .Where(c => c.IsPublished 
                                 && c.CategoryId.HasValue 
                                 && userInterests.Contains(c.CategoryId.Value))
                        .OrderByDescending(c => c.AverageRating)
                        .ThenByDescending(c => c.TotalReviews)
                        .Take(10)
                        .ToListAsync();
                }
            }
            
            // Nếu không có recommended courses, lấy random courses
            if (!recommendedCourses.Any())
            {
                recommendedCourses = await _context.Courses
                    .Include(c => c.Owner)
                    .Include(c => c.Category)
                    .Where(c => c.IsPublished)
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(10)
                    .ToListAsync();
            }
            
            // Lấy top rated courses
            var topRatedCourses = await _context.Courses
                .Include(c => c.Owner)
                .Include(c => c.Category)
                .Where(c => c.IsPublished && c.AverageRating > 0)
                .OrderByDescending(c => c.AverageRating)
                .ThenByDescending(c => c.TotalReviews)
                .Take(5)
                .ToListAsync();
            
            // Lấy public flashcard sets
            var publicFlashcardSets = await _flashcardService.GetPublicFlashcardSetsAsync();
            
            ViewBag.RecommendedCourses = recommendedCourses;
            ViewBag.TopRatedCourses = topRatedCourses;
            ViewBag.PublicFlashcardSets = publicFlashcardSets.Take(10).ToList();
            
            return View(categories);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [Authorize]
        [Route("/home-checkout")]
        public async Task<IActionResult> Checkout()
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
                return RedirectToAction("Index");
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

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // Debug action to test Onboarding view
        [Route("/debug-onboarding")]
        public async Task<IActionResult> DebugOnboarding()
        {
            try
            {
                var categories = await _context.CourseCategories
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                var viewModel = new Quiz_Web.Models.ViewModels.OnboardingViewModel
                {
                    Categories = categories
                };

                ViewBag.Debug = $"Found {categories.Count} categories";
                
                return View("~/Views/Onboarding/Index.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                return Content($"Error: {ex.Message}\n\nStack: {ex.StackTrace}");
            }
        }
  //      [Route("/course")]        
  //      public IActionResult Course()
  //      {
  //          var courses = _courseService.GetAllPublishedCourses();

		//	return View(courses);
  //      }

		//[Route("/course/{slug}")]
		//public IActionResult CourseDetail(string slug)
		//{
		//	if (string.IsNullOrEmpty(slug))
		//	{
		//		return RedirectToAction("Course");
		//	}

		//	var course = _courseService.GetCourseBySlug(slug);

		//	if (course == null)
		//	{
		//		_logger.LogWarning($"Course not found with slug: {slug}");
		//		return NotFound();
		//	}

		//	return View(course);
		//}
	}
}
