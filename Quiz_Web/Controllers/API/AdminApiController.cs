using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quiz_Web.Models.EF;
using Quiz_Web.Models.Entities;

namespace Quiz_Web.Controllers.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminApiController : ControllerBase
    {
        private readonly LearningPlatformContext _context;
        private readonly ILogger<AdminApiController> _logger;

        public AdminApiController(LearningPlatformContext context, ILogger<AdminApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/AdminApi/categories
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
        [Authorize(Roles = "Admin")]
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory([FromBody] ApiCategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Slug))
                {
                    return BadRequest(new { success = false, message = "Tên và Slug danh mục không được để trống" });
                }

                // Check slug unique
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
        [Authorize(Roles = "Admin")]
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

                // Check slug unique
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
        [Authorize(Roles = "Admin")]
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
    }

    public class ApiCategoryRequest
    {
        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
        public int DisplayOrder { get; set; }
    }
}
