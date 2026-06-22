using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Helper;
using Quiz_Web.Utils;
using Quiz_Web.Services.IServices;
using Quiz_Web.Models.ViewModels;
using System.Text.Json;

namespace Quiz_Web.Controllers
{
	[Authorize(Roles = "Admin,Teacher")]
	public class AdminController : Controller
	{
		private readonly LearningPlatformContext _context;
		private readonly IDashboardService _dashboardService;
		private readonly IFlashcardService _flashcardService;

		public AdminController(
			LearningPlatformContext context, 
			IDashboardService dashboardService,
			IFlashcardService flashcardService)
		{
			_context = context;
			_dashboardService = dashboardService;
			_flashcardService = flashcardService;
		}

		[Route("/admin")]
		public IActionResult Index()
		{
			var data = _dashboardService.GetOverviewData();
			return View(data);
		}

		// DASHBOARD ACTIONS
		public IActionResult UserAnalytics()
		{
			var data = _dashboardService.GetUserAnalytics();
			return View(data);
		}

		public IActionResult LearningActivities()
		{
			var data = _dashboardService.GetLearningActivities();
			return View(data);
		}

		public IActionResult RevenuePayments()
		{
			var data = _dashboardService.GetRevenuePayments();
			return View(data);
		}

		public IActionResult LearningResults()
		{
			var data = _dashboardService.GetLearningResults();
			return View(data);
		}

		// DASHBOARD API ENDPOINTS
		[HttpGet]
		public JsonResult GetOverviewData()
		{
			var data = _dashboardService.GetOverviewData();
			return Json(data);
		}

		[HttpGet]
		public JsonResult GetUserAnalyticsData()
		{
			var data = _dashboardService.GetUserAnalytics();
			return Json(data);
		}

		[HttpGet]
		public JsonResult GetLearningActivitiesData()
		{
			var data = _dashboardService.GetLearningActivities();
			return Json(data);
		}

		[HttpGet]
		public JsonResult GetRevenuePaymentsData()
		{
			var data = _dashboardService.GetRevenuePayments();
			return Json(data);
		}

		[HttpGet]
		public JsonResult GetLearningResultsData()
		{
			var data = _dashboardService.GetLearningResults();
			return Json(data);
		}

		// USER MANAGEMENT
		public async Task<IActionResult> Users()
		{
			var users = await _context.Users.Include(u => u.Role).ToListAsync();
			ViewBag.Roles = await _context.Roles.ToListAsync();
			return View(users);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateUser(User user, string password)
		{
			// Custom validation
			if (!Validation.IsValidUsername(user.Username))
			{
				TempData["Error"] = "Username must be between 3 and 100 characters";
				return RedirectToAction("Users");
			}

			if (!Validation.IsValidFullName(user.FullName))
			{
				TempData["Error"] = "Full name is required and cannot exceed 200 characters";
				return RedirectToAction("Users");
			}

			if (!Validation.IsValidEmail(user.Email))
			{
				TempData["Error"] = "Invalid email format";
				return RedirectToAction("Users");
			}

			if (!Validation.IsValidPassword(password))
			{
				TempData["Error"] = "Password must be at least 8 characters with uppercase, lowercase, number and special character";
				return RedirectToAction("Users");
			}

			if (!Validation.IsValidPhone(user.Phone))
			{
				TempData["Error"] = "Invalid phone number format";
				return RedirectToAction("Users");
			}

			if (await _context.Users.AnyAsync(u => u.Username == user.Username))
			{
				TempData["Error"] = "Username already exists";
				return RedirectToAction("Users");
			}

			if (await _context.Users.AnyAsync(u => u.Email == user.Email))
			{
				TempData["Error"] = "Email already exists";
				return RedirectToAction("Users");
			}

			user.Username = user.Username.ToLower().Trim();
			user.FullName = user.FullName.Trim();
			user.Email = user.Email.ToLower().Trim();
			user.PasswordHash = HashHelper.ComputeHash(password);
			user.Status = 1;
			user.CreatedAt = DateTimeHelper.Now;

			_context.Users.Add(user);
			await _context.SaveChangesAsync();
			TempData["Success"] = "User created successfully";
			return RedirectToAction("Users");
		}

		public async Task<IActionResult> EditUser(int id)
		{
			var user = await _context.Users.FindAsync(id);
			if (user == null) return NotFound();

			ViewBag.Roles = await _context.Roles.ToListAsync();
			return View(user);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditUser(User user)
		{
			// Custom validation
			if (!Validation.IsValidUsername(user.Username))
			{
				TempData["Error"] = "Username must be between 3 and 100 characters";
				ViewBag.Roles = await _context.Roles.ToListAsync();
				return View(user);
			}

			if (!Validation.IsValidFullName(user.FullName))
			{
				TempData["Error"] = "Full name is required and cannot exceed 200 characters";
				ViewBag.Roles = await _context.Roles.ToListAsync();
				return View(user);
			}

			if (!Validation.IsValidEmail(user.Email))
			{
				TempData["Error"] = "Invalid email format";
				ViewBag.Roles = await _context.Roles.ToListAsync();
				return View(user);
			}

			if (!Validation.IsValidPhone(user.Phone))
			{
				TempData["Error"] = "Invalid phone number format";
				ViewBag.Roles = await _context.Roles.ToListAsync();
				return View(user);
			}

			if (await _context.Users.AnyAsync(u => u.Username == user.Username && u.UserId != user.UserId))
			{
				TempData["Error"] = "Username already exists";
				ViewBag.Roles = await _context.Roles.ToListAsync();
				return View(user);
			}

			var existingUser = await _context.Users.FindAsync(user.UserId);
			if (existingUser == null) return NotFound();

			existingUser.Username = user.Username.ToLower().Trim();
			existingUser.FullName = user.FullName.Trim();
			existingUser.Email = user.Email.ToLower().Trim();
			existingUser.Phone = user.Phone;
			existingUser.RoleId = user.RoleId;
			existingUser.Status = user.Status;

			await _context.SaveChangesAsync();
			TempData["Success"] = "User updated successfully";
			return RedirectToAction("Users");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteUser(int id)
		{
			var user = await _context.Users.FindAsync(id);
			if (user != null)
			{
				_context.Users.Remove(user);
				await _context.SaveChangesAsync();
				TempData["Success"] = "User deleted successfully";
			}
			return RedirectToAction("Users");
		}

		// COURSE MANAGEMENT
		public async Task<IActionResult> Courses()
		{
			var courses = await _context.Courses.Include(c => c.Owner).Include(c => c.Category).ToListAsync();
			ViewBag.Categories = await _context.CourseCategories.ToListAsync();
			return View(courses);
		}

		// CATEGORY MANAGEMENT
		public async Task<IActionResult> Categories()
		{
			var categories = await _context.CourseCategories.ToListAsync();
			return View(categories);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateCategory(CourseCategory category)
		{
			if (string.IsNullOrWhiteSpace(category.Name) || category.Name.Length > 100)
			{
				TempData["Error"] = "Tên danh mục là bắt buộc và không quá 100 ký tự";
				return RedirectToAction("Categories");
			}

			if (string.IsNullOrWhiteSpace(category.Slug) || category.Slug.Length > 100)
			{
				TempData["Error"] = "Slug là bắt buộc và không quá 100 ký tự";
				return RedirectToAction("Categories");
			}

			if (await _context.CourseCategories.AnyAsync(c => c.Slug == category.Slug.ToLower().Trim()))
			{
				TempData["Error"] = "Slug đã tồn tại";
				return RedirectToAction("Categories");
			}

			category.Name = category.Name.Trim();
			category.Slug = category.Slug.ToLower().Trim();
			category.Description = category.Description?.Trim();

			_context.CourseCategories.Add(category);
			await _context.SaveChangesAsync();
			TempData["Success"] = "Tạo danh mục thành công";
			return RedirectToAction("Categories");
		}

		[HttpPost]
		public async Task<IActionResult> EditCategory(int id, string name, string slug, string description)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(name))
				{
					return Json(new { success = false, message = "Tên danh mục không được để trống" });
				}

				var category = await _context.CourseCategories.FindAsync(id);
				if (category == null)
				{
					return Json(new { success = false, message = "Không tìm thấy danh mục" });
				}

				// Check if slug already exists (excluding current category)
				if (!string.IsNullOrWhiteSpace(slug) && await _context.CourseCategories
					.AnyAsync(c => c.Slug == slug.ToLower().Trim() && c.CategoryId != id))
				{
					return Json(new { success = false, message = "Slug đã tồn tại" });
				}

				category.Name = name.Trim();
				category.Slug = slug?.ToLower().Trim() ?? category.Slug;
				category.Description = description?.Trim();
				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "Cập nhật danh mục thành công" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
			}
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteCategory(int id)
		{
			var category = await _context.CourseCategories.FindAsync(id);
			if (category != null)
			{
				_context.CourseCategories.Remove(category);
				await _context.SaveChangesAsync();
				TempData["Success"] = "Xóa danh mục thành công";
			}
			return RedirectToAction("Categories");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateCourse(Course course)
		{
			// Custom validation
			if (string.IsNullOrWhiteSpace(course.Title) || course.Title.Length > 200)
			{
				TempData["Error"] = "Title is required and cannot exceed 200 characters";
				return RedirectToAction("Courses");
			}

			if (string.IsNullOrWhiteSpace(course.Slug) || course.Slug.Length > 100)
			{
				TempData["Error"] = "Slug is required and cannot exceed 100 characters";
				return RedirectToAction("Courses");
			}

			if (course.Price < 0)
			{
				TempData["Error"] = "Price must be a positive number";
				return RedirectToAction("Courses");
			}

			if (!string.IsNullOrEmpty(course.CoverUrl) && !Uri.IsWellFormedUriString(course.CoverUrl, UriKind.Absolute))
			{
				TempData["Error"] = "Invalid URL format for cover image";
				return RedirectToAction("Courses");
			}

			if (await _context.Courses.AnyAsync(c => c.Slug == course.Slug.ToLower().Trim()))
			{
				TempData["Error"] = "Slug already exists";
				return RedirectToAction("Courses");
			}

			course.Title = course.Title.Trim();
			course.Slug = course.Slug.ToLower().Trim();
			course.Summary = course.Summary?.Trim();
			course.OwnerId = GetCurrentUserId();
			course.CreatedAt = DateTimeHelper.Now;

			_context.Courses.Add(course);
			await _context.SaveChangesAsync();
			TempData["Success"] = "Course created successfully";
			return RedirectToAction("Courses");
		}

		public async Task<IActionResult> EditCourse(int id)
		{
			var course = await _context.Courses.FindAsync(id);
			if (course == null) return NotFound();
			return View(course);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditCourse(Course course)
		{
			if (string.IsNullOrWhiteSpace(course.Title) || course.Title.Length > 200)
			{
				TempData["Error"] = "Title is required and cannot exceed 200 characters";
				return View(course);
			}

			if (string.IsNullOrWhiteSpace(course.Slug) || course.Slug.Length > 100)
			{
				TempData["Error"] = "Slug is required and cannot exceed 100 characters";
				return View(course);
			}

			if (course.Price < 0)
			{
				TempData["Error"] = "Price must be a positive number";
				return View(course);
			}

			if (!string.IsNullOrEmpty(course.CoverUrl) && !Uri.IsWellFormedUriString(course.CoverUrl, UriKind.Absolute))
			{
				TempData["Error"] = "Invalid URL format for cover image";
				return View(course);
			}

			if (await _context.Courses.AnyAsync(c => c.Slug == course.Slug.ToLower().Trim() && c.CourseId != course.CourseId))
			{
				TempData["Error"] = "Slug already exists";
				return View(course);
			}

			var existingCourse = await _context.Courses.FindAsync(course.CourseId);
			if (existingCourse == null) return NotFound();

			existingCourse.Title = course.Title.Trim();
			existingCourse.Slug = course.Slug.ToLower().Trim();
			existingCourse.Summary = course.Summary?.Trim();
			existingCourse.Price = course.Price;
			existingCourse.CoverUrl = course.CoverUrl;
			existingCourse.IsPublished = course.IsPublished;

			await _context.SaveChangesAsync();
			TempData["Success"] = "Course updated successfully";
			return RedirectToAction("Courses");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteCourse(int id)
		{
			var course = await _context.Courses.FindAsync(id);
			if (course != null)
			{
				_context.Courses.Remove(course);
				await _context.SaveChangesAsync();
				TempData["Success"] = "Course deleted successfully";
			}
			return RedirectToAction("Courses");
		}

		// FLASHCARD SET MANAGEMENT
		public async Task<IActionResult> FlashcardSets()
		{
			var flashcardSets = await _context.FlashcardSets
				.Include(fs => fs.Owner)
				.Include(fs => fs.Flashcards)
				.Where(fs => !fs.IsDeleted)
				.ToListAsync();
			return View(flashcardSets);
		}

		public async Task<IActionResult> FlashcardSetDetails(int id)
		{
			var flashcardSet = await _context.FlashcardSets
				.Include(fs => fs.Owner)
				.Include(fs => fs.Flashcards)
				.FirstOrDefaultAsync(fs => fs.SetId == id && !fs.IsDeleted);
			if (flashcardSet == null) return NotFound();

			flashcardSet.Flashcards = flashcardSet.Flashcards.OrderBy(f => f.OrderIndex).ToList();
			return View(flashcardSet);
		}

		[HttpGet]
		public IActionResult CreateFlashcardSet()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateFlashcardSet(CreateFlashcardSetViewModel model, IFormFile? coverFile)
		{
			if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Length > 200)
			{
				TempData["Error"] = "Tiêu đề bộ Flashcard là bắt buộc và không được vượt quá 200 ký tự.";
				return View(model);
			}

			var userId = GetCurrentUserId();

			if (coverFile is { Length: > 0 })
			{
				var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
				var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
				if (!allowed.Contains(ext))
				{
					ModelState.AddModelError(nameof(model.CoverUrl), "Định dạng ảnh không hợp lệ(jpg, jpeg, png, gif, webp).");
					return View(model);
				}

				var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
				var folder = $"uploads/flashcards/{DateTimeHelper.Now:yyyy/MM}";
				var physical = Path.Combine(env.WebRootPath, folder);
				Directory.CreateDirectory(physical);

				var fileName = $"{Guid.NewGuid():N}{ext}";
				var fullPath = Path.Combine(physical, fileName);

				await using (var stream = System.IO.File.Create(fullPath))
				{
					await coverFile.CopyToAsync(stream);
				}

				model.CoverUrl = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
			}

			var flashcardSet = _flashcardService.CreateFlashcardSet(model, userId);

			if (flashcardSet == null)
			{
				TempData["Error"] = "Có lỗi xảy ra khi tạo bộ flashcard";
				return View(model);
			}

			if (!string.IsNullOrWhiteSpace(model.FlashcardsJson))
			{
				try
				{
					var flashcardsData = JsonSerializer.Deserialize<List<FlashcardDataViewModel>>(
						model.FlashcardsJson,
						new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
					);

					if (flashcardsData != null && flashcardsData.Any())
					{
						foreach (var flashcardData in flashcardsData)
						{
							var flashcardModel = new CreateFlashcardViewModel
							{
								SetId = flashcardSet.SetId,
								FrontText = flashcardData.FrontText,
								BackText = flashcardData.BackText,
								Hint = flashcardData.Hint,
								OrderIndex = flashcardData.OrderIndex
							};

							_flashcardService.CreateFlashcard(flashcardModel, userId);
						}
					}
				}
				catch (Exception ex)
				{
					// Log the error
				}
			}

			TempData["Success"] = "Tạo bộ Flashcard thành công.";
			return RedirectToAction("FlashcardSets");
		}

		[HttpGet]
		public async Task<IActionResult> EditFlashcardSet(int id)
		{
			var set = await _context.FlashcardSets.FindAsync(id);
			if (set == null || set.IsDeleted) return NotFound();
			return View(set);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditFlashcardSet(FlashcardSet model, IFormFile? coverFile)
		{
			if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Length > 200)
			{
				TempData["Error"] = "Tiêu đề bộ Flashcard là bắt buộc và không được vượt quá 200 ký tự.";
				return View(model);
			}

			var set = await _context.FlashcardSets.FindAsync(model.SetId);
			if (set == null || set.IsDeleted) return NotFound();

			set.Title = model.Title.Trim();
			set.Description = model.Description?.Trim();
			set.Visibility = string.IsNullOrWhiteSpace(model.Visibility) ? "Private" : model.Visibility;
			set.Language = string.IsNullOrWhiteSpace(model.Language) ? "Tiếng Việt" : model.Language;
			set.TagsText = model.TagsText?.Trim();
			set.UpdatedAt = DateTimeHelper.Now;

			if (coverFile is { Length: > 0 })
			{
				var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
				var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
				if (allowed.Contains(ext))
				{
					var folder = $"uploads/flashcards/{DateTimeHelper.Now:yyyy/MM}";
					var env = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
					var physical = Path.Combine(env.WebRootPath, folder);
					Directory.CreateDirectory(physical);

					var fileName = $"{Guid.NewGuid():N}{ext}";
					var fullPath = Path.Combine(physical, fileName);

					await using (var stream = System.IO.File.Create(fullPath))
					{
						await coverFile.CopyToAsync(stream);
					}

					set.CoverUrl = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
				}
			}

			await _context.SaveChangesAsync();
			TempData["Success"] = "Cập nhật bộ Flashcard thành công.";
			return RedirectToAction("FlashcardSets");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteFlashcardSet(int id)
		{
			var set = await _context.FlashcardSets.FindAsync(id);
			if (set != null && !set.IsDeleted)
			{
				set.IsDeleted = true;
				set.UpdatedAt = DateTimeHelper.Now;
				await _context.SaveChangesAsync();
				TempData["Success"] = "Đã xóa bộ Flashcard.";
			}
			return RedirectToAction("FlashcardSets");
		}

		// CARD MANAGEMENT WITHIN SET
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddFlashcardToSet(int setId, string frontText, string backText, string? hint)
		{
			if (string.IsNullOrWhiteSpace(frontText) || string.IsNullOrWhiteSpace(backText))
			{
				TempData["Error"] = "Mặt trước và mặt sau không được để trống.";
				return RedirectToAction("FlashcardSetDetails", new { id = setId });
			}

			var set = await _context.FlashcardSets.Include(fs => fs.Flashcards).FirstOrDefaultAsync(fs => fs.SetId == setId && !fs.IsDeleted);
			if (set == null) return NotFound();

			var maxOrderIndex = set.Flashcards.Any() ? set.Flashcards.Max(f => f.OrderIndex) : 0;

			var card = new Flashcard
			{
				SetId = setId,
				FrontText = frontText.Trim(),
				BackText = backText.Trim(),
				Hint = hint?.Trim(),
				OrderIndex = maxOrderIndex + 1,
				CreatedAt = DateTimeHelper.Now
			};

			_context.Flashcards.Add(card);
			await _context.SaveChangesAsync();
			TempData["Success"] = "Đã thêm thẻ mới.";
			return RedirectToAction("FlashcardSetDetails", new { id = setId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditFlashcardInSet(int cardId, string frontText, string backText, string? hint)
		{
			if (string.IsNullOrWhiteSpace(frontText) || string.IsNullOrWhiteSpace(backText))
			{
				TempData["Error"] = "Mặt trước và mặt sau không được để trống.";
				return RedirectToAction("FlashcardSets");
			}

			var card = await _context.Flashcards.FindAsync(cardId);
			if (card == null) return NotFound();

			card.FrontText = frontText.Trim();
			card.BackText = backText.Trim();
			card.Hint = hint?.Trim();
			card.UpdatedAt = DateTimeHelper.Now;

			await _context.SaveChangesAsync();
			TempData["Success"] = "Cập nhật thẻ thành công.";
			return RedirectToAction("FlashcardSetDetails", new { id = card.SetId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteFlashcardFromSet(int id)
		{
			var card = await _context.Flashcards.FindAsync(id);
			int setId = 0;
			if (card != null)
			{
				setId = card.SetId;
				_context.Flashcards.Remove(card);
				await _context.SaveChangesAsync();
				TempData["Success"] = "Đã xóa thẻ.";
			}
			if (setId > 0)
			{
				return RedirectToAction("FlashcardSetDetails", new { id = setId });
			}
			return RedirectToAction("FlashcardSets");
		}

		// TEST MANAGEMENT
		public async Task<IActionResult> Tests()
		{
			var tests = await _context.Tests.Include(t => t.Owner).ToListAsync();
			return View(tests);
		}

		public async Task<IActionResult> TestDetails(int id)
		{
			var test = await _context.Tests
				.Include(t => t.Questions)
					.ThenInclude(q => q.QuestionOptions)
				.FirstOrDefaultAsync(t => t.TestId == id);
			return View(test);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> CreateTest(Test test)
		{
			// Custom validation
			if (string.IsNullOrWhiteSpace(test.Title) || test.Title.Length > 200)
			{
				TempData["Error"] = "Title is required and cannot exceed 200 characters";
				return RedirectToAction("Tests");
			}

			if (test.TimeLimitSec.HasValue && (test.TimeLimitSec < 1 || test.TimeLimitSec > 1440))
			{
				TempData["Error"] = "Time limit must be between 1 and 1440 minutes";
				return RedirectToAction("Tests");
			}

			if (test.MaxAttempts < 1 || test.MaxAttempts > 10)
			{
				TempData["Error"] = "Max attempts must be between 1 and 10";
				return RedirectToAction("Tests");
			}

			if (!string.IsNullOrEmpty(test.Visibility) && test.Visibility != "public" && test.Visibility != "private")
			{
				TempData["Error"] = "Visibility must be either public or private";
				return RedirectToAction("Tests");
			}

			if (!string.IsNullOrEmpty(test.GradingMode) && test.GradingMode != "auto" && test.GradingMode != "manual")
			{
				TempData["Error"] = "Grading mode must be either auto or manual";
				return RedirectToAction("Tests");
			}

			test.Title = test.Title.Trim();
			test.Description = test.Description?.Trim();
			test.Visibility = test.Visibility ?? "private";
			test.GradingMode = test.GradingMode ?? "auto";
			test.OwnerId = GetCurrentUserId();
			test.CreatedAt = DateTimeHelper.Now;

			_context.Tests.Add(test);
			await _context.SaveChangesAsync();
			TempData["Success"] = "Test created successfully";
			return RedirectToAction("Tests");
		}

		public async Task<IActionResult> EditTest(int id)
		{
			var test = await _context.Tests.FindAsync(id);
			if (test == null) return NotFound();

			var model = new Test
			{
				TestId = test.TestId,
				Title = test.Title,
				Description = test.Description,
				TimeLimitSec = test.TimeLimitSec,
				Visibility = test.Visibility,
				GradingMode = test.GradingMode,
				MaxAttempts = test.MaxAttempts
			};

			return View(model);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> EditTest(Test model)
		{
			// Custom validation
			if (string.IsNullOrWhiteSpace(model.Title) || model.Title.Length > 200)
			{
				TempData["Error"] = "Title is required and cannot exceed 200 characters";
				return View(model);
			}

			if (model.TimeLimitSec.HasValue && (model.TimeLimitSec < 1 || model.TimeLimitSec > 1440))
			{
				TempData["Error"] = "Time limit must be between 1 and 1440 minutes";
				return View(model);
			}

			if (model.MaxAttempts < 1 || model.MaxAttempts > 10)
			{
				TempData["Error"] = "Max attempts must be between 1 and 10";
				return View(model);
			}

			if (!string.IsNullOrEmpty(model.Visibility) && model.Visibility != "public" && model.Visibility != "private")
			{
				TempData["Error"] = "Visibility must be either public or private";
				return View(model);
			}

			if (!string.IsNullOrEmpty(model.GradingMode) && model.GradingMode != "auto" && model.GradingMode != "manual")
			{
				TempData["Error"] = "Grading mode must be either auto or manual";
				return View(model);
			}

			var test = await _context.Tests.FindAsync(model.TestId);
			if (test == null) return NotFound();

			test.Title = model.Title.Trim();
			test.Description = model.Description?.Trim();
			test.TimeLimitSec = model.TimeLimitSec;
			test.Visibility = model.Visibility ?? "private";
			test.GradingMode = model.GradingMode ?? "auto";
			test.MaxAttempts = model.MaxAttempts;

			await _context.SaveChangesAsync();
			TempData["Success"] = "Test updated successfully";
			return RedirectToAction("Tests");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteTest(int id)
		{
			var test = await _context.Tests.FindAsync(id);
			if (test != null)
			{
				_context.Tests.Remove(test);
				await _context.SaveChangesAsync();
				TempData["Success"] = "Test deleted successfully";
			}
			return RedirectToAction("Tests");
		}

		// QUESTION MANAGEMENT
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> AddQuestion(int TestId, string StemText, decimal Points, string Type, string[] OptionTexts, int CorrectOption)
		{
			// Custom validation
			if (string.IsNullOrWhiteSpace(StemText))
			{
				TempData["Error"] = "Question text is required";
				return RedirectToAction("TestDetails", new { id = TestId });
			}

			if (!Validation.IsValidPoints(Points))
			{
				TempData["Error"] = "Points must be between 0.1 and 100";
				return RedirectToAction("TestDetails", new { id = TestId });
			}

			if (Type == "multiple_choice" && (OptionTexts == null || !OptionTexts.Any(o => !string.IsNullOrWhiteSpace(o))))
			{
				TempData["Error"] = "At least one option is required for multiple choice questions";
				return RedirectToAction("TestDetails", new { id = TestId });
			}

			var question = new Question
			{
				TestId = TestId,
				StemText = StemText.Trim(),
				Points = Points,
				Type = Type,
				OrderIndex = await _context.Questions.Where(q => q.TestId == TestId).CountAsync() + 1
			};

			_context.Questions.Add(question);
			await _context.SaveChangesAsync();

			// Add options for multiple choice
			if (Type == "multiple_choice" && OptionTexts != null)
			{
				for (int i = 0; i < OptionTexts.Length; i++)
				{
					if (!string.IsNullOrWhiteSpace(OptionTexts[i]))
					{
						_context.QuestionOptions.Add(new QuestionOption
						{
							QuestionId = question.QuestionId,
							OptionText = OptionTexts[i].Trim(),
							IsCorrect = i == CorrectOption,
							OrderIndex = i + 1
						});
					}
				}
				await _context.SaveChangesAsync();
			}

			TempData["Success"] = "Question added successfully";
			return RedirectToAction("TestDetails", new { id = TestId });
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> DeleteQuestion(int id)
		{
			var question = await _context.Questions.Include(q => q.QuestionOptions).FirstOrDefaultAsync(q => q.QuestionId == id);
			if (question != null)
			{
				_context.QuestionOptions.RemoveRange(question.QuestionOptions);
				_context.Questions.Remove(question);
				await _context.SaveChangesAsync();
				TempData["Success"] = "Question deleted successfully";
				return RedirectToAction("TestDetails", new { id = question.TestId });
			}
			return RedirectToAction("Tests");
		}

		// CLASS MANAGEMENT
		//public async Task<IActionResult> Classes()
		//{
		//	var classes = await _context.Class.Include(c => c.Teacher).ToListAsync();
		//	return View(classes);
		//}

		//[HttpPost]
		//[ValidateAntiForgeryToken]
		//public async Task<IActionResult> CreateClass(Class classEntity)
		//{
		//	// Custom validation
		//	if (string.IsNullOrWhiteSpace(classEntity.Name) || classEntity.Name.Length > 200)
		//	{
		//		TempData["Error"] = "Class name is required and cannot exceed 200 characters";
		//		return RedirectToAction("Classes");
		//	}

		//	if (string.IsNullOrWhiteSpace(classEntity.Code) || classEntity.Code.Length > 20)
		//	{
		//		TempData["Error"] = "Class code is required and cannot exceed 20 characters";
		//		return RedirectToAction("Classes");
		//	}

		//	if (await _context.Classes.AnyAsync(c => c.Code == classEntity.Code.ToUpper().Trim()))
		//	{
		//		TempData["Error"] = "Class code already exists";
		//		return RedirectToAction("Classes");
		//	}

		//	classEntity.Name = classEntity.Name.Trim();
		//	classEntity.Code = classEntity.Code.ToUpper().Trim();
		//	classEntity.Term = classEntity.Term?.Trim();
		//	classEntity.Description = classEntity.Description?.Trim();
		//	classEntity.CreatedAt = DateTimeHelper.Now;
		//	classEntity.TeacherId = GetCurrentUserId();

		//	_context.Classes.Add(classEntity);
		//	await _context.SaveChangesAsync();
		//	TempData["Success"] = "Class created successfully";
		//	return RedirectToAction("Classes");
		//}

		//public async Task<IActionResult> EditClass(int id)
		//{
		//	var classEntity = await _context.Classes.FindAsync(id);
		//	if (classEntity == null) return NotFound();
		//	return View(classEntity);
		//}

		//[HttpPost]
		//[ValidateAntiForgeryToken]
		//public async Task<IActionResult> EditClass(Class classEntity)
		//{
		//	// Custom validation
		//	if (string.IsNullOrWhiteSpace(classEntity.Name) || classEntity.Name.Length > 200)
		//	{
		//		TempData["Error"] = "Class name is required and cannot exceed 200 characters";
		//		return View(classEntity);
		//	}

		//	if (string.IsNullOrWhiteSpace(classEntity.Code) || classEntity.Code.Length > 20)
		//	{
		//		TempData["Error"] = "Class code is required and cannot exceed 20 characters";
		//		return View(classEntity);
		//	}

		//	if (await _context.Classes.AnyAsync(c => c.Code == classEntity.Code.ToUpper().Trim() && c.ClassId != classEntity.ClassId))
		//	{
		//		TempData["Error"] = "Class code already exists";
		//		return View(classEntity);
		//	}

		//	var existingClass = await _context.Classes.FindAsync(classEntity.ClassId);
		//	if (existingClass == null) return NotFound();

		//	existingClass.Name = classEntity.Name.Trim();
		//	existingClass.Code = classEntity.Code.ToUpper().Trim();
		//	existingClass.Term = classEntity.Term?.Trim();
		//	existingClass.Description = classEntity.Description?.Trim();

		//	await _context.SaveChangesAsync();
		//	TempData["Success"] = "Class updated successfully";
		//	return RedirectToAction("Classes");
		//}

		//[HttpPost]
		//[ValidateAntiForgeryToken]
		//public async Task<IActionResult> DeleteClass(int id)
		//{
		//	var classEntity = await _context.Classes.FindAsync(id);
		//	if (classEntity != null)
		//	{
		//		_context.Classes.Remove(classEntity);
		//		await _context.SaveChangesAsync();
		//		TempData["Success"] = "Class deleted successfully";
		//	}
		//	return RedirectToAction("Classes");
		//}

		// REPORTS
		public async Task<IActionResult> UserReports()
		{
			var data = new
			{
				TotalUsers = await _context.Users.CountAsync(),
				ActiveUsers = await _context.Users.Where(u => u.Status == 1).CountAsync(),
				NewUsersThisMonth = await _context.Users.Where(u => u.CreatedAt >= DateTimeHelper.Now.AddDays(-30)).CountAsync(),
				UsersByRole = await _context.Users.Include(u => u.Role).GroupBy(u => u.Role.Name).Select(g => new { Role = g.Key, Count = g.Count() }).ToListAsync()
			};
			return View(data);
		}

		public async Task<IActionResult> CourseReports()
		{
			var data = new
			{
				TotalCourses = await _context.Courses.CountAsync(),
				PublishedCourses = await _context.Courses.Where(c => c.IsPublished).CountAsync(),
				TotalPurchases = await _context.CoursePurchases.Where(p => p.Status == "Paid").CountAsync(),
				PopularCourses = await _context.CoursePurchases.Where(p => p.Status == "Paid").GroupBy(p => p.Course.Title).Select(g => new { Course = g.Key, Purchases = g.Count() }).OrderByDescending(x => x.Purchases).Take(10).ToListAsync()
			};
			return View(data);
		}

		public async Task<IActionResult> TestReports()
		{
			// ✅ Xử lý null-safe và đảm bảo luôn trả về dữ liệu hợp lệ
			var totalTests = await _context.Tests.CountAsync();
			var totalAttempts = await _context.TestAttempts.CountAsync();

			var recentAttempts = await _context.TestAttempts
				.Include(a => a.User)
				.Include(a => a.Test)
				.OrderByDescending(a => a.StartedAt)
				.Take(15)
				.Select(a => new
				{
					FullName = a.User.FullName ?? "Unknown",
					Title = a.Test.Title ?? "Unknown Test",
					Score = a.Score,
					MaxScore = a.MaxScore,
					StartedAt = a.StartedAt
				})
				.ToListAsync();

			var topScores = await _context.TestAttempts
				.Include(a => a.User)
				.Include(a => a.Test)
				.Where(a => a.Score.HasValue && a.MaxScore.HasValue && a.MaxScore > 0)
				.OrderByDescending(a => (decimal)a.Score!.Value / a.MaxScore!.Value)
				.Take(15)
				.Select(a => new
				{
					FullName = a.User.FullName ?? "Unknown",
					Title = a.Test.Title ?? "Unknown Test",
					Score = a.Score!.Value,
					MaxScore = a.MaxScore!.Value,
					Percentage = (decimal)a.Score!.Value / a.MaxScore!.Value * 100
				})
				.ToListAsync();

			var data = new
			{
				TotalTests = totalTests,
				TotalAttempts = totalAttempts,
				RecentAttempts = recentAttempts,
				TopScores = topScores
			};

			return View(data);
		}

		public async Task<IActionResult> RevenueReports()
		{
			// ✅ DEBUG: In ra log để kiểm tra dữ liệu
			var allPurchases = await _context.CoursePurchases.ToListAsync();
			var paidPurchases = await _context.CoursePurchases.Where(p => p.Status == "Paid").ToListAsync();
			
			Console.WriteLine($"=== REVENUE REPORTS DEBUG ===");
			Console.WriteLine($"Total CoursePurchases: {allPurchases.Count}");
			Console.WriteLine($"Paid CoursePurchases: {paidPurchases.Count}");
			Console.WriteLine($"Statuses: {string.Join(", ", allPurchases.Select(p => p.Status).Distinct())}");

			// ✅ Tính tổng doanh thu từ CoursePurchases (100%)
			var totalGrossRevenue = await _context.CoursePurchases
				.Where(p => p.Status == "Paid")
				.SumAsync(p => (decimal?)p.PricePaid) ?? 0;

			var monthlyGrossRevenue = await _context.CoursePurchases
				.Where(p => p.Status == "Paid" && p.PurchasedAt >= DateTimeHelper.Now.AddDays(-30))
				.SumAsync(p => (decimal?)p.PricePaid) ?? 0;

			Console.WriteLine($"Total Gross Revenue: {totalGrossRevenue}");
			Console.WriteLine($"Monthly Gross Revenue: {monthlyGrossRevenue}");

			// ✅ Tính 40% cho admin
			var totalRevenue = totalGrossRevenue * 0.40m;
			var monthlyRevenue = monthlyGrossRevenue * 0.40m;

			// ✅ Lấy giao dịch gần đây với null-safe
			var recentPurchases = await _context.CoursePurchases
				.Include(p => p.Buyer)
				.Include(p => p.Course)
				.Where(p => p.Status == "Paid")
				.OrderByDescending(p => p.PurchasedAt)
				.Take(15)
				.Select(p => new { 
					FullName = p.Buyer != null ? p.Buyer.FullName : "Unknown",
					Title = p.Course != null ? p.Course.Title : "Unknown Course",
					PricePaid = p.PricePaid,
					PurchasedAt = p.PurchasedAt
				})
				.ToListAsync();

			Console.WriteLine($"Recent Purchases Count: {recentPurchases.Count}");

			// ✅ TopPayments từ Payments table
			var topPayments = await _context.Payments
				.Where(p => p.Status == "Paid")
				.OrderByDescending(p => p.Amount)
				.Take(10)
				.Select(p => new { p.Amount, p.PaidAt })
				.ToListAsync();

			Console.WriteLine($"Top Payments Count: {topPayments.Count}");
			Console.WriteLine($"=== END DEBUG ===");

			var data = new
			{
				TotalRevenue = totalRevenue, // 40% cho admin
				MonthlyRevenue = monthlyRevenue, // 40% cho admin
				RecentPurchases = recentPurchases,
				TopPayments = topPayments
			};

			return View(data);
		}

		// PROFILE & SETTINGS
		public async Task<IActionResult> Profile()
		{
			var userId = GetCurrentUserId();
			var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == userId);
			if (user == null) return NotFound();
			return View(user);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Profile(User user)
		{
			var currentUserId = GetCurrentUserId();
			if (user.UserId != currentUserId) return Forbid();

			// Validation
			if (string.IsNullOrWhiteSpace(user.Username) || user.Username.Length > 100)
			{
				TempData["Error"] = "Username is required and cannot exceed 100 characters";
				return View(user);
			}

			if (string.IsNullOrWhiteSpace(user.FullName) || user.FullName.Length > 200)
			{
				TempData["Error"] = "Full name is required and cannot exceed 200 characters";
				return View(user);
			}

			if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@"))
			{
				TempData["Error"] = "Valid email is required";
				return View(user);
			}

			if (await _context.Users.AnyAsync(u => u.Username == user.Username && u.UserId != user.UserId))
			{
				TempData["Error"] = "Username already exists";
				return View(user);
			}

			if (await _context.Users.AnyAsync(u => u.Email == user.Email && u.UserId != user.UserId))
			{
				TempData["Error"] = "Email already exists";
				return View(user);
			}

			var existingUser = await _context.Users.FindAsync(user.UserId);
			if (existingUser == null) return NotFound();

			existingUser.Username = user.Username.ToLower().Trim();
			existingUser.FullName = user.FullName.Trim();
			existingUser.Email = user.Email.ToLower().Trim();
			existingUser.Phone = user.Phone;

			await _context.SaveChangesAsync();
			TempData["Success"] = "Profile updated successfully";
			return View(existingUser);
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
		{
			if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword))
			{
				TempData["Error"] = "All password fields are required";
				return RedirectToAction("Profile");
			}

			if (newPassword != confirmPassword)
			{
				TempData["Error"] = "New passwords do not match";
				return RedirectToAction("Profile");
			}

			if (newPassword.Length < 8)
			{
				TempData["Error"] = "Password must be at least 8 characters";
				return RedirectToAction("Profile");
			}

			var userId = GetCurrentUserId();
			var user = await _context.Users.FindAsync(userId);
			if (user == null) return NotFound();

			// Verify current password
			if (user.PasswordHash != HashHelper.ComputeHash(currentPassword))
			{
				TempData["Error"] = "Current password is incorrect";
				return RedirectToAction("Profile");
			}

			user.PasswordHash = HashHelper.ComputeHash(newPassword);
			await _context.SaveChangesAsync();
			TempData["Success"] = "Password changed successfully";
			return RedirectToAction("Profile");
		}

		public IActionResult Settings()
		{
			return View();
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult UpdateTheme(string theme)
		{
			// Store theme preference in session/cookie
			Response.Cookies.Append("Theme", theme, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			TempData["Success"] = "Theme updated successfully";
			return RedirectToAction("Settings");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult UpdateLanguage(string language, string dateFormat, string timeZone)
		{
			// Store language preferences in session/cookie
			Response.Cookies.Append("Language", language, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			Response.Cookies.Append("DateFormat", dateFormat, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			Response.Cookies.Append("TimeZone", timeZone, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			TempData["Success"] = "Language settings updated successfully";
			return RedirectToAction("Settings");
		}

		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult UpdatePreferences(bool emailNotifications, bool soundNotifications, bool autoSave, bool showTips)
		{
			// Store preferences in session/cookie
			Response.Cookies.Append("EmailNotifications", emailNotifications.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			Response.Cookies.Append("SoundNotifications", soundNotifications.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			Response.Cookies.Append("AutoSave", autoSave.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			Response.Cookies.Append("ShowTips", showTips.ToString(), new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
			TempData["Success"] = "Preferences updated successfully";
			return RedirectToAction("Settings");
		}

		private int GetCurrentUserId()
		{
			var username = User.Identity?.Name;
			var user = _context.Users.FirstOrDefault(u => u.Username == username);
			return user?.UserId ?? 1;
		}
	}
}
