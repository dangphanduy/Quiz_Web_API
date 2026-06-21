using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;
using Quiz_Web.Models.ViewModels;
using Quiz_Web.Services.IServices;
using Quiz_Web.Helper;
using Quiz_Web.Utils;
using System.Security.Claims;

namespace Quiz_Web.Controllers.API
{
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly IDashboardService _dashboardService;
        private readonly ILogger<AdminApiController> _logger;

        public AdminApiController(
            LearningPlatformContext context,
            IDashboardService dashboardService,
            ILogger<AdminApiController> logger)
        {
            _context = context;
            _dashboardService = dashboardService;
            _logger = logger;
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

        #region Category Management (Public GET / Admin Write)

        // GET: api/AdminApi/categories
        [AllowAnonymous]
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.CourseCategories
                    .OrderBy(c => c.DisplayOrder)
                    .Select(c => new
                    {
                        categoryId = c.CategoryId,
                        name = c.Name,
                        slug = c.Slug,
                        description = c.Description,
                        iconUrl = c.IconUrl,
                        displayOrder = c.DisplayOrder,
                        createdAt = c.CreatedAt,
                        courseCount = c.Courses.Count
                    })
                    .ToListAsync();

                return Ok(new { success = true, categories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting categories");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tải danh sách danh mục" });
            }
        }

        // POST: api/AdminApi/categories
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] ApiCategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
                {
                    return BadRequest(new { success = false, message = "Tên và Slug danh mục không được để trống" });
                }

                var exists = await _context.CourseCategories.AnyAsync(c => c.Slug == request.Slug.Trim());
                if (exists)
                {
                    return BadRequest(new { success = false, message = "Slug danh mục đã tồn tại" });
                }

                var category = new CourseCategory
                {
                    Name = request.Name.Trim(),
                    Slug = request.Slug.Trim(),
                    Description = request.Description,
                    IconUrl = request.IconUrl,
                    DisplayOrder = request.DisplayOrder,
                    CreatedAt = DateTime.UtcNow
                };

                _context.CourseCategories.Add(category);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, categoryId = category.CategoryId, message = "Tạo danh mục thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error creating category");
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi tạo danh mục" });
            }
        }

        // PUT: api/AdminApi/categories/{id}
        [HttpPut("categories/{id:int}")]
        public async Task<IActionResult> EditCategory(int id, [FromBody] ApiCategoryRequest request)
        {
            try
            {
                var category = await _context.CourseCategories.FindAsync(id);
                if (category == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy danh mục" });
                }

                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
                {
                    return BadRequest(new { success = false, message = "Tên và Slug danh mục không được để trống" });
                }

                var exists = await _context.CourseCategories.AnyAsync(c => c.Slug == request.Slug.Trim() && c.CategoryId != id);
                if (exists)
                {
                    return BadRequest(new { success = false, message = "Slug danh mục đã tồn tại" });
                }

                category.Name = request.Name.Trim();
                category.Slug = request.Slug.Trim();
                category.Description = request.Description;
                category.IconUrl = request.IconUrl;
                category.DisplayOrder = request.DisplayOrder;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Cập nhật danh mục thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error editing category {CategoryId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi cập nhật danh mục" });
            }
        }

        // DELETE: api/AdminApi/categories/{id}
        [HttpDelete("categories/{id:int}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            try
            {
                var category = await _context.CourseCategories
                    .Include(c => c.Courses)
                    .FirstOrDefaultAsync(c => c.CategoryId == id);

                if (category == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy danh mục" });
                }

                if (category.Courses.Any())
                {
                    return BadRequest(new { success = false, message = "Không thể xóa danh mục đang có khóa học" });
                }

                _context.CourseCategories.Remove(category);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa danh mục thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error deleting category {CategoryId}", id);
                return StatusCode(500, new { success = false, message = "Có lỗi xảy ra khi xóa danh mục" });
            }
        }

        #endregion

        #region Dashboard Analytics

        // GET: api/AdminApi/analytics/overview
        [HttpGet("analytics/overview")]
        public IActionResult GetAnalyticsOverview()
        {
            try
            {
                var data = _dashboardService.GetOverviewData();
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting overview analytics");
                return StatusCode(500, new { success = false, message = "Lỗi tải thống kê tổng quan" });
            }
        }

        // GET: api/AdminApi/analytics/users
        [HttpGet("analytics/users")]
        public IActionResult GetAnalyticsUsers()
        {
            try
            {
                var data = _dashboardService.GetUserAnalytics();
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting user analytics");
                return StatusCode(500, new { success = false, message = "Lỗi tải thống kê người dùng" });
            }
        }

        // GET: api/AdminApi/analytics/activities
        [HttpGet("analytics/activities")]
        public IActionResult GetAnalyticsActivities()
        {
            try
            {
                var data = _dashboardService.GetLearningActivities();
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting learning activities analytics");
                return StatusCode(500, new { success = false, message = "Lỗi tải thống kê hoạt động" });
            }
        }

        // GET: api/AdminApi/analytics/revenue
        [HttpGet("analytics/revenue")]
        public IActionResult GetAnalyticsRevenue()
        {
            try
            {
                var data = _dashboardService.GetRevenuePayments();
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting revenue analytics");
                return StatusCode(500, new { success = false, message = "Lỗi tải thống kê doanh thu" });
            }
        }

        // GET: api/AdminApi/analytics/learning-results
        [HttpGet("analytics/learning-results")]
        public IActionResult GetAnalyticsLearningResults()
        {
            try
            {
                var data = _dashboardService.GetLearningResults();
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting learning results analytics");
                return StatusCode(500, new { success = false, message = "Lỗi tải thống kê kết quả học tập" });
            }
        }

        // GET: api/AdminApi/analytics/system-activity
        [HttpGet("analytics/system-activity")]
        public IActionResult GetAnalyticsSystemActivity()
        {
            try
            {
                var data = _dashboardService.GetSystemActivity();
                return Ok(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting system activity analytics");
                return StatusCode(500, new { success = false, message = "Lỗi tải thống kê hệ thống" });
            }
        }

        #endregion

        #region User CRUD

        // GET: api/AdminApi/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new
                    {
                        userId = u.UserId,
                        username = u.Username,
                        email = u.Email,
                        fullName = u.FullName,
                        phone = u.Phone,
                        roleId = u.RoleId,
                        roleName = u.Role != null ? u.Role.Name : "User",
                        status = u.Status,
                        createdAt = u.CreatedAt
                    })
                    .ToListAsync();
                return Ok(new { success = true, users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting user list");
                return StatusCode(500, new { success = false, message = "Lỗi khi lấy danh sách thành viên" });
            }
        }

        // POST: api/AdminApi/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] ApiCreateUserRequest request)
        {
            try
            {
                if (!Validation.IsValidUsername(request.Username))
                {
                    return BadRequest(new { success = false, message = "Username must be between 3 and 100 characters" });
                }

                if (!Validation.IsValidFullName(request.FullName))
                {
                    return BadRequest(new { success = false, message = "Full name is required and cannot exceed 200 characters" });
                }

                if (!Validation.IsValidEmail(request.Email))
                {
                    return BadRequest(new { success = false, message = "Invalid email format" });
                }

                if (!Validation.IsValidPassword(request.Password))
                {
                    return BadRequest(new { success = false, message = "Password must be at least 8 characters with uppercase, lowercase, number and special character" });
                }

                if (!Validation.IsValidPhone(request.Phone))
                {
                    return BadRequest(new { success = false, message = "Invalid phone number format" });
                }

                if (await _context.Users.AnyAsync(u => u.Username == request.Username.Trim().ToLower()))
                {
                    return BadRequest(new { success = false, message = "Username already exists" });
                }

                if (await _context.Users.AnyAsync(u => u.Email == request.Email.Trim().ToLower()))
                {
                    return BadRequest(new { success = false, message = "Email already exists" });
                }

                var user = new User
                {
                    Username = request.Username.ToLower().Trim(),
                    FullName = request.FullName.Trim(),
                    Email = request.Email.ToLower().Trim(),
                    Phone = request.Phone,
                    PasswordHash = HashHelper.ComputeHash(request.Password),
                    RoleId = request.RoleId,
                    Status = 1,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, userId = user.UserId, message = "User created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error creating user");
                return StatusCode(500, new { success = false, message = "Lỗi khi tạo thành viên" });
            }
        }

        // PUT: api/AdminApi/users/{id}
        [HttpPut("users/{id:int}")]
        public async Task<IActionResult> EditUser(int id, [FromBody] ApiEditUserRequest request)
        {
            try
            {
                var existingUser = await _context.Users.FindAsync(id);
                if (existingUser == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                if (!Validation.IsValidUsername(request.Username))
                {
                    return BadRequest(new { success = false, message = "Username must be between 3 and 100 characters" });
                }

                if (!Validation.IsValidFullName(request.FullName))
                {
                    return BadRequest(new { success = false, message = "Full name is required and cannot exceed 200 characters" });
                }

                if (!Validation.IsValidEmail(request.Email))
                {
                    return BadRequest(new { success = false, message = "Invalid email format" });
                }

                if (!Validation.IsValidPhone(request.Phone))
                {
                    return BadRequest(new { success = false, message = "Invalid phone number format" });
                }

                if (await _context.Users.AnyAsync(u => u.Username == request.Username.Trim().ToLower() && u.UserId != id))
                {
                    return BadRequest(new { success = false, message = "Username already exists" });
                }

                existingUser.Username = request.Username.ToLower().Trim();
                existingUser.FullName = request.FullName.Trim();
                existingUser.Email = request.Email.ToLower().Trim();
                existingUser.Phone = request.Phone;
                existingUser.RoleId = request.RoleId;
                existingUser.Status = request.Status;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error editing user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi cập nhật thông tin" });
            }
        }

        // DELETE: api/AdminApi/users/{id}
        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error deleting user {UserId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa thành viên" });
            }
        }

        #endregion

        #region Course CRUD

        // GET: api/AdminApi/courses
        [HttpGet("courses")]
        public async Task<IActionResult> GetCourses()
        {
            try
            {
                var courses = await _context.Courses
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new
                    {
                        courseId = c.CourseId,
                        title = c.Title,
                        slug = c.Slug,
                        summary = c.Summary,
                        price = c.Price,
                        coverUrl = c.CoverUrl,
                        isPublished = c.IsPublished,
                        createdAt = c.CreatedAt,
                        ownerName = c.Owner != null ? c.Owner.FullName : "Unknown"
                    })
                    .ToListAsync();
                return Ok(new { success = true, courses });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting courses");
                return StatusCode(500, new { success = false, message = "Lỗi khi lấy danh sách khóa học" });
            }
        }

        // POST: api/AdminApi/courses
        [HttpPost("courses")]
        public async Task<IActionResult> CreateCourse([FromBody] ApiCreateCourseRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
                {
                    return BadRequest(new { success = false, message = "Title is required and cannot exceed 200 characters" });
                }

                if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length > 100)
                {
                    return BadRequest(new { success = false, message = "Slug is required and cannot exceed 100 characters" });
                }

                if (request.Price < 0)
                {
                    return BadRequest(new { success = false, message = "Price must be a positive number" });
                }

                if (await _context.Courses.AnyAsync(c => c.Slug == request.Slug.ToLower().Trim()))
                {
                    return BadRequest(new { success = false, message = "Slug already exists" });
                }

                var course = new Course
                {
                    Title = request.Title.Trim(),
                    Slug = request.Slug.ToLower().Trim(),
                    Summary = request.Summary?.Trim(),
                    Price = request.Price,
                    CoverUrl = request.CoverUrl,
                    CategoryId = request.CategoryId,
                    OwnerId = GetCurrentUserId(),
                    CreatedAt = DateTime.UtcNow,
                    IsPublished = request.IsPublished
                };

                _context.Courses.Add(course);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, courseId = course.CourseId, message = "Course created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error creating course");
                return StatusCode(500, new { success = false, message = "Lỗi khi tạo khóa học" });
            }
        }

        // PUT: api/AdminApi/courses/{id}
        [HttpPut("courses/{id:int}")]
        public async Task<IActionResult> EditCourse(int id, [FromBody] ApiEditCourseRequest request)
        {
            try
            {
                var existingCourse = await _context.Courses.FindAsync(id);
                if (existingCourse == null)
                {
                    return NotFound(new { success = false, message = "Course not found" });
                }

                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
                {
                    return BadRequest(new { success = false, message = "Title is required and cannot exceed 200 characters" });
                }

                if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length > 100)
                {
                    return BadRequest(new { success = false, message = "Slug is required and cannot exceed 100 characters" });
                }

                if (request.Price < 0)
                {
                    return BadRequest(new { success = false, message = "Price must be a positive number" });
                }

                if (await _context.Courses.AnyAsync(c => c.Slug == request.Slug.ToLower().Trim() && c.CourseId != id))
                {
                    return BadRequest(new { success = false, message = "Slug already exists" });
                }

                existingCourse.Title = request.Title.Trim();
                existingCourse.Slug = request.Slug.ToLower().Trim();
                existingCourse.Summary = request.Summary?.Trim();
                existingCourse.Price = request.Price;
                existingCourse.CoverUrl = request.CoverUrl;
                existingCourse.CategoryId = request.CategoryId;
                existingCourse.IsPublished = request.IsPublished;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Course updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error editing course {CourseId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi cập nhật khóa học" });
            }
        }

        // DELETE: api/AdminApi/courses/{id}
        [HttpDelete("courses/{id:int}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            try
            {
                var course = await _context.Courses.FindAsync(id);
                if (course == null)
                {
                    return NotFound(new { success = false, message = "Course not found" });
                }

                _context.Courses.Remove(course);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Course deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error deleting course {CourseId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa khóa học" });
            }
        }

        #endregion

        #region Test & Question CRUD

        // GET: api/AdminApi/tests
        [HttpGet("tests")]
        public async Task<IActionResult> GetTests()
        {
            try
            {
                var tests = await _context.Tests
                    .Where(t => !t.IsDeleted)
                    .OrderByDescending(t => t.CreatedAt)
                    .Select(t => new
                    {
                        testId = t.TestId,
                        title = t.Title,
                        description = t.Description,
                        timeLimitSec = t.TimeLimitSec,
                        maxAttempts = t.MaxAttempts,
                        visibility = t.Visibility,
                        gradingMode = t.GradingMode,
                        createdAt = t.CreatedAt,
                        questionCount = t.Questions.Count
                    })
                    .ToListAsync();
                return Ok(new { success = true, tests });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting tests");
                return StatusCode(500, new { success = false, message = "Lỗi khi lấy danh sách bài thi" });
            }
        }

        // GET: api/AdminApi/tests/{id}
        [HttpGet("tests/{id:int}")]
        public async Task<IActionResult> GetTestDetails(int id)
        {
            try
            {
                var test = await _context.Tests
                    .Include(t => t.Questions.OrderBy(q => q.OrderIndex))
                        .ThenInclude(q => q.QuestionOptions.OrderBy(o => o.OrderIndex))
                    .FirstOrDefaultAsync(t => t.TestId == id && !t.IsDeleted);

                if (test == null)
                {
                    return NotFound(new { success = false, message = "Test not found" });
                }

                var testDetail = new
                {
                    testId = test.TestId,
                    title = test.Title,
                    description = test.Description,
                    timeLimitSec = test.TimeLimitSec,
                    maxAttempts = test.MaxAttempts,
                    visibility = test.Visibility,
                    gradingMode = test.GradingMode,
                    questions = test.Questions.Select(q => new
                    {
                        questionId = q.QuestionId,
                        stemText = q.StemText,
                        points = q.Points,
                        type = q.Type,
                        orderIndex = q.OrderIndex,
                        options = q.QuestionOptions.Select(o => new
                        {
                            optionId = o.OptionId,
                            optionText = o.OptionText,
                            isCorrect = o.IsCorrect,
                            orderIndex = o.OrderIndex
                        }).ToList()
                    }).ToList()
                };

                return Ok(new { success = true, test = testDetail });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error getting test details for {TestId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi tải chi tiết bài thi" });
            }
        }

        // POST: api/AdminApi/tests
        [HttpPost("tests")]
        public async Task<IActionResult> CreateTest([FromBody] ApiCreateTestRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
                {
                    return BadRequest(new { success = false, message = "Title is required and cannot exceed 200 characters" });
                }

                if (request.TimeLimitSec.HasValue && (request.TimeLimitSec < 60 || request.TimeLimitSec > 86400))
                {
                    return BadRequest(new { success = false, message = "Time limit must be between 1 minute (60s) and 24 hours (86400s)" });
                }

                if (request.MaxAttempts < 1 || request.MaxAttempts > 10)
                {
                    return BadRequest(new { success = false, message = "Max attempts must be between 1 and 10" });
                }

                var test = new Test
                {
                    Title = request.Title.Trim(),
                    Description = request.Description?.Trim(),
                    TimeLimitSec = request.TimeLimitSec,
                    Visibility = request.Visibility ?? "private",
                    GradingMode = request.GradingMode ?? "auto",
                    MaxAttempts = request.MaxAttempts,
                    OwnerId = GetCurrentUserId(),
                    CreatedAt = DateTime.UtcNow
                };

                _context.Tests.Add(test);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, testId = test.TestId, message = "Test created successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error creating test");
                return StatusCode(500, new { success = false, message = "Lỗi khi tạo bài thi" });
            }
        }

        // PUT: api/AdminApi/tests/{id}
        [HttpPut("tests/{id:int}")]
        public async Task<IActionResult> EditTest(int id, [FromBody] ApiCreateTestRequest request)
        {
            try
            {
                var test = await _context.Tests.FindAsync(id);
                if (test == null || test.IsDeleted)
                {
                    return NotFound(new { success = false, message = "Test not found" });
                }

                if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
                {
                    return BadRequest(new { success = false, message = "Title is required and cannot exceed 200 characters" });
                }

                if (request.TimeLimitSec.HasValue && (request.TimeLimitSec < 60 || request.TimeLimitSec > 86400))
                {
                    return BadRequest(new { success = false, message = "Time limit must be between 1 minute (60s) and 24 hours (86400s)" });
                }

                if (request.MaxAttempts < 1 || request.MaxAttempts > 10)
                {
                    return BadRequest(new { success = false, message = "Max attempts must be between 1 and 10" });
                }

                test.Title = request.Title.Trim();
                test.Description = request.Description?.Trim();
                test.TimeLimitSec = request.TimeLimitSec;
                test.Visibility = request.Visibility ?? "private";
                test.GradingMode = request.GradingMode ?? "auto";
                test.MaxAttempts = request.MaxAttempts;

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Test updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error editing test {TestId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi cập nhật bài thi" });
            }
        }

        // DELETE: api/AdminApi/tests/{id}
        [HttpDelete("tests/{id:int}")]
        public async Task<IActionResult> DeleteTest(int id)
        {
            try
            {
                var test = await _context.Tests.FindAsync(id);
                if (test == null || test.IsDeleted)
                {
                    return NotFound(new { success = false, message = "Test not found" });
                }

                test.IsDeleted = true; // Soft delete
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Test deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error deleting test {TestId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa bài thi" });
            }
        }

        // POST: api/AdminApi/tests/{id}/questions
        [HttpPost("tests/{id:int}/questions")]
        public async Task<IActionResult> AddQuestion(int id, [FromBody] ApiAddQuestionRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.StemText))
                {
                    return BadRequest(new { success = false, message = "Question text is required" });
                }

                if (!Validation.IsValidPoints(request.Points))
                {
                    return BadRequest(new { success = false, message = "Points must be between 0.1 and 100" });
                }

                if (request.Type == "multiple_choice" && (request.OptionTexts == null || !request.OptionTexts.Any(o => !string.IsNullOrWhiteSpace(o))))
                {
                    return BadRequest(new { success = false, message = "At least one option is required for multiple choice questions" });
                }

                var question = new Question
                {
                    TestId = id,
                    StemText = request.StemText.Trim(),
                    Points = request.Points,
                    Type = request.Type,
                    OrderIndex = await _context.Questions.Where(q => q.TestId == id).CountAsync() + 1
                };

                _context.Questions.Add(question);
                await _context.SaveChangesAsync();

                // Add options
                if (request.Type == "multiple_choice" && request.OptionTexts != null)
                {
                    for (int i = 0; i < request.OptionTexts.Length; i++)
                    {
                        var optText = request.OptionTexts[i];
                        if (string.IsNullOrWhiteSpace(optText)) continue;

                        var option = new QuestionOption
                        {
                            QuestionId = question.QuestionId,
                            OptionText = optText.Trim(),
                            IsCorrect = i == request.CorrectOption,
                            OrderIndex = i + 1
                        };
                        _context.QuestionOptions.Add(option);
                    }
                    await _context.SaveChangesAsync();
                }

                return Ok(new { success = true, questionId = question.QuestionId, message = "Question added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error adding question to test {TestId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi thêm câu hỏi" });
            }
        }

        // DELETE: api/AdminApi/tests/questions/{id}
        [HttpDelete("tests/questions/{id:int}")]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            try
            {
                var question = await _context.Questions.Include(q => q.QuestionOptions).FirstOrDefaultAsync(q => q.QuestionId == id);
                if (question == null)
                {
                    return NotFound(new { success = false, message = "Question not found" });
                }

                _context.QuestionOptions.RemoveRange(question.QuestionOptions);
                _context.Questions.Remove(question);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Question deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API error deleting question {QuestionId}", id);
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa câu hỏi" });
            }
        }

        #endregion


    }

    #region Request DTOs

    public class ApiCreateUserRequest
    {
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Phone { get; set; }
        public int RoleId { get; set; }
    }

    public class ApiEditUserRequest
    {
        public string Username { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? Phone { get; set; }
        public int RoleId { get; set; }
        public int Status { get; set; }
    }

    public class ApiCreateCourseRequest
    {
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }
        public decimal Price { get; set; }
        public string? CoverUrl { get; set; }
        public int? CategoryId { get; set; }
        public bool IsPublished { get; set; }
    }

    public class ApiEditCourseRequest
    {
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }
        public decimal Price { get; set; }
        public string? CoverUrl { get; set; }
        public int? CategoryId { get; set; }
        public bool IsPublished { get; set; }
    }

    public class ApiCategoryRequest
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ApiCreateTestRequest
    {
        public string Title { get; set; } = null!;
        public string? Description { get; set; }
        public int? TimeLimitSec { get; set; }
        public int? MaxAttempts { get; set; }
        public string? Visibility { get; set; }
        public string? GradingMode { get; set; }
    }

    public class ApiAddQuestionRequest
    {
        public string StemText { get; set; } = null!;
        public decimal Points { get; set; }
        public string Type { get; set; } = null!;
        public string[]? OptionTexts { get; set; }
        public int CorrectOption { get; set; }
    }



    #endregion
}
