using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;
using System.Security.Claims;

namespace Quiz_Web.Controllers
{
    [Authorize]
    public class OnboardingController : Controller
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<OnboardingController> _logger;

        public OnboardingController(LearningPlatformContext context, ILogger<OnboardingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Test action to verify routing works
        [AllowAnonymous]
        [Route("/test-onboarding-route")]
        public IActionResult TestRoute()
        {
            return Content("Onboarding Controller is accessible! ?");
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return RedirectToAction("Login", "Account");
                }

                // Check if user has already completed onboarding
                // We check both UserProfile and UserInterests
                var hasProfile = await _context.UserProfiles.AnyAsync(up => up.UserId == userId);
                var hasInterests = await _context.UserInterests.AnyAsync(ui => ui.UserId == userId);

                if (hasProfile && hasInterests)
                {
                    // User has already completed onboarding, redirect to home
                    return RedirectToAction("Index", "Home");
                }

                // Load all categories for display
                var categories = await _context.CourseCategories
                    .OrderBy(c => c.DisplayOrder)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                var viewModel = new OnboardingViewModel
                {
                    Categories = categories
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading onboarding page");
                TempData["Error"] = "?� x?y ra l?i khi t?i trang. Vui l�ng th? l?i.";
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(OnboardingViewModel model)
        {
            try
            {
                _logger.LogInformation("=== Starting Onboarding Submission ===");
                
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    _logger.LogWarning("User ID not found in claims");
                    return Json(new { success = false, message = "Kh�ng t�m th?y th�ng tin ng??i d�ng" });
                }

                _logger.LogInformation("UserId: {UserId}", userId);
                _logger.LogInformation("Selected Categories Count: {Count}", model.SelectedCategoryIds?.Count ?? 0);

                // Validate that at least one category is selected
                if (model.SelectedCategoryIds == null || !model.SelectedCategoryIds.Any())
                {
                    _logger.LogWarning("No categories selected");
                    return Json(new { success = false, message = "Vui l�ng ch?n �t nh?t m?t ch? ?? quan t�m" });
                }

                // Check if user already has profile
                var existingProfile = await _context.UserProfiles
                    .FirstOrDefaultAsync(up => up.UserId == userId);

                // Check existing interests
                var existingInterests = await _context.UserInterests
                    .Where(ui => ui.UserId == userId)
                    .ToListAsync();

                if (existingProfile != null && existingInterests.Any())
                {
                    _logger.LogWarning("User {UserId} already completed onboarding", userId);
                    return Json(new { 
                        success = false, 
                        message = "B?n ?� ho�n th�nh vi?c thi?t l?p h? s?. N?u mu?n c?p nh?t, vui l�ng v�o trang c�i ??t." 
                    });
                }

                _logger.LogInformation("Creating/Updating UserProfile for user {UserId}", userId);

                // Step A: Save or Update User Profile
                if (existingProfile == null)
                {
                    var userProfile = new UserProfile
                    {
                        UserId = userId,
                        DoB = model.DoB,
                        Gender = model.Gender,
                        Bio = model.Bio,
                        SchoolName = model.SchoolName,
                        GradeLevel = model.GradeLevel,
                        Locale = "vi-VN",
                        TimeZone = "SE Asia Standard Time"
                    };
                    _context.UserProfiles.Add(userProfile);
                    _logger.LogInformation("UserProfile added to context");
                }
                else
                {
                    // Update existing profile
                    existingProfile.DoB = model.DoB;
                    existingProfile.Gender = model.Gender;
                    existingProfile.Bio = model.Bio;
                    existingProfile.SchoolName = model.SchoolName;
                    existingProfile.GradeLevel = model.GradeLevel;
                    _logger.LogInformation("UserProfile updated in context");
                }

                // Step B: Clear old interests and add new ones
                if (existingInterests.Any())
                {
                    _context.UserInterests.RemoveRange(existingInterests);
                    _logger.LogInformation("Removed {Count} existing interests", existingInterests.Count);
                }

                var userInterests = model.SelectedCategoryIds.Select(categoryId => new UserInterest
                {
                    UserId = userId,
                    CategoryId = categoryId,
                    CreatedAt = DateTimeHelper.Now
                }).ToList();

                await _context.UserInterests.AddRangeAsync(userInterests);
                _logger.LogInformation("Added {Count} user interests to context", userInterests.Count);

                // Step C: Save all changes
                await _context.SaveChangesAsync();
                _logger.LogInformation("Successfully saved changes to database");

                _logger.LogInformation("User {UserId} completed onboarding with profile and {Count} interests",
                    userId, model.SelectedCategoryIds.Count);

                return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database update error during onboarding: {Message}", dbEx.InnerException?.Message ?? dbEx.Message);
                return Json(new { 
                    success = false, 
                    message = "L?i khi l?u v�o database. Vui l�ng ki?m tra l?i th�ng tin." 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving onboarding data: {Message}", ex.Message);
                return Json(new { 
                    success = false, 
                    message = "?� x?y ra l?i khi l?u d? li?u. Vui l�ng th? l?i." 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Skip()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Json(new { success = false });
                }

                // Create minimal profile to mark onboarding as complete
                var existingProfile = await _context.UserProfiles
                    .FirstOrDefaultAsync(up => up.UserId == userId);

                if (existingProfile == null)
                {
                    var minimalProfile = new UserProfile
                    {
                        UserId = userId,
                        Locale = "vi-VN",
                        TimeZone = "SE Asia Standard Time"
                    };
                    _context.UserProfiles.Add(minimalProfile);
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("User {UserId} skipped onboarding", userId);

                return Json(new { success = true, redirectUrl = Url.Action("Index", "Home") });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error skipping onboarding");
                return Json(new { success = false });
            }
        }
    }
}
