using Microsoft.AspNetCore.Mvc;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;

namespace Quiz_Web.Controllers
{
	public class CourseController : Controller
	{
		private readonly ILogger<CourseController> _logger;
		private readonly ICourseService _courseService;
		private readonly IWebHostEnvironment _env;
		private readonly LearningPlatformContext _context;
		private readonly IStorageService _storageService;

		public CourseController(
			ILogger<CourseController> logger,
			ICourseService courseService,
			IWebHostEnvironment env,
			LearningPlatformContext context,
			IStorageService storageService)
		{
			_logger = logger;
			_courseService = courseService;
			_env = env;
			_context = context;
			_storageService = storageService;
		}

		// GET: /courses
		[Route("/courses")]
		[HttpGet]
		public IActionResult Index(
			string? search, 
			int page = 1, 
			int pageSize = 12,
			decimal? minRating = null,
			decimal? maxRating = null,
			bool? isFree = null,
			string? sortBy = null)
		{
			_logger.LogInformation($"Courses Index - Search: {search}, Page: {page}, MinRating: {minRating}, MaxRating: {maxRating}, IsFree: {isFree}, SortBy: {sortBy}");

			var courses = _courseService.GetFilteredAndSortedCourses(
				searchKeyword: search,
				categorySlug: null,
				minRating: minRating,
				maxRating: maxRating,
				isFree: isFree,
				sortBy: sortBy);

			ViewBag.SearchKeyword = search;
			ViewBag.MinRating = minRating;
			ViewBag.MaxRating = maxRating;
			ViewBag.IsFree = isFree;
			ViewBag.SortBy = sortBy;

			var totalCount = courses.Count;
			var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
			var pagedCourses = courses.Skip((page - 1) * pageSize).Take(pageSize).ToList();

			ViewBag.CurrentPage = page;
			ViewBag.TotalPages = totalPages;
			ViewBag.PageSize = pageSize;
			ViewBag.TotalCount = totalCount;

			return View(pagedCourses);
		}

		// GET: /courses/search?q=keyword
		[Route("/courses/search")]
		[HttpGet]
		public IActionResult Search(
			string q, 
			int page = 1, 
			int pageSize = 12,
			decimal? minRating = null,
			decimal? maxRating = null,
			bool? isFree = null,
			string? sortBy = null)
		{
			_logger.LogInformation($"Course Search - Query: {q}");
			if (string.IsNullOrWhiteSpace(q))
				return RedirectToAction(nameof(Index));

			var courses = _courseService.GetFilteredAndSortedCourses(
				searchKeyword: q,
				categorySlug: null,
				minRating: minRating,
				maxRating: maxRating,
				isFree: isFree,
				sortBy: sortBy);

			ViewBag.SearchKeyword = q;
			ViewBag.MinRating = minRating;
			ViewBag.MaxRating = maxRating;
			ViewBag.IsFree = isFree;
			ViewBag.SortBy = sortBy;

			var totalCount = courses.Count;
			var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
			var pagedCourses = courses.Skip((page - 1) * pageSize).Take(pageSize).ToList();

			ViewBag.TotalCount = totalCount;
			ViewBag.TotalPages = totalPages;
			ViewBag.CurrentPage = page;
			ViewBag.PageSize = pageSize;
			ViewBag.CategorySlug = null;

			return View("Index", pagedCourses);
		}

		// GET: /courses/create
		[Authorize]
		[Route("/courses/create")]
		[HttpGet]
		public IActionResult Create()
		{
			return View();
		}

		// POST: /courses/create
		[Authorize]
		[Route("/courses/create")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(CreateCourseViewModel model, IFormFile? coverFile, [FromServices] HtmlSanitizer sanitizer)
		{
			if (!ModelState.IsValid)
			{
				return View(model);
			}

			model.Description = sanitizer.Sanitize(model.Description ?? string.Empty);

			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
			{
				return Challenge();
			}

			if (coverFile is { Length: > 0 })
			{
				var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
				var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
				if (!allowed.Contains(ext))
				{
					ModelState.AddModelError(nameof(model.CoverUrl), "Ð?nh d?ng ?nh không h?p l? (jpg, jpeg, png, gif, webp).");
					return View(model);
				}

				var folder = $"uploads/courses/{DateTime.UtcNow:yyyy/MM}";
				var physical = Path.Combine(_env.WebRootPath, folder);
				Directory.CreateDirectory(physical);

				var fileName = $"{Guid.NewGuid():N}{ext}";
				var fullPath = Path.Combine(physical, fileName);

				await using (var stream = System.IO.File.Create(fullPath))
				{
					await coverFile.CopyToAsync(stream);
				}

				model.CoverUrl = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
			}

			if (!_courseService.IsSlugUnique(model.Slug))
			{
				ModelState.AddModelError("Slug", "Slug này dã t?n t?i. Vui lòng ch?n slug khác.");
				return View(model);
			}

			var course = _courseService.CreateCourse(model, userId);

			if (course == null)
			{
				TempData["Error"] = "Có l?i x?y ra khi t?o khóa h?c";
				return View(model);
			}

			TempData["Success"] = "T?o khóa h?c thành công!";
			return RedirectToAction("Detail", new { slug = course.Slug });
		}

		// GET: /courses/mine - list all courses created by current user
		[Authorize]
		[Route("/courses/mine")]
		[HttpGet]
		public IActionResult My()
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			var courses = _courseService.GetCoursesByOwner(userId);
			return View("My", courses);
		}

		// GET: /courses/edit/{id}
		[Authorize]
		[Route("/courses/edit/{id:int}")]
		[HttpGet]
		public IActionResult Edit(int id)
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			var course = _courseService.GetOwnedCourse(id, userId);
			if (course == null) return NotFound();

			var vm = new EditCourseViewModel
			{
				CourseId = course.CourseId,
				Title = course.Title,
				Slug = course.Slug,
				Description = course.Summary,
				Price = course.Price,
				//Currency = course.Currency,
				IsPublished = course.IsPublished,
				CoverUrl = course.CoverUrl
			};
			return View("Edit", vm);
		}

		// POST: /courses/edit/{id}
		[Authorize]
		[Route("/courses/edit/{id:int}")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(int id, EditCourseViewModel model, IFormFile? coverFile, [FromServices] HtmlSanitizer sanitizer)
		{
			if (id != model.CourseId) return BadRequest();

			if (!ModelState.IsValid)
				return View("Edit", model);

			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			// upload new cover if provided
			if (coverFile is { Length: > 0 })
			{
				var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
				var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
				if (!allowed.Contains(ext))
				{
					ModelState.AddModelError(nameof(model.CoverUrl), "Ð?nh d?ng ?nh không h?p l? (jpg, jpeg, png, gif, webp).");
					return View("Edit", model);
				}

				var folder = $"uploads/courses/{DateTime.UtcNow:yyyy/MM}";
				var physical = Path.Combine(_env.WebRootPath, folder);
				Directory.CreateDirectory(physical);

				var fileName = $"{Guid.NewGuid():N}{ext}";
				var fullPath = Path.Combine(physical, fileName);
				await using (var stream = System.IO.File.Create(fullPath))
				{
					await coverFile.CopyToAsync(stream);
				}

				model.CoverUrl = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
			}

			// slug unique check excluding current course
			var slugClash = _courseService
				.GetCoursesByOwner(userId)
				.Any(c => c.Slug == model.Slug && c.CourseId != model.CourseId);
			if (slugClash)
			{
				ModelState.AddModelError(nameof(model.Slug), "Slug này dã t?n t?i.");
				return View("Edit", model);
			}

			var sanitized = sanitizer.Sanitize(model.Description ?? string.Empty);
			var updated = _courseService.UpdateCourse(model, userId, sanitized);
			if (updated == null)
			{
				TempData["Error"] = "Không th? c?p nh?t khóa h?c.";
				return View("Edit", model);
			}

			TempData["Success"] = "C?p nh?t khóa h?c thành công!";
			return RedirectToAction("Detail", new { slug = updated.Slug });
		}

		// GET: /courses/category/{category}
		[Route("/courses/category/{category}")]
		[HttpGet]
		public IActionResult Category(
			string category,
			int page = 1,
			int pageSize = 3,
			decimal? minRating = null,
			decimal? maxRating = null,
			bool? isFree = null,
			string? sortBy = null)
		{
			_logger.LogInformation($"Course Category - Category: {category}, Page: {page}");

			if (string.IsNullOrWhiteSpace(category))
			{
				return RedirectToAction(nameof(Index));
			}

			var courses = _courseService.GetFilteredAndSortedCourses(
				searchKeyword: null,
				categorySlug: category,
				minRating: minRating,
				maxRating: maxRating,
				isFree: isFree,
				sortBy: sortBy);

			// Get category name for display
			var categoryEntity = _courseService.GetAllCategories()
				.FirstOrDefault(c => c.Slug == category);

			ViewBag.CategorySlug = category;
			ViewBag.CategoryName = categoryEntity?.Name;
			ViewBag.MinRating = minRating;
			ViewBag.MaxRating = maxRating;
			ViewBag.IsFree = isFree;
			ViewBag.SortBy = sortBy;

			var totalCount = courses.Count;
			var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
			var pagedCourses = courses.Skip((page - 1) * pageSize).Take(pageSize).ToList();

			ViewBag.CurrentPage = page;
			ViewBag.TotalPages = totalPages;
			ViewBag.PageSize = pageSize;
			ViewBag.TotalCount = totalCount;

			return View("Index", pagedCourses);
		}

		// GET: /courses/{slug}
		[Route("/courses/{slug}")]
		[HttpGet]
		public IActionResult Detail(string slug)
		{
			_logger.LogInformation($"Course Detail - Slug: {slug}");
			if (string.IsNullOrWhiteSpace(slug))
				return RedirectToAction(nameof(Index));

			var course = _courseService.GetCourseBySlugWithFullDetails(slug);
			if (course == null)
			{
				_logger.LogWarning($"Course not found with slug: {slug}");
				return RedirectToAction(nameof(Index));
			}

			var isOwner = false;
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out var userId))
				isOwner = course.OwnerId == userId;

			ViewBag.IsOwner = isOwner;
			return View(course);
		}

		// POST: /courses/{id}/enroll (Future feature)
		[Route("/courses/{id:int}/enroll")]
		[HttpPost]
		public IActionResult Enroll(int id)
		{
			_logger.LogInformation($"Enroll attempt for course ID: {id}");
			TempData["Info"] = "Tính nang dang ký khóa h?c dang du?c phát tri?n!";
			return RedirectToAction(nameof(Detail), new { id });
		}

		// POST: /courses/delete/{id}
		[Authorize]
		[HttpPost]
		[ValidateAntiForgeryToken]
		[Route("/courses/delete/{id:int}")]
		public IActionResult Delete(int id)
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			var ok = _courseService.DeleteCourse(id, userId, _env.WebRootPath);
			if (!ok)
				TempData["Error"] = "Không th? xóa khóa h?c.";
			else
				TempData["Success"] = "Ðã xóa khóa h?c.";

			return RedirectToAction(nameof(My));
		}

		// GET: /courses/builder
		[Authorize]
		[Route("/courses/builder")]
		[HttpGet]
		public IActionResult Builder(int? id)
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			// Load categories for dropdown
			ViewBag.Categories = _courseService.GetAllCategories();

			if (id.HasValue)
			{
				// Edit mode
				var model = _courseService.GetCourseBuilderData(id.Value, userId);
				if (model == null) return NotFound();
				ViewBag.CourseId = id.Value;
				return View("Builder", model);
			}

			// Create mode
			return View("Builder", new CourseBuilderViewModel());
		}

		// POST: /courses/builder/autosave
		[Authorize]
		[Route("/courses/builder/autosave")]
		[HttpPost]
		public IActionResult Autosave([FromBody] CourseAutosaveViewModel model)
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Unauthorized();

			// Proactively return 409 if slug duplicates (excluding current course when editing)
			if (!_courseService.IsSlugUnique(model.Slug, model.CourseId))
			{
				return StatusCode(409, new { success = false, code = "DuplicateSlug", message = "Slug này dã t?n t?i." });
			}

			var success = _courseService.AutosaveCourse(model.CourseId, model, userId);

			return Json(new CourseBuilderResponse
			{
				Success = success,
				Message = success ? "Ðã luu t? d?ng" : "L?i luu t? d?ng"
			});
		}

		// POST: /courses/builder/save
		[Authorize]
		[Route("/courses/builder/save")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SaveBuilder(
			[FromForm] string jsonData,
			IFormFile? coverFile,
			[FromServices] HtmlSanitizer sanitizer)
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			try
			{
				var model = System.Text.Json.JsonSerializer.Deserialize<CourseBuilderViewModel>(
					jsonData,
					new System.Text.Json.JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					}
				);

				if (model == null)
				{
					TempData["Error"] = "D? li?u không h?p l?";
					return RedirectToAction(nameof(Builder));
				}

				// Upload cover image
				if (coverFile is { Length: > 0 })
				{
					var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
					var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
					if (!allowed.Contains(ext))
					{
						TempData["Error"] = "Ð?nh d?ng ?nh không h?p l? (jpg, jpeg, png, gif, webp).";
						return RedirectToAction(nameof(Builder));
					}

					var folder = $"uploads/courses/{DateTime.UtcNow:yyyy/MM}";
					var physical = Path.Combine(_env.WebRootPath, folder);
					Directory.CreateDirectory(physical);

					var fileName = $"{Guid.NewGuid():N}{ext}";
					var fullPath = Path.Combine(physical, fileName);

					await using (var stream = System.IO.File.Create(fullPath))
					{
						await coverFile.CopyToAsync(stream);
					}

					model.CoverUrl = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
				}

				// Sanitize HTML content
				if (!string.IsNullOrEmpty(model.Summary))
					model.Summary = sanitizer.Sanitize(model.Summary);

				foreach (var chapter in model.Chapters)
				{
					if (!string.IsNullOrEmpty(chapter.Description))
						chapter.Description = sanitizer.Sanitize(chapter.Description);

					foreach (var lesson in chapter.Lessons)
					{
						foreach (var content in lesson.Contents)
						{
							if (!string.IsNullOrEmpty(content.Body))
								content.Body = sanitizer.Sanitize(content.Body);
						}
					}
				}

				// Check slug uniqueness
				if (!_courseService.IsSlugUnique(model.Slug))
				{
					TempData["Error"] = "Slug này dã t?n t?i. Vui lòng ch?n slug khác.";
					ViewBag.Categories = _courseService.GetAllCategories();
					return View("Builder", model);
				}

				// Create course with full structure
				var course = _courseService.CreateCourseWithStructure(model, userId);

				if (course == null)
				{
					TempData["Error"] = "Có l?i x?y ra khi t?o khóa h?c";
					ViewBag.Categories = _courseService.GetAllCategories();
					return View("Builder", model);
				}

				TempData["Success"] = "T?o khóa h?c thành công!";
				return RedirectToAction("Detail", new { slug = course.Slug });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error saving course builder");
				TempData["Error"] = "Có l?i x?y ra: " + ex.Message;
				return RedirectToAction(nameof(Builder));
			}
		}

		// POST: /courses/builder/update/{id}
		[Authorize]
		[Route("/courses/builder/update/{id:int}")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> UpdateBuilder(
			int id,
			[FromForm] string jsonData,
			IFormFile? coverFile,
			[FromServices] HtmlSanitizer sanitizer)
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			try
			{
				var model = System.Text.Json.JsonSerializer.Deserialize<CourseBuilderViewModel>(
					jsonData,
					new System.Text.Json.JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					}
				);

				if (model == null)
				{
					TempData["Error"] = "D? li?u không h?p l?";
					return RedirectToAction(nameof(Builder), new { id });
				}

				// Upload cover image
				if (coverFile is { Length: > 0 })
				{
					var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
					var ext = Path.GetExtension(coverFile.FileName).ToLowerInvariant();
					if (!allowed.Contains(ext))
					{
						TempData["Error"] = "Ð?nh d?ng ?nh không h?p l? (jpg, jpeg, png, gif, webp).";
						return RedirectToAction(nameof(Builder), new { id });
					}

					var folder = $"uploads/courses/{DateTime.UtcNow:yyyy/MM}";
					var physical = Path.Combine(_env.WebRootPath, folder);
					Directory.CreateDirectory(physical);

					var fileName = $"{Guid.NewGuid():N}{ext}";
					var fullPath = Path.Combine(physical, fileName);

					await using (var stream = System.IO.File.Create(fullPath))
					{
						await coverFile.CopyToAsync(stream);
					}

					model.CoverUrl = "/" + Path.Combine(folder, fileName).Replace("\\", "/");
				}

				// Sanitize HTML content
				if (!string.IsNullOrEmpty(model.Summary))
					model.Summary = sanitizer.Sanitize(model.Summary);

				foreach (var chapter in model.Chapters)
				{
					if (!string.IsNullOrEmpty(chapter.Description))
						chapter.Description = sanitizer.Sanitize(chapter.Description);

					foreach (var lesson in chapter.Lessons)
					{
						foreach (var content in lesson.Contents)
						{
							if (!string.IsNullOrEmpty(content.Body))
								content.Body = sanitizer.Sanitize(content.Body);
						}
					}
				}

				// Update course structure
				var course = _courseService.UpdateCourseStructure(id, model, userId);

				if (course == null)
				{
					TempData["Error"] = "Không th? c?p nh?t khóa h?c.";
					return RedirectToAction(nameof(Builder), new { id });
				}

				TempData["Success"] = "C?p nh?t khóa h?c thành công!";
				return RedirectToAction("Detail", new { slug = course.Slug });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error updating course builder");
				TempData["Error"] = "Có l?i x?y ra: " + ex.Message;
				return RedirectToAction(nameof(Builder), new { id });
			}
		}

		// NEW: API ki?m tra slug có kh? d?ng không (dùng cho Builder step 1)
		[Authorize]
		[HttpGet]
		[Route("/courses/check-slug")]
		public IActionResult CheckSlug([FromQuery] string slug, [FromQuery] int? excludeId)
		{
			if (string.IsNullOrWhiteSpace(slug))
				return Json(new { available = false, message = "Slug không h?p l?" });

			var available = _courseService.IsSlugUnique(slug, excludeId);
			return Json(new { available });
		}

		// POST: /courses/upload-video - Upload video for lesson content
		[Authorize]
		[Route("/courses/upload-video")]
		[HttpPost]
		[RequestSizeLimit(104_857_600)] // 100MB limit
		[RequestFormLimits(MultipartBodyLengthLimit = 104_857_600)]
		public async Task<IActionResult> UploadVideo(IFormFile video)
		{
			try
			{
				_logger.LogInformation("Video upload request received for Google Cloud Storage");

				if (video == null || video.Length == 0)
				{
					_logger.LogWarning("No video file received");
					return Json(new { success = false, message = "Khng c file no du?c nh?n." });
				}

				_logger.LogInformation($"Uploading video to GCS: {video.FileName}, Size: {video.Length} bytes");

				// Validate file type
				var allowed = new[] { ".mp4", ".webm", ".ogg", ".mov", ".avi", ".mkv" };
				var ext = Path.GetExtension(video.FileName).ToLowerInvariant();
				if (!allowed.Contains(ext))
				{
					_logger.LogWarning($"Invalid file type: {ext}");
					return Json(new { success = false, message = $"D?nh d?ng video khng h?p l?. Ch? ch?p nh?n: {string.Join(", ", allowed)}" });
				}

				// Validate file size (100MB)
				const long maxSize = 104_857_600; // 100MB
				if (video.Length > maxSize)
				{
					_logger.LogWarning($"File too large: {video.Length} bytes");
					return Json(new { success = false, message = "Kch thu?c video khng du?c vu?t qu 100MB." });
				}

				// G?i service ?? upload tr?c ti?p ln GCS trong thu m?c ""uploads/videos""
				var videoUrl = await _storageService.UploadFileAsync(video, "uploads/videos");

				_logger.LogInformation($"Video uploaded to GCS successfully: {videoUrl}");

				return Json(new { success = true, videoUrl = videoUrl });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Video upload to GCS failed");
				return Json(new { success = false, message = $"C l?i x?y ra khi t?i video ln GCS: {ex.Message}" });
			}
		}

		// GET: /courses/{slug}/learn
		[Authorize]
		[Route("/courses/{slug}/learn")]
		[HttpGet]
		public IActionResult Learn(string slug, int? chapterId = null, int? lessonId = null)
		{
			_logger.LogInformation($"Course Learn - Slug: {slug}, ChapterId: {chapterId}, LessonId: {lessonId}");
			
			if (string.IsNullOrWhiteSpace(slug))
				return RedirectToAction(nameof(Index));

			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			// Get course with full structure
			var course = _courseService.GetCourseBySlugWithFullDetails(slug);
			if (course == null)
			{
				_logger.LogWarning($"Course not found with slug: {slug}");
				TempData["Error"] = "Không tìm th?y khóa h?c.";
				return RedirectToAction(nameof(Index));
			}

			// Check if user has access to this course (purchased or is owner)
			var isOwner = course.OwnerId == userId;
			var hasPurchased = course.CoursePurchases?.Any(p => p.BuyerId == userId && p.Status == "Paid") ?? false;

			// ? FIXED: Allow owner to preview even if not published
			if (!isOwner && !hasPurchased)
			{
				TempData["Error"] = "B?n c?n mua khóa h?c này d? xem n?i dung.";
				return RedirectToAction("Detail", new { slug });
			}

			// If no chapters or lessons exist
			if (course.CourseChapters == null || !course.CourseChapters.Any())
			{
				TempData["Error"] = "Khóa h?c này chua có n?i dung.";
				if (isOwner)
				{
					TempData["Info"] = "Hãy thêm chuong và bài h?c vào khóa h?c c?a b?n.";
					return RedirectToAction("Builder", new { id = course.CourseId });
				}
				return RedirectToAction("Detail", new { slug });
			}

			// If no specific lesson is requested, get the first lesson
			if (!chapterId.HasValue || !lessonId.HasValue)
			{
				var firstChapter = course.CourseChapters.OrderBy(c => c.OrderIndex).FirstOrDefault();
				if (firstChapter != null)
				{
					var firstLesson = firstChapter.Lessons?.OrderBy(l => l.OrderIndex).FirstOrDefault();
					if (firstLesson != null)
					{
						return RedirectToAction("Learn", new { slug, chapterId = firstChapter.ChapterId, lessonId = firstLesson.LessonId });
					}
				}
				
				// No lessons found
				TempData["Error"] = "Khóa h?c này chua có bài h?c nào.";
				if (isOwner)
				{
					return RedirectToAction("Builder", new { id = course.CourseId });
				}
				return RedirectToAction("Detail", new { slug });
			}

			// Get current lesson
			var currentChapter = course.CourseChapters.FirstOrDefault(c => c.ChapterId == chapterId);
			var currentLesson = currentChapter?.Lessons?.FirstOrDefault(l => l.LessonId == lessonId);

			if (currentChapter == null || currentLesson == null)
			{
				_logger.LogWarning($"Lesson not found - ChapterId: {chapterId}, LessonId: {lessonId}");
				TempData["Error"] = "Không tìm th?y bài h?c.";
				return RedirectToAction("Detail", new { slug });
			}

			ViewBag.Course = course;
			ViewBag.CurrentChapter = currentChapter;
			ViewBag.CurrentLesson = currentLesson;
			ViewBag.IsOwner = isOwner;

			return View();
		}

		// GET: /courses/revenue - th?ng kê doanh thu t? các khóa h?c c?a ngu?i dùng
		[Authorize]
		[Route("/courses/revenue")]
		[HttpGet]
		public IActionResult Revenue()
		{
			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
			if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
				return Challenge();

			// L?y danh sách khóa h?c c?a ngu?i dùng cùng v?i thông tin mua hàng
			var courses = _context.Courses
				.Include(c => c.CoursePurchases)
				.Where(c => c.OwnerId == userId && c.IsPublished)
				.OrderByDescending(c => c.CreatedAt)
				.ToList();

			// Tính toán doanh thu cho t?ng khóa h?c
			var revenueData = courses.Select(c => new CourseRevenueViewModel
			{
				CourseId = c.CourseId,
				CourseTitle = c.Title,
				CoursePrice = c.Price,
				TotalPurchases = c.CoursePurchases.Count(p => p.Status == "Paid"),
				GrossRevenue = c.Price * c.CoursePurchases.Count(p => p.Status == "Paid"),
				InstructorRevenue = c.Price * c.CoursePurchases.Count(p => p.Status == "Paid") * 0.60m, // 60% cho ngu?i t?o
				PlatformFee = c.Price * c.CoursePurchases.Count(p => p.Status == "Paid") * 0.40m // 40% phí n?n t?ng
			}).ToList();

			// Tính t?ng doanh thu
			ViewBag.TotalGrossRevenue = revenueData.Sum(r => r.GrossRevenue);
			ViewBag.TotalInstructorRevenue = revenueData.Sum(r => r.InstructorRevenue);
			ViewBag.TotalPlatformFee = revenueData.Sum(r => r.PlatformFee);
			ViewBag.TotalPurchases = revenueData.Sum(r => r.TotalPurchases);

			return View(revenueData);
		}
	}
}